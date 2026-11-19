# C03 verdict contract-grade plan

Revision: 2. Base commit: `main` at `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`.

This plan writes the C03 evidence pack and verdict pull request from full run 44562323424, the one C03 run on `main` whose two phases are both `complete_valid` with complete archives. Both hypotheses that decide the verdict hold in all five repeats of each configuration, so the expected outcome selects `confirmed`. The README row and the report state, in plain words, that heartbeats and the poll limit are two separate timers, as Hari decided on #499.

Item: C03, processing longer than `max.poll.interval.ms`, with pause and resume (K-55) and no commit on partitions lost (K-28) folded in [issue #511; parent entry 8 of #499]. Workload data is synthetic. Every number below is copied from a pinned run artifact, a pinned log or a cited source, or computed from them by script on 2026-11-19; no number is an estimate unless labelled.

## 1. What changed

Revision 2 applies the [plan check of revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022418697):

- Finding 1: librdkafka v2.16.0 stops sending heartbeats once `max.poll.interval.ms` is exceeded (`src/rdkafka_cgrp.c` lines 3959 to 3962 and 5767), and the pinned sources do not show that `Consume` is the only call that resets the poll timer. The plain-words paragraph and the H1 line of section 4.4, the README row of section 4.5 and section 4.6 points 1 and 2 are rewritten: heartbeats keep the member in the group until the poll limit is exceeded, and a `Consume` call resets the poll timer. This changes scientific text, so revision 2 needs a new check.
- Finding 2 (`apply in revision`): the check's literal texts are applied to section 7 (case `h1_takeover_at_threshold` now expects `FAIL G9`) and section 4.4 (the takeover range is rendered from the data).
- Finding 3 (`apply in revision`): the check's literal text is applied to section 4.4 item 5 (pod log and `pods.json` links for every Job of each repeat and the judge).
- The `apply in implementation` P2: the check's literal receipt item for run 44248514485 is applied to section 4.7.
- Other non-blocking points: section 4.6 point 2 cites the pinned classic group protocol in `BaselineSettingsCatalog.cs` line 145 instead of inferring it; section 3 cites rule 8's paragraph by its opening words; section 4.4 item 6 cites the log lines by number and quotes none of them, because they carry ANSI escapes; the H3 line names its evidence source.
- Section 2 records the `Tool: Claude` comment posted after revision 1.

## 2. Goal and authority

Goal: publish C03's proposed verdict with the evidence pack that docs/agents/factory.md "Evidence pack" requires, from run 44562323424 only, so Hari can approve or reject it.

Authority, in creation order. No item state file exists for #511 on `main` at the base commit.

1. [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511), created 2026-11-15T13:50:15Z, `updated_at` 2026-11-15T13:50:15Z: "Verdict pull request for C03 from the batch run of entry 5. Its README row and report state, in plain words, the distinction between heartbeats and the poll limit (decision 6894564479). Verdict pull requests go to Hari." It names `Parent items: #499`, `Split entry: 8 (C03 verdict)`, `Predecessor PRs: the pull request of #508 (entry 5)`, and asks the plan to read [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135) and the #499 decisions [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) and [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673). It names Claude for planning and Codex for implementation, with the Claude review gate of decision 6894587233.
2. [Decision 6895538971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-6895538971), 2026-11-15T14:03:40Z, adopts [standing decision 6895535291 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): grilling answers adopted in advance, plan approval on a check with `Blocking findings: none` and "Open questions for Hari" `None`, recorded by the control session; not covered: expected outcomes and verdicts, protected-path merges and the `ready-for-agent` label.

The decisions the issue body names apply through the body. What this plan relies on from them:
- 6894534441 point 3: K-55 and K-28 are tested with C03; K-47 is "Dropped from this batch and published as not tested". Point 4: Codex implements.
- 6894564479 point 2: "When the C03 result is published, its README row and its report state this distinction in plain words." The distinction: heartbeats from librdkafka's background thread keep the member alive against `session.timeout.ms` whether or not the application calls `Consume`; `max.poll.interval.ms` needs `Consume` calls. The decision requires the background-thread claim to be verified against librdkafka v2.16.0; section 4.6 does that.
- 6894587233 point 2 and 6894614673 point 2: Codex implements and each Codex pull request gets the Claude review gate of [decision 6633528258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633528258) point 2.

Other #499 decisions (6895299617, 6895328446, 6895343227, 6899939624, 6926376436, 6958521790) are not linked and adopted by a decision on #511, so under docs/agents/factory.md "Item lifecycle" step 3 this plan relies on none of them. Their substance that matters here is already merged in ADR-022 and `scenarios/C03/expected.yaml`. The merge method decision 6958521790 is irrelevant: the verdict pull request goes to Hari (AGENTS.md GOVERNANCE "Verdict integrity").

Predecessors: #508's two pull requests, [545](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/545) and [547](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/547), are merged; `items/508/state.md` at the base commit names the approved plan and its decisions. The implementer verifies both merged before starting (factory.md "Item lifecycle" step 3).

Design authority on `main` at the base commit: `scenarios/C03/expected.yaml` (SHA-256 `71ffd90fa84d837bfe173244341388bca0cd0cb2f86bfc963b4f5aaad860b68f`), `scenarios/C03/scenario.yaml` (`2b6da0fff85d6ebf124e30fa5d341d4d88d1db8b02b30b6d7a29422dca5e9e31`), `scenarios/profiles/c03-consumer.yaml` (`721d5f2fec07a066581ffa21ad3925dfba824457e805f59a3a1b6a0d7483fa65`), all by `sha256sum` on 2026-11-19; docs/blueprint.md ADR-022 (from line 736); docs/operations/verifier-contract.md line 128 (consumer block and `membership`); docs/agents/factory.md "Evidence pack".

Candidate selection: #511 is a child of the #499 split, entry 8, with no plan comment yet. Its one pull request is named `C03 verdict`. It is one coherent piece: one evidence pack for one run.

Implementing tool: the issue body's decisions name Codex (6894534441 point 4, 6894614673 point 2). Under standing decision 6895535291 point 5 the control session asks Hari before he applies `ready-for-agent`. This plan does not decide the tool. After revision 1, a comment headed `Tool: Claude` ([7022347026](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022347026), 2026-11-19T21:11:47Z, body exactly `Tool: Claude`) was posted on the issue. It is not a `## Decision` comment; under docs/agents/factory.md "Item lifecycle" step 2 a comment naming the tool before `ready-for-agent` is applied selects the tool, and the implementing session determines it then.

## 3. Scope

Files allowed to change, and only these six:

| Path | Change | Protected |
|---|---|---|
| `results/C03/2026-11-19.json` | new | yes, `/results/` |
| `results/C03/2026-11-19.md` | new | yes, `/results/` |
| `README.md` | line 32, the C03 row, only | yes, `/*.md` |
| `docs/operations/evidence/issue-511-c03-verdict/make_results.py` | new | no |
| `docs/operations/evidence/issue-511-c03-verdict/receipt.md` | new | no |
| `items/511/state.md` | new | no |

The date is the run's UTC date, 2026-11-19 (run `created_at` 2026-11-19T16:06:27Z), as in the earlier verdicts, for example `results/B10/2026-11-15.md` for run 43689822806 created 2026-11-15T07:24:39Z.

Size estimate (estimate, 2026-11-19; basis: the B10 precedent at `docs/operations/evidence/issue-492-b10-verdict/`, whose generator is 1162 lines and receipt 68 lines by `git show --stat e5fe436`, and this plan's smaller rule set, about half of B10's checks): about 600 lines for `make_results.py`, 50 for `receipt.md`, 200 for the report, 25 for the state file and 1 changed line in `README.md`. The results JSON is generator output and is not estimated by hand. Six files.

Paths that must not change: `scenarios/` (all), `src/`, `tests/`, `charts/`, `infra/`, `scripts/`, `.github/`, `docs/blueprint.md`, `docs/agents/`, `docs/operations/` outside the new evidence folder, every other path under `results/` and `items/`. No workflow is dispatched. No expected outcome, decision rule, verifier output or candidate is edited (AGENTS.md GOVERNANCE "Verdict integrity").

The results files are generator output. docs/agents/pull-requests.md rule 8 says "Agents commit no program output"; its paragraph beginning "Some Code Review Rules flag work that only Hari can approve" keeps such work only if "the verdict step requires it", which it does here, as in every earlier verdict pull request. The pull request description lists the protected-path and results-file findings under that paragraph and leaves the merge to Hari.

## 4. External contracts

### 4.1 Inputs

The generator reads only these inputs from one directory `$IN`, each pinned by SHA-256. Each ZIP was downloaded on 2026-11-19 through `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip`, and each ZIP hash equals the artifact API `digest`. Logs came from `gh api .../actions/jobs/<id>/logs`.

| File in `$IN` | Source | SHA-256 |
|---|---|---|
| `c03-C0-candidate.zip` | artifact 14557021737, `c03-C0-candidate-44562323424-1` | `f264f1a0077ccc97650163db6a9809a0442d1d3936185f5016f540c7ed7de11b` |
| its member `candidate.json` | | `2cf06532f472cb2bf0fc8b4f71ba0f23ace3aec0bb69585b153ca6de68540baf` |
| `c03-C1-candidate.zip` | artifact 14566219424, `c03-C1-candidate-44562323424-1` | `b104889647747d834724353e95db86c5f26f90086d5c59c2969f5cb9a8891c53` |
| its member `candidate.json` | | `2052d4eed484e12224919fcf151f7dd10b8b2b3d7387ae2026cd6b2733fd6d96` |
| `c03-C0-archive.zip` | artifact 14558051290, `c03-C0-archive-44562323424-1` | `ecb35b70d1c0761ff312af7f76abdebcd00617e41eba17a72be5917d8a06b15c` |
| its member `archive-receipt.json` | | `de9e19a729749881311576f5e852be929e69b28307a319941e34b98deb19a630` |
| its member `archive-manifest.json` | | `19acf2f9ca967b1778cd75a474a9224b99e8c0d1f069f61cfa377ffedd450a10` |
| `c03-C1-archive.zip` | artifact 14568811998, `c03-C1-archive-44562323424-1` | `507565ac9b104a296810822685fe39a77ad43a6f258eca50d7ad381b437cd394` |
| its member `archive-receipt.json` | | `8bf3795a069e022cfff33350834fdfa1773f72f2465686d1f9a1ca6f2373b0a6` |
| its member `archive-manifest.json` | | `388adf58f4d7e6d382bc7dd2bf4d77a7f977bc48300a431883068cb89d832f26` |
| `c03-C0-pods.zip` | artifact 14556692046, `c03-C0-pods-44562323424-1` | `5680e14ded810a42610a80d06768f4ffb4fdd0ea3c6c4d4bf98989a9e6b205d6` |
| its member `phase-status.json` | | `ee300acc35eb319b47125545f0702706240c99a961cec7ebc853a944ff2c6163` |
| `c03-C1-pods.zip` | artifact 14567423531, `c03-C1-pods-44562323424-1` | `2e92c81db1194c38eb799bf896305ebf4e60acaef358cf38e4ed33d3fe726e13` |
| its member `phase-status.json` | | `80efd71bac8aba1431935e868cbee528aa9d91e28df65bba6e7878adf288bc89` |
| `probe.log` | job 135694703105 (`probe`, ran C0) | `8aed0608dc5d8381ba8a933a1d793375f663ce412bc546e884643f596852d84e` |
| `full-C3.log` | job 135754218668 (`full-C3`, ran C1) | `357f8f9dd65d45f98a340c7c99b57112528aefa0e305b251349bbf85239e593c` |
| `c5-summary.zip` | artifact 14477182821, `c5-summary-44402419383-1` | `bc72e78858246b9e560587fcf191c067cbf0746d5b09c8b0e8dda4dda56c03f7` |
| its member `summary.json` | | `55727369b6c9f770c3d3327f978d3f5e268d368319788e183efaa018996b9410` |
| `scenarios/C03/expected.yaml` | working tree, equal to the base commit | `71ffd90fa84d837bfe173244341388bca0cd0cb2f86bfc963b4f5aaad860b68f` |

Each archive receipt's `manifest.sha256` equals its manifest member hash above (C0 `19acf2f9...`, C1 `388adf58...`). The generator reads the pods members `<job>/pods.json` for the consumer subject and witness of each repeat by path inside the ZIP, covered by the ZIP hash. Artifacts expire 2027-02-17T16:06:29Z (artifact API `expires_at`); the report states that date. The job logs show phase mapping: `probe` is C0 and `full-C3` is C1, as the phase-status members' `phase` fields and the job times show (probe 16:33:09Z to 17:36:33Z; full-C3 19:01:22Z to 20:00:09Z).

If any pinned hash differs from a fresh download, the implementer stops under section 11.

### 4.2 Pinned outcomes

The generator recomputes each rule of `scenarios/C03/expected.yaml` from the candidates, exactly as written there, and requires these results (G3):

| Rule | Recomputed result |
|---|---|
| `configurations.C0.H1_poll_limit_loses_partitions` | `holds` |
| `configurations.C0.H3_no_commit_on_lost` | `holds` |
| `configurations.C0.consumer_loss` (recorded only) | `[0, 0, 0, 0, 0]` |
| `configurations.C1.H2_pause_keeps_partitions` | `holds` |
| `configurations.C1.consumer_loss` (recorded only) | `[0, 0, 0, 0, 0]` |
| `verdict` | `confirmed` |

Each hypothesis has three outcomes, `holds`, `contradicted` and `inconclusive`, evaluated as: `holds` when its `holds_when` is true; else `contradicted` when its `contradicted_when` is true; else `inconclusive`. "All 5 repeats complete_valid" means exactly repeats 1 to 5, each with candidate repeat `status` and `consumer.status` both `complete_valid`. The verdict is `confirmed` when H1 and H2 both hold, `contradicted` when either is contradicted, and otherwise `no_verdict`.

G3 also requires these preconditions of the published sentences in section 4.4: in every C0 repeat `slow_processed_rows` is 2 and `counts.processed_again` is 1 (so the one message processed again is the slow message), `subject_revoked_rows` is 0; and in every C0 and C1 repeat the consumer subject and witness `pods.json` show the same `spec.nodeName` and a terminated container exit code 0.

### 4.3 `results/C03/2026-11-19.json`

Written as `json.dumps(document, indent=2, ensure_ascii=True) + "\n"`. Top-level keys exactly `publication` and `candidates`, in that order. `candidates` has exactly `C0` and `C1`, each JSON-equal to its pinned `candidate.json` member. `publication` has exactly these keys, in this order, with these values; `<...>` marks a value the generator copies from the named input.

```
item: "C03"
workload: "synthetic"
run: {workflow_run_id: 44562323424, attempt: 1, event: "workflow_dispatch", ref: "main",
      head_sha: "a1cf429dc7a57827c2a3d5bb658ae290f5ea9145", created_at: "2026-11-19T16:06:27Z",
      conclusion: "success", run_id: "c03-44562323424-1"}
expected_outcome: {path: "scenarios/C03/expected.yaml", sha256: "71ffd90fa84d837bfe173244341388bca0cd0cb2f86bfc963b4f5aaad860b68f"}
proposed_verdict: "confirmed"
rule: "scenarios/C03/expected.yaml verdict.confirmed_when: H1 holds and H2 holds"
hypotheses:
  H1_poll_limit_loses_partitions: {configuration: "C0", result: "holds"}
  H2_pause_keeps_partitions: {configuration: "C1", result: "holds"}
  H3_no_commit_on_lost: {configuration: "C0", result: "holds", separate_line: true}
consumer_loss: {C0: [0, 0, 0, 0, 0], C1: [0, 0, 0, 0, 0], mode: "recorded_only"}
c0_takeover_microseconds: [60848988, 60905286, 60716276, 60553624, 62991074]
takeover_threshold_microseconds: 45000000
scaled_limit: <expected.yaml assessment_rules.scaled_limit, verbatim, whitespace folded as YAML folds it>
not_tested: {K-47: <expected.yaml not_tested.K-47.reason, verbatim>}
archives: {C0: {archive_complete: true, gaps: [], manifest_sha256: "19acf2f9ca967b1778cd75a474a9224b99e8c0d1f069f61cfa377ffedd450a10"},
           C1: {archive_complete: true, gaps: [], manifest_sha256: "388adf58f4d7e6d382bc7dd2bf4d77a7f977bc48300a431883068cb89d832f26"}}
consumer_nodes: {C0: <list of 5 node names, one per repeat>, C1: <list of 5>}
broker_connection_counts: <per configuration, per repeat: {repeat, role, maximum, first_post_drain, lower_bound} copied from each repeat's broker_count>
environment: {namespace_sku: "Premium", processing_units: 1, region: "uksouth",
              node_size: "Standard_D4ds_v5", node_size_source: "inferred from infra/cluster/aks.tf lines 35, 44 and 51 at the base commit; not captured by the run"}
versions: <C0 candidate versions object; G2 requires the C1 object to be equal>
mutation_acceptance: {workflow_run_id: 44402419383, commit: "e85e651af7e291ccdab240c46cdb89edc7ba4c63",
                      measurement_complete: true, break_passed: true,
                      summary_sha256: "55727369b6c9f770c3d3327f978d3f5e268d368319788e183efaa018996b9410",
                      relevance_check_to_run_commit: "not_applicable"}
inputs: <one object per row of section 4.1: name, source, sha256>
```

`c0_takeover_microseconds` is copied from each C0 repeat's `membership.witness_takeover_microseconds` in repeat order. Absent never becomes zero: no key holds 0 or an empty list for an unavailable measurement. `gaps: []` is the receipt's own empty list, not a substitute.

### 4.4 `results/C03/2026-11-19.md`

ASCII only. Times in seconds are microseconds divided by 1000000 and rounded down to two decimals (60553624 gives `60.55`). The generator renders the takeover range in the summary, the H1 line and the README row from the C0 `witness_takeover_microseconds` values under this rule; it does not hold those numbers as constants. Title line, exactly:

`# C03: a consumer that kept heartbeating but made no Consume call lost its partitions at the poll limit; a paused consumer that kept calling Consume kept them`

Then this three-sentence summary paragraph, exactly:

> In run 44562323424 on Event Hubs Premium, a consumer that processed one message inline for 90000 ms, making no `Consume` call, lost its partitions in all five repeats of C0: the other group member took over the slow message's partition 60.55 s to 62.99 s after the slow message started, after the subject's 60000 ms `max.poll.interval.ms` and not at its 30000 ms `session.timeout.ms`, and processed that message a second time. In all five repeats of C1, a consumer that paused its partitions for 90000 ms but kept calling `Consume` kept them, and no message was processed twice. Both deciding hypotheses hold, so the proposed verdict is `confirmed`; no message was committed without being processed in any of the ten repeats, and the workload is synthetic.

Then this paragraph, exactly (decision 6894564479 point 2):

> In plain words: a consumer has two separate timers. Heartbeats are sent by a librdkafka background thread, whether or not the application calls `Consume`, and they keep the member in the group as long as one arrives within `session.timeout.ms`. `max.poll.interval.ms` is different: a `Consume` call resets it, and when the gap between calls is longer, librdkafka stops the heartbeats, leaves the group itself, and the partitions move to another member. So a consumer that blocks in its own processing code for longer than `max.poll.interval.ms` loses its partitions even though its heartbeats had kept it in the group past the session timeout, as C0 showed; a consumer that pauses its partitions and keeps calling `Consume`, as C1 did, keeps them.

Then the line `Proposed verdict: \`confirmed\`.`

Then these sections, in this order, with these headings:

1. `## What was tested`. This paragraph, exactly: "Each repeat ran two consumers in one group: a subject that carried the slow message and a witness that ran the baseline consumer. One message, at the slow-message ordinal 100 fixed in the scenario, took the subject 90000 ms. In C0 the subject processed it inline, with no `Consume` call for 90000 ms. In C1 the subject paused its partitions for 90000 ms and kept calling `Consume`. The verifier counted 240 delivered synthetic events per repeat, in five repeats per configuration." Then the scaled-limit text as a quoted paragraph, verbatim from `assessment_rules.scaled_limit` with YAML folding applied, preceded by the line `Scaled limit (from the expected outcome):`. Then this sentence, exactly: "Microsoft's Event Hubs guidance keeps `max.poll.interval.ms` at its 300000 ms default, says it must be greater than `session.timeout.ms`, and says to increase it only if processing takes longer than 5 minutes; this run tested a 60000 ms limit with the same mechanism, not 300000 ms itself."
2. `## Results by repeat`. Table `C0, inline slow message` with columns, in order: `Repeat`, `Status`, `Subject lost rows`, `Subject revoked rows`, `Witness took slow partition during slow message`, `Takeover (s)`, `Slow message processed rows`, `Processed again`, `Committed, never processed`, `Subject commit entries after lost`; values from `status`, `consumer.membership` and `consumer.counts` of each C0 repeat (`Status` is the repeat `status`; the boolean prints `yes` or `no`). Table `C1, paused slow message` with columns `Repeat`, `Status`, `Subject lost rows`, `Subject revoked rows`, `Witness assigned rows`, `Slow message processed rows`, `Processed again`, `Committed, never processed`. Each table is preceded by its name as a bold line.
3. `## Hypotheses`. One line each, exactly:
   - `- H1, the poll limit loses partitions (C0): holds. In each of the five repeats the subject received a lost callback, the witness took the slow message's partition during the slow message 60.55 s to 62.99 s after it started, at or above the 45 s threshold, and a message was processed again.`
   - `- H2, a paused consumer keeps its partitions (C1, K-55): holds. In each of the five repeats the subject had no lost or revoked callback, the witness was assigned nothing, the slow message was processed once and nothing was processed again.`
   - `- H3, no commit on partitions lost (C0, K-28), published on its own line and not part of the verdict: holds. No repeat's subject commit-callback rows record a successful commit for the lost partitions between the lost callback and the subject's next assignment.` followed by the indented line `  H3 observes only the time after the application received the lost callback. The window between the poll-limit expiry and the subject's next Consume call was not observed.` (expected.yaml `published_text_must_state`, verbatim).
   - `- Consumer loss, recorded only: 0 messages committed but never processed in each of the ten repeats.`
   Then the paragraph, exactly: "The 45 s threshold is a policy value between the 30 s session timeout and the subject's 60 s poll limit. A takeover at or above it means the group did not remove the subject at the session timeout, so its heartbeats continued while it made no `Consume` call."
4. `## Producer side and broker connections`. A table with columns `Configuration`, `Repeat`, `ok`, `loss`, `phantom`, `reported_failure`, `duplicate`, `reorder`, `Broker connections (maximum)`, `Broker connections (first after drain)`, from each repeat's `counts` and `broker_count`, then the sentence "Broker connection counts are lower bounds, as each repeat's `broker_count.lower_bound` records."
5. `## Run validity and evidence`. Both candidates `complete_valid` with no reasons; both phase-status files report every Job `complete` or `ready` and `safety_stop` false; both archive receipts `archive_complete` true with no gaps, each with its manifest SHA-256. Per configuration, a link to each of the three artifacts (`https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44562323424/artifacts/<id>`) and, for each repeat, the member paths inside the pods artifact of the pod log and `pods.json` of each of its five Jobs (`consumer-subject`, `consumer-witness`, `start-line`, `loadgen` and `verify`), then the member paths of the phase's `judge` pod log and `pods.json`. The node name of each pair, and the sentence "Both consumers of every repeat ran on the same `workload` node and exited 0." The line `Artifacts expire 2027-02-17T16:06:29Z.`
6. `## Environment and versions`. Namespace SKU Premium, 1 processing unit and region uksouth, each cited by its log line number (`probe.log` lines 1562, 1547 and 1556; `full-C3.log` lines 1397, 1382 and 1391), with no log text quoted, because those lines carry ANSI colour escapes; node size `Standard_D4ds_v5`, stated as inferred from `infra/cluster/aks.tf` lines 35, 44 and 51 and not captured; the `versions` object; `source_commit`; each phase's `worker_started_utc` and `judge_completed_utc`.
7. `## Mutation acceptance`. Run 44402419383 on `e85e651af7e291ccdab240c46cdb89edc7ba4c63` reports `measurement_complete` true and `break_passed` true; that commit is an ancestor of `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`; `c5_runtime.py check --base e85e651af7e291ccdab240c46cdb89edc7ba4c63 --head a1cf429dc7a57827c2a3d5bb658ae290f5ea9145` reports `not_applicable`, so no mutation input changed between them (docs/agents/factory.md "Evidence pack").
8. `## Not tested`. Exactly: "- Consumer token refresh only inside `Consume` (K-47): not tested. " followed by the `not_tested.K-47.reason` text verbatim. Then "- The 300000 ms baseline poll limit and a 6-minute slow message: the run scaled both, as stated above." Then "- Whether a commit can happen between the poll-limit expiry and the subject's next `Consume` call: not observed (H3)."
9. `## Sources`. The sources of section 10 that the report's statements rest on, each with its URL and the lines cited there.

The report names no earlier C03 run. A published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").

### 4.5 `README.md` line 32

Replace line 32 with exactly:

`| C03 | Long processing and poll interval | A consumer that kept heartbeating but made no \`Consume\` call for 90 s lost its partitions in all five repeats: the other member took over 60.55 s to 62.99 s after the slow message started, after the 60 s poll limit and not at the 30 s session timeout, and processed that message again. A consumer that paused for 90 s but kept calling \`Consume\` kept its partitions in all five repeats, with nothing processed twice. Heartbeats come from a librdkafka background thread and keep a member in the group against \`session.timeout.ms\` until \`max.poll.interval.ms\` is exceeded; \`max.poll.interval.ms\` is a separate limit that \`Consume\` calls reset. The poll limit was scaled to 60000 ms from 300000 ms. Consumer token refresh was not tested. Synthetic workload. | confirmed | [Report](results/C03/2026-11-19.md) |`

The generator prints this row; acceptance step 2 compares it with line 32 byte for byte.

### 4.6 External behaviour relied on

Verified with the tools named, on 2026-11-19 (plan rule 1):

1. Heartbeats run on a librdkafka internal thread, not in the application's `Consume` call, until the poll limit is exceeded. librdkafka v2.16.0 source, fetched from `raw.githubusercontent.com/confluentinc/librdkafka/v2.16.0/` with `curl`: `src/rdkafka.c` (SHA-256 `57ea67830c2d41089258c400b46f902e2d55b45a3ffe125d3010e5c0be0b8a67`) line 3003 creates the thread `rd_kafka_thread_main`, which names itself `rdk:main` (lines 2434 to 2438) and calls `rd_kafka_cgrp_serve` in its loop (line 2486); `src/rdkafka_cgrp.c` (`4929187b82e7c4baa5e5965bc41e9549d971c0879092959f38d2263b9d7881e1`) `rd_kafka_cgrp_serve` (line 7269) calls `rd_kafka_cgrp_join_state_serve` (lines 7362 and 7388), which in the `STEADY` state sends a heartbeat each `heartbeat.interval.ms` (lines 6810 to 6816). When the poll limit is exceeded, the timer callback sets `RD_KAFKA_CGRP_F_MAX_POLL_EXCEEDED` (line 5767), and `rd_kafka_cgrp_heartbeat` then returns without sending (lines 3959 to 3962: "Don't send heartbeats if max.poll.interval.ms was exceeded"); the member does not rejoin until the application polls again (lines 2717 to 2721). Confirmed. The run observes no heartbeat directly: a takeover at or above 45 s supports only the expected.yaml inference that the group did not remove the subject at the session timeout. INTRODUCTION.md (`109a00994a9c8eed91564b11af7b95251363dac6e514740563fb4f69b378de31`) line 2529 lists "KIP-62 - max.poll.interval and background heartbeats" as Supported.
2. A `Consume` call resets the poll timer, and exceeding it makes librdkafka leave the group and revoke the partitions as lost. The sources show other poll paths that also reset it (`src/rdkafka.c` line 5022, `rd_kafka_consume_callback0`); the published text therefore does not say "only". `src/rdkafka_int.h` (`9c838e16e7ed67e85791c0285e298c9aeaae53005859b1a1d7291c917b705e48`) lines 1333 to 1352 compute the excess from `rk_ts_last_poll`, which `rd_kafka_app_polled` sets (lines 1405 to 1410); `src/rdkafka.c` `rd_kafka_consume0` (line 5082) calls it (line 5105 and others), and `rd_kafka_consumer_poll` (line 5183) calls `rd_kafka_consume0` (line 5192); `src/rdkafka_cgrp.c` `rd_kafka_cgrp_max_poll_interval_check_tmr_cb` (lines 5741 to 5806) logs "leaving group", calls `rd_kafka_cgrp_leave` for a non-static member under the classic protocol and calls `rd_kafka_cgrp_revoke_all_rejoin_maybe` with `lost` true and reason "max.poll.interval.ms exceeded". Confluent.Kafka v2.16.0 `Consumer.Consume` calls `kafkaHandle.ConsumerPoll` (`src/Confluent.Kafka/Consumer.cs` line 911, SHA-256 `0b1b21af9f763a7fc5c81e81fed15a36eadb1c05cd0185a86fb45c6bad6cb3ad`), which calls `Librdkafka.consumer_poll` (`src/Confluent.Kafka/Impl/SafeKafkaHandle.cs` line 764, `c297a0f5963586d1e412ff0560560fc04513f99934ca538947109f551109b116`). Confirmed. The consumers ran the classic group protocol: `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 145 at the base commit pins `group.protocol` to `classic`, and `scenarios/C03/scenario.yaml` overrides only `max.poll.interval.ms`. Under either protocol branch the callback leaves the group (`src/rdkafka_cgrp.c` lines 5772 to 5805).
3. CONFIGURATION.md v2.16.0 (`b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`) line 131: "If no hearts are received by the broker for a group member within the session timeout, the broker will remove the consumer from the group and trigger a rebalance"; line 137: "If this interval is exceeded the consumer is considered failed and the group will rebalance in order to reassign the partitions to another consumer group member."
4. Microsoft, [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read with the Microsoft Learn `microsoft_docs_fetch` tool on 2026-11-19, librdkafka "Consumer only" table: `max.poll.interval.ms` default 300000, recommended "300000 (keep default)", constraint "Must be > `session.timeout.ms`", note "Increase only if processing takes longer than 5 minutes"; `session.timeout.ms` recommended 30000.

The report and README state no other Kafka, librdkafka or Event Hubs behaviour.

### 4.7 `docs/operations/evidence/issue-511-c03-verdict/receipt.md`

Records: the six input artifacts and two logs with hashes (section 4.1); the generator commands and their outputs (section 9); and the earlier full runs, as provenance only, with no data from them: run 44461695288 (head `b4cf81945a...`, created 2026-11-19T04:56:17Z, conclusion `failure`, job `full-C3` failed) and run 44488066541 (head `46295ce814...`, created 2026-11-19T08:29:43Z, conclusion `failure`, job `full-C3` failed), each with the sentence "Nothing from this run is published; the published result shows the final run only (AGENTS.md GOVERNANCE \"Verdict integrity\")." After those two, this item, exactly: `- Run 44248514485 (head \`730581540a7225a3a27b27a626f03fe2ea78de83\`, the merge commit of pull request 545, created 2026-11-18T06:03:20Z, conclusion \`success\`, jobs \`probe\` and \`full-C3\` succeeded). It ran before pull request 547 (merge commit \`e85e651af7e291ccdab240c46cdb89edc7ba4c63\`) added the \`membership\` observation that H1 to H3 read. Nothing from this run is published; the published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").` Each fact comes from `gh api .../actions/runs/<id>` and `.../jobs`; the receipt states no failure cause, because none was read from a source in this plan.

### 4.8 `items/511/state.md`

In the format of docs/agents/factory.md "Item state file", written after the catch-up of `implement-plan` step 1. Approved plan: this plan's revision and commit as the approval names them.

## 5. Requirements

- R1. WHEN the generator starts, THEN it checks every hash of section 4.1 and fails `G1` on any difference.
- R2. WHEN both candidates are read, THEN each must have `scenario_id` `C03`, `configuration_id` and `phase` equal to its configuration, `run_id` `c03-44562323424-1`, `source_commit` `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`, `synthetic` true, root `status` and `consumer.status` `complete_valid` with empty `reasons`, and exactly repeats 1 to 5, each with `status` and `consumer.status` `complete_valid` and empty `reasons`; and the two `versions` objects must be equal. Otherwise `G2`.
- R3. WHEN the rules of section 4.2 are recomputed, THEN each result and each precondition must equal section 4.2. Otherwise `G3`, and the implementer stops (a finding, not a fix).
- R4. WHEN root counts are compared, THEN each root `counts` key equals the sum over repeats of that key, and each root `consumer.counts` key equals the sum of the repeats' `consumer.counts`. Otherwise `G4`.
- R5. WHEN the archive receipts are read, THEN each has `archive_complete` true, `gaps` `[]`, `kind` `run_evidence_archive_receipt`, `identity` with `github_run_id` `"44562323424"`, `github_run_attempt` 1, `phase` equal to its configuration, `run_id` `c03-44562323424-1` and `source_commit` the base commit, and `manifest.sha256` equal to the manifest member hash. Otherwise `G5`.
- R6. WHEN the phase-status members are read, THEN each has `phase` equal to its configuration, `safety_stop` false, `candidate.status` `complete_valid`, `candidate.repeats` `[[1, "complete_valid"], ..., [5, "complete_valid"]]`, and every `modes` entry `outcome` `complete` or, for `consumer-readiness`, `ready`. Otherwise `G6`.
- R7. WHEN run with `--check`, THEN the rendered JSON and report equal the committed files byte for byte, and the printed README row equals `README.md` line 32. Otherwise `G7`.
- R8. WHEN the report is rendered, THEN it contains only ASCII. Otherwise `G8`.
- R9. WHEN the report is rendered, THEN it contains, verbatim, the title, the summary, the plain-words paragraph, the H3 indented line, and the K-47 reason, and the README row contains the sentence beginning "Heartbeats come from a librdkafka background thread". Otherwise `G9`.
- R10. WHEN the mutation summary is read, THEN `commit` is `e85e651af7e291ccdab240c46cdb89edc7ba4c63`, `measurement_complete` true and `break_passed` true. Otherwise `G10`.
- R11. The checks run in the order G1, G2, G3, G4, G5, G6, G10, then rendering, G8, G9, and G7 only with `--check`. The first failure prints `FAIL G<n>: <reason>` and exits 1.
- R12. `--self-test` runs the real inputs, printing `PASS real_inputs`, then each case of section 7 on an in-memory copy after G1, printing `PASS <case>` when the case fails at its expected check, and exits 0 only if every case matched. G7 runs in self-test only for case `report_edit`.

## 6. Invariants and safety rules

- No edit to an expected outcome, a verdict rule, a verifier output or a candidate (AGENTS.md GOVERNANCE "Verdict integrity"). A recomputation that disagrees with a candidate or with section 4.2 is a finding: stop under section 11.
- No dispatch, no Azure access, no secret, tenant ID or subscription ID (hard safety rules 2, 3 and 7). The generator copies no log text other than the lines section 4.4 cites.
- No number that was not measured or computed by script (hard safety rule 11; docs/agents/pull-requests.md rule 4).
- The published results show run 44562323424 only.
- The pull request is labelled `verdict`, touches protected paths and is merged by Hari only.

## 7. Test map

The tests are the generator's `--self-test` cases. Each changes one value in memory after G1. No existing test changes, so plan rule 2's standing fixture rule has no case; if one arose, the implementer follows it exactly: (a) change the fixture so it passes the new checks, keeping every assertion and boundary, or (b) where invalid input is the test's purpose, keep the input and change expected values to exactly what the checks produce; never remove a case, loosen an assertion or change a check, and list each change with the check that forces it in the PR implementation notes.

| Requirement | Case | Change | Expected |
|---|---|---|---|
| R1 | `hash_c0_candidate` | one byte of the C0 `candidate.json` member, before G1 | `FAIL G1` |
| R1 | `expected_yaml_edit` | one byte of `expected.yaml`, before G1 | `FAIL G1` |
| R2 | `c0_repeat_status` | C0 repeat 3 `consumer.status` to `incomplete` | `FAIL G2` |
| R2 | `versions_differ` | C1 `versions.librdkafka` to `2.15.0` | `FAIL G2` |
| R3 | `h1_takeover_at_threshold` | C0 repeat 4 `witness_takeover_microseconds` to 45000000 | `FAIL G9` (H1 still holds at the boundary, so G3 to G6 and G10 pass; the summary rendered from the changed time then differs from the pinned summary) |
| R3 | `h1_takeover_below` | C0 repeat 4 `witness_takeover_microseconds` to 44999999 | `FAIL G3` (H1 contradicted) |
| R3 | `h1_no_lost` | C0 repeat 2 `subject_lost_rows` to 0 | `FAIL G3` (H1 contradicted) |
| R3 | `h1_no_reprocess` | C0 repeat 5 `consumer.counts.processed_again` to 0 | `FAIL G3` (H1 inconclusive) |
| R3 | `h3_commit_after_lost` | C0 repeat 1 `subject_commit_entries_after_lost` to 1 | `FAIL G3` (H3 contradicted) |
| R3 | `h2_lost` | C1 repeat 1 `subject_lost_rows` to 1 | `FAIL G3` (H2 contradicted) |
| R3 | `h2_witness_assigned` | C1 repeat 3 `witness_assigned_rows` to 1 | `FAIL G3` (H2 inconclusive) |
| R3 | `loss_nonzero` | C1 repeat 2 `consumer.counts.committed_never_processed` to 1 | `FAIL G3` (recorded value differs) |
| R3 | `slow_rows` | C0 repeat 3 `slow_processed_rows` to 3 | `FAIL G3` (precondition) |
| R3 | `node_split` | C1 repeat 5 witness `spec.nodeName` to another name | `FAIL G3` (precondition) |
| R4 | `root_consumer_sum` | C0 root `consumer.counts.processed_again` 5 to 4 | `FAIL G4` |
| R4 | `root_producer_sum` | C1 root `counts.ok` 1200 to 1199 | `FAIL G4` |
| R5 | `archive_gap` | C0 receipt `gaps` to `["x"]` | `FAIL G5` |
| R5 | `archive_phase` | C1 receipt `identity.phase` to `C0` | `FAIL G5` |
| R6 | `phase_safety_stop` | C0 phase-status `safety_stop` to true | `FAIL G6` |
| R10 | `mutation_incomplete` | summary `measurement_complete` to false | `FAIL G10` |
| R8 | `non_ascii` | an en dash in the rendered report | `FAIL G8` |
| R9 | `missing_plain_words` | delete the plain-words paragraph from the rendered report | `FAIL G9` |
| R7 | `report_edit` | change one table cell of the committed report, with `--check` | `FAIL G7` |

Boundary: H1's takeover threshold is `>= 45000000` for holds and `< 45000000` for contradicted (expected.yaml); the two `h1_takeover_*` cases test at and just below it. 23 cases.

## 8. Deployment assumptions

None. The pull request deploys nothing and runs no code against a container, image, chart or runtime. All live evidence is recorded output of the completed run, pinned by hash in section 4.1; no assumption about an open or fresh connection, or an idle or loaded state, is relied on.

## 9. Acceptance

Run from the repository root, with the inputs of section 4.1 in a scratch directory `$IN` outside the repository.

1. `sha256sum` of each ZIP, member and log equals section 4.1. Evidence: the output in the receipt.
2. `python3 -I docs/operations/evidence/issue-511-c03-verdict/make_results.py --inputs "$IN"` writes the two results files and prints the README row. Evidence: exit 0.
3. `... make_results.py --inputs "$IN" --check`. Evidence: exit 0 and `PASS check`.
4. `... make_results.py --inputs "$IN" --self-test`. Evidence: `PASS real_inputs` and one `PASS <case>` line for each of the 23 cases of section 7, exit 0.
5. `python3 -m json.tool results/C03/2026-11-19.json > /dev/null`. Evidence: exit 0.
6. `git merge-base --is-ancestor e85e651af7e291ccdab240c46cdb89edc7ba4c63 a1cf429dc7a57827c2a3d5bb658ae290f5ea9145` exits 0, and `git show a1cf429dc7a57827c2a3d5bb658ae290f5ea9145:tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json > "$IN/q.json" && python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base e85e651af7e291ccdab240c46cdb89edc7ba4c63 --head a1cf429dc7a57827c2a3d5bb658ae290f5ea9145 --qualification "$IN/q.json"` prints `{"reasons": [], "status": "not_applicable"}` (observed in planning on 2026-11-19). Evidence: the output in the receipt.
7. `npm run check:docs`. Evidence: no warning on a changed file, or each explained in the pull request.
8. `LC_ALL=C grep -rnP '[^\x00-\x7F]' results/C03/ docs/operations/evidence/issue-511-c03-verdict/ items/511/state.md` and the same on line 32 of `README.md`. Evidence: no output.
9. `git diff --name-only origin/main...HEAD` lists exactly the six paths of section 3, and `git diff origin/main...HEAD -- README.md` changes only line 32.
10. Cold review under AGENTS.md "Before opening a pull request" rule 8; when Codex implements, the Claude review gate of decision 6633528258 point 2; Codex review and Codex security review requested as hard safety rule 10 allows; the pull request labelled `verdict`.

`scripts/verify-dotnet.sh` is not required: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates").

## 10. Sources

- Run [44562323424](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44562323424), its artifacts and job logs, read 2026-11-19 through the GitHub REST API.
- Mutation run [44402419383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44402419383), `summary.json`: `commit` `e85e651af7e291ccdab240c46cdb89edc7ba4c63`, `measurement_complete` true, `break_passed` true; it is the newest `verifier-c5-mutation.yml` run (runs list, 2026-11-19).
- librdkafka v2.16.0 `CONFIGURATION.md`, `INTRODUCTION.md`, `src/rdkafka.c`, `src/rdkafka_cgrp.c`, `src/rdkafka_int.h`, and Confluent.Kafka v2.16.0 `src/Confluent.Kafka/Consumer.cs` and `src/Confluent.Kafka/Impl/SafeKafkaHandle.cs`, at the hashes and lines of section 4.6.
- [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read 2026-11-19 with `microsoft_docs_fetch`.
- `scenarios/C03/expected.yaml`, `scenarios/C03/scenario.yaml`, `scenarios/profiles/c03-consumer.yaml`, docs/blueprint.md ADR-022 and docs/operations/verifier-contract.md line 128, at the base commit.
- `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit (`vm_size = "Standard_D4ds_v5"` for all three pools).
- `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 145 at the base commit, for the classic group protocol in section 4.6 point 2.

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when a pinned hash in section 4.1 differs from a fresh download; when G2 to G6 or G10 fails on the real inputs (a finding under "Verdict integrity", not a fix); or when a predecessor pull request of #508 is not merged.

## 12. Choices made under "Act or ask"

These are ordinary reversible choices within authority (docs/agents/factory.md "Act or ask"); Hari may change any of them when he reviews the verdict pull request.
1. Earlier full runs 44461695288 and 44488066541 are named only in the receipt, with no data, and not in `results/` or the README, so the published result shows the final run only. The B10 verdict listed its earlier attempt in its results JSON; this plan keeps it out of `results/` because the prompt that started this session asked the plan to follow "Verdict integrity" on that point.
2. Takeover times are printed rounded down to two decimals of a second; the JSON keeps the microseconds.
3. The report and README quote no failure cause for the earlier runs, because none was read from a source.

## 13. Plan-check answers

(a) No step's effort scales with a number unknown at planning time. The inputs are fixed: two candidates, two archive receipts and manifests, two pods artifacts, two job logs and one mutation summary. The self-test has exactly 23 cases plus the real-input run.

(b) The parent's split row 8 says only "One verdict pull request each for C03, C10 and C01; C03's README row and report state the heartbeat and poll-limit distinction". This plan is stricter: it adds the generator's recomputation of every expected.yaml rule, its archive, phase-status and mutation checks, and its self-test. Why: docs/agents/pull-requests.md rule 4 requires counts and totals by script, and AGENTS.md "Verdict integrity" makes any disagreement with a candidate a finding, which only a recomputation can detect.

(c) Dependencies neither on `main` nor in the pull request: the run's artifacts and job logs and the mutation summary, recorded output pinned by hash in section 4.1 and retained until 2027-02-17T16:06:29Z (mutation artifacts until 2027-02-16T21:31:53Z); the librdkafka and Confluent.Kafka sources, pinned by tag and hash; and the Microsoft page, current content read 2026-11-19. No deployment assumption exists (section 8).

## 14. Open questions for Hari

None. The implementing tool is set by the issue body's decisions (section 2). Approving or rejecting the verdict stays with Hari on the verdict pull request; this plan proposes it and decides nothing about it.
