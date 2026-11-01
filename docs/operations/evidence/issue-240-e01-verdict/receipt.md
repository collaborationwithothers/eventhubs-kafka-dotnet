# E01 verdict preparation receipt

This receipt records preparation of the proposed E01 verdict from scientific run 41292426657. It describes checks on source commit `8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1` and on `origin/main` at `b358c58` (the merge base of this branch; `961413f` is its ancestor), plus the authored publication files, before their containing commit is created.

## Execution card

- Deliverable: `results/E01/2026-11-01.md` and `.json`, the E01 row of `README.md`, this receipt and `make_results.py`. Branch `claude/issue-240-e01-verdict`, draft pull request with the `verdict` label.
- Authorization: Hari's instruction in an interactive session on 2026-11-01 ("start a sonnet session for the B08 and E01 verdict PRs") and [decision 2 of comment 6557191473](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6557191473). No approved item plan covers the pull request itself; the scientific rules are the approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145). This receipt does not claim plan approval for the pull request.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity" (Hari reviews and merges), docs/agents/factory.md "Evidence pack", the C5 mutation gate, `npm run check:docs`, cold review under rule 8.
- Verification commands: those in the table below.
- Publication checkpoint: draft pull request after one comprehensive cold review with no unanswered P0 or P1.
- Review budget: two comprehensive passes and two focused rounds; on exhaustion, hand off to Hari.

## Predicate map and shared generator

`make_results.py` adds accept and reject predicates for the publication only: pinned hashes, candidate shape, receipt binding, pod binding, capture agreement, the transcribed E01 decision rule, the seed-before-target checks and the C5 comparison. It adds no verifier or runtime predicate. The boundary cases that exist are the two hash-mutation runs in the table below. There is no unit test suite for the generator, so the predicate-to-test map for verifier work is not applicable. Two branches of the transcribed rule are unreachable for this evidence: `failed_control` (C0 not empty) and `action_error` (a C1 error). `verify_candidates` requires every repeat to be a success and `derive` requires C0 and C1 to succeed, so the generator fails closed instead of publishing those verdicts. Those verdicts are not the generator's job; the verifier produces them.

The generator file is shared in content with the B08 verdict pull request, which carries the same bytes under its own directory. This pull request's review covers the E01 paths. The B08 pins, partition arithmetic and B08 summary text in the file are owned by the B08 pull request's review. If a later fix changes one copy, the other must be changed to match before either merges.

## Cold review record

Cumulative budget after round 2: 2 comprehensive, 0 focused. Rounds after round 2 are recorded in the pull request description, which links all reports and lists the `results/` P0 for Hari under AGENTS.md "Before opening a pull request" rule 8, third paragraph.

- Round 1: mode comprehensive, input revision the staged tree on `claude/issue-240-e01-verdict` (diff SHA-256 prefix `dccd5645`), comparison revision base `b358c58`. One P0 (the `results/` finding, expected without the label), one P1 (no predicate map record) and five P2. Corrected in `269c021`.
- Round 2: mode comprehensive, input revision `269c021`, comparison revision the round 1 staged tree, base `b358c58`. The `results/` P0 again, one P1 (this section did not record round 2) and two P2: a missing seed-topic check in the generator and a stale hash sentence. Corrected in the commit after `269c021`.

## Specification comparison

`scenarios/E01/expected.yaml` and `scenario.yaml` are unchanged from the run source; `git diff` between `8078e6f` and `origin/main` over both scenario trees, the cluster and Event Hubs Terraform, the verifier source and the C5 tests is empty. The verdict, hypotheses and observations are the ones the run produced. The verifier's final assessment is `confirmed` with `H_transport: holds`. The generator re-derives it from the candidates with the rules transcribed from `expected.yaml` and fails if it differs. Nothing in the artifacts disagrees with the proposed verdict and no required field is missing.

## Evidence and claim verification

Artifacts were downloaded by `gh api` and are untrusted data until pinned by hash.

