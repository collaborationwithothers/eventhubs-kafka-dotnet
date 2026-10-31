"""Synthetic workflow-contract checks; these fixtures never invoke Stryker."""

from __future__ import annotations

import copy
import hashlib
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
        populations = populations or [["Killed"] for _ in workflow.SHARDS]
        for (shard, target), population in zip(workflow.SHARDS.items(), populations, strict=True):
            artifact = self.input / f"c5-mutation-{RUN_ID}-{ATTEMPT}-{shard}"
            artifact.mkdir()
            self.paths[shard] = artifact
            write_json(artifact / "shard-config.json", workflow.shard_config(shard))
            (artifact / "command.log").write_bytes(b"synthetic output\n")
            report = {
                "schemaVersion": "2", "projectRoot": "/synthetic/src/Parchwell.Verifier",
                "thresholds": {"high": 80, "low": 60},
                "files": {
                    "/synthetic/src/Parchwell.Verifier/" + target: {
                        "language": "cs", "source": "if (value) return true;",
                        "mutants": [mutant(i, status) for i, status in enumerate(population)],
                    },
                    "/synthetic/src/Parchwell.Verifier/Other.cs": {
                        "language": "none", "source": "File ignored by mutate filter", "mutants": [],
                    },
                },
            }
            self.reports[shard] = report
            original = {projection.VERIFIER + name: b"if (value) return true;" for name in workflow.SHARDS.values()}
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
                "child_exit_code": 0 if "Killed" in population else 2,
                "termination_reason": None, "command_completed": True,
                "source_sha256_before": dict(source), "source_sha256_after": dict(source),
                "tracked_clean_before": True, "tracked_clean_after": True,
                "config_sha256_before": None, "config_sha256_after": None,
                "report_sha256": None, "command_log_sha256": None,
                "projection_manifest_sha256": digest(artifact / "projection.json"),
            }
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
    def test_six_literal_targets_match_code_config_and_workflow(self):
        expected = {
            "phase": "Judge/PhaseJudge.cs",
            "publication": "VerifyRun/Publication/PublicationAcceptance.cs",
            "a01": "VerifyRun/A01/A01EvidenceRecorder.cs",
            "workload": "VerifyRun/A01/WorkloadRecorder.cs",
            "single-action-repeat": "VerifyRun/SingleAction/SingleActionRepeatJudgement.cs",
            "single-action-configuration": "VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs",
        }
        root = Path(__file__).resolve().parents[3]
        self.assertEqual(expected, workflow.SHARDS)
        self.assertEqual(list(expected.values()), workflow.BASE_CONFIG["stryker-config"]["mutate"])
        committed = json.loads((root / "tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json").read_text())
        self.assertEqual(list(expected.values()), committed["stryker-config"]["mutate"])
        # PyYAML is supplied by the pinned lifecycle tooling; this checks actual matrix data.
        import yaml
        matrix = yaml.safe_load((root / ".github/workflows/verifier-c5-mutation.yml").read_text())["jobs"]["mutation"]["strategy"]["matrix"]["include"]
        self.assertEqual(expected, {row["shard"]: row["target"] for row in matrix})

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
        for shard in workflow.SHARDS:
            fixture.target_rows(shard).extend(mutant("extra-" + status, status)
                                               for status in ("CompileError", "RuntimeError", "Ignored"))
            fixture.save(shard)
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
        for shard in workflow.SHARDS:
            fixture.target_rows(shard).extend(mutant("extra-" + status, status)
                                               for status in ("CompileError", "RuntimeError", "Ignored"))
            fixture.save(shard)
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
report = args.output / 'reports/mutation-report.json'
report.parent.mkdir(parents=True)
report.write_text('{}')
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

    def invoke(self, mode="0", output=None, **overrides):
        output = output or self.root / ("output-" + mode)
        values = {"repository": self.root, "command_prefix": [sys.executable, str(self.child), mode], "timeout": 5}
        values.update(overrides)
        result = workflow.run_shard("phase", output, **values)
        receipt_path = output / "receipt.json"
        receipt = json.loads(receipt_path.read_text()) if receipt_path.exists() else None
        return result, receipt, output

    def test_singleton_configs_preserve_all_other_values(self):
        for shard, target in workflow.SHARDS.items():
            with self.subTest(shard=shard):
                config = workflow.shard_config(shard)
                self.assertEqual([target], config["stryker-config"]["mutate"])
                config["stryker-config"]["mutate"] = list(workflow.SHARDS.values())
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


if __name__ == "__main__":
    unittest.main()
