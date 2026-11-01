# Issue 244: native-authentication-diagnostic receipt

This pull request builds the means to run three native librdkafka OIDC producers beside the custom-handler control. It runs nothing live and publishes no F02 result. The approved plan is Revision 4, implemented as written with its two `apply in implementation` P2s.

Base revision: `a3b9e10`
Branch: `claude/issue-244-native-authentication-diagnostic`

## Authority and scope

Selected plan: [issue 244 plan comment 6547370647](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547370647), Revision 4, `updated_at` 2026-10-31T21:04:04Z. It is the only plan on the issue and its pull request is not merged.

Recognized later decisions, in creation order:
1. [Decision 6547412663](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412663): the nine Revision 1 answers.
2. [Decision 6548960917](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6548960917): native stages after the C6 judge, the GUID rule in `Validate`, and the Revision 3 P2s folded in.
3. [Approval 6554645171](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6554645171), posted after the plan's latest edit and naming Revision 4 and its `updated_at`, in light of the [Revision 4 plan check 6554607130](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6554607130) (`Blocking findings: none`). It orders the check's two `apply in implementation` P2s applied as written.

The implementing tool is Claude Code. [Comment 6547412843](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412843) (2026-10-31T18:25:50Z) names it before `ready-for-agent` was applied at 2026-11-01T06:53:28Z; [comment 6554652156](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6554652156) (06:53:38Z) repeats it ten seconds after the label, and a missing comment at label time would also mean Claude Code.

Nothing here authorizes a dispatch, an Azure action or spend (approval 6554645171; decision 6547412663, answer 8).

## Execution card

- Deliverable: the plan's changes 1 to 7, the listed tests, this receipt and `items/244/state.md`.
- Current authorization: approval 6554645171 for implementation and offline verification only.
- Mandatory gates: the plan's "Acceptance commands"; cold review under AGENTS.md "Before opening a pull request" rule 8; hosted Codex review and CI; Hari's merge, because `.github/workflows/scenario.yml` and its mirror are protected paths.
- Verification commands: those in "Verification record" below.
- Earliest authorized publication checkpoint: after the first comprehensive cold review and its corrections.
- Review budget: rule 8's cumulative budget, 2 comprehensive and 2 focused. Exhaustion action: hand off to Hari.

## Specification comparison

| Plan change | Where |
|---|---|
| 1 Dispatch input, mirror, `check-offline.sh` | `.github/workflows/scenario.yml`, `docs/operations/workflows/scenario.yml.txt`, `infra/scripts/lifecycle/check-offline.sh` |
| 2 Phase state, including the corrected `prepare_stop` lines | `infra/scripts/phase/phase.py` |
| 3 Stage graph | `infra/scripts/phase/probe_v2.py` |
| 4 Chart | `charts/parchwell/values.schema.json`, `charts/parchwell/templates/_probe-v2.tpl` |
| 5 Options and stage | `ProbeV2Stage.cs`, `ProbeV2Options.cs` |
| 6 Native path and redaction | `ProbeV2NativeAuthentication.cs` (new), `ProbeV2Capture.cs`, `ProbeV2Worker.cs`, `ProbeV2SessionFactory.cs` |
| 7 Documentation, including the corrected safety-stop sentence | `authentication-lifecycle-observation.md`, `kafka-authentication.md` |

Plan check P2 1 (`prepare_stop`): the new line follows `refresh_test = test_handler_refresh_path()` and the `if` gains `or native_test`. P2 2 (documentation sentence): the replacement text from the check is used verbatim. P2 3 (citation of `rd_kafka_broker_fail` lines) changes plan text only and no file.

No difference between the plan and the repository changed any meaning. No `results/`, `expected.yaml`, verifier, judge, `*.csproj` or package change. `docs/blueprint.md`, AGENTS.md and factory.md are unchanged.

## Predicate map

| Predicate | Below / at / above | Test |
|---|---|---|
| Flag parser (`test_native_authentication`) | absent and `false` give False; `true` gives True; `True`, `1`, empty raise | `test_phase.py` parser test |
| Tuple gate (`phase.py`, `probe_v2.selection`, five job `if:` expressions) | exact tuple admitted; each of refresh, capture, items, run kind and scenario crossed alone is rejected | `test_phase.py`, `test_probe_v2.py`, `test_workflow.py`, `WorkflowAuthenticationContractTests.cs` |
| Frozen-state mismatch in `prepare_shared` | requested true with frozen false, and the reverse, both rejected | `test_phase.py` |
| Chart schema and template pairing | stage and mode paired; each negative case from the plan rejected | `test_probe_v2_chart.py` |
| `ProbeV2Options.Validate` principal rule | 36-character hyphenated GUID accepted; empty, `x`, 32-digit and braced forms rejected | `ProbeV2OptionsTests` |
| `Bind` native value | whitespace and CR or LF rejected; empty leaves `""` | `ProbeV2OptionsTests` |
| `Redact` | each rewrite in the plan's list, including empty `FederatedTokenFile` and `TenantId` | `ProbeV2NativeAuthenticationTests` |
| Capture redaction | `Log`, `Error`, `Exception`, `Delivery`; null `redact` records raw text | `ProbeV2CaptureTests` |
| `ProcessLifetimeSeconds` (`produce_one && WindowMs == 330000`) | native stage (`WindowMs` 120000) needs 840; existing `produce_one` stages still need 1920 | `ProbeV2OptionsTests` |
| `ProbeV2Stage.For` native arm (`number == 6`) | C6 accepted; C9 rejected as `v2_stage`; no C7 or below-boundary case, because configuration validity (`v2_configuration`) runs first and the chart rejects other configurations | `ProbeV2OptionsTests`, `test_probe_v2_chart.py` |
| `stages(..., native)` C6-only gate | C6 gains three stages; C9 and every other configuration equal `main` | `test_probe_v2.py` |
| `__NativePrincipalId` redaction suffix | value becomes `[redacted]`; `__PrincipalId` still matches separately | `test_probe_v2.py` |

