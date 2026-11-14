"""Checks for the independent native-ID cohort runner."""
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

    def run_fake(self, mutate_plan=None):
        shard = "b10-final-projection-codec-p1-a"
        owner, ids = workflow.COHORTS[shard]
        all_ids = [identifier for logical, group in workflow.COHORTS.values()
                   if logical == owner for identifier in group]
        first = {"mutant_id": ids[0], "span": [668, 698], "exclusions": []}
        plan = [first] + [{"mutant_id": identifier, "span": [1, 2], "exclusions": []}
                          for identifier in all_ids[1:]]
        if mutate_plan:
            mutate_plan(plan)
        calls = []
        def execute(target, selected, output, prefix, cwd, deadline):
            calls.append(selected)
            if len(calls) == 1:
                report = output / "isolated" / ids[0] / workflow.REPORT_PATH
                report.parent.mkdir(parents=True)
                report.write_text(json.dumps({}))
            return [{"mutant_id": item["mutant_id"]} for item in selected], None
        rows = [{"id": ids[0], "status": "Killed"}]
        with tempfile.TemporaryDirectory() as temp, \
                patch.object(workflow, "run_isolation_plan", side_effect=execute), \
                patch.object(workflow, "report_records", return_value=rows), \
                patch.object(workflow, "isolation_plan", return_value=plan), \
                patch.object(workflow, "merge_isolated"):
            result = workflow.run_cohort(shard, "target", b"source", [(0, 1000)], Path(temp), [], Path(temp), 100)
        return result, calls, ids

    def test_each_assigned_id_runs_once_in_order(self):
        (entries, error), calls, ids = self.run_fake()
        self.assertIsNone(error)
        self.assertEqual(list(ids), [entry["mutant_id"] for entry in entries])
        self.assertEqual([list(ids[:1]), list(ids[1:])],
                         [[item["mutant_id"] for item in call] for call in calls])

    def test_changed_inventory_or_bootstrap_stops_before_rest(self):
        for mutate in (lambda plan: plan.pop(), lambda plan: plan[0]["span"].append(1)):
            with self.subTest(mutate=mutate):
                (_, error), calls, _ = self.run_fake(mutate)
                self.assertIsNotNone(error)
                self.assertEqual(1, len(calls))


if __name__ == "__main__":
    unittest.main()
