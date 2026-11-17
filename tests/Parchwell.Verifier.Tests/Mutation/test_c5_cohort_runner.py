"""Checks for the independent native-ID cohort runner."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import c5_workflow as workflow


class CohortRunnerTests(unittest.TestCase):
    def test_cohorts_are_disjoint_and_complete(self):
        for part, low, high in ((1, 116, 128), (2, 128, 143), (3, 143, 156)):
            owner = f"b10-final-projection-codec-p{part}"
            left, right = (workflow.COHORTS[f"{owner}-{suffix}"][1] for suffix in ("a", "b"))
            self.assertFalse(set(left) & set(right))
            self.assertEqual(set(map(str, range(low, high))), set(left) | set(right))
            self.assertEqual(workflow.PARTS[f"{owner}-a"], workflow.PARTS[f"{owner}-b"])

    def run_fake(self, shifted=False, damage=None, source_drift=False, *, native_ids=None,
                 bootstrap_span=(0, 1), cohorts=None):
        # Independently authored native-shaped inventory; only pins are patched.
        source = b"abcdefghij"
        rows = [{"id": str(native_ids[i] if native_ids else (654 if shifted else 116) + i), "mutatorName": "synthetic",
                 "replacement": str(i), "location": {"start": {"line": 1, "column": 2 * i + 1},
                 "end": {"line": 1, "column": 2 * i + 2}}, "static": True,
                 "status": "Ignored", "statusReason": "Removed by mutate filter"} for i in range(4)]
        identities = {str(116 + i): {key: row[key] for key in (*workflow.IDENTITY, "static")}
                      for i, row in enumerate(rows)}
        keys = ("mutatorName", "replacement", "location", "static")
        canonical = lambda value: json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True)
        values = [{key: row[key] for key in keys} for row in rows]
        values.sort(key=canonical)
        inventory = hashlib.sha256(canonical(values).encode()).hexdigest()
        report_rows = copy.deepcopy(rows)
        report_rows[0].update(status="Killed", statusReason=None)
        if damage:
            damage(report_rows)
        report = {"schemaVersion": "2", "projectRoot": "/synthetic", "files": {
            "/synthetic/target": {"language": "cs", "source": source.decode(), "mutants": report_rows}}}
        cohorts = cohorts or {"a": ("owner", ("116", "117")), "b": ("owner", ("118", "119"))}
        calls = []
        def execute(target, selected, output, prefix, cwd, deadline, *, bootstrap_shard=None):
            calls.append(copy.deepcopy(selected))
            entries = []
            for item in selected:
                entry = dict(item)
                if bootstrap_shard:
                    entry["directory_id"] = "bootstrap-116"
                folder = workflow.isolated_directory(output, entry, bootstrap_shard)
                folder.mkdir(parents=True)
                if bootstrap_shard:
                    path = folder / workflow.REPORT_PATH
                    path.parent.mkdir(parents=True)
                    path.write_text(json.dumps(report))
                entries.append(entry)
            return entries, None
        with tempfile.TemporaryDirectory() as temp, \
                patch.object(workflow, "COHORTS", cohorts), \
                patch.object(workflow, "COHORT_BOOTSTRAP_SPANS", {"116": bootstrap_span}), \
                patch.object(workflow, "COHORT_IDENTITIES", identities), \
                patch.object(workflow, "COHORT_SOURCE_SHA256", hashlib.sha256(source).hexdigest()), \
                patch.object(workflow, "COHORT_INVENTORY_SHA256", inventory), \
                patch.object(workflow, "run_isolation_plan", side_effect=execute):
            result = workflow.run_cohort("a", "target", b"changed" if source_drift else source, [(0, 10)], Path(temp), [], Path(temp), 100)
        return result, calls

    def test_each_assigned_id_runs_once_in_order(self):
        for shifted in (False, True):
            with self.subTest(shifted=shifted):
                (entries, error), calls = self.run_fake(shifted)
                first = "654" if shifted else "116"
                self.assertIsNone(error)
                self.assertEqual([first, str(int(first) + 1)], [entry["mutant_id"] for entry in entries])
                self.assertEqual([["116"], [str(int(first) + 1)]],
                                 [[item["mutant_id"] for item in call] for call in calls])
                self.assertEqual("bootstrap-116", entries[0].get("directory_id"))

    def test_actual_id_can_equal_reference_directory_token(self):
        (entries, error), calls = self.run_fake(native_ids=(115, 116, 117, 118))
        self.assertIsNone(error)
        self.assertEqual(["115", "116"], [entry["mutant_id"] for entry in entries])
        self.assertEqual("bootstrap-116", entries[0]["directory_id"])
        self.assertEqual([["116"], ["116"]], [[item["mutant_id"] for item in call] for call in calls])

    def test_isolation_plan_uses_distinct_bootstrap_and_native_directories(self):
        cohorts = {"a": ("owner", ("116", "117"))}
        def launch(argv, cwd, log, timeout):
            log.write_text("synthetic output")
            report = Path(argv[argv.index("--output") + 1]) / "reports/mutation-report.json"
            report.parent.mkdir(parents=True)
            report.write_text("{}")
            return 0, None
        item = {"mutant_id": "116", "span": [0, 1], "exclusions": []}
        with tempfile.TemporaryDirectory() as temp, \
                patch.object(workflow, "COHORTS", cohorts), \
                patch.object(workflow, "launch", side_effect=launch), \
                patch.object(workflow, "diagnostic"):
            output = Path(temp)
            first, first_error = workflow.run_isolation_plan(
                "target", [item], output, ["dotnet", "stryker"], output, float("inf"), bootstrap_shard="a")
            rest, rest_error = workflow.run_isolation_plan(
                "target", [item], output, ["dotnet", "stryker"], output, float("inf"))
            self.assertIsNone(first_error)
            self.assertIsNone(rest_error)
            self.assertEqual("bootstrap-116", first[0]["directory_id"])
            self.assertTrue((output / "isolated/bootstrap-116" / workflow.REPORT_PATH).is_file())
            self.assertTrue((output / "isolated/116" / workflow.REPORT_PATH).is_file())
            self.assertNotIn("directory_id", rest[0])

    def test_bootstrap_geometry_mismatch_stops_before_rest(self):
        (_, error), calls = self.run_fake(bootstrap_span=(0, 2))
        self.assertIn("bootstrap geometry changed", error)
        self.assertEqual(1, len(calls))

    def test_incomplete_or_overlapping_assignment_stops_before_rest(self):
        for peers in (("118",), ("117", "118", "119")):
            with self.subTest(peers=peers):
                cohorts = {"a": ("owner", ("116", "117")), "b": ("owner", peers)}
                (_, error), calls = self.run_fake(cohorts=cohorts)
                self.assertIn("cohort inventory changed", error)
                self.assertEqual(1, len(calls))

    def test_changed_source_stops_before_rest(self):
        (_, error), calls = self.run_fake(True, source_drift=True)
        self.assertIsNotNone(error)
        self.assertEqual(1, len(calls))

    def test_changed_inventory_or_bootstrap_stops_before_rest(self):
        def duplicate(rows):
            rows[1].update({key: copy.deepcopy(rows[0][key]) for key in (*workflow.IDENTITY, "static")})
        changes = (lambda rows: rows.pop(),
                   lambda rows: rows.append({**rows[0], "id": "999"}), duplicate,
                   lambda rows: rows[0].update(mutatorName="different"),
                   lambda rows: rows[0].update(replacement="different"),
                   lambda rows: rows[0]["location"]["start"].update(column=2),
                   lambda rows: rows[0].update(static=False),
                   lambda rows: rows[1].update(status="Killed", statusReason=None))
        for change in changes:
            with self.subTest(change=change):
                (_, error), calls = self.run_fake(True, change)
                self.assertIsNotNone(error)
                self.assertEqual(1, len(calls))


if __name__ == "__main__":
    unittest.main()
