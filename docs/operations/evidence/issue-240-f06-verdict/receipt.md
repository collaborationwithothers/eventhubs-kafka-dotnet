# F06 verdict preparation receipt

This receipt records preparation of the proposed F06 verdict from scientific rerun 41302009289. It describes checks on source commit `961413f3fc81ec1b58b3fecc7f9e5122a8a21010` and on `origin/main` at `40ba3b8`, plus the authored publication files, before their containing commit is created.

## Execution card

- Deliverable: `results/F06/2026-11-01.md` and `.json`, the F06 row of `README.md`, this receipt and `make_results.py`. Branch `claude/issue-240-f06-verdict`, draft pull request with the `verdict` label.
- Authorization: Hari's instruction in an interactive session on 2026-11-01 ("start a sonnet session for the F06 verdict PR") and [decision 6557921132](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6557921132). The rerun was authorized by [decision 6557191473](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6557191473). No approved item plan covers the pull request itself; the scientific rules are the approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145). This receipt does not claim plan approval for the pull request.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity" (Hari reviews and merges), docs/agents/factory.md "Evidence pack", the C5 mutation gate, `npm run check:docs`, cold review under rule 8.
- Verification commands: those in the table below.
- Publication checkpoint: draft pull request after one comprehensive cold review with no unanswered P0 or P1.
- Review budget: two comprehensive passes and two focused rounds; on exhaustion, hand off to Hari.

## Predicate map and generator

`make_results.py` adds accept and reject predicates for the publication only: pinned hashes, scenario matrix equality, candidate shape, receipt binding, pod binding, capture agreement, seed-location agreement, the permission-audit projection checks, the transcribed F06 decision rule, the first-dispatch counts and the C5 comparison. It adds no verifier or runtime predicate. There is no unit test suite for the generator, so the predicate-to-test map for verifier work is not applicable. Boundary cases that were run are in the table below: one hash mutation and five single-line mutations of a copy of the generator (a changed positive set, changed intended grants, a changed admitted-slot expectation, a changed key rule and a changed first-dispatch count). Each made the generator exit 1 with the matching message.

## Specification comparison

`scenarios/F06/expected.yaml` and `scenario.yaml` are unchanged from the run source; `git diff` between `961413f` and `origin/main` over the scenario trees, cluster and ephemeral Terraform, verifier source and C5 tests is empty. The verdict, hypotheses and observations are the ones the run produced. The verifier's final assessment is `undocumented` with `H_positive_scope: holds` and `H_negative_surface: recorded_only`. The generator re-derives it from the candidates with the rules transcribed from `expected.yaml` and fails if it differs. Nothing in the artifacts disagrees with the proposed verdict and no required field is missing.

The permission-audit requirement of the plan (a complete audit whose entries equal the intended table, before any F06 client, on the first admission within 60000 ms) is checked by script against `probe-v2-access-check.json` and `phase-status.json`: status `complete`, no reasons, no unsupported entries, four entries equal to the four true cells of the table, projection hash equal to the phase status and to every candidate's diagnostics, submission age 793.7 ms. Not checkable from public artifacts, and said so in the report: the private binding of aliases to principals and the persistent-output cross-check.

First dispatch: `phase-status.json` of run 41292426657 shows all 21 F06 client stages `not_attempted` with `permission_evidence_incomplete`, an incomplete audit and no valid F06 candidate. The report cites it as provenance only and publishes none of its content as a result, under GOVERNANCE "Verdict integrity".

## Evidence and claim verification

Artifacts were downloaded by `gh api` and are untrusted data until pinned by hash.

| Artifact | ID | Downloaded ZIP SHA-256 |
| --- | ---: | --- |
| `probe-batch-candidates-41302009289-1` | 13082448520 | `2a6d57607688d179a5db56be7d3d0eead4fa70991a3bb36ceec03ecbb27f0ae9` |
| `probe-batch-pods-41302009289-1` | 13082618179 | `db387713e617835c4821ad26acbf6b36840049d064d9337740509ef0bb51611b` |
| `probe-batch-pods-41292426657-1` (first dispatch, `phase-status.json` only) | 13078313354 | `763955fb365d0ac652936c670a9850bca4142139de822befa33e42b6c92330de` |
| `c5-summary-41232286130-1` | 13057507952 | `5c3ee705059465c250fea44cfbb859c963a2a310e13bc5bb55739c984e4e8218` |

`make_results.py` pins the SHA-256 of all 24 candidate and receipt files, all 99 pod members, the three top-level pod-artifact files, the first dispatch's phase status, the C5 summary, and the scenario, expected outcome, cluster, Event Hubs and identity Terraform files at the run source. Its docstring states supported inputs, failure conditions and a regression example.

Checked by script against the pod captures, not only the candidates: every request (topic, explicit partition 0, key and value), delivery and error; each independent read equals the delivered location, key and value; each seed is read at its location by the probe and by the second identity; C2 and C4 read stages were skipped for want of a delivery; each negative configuration has no delivery and no read record; each Job ran once, exit 0, on the expected service account and image digest; each judge receipt binds its candidate hash and its pod name and UID; the scenario matrix equals the transcribed one.

