# Runner CLI versions: contract-grade plan for issue #578

Revision: r3. Base commit: `main` at 8c39cba25c912a94699b79002b7afcd5582f3dab.

The archive preflight will accept whatever Azure CLI and `azure-storage-blob` versions the runner has, as long as it can read them, and every archive manifest and smoke report will record them. This removes the `archive transport version` failure that has blocked every archive since the runner image moved to Azure CLI 2.93.0. One pull request covers it. Manifests archived before this change still verify, and Hari accepted the runner CLI output assumption by name ([decision 7065588872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065588872)).

## Goal and authority

- Authority, in creation order: the issue body of #578 as rewritten (`updated_at` 2026-11-21T16:04:46Z); decision [7065282362](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065282362) (pinned install), replaced by decision [7065364080](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065364080): "just use whatever is there in the runner", accepting by name the risk that newer CLI and storage library versions change retries, CLI debug output or token hand-off without a recheck. The later decision wins. Decision 7065588872 ([decision 7065588872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065588872)) answers this plan's open questions: option (A) for manifests archived before this change, acceptance of D1 by name, and `docs/operations/archive-smoke.md` line 40 left unchanged. There is no item state file, parent issue or item plan; the issue body is the specification.
- Goal: issue requirements 1 to 5. The archive's copy and verification logic, workflows, pinned installs, scenarios and dispatches are out of scope (issue "Out of scope").
- Candidate selection (docs/agents/factory.md "Item lifecycle" step 3): no split and no plan comment exist, so the candidate is the whole issue as one pull request, named **runner-cli-versions**. An earlier draft for the replaced pinned-install scope was pushed to this plan branch (commit 92dbc5dedcdb16ca30e9c38cacac9cbc32271761) before decision 7065364080 was read; it was never posted, checked or approved, and this commit deletes it.
- docs/agents/factory.md "Item lifecycle" step 1's live-smoke rule covers plans that change live infrastructure. This plan changes Python scripts and one document, not infrastructure, so the rule does not apply.

## Scope

One pull request, branch `claude/578-runner-cli-versions`.

Files allowed to change:

