"""Synthetic workflow-contract checks; these fixtures never invoke Stryker."""

from __future__ import annotations

import contextlib
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

import c5_workflow as workflow
import c5_projection as projection


COMMIT = "1" * 40
TREE = "2" * 40
RUN_ID = "12345"
ATTEMPT = "2"
STATUSES = ("Killed", "Timeout", "Survived", "NoCoverage", "CompileError", "RuntimeError", "Ignored")
SPAN_END = 2147483647
SOURCE = "".join(f"    void M{i}()\n    {{\n    }}\n" for i in range(8)).encode()
FILTERED = {"status": "Ignored", "statusReason": "Removed by mutate filter"}
REPEAT = "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs"
CONFIGURATION = "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def mutant(identifier, status):
    return {
        "id": str(identifier), "mutatorName": "Synthetic mutation", "replacement": "false",
        "status": status, "location": {"start": {"line": 1, "column": 1},
                                         "end": {"line": 1, "column": 2}},
    }


class AggregateFixture:
    def __init__(self, directory, populations=None):
        self.input = directory / "input"
        self.output = directory / "summary"
        self.input.mkdir()
        self.paths = {}
        self.receipts = {}
        self.reports = {}
        populations = populations or [["Killed"] for _ in workflow.TARGETS]
        for shard, target in workflow.SHARDS.items():
            artifact = self.input / f"c5-mutation-{RUN_ID}-{ATTEMPT}-{shard}"
            artifact.mkdir()
            self.paths[shard] = artifact
            write_json(artifact / "shard-config.json", workflow.shard_config(shard, SOURCE))
            (artifact / "command.log").write_bytes(b"synthetic output\n")
            report = {
                "schemaVersion": "2", "projectRoot": "/synthetic/src/Parchwell.Verifier",
                "thresholds": {"high": 80, "low": 60},
                "files": {
                    "/synthetic/src/Parchwell.Verifier/" + target: {
                        "language": "cs", "source": SOURCE.decode(), "mutants": [],
                    },
                    "/synthetic/src/Parchwell.Verifier/Other.cs": {
                        "language": "none", "source": "File ignored by mutate filter", "mutants": [],
                    },
                },
            }
            self.reports[shard] = report
            original = {projection.VERIFIER + name: SOURCE for name in workflow.SHARDS.values()}
            original["tests/Parchwell.Verifier.Tests/SyntheticTests.cs"] = b"synthetic test source"
            source = {name: projection.digest(data) for name, data in original.items()}
            disabled = sorted(name for name in original if name.startswith(projection.VERIFIER) and name != projection.VERIFIER + target)
            transformed = {name: projection.prefixed(data) if name in disabled else data for name, data in original.items()}
            projected = {name: projection.digest(data) for name, data in transformed.items()}
            for name, data in transformed.items():
                if name.startswith(projection.VERIFIER):
                    path = artifact / "projection-src" / name
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(data)
            write_json(artifact / "projection.json", {
                "policy": projection.POLICY, "target": target, "disabled": disabled,
                "original_sha256": source, "before_sha256": projected, "after_sha256": projected,
            })
            self.receipts[shard] = {
                "shard": shard, "target": target, "commit": COMMIT, "tree": TREE,
                "run_id": RUN_ID, "run_attempt": ATTEMPT,
                "argv": ["dotnet", "stryker", "--config-file", str(artifact / "shard-config.json"),
                         "--output", str(artifact / "stryker"), "--log-to-file", "--verbosity", "debug"],
                "cwd": "/synthetic/tests/Parchwell.Verifier.Tests",
                "environment_overrides": {"ParchwellVerifierMutationOnly": "true"},
                "started_at_utc": "2026-10-25T18:00:00+00:00",
                "finished_at_utc": "2026-10-25T18:00:01+00:00", "elapsed_seconds": 1.0,
                "child_exit_code": 0,
                "termination_reason": None, "command_completed": True,
                "source_sha256_before": dict(source), "source_sha256_after": dict(source),
                "tracked_clean_before": True, "tracked_clean_after": True,
                "config_sha256_before": None, "config_sha256_after": None,
                "report_sha256": None, "command_log_sha256": None,
                "projection_manifest_sha256": digest(artifact / "projection.json"),
            }
            self.save(shard)
        for (key, _), population in zip(workflow.TARGETS.items(), populations, strict=True):
            self.add_owned(key, [mutant(i, status) for i, status in enumerate(population)])
            for shard in self.members(key):
                self.receipts[shard]["child_exit_code"] = 0 if "Killed" in population else 2
                self.save(shard)

    def members(self, key):
        return [shard for shard, (part_key, _, _) in workflow.PARTS.items() if part_key == key]

    def add_owned(self, key, rows):
        members = self.members(key)
        for number, row in enumerate(rows):
            for index, shard in enumerate(members):
                self.target_rows(shard).append(copy.deepcopy(row) if index == number % len(members)
                                               else {**row, **FILTERED})
        for shard in members:
            self.save(shard)

    def add_shared(self, key, rows):
        for shard in self.members(key):
            self.target_rows(shard).extend(copy.deepcopy(row) for row in rows)
            self.save(shard)

    def target_rows(self, shard):
        target = workflow.SHARDS[shard]
        return self.reports[shard]["files"]["/synthetic/src/Parchwell.Verifier/" + target]["mutants"]

    def save(self, shard):
        artifact = self.paths[shard]
        report_path = artifact / "stryker/reports/mutation-report.json"
        write_json(report_path, self.reports[shard])
        receipt = self.receipts[shard]
        receipt["config_sha256_before"] = digest(artifact / "shard-config.json")
        receipt["config_sha256_after"] = receipt["config_sha256_before"]
        receipt["report_sha256"] = digest(report_path)
        receipt["command_log_sha256"] = digest(artifact / "command.log")
        write_json(artifact / "receipt.json", receipt)

    def invoke(self, **overrides):
        values = {"expected_commit": COMMIT, "run_id": RUN_ID, "run_attempt": ATTEMPT}
        values.update(overrides)
        result = workflow.aggregate(self.input, values["expected_commit"], values["run_id"],
                                    values["run_attempt"], self.output)
        summary = json.loads((self.output / "summary.json").read_text())
        return result, summary