The `v2_native_consumer` guard in `ProbeV2SessionFactory` has no test and is unreachable through the worker, because every native stage is `produce_one`. It guards a future misuse only.

In `batch_items`, only the `not refresh_test` factor of the native check is independently reachable. The `kind`, `value` and `capture` factors repeat routing at the lines after it and are unreachable.

## Verification record

Source commits: the Python, chart, host, `verify-dotnet.sh` and docs runs were made on the working tree that became commit `d6127a0` (source and test files identical to it; the receipt and state file were written after those runs). After the round 1 corrections, which only rename the 24 new tests and edit this receipt and `items/244/state.md`, the solution build, the LoadGen tests, `WorkflowAuthenticationContractTests` (with `PARCHWELL_REPOSITORY_ROOT` set) and `npm run check:docs` were run again on the tree that became commit `43df08a`. The Python tests do not depend on C# test names and were not rerun.

Run in this session's container (Linux, .NET SDK 10.0.402 installed from the official script, Helm 4.3.1 and kubeconform 0.8.0 with the SHA-256 values pinned in `infra/scripts/lifecycle/install-tools.sh`).

| Command | Result |
|---|---|
| `python -m unittest discover -s infra/scripts/phase/tests` | 185 run; 1 failure and 1 error, both `test_probe_v2_access.ProductionReaderTests.test_both_streams_capped`, which fails the same way on a clean checkout of `origin/main` in this container; `main`'s CI run 41245270515 (`.NET verification` on `a3b9e10`) succeeded, so the cause looks specific to this container and was not investigated |
| `python -m unittest discover -s infra/scripts/tests` | 233 run; the same 1 failure and 1 error |
| `python3 charts/parchwell/tests/test_probe_v2_chart.py` | passed: 153 positive, 138 negative |
| `python3 charts/parchwell/tests/test_loadgen_host.py` | passed: 101 rendered environments and 14 rejected input regressions (run by the implementing subagent) |
| `./scripts/verify-dotnet.sh` | locked restore, solution build (warnings are errors), Kafka XML inventory, architecture tests (14 passed) and enrichment tests (56 passed) passed; it stopped at `Parchwell.Kafka.Tests`, whose single failure is a Docker-dependent spike test in a project this change does not touch |
| `dotnet test --project tests/Parchwell.LoadGen.Tests --configuration Release --no-build` | 4 failures, all `LocalKafka` tests that need Docker; every other test passed, including the new ones |
| `npm run test:docs` / `npm run check:docs` | tests pass; 8 advisory warnings, none in files this pull request changes |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | silent |
| `git diff --check origin/main` | clean |

Not run here, to be covered by CI: `./scripts/verify-worker-images.sh` and the LoadGen Docker tests (no Docker daemon); `check-offline.sh` as a whole (its `shellcheck` stage; the implementing subagent ran its embedded structural check, which includes the workflow input check, with exit 0); the remaining test projects after `Parchwell.Kafka.Tests` in `verify-dotnet.sh`. `actionlint` reports `unexpected key "queue" for "concurrency"`, identically on `origin/main`'s copy of the file.

The first backup push of this session committed Python bytecode by mistake; it was removed before the real push and the real branch contains none.

## Cold review record

Test names: `docs/engineering/dotnet.md` "Testing" requires `Given_<state>_When_<action>_Then_<outcome>`. The plan's architecture test name omitted the underscore after `Given`; the binding standard governs, so all new tests use `Given_`. About 85 older tests on `main` do not, which this change leaves alone.

Round 1: comprehensive (1 of 2), reviewed commit `d6127a0` against `a3b9e10`. Result: 1 P1, 4 P2. Complete report held by the coordinating session. Dispositions:
- P1 (24 new test names lacked the underscore after `Given`): fixed by renaming; see "Test names" above.
- P2 (the two `azure_imds` construction cases make librdkafka queue a real token request to the metadata address on the CI host): `Out of contract:` the plan's "Tests" for `ProbeV2NativeAuthenticationTests` lists this construction test and its "Plan-check questions" (c) scope it to proving only that the pinned binary accepts the literals.
- P2 (tool comment timing): fixed here and in `items/244/state.md` by citing the comment posted before the label.
- P2 (receipt did not say `main`'s CI passes the failing Python test): fixed in "Verification record".
- P2 (predicate map gaps): fixed in "Predicate map".

Round 2: comprehensive (2 of 2). Input: the staged diff against `origin/main` at `a3b9e10`, which contains the Round 1 corrections. Comparison revision: `d6127a0`. Cumulative before this round: 1 comprehensive, 0 focused.

Round 2 result: reviewed tree of commit `43df08a` against `a3b9e10`; 1 P1, 1 P2. Dispositions: P1 (the verification record named no commit for its results): fixed by the "Source commits" paragraph under "Verification record". P2 (a blank line split the predicate-map table): fixed.

Round 3: focused (1 of 2). Input: the change since `43df08a`, which edits only this receipt. Comparison revision: `43df08a`. The full pull request diff against `a3b9e10` is context. Cumulative before this round: 2 comprehensive, 0 focused. The comprehensive budget is spent; this round reports only unresolved findings and the corrections.