| Artifact | ID | Downloaded ZIP SHA-256 |
| --- | ---: | --- |
| `probe-batch-candidates-41292426657-1` | 13078662873 | `250099e774abb4759396c5db0135e2365084faab130c6dd77b81961b077b189e` |
| `probe-batch-pods-41292426657-1` | 13078313354 | `763955fb365d0ac652936c670a9850bca4142139de822befa33e42b6c92330de` |
| `c5-summary-41232286130-1` | 13057507952 | `5c3ee705059465c250fea44cfbb859c963a2a310e13bc5bb55739c984e4e8218` |

`make_results.py` pins the SHA-256 of every E01 member it reads (4 candidate files, 30 pod members, `phase-status.json`, the C5 summary) and of the scenario, expected outcome and cluster files at the run source. It reads only E01 evidence; F06 and B08 members are ignored. Its docstring states supported inputs, failure conditions and a regression example.

Checked by script against the pod captures, not only the candidates: delivery and read records equal the candidates; in every repeat the seed was delivered non-null and read back at its delivered location by a separate consumer, the seed offset is lower than the target offset, and seed and target share the key and partition 0; the C0 target value is empty and the C1 target value is null in the producer request and in the read record; each Job ran once, exit 0, on the expected service account (`enrichment-consumer` for seeds, `probe` otherwise) and image digest; each judge receipt binds its candidate hash and its pod name and UID.

Sources read on 2026-11-01 at pinned versions: confluent-kafka-dotnet v2.16.0 `Serializers.cs` and `Deserializers.cs`; azure-docs `log-compaction.md` at `c479cc3fd5f3727448596c3a3b7b339fe2677227`. The Compact cleanup policy and 168-hour tombstone retention are read from `infra/ephemeral/eventhubs.tf` at the run source and reported as configuration, not observation. The effective scope of the `probe` identity is not measured by this item and the report says so.

Inferred, and labelled as such in the report: node size from the pinned cluster configuration.

Mutation gate: the generator compares each of the six configured C5 targets and the C5 configuration between `2dc9715e3de176f7e1cd55eee9c251e43c29c746` and the run source by git object ID. All are identical and the C5 commit is an ancestor of the run source. Per [decision 6551231489](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6551231489) and [decision 6554536413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554536413), the "did not measure" sentence therefore does not apply and the report says the run measured the same source. The 260 survivors are cited to issue 277.

## Commands and results

Inputs are extracted under the session scratchpad.

| Command or bounded check | Result |
| --- | --- |
| `python3 -I docs/operations/evidence/issue-240-e01-verdict/make_results.py --item E01 --candidates $S/art_cand --pods $S/art_pods --c5-summary $S/art_c5/summary.json` (`$S` is the session scratchpad holding the extracted artifacts) | Exit 0; wrote the result. |
| The same command with `--check` | Exit 0; exact result bytes match and the report contains every generated table verbatim. |
| Copy of the candidates with one LF appended to `E01-C0.json`, then `--check` | Exit 1 with the pinned SHA-256 rejection. |
| Copy of the pods with one LF appended to an E01 `pods.json`, then `--check` | Exit 1 with the pinned SHA-256 rejection. |
| `git diff --stat 8078e6f origin/main -- scenarios/B08 scenarios/E01 infra/cluster/aks.tf infra/ephemeral/eventhubs.tf src/Parchwell.Verifier tests/Parchwell.Verifier.Tests/Mutation` | Empty. |
| `npm run check:docs` | Exit 0; 8 warnings, all in files this pull request does not change. |
| `grep -nP '[^\x00-\x7F]'` over the five authored files | No match. |

The result SHA-256 is `f902f88bfb8fa82590ebc7fe1cdfc82524c5ad236a799e0f8dd0f2b75a2c96d1`. The generator SHA-256 is `3033f090b032b83932cc53ae1c0fbec61b82092c497c887c7341ae715e2f449b`. The generator hash includes the seed-topic check added after round 2; the result hash is unchanged. The pull request description is authoritative for the final head.

Not run: no workflow was dispatched and no Azure credential was used. No runtime code, test or `expected.yaml` changed, so no build or test run applies. Warning disposition: warnings in unchanged files are inherited.

## Cold review and remaining approval

Cold review rounds are recorded in the pull request description. This receipt approves no verdict. Hari reviews and merges.