Sources read on 2026-11-01 at pinned versions: librdkafka v2.16.0 `src/rdkafka.h` (SHA-256 `09f5d9bf81240b985d1adb83d1cb4e84faa7d0591de01478b2a47e776550cd1a`) and `src/rdkafka.c` (`57ea67830c2d41089258c400b46f902e2d55b45a3ffe125d3010e5c0be0b8a67`); confluent-kafka-dotnet v2.16.0 `ErrorCode.cs` (`99af8c1567302cf14d897c4d00129ba55cf211be7c60c74ec36bd45ea88490b8`), for the meaning of error code 29; librdkafka v2.16.0 `src/rdkafka_fetcher.c` L305-L306 (SHA-256 `f9a8301b7437eaafac1268f20902af727e3653e5f49ffefaafd97e9365b48427`), for the `Fetch from broker` text; azure-docs `articles/event-hubs/authorize-access-azure-active-directory.md` at `84ce263e4a1bdd0ce29cb5afde8ae98d1ad4068f`, for scopes; `infra/ephemeral/identity.tf` at the run source, for the grants. `STATISTICS.md` at v2.16.0 line 87 was read and lists the broker `source` values (learned, configured, internal, logical); the counting rule itself (state UP, internal excluded) is the verifier contract's, not that line's.

Not verified by me: the ephemeral environment facts (SKU, capacity, region, partition counts, timestamps) come from reading the batch job log of the run, which is not a pinned input; the permission-check log line and the `aks_stopped` line were read from job logs. Node size is inferred from the pinned cluster configuration, and the report says so.

Mutation gate: the generator compares each of the six configured C5 targets and the C5 configuration between `2dc9715e3de176f7e1cd55eee9c251e43c29c746` and the run source by git object ID. All are identical and the C5 commit is an ancestor of the run source. Per [decision 6551231489](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6551231489) and [decision 6554536413](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/275#issuecomment-6554536413), the "did not measure" sentence therefore does not apply. The 260 survivors are cited to issue 277. No mutation run was started.

## Commands and results

Inputs are extracted under the session scratchpad.

| Command or bounded check | Result |
| --- | --- |
| `python3 -I docs/operations/evidence/issue-240-f06-verdict/make_results.py --candidates DIR --pods DIR --c5-summary FILE --first-dispatch-status FILE` | Exit 0; wrote the result. |
| The same command with `--check` | Exit 0; exact result bytes match and the report contains every generated table verbatim. |
| Copy of the candidates with one LF appended to `F06-C0.json`, then `--check` | Exit 1 with the pinned SHA-256 rejection. |
| Five single-line mutations of a copy of the generator, then `--check` | Exit 1 each, with the matching rejection. |
| `git diff --stat` between `961413f` and `origin/main` over scenarios, cluster and ephemeral Terraform, verifier source and C5 tests | Empty. |
| `npm run check:docs` | Exit 0; 8 warnings, all in files this pull request does not change (rerun after the round 1 corrections). |
| `grep -nP '[^\x00-\x7F]'` over the authored files | No match. |

The result SHA-256 is `328a8e004f3fd612fe32789951b2166b2800f65e4db2ba8fc34aab24f3d0af67`. The generator SHA-256 is `7b81b9d13eae4f0cc29000680d6aacabd666fd048149c84322b57eacb781a9c0`. The pull request description is authoritative for the final head.

Not run: no workflow was dispatched and no Azure credential was used. No runtime code, test or `expected.yaml` changed, so no build or test run applies. Warning disposition: warnings in unchanged files are inherited.

## Cold review record

Cumulative budget after round 3: 2 comprehensive, 1 focused (the prose-only P2-correction round under rule 8).

- Round 1: mode comprehensive, input revision `af859df` (tree equal to the reviewed staged tree), comparison revision `7d51a8a`. Three P0 (the `results/` and README verdict finding, expected without the `verdict` label), three P1 and two P2. All P1 and P2 were verified against their sources and corrected: the full public audit projection is now embedded verbatim in the result (the plan says to include it); the Learn-page wording now lists its Kafka references; the `Fetch from broker` text is cited to `rdkafka_fetcher.c` and the consumer-group claim was replaced by a statement about configured grants; the survivor sentence no longer says they have no disposition; a dead check in the generator was removed with a comment naming the checks that cover it. `origin/main` was merged (now `40ba3b8`); the configured C5 targets, C5 configuration, scenarios, cluster and ephemeral Terraform are unchanged between `961413f` and it.
- Round 2: mode comprehensive, input revision `e84788b`, comparison revision `40ba3b8`, previous reviewed revision `af859df`. The same three expected P0 (`results/` and README verdict, label not visible in a diff), no P1, four P2. Fixed: the table column header that attributed grants to the scenario (now "Grant by design"), and a generator comment that named a key that does not exist. Not fixed, with reasons: the survivor sentence (decision 6557335536 on issue 277, point 1 and 2, bars a pack from recording one child's dispositions; one later `verdict` pull request updates all packs), and the published key `boot_identity_clock` (renaming it changes result data, and rule 8 allows only prose-only P2 fixes in this round).
- Round 3: mode focused P2-correction, input revision the head containing these fixes, comparison revision `e84788b`, full pull request diff as context. Reports only P0 or P1. Its result is recorded in the pull request description.

Later rounds are recorded in the pull request description, which links all reports and lists the `results/` P0 for Hari under AGENTS.md "Before opening a pull request" rule 8, third paragraph.

## Remaining approval

This receipt approves no verdict. Hari reviews and merges.
