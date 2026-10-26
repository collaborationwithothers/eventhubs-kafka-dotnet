#!/bin/bash
set -euo pipefail
trap 'printf "%s:%s: exit %s\n" "${0##*/}" "$LINENO" "$?" >&2' ERR
umask 077
script_directory=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repository=$(cd "$script_directory/../../.." && pwd)
if [ "${1:-}" != --bounded ]; then
  python3 "$script_directory/command.py" 600 -- bash "$0" --bounded "$@"
  exit 0
fi
shift
cd "$repository"
python=python3
if [ "${1:-}" != --terraform-test-paths ]; then
  : "${PARCHWELL_TOOLS_DIRECTORY:?set the pinned tools installation directory}"
  python="$PARCHWELL_TOOLS_DIRECTORY/venv/bin/python"
  export PATH="$PARCHWELL_TOOLS_DIRECTORY:$PATH"
fi
"$python" - "$repository" "$@" <<'PY'
from pathlib import Path
import sys


def terraform_test_paths(repository):
    for name in ('persistent', 'ephemeral', 'cluster'):
        root = repository / 'infra' / name
        if name == 'cluster' and not root.exists():
            print('C1 future dependency: infra/cluster is absent; cluster checks have not passed')
            continue
        tests = root / 'tests'
        canonical = repository / 'tests/terraform' / name
        if not tests.is_dir() or tests.resolve() != canonical.resolve() or not any(tests.glob('*.tftest.hcl')):
            raise ValueError(f'{name}: root-local tests must resolve to {canonical} and contain *.tftest.hcl; C1 owns cluster prerequisites')


def load_yaml(text):
    import yaml

    class UniqueLoader(yaml.BaseLoader):
        def construct_mapping(self, node, deep=False):
            mapping = {}
            for key_node, value_node in node.value:
                key = self.construct_object(key_node, deep=deep)
                if key in mapping:
                    raise ValueError(f'duplicate YAML mapping key: {key}')
                mapping[key] = self.construct_object(value_node, deep=deep)
            return mapping

    return yaml.load(text, Loader=UniqueLoader)


def check_queue(document, filename):
    expected = {'group': 'parchwell-ephemeral', 'queue': 'max', 'cancel-in-progress': 'false'}
    required = filename in ('cluster-smoke.yml', 'sweeper.yml')
    allowed = required or filename == 'scenario.yml' and isinstance(document.get('concurrency'), dict) and 'queue' in document['concurrency']
    if allowed and document.get('concurrency') != expected:
        raise ValueError(f'{filename}: concurrency must be exactly {expected}')

    def visit(value, path=()):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'queue' and not (allowed and path == ('concurrency',) and child == 'max'):
                    raise ValueError(f'{filename}: queue outside checked top-level concurrency')
                visit(child, path + (key,))
        elif isinstance(value, list):
            for child in value:
                visit(child, path + ('[]',))
    visit(document)
    return allowed


def check_workflows(repository):
    expected_triggers = {
        'cluster-smoke.yml': {'workflow_dispatch': ''},
        'scenario.yml': {'workflow_dispatch': {'inputs': {
            'scenario': {'description': 'Scenario definition to run', 'type': 'choice', 'required': 'true', 'default': 'A01', 'options': ['A01', 'B04', 'probe-batch']},
            'run_kind': {'description': 'Probe batch run kind', 'type': 'choice', 'required': 'true', 'default': 'smoke', 'options': ['smoke', 'batch']}}}},
        'sweeper.yml': {'workflow_dispatch': '', 'schedule': [{'cron': '*/15 * * * *'}]},
        'dotnet-verification.yml': {'pull_request': '', 'push': {'branches': ['main']}, 'workflow_dispatch': ''},
        'docs-advisory.yml': {'pull_request': {'types': ['opened', 'synchronize', 'reopened', 'ready_for_review']}},
    }
    for path in sorted((repository / '.github/workflows').glob('*.yml')):
        document = load_yaml(path.read_text())
        check_queue(document, path.name)
        if path.name in expected_triggers and document.get('on') != expected_triggers[path.name]:
            raise ValueError(f'{path.name}: unexpected workflow triggers')
        if path.name == 'cluster-smoke.yml':
            uploads = [step for job in document['jobs'].values() for step in job.get('steps', []) if str(step.get('uses', '')).startswith('actions/upload-artifact@')]
            if len(uploads) != 1 or uploads[0].get('with', {}).get('path') != '${{ env.PARCHWELL_ARTIFACT_DIRECTORY }}':
                raise ValueError('smoke uploads must select only the public artifact directory')
        triggers = document.get('on', {})
        if 'pull_request' in triggers or 'pull_request_target' in triggers:
            jobs = document.get('jobs', {})
            if document.get('permissions', {}).get('id-token') == 'write':
                raise ValueError(f'{path.name}: PR workflow requests Azure identity')
            for job in jobs.values():
                if job.get('permissions', {}).get('id-token') == 'write' or any(str(step.get('uses', '')).startswith('azure/login@') for step in job.get('steps', [])):
                    raise ValueError(f'{path.name}: PR job requests Azure identity')
    if (repository / '.github/workflows/sweeper.yml').read_bytes() != (repository / 'docs/operations/workflows/sweeper.yml.txt').read_bytes():
        raise ValueError('sweeper mirror differs')