1. `infra/scripts/archive/smoke.py`: `preflight`, `run_probe` (manifest at line 598 and report at line 631), `main` (lines 659 to 670) only as needed to pass the preflight result.
2. `infra/scripts/phase/archive.py`: `validate_manifest` (lines 615 onwards), `publish_controls` (lines 572 to 600), and `execute` (lines 2223 to 2340) where it calls `_common.preflight`.
3. `infra/scripts/tests/test_archive_smoke.py` (the tests of `infra/scripts/archive/smoke.py`; `infra/scripts/archive/` has no test directory, so issue requirement 5's "tests in `infra/scripts/archive/`" is read as this file).
4. `infra/scripts/phase/tests/test_archive_transfer.py`, `infra/scripts/phase/tests/test_archive_config.py`, `infra/scripts/phase/tests/test_archive_inventory_integration.py`, and any other test under `infra/scripts/phase/tests/` or `infra/scripts/tests/` only under the standing fixture rule below.
5. `docs/operations/archive-smoke.md` lines 15, 52, 62, 64 and 68.
6. `items/578/state.md` (new; written by the implementing session under docs/agents/factory.md "Item state file").

Paths that must not change: `.github/workflows/`, `docs/operations/workflows/`, `scripts/azure-cli-session/`, the archive's copy and verification logic (`Transfer`, `Collector`, hashing and size checks), the receipt and completion-marker formats, `POLICY`, `docs/operations/evidence/` (historical evidence), `scenarios/`, `results/`.

## External contracts

C1. Version pattern. A version is readable when it is a Python `str`, all ASCII, and matches in full:

```
[0-9]+\.[0-9]+\.[0-9]+[0-9A-Za-z.+-]{0,32}
```

C2. `preflight(deadline)` in `infra/scripts/archive/smoke.py` returns a `dict` with exactly the keys `python` (the absolute interpreter path, as today), `azure_cli` and `azure_storage_blob`. It raises `Invalid` when any of these holds, and otherwise accepts:

- `az version --output json` exits non-zero, is not a JSON object, or its `azure-cli` value is not readable under C1;
- `az --version` exits non-zero or does not contain exactly one `Python location '<path>'` line (the existing regular expression at line 266, unchanged);
- the path is not absolute, not a file or not executable (unchanged);
- the interpreter command `importlib.metadata.version("azure-storage-blob")` exits non-zero (for example, the library is missing), or its output with surrounding ASCII whitespace removed is not readable under C1;
- `OSError`, `ValueError` or `TypeError` (unchanged), including output that is not ASCII.

No other version comparison remains. `Gap` from `command` still propagates as today.

C3. Manifest field. The canonical manifest (`kind: run_evidence_archive`, `schema_version` 1 unchanged) gains a top-level key `transport_versions` whose value is `{"azure_cli": <str>, "azure_storage_blob": <str>}`, both readable under C1, holding the values preflight returned. Both writers add it: `execute` in `infra/scripts/phase/archive.py`, immediately after `_common.preflight` succeeds, and `run_probe` in `smoke.py` for the smoke manifest. A manifest written because preflight failed (the `local_failure` path at line 2249) has no `transport_versions` key. Nothing else in the manifest changes (issue requirement 2).

C4. Manifest validation, in `validate_manifest`: the top-level key set is the existing 12 keys, or those 12 plus `transport_versions` (option (A) of [decision 7065588872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065588872)). When the key is present, its value must be a `dict` with exactly the keys `azure_cli` and `azure_storage_blob`, each readable under C1; otherwise `InvalidArchiveConfig`. `null` is rejected. `publish_controls` raises `InvalidArchiveConfig` before any `write_control` call when the manifest has no `transport_versions` key.

C5. Smoke report. In `run_probe`'s report (line 631), `cli_version` and `storage_sdk_version` hold the `azure_cli` and `azure_storage_blob` values preflight returned, in place of the literals `'2.92.0'` and `'12.29.0b1'`. `failure_report` (line 647, its report at line 653) keeps `null` for both.

C6. Failure names unchanged: a preflight `Invalid` in `execute` still records gap `config_invalid` at boundary `archive transport version` and returns 2.

## Requirements

R1 (issue 1). WHEN the runner's CLI reports any `azure-cli` version readable under C1 and its interpreter reports any readable `azure-storage-blob` version, THEN preflight returns them under C2 and does not raise. WHEN a version is unreadable, the interpreter cannot be found or run, or the library is missing, THEN preflight raises `Invalid`.

R2 (issue 2). WHEN `execute` passes preflight, THEN the manifest it validates, writes locally and publishes carries `transport_versions` under C3. WHEN preflight raises `Invalid`, THEN the local manifest has no `transport_versions` key and the receipt gap is as C6.

R3 (issue 2). WHEN a manifest is validated, THEN C4 applies. WHEN `publish_controls` receives a manifest without `transport_versions`, THEN it raises `InvalidArchiveConfig` and writes nothing.

R4 (issue 3). WHEN the smoke runs, THEN its manifest carries `transport_versions` under C3 and its report carries the observed versions under C5. The search for other places that fix the versions found, on the base commit, only `smoke.py` lines 263, 273 and 631, the test fixtures listed in the test map, `docs/operations/archive-smoke.md` lines 15, 52, 62, 64 and 68, and historical evidence under `docs/operations/evidence/issue-114/`, which records what was checked and does not change.

R5 (issue 4). `docs/operations/archive-smoke.md` changes exactly as follows.

Line 15 becomes:

> The archive uses the Azure CLI that the GitHub runner image provides, with the `azure-storage-blob` version in that CLI's own Python environment. Hari accepted on 2026-11-21 the risk that versions newer than the checked Azure CLI 2.92.0 and `azure-storage-blob` 12.29.0b1 change retries, CLI debug output or token hand-off without a recheck ([decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065364080)). Preflight reads both versions and the interpreter path. It fails closed when it cannot read a version of the form digits, dot, digits, dot, digits with an optional suffix of up to 32 letters, digits, dots, plus or minus signs, when the interpreter cannot be found or run, or when `azure-storage-blob` is missing from it. Each archive manifest records the observed versions in `transport_versions`, and the smoke report in `cli_version` and `storage_sdk_version`. Per-object SHA-256 and size verification does not depend on these versions. The checked versions follow the [CLI release](https://github.com/Azure/azure-cli/releases/tag/azure-cli-2.92.0), its [Linux dependency](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli/requirements.py3.Linux.txt#L91) and [Python-location formatter](https://github.com/Azure/azure-cli/blob/azure-cli-2.92.0/src/azure-cli-core/azure/cli/core/util.py#L442).

In line 52, `Native retries are still enabled in the [pinned SDK policy](` becomes `Native retries are still enabled in the [SDK policy as checked at `azure-storage-blob` 12.29.0b1](`; the URL and the rest of the line stay.

In line 62, `are defined by the pinned CLI's [argument registration](` becomes `are defined, as checked at Azure CLI 2.92.0, by the CLI's [argument registration](`; the rest stays.

In line 64, `The CLI's pinned Knack 0.14.0 [verbosity validator](` becomes `As checked at Azure CLI 2.92.0, the CLI's Knack 0.14.0 [verbosity validator](`; the rest stays.

In line 68, `Sources: the pinned CLI's [token output](` becomes `Sources, as checked at Azure CLI 2.92.0 and `azure-storage-blob` 12.29.0b1: the CLI's [token output](`, and `the pinned Blob SDK's [client and transport construction](` becomes `the Blob SDK's [client and transport construction](`; the rest stays.

## Invariants and safety rules

- The archive's per-object SHA-256 and size verification, `POLICY`, the receipt and the completion marker do not change.
- Preflight still fails closed on every unreadable input listed in C2.
- Verdict integrity: no expected outcome, verdict or `results/` file changes.
- No workflow changes (issue "Out of scope"), so AGENTS.md hard safety rules 8 and 9 are untouched.
- Assertion changes forced by issue requirement 1 are named in the test map ("flipped") and listed in the PR implementation notes. They are the only existing assertions that change outcome; Code Review Rules flag a loosened assertion as P1, so the PR description cites issue requirement 1 and this plan for each.

## Test map

| Req | Test | Input | Literal expectation |
|---|---|---|---|
| R1 | `test_archive_transfer.py` `test_actual_wrapper_preflight_cli_location_and_sdk_contract` (extended; the fake CLI and interpreter at lines 566 to 569 stay) | defaults `2.92.0` / `12.29.0b1` | returns `{'python': str(python), 'azure_cli': '2.92.0', 'azure_storage_blob': '12.29.0b1'}` |
| R1 | same test, accepted cases | `ARCHIVE_TEST_CLI` `2.93.0`; `2.91.0` (flipped from refused); `3.0.0b1`; `2.93.0` followed by 32 characters `a`; `ARCHIVE_TEST_SDK` `12.28.0` (flipped from refused); `12.30.0b2` | each returns the dict with that exact value in its key, the other key at its default |
| R1 | same test, refused cases | CLI `2.93`; `v2.93.0`; `2.93.0 ` (trailing space inside the JSON string); `2.93.0` followed by 33 characters `a`; empty string; SDK output empty; SDK `not-a-version`; SDK `12.29` | each raises `Invalid` |
| R1 | same test, refused cases (existing, unchanged) | the five `ARCHIVE_TEST_LOCATION` cases at lines 579 to 582 | each raises `Invalid` |
| R1 | same test, missing library | fake interpreter exits 1 with `PackageNotFoundError` on stderr (new env switch on the fake, for example `ARCHIVE_TEST_SDK_EXIT=1`) | raises `Invalid` |
| R1 | `test_archive_smoke.py` `test_unsupported_versions_fail_closed` (name kept) | `command` mock returning `{"azure-cli":2.93}` (number), `{"azure-cli":"2.93.0\u00e9"}` (JSON escape for a non-ASCII letter), `{}`; and the existing documents `[]`, `null`, `true`, `3`, `"unsupported"` | each raises `smoke.Invalid`. The existing `2.91.0` case moves to the next row, because under C2 it is readable |
| R1 | `test_archive_smoke.py` `test_readable_versions_are_returned` (new) | `command` side effects: version JSON `{"azure-cli":"2.91.0"}`, `az --version` output with one `Python location '<abs path of an executable temp file>'` line, interpreter output `12.29.0b1\n` | returns `{'python': <that path>, 'azure_cli': '2.91.0', 'azure_storage_blob': '12.29.0b1'}` |
| R1 | `test_archive_smoke.py` same test, failing interpreter | interpreter command returns exit 1 | raises `smoke.Invalid` |
| R2 | `test_archive_transfer.py` tests at lines 379, 408 and 436 | `preflight` patched with `return_value={'python': '/usr/bin/python3', 'azure_cli': '2.93.0', 'azure_storage_blob': '12.29.0b1'}` (fixture change, case (a)) | their existing assertions; where the test reads `archive-manifest.json`, add `transport_versions == {'azure_cli': '2.93.0', 'azure_storage_blob': '12.29.0b1'}` |
| R2 | `test_archive_transfer.py` line 430 case | `preflight` raises `Invalid` | existing `2` and `config_invalid`; add: receipt gap boundary `archive transport version`; local `archive-manifest.json` has no `transport_versions` key |
| R2 | `test_archive_inventory_integration.py` line 181 | `preflight` patched as in the R2 row above (fixture change, case (a)) | existing assertions; the published manifest has `transport_versions` equal to the patched values |
| R3 | `test_archive_config.py` `test_manifest_transport_versions` (new), on the fixture of `test_manifest_zero_data_and_object_completion_rejected` (lines 136 to 151) | no key; `{'azure_cli': '2.93.0', 'azure_storage_blob': '12.29.0b1'}`; `None`; `{}`; `{'azure_cli': '2.93.0'}`; the valid dict plus `'python': '/x'`; `{'azure_cli': 2, 'azure_storage_blob': '12.29.0b1'}`; `{'azure_cli': '2.93', 'azure_storage_blob': '12.29.0b1'}` | first two return the identity (the first under option (A) of decision 7065588872); the rest raise `InvalidArchiveConfig` |
| R3 | `test_archive_config.py` `test_publish_requires_transport_versions` (new) | a valid manifest without the key, and a stub transport recording `write_control` calls | raises `InvalidArchiveConfig`; zero `write_control` calls |
| R3 | `test_archive_config.py` `verify_controls` test at line 282 | its existing triple (no key) and the same triple with the valid dict added and the marker and receipt digests recomputed | both accepted (the first under option (A) of decision 7065588872) |
| R4 | `test_archive_smoke.py`, an existing successful `run_probe` test extended | `diagnostic_python` and versions `2.93.0` / `12.29.0b1` passed as the implementer wires them | `archive-smoke.json` has `cli_version` `2.93.0` and `storage_sdk_version` `12.29.0b1`; the written `archive-manifest.json` has `transport_versions` with those values |
| C5 | `test_archive_smoke.py` `test_failure_report_keeps_null_versions` (new) | `smoke.failure_report` called with a context whose public directory is a temp directory and `smoke.Gap('config_invalid', 'archive transport version')` | written `archive-smoke.json` has `cli_version` `null` and `storage_sdk_version` `null` |

Standing fixture rule (plan-pr "Plan rules" rule 2, modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2): when an existing test's fixture is rejected by a new check of this plan, the implementer does exactly one of (a) change the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keep the input and change the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it; the cold review and Codex review check each listed change. The two "flipped" cases above (`2.91.0` and `12.28.0` now accepted) are planned case (b) changes forced by issue requirement 1, not by a new check.

## Deployment assumptions

- D1. The runner's Azure CLI (2.93.0 on 2026-11-21) prints `az version --output json` with a string `azure-cli` value, prints exactly one `Python location '<absolute path>'` line in `az --version`, and its interpreter has `azure-storage-blob` installed. Evidence: the probe job log of run [44896017344](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44896017344) (job 136821799721, `Image: ubuntu-24.04`, `Version: 20261116.376.1`) shows `"azure-cli": "2.93.0"` from `az version`. The other two parts rest on source, not on the deployed binary: [azure-cli 2.93.0 `util.py` line 439](https://github.com/Azure/azure-cli/blob/azure-cli-2.93.0/src/azure-cli-core/azure/cli/core/util.py#L439) prints `Python location '{}'`, and [2.93.0 `requirements.py3.Linux.txt` line 92](https://github.com/Azure/azure-cli/blob/azure-cli-2.93.0/src/azure-cli/requirements.py3.Linux.txt#L92) pins `azure-storage-blob==12.29.0b1`. That the image's binary matches its tag's source is inferred. The log is a recorded log of a finished run, taken on that job's fresh runner, idle; no live connection was made. No offline test can cover a future image. Hari accepted D1 by name in [decision 7065588872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065588872), point 2.

## Acceptance commands

From the repository root on the pull request head:

```bash
python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'
npm run check:docs
grep -nF -e "'2.92.0'" -e "'12.29.0b1'" infra/scripts/archive/smoke.py infra/scripts/phase/archive.py
```

The first three exit 0. The `grep` prints nothing and exits 1, showing no fixed version remains in the archive code. The "Lifecycle verification" job of `dotnet-verification.yml` runs the first command on the final head; the PR description links that run.

## Deferred work

- Post-merge live check. Evidence: the issue's second acceptance item. On the verdict path: yes; it unblocks the archive of every scenario run, including the C10 and C01 batch runs of #510. Deferral is required because the issue puts any dispatch out of scope and agents dispatch only against `main` (hard safety rule 8). Responsible: the control session that next dispatches a scenario. Stage: the first scenario dispatch after merge; no extra dispatch is authorized here. Completion evidence: the archive receipt shows `archive_complete` true, or a gap whose boundary is not `archive transport version`, and the archive manifest's `transport_versions`; the control session records both on #578. That dispatch also tests D1. Exhaustion action: if the receipt still shows `archive transport version`, the control session records the job log on #578 and hands off to Hari.

## Sources

Tools used, all on 2026-11-21: `grep` over the repository at the base commit; the GitHub REST API for issue #578, its comments, and run 44896017344's jobs and probe log; `curl` of `raw.githubusercontent.com` for the azure-cli files at tags `azure-cli-2.92.0` and `azure-cli-2.93.0`. No Microsoft Learn page is relied on: the plan states no Azure service behaviour, only the CLI's printed output, taken from its source and a run log.

- S1. Issue #578 body and decisions 7065282362 and 7065364080.
- S2. `infra/scripts/archive/smoke.py` lines 24 to 26, 259 to 278, 477, 598, 631, 647 to 657 and 659 to 670; `infra/scripts/phase/archive.py` lines 572 to 600, 615 to 690, 1654 to 1673, 2212 to 2220 and 2238 to 2255, on the base commit.
- S3. `infra/scripts/tests/test_archive_smoke.py` lines 533 to 539; `infra/scripts/phase/tests/test_archive_transfer.py` lines 379, 408, 420 to 448 and 561 to 585; `infra/scripts/phase/tests/test_archive_config.py` lines 125 to 151 and 282 to 285; `infra/scripts/phase/tests/test_archive_inventory_integration.py` line 181.
- S4. `docs/operations/archive-smoke.md` lines 15, 52, 62, 64, 68 and 111 (`verify_pack` with `archive.verify_controls` for future evidence packs).
- S5. Run 44896017344 probe job 136821799721 log: `"azure-cli": "2.93.0"` at 2026-11-21T10:48:56Z; `PARCHWELL_REAL_AZ: /usr/bin/az`.
- S6. azure-cli source: 2.92.0 `util.py` line 442 and 2.93.0 line 439, both `_print("Python location '{}'".format(os.path.abspath(sys.executable)))`; `requirements.py3.Linux.txt` `azure-storage-blob==12.29.0b1` at 2.92.0 line 91 and 2.93.0 line 92; `knack==0.14.0` at 2.92.0 line 114 and 2.93.0 line 115.

## Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time: none. The places that fix the versions were found by one repository `grep` on the base commit (R4).
- (b) Stricter than the parent: there is no parent plan. Against the issue body, the plan defines "readable" with the C1 pattern and a 32-character suffix bound, so the version string written into the manifest is ASCII of a fixed form the validator can check (the suffix is bounded at 32 characters; the three numeric parts are not); and it requires `transport_versions` in every manifest `publish_controls` publishes, so a writer that omits the versions cannot publish. The issue asks that every archive record the versions; this makes that a failing check rather than a convention.
- (c) Behaviour neither on `main` nor in the pull request: the runner's CLI output, D1. Checked against a recorded run log and the CLI source at its tag; the deployed binary itself is unchecked.

## Open questions for Hari

None. Both questions of r2 are answered in [decision 7065588872](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/578#issuecomment-7065588872): Q1 by option (A), which C4 and the R3 rows already state, and Q2 by accepting D1 by name. The same decision keeps `docs/operations/archive-smoke.md` line 40 out of scope.

## Size estimate

Estimate, by the planning session on 2026-11-21, from the base-commit code read above; not measured: 9 files; about 15 changed lines in `smoke.py`, about 15 in `archive.py`, 5 changed lines in `docs/operations/archive-smoke.md`, about 100 to 150 added or changed test lines across the five test files (including fixture changes in `infra/scripts/phase/tests/test_archive_boundaries.py`, which calls `publish_controls` on its `manifest()` fixture), and about 15 lines for `items/578/state.md`. Size is reported, not enforced.

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract (C1 to C6), a requirement (R1 to R5) or the scope, including any workflow change or any change to the archive's copy and verification logic.
