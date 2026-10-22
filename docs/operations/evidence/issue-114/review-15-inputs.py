import datetime, hashlib, json, pathlib, subprocess, sys
root = pathlib.Path('/workspace/eventhubs-kafka-dotnet')
out = root / 'docs/operations/evidence/issue-114/raw/review-15-fixes' / sys.argv[1]
paths = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=root).decode().split('\0')
files = []
for name in sorted(set(paths)):
    if not name or name.startswith('docs/operations/evidence/'):
        continue
    path = root / name
    if not path.is_file() or path.is_symlink():
        continue
    data = path.read_bytes()
    files.append({'path': name, 'size_bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()})
record = {'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip(), 'source_status': 'dirty user-directed fixcomments worktree before local build; not exact-head PR CI', 'captured_utc': datetime.datetime.now(datetime.UTC).isoformat(), 'files': files}
out.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'path': str(out), 'files': len(files)}))
