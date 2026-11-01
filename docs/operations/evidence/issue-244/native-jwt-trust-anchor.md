# Issue 244: native-jwt-trust-anchor receipt

This pull request repeats only the native JWT-bearer arm of the merged diagnostic, with librdkafka's HTTPS trust anchor set explicitly. It runs nothing live and publishes no F02 result. The approved plan is Revision 1, implemented as written with its two `apply in implementation` P2s.

Base revision: `8078e6f`
Branch: `claude/issue-244-native-jwt-trust-anchor`

## Authority and scope

Selected plan: [issue 244 plan comment 6555876576](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6555876576), Revision 1, `updated_at` 2026-11-01T09:05:00Z. It is the newest plan, and its pull request is not merged.

Recognized later decisions, in creation order:
1. [Decision 6547412663](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412663): the Revision 1 answers of the merged diagnostic. Answer 2 (three native arms) is superseded for the phase schedule by item 3.
2. [Decision 6548960917](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6548960917): native stages after the C6 judge and the GUID rule.
3. [Approval 6556147592](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6556147592), posted at 09:32:16Z, after the plan's latest edit at 09:05:00Z. It names Revision 1 and its `updated_at`, adopts the scope, approves the schedule change and the explicit path, and orders the check's two P2s applied. It is given in light of the [plan check 6556022257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6556022257).

The implementing tool is Claude Code ([comment 6547412843](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412843), before `ready-for-agent`).

Nothing here authorizes a dispatch, an Azure action or spend.

## Execution card

- Deliverable: plan changes 1 to 3, the listed tests, this receipt and `items/244/state.md`.
- Current authorization: approval 6556147592, for implementation and offline verification only.
- Mandatory gates: the plan's "Acceptance commands"; cold review under AGENTS.md "Before opening a pull request" rule 8; hosted Codex review and CI. No protected path is touched, so the merge rule decides whether the agent may merge.
- Verification commands: those in "Verification record".
- Earliest publication checkpoint: after the first comprehensive cold review and its corrections.
- Review budget: rule 8's cumulative budget, 2 comprehensive and 2 focused. Exhaustion action: hand off to Hari.

## Specification comparison

| Plan change | Where |
|---|---|
| 1 Native settings | `src/Parchwell.LoadGen/SingleActionV2/ProbeV2NativeAuthentication.cs`, `case "jwt-bearer":` |
| 2 Stage graph | `infra/scripts/phase/probe_v2.py` (`NATIVE_STAGES`, `maximum_clients`) |
| 3 Documentation | `docs/operations/authentication-lifecycle-observation.md` |
| Tests | `ProbeV2NativeAuthenticationTests.cs`, `ProbeV2LifecycleTests.cs`, `test_probe_v2.py` |

Plan check P2 4: the operations doc says that the file's absence from the image is inferred, using the check's replacement text. P2 5: the doc says the workflow input's description still says three producers and that the workflow is not changed here. The plan's line numbers matched `main` at `8078e6f`; edits were placed by the anchor text the plan quotes.

The three Python assertions narrowed to `nativejwt` (tuple, `modes` twice, `seen`) follow the behaviour change, as the plan states; a new negative test covers the two removed stages. Beyond the plan's text, the operations doc's second paragraph carries four source citations (librdkafka `rdkafka_conf.c` and `rdkafka.c`, Confluent.Kafka `SafeKafkaHandle.cs`, all at v2.16.0), added under AGENTS.md "Before opening a pull request" rules 1 and 2 after cold review round 1. No `results/`, `expected.yaml`, verifier, judge, `*.csproj`, chart, workflow or package change. `docs/blueprint.md`, AGENTS.md and factory.md are unchanged.

## Predicate map

| Predicate | Below / at / above | Test |
|---|---|---|
| `jwt-bearer` sets `https.ca.location` | exact key set including the new key; the two IMDS modes do not contain it | `ProbeV2NativeAuthenticationTests` |
| Recorded value | the path is recorded unredacted | `ProbeV2LifecycleTests` |
| Missing file fails construction | a nonexistent path throws `InvalidOperationException` with librdkafka's text; the existing real-producer test builds with the real path | `ProbeV2NativeAuthenticationTests` |
| Native stage list | flag true gives only `nativejwt`; flag false unchanged; `nativeimds` and `nativeimdsnode` rejected as `v2_stage` | `test_probe_v2.py` |
| Batch counts | 6 Jobs, `maximum_clients` 20, 20 admitted slots | `test_probe_v2.py` |

