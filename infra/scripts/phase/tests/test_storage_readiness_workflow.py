"""Keep the storage readiness wait on the real ephemeral workflow paths."""
from pathlib import Path
import unittest

import yaml

REPO = Path(__file__).resolve().parents[4]


class StorageReadinessWorkflowTests(unittest.TestCase):
    def test_check_dns_immediately_follows_every_ephemeral_apply_with_the_same_condition(self):
        observed = {}
        for path in sorted((REPO / '.github/workflows').glob('*.yml')):
            document = yaml.safe_load(path.read_text())
            for name, job in document.get('jobs', {}).items():
                with self.subTest(workflow=path.name, job=name):
                    steps = job.get('steps', [])
                    applies = [index for index, step in enumerate(steps)
                               if 'lifecycle.sh apply-ephemeral' in step.get('run', '')]
                    checks = [index for index, step in enumerate(steps)
                              if 'phase.sh check-dns' in step.get('run', '')]
                    self.assertLessEqual(len(applies), 1)
                    if not applies:
                        continue
                    key = (path.name, name)
                    if not checks:
                        observed[key] = 'no wait'
                        continue
                    self.assertEqual(1, len(checks))
                    self.assertEqual(applies[0] + 1, checks[0])
                    self.assertEqual(steps[applies[0]].get('if'), steps[checks[0]].get('if'))
                    observed[key] = steps[checks[0]].get('if')
        self.assertEqual({
            ('b10-abrupt-trial.yml', 'trial'): None,
            ('cluster-smoke.yml', 'smoke'): 'no wait',
            ('scenario.yml', 'batch'): None,
            ('scenario.yml', 'full-C0'): None,
            ('scenario.yml', 'full-C3'): "steps.allowed.outputs.workload == 'true'",
            ('scenario.yml', 'probe'): None,
        }, observed)


if __name__ == '__main__':
    unittest.main()
