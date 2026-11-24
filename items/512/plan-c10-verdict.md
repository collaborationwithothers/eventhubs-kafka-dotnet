# C10 verdict contract-grade plan

Revision: 1. Base commit: `main` at `3e7163dc641e724437025d917032fd40419e6c4d`.

This plan writes the C10 evidence pack and verdict pull request from batch run 45074268881, whose two phases are both `complete_valid` with complete archives. Storing an offset only after processing left no message marked done that nobody processed in any of five repeats, so the expected outcome selects `confirmed`. The report explains, in plain words and before any verifier term, what the crash was, what was compared, what was measured and what the zero under librdkafka's default store does not show, as Hari decided on 2026-11-24.

Item: C10, store-after-process versus the default automatic offset store, loss and duplicates on crash [issue #512; parent entry 9 of #499]. Workload data is synthetic. Every number below is copied from a pinned run artifact, a pinned log or a cited source, or computed from them by script on 2026-11-24; no number is an estimate unless labelled.

## 1. What changed

Revision 1 is the first revision.

## 2. Goal and authority

Goal: publish C10's proposed verdict with the evidence pack that docs/agents/factory.md "Evidence pack" requires, from run 45074268881 only, so Hari can approve or reject it.

Authority, in creation order. No item state file is read: files at `items/<issue>/state.md` are historical (docs/agents/factory.md "Decision log", last paragraph).

1. [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512), created 2026-11-15T13:50:15Z, issue `updated_at` 2026-11-24T08:32:29Z: "Verdict pull request for C10 from the batch run of entry 7. Verdict pull requests go to Hari." It names `Parent items: #499`, `Split entry: 9 (C10 verdict)`, `Predecessor PRs: the pull request of #510 (entry 7)`, and asks the plan to read [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135) and the #499 decisions [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) and [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673). It names Claude for planning, with the `grilling` skill and then the `plan-pr` skill, and Codex for implementation, with the Claude review gate of decision 6894587233.
2. [Decision 6895539372](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-6895539372), 2026-11-15T14:03:41Z, adopts [standing decision 6895535291 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): the control session, grilling answers adopted in advance (point 2), plan approval on a check with `Blocking findings: none` and "Open questions for Hari" `None` (point 3); not covered (point 4): money, Azure dispatch, expected outcomes and verdicts, protected-path merges, scope beyond the approved split, and the `ready-for-agent` label.
3. [Decision 7126256621](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7126256621), 2026-11-24T08:32:19Z, `updated_at` 2026-11-24T08:32:29Z. Hari: "ok start planning the verdict PRs . All this needs to be clear in that like you have explained it to me". The published C10 result must explain itself in plain words before any verifier terms, in six points: the scenario as a production event; the two settings in plain words; what was measured; what the verdict means and does not mean; processed-twice as the cost of periodic commits; and the before-first-commit case belonging to C01 (#513). It states: "This adds to the `text_must_state` list in `scenarios/C10/expected.yaml`; it changes no expected outcome or verdict rule." Section 4.4 encodes the six points; section 15 question 1 asks how the addition is recorded.

What this plan relies on from the decisions the issue body names:
- 6894534441 point 2: the consumer ends its own process at a point fixed before the run. Point 3: K-57 is folded into C10 for this batch. Point 4: Codex implements.
- 6894587233 point 2 and 6894614673 points 1 and 2: Claude plans with `grilling` then `plan-pr`; Codex implements; each Codex pull request gets the Claude review gate of [decision 6633528258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633528258) point 2.

K-57 placement. Decision 6894534441 point 3 put K-57 under C10. The merged blueprint section 12 row (`docs/blueprint.md` line 1276 at the base commit) and `scenarios/C10/expected.yaml` `verdict.text_must_state` ("K-57 is tested under C01, not C10") later placed it under C01, and decision 7126256621 point 6 agrees. This plan follows the merged files and the newest decision.

Grilling. Hari is not present in this session, so under standing decision 6895535291 point 2 each question the `grilling` skill would ask is written with its recommended answer. Questions whose answer is an ordinary reversible choice within authority are answered in section 12 under docs/agents/factory.md "Act or ask"; the one question in a reserved area (expected outcomes, point 4 of that decision) is in section 15.

Other #499 decisions (6895299617, 6895328446, 6895343227, 6899939624, 6926376436, 6958521790) are not linked and adopted by a decision on #512, so under docs/agents/factory.md "Item lifecycle" step 3 this plan relies on none of them. Their substance that matters here is merged in ADR-022, `scenarios/C10/expected.yaml` and docs/operations/verifier-contract.md. The merge method decision is irrelevant: the verdict pull request goes to Hari (AGENTS.md GOVERNANCE "Verdict integrity").

Predecessors: #510's pull requests [568](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/568), [572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/572) and [573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/573) are merged (2026-11-21). The run's head `f23fa035405cb13699235b03e98f058b3cd6cf25` is an ancestor of the base commit. The implementer verifies all three merged before starting (factory.md "Item lifecycle" step 3).

Run evidence handed over: [#510 comment 7093419526](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7093419526) reports run 45074268881 and its per-repeat counts. That comment is a handoff, not a source; every number here was re-read from the artifacts by script.

Design authority on `main` at the base commit, each equal to the run's head by `sha256sum` on 2026-11-24: `scenarios/C10/expected.yaml` (SHA-256 `21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c`), `scenarios/C10/scenario.yaml` (`d72b4ab55fb1c05b7ef3312ced62e2ae95a65903a8c9266bda785fcf5647d290`), `scenarios/profiles/c03-consumer.yaml` (`721d5f2fec07a066581ffa21ad3925dfba824457e805f59a3a1b6a0d7483fa65`); docs/blueprint.md ADR-022 (from line 737); docs/operations/verifier-contract.md lines 128 and 136 (consumer block, `subject_commit_coverage`, `offset_read`, root projection); docs/agents/factory.md "Evidence pack".

Candidate selection: #512 is a child of the #499 split, entry 9, with no plan comment. Its one pull request is named `C10 verdict`. It is one coherent piece: one evidence pack for one run.

Implementing tool: the issue body's decisions name Codex. Under standing decision 6895535291 point 5 the control session asks Hari before he applies `ready-for-agent`. This plan does not decide the tool.

## 3. Scope

Files allowed to change, and only these five:

| Path | Change | Protected |
|---|---|---|
| `results/C10/2026-11-22.json` | new | yes, `/results/` |
| `results/C10/2026-11-22.md` | new | yes, `/results/` |
| `README.md` | line 39, the C10 row, only | yes, `/*.md` |
| `docs/operations/evidence/issue-512-c10-verdict/make_results.py` | new | no |
| `docs/operations/evidence/issue-512-c10-verdict/receipt.md` | new | no |

The date is the run's UTC creation date, 2026-11-22 (run `created_at` 2026-11-22T21:49:48Z), as in `results/C03/2026-11-19.md` for run 44562323424 created 2026-11-19T16:06:27Z. Phase C1 ran on 2026-11-23; the report states both phases' times.

The generator may start from a copy of `docs/operations/evidence/issue-511-c03-verdict/make_results.py` (866 lines at the base commit); it is a new file in the new folder, and the C03 file does not change.

Size estimate (estimate, 2026-11-24; basis: the C03 precedent at the base commit, whose generator is 866 lines and receipt 85 lines by `wc -l`, and this plan's similar rule set): about 750 lines for `make_results.py`, 60 for `receipt.md`, 200 for the report and 1 changed line in `README.md`. The results JSON is generator output and is not estimated by hand. Five files.

Paths that must not change: `scenarios/` (all, including `scenarios/C10/expected.yaml`, unless section 15 question 1 is answered otherwise), `src/`, `tests/`, `charts/`, `infra/`, `scripts/`, `.github/`, `docs/blueprint.md`, `docs/agents/`, `docs/operations/` outside the new evidence folder, every other path under `results/` and `items/`. No workflow is dispatched by the implementer. No expected outcome, decision rule, verifier output or candidate is edited (AGENTS.md GOVERNANCE "Verdict integrity").

The results files are generator output. docs/agents/pull-requests.md rule 8 says "Agents commit no program output"; its paragraph beginning "Some Code Review Rules flag work that only Hari can approve" keeps such work only if "the verdict step requires it", which it does here, as in every earlier verdict pull request. The pull request description lists the protected-path and results-file findings under that paragraph and leaves the merge to Hari.

## 4. External contracts

### 4.1 Inputs

The generator reads only these inputs from one directory `$IN`, each pinned by SHA-256. Each ZIP was downloaded on 2026-11-24 through `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip`, and each ZIP hash equals the artifact API `digest`. Logs came from `gh api .../actions/jobs/<id>/logs`.

| File in `$IN` | Source | SHA-256 |
|---|---|---|
| `c10-C0-candidate.zip` | artifact 14776044642, `c10-C0-candidate-45074268881-1` | `14f18dd838c796e5fddb8b96bf15dfaacfb4fb51a893c8e0cec01176e5a80953` |
| its member `candidate.json` | | `7aef903eb4f519d17d991c28085b9e2efef7203e4e4f53a8a8bb579bd15179a7` |
| `c10-C1-candidate.zip` | artifact 14793443758, `c10-C1-candidate-45074268881-1` | `483c030bcbcc856d1725913a894c834b552217574c787f48564c23a88096beee` |
| its member `candidate.json` | | `bdd7ab2c89f628a494c7dbf1e30363f6f3e0f106ee93303a5c66c9b356114827` |
| `c10-C0-archive.zip` | artifact 14776529837, `c10-C0-archive-45074268881-1` | `e0c7d4f3a730c78890d7c05457dae561f18ebf455ade07e0fe1fed8abef4cddb` |
| its member `archive-receipt.json` | | `f8217c10ee6eadd1eab7a3b78ec9f152de7ac497cf7e2d0578686f6761d1edad` |
| its member `archive-manifest.json` | | `c474ac169f0ea2ed66600719fa041f51bd19160f1420a4f1ffe6f4727624f960` |
| `c10-C1-archive.zip` | artifact 14794880764, `c10-C1-archive-45074268881-1` | `36962b576f1ab0f3007e24c54785354fa3c0748e2c16f35c9d3c6229a1706b8d` |
| its member `archive-receipt.json` | | `e40fb4b8e5afe967d825d84096d869dba51df24ce23886e09ef9c0eff2fff3a1` |
| its member `archive-manifest.json` | | `2ee1d7fad27daeec0302e6a2084d746c57e4586a3e53bacc9a1a40dbf0252ec2` |
| `c10-C0-pods.zip` | artifact 14775989790, `c10-C0-pods-45074268881-1` | `9de6a66e8972befb2f5669316a7658f1f94a04d323bf984e56849584a81bbe9e` |
| its member `phase-status.json` | | `98604aa1258ad08e07fc01ee4ed8bcec4bb5defabf9c7d6d965f18fa6d67ffc7` |
| `c10-C1-pods.zip` | artifact 14793209973, `c10-C1-pods-45074268881-1` | `d22a170953de53161cfc639aa96b08ec6e29541dd804e8d65e69ad9f8750a33d` |
| its member `phase-status.json` | | `ab8a929dc4b00b88096de2b110c396df610c24aa64ba3a45bc967999ee1c6a6b` |
| `probe.log` | job 137367883128 (`probe`, ran C0) | `c5708404195a7f9b7e07f7fbb555865238b10e486db38dce2ddd0e73bd307069` |
| `full-C3.log` | job 137388417872 (`full-C3`, ran C1) | `3985285031dcb90ac3bea90fb5e6ed0e973f09d1830c1b9d94ad74d5b723ee82` |
| `c5-summary.zip` | the mutation summary artifact of section 4.9; not yet available | recorded by the implementer; must equal the artifact API `digest` |
| its member `summary.json` | | recorded by the implementer |
| `scenarios/C10/expected.yaml` | working tree, equal to the base commit | `21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c` |

The generator takes the two mutation hashes as arguments `--mutation-zip-sha256` and `--mutation-summary-sha256`, and the mutation run ID as `--mutation-run`; every other hash is a constant in the generator. Each archive receipt's `manifest.sha256` equals its manifest member hash above. The generator reads the pods members `<job>/pods.json` for the consumer subject and witness of each repeat by path inside the ZIP, covered by the ZIP hash. Run artifacts expire 2027-02-20T21:49:49Z (artifact API `expires_at`); the report states that date. Phase mapping: `probe` ran C0 (21:55:55Z to 23:00:34Z on 2026-11-22) and `full-C3` ran C1 (05:22:04Z to 06:20:23Z on 2026-11-23), as the phase-status members' `phase` fields and the job times show. Jobs `batch`, `batch-stop` and `full-C0` were skipped.

If any pinned hash differs from a fresh download, the implementer stops under section 11.

### 4.2 Pinned outcomes

The generator recomputes each rule of `scenarios/C10/expected.yaml` from the candidates, exactly as written there, and requires these results (G3):

| Rule | Recomputed result |
|---|---|
| `assessment_rules.commit_precondition`, per repeat | met in all ten repeats (`subject_commit_coverage.uncovered_partitions` `[]`) |
| `configurations.C0.H1_store_after_process_loses_nothing` | `holds` |
| `configurations.C0.processed_again` (recorded only) | `processed_again` `[2, 4, 2, 3, 4]`, `processed_again_extra_rows` `[2, 4, 2, 3, 4]` |
| `configurations.C1.H2_auto_store_can_pass_an_unprocessed_message` | `inconclusive` |
| `configurations.C1.processed_again` (recorded only) | `processed_again` `[4, 1, 2, 2, 5]`, `processed_again_extra_rows` `[4, 1, 2, 2, 5]` |
| `verdict` | `confirmed` |

Evaluation, for each hypothesis: `holds` when its `holds_when` is true; else `contradicted` when its `contradicted_when` is true (H2's is `never`, so never true); else `inconclusive`. A repeat is counted when its candidate repeat `status` and `consumer.status` are both `complete_valid` and its `consumer.subject_commit_coverage.uncovered_partitions` is `[]`. "All 5 repeats counted" means exactly repeats 1 to 5, each counted. H1 holds when all 5 are counted and each has `consumer.counts.committed_never_processed` 0; H1 is contradicted when all 5 are counted and at least one has it at least 1. H2 holds when all 5 are counted and at least one has it at least 1. The verdict is `confirmed` when H1 holds, `contradicted` when H1 is contradicted, and otherwise `no_verdict`.

G3 also requires these preconditions of the published sentences in section 4.4:
- in every repeat of both configurations, `consumer.kill_count` is 110 and `processed_again_extra_rows` equals `processed_again` (so every message processed again was processed exactly twice, which the plain words state);
- in every repeat, `processed_again` is at least 1 (the summary says processed twice "in every repeat");
- in every repeat, `consumer.offset_read.outcome` is `success`;
- in every repeat, the subject's `pods.json` shows one pod whose container terminated with exit code 137, and the witness's one pod with exit code 0, both with the same `spec.nodeName`;
- in each archive manifest, exactly five `objects` entries have `gap_code` `configured_self_kill`, one per repeat 1 to 5, each with `kind` `native_monitoring_index` and a `source.name` containing `-consumer-subject/`.

### 4.3 `results/C10/2026-11-22.json`

Written as `json.dumps(document, indent=2, ensure_ascii=True) + "\n"`. Top-level keys exactly `publication` and `candidates`, in that order. `candidates` has exactly `C0` and `C1`, each JSON-equal to its pinned `candidate.json` member. `publication` has exactly these keys, in this order, with these values; `<...>` marks a value the generator copies from the named input.

```
item: "C10"
workload: "synthetic"
run: {workflow_run_id: 45074268881, attempt: 1, event: "workflow_dispatch", ref: "main",
      head_sha: "f23fa035405cb13699235b03e98f058b3cd6cf25", created_at: "2026-11-22T21:49:48Z",
      conclusion: "success", run_id: "c10-45074268881-1"}
expected_outcome: {path: "scenarios/C10/expected.yaml", sha256: "21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c"}
publication_decision: {comment_id: 7126256621, url: "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7126256621"}
proposed_verdict: "confirmed"
rule: "scenarios/C10/expected.yaml verdict.confirmed_when: H1 holds."
kill_point: <expected.yaml assessment_rules.kill_point, verbatim, whitespace folded as YAML folds it>
commit_precondition: {C0: {met: [1, 2, 3, 4, 5], failed: []}, C1: {met: [1, 2, 3, 4, 5], failed: []}}
hypotheses:
  H1_store_after_process_loses_nothing: {configuration: "C0", result: "holds",
                                         committed_never_processed: [0, 0, 0, 0, 0]}
  H2_auto_store_can_pass_an_unprocessed_message: {configuration: "C1", result: "inconclusive",
                                                  committed_never_processed: [0, 0, 0, 0, 0], separate_line: true}
processed_again: {mode: "recorded_only", separate_line: true,
                  C0: {processed_again: [2, 4, 2, 3, 4], processed_again_extra_rows: [2, 4, 2, 3, 4]},
                  C1: {processed_again: [4, 1, 2, 2, 5], processed_again_extra_rows: [4, 1, 2, 2, 5]}}
archives: {C0: {archive_complete: true, gaps: [], manifest_sha256: "c474ac169f0ea2ed66600719fa041f51bd19160f1420a4f1ffe6f4727624f960", configured_self_kill_repeats: [1, 2, 3, 4, 5]},
           C1: {archive_complete: true, gaps: [], manifest_sha256: "2ee1d7fad27daeec0302e6a2084d746c57e4586a3e53bacc9a1a40dbf0252ec2", configured_self_kill_repeats: [1, 2, 3, 4, 5]}}
consumer_nodes: {C0: <list of 5 node names, one per repeat>, C1: <list of 5>}
consumer_exit_codes: {C0: {subject: [137, 137, 137, 137, 137], witness: [0, 0, 0, 0, 0]}, C1: {subject: [137, 137, 137, 137, 137], witness: [0, 0, 0, 0, 0]}}
broker_connection_counts: <per configuration, per repeat: {repeat, role, maximum, first_post_drain, lower_bound} copied from each repeat's broker_count>
environment: {namespace_sku: "Premium", processing_units: 1, region: "uksouth",
              node_size: "Standard_D4ds_v5", node_size_source: "inferred from infra/cluster/aks.tf lines 35, 44 and 51 at the base commit; not captured by the run"}
versions: <C0 candidate versions object; G2 requires the C1 object to be equal>
mutation_acceptance: {workflow_run_id: <--mutation-run>, commit: <summary commit>,
                      measurement_complete: true, break_passed: true,
                      summary_sha256: <--mutation-summary-sha256>,
                      includes_run_head: "f23fa035405cb13699235b03e98f058b3cd6cf25"}
inputs: <one object per row of section 4.1: name, source, sha256>
```

The consumer `reassignment` values stay inside the copied candidates and are not projected into `publication` or the report; reassignment time is C01's measure, not C10's. Absent never becomes zero: no key holds 0 or an empty list for an unavailable measurement. `gaps: []` is the receipt's own empty list, not a substitute.

### 4.4 `results/C10/2026-11-22.md`

ASCII only. Ranges are rendered from the data as `<min> to <max>` over the five repeats of a configuration; the generator holds no range as a constant. Title line, exactly:

`# C10: a consumer killed while holding an unprocessed message left no message marked done that nobody processed; some messages were processed twice`

Then this three-sentence summary paragraph, exactly, with `{C0 range}` and `{C1 range}` rendered (here `2 to 4` and `1 to 5`):

> In run 45074268881 on Event Hubs Premium, a consumer in a two-member group was killed while it held a message it had not processed, and the other member took over its partitions; in all five repeats that stored each offset only after processing the message, and in all five that used librdkafka's default of storing it when the message is handed to the application, no message was marked done without being processed. In every repeat some messages were processed twice ({C0 range} per repeat when storing after processing, {C1 range} with the default), because after the crash the surviving member redid the work done since the last periodic commit. Storing after processing lost nothing, so the proposed verdict is `confirmed`; the zero under the default store does not show that the default is safe, and the workload is synthetic.

Then the line `Proposed verdict: \`confirmed\`.`

Then the section `## In plain words`, before any section that uses verifier terms (decision 7126256621). Its paragraphs, exactly, with ranges rendered:

1. `**The production event.** A consumer has been running in a group and committing its progress. It is killed while it holds a message it has not processed yet, and another member of the group takes over its partitions. The question is whether that crash leaves any message marked done that nobody processed. "Marked done" means the group's committed offset for the partition has moved past the message, so no member will read it again.`
2. `**The two settings.** With the first setting (C0, the baseline), the application stores a message's offset only after it has processed the message. With the second (C1), librdkafka's default \`enable.auto.offset.store\` stores the offset as soon as the message is handed to the application, before the application has processed it. Only the killed consumer changes setting; the member that takes over runs the baseline in both. In both, librdkafka commits the stored offsets in the background every 5000 ms.`
3. `**What was measured.** In every repeat of both settings, zero messages were marked done without being processed. Some messages were processed twice: {C0 range} per repeat when storing after processing, and {C1 range} per repeat with the default.`
4. `**What the verdict means.** Storing after processing lost nothing, so the prediction is confirmed. The zero under the default store does not show that the default is safe. At this kill point a message can be marked done without being processed only if librdkafka commits in the short time between handing the message over and the kill, which is unlikely, so a run of this length is not expected to catch it.`
5. `**Why some messages were processed twice.** Both settings showed it. It is the cost of committing periodically, called at-least-once delivery: after a crash, the surviving member starts from the last committed offset and redoes the work done since that commit.`
6. `**What this result does not cover.** A consumer that dies before its first commit is a different failure with a different cause. It is tested under C01 (#513), not here.`

Then these sections, in this order, with these headings:

1. `## What was tested`. This paragraph, exactly: "Each repeat ran two consumers in one group: a subject that carried the fault and a witness that ran the baseline consumer and received the subject's partitions. The verifier counted 240 delivered synthetic events per repeat, in five repeats per configuration. C0 runs the baseline, which stores an offset after processing. C1 sets `enable.auto.offset.store` to `true` on the subject and changes nothing else; the witness runs the baseline in both configurations." Then the kill point as a quoted paragraph, verbatim from `assessment_rules.kill_point` with YAML folding applied, preceded by the line `Kill point (from the expected outcome):`. Then the commit precondition the same way, preceded by `Commit precondition (from the expected outcome):`, followed by the sentence, exactly: "No repeat failed the commit precondition: in all ten, every qualifying partition had a successful subject commit before the kill." (G3 requires `failed` empty for both configurations; if it were not, the implementer stops under section 11.)
2. `## Results by repeat`. One table per configuration, named by a bold line `C0, store after processing` and `C1, default automatic store`, with columns, in order: `Repeat`, `Status`, `Consumer status`, `Commit precondition`, `Processed once`, `Processed again`, `Extra processing rows`, `Committed, never processed`; values from each repeat's `status`, `consumer.status`, `consumer.subject_commit_coverage` (`met` when `uncovered_partitions` is empty, otherwise `failed: <partitions>`) and `consumer.counts`. Then the sentence, exactly: "\"Committed, never processed\" is the verifier's name for a message marked done that nobody processed. \"Processed again\" counts messages with more than one processing row; \"Extra processing rows\" counts the rows beyond the first, and the two are equal in every repeat, so each such message was processed exactly twice."
3. `## Hypotheses`. One line each, exactly, with ranges rendered:
   - `- H1, storing after processing loses nothing (C0): holds. All five repeats counted, and in each no message was committed without being processed.`
   - `- H2, the default automatic store can pass an unprocessed message (C1), published on its own line and not part of the verdict: inconclusive. All five repeats counted, and none had a message committed without being processed; a zero count does not contradict a possibility.` followed by the indented line `  ` plus H2's `published_text_must_state`, verbatim with YAML folding applied.
   - `- Processed again, recorded only and published on its own line: {C0 range} messages per repeat in C0 and {C1 range} in C1.`
4. `## Commit evidence`. This paragraph, exactly: "After each repeat the verifier read the group's committed offsets from Event Hubs without joining the group (`offset_read`, outcome `success` in all ten repeats). A repeat is valid only when every committed offset it read matches an offset the members' own commit records report, and only when every partition's committed offset reached the run's end line. So \"committed\" here is observed from the broker, not only reported by the consumers." Source: `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 63 to 74 at the base commit (the `consumer_commit_record_disagreement` and `consumer_commit_frontier_short` checks).
5. `## Producer side and broker connections`. A table with columns `Configuration`, `Repeat`, `ok`, `loss`, `phantom`, `reported_failure`, `duplicate`, `reorder`, `Broker connections (maximum)`, `Broker connections (first after drain)`, from each repeat's `counts` and `broker_count`, then the sentence "Broker connection counts are lower bounds, as each repeat's `broker_count.lower_bound` records."
6. `## Run validity and evidence`. Both candidates `complete_valid` with no reasons; both phase-status files report `safety_stop` false, every `consumer-subject` Job `failed` (the subject ends itself with SIGKILL, as the kill point requires) and every other Job `complete` or, for `consumer-readiness`, `ready`; both archive receipts `archive_complete` true with no gaps, each with its manifest SHA-256. Then this sentence, exactly: "The subject has no native-monitoring evidence; the archive records its missing index as `configured_self_kill` in each of the ten repeats." Per configuration, a link to each of the three artifacts (`https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45074268881/artifacts/<id>`) and, for each repeat, the member paths inside the pods artifact of the pod log and `pods.json` of each of its five Jobs (`consumer-subject`, `consumer-witness`, `start-line`, `loadgen` and `verify`), then the member paths of the phase's `judge` pod log and `pods.json`. The node name of each pair, and the sentence "Both consumers of every repeat ran on the same `workload` node; every subject exited with code 137 (SIGKILL) and every witness exited 0." The line `Artifacts expire 2027-02-20T21:49:49Z.`
7. `## Environment and versions`. Namespace SKU Premium, 1 processing unit and region uksouth, each cited by its log line number (`probe.log` lines 1582, 1567 and 1576; `full-C3.log` lines 1409, 1394 and 1403), with no log text quoted, because those lines carry ANSI colour escapes; node size `Standard_D4ds_v5`, stated as inferred from `infra/cluster/aks.tf` lines 35, 44 and 51 and not captured; the `versions` object; `source_commit`; each phase's `worker_started_utc` and `judge_completed_utc`.
8. `## Mutation acceptance`. The run ID, its `commit`, `measurement_complete` true and `break_passed` true, the summary SHA-256, and the sentence "That commit is on `main` and includes `f23fa035405cb13699235b03e98f058b3cd6cf25`, the run's head (docs/agents/factory.md \"Evidence pack\")."
9. `## Not tested`. Exactly: "- A consumer that dies before its first commit (K-57): tested under C01, not C10." Then "- Loss under the default automatic store at a kill point other than the 110th message, or when a periodic commit lands between hand-over and the kill: not observed (H2)."
10. `## Sources`. The sources of section 10 that the report's statements rest on, each with its URL and the lines cited there, and the decision 7126256621 link.

The report names no earlier C10 run. A published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").

The six points of decision 7126256621 map to the report as follows; G9 checks each: point 1 to paragraph 1; point 2 to paragraph 2; point 3 to paragraph 3 and the summary; point 4 to paragraph 4 and the H2 line; point 5 to paragraph 5; point 6 to paragraph 6 and "Not tested". The `text_must_state` items of `scenarios/C10/expected.yaml` map as follows: the kill point to "What was tested"; failed commit preconditions to "What was tested"; H2's text to the H2 line; K-57 under C01 to paragraph 6 and "Not tested"; `configured_self_kill` to "Run validity and evidence".

### 4.5 `README.md` line 39

Replace line 39 with exactly (ranges as rendered from the data):

`| C10 | Offset storage and crash outcomes | A consumer killed while holding a message it had not processed, with another member taking over its partitions, left no message marked done that nobody processed: zero in all five repeats that stored each offset only after processing, and zero in all five that used librdkafka's default of storing it when the message is handed over. Some messages were processed twice in every repeat (2 to 4 per repeat after processing, 1 to 5 with the default): after a crash the surviving member redoes the work done since the last periodic commit. The zero under the default does not show that the default is safe: loss there needs a commit to land between hand-over and the kill, which is unlikely at this kill point. A consumer that dies before its first commit is tested under C01. Synthetic workload. | confirmed | [Report](results/C10/2026-11-22.md) |`

The generator prints this row; acceptance step 2 compares it with line 39 byte for byte.

### 4.6 External behaviour relied on

Verified on 2026-11-24 by a reader subagent and checked by this session at each cited line (plan rule 1). Tool: `curl` from `raw.githubusercontent.com/confluentinc/librdkafka/v2.16.0/` and `raw.githubusercontent.com/confluentinc/confluent-kafka-dotnet/v2.16.0/`, hashed with `sha256sum`. No Microsoft page is relied on: the report states no Event Hubs behaviour beyond the SKU, capacity and region that the run's own logs record.

Files: librdkafka v2.16.0 `CONFIGURATION.md` (`b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`), `src/rdkafka.c` (`57ea67830c2d41089258c400b46f902e2d55b45a3ffe125d3010e5c0be0b8a67`), `src/rdkafka_op.c` (`6fdcbcc4e76adb7260de0572e48ed53986414ece262009d1b7e2a04dc202c554`), `src/rdkafka_offset.c` (`bb8142a6b268ab4639b18b21484964875169e53f4c0d3b0639a454145e94a2ec`), `src/rdkafka_cgrp.c` (`4929187b82e7c4baa5e5965bc41e9549d971c0879092959f38d2263b9d7881e1`), `src/rdkafka_assignment.c` (`315e5b2a8ca68c974c9048ac8647aee6202d2e84a3c5215b05871a2d49fecb6f`); Confluent.Kafka v2.16.0 `src/Confluent.Kafka/Consumer.cs` (`0b1b21af9f763a7fc5c81e81fed15a36eadb1c05cd0185a86fb45c6bad6cb3ad`).

1. The default store happens at hand-over, before processing. Confirmed. `CONFIGURATION.md` line 140: `enable.auto.offset.store` default `true`, "Automatically store offset of last message provided to application." `src/rdkafka.c` lines 5126 to 5128: `rd_kafka_consume0`, reached from `rd_kafka_consumer_poll` (lines 5183 to 5193), calls `rd_kafka_fetch_op_app_prepare` ("Store offset, etc") before it returns the message. `src/rdkafka_op.c` lines 1130 to 1156: that function "must be called just prior to passing/returning a consumed message to the application" and sets the position to the message offset + 1 (line 1152) through `rd_kafka_update_app_pos`. `src/rdkafka_offset.c` line 1558 stores it only when `enable_auto_offset_store` is set. Confluent.Kafka `Consume` calls `consumer_poll` (`Consumer.cs` lines 909 to 911).
2. Store-after-process stores only on the application's call. Confirmed. `Consumer.cs` lines 470 to 486: `StoreOffset` stores `result.Offset + 1` through `offsets_store`. `src/rdkafka_offset.c` line 674 rejects a manual store when the automatic store is on; with it off, item 1's store does not run. The baseline sets `enable.auto.offset.store` `false` (`src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 183 at the base commit), and ADR-022 has the subject append its processed row before it stores the offset (docs/blueprint.md line 756).
3. Stored offsets are committed in the background every `auto.commit.interval.ms`. Confirmed. `CONFIGURATION.md` lines 138 and 139: `enable.auto.commit` default `true`, "Automatically and periodically commit offsets in the background"; `auto.commit.interval.ms` default 5000. `src/rdkafka_cgrp.c` lines 552 to 557 start the commit timer. The baseline pins both (`BaselineSettingsCatalog.cs` lines 179 and 181), and C1 changes neither (`scenarios/C10/scenario.yaml`).
4. Other commits. Partly; this is why plain-words paragraph 4 says "commits", not "periodically commits". `src/rdkafka_assignment.c` lines 503 to 512 also commit stored offsets when partitions are removed, at a rebalance or a close, "unless this is a consumer destruction" without close. Inference, not stated by a source: a process killed with SIGKILL runs none of these paths, so it makes no final commit. The sources do not say whether a commit already sent at the moment of the kill takes effect.
5. The member that takes over starts at the group's committed offset. Confirmed. `src/rdkafka_assignment.c` lines 631 to 650: "use the last committed offset for partition", fetched from the group coordinator; lines 214 to 220: with no committed offset, `auto.offset.reset` applies (`CONFIGURATION.md` line 206). The commit precondition keeps that case out of counted repeats (`scenarios/C10/expected.yaml` `assessment_rules.commit_precondition`).
6. Inference from items 1 to 5, as ADR-022 states it (docs/blueprint.md line 792, "inference from S6"): under store-after-process a commit never passes an unprocessed message, and the survivor redoes messages processed since the last commit; under the automatic store, a commit after hand-over and before the kill passes the killed message. The report presents only the counts as measured.
7. The witness runs the baseline in both configurations: `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` line 11 at the base commit ("the witness gets the same window and delay and runs the baseline") and `scenarios/C10/expected.yaml` `assessment_rules.kill_point` ("The witness runs the baseline.").

The report and README state no other Kafka, librdkafka or Event Hubs behaviour.

### 4.7 `docs/operations/evidence/issue-512-c10-verdict/receipt.md`

Records: the six input artifacts, two logs and the mutation summary with hashes (section 4.1); the generator commands and their outputs (section 9); and the mutation-relevance finding of section 4.9, as provenance. The receipt names no other C10 run, because no other C10 run on `main` produced a candidate this plan reads; it states no failure cause of any run.

### 4.8 Publication record

No state file is written (section 2).

### 4.9 Mutation acceptance (prerequisite)

docs/agents/factory.md "Evidence pack" requires a passing `verifier-c5-mutation.yml` run on `main` whose `summary.json` reports `measurement_complete: true` and `break_passed: true` and names a commit whose history includes the merge commit of each mutation-input change; "A missing, failed or incomplete latest applicable run blocks evidence-pack publication."

Evidence, 2026-11-24:
- The newest passing run, [45028998990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45028998990), measured `9c7fd04eaf015fa0896fb89637fa99acec6ce051` (`summary.json` `commit`), an ancestor of the run head.
- Mutation inputs changed between that commit and the run head: `git diff --stat 9c7fd04e f23fa035` lists `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` and the mutation qualification and runner files, and `c5_runtime.py check --base 9c7fd04eaf015fa0896fb89637fa99acec6ce051 --head f23fa035405cb13699235b03e98f058b3cd6cf25`, run from a checkout of the run head, returns `{"reasons": ["invalid_renewal", "missing_measurement", "stale_inputs"], "status": "rejected"}`, not `not_applicable`. So run 45028998990 does not cover the run head.
- The newest run, [45270881762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45270881762) on `e62bdc9b39620897c63e615b059d20646b0546e4` (a descendant of the run head), concluded `failure`; its failed job is `mutation (phase-p1, Judge/PhaseJudge.cs)`. Mutation shard timing is the subject of open issue #494.

Therefore the acceptance this pull request needs does not exist yet. The implementer requires (G10): a `verifier-c5-mutation.yml` run with `event` `workflow_dispatch`, `head_branch` `main`, `conclusion` `success`, whose `summary.json` has `measurement_complete` true, `break_passed` true and a `commit` that is on `main` and has `f23fa035405cb13699235b03e98f058b3cd6cf25` as an ancestor; and it must be the newest completed `verifier-c5-mutation.yml` run on `main` when the implementer downloads it. The ancestry is checked by acceptance step 6, not by the generator. See "Deferred work".

## 5. Requirements

- R1. WHEN the generator starts, THEN it checks every hash of section 4.1, including the two mutation hashes passed as arguments, and fails `G1` on any difference.
- R2. WHEN both candidates are read, THEN each must have `scenario_id` `C10`, `configuration_id` and `phase` equal to its configuration, `run_id` `c10-45074268881-1`, `source_commit` `f23fa035405cb13699235b03e98f058b3cd6cf25`, `synthetic` true, root `status` and `consumer.status` `complete_valid` with empty `reasons`, and exactly repeats 1 to 5, each with `status` and `consumer.status` `complete_valid` and empty `reasons`; and the two `versions` objects must be equal. Otherwise `G2`.
- R3. WHEN the rules of section 4.2 are recomputed, THEN each result and each precondition must equal section 4.2. Otherwise `G3`, and the implementer stops (a finding, not a fix).
- R4. WHEN root counts are compared, THEN each root `counts` key equals the sum over repeats of that key, and each root `consumer.counts` key equals the sum of the repeats' `consumer.counts`. Otherwise `G4`.
- R5. WHEN the archive receipts are read, THEN each has `archive_complete` true, `gaps` `[]`, `kind` `run_evidence_archive_receipt`, `identity` with `github_run_id` `"45074268881"`, `github_run_attempt` 1, `phase` equal to its configuration, `run_id` `c10-45074268881-1` and `source_commit` `f23fa035405cb13699235b03e98f058b3cd6cf25`, and `manifest.sha256` equal to the manifest member hash. Otherwise `G5`.
- R6. WHEN the phase-status members are read, THEN each has `phase` equal to its configuration, `run_id` `c10-45074268881-1`, `safety_stop` false, `candidate.status` `complete_valid`, `candidate.repeats` `[[1, "complete_valid"], ..., [5, "complete_valid"]]`, and every `modes` entry `outcome` `failed` for mode `consumer-subject`, `ready` for mode `consumer-readiness` and `complete` for every other mode, with exactly 31 entries (6 per repeat and the judge). Otherwise `G6`.
- R7. WHEN run with `--check`, THEN the rendered JSON and report equal the committed files byte for byte, and the printed README row equals `README.md` line 39. Otherwise `G7`.
- R8. WHEN the report is rendered, THEN it contains only ASCII, and so does the README row. Otherwise `G8`.
- R9. WHEN the report is rendered, THEN it contains, verbatim, the title, the summary, the `## In plain words` heading and its six paragraphs, the kill-point and commit-precondition quotations, the H2 indented line, the `configured_self_kill` sentence and the two "Not tested" lines; the `## In plain words` heading comes before `## What was tested`, and no text between the title and `## In plain words` contains any of `committed_never_processed`, `processed_again`, `subject_commit_coverage`, `offset_read` or `complete_valid`; and the README row contains the sentence beginning "The zero under the default does not show". Otherwise `G9`.
- R10. WHEN the mutation summary is read, THEN `measurement_complete` is true, `break_passed` is true, `run_id` equals `--mutation-run`, and `commit` is a 40-character lowercase hexadecimal SHA. Otherwise `G10`.
- R11. The checks run in the order G1, G2, G3, G4, G5, G6, G10, then rendering, G8, G9, and G7 only with `--check`. The first failure prints `FAIL G<n>: <reason>` and exits 1.
- R12. `--self-test` runs the real inputs, printing `PASS real_inputs`, then each case of section 7 on an in-memory copy after G1, printing `PASS <case>` when the case fails at its expected check, and exits 0 only if every case matched. G7 runs in self-test only for case `report_edit`.

## 6. Invariants and safety rules

- No edit to an expected outcome, a verdict rule, a verifier output or a candidate (AGENTS.md GOVERNANCE "Verdict integrity"). A recomputation that disagrees with a candidate or with section 4.2 is a finding: stop under section 11.
- No dispatch by the implementer, no Azure access, no secret, tenant ID or subscription ID (hard safety rules 2, 3 and 7). The generator copies no log text other than the lines section 4.4 cites, and those by number only.
- No number that was not measured or computed by script (hard safety rule 11; docs/agents/pull-requests.md rule 4).
- The published results show run 45074268881 only.
- The pull request is labelled `verdict`, touches protected paths and is merged by Hari only.

## 7. Test map

The tests are the generator's `--self-test` cases. Each changes one value in memory after G1. No existing test changes, so plan rule 2's standing fixture rule has no case; if one arose, the implementer follows it exactly: (a) change the fixture so it passes the new checks, keeping every assertion and boundary, or (b) where invalid input is the test's purpose, keep the input and change expected values to exactly what the checks produce; never remove a case, loosen an assertion or change a check, and list each change with the check that forces it in the PR implementation notes.

| Requirement | Case | Change | Expected |
|---|---|---|---|
| R1 | `hash_c0_candidate` | one byte of the C0 `candidate.json` member, before G1 | `FAIL G1` |
| R1 | `expected_yaml_edit` | one byte of `expected.yaml`, before G1 | `FAIL G1` |
| R1 | `mutation_hash` | `--mutation-summary-sha256` with its last hex digit changed, before G1 | `FAIL G1` |
| R2 | `c1_repeat_status` | C1 repeat 3 `consumer.status` to `incomplete` | `FAIL G2` |
| R2 | `versions_differ` | C1 `versions.librdkafka` to `2.15.0` | `FAIL G2` |
| R3 | `h1_loss_one` | C0 repeat 2 `consumer.counts.committed_never_processed` to 1 | `FAIL G3` (H1 contradicted) |
| R3 | `h1_precondition_failed` | C0 repeat 4 `subject_commit_coverage.uncovered_partitions` to `[3]` | `FAIL G3` (repeat not counted; H1 inconclusive) |
| R3 | `h2_loss_one` | C1 repeat 5 `consumer.counts.committed_never_processed` to 1 | `FAIL G3` (H2 holds, not `inconclusive`) |
| R3 | `h2_precondition_failed` | C1 repeat 1 `subject_commit_coverage.uncovered_partitions` to `[0]` | `FAIL G3` (precondition) |
| R3 | `extra_rows_differ` | C0 repeat 3 `consumer.counts.processed_again_extra_rows` 2 to 3 | `FAIL G3` (precondition) |
| R3 | `processed_again_zero` | C1 repeat 2 `consumer.counts.processed_again` and `processed_again_extra_rows` 1 to 0 | `FAIL G3` (precondition) |
| R3 | `kill_count` | C1 repeat 4 `consumer.kill_count` to 111 | `FAIL G3` (precondition) |
| R3 | `offset_read_failed` | C0 repeat 1 `consumer.offset_read.outcome` to `kafka_exception` | `FAIL G3` (precondition) |
| R3 | `subject_exit_zero` | C0 repeat 5 subject container exit code to 0 | `FAIL G3` (precondition) |
| R3 | `node_split` | C1 repeat 2 witness `spec.nodeName` to another name | `FAIL G3` (precondition) |
| R3 | `self_kill_missing` | C1 manifest: the repeat-3 `configured_self_kill` entry's `gap_code` to null | `FAIL G3` (precondition) |
| R4 | `root_consumer_sum` | C0 root `consumer.counts.processed_again` 15 to 14 | `FAIL G4` |
| R4 | `root_producer_sum` | C1 root `counts.ok` 1200 to 1199 | `FAIL G4` |
| R5 | `archive_gap` | C0 receipt `gaps` to `["x"]` | `FAIL G5` |
| R5 | `archive_phase` | C1 receipt `identity.phase` to `C0` | `FAIL G5` |
| R6 | `phase_safety_stop` | C0 phase-status `safety_stop` to true | `FAIL G6` |
| R6 | `subject_mode_complete` | C1 phase-status repeat-1 `consumer-subject` `outcome` to `complete` | `FAIL G6` |
| R10 | `mutation_incomplete` | summary `measurement_complete` to false | `FAIL G10` |
| R10 | `mutation_run_id` | summary `run_id` to a different run ID | `FAIL G10` |
| R8 | `non_ascii` | an en dash in the rendered report | `FAIL G8` |
| R9 | `missing_plain_words` | delete paragraph 4 of `## In plain words` from the rendered report | `FAIL G9` |
| R9 | `verifier_term_early` | insert `committed_never_processed` into the summary paragraph of the rendered report | `FAIL G9` |
| R7 | `report_edit` | change one table cell of the committed report, with `--check` | `FAIL G7` |

Boundaries: H1 and H2 read `committed_never_processed` at 0 (pass) and 1 (`h1_loss_one`, `h2_loss_one`); the counted-repeat rule reads `uncovered_partitions` `[]` (pass) and one partition (`h1_precondition_failed`, `h2_precondition_failed`); "processed exactly twice" reads `processed_again_extra_rows` equal (pass) and one more (`extra_rows_differ`). 28 cases.

## 8. Deployment assumptions

None. The pull request deploys nothing and runs no code against a container, image, chart or runtime. All live evidence is recorded output of the completed run, pinned by hash in section 4.1; no assumption about an open or fresh connection, or an idle or loaded state, is relied on. The mutation summary is recorded output of a future run, checked by G1, G10 and acceptance step 6.

## 9. Acceptance

Run from the repository root, with the inputs of section 4.1 in a scratch directory `$IN` outside the repository.

1. `sha256sum` of each ZIP, member and log equals section 4.1, and the mutation ZIP hash equals its artifact API `digest`. Evidence: the output in the receipt.
2. `python3 -I docs/operations/evidence/issue-512-c10-verdict/make_results.py --inputs "$IN" --mutation-run <id> --mutation-zip-sha256 <hash> --mutation-summary-sha256 <hash>` writes the two results files and prints the README row. Evidence: exit 0.
3. The same command with `--check`. Evidence: exit 0 and `PASS check`.
4. The same command with `--self-test`. Evidence: `PASS real_inputs` and one `PASS <case>` line for each of the 28 cases of section 7, exit 0.
5. `python3 -m json.tool results/C10/2026-11-22.json > /dev/null`. Evidence: exit 0.
6. `git merge-base --is-ancestor f23fa035405cb13699235b03e98f058b3cd6cf25 <summary commit>` and `git merge-base --is-ancestor <summary commit> origin/main` both exit 0; `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/verifier-c5-mutation.yml/runs?per_page=5` shows that run as the newest completed run on `main`, with `conclusion` `success`. Evidence: the outputs in the receipt.
7. `npm run check:docs`. Evidence: no warning on a changed file, or each explained in the pull request.
8. `LC_ALL=C grep -rnP '[^\x00-\x7F]' results/C10/ docs/operations/evidence/issue-512-c10-verdict/` and the same on line 39 of `README.md`. Evidence: no output.
9. `git diff --name-only origin/main...HEAD` lists exactly the five paths of section 3, and `git diff origin/main...HEAD -- README.md` changes only line 39.
10. Cold review under AGENTS.md "Before opening a pull request" rule 8; when Codex implements, the Claude review gate of decision 6633528258 point 2; Codex review and Codex security review requested as hard safety rule 10 allows; the pull request labelled `verdict`.

`scripts/verify-dotnet.sh` is not required: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates").

## 10. Sources

- Run [45074268881](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45074268881), its artifacts and job logs, read 2026-11-24 through the GitHub REST API.
- Mutation runs [45028998990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45028998990) (`summary.json` `commit` `9c7fd04eaf015fa0896fb89637fa99acec6ce051`, `measurement_complete` true, `break_passed` true) and [45270881762](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45270881762) (`failure`), read 2026-11-24.
- librdkafka v2.16.0 `CONFIGURATION.md`, `src/rdkafka.c`, `src/rdkafka_op.c`, `src/rdkafka_offset.c`, `src/rdkafka_cgrp.c` and `src/rdkafka_assignment.c`, and Confluent.Kafka v2.16.0 `src/Confluent.Kafka/Consumer.cs`, at the hashes and lines of section 4.6.
- `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` lines 179, 181 and 183 and `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` line 11 at the base commit.
- `scenarios/C10/expected.yaml`, `scenarios/C10/scenario.yaml`, `scenarios/profiles/c03-consumer.yaml`, docs/blueprint.md ADR-022 and section 12 line 1276, and docs/operations/verifier-contract.md lines 128 and 136, at the base commit.
- `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 63 to 74 at the base commit, for the commit evidence of section 4.4.
- `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit (`vm_size = "Standard_D4ds_v5"` for all three pools).
- Decision [7126256621](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7126256621).

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when a pinned hash in section 4.1 differs from a fresh download; when G2 to G6 or G10 fails on the real inputs (a finding under "Verdict integrity", not a fix); when no mutation run meets section 4.9; or when a predecessor pull request of #510 is not merged.

## 12. Choices made under "Act or ask"

These are ordinary reversible choices within authority (docs/agents/factory.md "Act or ask"), and the grilling questions answered under standing decision 6895535291 point 2; Hari may change any of them when he reviews the verdict pull request.

1. Wording. The title, summary, plain-words paragraphs and README row of sections 4.4 and 4.5 carry the six points of decision 7126256621 in its own words where it gives them, and add only the definition of "marked done", the sentence that only the killed consumer changes setting, and the background-commit sentence, each from section 4.6.
2. "Marked done". The report uses "marked done" for a committed offset that has moved past a message, defined once in plain-words paragraph 1, and gives the verifier name `committed_never_processed` only after the plain words.
3. "Processed twice". Used because `processed_again_extra_rows` equals `processed_again` in every repeat; G3 enforces that precondition.
4. Reassignment time. Not published for C10; it stays in the copied candidates. It is C01's measure.
5. C01 reference. The report says K-57 "is tested under C01 (#513)", not that a C01 result exists, because C01 is unpublished at the base commit (`README.md` line 30).
6. Mutation acceptance. The plan does not ask for a dispatch: open issue #494 is repairing the mutation workload whose newest run failed, and a dispatch is not this item's to make. Section 4.9 and "Deferred work" state the gate.

## 13. Deferred work

- Mutation acceptance on `main` covering `f23fa035405cb13699235b03e98f058b3cd6cf25` (section 4.9). Evidence: section 4.9. On the verdict path: yes; docs/agents/factory.md "Evidence pack" makes it a publication gate. Deferral is permitted because it cannot be completed by this pull request: it needs a passing hosted run on `main`, and the current newest run failed under open issue #494. Responsible: the session that dispatches the next `verifier-c5-mutation.yml` campaign on `main` under #494, coordinated by the batch's control session ([standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291) point 1). Stage: before the implementer starts. Workload bound: none set here; the campaign's own limits are #494's. Exhaustion action: push -wip and hand off to the next session (AGENTS.md "Before opening a pull request" rule 11), naming the newest mutation run. Completion evidence: a run meeting G10 and acceptance step 6. Gate it blocks: G10, and so the verdict pull request.

## 14. Plan-check answers

(a) No step's effort scales with a number unknown at planning time. The inputs are fixed: two candidates, two archive receipts and manifests, two pods artifacts, two job logs and one mutation summary. The self-test has exactly 28 cases plus the real-input run.

(b) The parent's split row 9 says only "One verdict pull request each for C03, C10 and C01". This plan is stricter: it adds the generator's recomputation of every expected.yaml rule, its archive, phase-status and mutation checks, the plain-words ordering check of R9, and its self-test. Why: docs/agents/pull-requests.md rule 4 requires counts and totals by script; AGENTS.md "Verdict integrity" makes any disagreement with a candidate a finding, which only a recomputation can detect; and decision 7126256621 requires the plain words before any verifier term, which only a check can hold.

(c) Dependencies neither on `main` nor in the pull request: the run's artifacts and job logs, recorded output pinned by hash in section 4.1 and retained until 2027-02-20T21:49:49Z; the mutation summary of a run that does not exist yet, checked by G1, G10 and acceptance step 6 (section 4.9); the librdkafka and Confluent.Kafka sources, pinned by tag and hash. No deployment assumption exists (section 8).

## 15. Open questions for Hari

1. How is decision 7126256621's addition to `text_must_state` recorded? The decision says "This adds to the `text_must_state` list in `scenarios/C10/expected.yaml`; it changes no expected outcome or verdict rule." Expected outcomes are outside the advance adoption of grilling answers ([standing decision 6895535291](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291) point 4), so this is asked, not assumed.
   - (A, recommended) The verdict pull request does not edit `scenarios/C10/expected.yaml`. The decision itself is the binding addition; the plan encodes its six points as required report text (section 4.4), G9 enforces them, and the results JSON links the decision (`publication_decision`). Why: the run was judged against `expected.yaml` at SHA-256 `21097f98...`, and the evidence pack must carry "the scenario definition and expected outcome as approved"; editing the file after the run changes the hash the published result pins, and AGENTS.md "Code Review Rules" makes "Any change to an existing `expected.yaml`" a P0 to defend in review.
   - (B) The verdict pull request also appends the six points to `verdict.text_must_state` in `scenarios/C10/expected.yaml`. Then the scope adds that file (protected), the results JSON pins both the run-time hash and the new hash, and G1 checks the new hash. A plan revision would pin the literal YAML text.
   Yes accepts (A).
