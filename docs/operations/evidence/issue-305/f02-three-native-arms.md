# Issue 305: f02-three-native-arms receipt

This pull request schedules the two IMDS native arms again beside the JWT-bearer arm, so one later dispatch can run all three arms after the custom-handler control. It runs nothing live, authorizes no dispatch and publishes no F02 result. The approved plan is Revision 1, implemented as written.

Base revision: `831a2e7`
Branch: `claude/issue-305-f02-three-native-arms`

## Authority and scope

Selected plan: [issue 305 plan comment 6560249552](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560249552), Revision 1, `updated_at` 2026-11-01T15:52:18Z. It is the only plan on the issue, and its pull request is not merged.

Recognized later decisions: none on this issue. [Approval 6560508780](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560508780), posted at 16:14:10Z after the plan's latest edit and after [plan check 6560365744](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560365744) (`Blocking findings: none`, no `apply in implementation` finding), approves Revision 1 for implementation and offline verification only.

The implementing tool is Claude Code ([comment 6560164776](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560164776); no naming comment before `ready-for-agent`, so the default applies).

Nothing here authorizes a dispatch, an Azure action or spend. Whether a new expectation line is needed stays open until before the later dispatch.

## Execution card

- Deliverable: plan changes 1 and 2, the listed test edits, this receipt and `items/305/state.md`.
- Current authorization: approval 6560508780, for implementation and offline verification only.
- Mandatory gates: the plan's "Acceptance commands"; cold review under AGENTS.md "Before opening a pull request" rule 8; hosted Codex review and CI. No protected path is touched, so the merge rule decides whether the agent may merge.
- Verification commands: those in "Verification record".
- Earliest publication checkpoint: after the first comprehensive cold review and its corrections.
- Review budget: rule 8's cumulative budget, 2 comprehensive and 2 focused. Exhaustion action: hand off to Hari.

## Specification comparison

| Plan change | Where |
|---|---|
| 1 Stage graph | `infra/scripts/phase/probe_v2.py`: `NATIVE_STAGES` has three pairs; `maximum_clients` is 30 with the flag |
| 2 Documentation | `docs/operations/authentication-lifecycle-observation.md`, "Native client compatibility diagnostic": first paragraph replaced, one sentence added to the second paragraph, one phrase replaced in the fifth |
| Tests | `infra/scripts/phase/tests/test_probe_v2.py`: stage graph, flag, flag-off, IMDS-scheduled, batch and Helm tests |
| State file | `items/305/state.md` |

The plan's line numbers matched `main` at `831a2e7`. Edits were applied by a script that asserted each original text first. `values.schema.json`, the chart templates and the C# files are unchanged: they already accept all three stages, and `ProbeV2NativeAuthentication.Apply` still sets `https.ca.location` only for `jwt-bearer`. No `results/`, `expected.yaml`, verifier, judge, `*.csproj`, chart, workflow or package change. `docs/blueprint.md`, AGENTS.md and factory.md are unchanged.

## Predicate map

| Predicate | Below / at / above | Test |
|---|---|---|
| Native stage list with the flag | flag true gives `nativejwt`, `nativeimds`, `nativeimdsnode` in that order after the C6 judge; C9 unchanged; flag false unchanged (49 stages in total either way) | `test_native_stage_graph_extends_only_the_f06_c6_route_and_only_with_the_flag` |
| Native stage rejected without the flag | all three stages raise `v2_stage` | `test_native_stage_is_rejected_without_the_frozen_flag` |
| Mode per stage | each stage maps to its mode; non-native stages carry no native keys | `test_native_authentication_flag_is_frozen_and_reaches_only_the_three_native_stages`, `test_imds_native_stages_are_scheduled_again_with_the_flag` |
| Batch counts | 8 Jobs, `maximum_clients` 30, 30 admitted slots (6 non-judge stages times 5 slots) | `test_native_selected_batch_runs_native_stages_after_the_c6_judge` |
| Helm rendering | all three stages render with the loadgen principal | `test_native_stage_values_render_through_helm_with_the_loadgen_principal` |
| Explicit CA file only on JWT-bearer | unchanged | `ProbeV2NativeAuthenticationTests` (not edited) |

## Verification record

Run in this session's container (Linux, .NET SDK 10.0.402 from the official installer, Helm 4.3.1 and kubeconform 0.8.0 downloaded with the SHA-256 values pinned in `infra/scripts/lifecycle/install-tools.sh`; the checks passed). Source: the working tree at base `831a2e7` plus the uncommitted changes of this branch.

| Command | Result |
|---|---|
| `./scripts/verify-dotnet.sh` | restore, build with warnings as errors, inventory, architecture and Enrichment tests passed; it stopped at `Parchwell.Kafka.Tests`: 477 of 478 passed. The failure is the Testcontainers-based `PhantomSpikeTests` setup (`NullReferenceException` in `ExperimentDockerAuthentication.get_Endpoint`, no Docker daemon here), the same failure the [issue 244 receipt](../issue-244/native-jwt-trust-anchor.md) recorded. The plan changes no C#. CI, with Docker, is the check |
| `python3 -m unittest discover -s infra/scripts/phase/tests` (with Helm) | 189 run, OK |
| `python3 charts/parchwell/tests/test_probe_v2_chart.py` | passed: 153 positive, 138 negative |
| `npm ci --ignore-scripts`, `npm run test:docs` | passed (0 failed) |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change; none in the changed documents |
| `git diff --check` | clean |

Without Helm on `PATH`, 125 of the phase tests fail with `helm` not found. That is an environment gap, not a regression; the run above has Helm.

## Cold review record

Filled in the pull request self-review record, which holds the reports; rows are added here only for rounds that review an earlier revision of this receipt.