class CommittedContractTests(unittest.TestCase):
    def test_literal_targets_parts_and_shards_match_code_config_and_workflow(self):
        targets = {
            "phase": ("Judge/PhaseJudge.cs", 1),
            "publication": ("VerifyRun/Publication/PublicationAcceptance.cs", 1),
            "a01": ("VerifyRun/A01/A01EvidenceRecorder.cs", 1),
            "workload": ("VerifyRun/A01/WorkloadRecorder.cs", 1),
            "single-action-repeat": (REPEAT, 3),
            "single-action-configuration": (CONFIGURATION, 6),
        }
        shards = {
            "phase": "Judge/PhaseJudge.cs",
            "publication": "VerifyRun/Publication/PublicationAcceptance.cs",
            "a01": "VerifyRun/A01/A01EvidenceRecorder.cs",
            "workload": "VerifyRun/A01/WorkloadRecorder.cs",
            "single-action-repeat-p1": REPEAT,
            "single-action-repeat-p2": REPEAT,
            "single-action-repeat-p3": REPEAT,
            "single-action-configuration-p1": CONFIGURATION,
            "single-action-configuration-p2": CONFIGURATION,
            "single-action-configuration-p3": CONFIGURATION,
            "single-action-configuration-p4": CONFIGURATION,
            "single-action-configuration-p5": CONFIGURATION,
            "single-action-configuration-p6": CONFIGURATION,
        }
        root = Path(__file__).resolve().parents[3]
        self.assertEqual(targets, workflow.TARGETS)
        self.assertEqual(shards, workflow.SHARDS)
        self.assertEqual([target for target, _ in targets.values()], workflow.BASE_CONFIG["stryker-config"]["mutate"])
        committed = json.loads((root / "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json").read_text())
        self.assertEqual(workflow.BASE_CONFIG, committed)
        # PyYAML is supplied by the pinned lifecycle tooling; this checks actual matrix data.
        import yaml
        matrix = yaml.safe_load((root / ".github/workflows/verifier-c5-mutation.yml").read_text())["jobs"]["mutation"]["strategy"]["matrix"]["include"]
        self.assertEqual(list(shards.items()), [(row["shard"], row["target"]) for row in matrix])
        self.assertEqual(13, len(matrix))

    def test_workflow_keeps_stryker_logs_out_of_aggregate_input(self):
        import yaml
        root = Path(__file__).resolve().parents[3]
        jobs = yaml.safe_load((root / ".github/workflows/verifier-c5-mutation.yml").read_text())["jobs"]
        mutation_steps = jobs["mutation"]["steps"]
        steps = {step["name"]: step for step in mutation_steps}
        upload = "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02"
        evidence, logs = steps["Retain shard evidence"], steps["Retain shard Stryker logs"]
        self.assertEqual({
            "name": "c5-mutation-${{ github.run_id }}-${{ github.run_attempt }}-${{ matrix.shard }}",
            "path": "${{ env.C5_OUTPUT }}\n!${{ env.C5_OUTPUT }}/stryker/logs/**\n",
            "if-no-files-found": "error", "retention-days": 90}, evidence["with"])
        self.assertEqual({
            "name": "c5-logs-${{ github.run_id }}-${{ github.run_attempt }}-${{ matrix.shard }}",
            "path": "${{ env.C5_OUTPUT }}/stryker/logs",
            "if-no-files-found": "ignore", "retention-days": 90}, logs["with"])
        for step in (evidence, logs):
            self.assertEqual("always()", step["if"])
            self.assertEqual(upload, step["uses"])
        self.assertEqual(mutation_steps.index(evidence) + 1, mutation_steps.index(logs))
        download = {step["name"]: step for step in jobs["aggregate"]["steps"]}["Download shard evidence"]
        self.assertEqual("c5-mutation-${{ github.run_id }}-${{ github.run_attempt }}-*", download["with"]["pattern"])

    def test_committed_split_targets_have_enough_member_boundaries(self):
        root = Path(__file__).resolve().parents[3]
        for key, (target, count) in workflow.TARGETS.items():
            with self.subTest(target=key):
                text = workflow.target_text((root / "src/Parchwell.Verifier" / target).read_bytes())
                selected = workflow.spans(text, count)
                self.assertEqual(count, len(selected))
                self.assertEqual(0, selected[0][0])
                self.assertEqual(SPAN_END, selected[-1][1])


class SpanTests(unittest.TestCase):
    TEXT = "namespace N;\n\npublic sealed class C\n{\n    void A()\n    {\n    }\n\n    void B()\n    {\n    }\n}\n"

    def test_member_boundaries_follow_exact_member_closing_lines(self):
        self.assertEqual([(63, 8), (89, 12)], workflow.member_boundaries(self.TEXT))
        self.assertEqual([], workflow.member_boundaries("     }\n\t}\n    } \n   }\n    };\nx\n"))
        self.assertEqual([(7, 2)], workflow.member_boundaries("    }\r\nx\r\n"))
        self.assertEqual([], workflow.member_boundaries("    }\n"))
        self.assertEqual([], workflow.member_boundaries(""))

    def test_span_count_below_at_and_above_boundary_limit(self):
        self.assertEqual([(0, SPAN_END)], workflow.spans(self.TEXT, 1))
        self.assertEqual([(0, 63), (63, SPAN_END)], workflow.spans(self.TEXT, 2))
        self.assertEqual([(0, 63), (63, 89), (89, SPAN_END)], workflow.spans(self.TEXT, 3))
        with self.assertRaisesRegex(ValueError, "too few member boundaries for span count"):
            workflow.spans(self.TEXT, 4)
        with self.assertRaisesRegex(ValueError, "too few member boundaries for span count"):
            workflow.spans("", 2)
        self.assertEqual([(0, SPAN_END)], workflow.spans("", 1))
        for count in (0, -1, True, "2", 1.0):
            with self.subTest(count=count), self.assertRaisesRegex(ValueError, "span count must be a positive integer"):
                workflow.spans(self.TEXT, count)

    def test_spans_minimize_the_longest_span_and_break_ties_low(self):
        self.assertEqual([(0, 6), (6, SPAN_END)], workflow.spans("    }\n    }\nabcde\n", 2))
        self.assertEqual([(0, 52), (52, 130), (130, SPAN_END)], workflow.spans(SOURCE.decode(), 3))
        self.assertEqual([(0, 26), (26, 52), (52, 78), (78, 104), (104, 156), (156, SPAN_END)],
                         workflow.spans(SOURCE.decode(), 6))

    def test_offsets_are_utf16_code_units_after_bom_removal(self):
        text = "\U0001F600\n    }\nx\n"
        self.assertEqual([(9, 3)], workflow.member_boundaries(text))
        self.assertEqual([(0, 9), (9, SPAN_END)], workflow.spans(text, 2))
        stripped = workflow.target_text(b"\xef\xbb\xbf    }\nx\n")
        self.assertEqual("    }\nx\n", stripped)
        self.assertEqual([(6, 2)], workflow.member_boundaries(stripped))
        with self.assertRaises(ValueError):
            workflow.target_text(b"\xff")

    def test_target_length_limit(self):
        with patch.object(workflow, "SPAN_END", 92):
            self.assertEqual([(0, 92)], workflow.spans(self.TEXT, 1))
        with patch.object(workflow, "SPAN_END", 91), self.assertRaisesRegex(ValueError, "target too long for span selection"):
            workflow.spans(self.TEXT, 1)

    def test_span_shard_configs(self):
        expected = {
            "single-action-repeat-p1": REPEAT + "{0..52}",
            "single-action-repeat-p2": REPEAT + "{52..130}",
            "single-action-repeat-p3": REPEAT + "{130..2147483647}",
            "single-action-configuration-p1": CONFIGURATION + "{0..26}",
            "single-action-configuration-p2": CONFIGURATION + "{26..52}",
            "single-action-configuration-p3": CONFIGURATION + "{52..78}",
            "single-action-configuration-p4": CONFIGURATION + "{78..104}",
            "single-action-configuration-p5": CONFIGURATION + "{104..156}",
            "single-action-configuration-p6": CONFIGURATION + "{156..2147483647}",
        }
        for shard in workflow.PARTS:
            with self.subTest(shard=shard):
                config = workflow.shard_config(shard, SOURCE)
                self.assertEqual([expected.get(shard, workflow.SHARDS[shard])], config["stryker-config"]["mutate"])
                config["stryker-config"]["mutate"] = workflow.BASE_CONFIG["stryker-config"]["mutate"]
                self.assertEqual(workflow.BASE_CONFIG, config)
        self.assertEqual(workflow.shard_config("phase"), workflow.shard_config("phase", b"ignored"))
        with self.assertRaisesRegex(ValueError, "span shard requires target source"):
            workflow.shard_config("single-action-repeat-p1")
        with self.assertRaisesRegex(ValueError, "too few member boundaries for span count"):
            workflow.shard_config("single-action-repeat-p1", b"if (value) return true;")


def row(identifier, status, reason=None, **changes):
    value = {**mutant(identifier, status), **changes}
    if reason is not None:
        value["statusReason"] = reason
    return value


