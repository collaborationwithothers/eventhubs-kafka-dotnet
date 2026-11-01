# B08 verdict preparation receipt

This receipt records preparation of the proposed B08 verdict from scientific run 41292426657. It describes checks on source commit `8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1` and on `origin/main` at `961413f`, plus the authored publication files, before their containing commit is created.

## Execution card

- Deliverable: `results/B08/2026-11-01.md` and `.json`, the B08 row of `README.md`, this receipt and `make_results.py`. Branch `claude/issue-240-b08-verdict`, draft pull request with the `verdict` label.
- Authorization: Hari's instruction in an interactive session on 2026-11-01 ("start a sonnet session for the B08 and E01 verdict PRs") and [decision 2 of comment 6557191473](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6557191473). No approved item plan covers the pull request itself; the scientific rules are the approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145). This receipt does not claim plan approval for the pull request.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity" (Hari reviews and merges), docs/agents/factory.md "Evidence pack", the C5 mutation gate, `npm run check:docs`, cold review under rule 8.
- Verification commands: those in the table below.
- Publication checkpoint: draft pull request after one comprehensive cold review with no unanswered P0 or P1.
- Review budget: two comprehensive passes and two focused rounds; on exhaustion, hand off to Hari.

## Predicate map and shared generator

`make_results.py` adds accept and reject predicates for the publication only: pinned hashes, candidate shape, receipt binding, pod binding, capture agreement, the transcribed B08 decision rule, the 0 to 31 metadata check, the partition arithmetic and the C5 comparison. It adds no verifier or runtime predicate. The boundary cases that exist are the two hash-mutation runs in the table below. There is no unit test suite for the generator; the predicate-to-test map for verifier work is therefore not applicable, and the generator's own failure conditions are listed in its docstring.

The generator file is shared in content with the E01 verdict pull request, which carries the same bytes under its own directory. This pull request's review covers the B08 paths; the E01 pins, rules and published E01 summary text in the file are owned by the E01 pull request's review.

## Cold review record

Cumulative budget after round 3: 2 comprehensive, 1 focused. One focused round remains.

- Round 1: mode comprehensive, input revision `f66d749`, comparison revision base `961413f`. Three P0 (the `results/` and README verdict finding, expected without the label), three P1 and three P2. Corrected in `0c915cd`.
- Round 2: mode comprehensive, input revision `0c915cd`, comparison revision base `961413f`, previous reviewed revision `f66d749`. One P1 (an untraceable example key count) and one P2. Corrected in `af572bb` by removing the example.
- Round 3: mode focused correction, input revision `af572bb`, comparison revision `0c915cd`, full pull request diff as context. The `results/` P0 again, and one P1 that this receipt did not record rounds 2 and 3, corrected by this section.

Rounds after round 3 are recorded in the pull request description, which links all reports and also lists the `results/` P0 for Hari under AGENTS.md "Before opening a pull request" rule 8, third paragraph. Specification sources are as listed in this receipt.

## Specification comparison

`scenarios/B08/expected.yaml` and `scenario.yaml` are unchanged from the run source; `git diff` between `8078e6f` and `origin/main` over both scenario trees, the cluster and Event Hubs Terraform, the verifier source and the C5 tests is empty. The verdict, hypotheses and observations are the ones the run produced. The verifier's final assessment is `confirmed` with `H_partition: holds`. The generator re-derives it from the candidates with the rules transcribed from `expected.yaml` and fails if it differs. Nothing in the artifacts disagrees with the proposed verdict and no required field is missing.

## Evidence and claim verification

Artifacts were downloaded by `gh api` and are untrusted data until pinned by hash.

| Artifact | ID | Downloaded ZIP SHA-256 |
| --- | ---: | --- |
| `probe-batch-candidates-41292426657-1` | 13078662873 | `250099e774abb4759396c5db0135e2365084faab130c6dd77b81961b077b189e` |
| `probe-batch-pods-41292426657-1` | 13078313354 | `763955fb365d0ac652936c670a9850bca4142139de822befa33e42b6c92330de` |
| `c5-summary-41232286130-1` | 13057507952 | `5c3ee705059465c250fea44cfbb859c963a2a310e13bc5bb55739c984e4e8218` |