The IMDS modes remain accepted by the worker and chart, unchanged by design (plan change 2), so no test of them changes except the new `DoesNotContain` assertion.

## Verification record

Run in this session's container (Linux, .NET SDK 10.0.402 from the official installer, Helm 4.3.1 and kubeconform 0.8.0 downloaded with the SHA-256 values pinned in `infra/scripts/lifecycle/install-tools.sh`). Source: source and test files as in commit `123d473` (the first commit on the branch); the later correction commits change only documentation, and `npm run check:docs` was rerun after them.

| Command | Result |
|---|---|
| `./scripts/verify-dotnet.sh` | restore, solution build with warnings as errors, inventory check, architecture tests (14) and Enrichment tests (56) passed; it stopped at `Parchwell.Kafka.Tests`: 477 of 478 passed, the one failure is `PhantomSpikeTests`, which starts Testcontainers and fails with no Docker daemon in this container |
| `dotnet test` for LoadGen, Scenarios, Verifier (Release, run directly because the script stops at the first failure) | Scenarios 239 passed. LoadGen 757 of 761 passed; the four failures are the local-Kafka and blob integration tests that need Docker. Verifier tests with "Component" in the name also fail here for the same reason; they were not compared with a clean `main`, and CI (Docker available) is the check |
| `ProbeV2NativeAuthenticationTests` and `ProbeV2LifecycleTests` | 46 of 46 passed, including the new missing-path test and the real-producer test with the real CA path |
| `python -m unittest discover -s infra/scripts/phase/tests` (with Helm) | 186 run; 1 failure and 1 error, both `test_probe_v2_access.ProductionReaderTests.test_both_streams_capped`, the same pair the [previous receipt](native-authentication-diagnostic.md) recorded on a clean `origin/main` in this container |
| `python3 charts/parchwell/tests/test_probe_v2_chart.py` | passed: 153 positive, 138 negative |
| `npm ci --ignore-scripts`, `npm run test:docs` | passed (0 failed) |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124`, `issue-140`, `issue-211`) |
| `git diff --check` | clean |

Tests that need Docker or a local Kafka run in CI only. The runner image is not checked here, so the first CI run is the check that `/etc/ssl/certs/ca-certificates.crt` exists there.

Authority refresh before the first push: issue body `updated_at` 2026-11-01T09:32:16Z, 26 comments, newest 6556147592, all unchanged since the catch-up; `origin/main` is `8078e6f`.

## Cold review record

Cumulative counts at the time of writing: comprehensive 2 of 2, focused 1 of 2 reported (round 3) and the second (round 4) pending. The reports of rounds 3 and 4 are recorded in the pull request self-review record, not here, because they review this receipt.

| Round | Mode | Input revision | Comparison revision | Findings | Disposition |
|---|---|---|---|---|---|
| 1 | comprehensive | `123d473` | `8078e6f` | P1: the new librdkafka claims in the operations doc had no citation. P2: the receipt did not name the verified commit | Both fixed in `dac7bea`: four pinned citations added; the verification record names `123d473` |
| 2 | comprehensive (consolidated re-review) | `dac7bea` | `8078e6f` | P1: this receipt had no cold-review record. P2: the receipt did not say the doc adds citations beyond the plan's text | Both fixed in the commit that adds this section: this record, and the note in "Specification comparison" |
| 3 | focused | `04ffbb2` | `dac7bea` | P1: this receipt did not name the mode and revisions of the review it was submitted for | Fixed in the commit that adds row 4 |
| 4 | focused (last of 2) | the commit that adds this row | `04ffbb2` | Pending; recorded in the pull request self-review record | Not applicable |

Round 1 and round 2 reported no missing, partial or incorrect requirement and no scope creep against the plan, plan check 6556022257 and approval 6556147592. Their pre-existing observations need no change here: the assessment comment 6555801700 has a `created_at` later than the comment the state file covers (a harmless re-read); `probe_v2.py` keeps `maximum_clients` hard-coded as the plan chose; and the protected workflow input description is recorded in the operations doc.
