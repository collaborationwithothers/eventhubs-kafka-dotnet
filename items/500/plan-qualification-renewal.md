# Plan: renew the C5 runtime qualification after a non-target input change (qualification-renewal)

Issue: [#500](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/500). This contract-grade plan covers the whole issue as one pull request, `qualification-renewal`. Today any change to a shared mutation input, even a comment, makes the required C5 qualification check reject with `stale_inputs`, and the code-pinned timing hashes make that rejection impossible to clear. The plan adds an explicit, reviewed renewal record that carries the measured estimates forward across such a change, a `renew` command that writes it, and removes a duplicated test.

Revision: r1. Planned against `main` at `392d44f06e6c60fc53d16921c52b0f3dd29f178e`. The files were read, and the prototype run, at `5f88803d54d52ca791f894e463c023f82d21c9b8`; `git diff --quiet 5f88803 392d44f` over `tests/Parchwell.Verifier.Tests/Mutation/`, `docs/operations/verifier-mutation.md`, `.github/workflows/dotnet-verification.yml`, `.github/CODEOWNERS`, AGENTS.md, CLAUDE.md, docs/agents/factory.md and docs/agents/pull-requests.md reports no difference, so every line number below holds at both commits.

## 1. Goal and authority

Goal: a pull request that changes shared mutation inputs, and no target source, can pass the required `.NET verification` qualification step by recording a renewal that a reviewer can read, without new measurement and without changing any pinned estimate or evidence constant.

Authority, in creation order:

1. The issue body (issue created 2026-11-15T12:44:49Z; body `updated_at` read as 2026-11-15T14:01:22Z, the time of the only comment). It scopes the issue to "renewal for changes that are not to targets", leaves sizing for new targets parked in #474, and names the duplicated test as a separate defect in the same record.
2. [Decision 6895483443](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/500#issuecomment-6895483443) (2026-11-15T14:01:22Z). Point 1: the planning session writes each question the `grilling` skill would ask, with its recommended answer, as an open question in this plan. Point 2: Hari adopts each recommended answer in advance, recorded later by the control session; the plan is then revised so "Open questions for Hari" reads `None`. Point 3: approval is of the revision whose newest plan check states `Blocking findings: none`. Point 4: the adoption does not extend to money, Azure dispatch, verdicts, protected-path merges, scope beyond this issue or the `ready-for-agent` label. No implementing tool is named.

No item state file exists (`items/500/state.md` is absent on `main`), and no item plan or parent plan exists; the issue is not a scenario item. The issue's comments at r1 are the decision above only.

Evidence used, not authority: the [#474 parking record](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894378761), first bullet under "Found", and the [#474 pilot decision 6880732047](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047), point 3, which this plan's Q2 asks Hari to amend for renewals only.

## 2. Scope

### 2.1 Root cause, as read on `main` at `5f88803`

- `validate` rejects with `stale_inputs` when `config['shared_inputs']` differs from the head's `input_maps` result (`tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py:1006-1007`).
- Each shard's `timing_inputs_sha256` must equal a hash computed from `config['shared_inputs']` and the target sources (`c5_runtime.py:1012-1013`, `:1055-1056`). All 256 shards in the committed qualification file carry `e15cd682a2731538bfde90aa333a9742bccb40e3db957f855d71a25a9e520463` (counted by script).
- The provisional pilot and several approved owners compare that hash with code constants: `PROVISIONAL_TIMING_INPUTS_HASH` (`:484`, compared at `:690`, `:907`, `:1026`) and `CODEC_TIMING_INPUTS_HASH` (`:828`, compared at `:867`). The six-tail owners pin `digest(canonical(item))`, which includes the shard's timing hash (`:705-713`).
- So refreshing `shared_inputs` and every shard hash still fails. Reproduced by script in a scratch worktree at `5f88803`: appending `// comment only` to `src/Parchwell.Verifier/AssemblyInfo.cs` gives `{"reasons": ["stale_inputs"], "status": "rejected"}`; then rewriting `shared_inputs` and all 256 shard hashes gives `{"reasons": ["invalid_estimate", "stale_inputs"], "status": "rejected"}`.

### 2.2 Design

The estimates stay bound to the inputs they were measured with. A new optional top-level `renewals` object records, per shared-input path, the hash at measurement and the current hash. The checker rebuilds the measured input map from `shared_inputs` and `renewals`, and computes the timing hash from that map. Every pinned constant therefore keeps matching, and `shared_inputs` must still equal the head exactly. The design was prototyped in a scratch worktree; the literal values in section 6 come from that prototype, run against the existing test fixtures.

### 2.3 Files allowed to change

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`
- `docs/operations/verifier-mutation.md`, section "Runtime qualification diagnostics" only

All three are protected paths (`.github/CODEOWNERS`: `Parchwell.Verifier*/` and `/docs/`), so Hari merges.

### 2.4 Paths that must not change

- `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`. This pull request records no renewal.
- `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `c5_projection.py`, `stryker-c5-config.json`, and every other file `input_maps` selects. This pull request changes no mutation input, so it needs no mutation run on `main` (section 7).
- `.github/workflows/`, `src/`, `results/`, `scenarios/`, every `expected.yaml`.
- Every constant in `c5_runtime.py` from `PROVISIONAL_TARGETS` through `NATIVE_PROOF_HASHES`, and every existing reason code.

### 2.5 Not in scope

Sizing or qualifying new mutation targets, or qualifying the three provisional targets (parked in #474 by the issue body).

## 3. External contracts

### 3.1 Qualification file: `renewals`

- Optional top-level key `renewals`. The top-level key set is exactly `{schema_version, budget_seconds, shared_inputs, targets}` or that set plus `renewals`. `schema_version` stays `1`. An empty object `{}` is valid and means no renewals.
- Its value is a JSON object. Each key is a repository path (string). Each value is an object with exactly the keys `measured_sha256`, `current_sha256` and `basis`.
- `measured_sha256` and `current_sha256` are each either a 64-character lowercase hex string (the existing `HEX` pattern) or `null`. `null` in `measured_sha256` means the path was not a shared input when the estimates were measured; `null` in `current_sha256` means it is not one now.
- `basis` is a non-empty JSON array of strings, each non-empty after stripping whitespace.
- Any shape violation raises in `shape()`, so `check` returns `{"reasons": ["invalid_input"], "status": "rejected"}` through its existing handler.
- Measured map: a copy of `shared_inputs`; for each renewal, delete the path when `measured_sha256` is `null`, otherwise set it to `measured_sha256`. The timing hash is `digest(canonical({'shared_inputs': <measured map>, 'target_sources': <as today>}))`. With no renewals this equals today's hash.

### 3.2 Non-renewable paths

Exactly these five, by repository path: `tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`, `.github/workflows/verifier-c5-mutation.yml`, `tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py`, `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, `.config/dotnet-tools.json`.

### 3.3 Reason code

One new reason code, `invalid_renewal`. All existing codes keep their meaning.

### 3.4 `check` result

When the status is `qualified` or `provisional` and `renewals` is non-empty, the result gains the key `renewed_inputs`: the sorted list of renewal paths. Otherwise the result is byte-for-byte as today. Example, from the prototype on the committed file after the reproduction change and one renewal:

```json
{"pending_targets": ["Common/Phase/B10FinalProjectionCodec.cs", "Judge/B10FinalJudge.cs", "VerifyRun/B10Final/B10FinalRepeatEvidence.cs"], "reasons": [], "renewed_inputs": ["src/Parchwell.Verifier/AssemblyInfo.cs"], "status": "provisional"}
```

### 3.5 `renew` command line

```bash
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head HEAD_SHA --qualification FILE --basis TEXT
```

All three flags are required. On success it prints one line, `json.dumps(result, sort_keys=True)`, and exits 0. On refusal it prints one line to standard error, exits 1 and leaves the file's bytes unchanged. It never runs `validate`; run `check` afterwards.

### 3.6 File format written by `renew`

`json.dumps(config, indent=2, sort_keys=True)` followed by one `\n`. Verified by script: this reproduces the committed qualification file byte for byte at `5f88803`. `renewals` is written only when non-empty, and removed when it becomes empty.

### 3.7 Documentation wording

In `docs/operations/verifier-mutation.md`, section "Runtime qualification diagnostics":

D1. In the paragraph at line 8, replace the sentence

> The qualification file binds each shard's estimate to its generated selection and current inputs; its estimates are not a mutation result or proof of hosted elapsed time.

with

> The qualification file binds each shard's estimate to its generated selection and to the inputs it was measured with; renewals, described below, record later changes to shared inputs. Its estimates are not a mutation result or proof of hosted elapsed time.

D2. In the code block at lines 14 to 18, add after the `check` line:

```
python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head HEAD_SHA --qualification FILE --basis TEXT
```

D3. After the paragraph at line 34 that starts "Omit `--base`", add this paragraph:

> A change to shared inputs that leaves every target source unchanged can renew the qualification instead of being measured again. Run `renew` on the committed head. Its basis says why the change cannot change mutation runtime, or why that risk is accepted. It writes the new shared input hashes and records each changed path under `renewals`, with the hash the estimates were measured with, the current hash and every basis given. The estimates stay bound to the measured inputs. The check rejects a renewal with `invalid_renewal` unless its current hash equals the path's hash in `shared_inputs` and differs from its measured hash. The mutation runner, the mutation workflow, `c5_projection.py`, `stryker-c5-config.json` and `.config/dotnet-tools.json` cannot be renewed; a change to them needs new measurement. A qualified or provisional result lists renewed paths as `renewed_inputs`. A renewal is not a measurement. The change still needs mutation acceptance on `main` under [Evidence pack](../agents/factory.md#evidence-pack). Estimates measured again bind to the current inputs, so the check accepts them only once the renewals they replace are removed.

## 4. Requirements

R1. WHEN `renewals` is absent or `{}`, THEN `shape`, `validate` and `check` behave exactly as on `main`.

R2. WHEN `renewals` violates section 3.1's shape, THEN `shape` raises `ValueError` and `check` returns `{"reasons": ["invalid_input"], "status": "rejected"}`.

R3. WHEN a renewal's path is in section 3.2's set, THEN `validate` adds `invalid_renewal`.

R4. WHEN a renewal's path is `src/Parchwell.Verifier/` followed by a key of `config['targets']`, THEN `validate` adds `invalid_renewal`.

R5. WHEN a renewal's `measured_sha256` equals its `current_sha256`, both `null` included, THEN `validate` adds `invalid_renewal`.

R6. WHEN a renewal's `current_sha256` differs from `config['shared_inputs'].get(path)` (`None` when absent), THEN `validate` adds `invalid_renewal`.

R7. `validate` computes the timing hash from the measured map of section 3.1, both for the per-shard comparison and for the provisional comparison with `PROVISIONAL_TIMING_INPUTS_HASH`. Every other check in `validate` is unchanged, including `config['shared_inputs'] != shared` adding `stale_inputs`.

R8. WHEN `check` would return `qualified` or `provisional` and `renewals` is non-empty, THEN the result carries `renewed_inputs` as in section 3.4.

R9. `renew` refuses, under section 3.5, WHEN `--basis` is empty after stripping whitespace; WHEN the running `c5_workflow.py` differs from the head's (the same test `check` makes); WHEN the file fails `shape`; WHEN the set of qualification targets differs from the head's targets, or any `source_sha256` differs from the head's target hash; or WHEN any changed path is in section 3.2's set. The refusal for target changes includes the text `target sources changed`, and the refusal for blocked paths includes `not renewable:` followed by the blocked paths, comma separated.

R10. WHEN no path differs between the head's shared inputs and `shared_inputs`, THEN `renew` prints `{"renewed": [], "status": "unchanged"}`, exits 0 and does not rewrite the file.

R11. Otherwise `renew`, for each changed path in sorted order: creates `{"measured_sha256": <old shared hash or null>, "current_sha256": <head hash or null>, "basis": [TEXT]}` when the path has no renewal; or sets `current_sha256` to the head hash and appends TEXT to `basis` when it has one, and deletes the renewal when `current_sha256` then equals `measured_sha256`. It sets `shared_inputs` to the head's map, writes the file under section 3.6, prints `{"renewed": [<changed paths>], "status": "renewed"}` and exits 0.

R12. `test_c5_runtime.py` keeps exactly one definition of `test_excluded_component_source_does_not_change_inputs`. The implementer deletes the second copy, lines 874 to 893 on `main`. Verified by script on `5f88803`: lines 854 to 873 and 874 to 893 are byte-identical, and the class dictionary holds only the definition starting at line 874, so the case that runs today keeps running and no assertion changes.

R13. Documentation changes D1 to D3 of section 3.7, exactly.

## 5. Invariants and safety rules

- No pinned constant, evidence binding, estimate or reason code changes. Section 2.4 lists the paths that must not change.
- A renewal never makes a target source change pass: `source_sha256` checks are unchanged, and R4 and R9 refuse target paths.
- A renewal is not mutation acceptance. factory.md "Evidence pack" still requires a passing `main` run after any change to a mutation input.
- Standing fixture rule. When an existing test's fixture is rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. The prototype ran the existing 73 tests with the new checks and all passed, so no change is expected. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.
- No workflow runs on this pull request beyond the existing `pull_request` triggers of `dotnet-verification.yml` and `docs-advisory.yml`. No Azure access.

## 6. Test map

All new tests go in `test_c5_runtime.py`, in one new class `RenewalTests` that reuses `QualificationTests.setUp`, `QualificationTests.errors`, `AdditionalQualificationTests.simulated_check` and `ProvisionalQualificationTests.provisional`, as the existing classes do. Fixture values: `shared = {'global.json': digest(b'sdk')}`, `D = runtime.digest`. "Renew P from OLD to NEW" means: set `self.shared[P] = NEW` (or delete it when NEW is `None`), set `config['shared_inputs'] = self.shared.copy()`, and set `config['renewals'] = {P: {'measured_sha256': OLD, 'current_sha256': NEW, 'basis': ['synthetic renewal']}}`.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 `test_unrenewed_shared_change_stays_stale` | R1, R7 | `self.shared['global.json'] = D(b'new sdk')`; `config['shared_inputs']` copied; no renewals | `errors() == {'stale_inputs'}` |
| T2 `test_renewed_change_keeps_measured_estimates` | R7 | Renew `global.json` from `D(b'sdk')` to `D(b'new sdk')` | `errors() == set()` |
| T3 `test_renewed_addition_and_deletion` | R7 | Subtest 1: renew `tests/fixtures/verifier-v1/new.json` from `None` to `D(b'x')`. Subtest 2, fresh fixture: renew `global.json` from `D(b'sdk')` to `None` | `errors() == set()` in both |
| T4 `test_non_renewable_paths` | R3 | For each of the five paths of section 3.2, a fresh fixture whose measured shared map includes the path at `D(b'r1')` with every shard's timing hash rebuilt, which first gives `errors() == set()`; then renew the path from `D(b'r1')` to `D(b'r2')` | `errors() == {'invalid_renewal'}` for each path |
| T5 `test_renewal_must_change_the_hash` | R5 | Subtest 1: `{'global.json': {'measured_sha256': D(b'sdk'), 'current_sha256': D(b'sdk'), 'basis': ['x']}}`. Subtest 2: `{'docs/x.json': {'measured_sha256': None, 'current_sha256': None, 'basis': ['x']}}` | `errors() == {'invalid_renewal'}` in both |
| T6 `test_renewal_must_match_shared_inputs` | R6 | `{'global.json': {'measured_sha256': D(b'old'), 'current_sha256': D(b'other'), 'basis': ['x']}}`, `shared_inputs` unchanged | `errors() == {'invalid_renewal', 'stale_inputs'}` |
| T7 `test_target_source_cannot_be_renewed` | R4 | `{'src/Parchwell.Verifier/a.cs': {'measured_sha256': D(b'a'), 'current_sha256': None, 'basis': ['x']}}` | `errors() == {'invalid_renewal', 'stale_inputs'}` |
| T8 `test_malformed_renewals_are_invalid_input` | R2 | One subtest each: `renewals` is `[]`; an entry with an extra key `note`; `measured_sha256` is `'A' * 64`; `current_sha256` is `'a' * 63`; `basis` is `[]`; `basis` is `['  ']`; `basis` is `'x'`; `basis` is `[1]`; top-level extra key `other` | `shape` raises `ValueError`; `simulated_check('README.md') == {'status': 'rejected', 'reasons': ['invalid_input']}` |
| T9 `test_empty_renewals_are_no_renewals` | R1, R8 | `config['renewals'] = {}` | `errors() == set()`; `simulated_check('global.json') == {'status': 'qualified', 'reasons': []}` |
| T10 `test_check_discloses_renewed_inputs` | R8 | T2's renewal; `simulated_check('src/Parchwell.Verifier/AssemblyInfo.cs', old_shared={'global.json': D(b'sdk')})` | `{'status': 'qualified', 'reasons': [], 'renewed_inputs': ['global.json']}` |
| T11 `test_provisional_target_survives_renewal` | R7, R8 | `provisional()`, then T2's renewal; `simulated_check('global.json', old_shared={'global.json': D(b'sdk')})` | `errors() == set()`; result `{'status': 'provisional', 'reasons': [], 'pending_targets': ['a.cs'], 'renewed_inputs': ['global.json']}` |
| T12 `test_rejected_result_has_no_renewed_inputs` | R8 | T2's renewal plus `config['targets']['b.cs']['shards']['b']['estimated_seconds'] = 1801`; `simulated_check('global.json', old_shared={'global.json': D(b'sdk')})` | `{'status': 'rejected', 'reasons': ['invalid_estimate']}` |
| T13 `test_committed_qualification_renews_a_comment_change` | R7, issue reproduction | Real repository at `HEAD`: `runtime.blob` patched to return the real bytes of `src/Parchwell.Verifier/AssemblyInfo.cs` plus `b'// renewal test\n'`, other paths unpatched; committed qualification file loaded; `shared, targets = input_maps('HEAD')`; sources and matrix read at `HEAD` as `check` does | Before renewal: `validate(...) == {'stale_inputs'}`. After setting `shared_inputs = shared` and one renewal of that path from the committed hash to the patched hash: `validate(...) == set()` |
| T14 `test_renew_command` | R9 to R11, section 3.6 | Through `runtime.main()` with `sys.argv` patched, on a temporary qualification file built from the fixture, with `git`, `blob` and `input_maps` mocked as in `simulated_check`. Subtests: (1) head changes `global.json` to `D(b'new sdk')`; (2) then head changes it to `D(b'newer sdk')` with basis `second`; (3) then head returns it to `D(b'sdk')`; (4) no change; (5) head changes `.config/dotnet-tools.json`; (6) head changes target `b.cs`; (7) basis `'  '` | (1) exit 0, stdout `{"renewed": ["global.json"], "status": "renewed"}`, file equals `json.dumps(expected, indent=2, sort_keys=True) + '\n'` with one renewal `{measured: D(b'sdk'), current: D(b'new sdk'), basis: ['synthetic renewal']}`, and `check` on it returns T10's result; (2) exit 0, same renewal with `current_sha256 == D(b'newer sdk')` and `basis == ['synthetic renewal', 'second']`; (3) exit 0, file has no `renewals` key; (4) exit 0, stdout `{"renewed": [], "status": "unchanged"}`, bytes unchanged; (5) exit 1, stderr contains `not renewable: .config/dotnet-tools.json`, bytes unchanged; (6) exit 1, stderr contains `target sources changed`, bytes unchanged; (7) exit 1, bytes unchanged |

R12 is checked by acceptance command A4, not by a test. R13 by A5.

## 7. Deployment assumptions

None. The checker is an offline script that the existing `Qualify verifier mutation runtime` step of `dotnet-verification.yml` runs on a hosted runner (`.github/workflows/dotnet-verification.yml:137-151`); that step discovers `test_c5_runtime.py` by pattern, so new tests in it run without a workflow change. This pull request changes no mutation input: `c5_runtime.py`, `test_c5_runtime.py` and the documentation are not in the `input_maps` selection (`c5_runtime.py:429-470`). Verified with the prototype committed in a scratch worktree: `check --base 5f88803 --head <prototype>` returned `{"reasons": [], "status": "not_applicable"}`. So no `verifier-c5-mutation.yml` dispatch is needed after merge.

Downstream: the first pull request that uses `renew`, such as the consumer batch proposed in #499, changes mutation inputs and so needs mutation acceptance on `main` after it merges, as today.

## 8. Acceptance commands

Run from the repository root of the pull request branch.

| ID | Command | Evidence |
|---|---|---|
| A1 | `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_runtime.py -v` | All pass; T1 to T14 listed by name; `test_excluded_component_source_does_not_change_inputs` listed once |
| A2 | `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py` | All pass |
| A3 | `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base origin/main --head HEAD --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, then the same without `--base` | First prints `{"reasons": [], "status": "not_applicable"}`; second prints the provisional result with the three pending targets and no `renewed_inputs` |
| A4 | Script: `grep -c 'def test_excluded_component_source_does_not_change_inputs' tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`, and `git diff origin/main -- tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py` shows the deleted lines equal to `main` lines 874 to 893 | `1`; the deleted block matches |
| A5 | `npm run check:docs`, and a script that checks D1 to D3 appear verbatim | No new warning in `verifier-mutation.md`; each text found once |
| A6 | Scratch worktree at the pull request head, outside the repository: append `// comment only` to `src/Parchwell.Verifier/AssemblyInfo.cs` and commit; run `check --base HEAD~1`; run `renew --basis 'comment-only change'`; commit; run `check --base HEAD~2` | `stale_inputs` rejection; then `{"renewed": ["src/Parchwell.Verifier/AssemblyInfo.cs"], "status": "renewed"}`; then the section 3.4 example result |
| A7 | `git diff --name-only origin/main` | Only the three files of section 2.3 |
| A8 | The verification gates of docs/agents/pull-requests.md "Verification gates", in its order | As that section requires; the hosted `.NET verification` check passes on the final head |

Size, an estimate made 2026-11-15 from the prototype diff (86 lines added and 5 removed in `c5_runtime.py`, counted by `git diff --stat`) plus the test map and section 3.7: about 330 changed lines in 3 files. Size is reported, not enforced.

## 9. Sources

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` at `5f88803`: lines 314-470 (inputs), 473-486 (provisional constants), 671-713 (codec neighbour and six-tail owners), 715-761 (`shape`), 826-884 (codec constants and owner), 904-921 (repeat owner), 1002-1108 (`validate`), 1111-1154 (`check`), 1180-1210 (`main`).
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py` at `5f88803`: lines 208-233 (fixture), 752-780 (`simulated_check`), 854-893 (duplicate), 1072-1202 (provisional tests, including `test_rebound_shared_input_cannot_authorize_another_pilot` at 1160, which stays as it is: rebinding without a renewal still rejects).
- `docs/operations/verifier-mutation.md` at `5f88803`: lines 4-34.
- `.github/workflows/dotnet-verification.yml` at `5f88803`: lines 137-151.
- docs/agents/factory.md "Evidence pack", paragraph on mutation acceptance.
- Issue sources of section 1. No Kafka, librdkafka, Confluent.Kafka or Azure behaviour is relied on, so plan rule 1 needs no documentation lookup.

## 10. Plan-check questions

(a) Effort scaled by a number unknown at planning time: none. Every test has fixed inputs; T13 reads the committed file's 256 shards, a count known now.

(b) Stricter than a parent plan: there is no parent plan.

(c) Tests depending on code, data or behaviour neither on `main` nor in the pull request: none. T13 reads the committed qualification file and sources on `main`.

## 11. Open questions for Hari

These are the questions the `grilling` skill would ask, each with its recommended answer, under decision 6895483443 point 1. The plan above already follows every recommendation, so adopting all of them changes no section.

Q1. How should a non-target change be renewed? Options: (A) carry the measured estimates forward with an explicit per-path renewal record; (B) require new measurement, which cannot happen before merge, because the C5 workflow is dispatched only on `main` (AGENTS.md hard safety rule 8); (C) detect comment-only C# changes and skip them, which needs a C# tokenizer the checker does not have and covers only one case. Recommended: A. Risk: a renewed test or source change can make mutation jobs slower than their estimates. That shows only in the next `main` mutation run, which the change needs anyway under "Evidence pack".

Q2. Does renewal apply to the three provisional pilot targets? [Decision 6880732047](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047) point 3 says "Preserve current source/input fingerprints" and "Malformed or stale bindings ... still reject". Recommended: yes, for shared inputs only, with each change listed under `renewals`; target sources stay pinned. Without this, every verifier change stays blocked until #474 qualifies those targets, which is the defect this issue reports. Adopting it amends that point for listed renewals only.

Q3. Which shared inputs cannot be renewed? Recommended: the five mutation-tooling files of section 3.2, which decide how mutants are generated and run. Everything else, including the SDK pin, package locks, tests and non-target sources, is renewable with a basis. Alternative: also exclude `global.json`, `Directory.Packages.props` and the `packages.lock.json` files, at the cost of blocking toolchain updates again.

Q4. Should `check` disclose renewals? Recommended: yes, as `renewed_inputs` on qualified and provisional results only (section 3.4), so CI logs show unmeasured carry-forward. Existing results are unchanged when there are no renewals.

Q5. Should there be a `renew` command, or a documented hand edit? Recommended: the command of section 3.5. A hand edit of 326 shared-input hashes is error-prone, and the command refuses target and tooling changes.

Q6. Schema: optional key with `schema_version` 1, or a required key with version 2? Recommended: optional, version 1. Absent and `{}` mean the same, and no existing fixture or the committed file needs to change.

Q7. Is there a cap on renewals before new measurement? Recommended: no cap. Renewals last until estimates are measured again; the check then requires them to be removed (R7 with section 3.7 D3). The next `main` mutation run reports actual performance.

Q8. The duplicated test. Recommended: delete the second, byte-identical copy in this pull request (R12). It is in a file this pull request changes, the deletion removes no case that runs today, and no assertion changes. Alternative: rename one copy, which would run the same case twice.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4 or the scope in section 2. This includes any change to the qualification file, a pinned constant, a reason code other than adding `invalid_renewal`, or a workflow.