`make_results.py` pins the SHA-256 of every B08 member it reads (2 candidate files, their 2 judge receipts, 18 pod members, `phase-status.json`, the C5 summary) and of the scenario, expected outcome and cluster files at the run source. It reads only B08 evidence; F06 and E01 members are ignored. Its docstring states supported inputs, failure conditions and a regression example.

Checked by script against the pod captures, not only the candidates: delivery and read records equal the candidates; the `partitioner` setting is `consistent_random` in all five C0 producers and `murmur2_random` in all five C1 producers; producer and reader metadata partitions are exactly 0 to 31; each Job ran once, exit 0, on the expected service account and image digest; each judge receipt binds its candidate hash and its pod name and UID.

Computed, not measured: CRC-32 2046850398 and Murmur2 2245682434 of `b08-synthetic-0` give partitions 30 and 2 of 32. The Murmur2 port is checked against the 11 non-NULL vectors of the 12 in librdkafka v2.16.0 `src/rdmurmur2.c`; the 12th is NULL with the same expected value as the empty key. Sources read on 2026-11-01 at pinned versions: librdkafka v2.16.0 `rdkafka_msg.c`, `rdmurmur2.c`, `rdcrc32.c`, `rdcrc32.h`, `CONFIGURATION.md`; azure-docs `apache-kafka-configurations.md` at `6da87e3c59251e962dd0b37dbd99ae364d767150`.

Inferred, and labelled as such in the report: node size from the pinned cluster configuration; that changing the partitioner can move a key to another partition (follows from the measured result for one key, not tested as a change on one hub); the report states only that one key was tested and does not claim that every key moves.

Mutation gate: the generator compares each of the six configured C5 targets and the C5 configuration between `2dc9715e3de176f7e1cd55eee9c251e43c29c746` and the run source by git object ID. All are identical and the C5 commit is an ancestor of the run source. Per [decision 6551231489](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6551231489) and [decision 6554536413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554536413), the "did not measure" sentence therefore does not apply and the report says the run measured the same source. The 260 survivors are cited to issue 277.

## Commands and results

Inputs are extracted under the session scratchpad.

| Command or bounded check | Result |
| --- | --- |
| `python3 -I docs/operations/evidence/issue-240-b08-verdict/make_results.py --item B08 --candidates DIR --pods DIR --c5-summary FILE` | Exit 0; wrote the result. |
| The same command with `--check` | Exit 0; exact result bytes match and the report contains every generated table verbatim. |
| Copy of the candidates with one LF appended to `B08-C0.json`, then `--check` | Exit 1 with the pinned SHA-256 rejection. |
| Copy of the pods with one LF appended to a B08 `pods.json`, then `--check` | Exit 1 with the pinned SHA-256 rejection. |
| `git diff --stat` between `8078e6f` and `origin/main` over scenarios, cluster and Event Hubs Terraform, verifier source and C5 tests | Empty. |
| `npm run check:docs` | Exit 0; 8 warnings, all in files this pull request does not change. |
| `grep -nP '[^\x00-\x7F]'` over the five authored files | No match. |

The result SHA-256 is `9a3e5ebbabe186712f168e652cfd1577292c451374960aa63a6e4f934afb6471`. The generator SHA-256 is `3033f090b032b83932cc53ae1c0fbec61b82092c497c887c7341ae715e2f449b`. The generator hash includes the seed-topic check added after round 3; the result hash is unchanged. The pull request description is authoritative for the final head.

Not run: no workflow was dispatched and no Azure credential was used. No runtime code, test or `expected.yaml` changed, so no build or test run applies. Warning disposition: warnings in unchanged files are inherited.

## Cold review and remaining approval

Cold review rounds are recorded in the pull request description. This receipt approves no verdict. Hari reviews and merges.