def filtered(identifier, **changes):
    return {**row(identifier, "Ignored", **changes), **FILTERED}


class ReconcileTests(unittest.TestCase):
    def test_single_part_passes_records_through(self):
        rows = [row(1, "Killed"), filtered(2)]
        self.assertEqual(([rows], rows), workflow.reconcile([rows]))

    def test_each_mutant_is_taken_from_its_one_owner(self):
        first = [row(1, "Killed"), filtered(2), row(3, "Ignored", "Removed by block already covered filter")]
        second = [filtered(1), row(2, "Survived"), filtered(3)]
        owned, population = workflow.reconcile([first, second])
        self.assertEqual([["1", "3"], ["2"]], [[record["id"] for record in rows] for rows in owned])
        self.assertEqual([("1", "Killed"), ("2", "Survived"), ("3", "Ignored")],
                         [(record["id"], record["status"]) for record in population])

    def test_identical_compile_errors_and_early_ignores_count_once(self):
        def part():
            return [row(1, "CompileError"), row(2, "Ignored", "Removed by ignore mutation filter")]
        owned, population = workflow.reconcile([part(), part()])
        self.assertEqual([[], []], owned)
        self.assertEqual(part(), population)

    def test_gaps_overlaps_and_inconsistent_inventories_fail(self):
        block = "Removed by block already covered filter"
        location = {"start": {"line": 2, "column": 1}, "end": {"line": 2, "column": 2}}
        cases = {
            "has no owning span": [[filtered(1)], [filtered(1)]],
            "more than one owning span": [[row(1, "Killed")], [row(1, "Killed")]],
            "more than one owning span ": [[row(1, "Killed")], [row(1, "Ignored", block)]],
            "compile error differs across spans": [[row(1, "CompileError")], [filtered(1)]],
            "compile error differs across spans ": [[row(1, "CompileError", "a")], [row(1, "CompileError", "b")]],
            "span inventories differ": [[row(1, "Killed")], [row(1, "Killed"), filtered(2)]],
            "span inventories differ ": [[row(1, "Killed")], [filtered(2)]],
            "identity differs across spans": [[row(1, "Killed")], [filtered(1, replacement="true")]],
            "identity differs across spans ": [[row(1, "Killed")], [filtered(1, mutatorName="Other")]],
            "identity differs across spans  ": [[row(1, "Killed")], [filtered(1, location=location)]],
        }
        for message, parts in cases.items():
            with self.subTest(message=message, parts=parts), self.assertRaisesRegex(ValueError, message.strip()):
                workflow.reconcile(parts)
        self.assertEqual(([[], []], []), workflow.reconcile([[], []]))


class AggregationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)

    def assert_incomplete(self, fixture, **overrides):
        result, summary = fixture.invoke(**overrides)
        self.assertEqual(1, result)
        self.assertIs(summary["measurement_complete"], False)
        self.assertIsNone(summary["break_passed"])
        self.assertTrue(summary["incomplete_reasons"])
        self.assertTrue((fixture.output / "summary.md").is_file())
        self.assertTrue((fixture.output / "survivors.json").is_file())

    def test_combined_threshold_keeps_below_threshold_shards(self):
        populations = [["Survived"], ["Killed"] * 3, ["Killed"], ["Killed"] * 2 + ["Survived"] * 3, ["Killed"], ["Killed"]]
        fixture = AggregateFixture(self.directory, populations)
        for key in workflow.TARGETS:
            fixture.add_shared(key, [mutant("extra-CompileError", "CompileError"), mutant("extra-Ignored", "Ignored")])
            fixture.add_owned(key, [mutant("extra-RuntimeError", "RuntimeError")])
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertIs(summary["measurement_complete"], True)
        self.assertIs(summary["break_passed"], True)
        self.assertEqual((8, 12), (summary["numerator"], summary["denominator"]))
        self.assertEqual(6, summary["status_counts"]["CompileError"])
        self.assertEqual(6, summary["status_counts"]["RuntimeError"])
        self.assertEqual(6, summary["status_counts"]["Ignored"])
        self.assertEqual(2, fixture.receipts["phase"]["child_exit_code"])
        self.assertTrue((fixture.output / "summary.md").is_file())
        self.assertTrue((fixture.output / "survivors.json").is_file())

    def test_cross_shard_projection_inputs_must_match(self):
        fixture = AggregateFixture(self.directory)
        artifact = fixture.paths["publication"]
        path = artifact / "projection.json"
        manifest = json.loads(path.read_text())
        for field in ("original_sha256", "before_sha256", "after_sha256"):
            manifest[field]["scenarios/extra.json"] = "f" * 64
        write_json(path, manifest)
        fixture.receipts["publication"]["projection_manifest_sha256"] = digest(path)
        fixture.save("publication")
        self.assert_incomplete(fixture)

    def test_projection_or_report_tampering_is_incomplete(self):
        cases = ("missing", "hash", "policy", "target", "target-bytes", "excluded-bytes", "test-bytes", "drift", "disabled", "report-source")
        for case in cases:
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temp:
                fixture = AggregateFixture(Path(temp))
                artifact = fixture.paths["phase"]
                path = artifact / "projection.json"
                manifest = json.loads(path.read_text())
                if case == "missing":
                    path.unlink()
                elif case == "hash":
                    path.write_text("{}")
                elif case == "target-bytes":
                    (artifact / "projection-src/src/Parchwell.Verifier/Judge/PhaseJudge.cs").write_text("changed")
                elif case == "excluded-bytes":
                    (artifact / "projection-src/src/Parchwell.Verifier/VerifyRun/A01/WorkloadRecorder.cs").write_text("changed")
                elif case == "report-source":
                    fixture.reports["phase"]["files"]["/synthetic/src/Parchwell.Verifier/Judge/PhaseJudge.cs"]["source"] = "changed"
                else:
                    if case == "policy": manifest["policy"] = "other"
                    elif case == "target": manifest["target"] = workflow.SHARDS["publication"]
                    elif case == "disabled": manifest["disabled"].clear()
                    elif case == "drift": manifest["after_sha256"] = None
                    else:
                        manifest["before_sha256"]["tests/Parchwell.Verifier.Tests/SyntheticTests.cs"] = "f" * 64
                        manifest["after_sha256"] = dict(manifest["before_sha256"])
                    write_json(path, manifest)
                    fixture.receipts["phase"]["projection_manifest_sha256"] = digest(path)
                fixture.save("phase")
                self.assert_incomplete(fixture)

    def test_complete_below_threshold_stays_complete(self):
        fixture = AggregateFixture(self.directory, [
            ["Survived"], ["Killed", "Killed", "Survived"], ["Killed"],
            ["Killed", "Killed", "Survived", "Survived", "Survived"], ["Survived"], ["Survived"],
        ])
        for key in workflow.TARGETS:
            fixture.add_shared(key, [mutant("extra-CompileError", "CompileError"), mutant("extra-Ignored", "Ignored")])
            fixture.add_owned(key, [mutant("extra-RuntimeError", "RuntimeError")])
        result, summary = fixture.invoke()
        self.assertEqual(1, result)
        self.assertIs(summary["measurement_complete"], True)
        self.assertIs(summary["break_passed"], False)
        self.assertEqual((5, 12), (summary["numerator"], summary["denominator"]))

    def test_timeout_and_no_coverage_score_populations(self):
        fixture = AggregateFixture(self.directory, [
            ["Killed", "Timeout", "Survived"], ["Killed", "Timeout", "NoCoverage"],
            ["Killed", "Timeout", "NoCoverage"], ["Killed"], ["Killed"], ["Killed"],
        ])
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertEqual((9, 12), (summary["numerator"], summary["denominator"]))
        self.assertGreaterEqual(100 * summary["numerator"], 60 * summary["denominator"])
        self.assertEqual({"Killed": 6, "Timeout": 3, "Survived": 1, "NoCoverage": 2,
                          "CompileError": 0, "RuntimeError": 0, "Ignored": 0}, summary["status_counts"])

    def test_identity_and_completeness_failures(self):
        cases = {
            "commit": ("commit", "other"), "run": ("run_id", "other"),
            "attempt": ("run_attempt", "other"), "shard": ("shard", "other"),
            "target": ("target", "Other.cs"), "tree": ("tree", "other"),
            "dirty-before": ("tracked_clean_before", False), "dirty-after": ("tracked_clean_after", False),
            "termination": ("termination_reason", "time_cap_exceeded"),
            "not-completed": ("command_completed", False), "execution": ("child_exit_code", 1),
            "source-before": ("source_sha256_before", {"src/a.cs": "b" * 64}),
            "source-after": ("source_sha256_after", {"src/a.cs": "b" * 64}),
            "empty-source": ("source_sha256_before", {}),
            "config-hash": ("config_sha256_after", "bad"),
            "report-hash": ("report_sha256", "bad"), "log-hash": ("command_log_sha256", "bad"),
        }
        for name, (key, value) in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                fixture.receipts["phase"][key] = value
                write_json(fixture.paths["phase"] / "receipt.json", fixture.receipts["phase"])
                self.assert_incomplete(fixture)

    def test_mismatched_source_across_shards(self):
        fixture = AggregateFixture(self.directory)
        source = {"src/Parchwell.Verifier/Synthetic.cs": "b" * 64}
        fixture.receipts["phase"]["source_sha256_before"] = source
        fixture.receipts["phase"]["source_sha256_after"] = source
        fixture.save("phase")
        self.assert_incomplete(fixture)

    def test_expected_identity_mismatch(self):
        for name in ("expected_commit", "run_id", "run_attempt"):
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                self.assert_incomplete(AggregateFixture(Path(directory)), **{name: "other"})

    def test_missing_malformed_or_extra_evidence(self):
        for name in ("shard", "report", "receipt", "config", "log", "malformed-report", "malformed-receipt", "extra"):
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                artifact = fixture.paths["phase"]
                if name == "shard":
                    artifact.rename(artifact.with_name("unrelated"))
                elif name == "extra":
                    (fixture.input / f"c5-mutation-{RUN_ID}-{ATTEMPT}-extra").mkdir()
                elif name.startswith("malformed"):
                    path = artifact / ("receipt.json" if name.endswith("receipt") else "stryker/reports/mutation-report.json")
                    path.write_text("{invalid")
                    if name.endswith("report"):
                        fixture.receipts["phase"]["report_sha256"] = digest(path)
                        write_json(artifact / "receipt.json", fixture.receipts["phase"])
                else:
                    path = {"report": "stryker/reports/mutation-report.json", "receipt": "receipt.json",
                            "config": "shard-config.json", "log": "command.log"}[name]
                    (artifact / path).unlink()
                self.assert_incomplete(fixture)

    def test_input_path_as_file_still_records_incomplete_summary(self):
        fixture = AggregateFixture(self.directory)
        fixture.input = self.directory / "not-a-directory"
        fixture.input.write_text("unsupported input")
        self.assert_incomplete(fixture)

    def test_missing_receipt_field_is_incomplete(self):
        fixture = AggregateFixture(self.directory)
        del fixture.receipts["phase"]["argv"]
        write_json(fixture.paths["phase"] / "receipt.json", fixture.receipts["phase"])
        self.assert_incomplete(fixture)

    def test_report_scope_schema_and_status_failures(self):
        cases = ("duplicate-id", "duplicate-path", "unknown", "pending", "notrun", "location", "bool-location",
                 "foreign", "zero-score", "missing-target", "schema", "language", "source", "empty-mutants")
        for name in cases:
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                report = fixture.reports["phase"]
                key = "/synthetic/src/Parchwell.Verifier/" + workflow.SHARDS["phase"]
                entry = report["files"][key]
                rows = fixture.target_rows("phase")
                if name == "duplicate-id":
                    rows.append(copy.deepcopy(rows[0]))
                elif name == "duplicate-path":
                    report["files"][workflow.SHARDS["phase"]] = copy.deepcopy(entry)
                elif name in ("unknown", "pending", "notrun"):
                    rows[0]["status"] = {"unknown": "Unknown", "pending": "Pending", "notrun": "NotRun"}[name]
                elif name in ("location", "bool-location"):
                    rows[0]["location"]["start"]["line"] = 0 if name == "location" else True
                elif name == "foreign":
                    report["files"]["/synthetic/src/Parchwell.Verifier/Other.cs"] = {
                        "language": "cs", "source": "true", "mutants": [mutant("other", "Killed")],
                    }
                elif name == "zero-score":
                    rows[:] = [mutant("0", "Ignored"), mutant("1", "CompileError")]
                elif name == "missing-target":
                    del report["files"][key]
                elif name == "schema":
                    report["schemaVersion"] = 2
                elif name == "language":
                    entry["language"] = "none"
                elif name == "source":
                    entry["source"] = None
                elif name == "empty-mutants":
                    rows.clear()
                fixture.save("phase")
                self.assert_incomplete(fixture)

    def test_foreign_ignored_records_and_windows_paths_are_supported(self):
        fixture = AggregateFixture(self.directory)
        for shard in workflow.SHARDS:
            report = fixture.reports[shard]
            report["files"]["Foreign.cs"] = {"language": "cs", "source": "true", "mutants": [mutant("ignore", "Ignored")]}
            report["files"] = {key.replace("/", "\\"): value for key, value in report["files"].items()}
            report["projectRoot"] = report["projectRoot"].replace("/", "\\")
            fixture.save(shard)
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertEqual(6, summary["denominator"])
        self.assertEqual(0, summary["status_counts"]["Ignored"])

    def test_foreign_compile_errors_do_not_enter_score_population(self):
        fixture = AggregateFixture(self.directory)
        for shard in workflow.SHARDS:
            fixture.reports[shard]["files"]["Foreign.cs"] = {
                "language": "cs", "source": "true",
                "mutants": [mutant("foreign-compile", "CompileError"),
                            mutant("foreign-ignore", "Ignored")],
            }
            fixture.save(shard)
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertIs(summary["measurement_complete"], True)
        self.assertIs(summary["break_passed"], True)
        self.assertEqual((6, 6), (summary["numerator"], summary["denominator"]))
        self.assertEqual({"Killed": 6, "Timeout": 0, "Survived": 0, "NoCoverage": 0,
                          "CompileError": 0, "RuntimeError": 0, "Ignored": 0}, summary["status_counts"])
        survivors = json.loads((fixture.output / "survivors.json").read_text())
        self.assertEqual([], survivors["survivors"])
        self.assertEqual([], survivors["disposition_sample"])
        self.assertEqual(0, survivors["unreviewed_beyond_cap"])

    def test_foreign_statuses_other_than_ignored_and_compile_error_are_incomplete(self):
        statuses = ("Killed", "Survived", "Timeout", "NoCoverage", "RuntimeError",
                    "Pending", "NotRun", "Unknown")
        for status in statuses:
            with self.subTest(status=status), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                fixture.reports["phase"]["files"]["Foreign.cs"] = {
                    "language": "cs", "source": "true", "mutants": [mutant("foreign", status)],
                }
                fixture.save("phase")
                self.assert_incomplete(fixture)
                summary = json.loads((fixture.output / "summary.json").read_text())
                self.assertNotIn("phase", summary["shards"])
                self.assertIn("phase: foreign mutant with disallowed status", summary["incomplete_reasons"])
                self.assertNotIn("phase: target incomplete because a span shard is incomplete",
                                 summary["incomplete_reasons"])

    def test_actual_config_is_checked_even_when_hashes_match(self):
        fixture = AggregateFixture(self.directory)
        path = fixture.paths["phase"] / "shard-config.json"
        config = json.loads(path.read_text())
        config["stryker-config"]["coverage-analysis"] = "perTest"
        write_json(path, config)
        fixture.save("phase")
        self.assert_incomplete(fixture)

    def test_survivor_sampling_does_not_change_raw_score(self):
        fixture = AggregateFixture(self.directory, [["Killed"] + ["Survived"] * 51,
                                                     ["Killed"], ["Killed"], ["Killed"], ["Killed"], ["Killed"]])
        result, summary = fixture.invoke()
        self.assertEqual(1, result)
        self.assertIs(summary["measurement_complete"], True)
        self.assertEqual((6, 57), (summary["numerator"], summary["denominator"]))
        survivors = json.loads((fixture.output / "survivors.json").read_text())
        self.assertEqual(51, len(survivors["survivors"]))
        self.assertEqual(50, len(survivors["disposition_sample"]))
        self.assertEqual(1, survivors["unreviewed_beyond_cap"])
        self.assertEqual(survivors["survivors"][:50], survivors["disposition_sample"])
        ordered = sorted(survivors["survivors"], key=lambda row: (
            row["file"], row["location"]["start"]["line"], row["location"]["start"]["column"],
            row["location"]["end"]["line"], row["location"]["end"]["column"], row["id"]))
        self.assertEqual(ordered, survivors["survivors"])

    def test_aggregate_never_overwrites_existing_output(self):
        fixture = AggregateFixture(self.directory)
        fixture.invoke()
        saved = {path.name: path.read_bytes() for path in fixture.output.iterdir()}
        self.assertEqual(1, workflow.aggregate(fixture.input, COMMIT, RUN_ID, ATTEMPT, fixture.output))
        self.assertEqual(saved, {path.name: path.read_bytes() for path in fixture.output.iterdir()})

    def test_span_shards_combine_into_one_target_population(self):
        fixture = AggregateFixture(self.directory, [["Killed"]] * 4 + [
            ["Killed", "Survived", "Killed"], ["Killed"] * 6 + ["Survived"] * 2])
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertEqual((12, 15), (summary["numerator"], summary["denominator"]))
        target = summary["targets"]["single-action-configuration"]
        self.assertEqual(6, target["parts"])
        self.assertEqual((6, 2), (target["status_counts"]["Killed"], target["status_counts"]["Survived"]))
        self.assertEqual((6, 8), (target["numerator"], target["denominator"]))
        self.assertEqual([2, 2, 1, 1, 1, 1], [summary["shards"][f"single-action-configuration-p{i}"]["denominator"]
                                              for i in range(1, 7)])
        self.assertEqual([104, 156], summary["shards"]["single-action-configuration-p5"]["span"])
        self.assertIsNone(summary["shards"]["phase"]["span"])
        self.assertEqual(13, len(summary["shards"]))
        self.assertEqual([], summary["rebalance_needed"])
        self.assertEqual(7200, summary["rebalance_threshold_seconds"])

    def test_span_reconciliation_failures_are_incomplete(self):
        def make_gap(fixture):
            fixture.target_rows("single-action-repeat-p1")[0].update(FILTERED)

        def make_overlap(fixture):
            fixture.target_rows("single-action-repeat-p2")[0] = mutant(0, "Killed")

        def make_compile_error(fixture):
            fixture.target_rows("single-action-repeat-p1")[0] = mutant(0, "CompileError")

        def add_extra_id(fixture):
            fixture.target_rows("single-action-repeat-p3").append({**mutant(99, "Ignored"), **FILTERED})

        for change in (make_gap, make_overlap, make_compile_error, add_extra_id):
            with self.subTest(change=change.__name__), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                change(fixture)
                for shard in fixture.members("single-action-repeat"):
                    fixture.save(shard)
                result, summary = fixture.invoke()
                self.assertEqual(1, result)
                self.assertIs(summary["measurement_complete"], False)
                self.assertNotIn("single-action-repeat", summary["targets"])
                self.assertEqual(1, len([reason for reason in summary["incomplete_reasons"]
                                         if reason.startswith("single-action-repeat: ")]))

    def test_failed_span_shard_blocks_its_target(self):
        fixture = AggregateFixture(self.directory)
        (fixture.paths["single-action-configuration-p3"] / "command.log").unlink()
        result, summary = fixture.invoke()
        self.assertEqual(1, result)
        self.assertIn("single-action-configuration: target incomplete because a span shard is incomplete",
                      summary["incomplete_reasons"])
        self.assertNotIn("single-action-configuration-p3", summary["shards"])
        self.assertNotIn("single-action-configuration", summary["targets"])
        self.assertIn("phase", summary["targets"])

    def test_span_config_must_match_recomputed_member_span(self):
        for mutate in (CONFIGURATION + "{26..53}", CONFIGURATION, CONFIGURATION + "{0..26}"):
            with self.subTest(mutate=mutate), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                path = fixture.paths["single-action-configuration-p2"] / "shard-config.json"
                config = json.loads(path.read_text())
                config["stryker-config"]["mutate"] = [mutate]
                write_json(path, config)
                fixture.save("single-action-configuration-p2")
                self.assert_incomplete(fixture)

    def test_elapsed_time_must_be_a_nonnegative_number(self):
        for value in (-0.001, "1", True, None):
            with self.subTest(value=value), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                fixture.receipts["phase"]["elapsed_seconds"] = value
                fixture.save("phase")
                self.assert_incomplete(fixture)
        fixture = AggregateFixture(self.directory)
        fixture.receipts["phase"]["elapsed_seconds"] = 0
        fixture.save("phase")
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertIs(summary["measurement_complete"], True)

    def test_rebalance_lists_slow_shards_without_changing_the_result(self):
        fixture = AggregateFixture(self.directory)
        for shard, elapsed in (("phase", 7199), ("single-action-configuration-p4", 7200),
                               ("single-action-configuration-p5", 7200.001)):
            fixture.receipts[shard]["elapsed_seconds"] = elapsed
            fixture.save(shard)
        result, summary = fixture.invoke()
        self.assertEqual(0, result)
        self.assertIs(summary["break_passed"], True)
        self.assertEqual([{"shard": "single-action-configuration-p5", "target": CONFIGURATION, "part": 5,
                           "parts": 6, "span": [104, 156], "span_verified": True,
                           "elapsed_seconds": 7200.001}], summary["rebalance_needed"])
        self.assertIn("- `single-action-configuration-p5` took 7200 s, over 7200 s: split part 5 of 6 of "
                      f"`{CONFIGURATION}`, span 104..156.", (fixture.output / "summary.md").read_text())

    def time_out(self, fixture, shard="single-action-repeat-p2"):
        fixture.receipts[shard].update(elapsed_seconds=16200.02, termination_reason="time_cap_exceeded",
                                       command_completed=False, child_exit_code=-9)
        fixture.save(shard)

    def test_timed_out_shard_is_listed_for_rebalance(self):
        fixture = AggregateFixture(self.directory)
        self.time_out(fixture)
        result, summary = fixture.invoke()
        self.assertEqual(1, result)
        self.assertIs(summary["measurement_complete"], False)
        self.assertEqual([{"shard": "single-action-repeat-p2", "target": REPEAT, "part": 2, "parts": 3,
                           "span": [52, 130], "span_verified": False, "elapsed_seconds": 16200.02}],
                         summary["rebalance_needed"])
        self.assertIn(f"split part 2 of 3 of `{REPEAT}`, span 52..130 (unverified).",
                      (fixture.output / "summary.md").read_text())

    def test_rebalance_span_of_unreadable_config_is_unknown(self):
        for name in ("missing", "no-span"):
            with self.subTest(name=name), tempfile.TemporaryDirectory(dir=self.directory) as directory:
                fixture = AggregateFixture(Path(directory))
                path = fixture.paths["single-action-repeat-p2"] / "shard-config.json"
                if name == "no-span":
                    config = json.loads(path.read_text())
                    config["stryker-config"]["mutate"] = [REPEAT]
                    write_json(path, config)
                self.time_out(fixture)
                if name == "missing":
                    path.unlink()
                result, summary = fixture.invoke()
                self.assertEqual(1, result)
                self.assertEqual([{"shard": "single-action-repeat-p2", "target": REPEAT, "part": 2, "parts": 3,
                                   "span": None, "span_verified": False, "elapsed_seconds": 16200.02}],
                                 summary["rebalance_needed"])
                self.assertIn(f"split part 2 of 3 of `{REPEAT}`.\n", (fixture.output / "summary.md").read_text())


