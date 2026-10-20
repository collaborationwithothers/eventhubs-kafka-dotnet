# Child A evidence retention repair

This repair retains the original warning-control build outputs omitted from PR #98 by Git ignore rules. It makes the published child A evidence agree with its existing manifest, as the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6294278647) required.

The [Codex finding](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/98#discussion_r4329743053) was deferred to [#99](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/99). Hari subsequently directed this session to fix the issue using parallel subagents. This is a retention repair to the approved full-output copy, with no additional runtime verification allowance.

## Retained bytes

The [retention check](raw/retention-check.json) compares the unchanged [child A manifest](../issue-93/child-a/raw-manifest.json) with merged baseline `09a70930e471353b97ad07f9560e9734e562294e`. That baseline contains 134 of the 154 manifest files. The repair adds the remaining 20 files, totalling 86271 bytes, under child A's `raw/suite-4/verification/warning-control/bin/` and `obj/` directories. Every file remains byte-for-byte identical to the prior local evidence and its manifest hash.

The same check reads all 154 files from Git's index and verifies their sizes and SHA-256 values against the manifest. This covers the omission missed by the first cold review's workspace-file checks. The published child A manifest, original producer identities, runtime evidence and test inputs are unchanged. The earlier [receipt](../issue-93/child-a/receipt.md) and [review record](../issue-93/child-a/cold-review.md) now identify the publication gap and this repair.

## Exact retention command

Run from the repository root at baseline `09a70930e471353b97ad07f9560e9734e562294e`, the command was `bash /tmp/issue99-retain-command.sh`. That temporary script contained exactly:

```bash
python3 - <<'RETENTION'
import hashlib, json, pathlib, subprocess
root = pathlib.Path.cwd()
base = "09a70930e471353b97ad07f9560e9734e562294e"
manifest_path = "docs/operations/evidence/issue-93/child-a/raw-manifest.json"
manifest = json.loads((root / manifest_path).read_text())["files"]
assert len({entry["path"] for entry in manifest}) == len(manifest)
tracked = set(subprocess.check_output(["git", "ls-tree", "-r", "--name-only", base], text=True).splitlines())
missing = [entry for entry in manifest if entry["path"] not in tracked]
assert len(missing) == 20
assert all("/raw/suite-4/verification/warning-control/" in entry["path"] for entry in missing)
for entry in manifest:
    data = (root / entry["path"]).read_bytes()
    assert len(data) == entry["size_bytes"]
    assert hashlib.sha256(data).hexdigest() == entry["sha256"]
subprocess.run(["git", "add", "--force", "--", *[entry["path"] for entry in missing]], check=True)
indexed = set(subprocess.check_output(["git", "ls-files", "-z"], text=True).split("\0"))
for entry in manifest:
    assert entry["path"] in indexed
    data = subprocess.check_output(["git", "show", ":" + entry["path"]])
    assert len(data) == entry["size_bytes"]
    assert hashlib.sha256(data).hexdigest() == entry["sha256"]
result = {
    "base_commit": base,
    "manifest_path": manifest_path,
    "manifest_sha256": hashlib.sha256((root / manifest_path).read_bytes()).hexdigest(),
    "manifest_files": len(manifest),
    "manifest_bytes": sum(entry["size_bytes"] for entry in manifest),
    "base_tracked_files": len(manifest) - len(missing),
    "retained_files": len(missing),
    "retained_bytes": sum(entry["size_bytes"] for entry in missing),
    "retained_paths": [entry["path"] for entry in missing],
    "indexed_files_verified": len(manifest),
    "all_indexed_sizes_and_hashes_match": True,
    "original_manifest_unchanged": True
}
output = root / "docs/operations/evidence/issue-99/raw/retention-check.json"
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(result, indent=2) + "\n")
print(json.dumps({key: value for key, value in result.items() if key != "retained_paths"}, indent=2))
RETENTION
```

## Validation and review

Documentation validation and a fresh cold review are pending. Their results will be linked here. No suite, focused test, discovery, mutation or replay command is needed to retain these existing bytes.