def main():
    repository = Path(sys.argv[1])
    terraform_test_paths(repository)
    if sys.argv[2:] == ['--terraform-test-paths']:
        return
    if sys.argv[2:]:
        raise ValueError('usage: check-offline.sh [--terraform-test-paths]')
    import yaml
    if yaml.__version__ != '6.0.3':
        raise ValueError('structural checks require pinned PyYAML 6.0.3')
    check_workflows(repository)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, OSError) as error:
        print(f'check-offline: {error}', file=sys.stderr)
        sys.exit(1)
PY
if [ "${1:-}" = --terraform-test-paths ]; then
  exit 0
fi
for tool in shellcheck actionlint helm kubeconform; do
  if ! command -v "$tool" >/dev/null; then
    printf "check-offline: missing executable: %s\n" "$tool" >&2
    exit 1
  fi
done
run() { python3 "$script_directory/command.py" 600 -- "$@"; }
while IFS= read -r -d '' script; do
  run shellcheck -x "$script"
done < <(find infra/scripts -type f -name '*.sh' -print0)
for script in scripts/*.sh; do
  if [ -f "$script" ]; then
    run shellcheck -x "$script"
  fi
done
run cmp .github/workflows/sweeper.yml docs/operations/workflows/sweeper.yml.txt
run cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt
run actionlint -ignore '^unexpected key "queue" for "concurrency" section\. expected one of "cancel-in-progress", "group"$' .github/workflows/cluster-smoke.yml .github/workflows/sweeper.yml
for workflow in .github/workflows/*.yml; do
  case "$workflow" in
    .github/workflows/cluster-smoke.yml|.github/workflows/sweeper.yml) continue ;;
  esac
  if [ "$workflow" = .github/workflows/scenario.yml ] && "$python" -c 'import sys,yaml; sys.exit("queue" not in yaml.load(open(sys.argv[1]), Loader=yaml.BaseLoader).get("concurrency", {}))' "$workflow"; then
    run actionlint -ignore '^unexpected key "queue" for "concurrency" section\. expected one of "cancel-in-progress", "group"$' "$workflow"
  else
    run actionlint "$workflow"
  fi
done
rendered=$(mktemp -d)
trap 'rm -rf "$rendered"' EXIT
render() {
  python3 "$script_directory/command.py" 120 --stdout-file "$rendered/chart.yaml" --max-bytes 4194304 -- helm template "$@"
  run kubeconform -strict -summary -kubernetes-version 1.35.0 "$rendered/chart.yaml"
  if [ "$2" = charts/smoke ]; then
    "$python" - "$rendered/chart.yaml" <<'PYSMOKE'
from pathlib import Path
import sys
import yaml

jobs = list(yaml.safe_load_all(Path(sys.argv[1]).read_text()))
assert len(jobs) == 1, 'smoke must render exactly one Job'
job = jobs[0]
assert job['apiVersion'] == 'batch/v1' and job['kind'] == 'Job'
assert job['metadata']['namespace'] == 'parchwell'
spec = job['spec']
for key, value in {'completions': 1, 'parallelism': 1, 'backoffLimit': 0, 'podReplacementPolicy': 'Failed', 'activeDeadlineSeconds': 120}.items():
    assert spec[key] == value, key
pod = spec['template']['spec']
for key, value in {'restartPolicy': 'Never', 'automountServiceAccountToken': False, 'nodeSelector': {'workload': 'workload'}, 'terminationGracePeriodSeconds': 10}.items():
    assert pod[key] == value, key
assert pod['securityContext'] == {'runAsUser': 65532, 'runAsGroup': 65532, 'runAsNonRoot': True, 'seccompProfile': {'type': 'RuntimeDefault'}}
assert len(pod['containers']) == 1
container = pod['containers'][0]
assert container['name'] == 'smoke' and container['image'] == 'busybox:1.37.0'
assert container['command'] == ['/bin/sh', '-c', "printf '%s\\n' parchwell-cluster-smoke-ok"]
assert container['resources'] == {'requests': {'cpu': '10m', 'memory': '16Mi'}, 'limits': {'cpu': '100m', 'memory': '32Mi'}}
assert container['securityContext'] == {'allowPrivilegeEscalation': False, 'readOnlyRootFilesystem': True, 'capabilities': {'drop': ['ALL']}}
assert not pod.get('volumes') and not pod.get('serviceAccountName') and not container.get('volumeMounts')
PYSMOKE
  fi
}
run bash charts/parchwell/tests/check.sh
render parchwell-smoke charts/smoke --namespace parchwell
render parchwell-smoke charts/smoke --namespace parchwell --set-string jobName=synthetic-alternate-smoke