CHILD = r'''
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

mode = sys.argv[1]
parser = argparse.ArgumentParser()
parser.add_argument('--config-file', type=Path)
parser.add_argument('--output', type=Path)
parser.add_argument('--log-to-file', action='store_true')
parser.add_argument('--verbosity')
args = parser.parse_args(sys.argv[2:])
root = Path.cwd().parents[1]
observed = Path(os.environ['SYNTHETIC_ROOT'])
with (observed / 'calls.txt').open('a') as calls:
    calls.write('called\n')
os.write(1, b'\xffstdout\n')
os.write(2, b'\xfestderr\n')
if mode == 'timeout':
    child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
    (observed / 'descendant.pid').write_text(str(child.pid))
    time.sleep(60)
if mode == 'source':
    (root / 'src/input.cs').write_text('modified')
if mode == 'proof':
    assert (root / 'src/Parchwell.Verifier/Judge/PhaseJudge.cs').read_bytes() == b'if (value) return true;'
    assert (root / 'src/Parchwell.Verifier/Excluded.cs').read_bytes().startswith(b'/* Stryker disable all : ')
    assert (root / 'tests/Parchwell.Verifier.Tests/fixture.py').read_bytes() == Path(__file__).read_bytes()
if mode == 'extra-source':
    (root / 'src/Parchwell.Verifier/Unexpected.cs').write_text('return false;')
if mode == 'config':
    with args.config_file.open('a') as config:
        config.write(' ')
if mode == 'no-report':
    sys.exit(0)
report = args.output / 'reports/mutation-report.json'
report.parent.mkdir(parents=True)
report.write_text('{}')
if mode == 'report-then-hang':
    time.sleep(60)
sys.exit(int(mode) if mode in ('0', '1', '2') else 0)
'''


