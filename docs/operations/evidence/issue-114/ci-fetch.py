import json
import pathlib
import subprocess
import sys
import zipfile

run_id, expected_head = sys.argv[1:]
repository = 'collaborationwithothers/eventhubs-kafka-dotnet'
output = pathlib.Path('/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-114/raw/ci')
output.mkdir(parents=True, exist_ok=True)
commands = {}

def capture(name, route):
    command = ['gh', 'api', f'repos/{repository}/{route}']
    commands[name] = command
    with (output / name).open('wb') as stream:
        subprocess.run(command, stdout=stream, check=True)
    return (output / name).read_bytes()

try:
    run = json.loads(capture('run.json', f'actions/runs/{run_id}'))
    if (str(run['id']) != run_id or run['event'] != 'pull_request'
            or run['head_sha'] != expected_head or run['status'] != 'completed'):
        raise RuntimeError('CI run identity or completion mismatch')
    jobs = json.loads(capture('jobs.json', f'actions/runs/{run_id}/jobs?per_page=100&page=1'))
    artifacts = json.loads(capture('artifacts.json', f'actions/runs/{run_id}/artifacts?per_page=100&page=1'))
    if jobs['total_count'] > len(jobs['jobs']) or artifacts['total_count'] > len(artifacts['artifacts']):
        raise RuntimeError('Additional REST pages require inspection')
    capture('logs.zip', f'actions/runs/{run_id}/logs')
    if not zipfile.is_zipfile(output / 'logs.zip'):
        raise RuntimeError('REST log response is not a ZIP')
    print(json.dumps({'run_id': run['id'], 'tested_head': run['head_sha'],
        'conclusion': run['conclusion'], 'url': run['html_url'],
        'jobs': [{'name': job['name'], 'conclusion': job['conclusion']} for job in jobs['jobs']],
        'artifact_count': artifacts['total_count']}))
finally:
    pathlib.Path('/workspace/scratch/issue-114-ci-capture-commands.json').write_text(
        json.dumps(commands, indent=2) + '\n')