class ProjectionTests(unittest.TestCase):
    def test_projection_preserves_bom_original_bytes_and_line_count(self):
        for data in (b"", b"using System;\nreturn false;\n", b"\xef\xbb\xbfusing System;\r\nreturn false;\r\n"):
            with self.subTest(data=data):
                transformed = projection.prefixed(data)
                self.assertEqual(data, projection.original(transformed))
                self.assertEqual(data.count(b"\n"), transformed.count(b"\n"))
        with self.assertRaisesRegex(ValueError, "missing projection pragma"):
            projection.original(b"return false;")

    def test_manifest_count_boundaries_and_invalid_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            fixture = AggregateFixture(Path(temp))
            root = fixture.paths["phase"]
            valid = json.loads((root / "projection.json").read_text())
            original = valid["original_sha256"]
            for bound, accepted in ((len(original) - 1, False), (len(original), True), (len(original) + 1, True)):
                with self.subTest(bound=bound), patch.object(projection, "MAX_FILES", bound):
                    if accepted: projection.validate(valid, root / "projection-src", workflow.SHARDS["phase"], original)
                    else:
                        with self.assertRaisesRegex(ValueError, "invalid projection inventory"):
                            projection.validate(valid, root / "projection-src", workflow.SHARDS["phase"], original)
            for bound, accepted in ((5, False), (6, True), (7, True)):
                with self.subTest(source_bound=bound), patch.object(projection, "MAX_VERIFIER_FILES", bound):
                    if accepted: projection.validate(valid, root / "projection-src", workflow.SHARDS["phase"], original)
                    else:
                        with self.assertRaisesRegex(ValueError, "projection scope/drift"):
                            projection.validate(valid, root / "projection-src", workflow.SHARDS["phase"], original)
            for name, value in (("../escape.cs", "a" * 64), ("/absolute.cs", "a" * 64), ("bad\\path.cs", "a" * 64), ("", "a" * 64), ("bad.cs", ""), ("bad.cs", True)):
                manifest = copy.deepcopy(valid)
                manifest["original_sha256"][name] = value
                with self.subTest(name=name, value=value), self.assertRaisesRegex(ValueError, "invalid projection source/hash"):
                    projection.validate(manifest, root / "projection-src", workflow.SHARDS["phase"], original)
            for field in ("original_sha256", "before_sha256", "after_sha256", "disabled"):
                manifest = copy.deepcopy(valid)
                manifest[field] = {} if field != "disabled" else []
                with self.subTest(empty=field), self.assertRaises(ValueError):
                    projection.validate(manifest, root / "projection-src", workflow.SHARDS["phase"], original)


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.addCleanup(self.temp.cleanup)
        test_root = self.root / "tests/Parchwell.Verifier.Tests"
        (test_root / "Mutation").mkdir(parents=True)
        (self.root / "src").mkdir()
        (self.root / "src/input.cs").write_text("unchanged")
        assigned = self.root / "src/Parchwell.Verifier/Judge/PhaseJudge.cs"
        assigned.parent.mkdir(parents=True)
        assigned.write_bytes(b"if (value) return true;")
        (self.root / "src/Parchwell.Verifier/Excluded.cs").write_bytes(b"return false;")
        write_json(test_root / "Mutation/stryker-c5-config.json", workflow.BASE_CONFIG)
        self.child = test_root / "fixture.py"
        self.child.write_text(CHILD)
        subprocess.run(["git", "init", "--quiet", str(self.root)], check=True)
        subprocess.run(["git", "add", "src", "tests"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "user.name=Synthetic", "-c", "user.email=synthetic@example.invalid",
                        "commit", "--quiet", "-m", "Synthetic fixture"], cwd=self.root, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        self.environment = {"GITHUB_SHA": commit, "GITHUB_REF": "refs/heads/main",
                            "GITHUB_RUN_ID": RUN_ID, "GITHUB_RUN_ATTEMPT": ATTEMPT,
                            "SYNTHETIC_SECRET": "must-not-be-in-receipt",
                            "SYNTHETIC_ROOT": str(self.root)}
        self.patch = patch.dict(os.environ, self.environment)
        self.patch.start()
        self.addCleanup(self.patch.stop)

    def invoke(self, mode="0", output=None, shard="phase", **overrides):
        output = output or self.root / ("output-" + mode)
        values = {"repository": self.root, "command_prefix": [sys.executable, str(self.child), mode], "timeout": 5}
        values.update(overrides)
        result = workflow.run_shard(shard, output, **values)
        receipt_path = output / "receipt.json"
        receipt = json.loads(receipt_path.read_text()) if receipt_path.exists() else None
        return result, receipt, output

    def test_singleton_configs_preserve_all_other_values(self):
        for shard, (_, _, count) in workflow.PARTS.items():
            if count != 1:
                continue
            target = workflow.SHARDS[shard]
            with self.subTest(shard=shard):
                config = workflow.shard_config(shard)
                self.assertEqual([target], config["stryker-config"]["mutate"])
                config["stryker-config"]["mutate"] = workflow.BASE_CONFIG["stryker-config"]["mutate"]
                self.assertEqual(workflow.BASE_CONFIG, config)
                self.assertEqual({"high": 80, "low": 60, "break": 60}, config["stryker-config"]["thresholds"])
                self.assertEqual("off", config["stryker-config"]["coverage-analysis"])

    def test_natural_child_exits_and_exact_raw_output(self):
        for code in (0, 2, 1):
            with self.subTest(code=code):
                result, receipt, output = self.invoke(str(code))
                self.assertEqual(0 if code in (0, 2) else 1, result)
                self.assertEqual(code, receipt["child_exit_code"])
                self.assertIs(receipt["command_completed"], True)
                self.assertIsNone(receipt["termination_reason"])
                self.assertEqual(b"\xffstdout\n\xfestderr\n", (output / "command.log").read_bytes())
                self.assertEqual({"ParchwellVerifierMutationOnly": "true"}, receipt["environment_overrides"])
                self.assertEqual(workflow.RECEIPT_FIELDS, set(receipt))
                self.assertNotIn("must-not-be-in-receipt", json.dumps(receipt))
                self.assertNotIn("SYNTHETIC_SECRET", json.dumps(receipt))
        self.assertEqual(["called"] * 3, (self.root / "calls.txt").read_text().splitlines())

    def test_actual_child_uses_projection_and_original_checkout_stays_identical(self):
        before = workflow.snapshot(self.root)
        result, receipt, output = self.invoke("proof")
        self.assertEqual(0, result)
        self.assertEqual(before, workflow.snapshot(self.root))
        self.assertNotEqual(str(self.root / "tests/Parchwell.Verifier.Tests"), receipt["cwd"])
        self.assertFalse(Path(receipt["cwd"]).exists(), "temporary source must be removed")
        manifest = json.loads((output / "projection.json").read_text())
        self.assertEqual("src/Parchwell.Verifier/Judge/PhaseJudge.cs", projection.VERIFIER + manifest["target"])
        self.assertEqual(["src/Parchwell.Verifier/Excluded.cs"], manifest["disabled"])
        self.assertEqual(digest(output / "projection.json"), receipt["projection_manifest_sha256"])

    def test_new_compile_source_fails_candidate(self):
        result, receipt, output = self.invoke("extra-source")
        self.assertEqual(1, result)
        self.assertTrue(receipt["command_completed"])
        self.assertIsNone(json.loads((output / "projection.json").read_text())["after_sha256"])

    def test_existing_directive_is_rejected_without_launch(self):
        (self.root / "src/Parchwell.Verifier/Excluded.cs").write_text("// Stryker restore all\nreturn false;")
        subprocess.run(["git", "add", "src"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "user.name=Synthetic", "-c", "user.email=synthetic@example.invalid", "commit", "--quiet", "-m", "Directive fixture"], cwd=self.root, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        with patch.dict(os.environ, {"GITHUB_SHA": commit}):
            result, _, _ = self.invoke()
        self.assertEqual(1, result)
        self.assertFalse((self.root / "calls.txt").exists())

    def test_export_byte_bound_below_at_and_above(self):
        names = subprocess.check_output(["git", "ls-files", "-z"], cwd=self.root).decode().split("\0")
        size = sum((self.root / name).stat().st_size for name in names if name)
        for adjustment, expected in ((-1, 1), (0, 0), (1, 0)):
            with self.subTest(adjustment=adjustment), patch.object(projection, "MAX_BYTES", size + adjustment):
                result, _, _ = self.invoke(output=self.root / ("byte-bound-" + str(adjustment)))
                self.assertEqual(expected, result)

    def test_export_inventory_bounds_below_at_and_above(self):
        names = subprocess.check_output(["git", "ls-files", "-z"], cwd=self.root).decode().split("\0")
        count = len([name for name in names if name])
        for setting, cap in (("MAX_FILES", count), ("MAX_VERIFIER_FILES", 2)):
            for adjustment, expected in ((-1, 1), (0, 0), (1, 0)):
                with self.subTest(setting=setting, adjustment=adjustment), patch.object(projection, setting, cap + adjustment):
                    result, _, _ = self.invoke(output=self.root / (setting + str(adjustment)))
                    self.assertEqual(expected, result)

    def test_tracked_directory_symlinks_are_preserved(self):
        folder = self.root / ".claude/skills"
        folder.mkdir(parents=True)
        (folder / "test-link").symlink_to("../../tests", target_is_directory=True)
        subprocess.run(["git", "add", ".claude"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "user.name=Synthetic", "-c", "user.email=synthetic@example.invalid", "commit", "--quiet", "-m", "Symlink fixture"], cwd=self.root, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        with patch.dict(os.environ, {"GITHUB_SHA": commit}):
            result, _, output = self.invoke()
        self.assertEqual(0, result)
        manifest = json.loads((output / "projection.json").read_text())
        self.assertEqual(projection.digest(b"../../tests"), manifest["original_sha256"][".claude/skills/test-link"])

    def test_verifier_source_symlink_is_rejected_without_launch(self):
        path = self.root / "src/Parchwell.Verifier/Judge/PhaseJudge.cs"
        path.unlink()
        path.symlink_to("../Excluded.cs")
        subprocess.run(["git", "add", "src"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "user.name=Synthetic", "-c", "user.email=synthetic@example.invalid", "commit", "--quiet", "-m", "Source symlink fixture"], cwd=self.root, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        with patch.dict(os.environ, {"GITHUB_SHA": commit}):
            result, _, _ = self.invoke()
        self.assertEqual(1, result)
        self.assertFalse((self.root / "calls.txt").exists())

    def test_failed_launch_has_explicit_receipt(self):
        result, receipt, _ = self.invoke(command_prefix=[str(self.root / "missing")])
        self.assertEqual(1, result)
        self.assertIs(receipt["command_completed"], False)
        self.assertTrue(receipt["termination_reason"])
        self.assertFalse((self.root / "calls.txt").exists())

    def test_timeout_kills_descendant_and_retains_output(self):
        result, receipt, output = self.invoke("timeout", timeout=0.5)
        self.assertEqual(1, result)
        self.assertEqual("time_cap_exceeded", receipt["termination_reason"])
        self.assertIs(receipt["command_completed"], False)
        self.assertEqual(b"\xffstdout\n\xfestderr\n", (output / "command.log").read_bytes())
        pid = int((self.root / "descendant.pid").read_text())
        state = Path(f"/proc/{pid}/stat")
        time.sleep(0.05)
        try:
            child_state = state.read_text().split()[2]
        except FileNotFoundError:
            child_state = "exited"
        self.assertIn(child_state, ("Z", "exited"), "fixture descendant remains alive")
        self.assertEqual(["called"], (self.root / "calls.txt").read_text().splitlines())

    def test_source_and_config_changes_fail_candidate(self):
        for mode in ("config", "source"):
            with self.subTest(mode=mode):
                result, receipt, _ = self.invoke(mode)
                self.assertEqual(1, result)
                self.assertIs(receipt["command_completed"], True)
                if mode == "config":
                    self.assertNotEqual(receipt["config_sha256_before"], receipt["config_sha256_after"])
                else:
                    self.assertEqual(receipt["source_sha256_before"], receipt["source_sha256_after"])
                    manifest = json.loads((self.root / ("output-" + mode) / "projection.json").read_text())
                    self.assertNotEqual(manifest["before_sha256"], manifest["after_sha256"])

    def test_unknown_shard_and_existing_output_launch_nothing(self):
        with patch.object(subprocess, "Popen") as launch:
            self.assertEqual(1, workflow.run_shard("unknown", self.root / "unknown", repository=self.root))
            launch.assert_not_called()
        existing = self.root / "existing"
        existing.mkdir()
        (existing / "sentinel").write_text("preserve")
        self.assertEqual(1, workflow.run_shard("phase", existing, repository=self.root))
        self.assertFalse((self.root / "calls.txt").exists())
        self.assertEqual("preserve", (existing / "sentinel").read_text())

    def test_wrong_ref_or_sha_is_rejected_before_child(self):
        for key, value in (("GITHUB_SHA", "other"), ("GITHUB_REF", "refs/heads/other")):
            with self.subTest(key=key), patch.dict(os.environ, {key: value}):
                result, _, _ = self.invoke(output=self.root / key)
                self.assertEqual(1, result)
        self.assertFalse((self.root / "calls.txt").exists())

    def test_dirty_tracked_checkout_is_rejected_before_child(self):
        (self.root / "src/input.cs").write_text("dirty")
        result, _, _ = self.invoke()
        self.assertEqual(1, result)
        self.assertFalse((self.root / "calls.txt").exists())

    def commit_configuration(self, data):
        path = self.root / "src/Parchwell.Verifier" / CONFIGURATION
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        subprocess.run(["git", "add", "src"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "user.name=Synthetic", "-c", "user.email=synthetic@example.invalid", "commit", "--quiet", "-m", "Configuration fixture"], cwd=self.root, check=True)
        commit = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        os.environ["GITHUB_SHA"] = commit

    def local_check(self, name, target=CONFIGURATION, span="26..52", timeout=5, mode="0"):
        output = self.root / name
        with contextlib.redirect_stdout(io.StringIO()) as printed:
            result = workflow.local_check(target, span, output, timeout=timeout, repository=self.root,
                                          command_prefix=[sys.executable, str(self.child), mode])
        return result, output, printed.getvalue()

    def test_span_shard_config_is_computed_from_projected_target(self):
        self.commit_configuration(SOURCE)
        result, receipt, output = self.invoke(shard="single-action-configuration-p2")
        self.assertEqual(0, result)
        self.assertEqual([CONFIGURATION + "{26..52}"],
                         json.loads((output / "shard-config.json").read_text())["stryker-config"]["mutate"])

    def test_span_selection_failure_launches_nothing(self):
        self.commit_configuration(b"if (value) return true;")
        result, receipt, _ = self.invoke(shard="single-action-configuration-p2")
        self.assertEqual(1, result)
        self.assertIsNone(receipt)
        self.assertFalse((self.root / "calls.txt").exists())

    def test_local_check_runs_one_member_span_and_marks_it_diagnostic(self):
        self.commit_configuration(SOURCE)
        result, output, printed = self.local_check("local")
        self.assertEqual(0, result)
        self.assertIn(workflow.LOCAL_PURPOSE, printed)
        self.assertEqual([CONFIGURATION + "{26..52}"],
                         json.loads((output / "local-config.json").read_text())["stryker-config"]["mutate"])
        receipt = json.loads((output / "local-receipt.json").read_text())
        self.assertEqual("Local C5 check: diagnostic only, never acceptance evidence.", receipt["purpose"])
        self.assertEqual([26, 52], receipt["span"])
        self.assertIsNone(receipt["termination_reason"])
        self.assertEqual(0, receipt["child_exit_code"])
        self.assertFalse((output / "receipt.json").exists())

    def test_local_check_rejects_invalid_input_without_launch(self):
        self.commit_configuration(SOURCE)
        boundary = "span must start and end at member boundaries"
        order = "span must satisfy START < END <= 2147483647"
        timeout = "timeout must be above 0 and at most 16200 seconds"
        # (arguments, expected message, whether the output directory is created before the check)
        cases = {
            "off-boundary-start": ({"span": "27..52"}, boundary, True),
            "off-boundary-end": ({"span": "0..53"}, boundary, True),
            "reversed": ({"span": "52..26"}, order, False),
            "empty": ({"span": "26..26"}, order, False),
            "end-too-large": ({"span": "0..2147483648"}, order, False),
            "not-numbers": ({"span": "a..b"}, "span must be START..END", False),
            "timeout-zero": ({"timeout": 0}, timeout, False),
            "timeout-over": ({"timeout": 16200.5}, timeout, False),
            "timeout-bool": ({"timeout": True}, timeout, False),
            "unknown-target": ({"target": "Judge/Other.cs"}, "unknown C5 target", False),
        }
        for name, (arguments, message, created) in cases.items():
            with self.subTest(name=name):
                result, output, printed = self.local_check(name, **arguments)
                self.assertEqual(1, result)
                self.assertIn(message, printed)
                self.assertEqual(created, output.exists())
        with self.subTest(name="existing-output"):
            (self.root / "existing-output").mkdir()
            result, _, printed = self.local_check("existing-output")
            self.assertEqual(1, result)
            self.assertIn("output directory already exists", printed)
        with self.subTest(name="dirty"):
            (self.root / "src/input.cs").write_text("dirty")
            result, output, printed = self.local_check("dirty")
            self.assertEqual(1, result)
            self.assertIn("local check requires a clean tracked checkout", printed)
            self.assertFalse(output.exists())
        self.assertFalse((self.root / "calls.txt").exists())

    def test_local_check_result_needs_completed_child_and_report(self):
        self.commit_configuration(SOURCE)
        # (child mode, timeout, expected result, child exit, termination reason, report expected)
        cases = (("2", 5, 0, 2, None, True), ("1", 5, 1, 1, None, True),
                 ("report-then-hang", 0.5, 1, -9, "time_cap_exceeded", True), ("no-report", 5, 1, 0, None, False))
        for mode, limit, expected, child_exit, reason, report in cases:
            with self.subTest(mode=mode):
                result, output, _ = self.local_check("result-" + mode, timeout=limit, mode=mode)
                self.assertEqual(expected, result)
                receipt = json.loads((output / "local-receipt.json").read_text())
                self.assertEqual(child_exit, receipt["child_exit_code"])
                self.assertEqual(reason, receipt["termination_reason"])
                self.assertEqual(report, receipt["report_sha256"] is not None)
                self.assertFalse((output / "receipt.json").exists())
        launch = workflow.launch

        def interrupted_after_clean_exit(*arguments):
            child_exit, _ = launch(*arguments)
            return child_exit, "interrupted"

        with self.subTest(mode="interrupted"), patch.object(workflow, "launch", interrupted_after_clean_exit):
            result, output, _ = self.local_check("result-interrupted")
            self.assertEqual(1, result)
            receipt = json.loads((output / "local-receipt.json").read_text())
            self.assertEqual((0, "interrupted"), (receipt["child_exit_code"], receipt["termination_reason"]))
            self.assertIsNotNone(receipt["report_sha256"])

    def test_local_check_accepts_whole_file_and_timeout_limit(self):
        self.commit_configuration(SOURCE)
        self.assertEqual(0, self.local_check("whole-file", span="0..2147483647", timeout=5)[0])
        self.assertEqual(0, self.local_check("timeout-limit", span="0..26", timeout=16200)[0])

    def test_member_boundaries_command_prints_offsets_and_lines(self):
        self.commit_configuration(SOURCE)
        with contextlib.redirect_stdout(io.StringIO()) as printed:
            self.assertEqual(0, workflow.print_member_boundaries(CONFIGURATION, self.root))
        self.assertEqual("26 4\n52 7\n78 10\n104 13\n130 16\n156 19\n182 22\n", printed.getvalue())
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, workflow.print_member_boundaries("Judge/Other.cs", self.root))


if __name__ == "__main__":
    unittest.main()
