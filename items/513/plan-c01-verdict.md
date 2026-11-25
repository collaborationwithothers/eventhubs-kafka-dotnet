# C01 verdict contract-grade plan

Revision: 4. Base commit: `main` at `e3c8eb9462a6ecae139a651c5eef39812be07801`.

This plan writes the C01 evidence pack and verdict pull request from batch run 45124695288, whose two phases are both `complete_valid` with complete archives. At `session.timeout.ms` 30000 the takeover fell inside its predicted window in all five repeats, and at 45000 it exceeded its upper bound in all five, so the expected outcome selects `contradicted`. The report explains, in plain words and before any verifier term, the production event, what happened step by step, what was measured, and what `contradicted` does and does not mean, as Hari decided on 2026-11-24.

Item: C01, `session.timeout.ms` 30000 (Microsoft) versus 45000 (librdkafka default), with K-57 (killed before the first commit, `auto.offset.reset` at its baseline `latest`) folded in [issue #513; parent entry 10 of #499]. Workload data is synthetic. Every number below is copied from a pinned run artifact, a pinned log or a cited source, or computed from them by script on 2026-11-24; no number is an estimate unless labelled.

## 1. What changed

Revision 4 applies Hari's [decision 7168528766](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7168528766) ("revert"), which supersedes decisions 7128051335 and 7128617765 after Codex's P0 on pull request #596 under AGENTS.md "Code Review Rules" ("Any change to an existing `expected.yaml` stays P0"):
- Point 1, option A: the pull request does not edit `scenarios/C01/expected.yaml` and does not change `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs`. Revision 3's sections 4.10 and 4.11, the check G11 (R13), its two self-test cases and the edited-hash pin are removed. The scope is five files (section 3), and the self-test has 36 cases (section 7).
- Point 2: the eight points of decision 7126257034 still bind the published text. The generator checks them as required report text (section 4.4, R9), the results JSON links that decision (`publication_decision`), and the results pin only the expected outcome the run was judged against (SHA-256 `7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb`).
- Because no file under `src/` or `tests/` changes, the verification gates of docs/agents/pull-requests.md "Verification gates" steps 1 and 3 no longer apply (section 9).
- Mutation acceptance now exists: run 45555996117 on `main` at `e3c8eb9462a6ecae139a651c5eef39812be07801`, which includes the run head (section 4.9). Its summary is pinned in section 4.1, and the "Deferred work" item is closed (section 13).
- The base commit moves to `e3c8eb9462a6ecae139a651c5eef39812be07801`. `git diff 3e7163dc641e724437025d917032fd40419e6c4d e3c8eb9462a6ecae139a651c5eef39812be07801` changes no file this plan cites or changes; the repository links in section 4.4 stay at `3e7163dc` and show the same lines.
- The `apply in implementation` P2 of the [revision 3 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128338634) is applied with its replacement text (the "Commit evidence" sentence, section 4.4 item 4), and its plan-text slips are fixed: section 7's statement about existing tests, the acceptance step numbering, and "or or" in section 11.
- Section 2 records the decisions and the approval posted after revision 3.

This revision changes scope and a check, so it needs a new check.

Revision 3 applied decision 7128051335 (option B and the test hash), now superseded. Revision 2 applied the [plan check of revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7127640270): the expected-outcome question went to Hari, and published text stopped stating inferences as measurements.

## 2. Goal and authority

Goal: publish C01's proposed verdict with the evidence pack that docs/agents/factory.md "Evidence pack" requires, from run 45124695288 only, so Hari can approve or reject it.

Authority, in creation order. No item state file is read: files at `items/<issue>/state.md` are historical (docs/agents/factory.md "Decision log", last paragraph).

1. [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513), created 2026-11-15T13:50:16Z, issue `updated_at` 2026-11-24T08:32:21Z: "Verdict pull request for C01 from the batch run of entry 7. Verdict pull requests go to Hari." It names `Parent items: #499`, `Split entry: 10 (C01 verdict)`, `Predecessor PRs: the pull request of #510 (entry 7)`, and asks the plan to read [proposal revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135) and the #499 decisions [6894534441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894534441), [6894564479](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894564479), [6894587233](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894587233) and [6894614673](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894614673). It names Claude for planning, with the `grilling` skill and then the `plan-pr` skill, and Codex for implementation, with the Claude review gate of decision 6894587233.
2. [Decision 6895539750](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-6895539750), 2026-11-15T14:03:42Z, adopts [standing decision 6895535291 on #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6895535291): the control session (point 1), grilling answers adopted in advance (point 2), plan approval on a check with `Blocking findings: none` and "Open questions for Hari" `None` (point 3); not covered (point 4): money, Azure dispatch, expected outcomes and verdicts, protected-path merges, scope beyond the approved split, and the `ready-for-agent` label.
3. [Decision 7126257034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034), 2026-11-24T08:32:20Z. Hari: "ok start planning the verdict PRs . All this needs to be clear in that like you have explained it to me". The published C01 result must make eight points clear in plain words, before any verifier terms: the scenario as a production event; what happens step by step; what was measured; what the verdict means and does not mean; the skip count growing with takeover time, at about 2 messages per second, labelled an inference; a shorter timeout shrinking the loss without removing it; remedies not tested, cited if named; and when the case happens in production, contrasted with the C10 case. It states: "This adds to the `text_must_state` list in `scenarios/C01/expected.yaml`; it changes no expected outcome or verdict rule." Section 4.4 encodes the eight points; decision 7168528766 (item 9) settles how the addition is recorded: as required report text, not an edit of `expected.yaml`.
4. Superseded by item 9. [Decision 7128051335](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128051335), 2026-11-24T09:54:30Z, answering handoff 7127860772. Hari: "Yes B". Point 1: option B, as for C10 ([#512 decision 7127777487](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7127777487)): the verdict pull request edits `scenarios/C01/expected.yaml`, appending the points of decision 7126257034 to `verdict.text_must_state`; the plan gives the literal YAML; the results pin both hashes; "The edit adds required published text only; it changes no hypothesis, window, holds/contradicted rule or verdict rule." Point 2: the scope adds `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs`, whose `C01` hash at line 149 becomes the edited file's hash, with a comment citing the decision; the `C10` line is changed by the C10 verdict pull request, and whichever merges second rebases.
5. Comment [7127778299](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7127778299), 2026-11-24T09:42:28Z, body `Tool: Claude`. It is not a `## Decision` comment; under docs/agents/factory.md "Item lifecycle" step 2 a comment naming the tool before `ready-for-agent` is applied selects the tool, and the implementing session determines it then.
6. [Decision 7128366867](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128366867), 2026-11-24T10:08:19Z: the control session records the adoption of grilling answers Q1 to Q9 under standing decision 6895535291 point 2.
7. [Approval 7128367275](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128367275), 2026-11-24T10:08:20Z, of revision 3 under standing decision point 3. Revision 4 is an amendment; decision 7168528766 point 3 directs it and sets how it is approved (item 9).
8. Superseded by item 9. [Decision 7128617765](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7128617765), 2026-11-24T10:19:20Z: a change to the line-153 comment of the test file.
9. [Decision 7168528766](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7168528766), 2026-11-25T16:47:10Z. Hari: "revert". Point 1: option A replaces option B; the pull request does not edit `scenarios/C01/expected.yaml` and does not change the test file, "neither the hash line nor the line-153 comment"; this supersedes decisions 7128051335 and 7128617765. Point 2: the plain-words requirements of decision 7126257034 still bind the published text; the generator checks each point as required report text; the results JSON links that decision; "The results pin only the expected outcome the run was judged against." Point 3: this revision gets a fresh check, and a clean result approves it under standing decision point 3, under the existing `ready-for-agent` label; the implementation session then updates pull request #596 to it.

What this plan relies on from the decisions the issue body names:
- 6894534441 point 2: the consumer ends its own process at a point fixed before the run. Point 4: Codex implements.
- 6894587233 point 2 and 6894614673 points 1 and 2: Claude plans with `grilling` then `plan-pr`; Codex implements; each Codex pull request gets the Claude review gate of [decision 6633528258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633528258) point 2.

K-57 placement: decision 6894534441 point 3 put K-57 under C10; the merged blueprint section 12 row (`docs/blueprint.md` line 1276 at the base commit) places it under C01 "for #499, with the subject killed at its first message", and `scenarios/C01/expected.yaml` tests it as H3. This plan follows the merged files.

Grilling. Under standing decision 6895535291 point 2, the grilling round for this plan was written with recommended answers, and the batch's control session (`session_01AjonZt9g5B8rzRapBsEYGw`, point 1) reported on 2026-11-24 that questions Q1 to Q9 are adopted as recommended. Section 12 lists each question and its adopted answer. The control session records the adoption in a `## Decision` comment on #513 under point 2; this plan relies on no answer outside those recommendations. The one question in a reserved area (expected outcomes, point 4 of that decision) went to Hari and is answered by decision 7168528766. Decision 7128366867 records the adoption.

Other #499 decisions (6895299617, 6895328446, 6895343227, 6899939624, 6926376436, 6958521790) are not linked and adopted by a decision on #513, so under docs/agents/factory.md "Item lifecycle" step 3 this plan relies on none of them. Their substance that matters here is merged in ADR-022, `scenarios/C01/expected.yaml` and docs/operations/verifier-contract.md. The merge method decision is irrelevant: the verdict pull request goes to Hari (AGENTS.md GOVERNANCE "Verdict integrity").

Predecessors: #510's pull requests [568](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/568), [572](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/572) and [573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/573) are merged (2026-11-21). The run's head `2d370de00059df30506154c1fd504d0f81d237bc` is an ancestor of the base commit. The implementer verifies all three merged before starting (factory.md "Item lifecycle" step 3).

Run evidence handed over: [#510 comment 7124315789](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/510#issuecomment-7124315789) reports run 45124695288 and its per-repeat values. That comment is a handoff, not a source; every number here was re-read from the artifacts by script.

Design authority on `main` at the base commit, each equal to the run's head by `sha256sum` on 2026-11-24: `scenarios/C01/expected.yaml` (SHA-256 `7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb`), `scenarios/C01/scenario.yaml` (`555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2`), `scenarios/profiles/c01-consumer.yaml` (`d36530d2cf026b1c7519241f9d613878b9506e20eaecb0d54f36a2be0da67b02`); docs/blueprint.md ADR-022 (from line 737); docs/operations/verifier-contract.md lines 128 and 136 (consumer block, `reassignment`, `offset_read`, root projection); docs/agents/factory.md "Evidence pack".

Candidate selection: #513 is a child of the #499 split, entry 10. Its one pull request is named `C01 verdict`; it is open as pull request #596 and unmerged, so its plan remains the candidate for revision. It is one coherent piece: one evidence pack for one run.

Implementing tool: Claude Code. The `Tool: Claude` comment (item 5) preceded the `ready-for-agent` label, which selects the tool under docs/agents/factory.md "Item lifecycle" step 2, and pull request #596 is a Claude Code pull request. The Claude review gate of decision 6633528258 point 2 applies to Codex pull requests and so does not apply here. This plan does not change the tool.

## 3. Scope

Files allowed to change, and only these five:

| Path | Change | Protected |
|---|---|---|
| `results/C01/2026-11-23.json` | new | yes, `/results/` |
| `results/C01/2026-11-23.md` | new | yes, `/results/` |
| `README.md` | line 30, the C01 row, only | yes, `/*.md` |
| `docs/operations/evidence/issue-513-c01-verdict/make_results.py` | new | no |
| `docs/operations/evidence/issue-513-c01-verdict/receipt.md` | new | no |

The date is the run's UTC creation date, 2026-11-23 (run `created_at` 2026-11-23T06:31:38Z), as in `results/C03/2026-11-19.md` for run 44562323424 created 2026-11-19T16:06:27Z. Phase C1 ran on 2026-11-24; the report states both phases' times.

The generator may start from a copy of `docs/operations/evidence/issue-511-c03-verdict/make_results.py` (866 lines at the base commit); it is a new file in the new folder, and the C03 file does not change.

Size estimate (estimate, 2026-11-24; basis: the C03 precedent at the base commit, whose generator is 866 lines by `wc -l`, and this plan's similar rule set with more published text): about 800 lines for `make_results.py`, 60 for `receipt.md`, 220 for the report and 1 changed line in `README.md`. The results JSON is generator output and is not estimated by hand. Five files.

Paths that must not change: `scenarios/` (all, including `scenarios/C01/expected.yaml`, decision 7168528766 point 1), `src/`, `tests/` (all, including `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs`), `charts/`, `infra/`, `scripts/`, `.github/`, `docs/blueprint.md`, `docs/agents/`, `docs/operations/` outside the new evidence folder, every other path under `results/` and `items/`. No workflow is dispatched by the implementer. No expected outcome, decision rule, verifier output or candidate is edited (AGENTS.md GOVERNANCE "Verdict integrity").

The results files are generator output. docs/agents/pull-requests.md rule 8 says "Agents commit no program output"; its paragraph beginning "Some Code Review Rules flag work that only Hari can approve" keeps such work only if "the verdict step requires it", which it does here, as in every earlier verdict pull request. The pull request description lists the protected-path and results-file findings under that paragraph and leaves the merge to Hari.

## 4. External contracts

### 4.1 Inputs

The generator reads only these inputs from one directory `$IN`, each pinned by SHA-256. Each ZIP was downloaded on 2026-11-24 through `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip`, and each ZIP hash equals the artifact API `digest`. Logs came from `gh api .../actions/jobs/<id>/logs`.

| File in `$IN` | Source | SHA-256 |
|---|---|---|
| `c01-C0-candidate.zip` | artifact 14867803464, `c01-C0-candidate-45124695288-1` | `bd388eac5dc682ca9f3d0a73d94847192aff6cbfef6d5cc120c70b84aac15fb3` |
| its member `candidate.json` | | `0448245936d7d27756b17088a81c17c208d8349451ac5a262202e9649d37929f` |
| `c01-C1-candidate.zip` | artifact 14895402374, `c01-C1-candidate-45124695288-1` | `52105da54df8e4e95415c02eaf925163980e70479d5d35eaf48c4daff5fbb596` |
| its member `candidate.json` | | `779a2625b0f7f817d6caa3ba1a62a6aace909be0263996af314e1979b98e149d` |
| `c01-C0-archive.zip` | artifact 14870530520, `c01-C0-archive-45124695288-1` | `be25c6ac639f362a5716575f9becab797b66ff927fba25ad15354aedf6b77772` |
| its member `archive-receipt.json` | | `df75ed9da0e8bdd11001485f0ab09badf3642cfdc66ba57f971bd38246632308` |
| its member `archive-manifest.json` | | `0d7cd201bc7e9d1cbb18db622b75e16c1c4084a38f08c91e5b710a46be281e94` |
| `c01-C1-archive.zip` | artifact 14896405743, `c01-C1-archive-45124695288-1` | `dac66b1feb1f5107e29dd2aec2e681db11b8869f85854e49c5fe040fd233a6ac` |
| its member `archive-receipt.json` | | `0a5e9d3577323f06f8a6969ef15cd45f0d7bb9acc0619bb5b77b65599a6802df` |
| its member `archive-manifest.json` | | `eb2d4f785a3e89ac5b2993e023177ffc934ffabcec49649b7a6d01de86ab8573` |
| `c01-C0-pods.zip` | artifact 14868618067, `c01-C0-pods-45124695288-1` | `e49f1a3f2088318fe1ea88acd70f38ac46fce463c335a1b5c9c2dbe2fefd7ad5` |
| its member `phase-status.json` | | `6741510c7377cb3bc524e766bab8fa4474c3c132f4183381d512b163a4c86df7` |
| `c01-C1-pods.zip` | artifact 14895726646, `c01-C1-pods-45124695288-1` | `aff647840c189209f12e6bdd0edfdba00c7a87a18e8da6833ed19ac92b19ba0a` |
| its member `phase-status.json` | | `605647a1a8c64c2257867f324e3a98c6a825d5cdfd2e071d92bb7b86d8381039` |
| `probe.log` | job 137529776430 (`probe`, ran C0) | `655d77582f398d448fedd8a33c1e96da87f4e3ccce8c8aa5dbc60482a914fffe` |
| `full-C3.log` | job 138067157139 (`full-C3`, ran C1) | `ba2481fa746610661a8d94ec2d91a818bd9e39fbf3a2bc1c24af79efd0b92439` |
| `c5-summary.zip` | artifact 14997997843, `c5-summary-45555996117-1`, of mutation run 45555996117 (section 4.9) | `764cdd3789329d07553303cd3b2508e640426499c6aaeee2f8224f01db04aae3` |
| its member `summary.json` | | `bb52a814b734277bd088a8b3745db6041c2bf5bc58ac57fb240588068667282d` |
| `scenarios/C01/expected.yaml` | working tree, equal to the base commit and the run head; the expected outcome the run was judged against | `7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb` |
| `scenarios/C01/scenario.yaml` | working tree, equal to the base commit and the run head | `555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2` |

The generator holds every hash of this table as a constant, the mutation summary's included, and takes no hash argument. Each archive receipt's `manifest.sha256` equals its manifest member hash above. The generator reads the pods members `<job>/pods.json` for the consumer subject and witness of each repeat by path inside the ZIP, covered by the ZIP hash. Run artifacts expire 2027-02-21T06:31:39Z and the mutation summary artifact 2027-02-23T05:47:27Z (artifact API `expires_at`); the report states the run artifacts' date. Phase mapping: `probe` ran C0 (21:33:08Z to 22:38:25Z on 2026-11-23) and `full-C3` ran C1 (05:36:19Z to 06:35:15Z on 2026-11-24), as the phase-status members' `phase` fields and the job times show. Jobs `batch`, `batch-stop` and `full-C0` were skipped.

If any pinned hash differs from a fresh download, the implementer stops under section 11.

### 4.2 Pinned outcomes

The generator recomputes each rule of `scenarios/C01/expected.yaml` from the candidates, exactly as written there, and requires these results (G3):

| Rule | Recomputed result |
|---|---|
| `configurations.C0.H1_removal_follows_30000` | `holds` |
| `configurations.C0.early_removal` (finding) | no finding: no counted repeat below 27000000 |
| `configurations.C0.H3_k57_latest_reset_skips_messages` | `holds` |
| `configurations.C0.reassignment` (recorded only) | `[31794219, 32994458, 35956669, 38562494, 37192516]` |
| `configurations.C1.H2_removal_follows_45000` | `contradicted` |
| `configurations.C1.early_removal` (finding) | no finding: no counted repeat below 42000000 |
| `configurations.C1.H3_k57_latest_reset_skips_messages` | `holds` |
| `configurations.C1.reassignment` (recorded only) | `[63539433, 68186056, 62547934, 68071749, 61342653]` |
| `verdict` | `contradicted` |

Evaluation, for each hypothesis: `holds` when its `holds_when` is true; else `contradicted` when its `contradicted_when` is true; else `inconclusive`. A repeat is `complete_valid` when its candidate repeat `status` and `consumer.status` are both `complete_valid`. Under `assessment_rules.counted_repeats`, a repeat is counted for H1 and H2 when it is `complete_valid` and its `consumer.reassignment.microseconds` is not null; for H3 and `early_removal`, when it is `complete_valid`. "All 5 repeats" means exactly repeats 1 to 5. H1 holds when all 5 are counted and each value is `>= 27000000` and `< 45000000`; H1 is contradicted when all 5 are counted and at least one is `>= 45000000`. H2 is the same with `42000000`, `60000000` and `60000000`. H3 holds when all 5 are `complete_valid` and each has `consumer.counts.committed_never_processed >= 1`; it is contradicted when all 5 are `complete_valid` and at least one has it 0. `early_removal` is a finding when at least one counted repeat is below the lower bound. The verdict is `confirmed` when H1 and H2 both hold, `contradicted` when H1 or H2 is contradicted, and otherwise `no_verdict`.

G3 also requires these preconditions of the published sentences in section 4.4:
- in every repeat of both configurations, `consumer.kill_count` is 1 and `consumer.reassignment.unavailable_reason` is null;
- in every repeat, `consumer.offset_read.outcome` is `success`, every `offset_read.offsets` entry has `offset >= 0` and `error_code` 0, and `offset_read.requested_partitions` is 0 to 31;
- in every repeat, `consumer.counts.processed_again` and `processed_again_extra_rows` are 0, and `processed_once + committed_never_processed` equals the repeat's producer `counts.ok`, which is 600 (so every delivered message was processed once or never, which the plain words state);
- in every repeat, the subject's `pods.json` shows one pod whose container terminated with exit code 137, and the witness's one pod with exit code 0, both with the same `spec.nodeName`;
- in each archive manifest, exactly five `objects` entries have a non-null `gap_code`, each `configured_self_kill`, one per repeat 1 to 5, each with `kind` `native_monitoring_index` and a `source.name` containing `-consumer-subject/`.

### 4.3 `results/C01/2026-11-23.json`

Written as `json.dumps(document, indent=2, ensure_ascii=True) + "\n"`. Top-level keys exactly `publication` and `candidates`, in that order. `candidates` has exactly `C0` and `C1`, each JSON-equal to its pinned `candidate.json` member. `publication` has exactly these keys, in this order, with these values; `<...>` marks a value the generator copies from the named input.

```
item: "C01"
workload: "synthetic"
run: {workflow_run_id: 45124695288, attempt: 1, event: "workflow_dispatch", ref: "main",
      head_sha: "2d370de00059df30506154c1fd504d0f81d237bc", created_at: "2026-11-23T06:31:38Z",
      conclusion: "success", run_id: "c01-45124695288-1"}
expected_outcome: {path: "scenarios/C01/expected.yaml", sha256: "7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb"}
scenario_definition: {path: "scenarios/C01/scenario.yaml", sha256: "555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2"}
publication_decision: {comment_id: 7126257034, url: "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034"}
proposed_verdict: "contradicted"
rule: "scenarios/C01/expected.yaml verdict.contradicted_when: H1 is contradicted or H2 is contradicted."
kill_point: <expected.yaml assessment_rules.kill_point, verbatim, whitespace folded as YAML folds it>
window_policy: <expected.yaml assessment_rules.window_policy, verbatim, folded likewise>
hypotheses:
  H1_removal_follows_30000: {configuration: "C0", result: "holds", window_microseconds: [27000000, 45000000]}
  H2_removal_follows_45000: {configuration: "C1", result: "contradicted", window_microseconds: [42000000, 60000000]}
  H3_k57_latest_reset_skips_messages: {C0: "holds", C1: "holds", separate_line: true}
early_removal: {C0: {finding: false, repeats: []}, C1: {finding: false, repeats: []}, separate_line: true}
reassignment_microseconds: {mode: "recorded_only", separate_line: true,
                            C0: [31794219, 32994458, 35956669, 38562494, 37192516],
                            C1: [63539433, 68186056, 62547934, 68071749, 61342653]}
committed_never_processed: {C0: [66, 70, 77, 78, 75], C1: [128, 140, 125, 141, 125]}
archives: {C0: {archive_complete: true, gaps: [], manifest_sha256: "0d7cd201bc7e9d1cbb18db622b75e16c1c4084a38f08c91e5b710a46be281e94", configured_self_kill_repeats: [1, 2, 3, 4, 5]},
           C1: {archive_complete: true, gaps: [], manifest_sha256: "eb2d4f785a3e89ac5b2993e023177ffc934ffabcec49649b7a6d01de86ab8573", configured_self_kill_repeats: [1, 2, 3, 4, 5]}}
consumer_nodes: {C0: <list of 5 node names, one per repeat>, C1: <list of 5>}
consumer_exit_codes: {C0: {subject: [137, 137, 137, 137, 137], witness: [0, 0, 0, 0, 0]}, C1: {subject: [137, 137, 137, 137, 137], witness: [0, 0, 0, 0, 0]}}
broker_connection_counts: <per configuration, per repeat: {repeat, role, maximum, first_post_drain, lower_bound} copied from each repeat's broker_count>
environment: {namespace_sku: "Premium", processing_units: 1, region: "uksouth",
              node_size: "Standard_D4ds_v5", node_size_source: "inferred from infra/cluster/aks.tf lines 35, 44 and 51 at the base commit; not captured by the run"}
versions: <C0 candidate versions object; G2 requires the C1 object to be equal>
mutation_acceptance: {workflow_run_id: 45555996117, commit: "e3c8eb9462a6ecae139a651c5eef39812be07801",
                      measurement_complete: true, break_passed: true,
                      summary_sha256: "bb52a814b734277bd088a8b3745db6041c2bf5bc58ac57fb240588068667282d",
                      includes_run_head: "2d370de00059df30506154c1fd504d0f81d237bc"}
inputs: <one object per row of section 4.1: name, source, sha256>
```

The per-second ratio and the excess over the session timeout (section 4.4) are rendered in the report only; the JSON carries the measured values they are computed from. Absent never becomes zero: no key holds 0 or an empty list for an unavailable measurement. `gaps: []` is the receipt's own empty list, and `early_removal.repeats: []` is the computed empty set of section 4.2, not a substitute.

### 4.4 `results/C01/2026-11-23.md`

ASCII only. Rendering rules, which the generator applies and holds no rendered value as a constant:
- A takeover time is `microseconds / 1e6` formatted with Python `'.1f'`, in seconds. A range is `<min> to <max>` over the five repeats of a configuration. Here: C0 `31.8 to 38.6`, C1 `61.3 to 68.2`.
- A skip range is `<min> to <max>` of `committed_never_processed` over the five repeats. Here: C0 `66 to 78`, C1 `125 to 141`.
- A per-second ratio is `committed_never_processed / (microseconds / 1e6)` per repeat, formatted `'.2f'`; its range is the min and max of the formatted values per configuration and over all ten. Here: C0 `2.02 to 2.14`, C1 `2.00 to 2.07`, all `2.00 to 2.14`.
- An excess is `microseconds / 1e6 - session timeout in seconds` (30 for C0, 45 for C1), formatted `'.1f'`. Here: C0 `1.8 to 8.6`, C1 `16.3 to 23.2`.

Title line, exactly, with ranges rendered:

`# C01: a consumer killed before its first commit was replaced 31.8 to 38.6 s after the kill at session timeout 30000 ms and 61.3 to 68.2 s at 45000 ms; in every repeat some messages were marked done without being processed`

Then this three-sentence summary paragraph, exactly, with ranges rendered:

> In run 45124695288 on Event Hubs Premium, a consumer in a new two-member group was killed at its first message, before processing it, and the other member took over its partitions {C0 takeover} s after the kill with `session.timeout.ms` 30000 and {C1 takeover} s with 45000, in five repeats each. In every repeat, messages were marked done without ever being processed: {C0 skips} per repeat at 30000 and {C1 skips} at 45000. The proposed verdict is `contradicted` because takeover at 45000 exceeded the 15 s rebalance allowance, a policy value, in all five repeats; this does not show that either value is right or wrong, and the workload is synthetic.

Then the line `Proposed verdict: \`contradicted\`.`

Then the section `## In plain words`, before any section that uses verifier terms (decision 7126257034). Its paragraphs, exactly, with ranges rendered:

1. `**The production event.** A consumer in a brand-new group is killed before it has ever committed an offset, and the other member of the group takes over its partitions. Both consumers run this repository's baseline settings: \`auto.offset.reset\` \`latest\`, offsets committed automatically every 5000 ms, and each offset stored only after its message is processed. Messages keep arriving throughout. "Marked done" means the group's committed offset for a partition has moved past a message, so no member will read it again. The first setting (C0) uses \`session.timeout.ms\` 30000, Microsoft's recommendation for Event Hubs; the second (C1) gives only the killed consumer 45000, the librdkafka default, and the member that takes over keeps 30000.`
2. `**What happens, step by step.** The run measured the takeover time and the counts below; the steps that connect them come from the librdkafka documentation and source listed under Sources, and are an inference, not a measurement. The killed member sends no more heartbeats, so its partitions go unread until the broker's session timeout removes it from the group. Messages keep arriving on those partitions. The member that takes over finds no committed offset for them, because the killed member never committed one, so \`auto.offset.reset\` \`latest\` starts it at the end of each partition. Everything that arrived between the crash and the takeover is skipped. Once the survivor commits newer offsets, the group treats the skipped messages as done. No error is raised: with \`latest\` librdkafka resets on its own, and only the setting \`error\` raises an error; the consumers' logs are empty, so this was not observed in the run. The killed member committed nothing; the survivor's commits are what pass the skipped messages.`
3. `**What was measured.** With session timeout 30000 ms the other member held the killed member's partitions {C0 takeover} s after the kill, and {C0 skips} messages per repeat were marked done without being processed. With 45000 ms on the killed member, the takeover came {C1 takeover} s after the kill, and {C1 skips} messages per repeat were skipped. After each repeat the verifier read the group's committed offsets from Event Hubs and found them at the run's end line on every partition that received messages. Each repeat sent 600 synthetic messages over 150 s, and all five repeats of each setting were valid.`
4. `**What the verdict means and does not mean.** The prediction for each setting allowed 15 s of rebalance time beyond the session timeout; that 15 s is a policy value chosen before the run, not a measurement. At 30000 ms every takeover fell inside its window, so that prediction held. At 45000 ms every takeover was later than 60 s, the end of its window, so that prediction is contradicted, and with it the verdict. The run does not show that 30000 ms is the right value or that 45000 ms is wrong. It measured failover time only. It did not test the cost of a shorter timeout: a live member that stalls being removed from the group by mistake. This result describes failover time and recommends no value.`
5. `**Time beyond the session timeout.** The takeover came {C0 excess} s after the session timeout at 30000 ms, and {C1 excess} s after it at 45000 ms. The run does not show why the extra time is larger at 45000 ms.`
6. `**More takeover time, more skipped messages.** In every repeat, the skipped messages divided by the takeover time in seconds came to {all ratio} ({C0 ratio} at 30000 ms, {C1 ratio} at 45000 ms), so about 2 messages were skipped for each second of takeover. This is an inference from the numbers, not a measured rate.`
7. `**A shorter timeout shrinks the loss but does not remove it.** Messages were skipped at both tested values, and fewer at 30000 ms. That every session timeout leaves some loss in this case is an inference: the survivor cannot take over before the broker removes the killed member, and messages that arrive until then are skipped.`
8. `**Remedies, not tested here.** With \`auto.offset.reset\` \`earliest\`, librdkafka resets a partition with no committed offset "to the smallest offset"; with \`error\`, it triggers the error \`ERR__AUTO_OFFSET_RESET\`, which the application receives when it consumes messages (librdkafka v2.16.0 \`CONFIGURATION.md\` line 206). Positioning the consumer after that error is the application's choice, an inference not tested here. Neither was tested here. Inference, not tested: with \`earliest\` the survivor reads every message still retained on those partitions again, so processing must tolerate repeats. Storing offsets only after processing, which both consumers did here, does not prevent this case, because nothing was ever committed.`
9. `**When this happens in production.** A new group's first deployment crashes before its first commit. A group's committed offsets have expired: Microsoft lists "Consumer offsets expired beyond Event Hubs retention period" as a likely cause of an unexpected offset reset, and gives no duration. A partition was never committed to. A group that has been committing normally resumes from its last commit instead; that case is tested under C10, offset storage and crash outcomes ([scenario definition](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/scenarios/C10/scenario.yaml)).`

Then these sections, in this order, with these headings:

1. `## What was tested`. This paragraph, exactly: "Each repeat ran two consumers in one group: a subject that was killed and a witness that ran the baseline consumer and received the subject's partitions. The load generator offered 4 synthetic messages per second for 150 s, 600 per repeat, in five repeats per configuration. C0 runs the baseline, with `session.timeout.ms` 30000. C1 sets `session.timeout.ms` to 45000 on the subject and changes nothing else; the witness runs the baseline in both configurations. K-57, a consumer killed before its first commit with `auto.offset.reset` at its baseline, is tested here and not under C10, because C10's two phases compare offset-store modes." Then the kill point as a quoted paragraph, verbatim from `assessment_rules.kill_point` with YAML folding applied, preceded by the line `Kill point (from the expected outcome):`. Then the window policy the same way, preceded by `Window policy (from the expected outcome):`.
2. `## Results by repeat`. One table per configuration, named by a bold line `C0, session.timeout.ms 30000` and `C1, session.timeout.ms 45000 on the subject`, with columns, in order: `Repeat`, `Status`, `Consumer status`, `Takeover (s)`, `Committed, never processed`, `Processed once`, `Skipped per second of takeover`; values from each repeat's `status`, `consumer.status`, `consumer.reassignment.microseconds` (rendered `'.1f'` seconds), `consumer.counts` and the per-repeat ratio. Then the sentence, exactly: "\"Committed, never processed\" is the verifier's name for a message marked done that nobody processed. \"Takeover\" is the verifier's `reassignment`: the time from the subject's last record row, its kill intent, to the witness's first assignment that includes every partition the subject held. \"Skipped per second of takeover\" is computed here, not measured."
3. `## Hypotheses`. One line each, exactly, with ranges rendered:
   - `- H1, removal follows 30000 (C0): holds. All five takeovers, {C0 takeover} s, are inside the window from 27 s up to but not including 45 s.`
   - `- H2, removal follows 45000 (C1): contradicted. All five takeovers, {C1 takeover} s, are at or after 60 s, the end of the window from 42 s up to but not including 60 s.`
   - `- H3, K-57, a latest reset skips messages, published on its own line and not part of the verdict: holds in C0 and C1. Every repeat had at least one message committed but never processed ({C0 skips} in C0, {C1 skips} in C1).`
   - `- Early removal, a finding on its own line: none. No takeover in either configuration came before the session timeout minus the heartbeat interval (27 s in C0, 42 s in C1).`
   - `- Takeover times, recorded only and published on their own line: as in Results by repeat.`
4. `## Commit evidence`. This paragraph, exactly: "After each repeat the verifier read the group's committed offsets from Event Hubs without joining the group (`offset_read`, outcome `success` in all ten repeats). A repeat counts only when every committed offset it read matches an offset the members' own commit records report (otherwise it is invalid), and only when, on every partition that received messages during the run, the committed offset reached the run's end line (otherwise it is incomplete). So \"committed\" here is observed from the broker, not only reported by the consumers. Every delivered message was processed once or never: none was processed twice." Source: `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 63 to 74 and 90 to 102 at the base commit, and `src/Parchwell.Verifier/VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs` line 11.
5. `## Producer side and broker connections`. A table with columns `Configuration`, `Repeat`, `ok`, `loss`, `phantom`, `reported_failure`, `duplicate`, `reorder`, `Broker connections (maximum)`, `Broker connections (first after drain)`, from each repeat's `counts` and `broker_count`, then the sentence "Broker connection counts are the load generator producer's only, and are lower bounds, as each repeat's `broker_count.lower_bound` records."
6. `## Run validity and evidence`. Both candidates `complete_valid` with no reasons; both phase-status files report `safety_stop` false, every `consumer-subject` Job `failed` (the subject ends itself with SIGKILL, as the kill point requires) and every other Job `complete` or, for `consumer-readiness`, `ready`; both archive receipts `archive_complete` true with no gaps, each with its manifest SHA-256. Then this sentence, exactly: "The subject has no native-monitoring evidence; the archive records its missing index as `configured_self_kill` in each of the ten repeats." Per configuration, a link to each of the three artifacts (`https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45124695288/artifacts/<id>`) and, for each repeat, the member paths inside the pods artifact of the pod log and `pods.json` of each of its five Jobs (`consumer-subject`, `consumer-witness`, `start-line`, `loadgen` and `verify`), then the member paths of the phase's `judge` pod log and `pods.json`. The node name of each pair, and the sentence "Both consumers of every repeat ran on the same `workload` node; every subject exited with code 137 (SIGKILL) and every witness exited 0." The line `Artifacts expire 2027-02-21T06:31:39Z.`
7. `## Environment and versions`. Namespace SKU Premium, 1 processing unit and region uksouth, each cited by its log line number (`probe.log` lines 1605, 1590 and 1599; `full-C3.log` lines 1411, 1396 and 1405), with no log text quoted, because those lines carry ANSI colour escapes; node size `Standard_D4ds_v5`, stated as inferred from `infra/cluster/aks.tf` lines 35, 44 and 51 and not captured; the `versions` object; `source_commit`; each phase's `worker_started_utc` and `judge_completed_utc`.
8. `## Mutation acceptance`. The run ID, its `commit`, `measurement_complete` true and `break_passed` true, the summary SHA-256, and the sentence "That commit is on `main` and includes `2d370de00059df30506154c1fd504d0f81d237bc`, the run's head (docs/agents/factory.md \"Evidence pack\")."
9. `## Not tested`. Exactly these lines:
   - `- \`auto.offset.reset\` \`earliest\` or \`error\` in this case: not tested.`
   - `- The cost of a shorter session timeout, a live member that stalls being removed by mistake: not tested.`
   - `- Session timeouts other than 30000 and 45000 ms: not tested.`
10. `## Sources`. Exactly these bullets, in this order (no placeholder):
    - `- Run [45124695288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45124695288), its artifacts and job logs, as listed above and in the JSON \`inputs\`.`
    - `- Scenario definition and expected outcome as approved: [scenarios/C01/scenario.yaml](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2d370de00059df30506154c1fd504d0f81d237bc/scenarios/C01/scenario.yaml) (SHA-256 \`555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2\`) and [scenarios/C01/expected.yaml](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/2d370de00059df30506154c1fd504d0f81d237bc/scenarios/C01/expected.yaml) (SHA-256 \`7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb\`), both at \`2d370de00059df30506154c1fd504d0f81d237bc\`. The plain-words points of the publication decision bind the published text in addition to the expected outcome\'s \`text_must_state\`.`
    - `- Publication decision: [7126257034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034) (the plain-words points).`
    - `- librdkafka v2.16.0 [\`CONFIGURATION.md\`](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) lines 131, 132, 138, 139, 140 and 206, and [\`src/rdkafka_assignment.c\`](https://github.com/confluentinc/librdkafka/blob/v2.16.0/src/rdkafka_assignment.c) lines 214 to 220 and 631 to 635.`
    - `- Microsoft, [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), "librdkafka configuration properties", consumer-only table (\`session.timeout.ms\` default 45000, recommended 30000), and "Troubleshooting common issues", row "Unexpected offset reset".`
    - `- This repository at \`3e7163dc641e724437025d917032fd40419e6c4d\`: [\`BaselineSettingsCatalog.cs\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs) lines 149, 151, 179, 181, 183 and 185; [\`ScenarioConsumerProcessingPlanSource.cs\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs) line 11; [\`ConsumerJudgement.cs\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs) lines 63 to 74, 90 to 102 and 169 to 175; [\`ConsumerCommittedOffsetReader.cs\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/src/Parchwell.Verifier/VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs) line 11; [\`c01-consumer.yaml\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/scenarios/profiles/c01-consumer.yaml) line 20; [\`aks.tf\`](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/infra/cluster/aks.tf) lines 35, 44 and 51; [docs/blueprint.md](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/3e7163dc641e724437025d917032fd40419e6c4d/docs/blueprint.md) lines 756 (ADR-022, store after the processed row) and 1276 (K-57 under C01).`
    - `- C5 mutation run [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117) and its \`summary.json\`, as in Mutation acceptance.`
    - `- Generator and receipt: [make_results.py](../../docs/operations/evidence/issue-513-c01-verdict/make_results.py) and [receipt.md](../../docs/operations/evidence/issue-513-c01-verdict/receipt.md).`

The report names no earlier C01 run. A published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").

The eight points of decision 7126257034 map to the report as follows; G9 checks each: point 1 to paragraph 1; point 2 to paragraph 2; point 3 to paragraph 3, the summary and "Results by repeat"; point 4 to paragraph 4 and the H1 and H2 lines; point 5 to paragraph 6; point 6 to paragraph 7; point 7 to paragraph 8 and "Not tested"; point 8 to paragraph 9. Paragraph 5 carries the excess figures of grilling question Q6 (section 12). The five `text_must_state` items of `scenarios/C01/expected.yaml` map as follows: the kill point and window policy to "What was tested"; the synthetic workload and its 150 s load to "What was tested" and paragraph 3; K-57 tested here to "What was tested"; early-removal findings to the early-removal line (none); `configured_self_kill` to "Run validity and evidence".

### 4.5 `README.md` line 30

Replace line 30 with exactly (ranges as rendered from the data):

`| C01 | Session timeout configuration | A consumer in a new group was killed before its first commit, and the other member took over its partitions 31.8 to 38.6 s later with \`session.timeout.ms\` 30000 (Microsoft's recommendation) and 61.3 to 68.2 s later with 45000 (the librdkafka default). In every repeat, messages were marked done without being processed: 66 to 78 per repeat at 30000 and 125 to 141 at 45000, about 2 for each second of takeover (an inference from the numbers). Inferred from the librdkafka documentation: the member that takes over finds no committed offset, and \`auto.offset.reset\` \`latest\` starts it at the end of each partition without an error. "Contradicted" is against a 15 s rebalance allowance, a policy value; the run measured failover time only and recommends no session timeout. Synthetic workload. | contradicted | [Report](results/C01/2026-11-23.md) |`

The generator prints this row; acceptance step 2 compares it with line 30 byte for byte.

### 4.6 External behaviour relied on

Verified on 2026-11-24 by this session at each cited line (plan rule 1). Tools: `curl` from `raw.githubusercontent.com/confluentinc/librdkafka/v2.16.0/`, hashed with `sha256sum`; the Microsoft Learn MCP `microsoft_docs_fetch` tool for the Microsoft page.

Files: librdkafka v2.16.0 `CONFIGURATION.md` (`b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`) and `src/rdkafka_assignment.c` (`315e5b2a8ca68c974c9048ac8647aee6202d2e84a3c5215b05871a2d49fecb6f`), the same hashes the C10 verdict plan records for those files.

1. A member that sends no heartbeat within the session timeout is removed. Confirmed. `CONFIGURATION.md` line 131: "If no hearts are received by the broker for a group member within the session timeout, the broker will remove the consumer from the group and trigger a rebalance"; default 45000. Line 132: `heartbeat.interval.ms` default 3000. The baseline pins 30000 and 3000 (`BaselineSettingsCatalog.cs` lines 149 and 151). The baseline's classic group protocol is pinned at line 145, under which `session.timeout.ms` applies (line 131 says it "is not supported for `group.protocol=consumer`").
2. With no committed offset, `auto.offset.reset` decides the start. Confirmed. `src/rdkafka_assignment.c` lines 214 to 220: "If rktpar->offset is RD_KAFKA_OFFSET_INVALID it means there was no committed offset for this partition ... the partition fetcher will employ auto.offset.reset to know what to do"; lines 631 to 635: otherwise "use the last committed offset for partition". `CONFIGURATION.md` line 206: `auto.offset.reset` "Action to take when there is no initial offset in offset store", with `latest` "automatically reset the offset to the largest offset" and `error` "trigger an error (ERR__AUTO_OFFSET_RESET)". The baseline sets `latest` for the consumer (`BaselineSettingsCatalog.cs` line 185).
3. The killed member committed nothing. Inference from sources, as `scenarios/C01/expected.yaml` `assessment_rules.kill_point` states it ("before processing it, so it has committed nothing (ADR-022)"): the baseline stores an offset only after processing (`enable.auto.offset.store` `false`, `BaselineSettingsCatalog.cs` line 183; ADR-022 has the member append its processed row before it stores the offset, docs/blueprint.md line 756), and the offset store "is an in-memory store of the next offset to (auto-)commit" (`CONFIGURATION.md` line 140), so a member killed before processing its first message has nothing to commit. Automatic commits every 5000 ms: `CONFIGURATION.md` lines 138 and 139; baseline lines 179 and 181.
4. The witness runs the baseline in both configurations: `ScenarioConsumerProcessingPlanSource.cs` line 11 at the base commit ("the witness gets the same window and delay and runs the baseline") and `scenarios/C01/expected.yaml` `assessment_rules.kill_point` ("The witness runs the baseline.").
5. Microsoft recommends `session.timeout.ms` 30000 for librdkafka clients, against the default 45000, within 6000 to 300000 ("librdkafka configuration properties", consumer-only table, fetched 2026-11-24). Its troubleshooting table lists, for "Unexpected offset reset", the likely cause "Consumer offsets expired beyond Event Hubs retention period". The page gives no offset-retention duration, so the report states none.
6. Measured, not inferred: the takeover time, the counts, and that the group's committed offsets reached the end line in every repeat (`ConsumerJudgement.cs` lines 63 to 74 make that a validity condition). The count of messages committed but never processed is a delivered message in the run window with no `processed` row from either member (`ConsumerJudgement.cs` lines 90 to 102).

The report and README state no other Kafka, librdkafka or Event Hubs behaviour.

### 4.7 `docs/operations/evidence/issue-513-c01-verdict/receipt.md`

Records: the six input artifacts, two logs and the mutation summary with hashes (section 4.1); the generator commands and their outputs (section 9); and the mutation-relevance finding of section 4.9, as provenance. The receipt names no other C01 run; it states no failure cause of any run.

### 4.8 Publication record

No state file is written (section 2).

### 4.9 Mutation acceptance (prerequisite)

docs/agents/factory.md "Evidence pack" requires a passing `verifier-c5-mutation.yml` run on `main` whose `summary.json` reports `measurement_complete: true` and `break_passed: true` and names a commit whose history includes the merge commit of each mutation-input change; "A missing, failed or incomplete latest applicable run blocks evidence-pack publication."

Evidence, 2026-11-25, from the runs list of `verifier-c5-mutation.yml`, the summary artifact and `git`:
- Mutation inputs changed after the last passing run before the C01 run: `git diff --stat 9c7fd04e 2d370de0` lists `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs`, `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`, `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py` and `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`. So run 45028998990 (on `9c7fd04eaf015fa0896fb89637fa99acec6ce051`) does not cover the run head.
- Run [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117): `event` `workflow_dispatch`, `head_branch` `main`, `head_sha` `e3c8eb9462a6ecae139a651c5eef39812be07801`, `conclusion` `success`, created 2026-11-25T05:47:26Z. Its `summary.json` (section 4.1) has `run_id` `"45555996117"`, `run_attempt` `"1"`, `commit` `e3c8eb9462a6ecae139a651c5eef39812be07801`, `measurement_complete` true and `break_passed` true. `git merge-base --is-ancestor 2d370de0 e3c8eb94` exits 0, so it covers the run head. It is the newest completed `verifier-c5-mutation.yml` run on 2026-11-25; the two before it, 45469622107 and 45459740602, failed on `4ea8fae3`, an ancestor of `e3c8eb9462a6ecae139a651c5eef39812be07801`.

The implementer requires (G10, acceptance step 6) that this run still meets these conditions, and that it is still the newest completed `verifier-c5-mutation.yml` run on `main` when the implementer checks. If a later run on `main` has failed, the implementer stops under section 11.

## 5. Requirements

- R1. WHEN the generator starts, THEN it checks every hash of section 4.1, including the mutation summary and `scenarios/C01/expected.yaml`, and fails `G1` on any difference.
- R2. WHEN both candidates are read, THEN each must have `scenario_id` `C01`, `configuration_id` and `phase` equal to its configuration, `run_id` `c01-45124695288-1`, `source_commit` `2d370de00059df30506154c1fd504d0f81d237bc`, `synthetic` true, root `status` and `consumer.status` `complete_valid` with empty `reasons`, and exactly repeats 1 to 5, each with `status` and `consumer.status` `complete_valid` and empty `reasons`; and the two `versions` objects must be equal. Otherwise `G2`.
- R3. WHEN the rules of section 4.2 are recomputed, THEN each result, each recorded value and each precondition must equal section 4.2. G3 compares the rules in the row order of the section 4.2 table, then the preconditions in their listed order, and reports the first difference as `FAIL G3: <rule> <computed>, expected <pinned>` for a rule (for example `FAIL G3: configurations.C0.H1_removal_follows_30000 contradicted, expected holds`) or `FAIL G3: precondition <n>` for the n-th precondition bullet. Otherwise `G3`, and the implementer stops (a finding, not a fix).
- R4. WHEN root counts are compared, THEN each root `counts` key equals the sum over repeats of that key, and each root `consumer.counts` key equals the sum of the repeats' `consumer.counts`. Otherwise `G4`.
- R5. WHEN the archive receipts are read, THEN each has `archive_complete` true, `gaps` `[]`, `kind` `run_evidence_archive_receipt`, `identity` with `github_run_id` `"45124695288"`, `github_run_attempt` 1, `phase` equal to its configuration, `run_id` `c01-45124695288-1` and `source_commit` `2d370de00059df30506154c1fd504d0f81d237bc`, and `manifest.sha256` equal to the manifest member hash. Otherwise `G5`.
- R6. WHEN the phase-status members are read, THEN each has `phase` equal to its configuration, `run_id` `c01-45124695288-1`, `safety_stop` false, `candidate.status` `complete_valid`, `candidate.repeats` `[[1, "complete_valid"], ..., [5, "complete_valid"]]`, and every `modes` entry `outcome` `failed` for mode `consumer-subject`, `ready` for mode `consumer-readiness` and `complete` for every other mode, with exactly 31 entries (6 per repeat and the judge). Otherwise `G6`.
- R7. WHEN run with `--check`, THEN the rendered JSON and report equal the committed files byte for byte, and the printed README row equals `README.md` line 30. Otherwise `G7`.
- R8. WHEN the report is rendered, THEN it contains only ASCII, and so does the README row. Otherwise `G8`.
- R9. WHEN the report is rendered, THEN it contains, verbatim, the title, the summary, the `## In plain words` heading and its nine paragraphs, the kill-point and window-policy quotations, the five hypothesis lines, the `configured_self_kill` sentence and the three "Not tested" lines; the `## In plain words` heading comes before `## What was tested`; no text between the title and `## In plain words` contains any of `committed_never_processed`, `reassignment`, `offset_read`, `complete_valid` or `H1`; paragraphs 2, 6 and 7 each contain the word `inference`; and the README row contains `an inference from the numbers`, `Inferred from the librdkafka documentation` and `recommends no session timeout`. Otherwise `G9`.
- R10. WHEN the mutation summary is read, THEN `measurement_complete` is true, `break_passed` is true, `run_id` is the JSON string `"45555996117"`, and `commit` is `e3c8eb9462a6ecae139a651c5eef39812be07801`. Otherwise `G10`.
- R11. The checks run in the order G1, G2, G3, G4, G5, G6, G10, then rendering, G8, G9, and G7 only with `--check`. The first failure prints `FAIL G<n>: <reason>` and exits 1.
- R12. `--self-test` runs the real inputs, printing `PASS real_inputs`, then each case of section 7 on an in-memory copy, the three R1 cases before G1 and every other case after G1, printing `PASS <case>` when the case fails at its expected check and, for G3, with the expected reason text up to and including the rule name or precondition number, and exits 0 only if every case matched. G7 runs in self-test only for case `report_edit`.

## 6. Invariants and safety rules

- No edit to an expected outcome, a verdict rule, a verifier output or a candidate (AGENTS.md GOVERNANCE "Verdict integrity"); `scenarios/C01/expected.yaml` and the test that pins its hash stay as they are on `main` (decision 7168528766 point 1). A recomputation that disagrees with a candidate or with section 4.2 is a finding: stop under section 11.
- No dispatch by the implementer, no Azure access, no secret, tenant ID or subscription ID (hard safety rules 2, 3 and 7). The generator copies no log text; section 4.4 cites log lines by number only.
- No number that was not measured or computed by script (hard safety rule 11; docs/agents/pull-requests.md rule 4). Every causal step the run did not measure is labelled an inference with its source (docs/agents/pull-requests.md rule 2): paragraph 2 as a whole, paragraphs 6, 7 and 8, and the README row.
- The report recommends no `session.timeout.ms` value (decision 7126257034 point 4).
- The published results show run 45124695288 only.
- The pull request is labelled `verdict`, touches protected paths and is merged by Hari only.

## 7. Test map

The tests are the generator's `--self-test` cases. Each changes one value in memory: the three R1 cases before G1, every other case after it. No existing test changes, so plan rule 2's standing fixture rule has no case; if one arose, the implementer follows it exactly: (a) change the fixture so it passes the new checks, keeping every assertion and boundary, or (b) where invalid input is the test's purpose, keep the input and change expected values to exactly what the checks produce; never remove a case, loosen an assertion or change a check, and list each change with the check that forces it in the PR implementation notes.

| Requirement | Case | Change | Expected |
|---|---|---|---|
| R1 | `hash_c0_candidate` | one byte of the C0 `candidate.json` member, before G1 | `FAIL G1` |
| R1 | `expected_yaml_edit` | one byte of `expected.yaml`, before G1 | `FAIL G1` |
| R1 | `mutation_hash` | one byte of the `summary.json` member, before G1 | `FAIL G1` |
| R2 | `c1_repeat_status` | C1 repeat 3 `consumer.status` to `incomplete` | `FAIL G2` |
| R2 | `versions_differ` | C1 `versions.librdkafka` to `2.15.0` | `FAIL G2` |
| R3 | `h1_at_upper_bound` | C0 repeat 2 `reassignment.microseconds` to 45000000 | `FAIL G3: configurations.C0.H1_removal_follows_30000 contradicted, expected holds` |
| R3 | `h1_below_upper_bound` | C0 repeat 2 `reassignment.microseconds` to 44999999 | `FAIL G3: configurations.C0.reassignment ...` (H1 still holds; only the recorded values differ) |
| R3 | `h1_at_lower_bound` | C0 repeat 4 `reassignment.microseconds` to 27000000 | `FAIL G3: configurations.C0.reassignment ...` (H1 still holds; no early-removal finding at the bound) |
| R3 | `h1_early` | C0 repeat 4 `reassignment.microseconds` to 26999999 | `FAIL G3: configurations.C0.H1_removal_follows_30000 inconclusive, expected holds` |
| R3 | `h1_null` | C0 repeat 1 `reassignment.microseconds` to null and `unavailable_reason` to `different_nodes` | `FAIL G3: configurations.C0.H1_removal_follows_30000 inconclusive, expected holds` (repeat not counted) |
| R3 | `h2_in_window` | all five C1 `reassignment.microseconds` to 59999999 | `FAIL G3: configurations.C1.H2_removal_follows_45000 holds, expected contradicted` |
| R3 | `h2_one_at_bound` | C1 values to 59999999 except repeat 5 to 60000000 | `FAIL G3: configurations.C1.reassignment ...` (H2 still contradicted at 60000000) |
| R3 | `h2_at_lower_bound` | C1 repeat 3 `reassignment.microseconds` to 42000000 | `FAIL G3: configurations.C1.reassignment ...` (no early-removal finding at the bound) |
| R3 | `h2_early` | C1 repeat 3 `reassignment.microseconds` to 41999999 | `FAIL G3: configurations.C1.early_removal ...` (H2 still contradicted; the finding differs) |
| R3 | `h3_zero` | C1 repeat 2 `committed_never_processed` to 0 and `processed_once` to 600 | `FAIL G3: configurations.C1.H3_k57_latest_reset_skips_messages contradicted, expected holds` |
| R3 | `processed_again_one` | C0 repeat 5 `processed_again` and `processed_again_extra_rows` to 1 | `FAIL G3: precondition 3` |
| R3 | `count_sum_short` | C0 repeat 3 `processed_once` 523 to 522 | `FAIL G3: precondition 3` |
| R3 | `kill_count` | C1 repeat 4 `consumer.kill_count` to 2 | `FAIL G3: precondition 1` |
| R3 | `offset_read_failed` | C0 repeat 1 `consumer.offset_read.outcome` to `kafka_exception` | `FAIL G3: precondition 2` |
| R3 | `offset_negative` | C1 repeat 1 partition 9 `offset` to -1001 | `FAIL G3: precondition 2` |
| R3 | `subject_exit_zero` | C0 repeat 5 subject container exit code to 0 | `FAIL G3: precondition 4` |
| R3 | `node_split` | C1 repeat 2 witness `spec.nodeName` to another name | `FAIL G3: precondition 4` |
| R3 | `self_kill_missing` | C1 manifest: the repeat-3 `configured_self_kill` entry's `gap_code` to null | `FAIL G3: precondition 5` |
| R4 | `root_consumer_sum` | C0 root `consumer.counts.committed_never_processed` 366 to 365 | `FAIL G4` |
| R4 | `root_producer_sum` | C1 root `counts.ok` 3000 to 2999 | `FAIL G4` |
| R5 | `archive_gap` | C0 receipt `gaps` to `["x"]` | `FAIL G5` |
| R5 | `archive_phase` | C1 receipt `identity.phase` to `C0` | `FAIL G5` |
| R6 | `phase_safety_stop` | C0 phase-status `safety_stop` to true | `FAIL G6` |
| R6 | `subject_mode_complete` | C1 phase-status repeat-1 `consumer-subject` `outcome` to `complete` | `FAIL G6` |
| R10 | `mutation_incomplete` | summary `measurement_complete` to false | `FAIL G10` |
| R10 | `mutation_run_id` | summary `run_id` to `"45555996118"` | `FAIL G10` |
| R8 | `non_ascii` | an en dash in the rendered report | `FAIL G8` |
| R9 | `missing_plain_words` | delete paragraph 4 of `## In plain words` from the rendered report | `FAIL G9` |
| R9 | `verifier_term_early` | insert `reassignment` into the summary paragraph of the rendered report | `FAIL G9` |
| R9 | `inference_label_dropped` | delete the word `inference` from paragraph 6 of the rendered report | `FAIL G9` |
| R7 | `report_edit` | change one table cell of the committed report, with `--check` | `FAIL G7` |

In this table `...` stands for the rendered computed and pinned values, which the self-test does not compare; it compares the text up to the rule name. Boundaries: H1 reads 45000000 as contradicted (`h1_at_upper_bound`) and 44999999 as inside the window (`h1_below_upper_bound`, which then fails only the recorded-value comparison); the lower bound reads 27000000 as inside (`h1_at_lower_bound`) and 26999999 as early (`h1_early`), and 42000000 as inside (`h2_at_lower_bound`) and 41999999 as early (`h2_early`); H2 reads 59999999 as inside (`h2_in_window`) and 60000000 as contradicted (`h2_one_at_bound`); H3 reads `committed_never_processed` 1 (pass, real inputs) and 0 (`h3_zero`); the count precondition reads the sum 600 (pass) and 599 (`count_sum_short`). 36 cases.

## 8. Deployment assumptions

None. The pull request deploys nothing and runs no code against a container, image, chart or runtime. All live evidence is recorded output of the completed run, pinned by hash in section 4.1; no assumption about an open or fresh connection, or an idle or loaded state, is relied on. The mutation summary is recorded output of the completed run 45555996117, pinned by hash in section 4.1.

## 9. Acceptance

Run from the repository root, with the inputs of section 4.1 in a scratch directory `$IN` outside the repository.

1. `sha256sum` of each ZIP, member and log equals section 4.1, and the mutation ZIP hash equals its artifact API `digest`. Evidence: the output in the receipt.
2. `python3 -I docs/operations/evidence/issue-513-c01-verdict/make_results.py --inputs "$IN"` writes the two results files and prints the README row. Evidence: exit 0.
3. The same command with `--check`. Evidence: exit 0 and `PASS check`.
4. The same command with `--self-test`. Evidence: `PASS real_inputs` and one `PASS <case>` line for each of the 36 cases of section 7, exit 0.
5. `python3 -m json.tool results/C01/2026-11-23.json > /dev/null`. Evidence: exit 0.
6. `git merge-base --is-ancestor 2d370de00059df30506154c1fd504d0f81d237bc e3c8eb9462a6ecae139a651c5eef39812be07801` and `git merge-base --is-ancestor e3c8eb9462a6ecae139a651c5eef39812be07801 origin/main` both exit 0; `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/verifier-c5-mutation.yml/runs?per_page=5` shows run 45555996117 as the newest completed run on `main`, with `conclusion` `success`. Evidence: the outputs in the receipt.
7. `npm run check:docs`. Evidence: no warning on a changed file, or each explained in the pull request.
8. `LC_ALL=C grep -rnP '[^\x00-\x7F]' results/C01/ docs/operations/evidence/issue-513-c01-verdict/` and the same on line 30 of `README.md`. Evidence: no output.
9. `git diff --name-only origin/main...HEAD` lists exactly the five paths of section 3, and `git diff origin/main...HEAD -- README.md` changes only line 30. On pull request #596, which revision 3 built, this means its commits that edit `scenarios/C01/expected.yaml` and `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs` are reverted so that neither file differs from `origin/main` (decision 7168528766 point 3).
10. Cold review under AGENTS.md "Before opening a pull request" rule 8 (Claude Code implements, so the Claude review gate for Codex pull requests does not apply); Codex review and Codex security review requested as hard safety rule 10 allows; the pull request labelled `verdict`.

`scripts/verify-dotnet.sh` is not required: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates", last paragraph). Revision 3's partial runs on pull request #596 covered a test change this revision removes.

## 10. Sources

- Run [45124695288](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45124695288), its artifacts and job logs, read 2026-11-24 through the GitHub REST API.
- Mutation runs [45028998990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45028998990) (`success`, head `9c7fd04eaf015fa0896fb89637fa99acec6ce051`, not covering the run head) and [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117) (`success`, head `e3c8eb9462a6ecae139a651c5eef39812be07801`) with its `summary.json`, read 2026-11-25.
- librdkafka v2.16.0 `CONFIGURATION.md` and `src/rdkafka_assignment.c`, at the hashes and lines of section 4.6.
- Microsoft, [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), fetched 2026-11-24.
- `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` lines 145, 149, 151, 179, 181, 183 and 185, and `src/Parchwell.Enrichment/ConsumerProcessing/ScenarioConsumerProcessingPlanSource.cs` line 11, at the base commit.
- `scenarios/C01/expected.yaml`, `scenarios/C01/scenario.yaml`, `scenarios/profiles/c01-consumer.yaml` (line 20: 4 attempts per second for 150 s), docs/blueprint.md ADR-022 and section 12 line 1276, and docs/operations/verifier-contract.md lines 128 and 136, at the base commit.
- `src/Parchwell.Verifier/VerifyRun/Consumer/Judgement/ConsumerJudgement.cs` lines 63 to 74 (commit evidence), 90 to 102 (counts) and 169 to 175 (reassignment), and `src/Parchwell.Verifier/VerifyRun/Consumer/Offsets/ConsumerCommittedOffsetReader.cs` line 11, at the base commit.
- `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit (`vm_size = "Standard_D4ds_v5"` for all three pools).
- Decisions [7126257034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034) and [7168528766](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7168528766).

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when a pinned hash in section 4.1 differs from a fresh download; when G2 to G6 or G10 fails on the real inputs (a finding under "Verdict integrity", not a fix); when no mutation run meets section 4.9; when a predecessor pull request of #510 is not merged; or when a `verifier-c5-mutation.yml` run on `main` newer than 45555996117 has completed without success.

## 12. Choices made under "Act or ask", and grilling answers

These are ordinary reversible choices within authority (docs/agents/factory.md "Act or ask") and the grilling answers adopted under standing decision 6895535291 point 2; Hari may change any of them when he reviews the verdict pull request.

1. Grilling Q1, shape: the C03 and C10 verdict shape, five files, date 2026-11-23. Adopted.
2. Grilling Q2, how "contradicted" reads: the production event first, then measured times and skip counts, then the verdict and what it does not mean. Adopted; section 4.4 order and R9.
3. Grilling Q3, remedies: name `earliest` and `error`, cite `CONFIGURATION.md` line 206, mark them not tested, label the repeat-tolerance point an inference. Adopted; paragraph 8 and "Not tested".
4. Grilling Q4, expired offsets: cite Microsoft's troubleshooting row and state no duration. Adopted; paragraph 9.
5. Grilling Q5, about 2 per second: computed per repeat by script, rendered as a range, labelled an inference. Adopted; paragraph 6 and the table column.
6. Grilling Q6, time beyond the timeout: the computed excess for each setting, with no cause claimed. Adopted; paragraph 5.
7. Grilling Q7, the committed-offset case: described in words, linked to the C10 scenario definition on `main`, not to a C10 result, so this pull request does not depend on C10's merge order. Adopted; paragraph 9.
8. Grilling Q8, a shorter timeout: measured at both values; the general claim labelled an inference. Adopted; paragraph 7.
9. Grilling Q9, no error raised: grounded in `CONFIGURATION.md` line 206. The consumer pod logs in both pods artifacts are empty (0 bytes for every `consumer-subject` and `consumer-witness` log), so the report says the absence of an error was not observed. Adopted; paragraph 2.
10. "Marked done" is used for a committed offset that has moved past a message, defined once in paragraph 1, as in the C10 verdict plan; the verifier name `committed_never_processed` appears only after the plain words.
11. The offered load of 4 messages per second (`c01-consumer.yaml` line 20) is stated in "What was tested" only. The report does not relate it to the 2 per second ratio, because the run does not record how many partitions the subject held.

## 13. Deferred work

None. Revision 3's one deferred item, mutation acceptance covering the run head, is complete: run 45555996117 (section 4.9).

## 14. Plan-check answers

(a) No step's effort scales with a number unknown at planning time. The inputs are fixed: two candidates, two archive receipts and manifests, two pods artifacts, two job logs and one mutation summary. The self-test has exactly 36 cases plus the real-input run.

(b) The parent's split row 10 says only "One verdict pull request each for C03, C10 and C01". This plan is stricter: it adds the generator's recomputation of every expected.yaml rule, its archive, phase-status and mutation checks, the plain-words ordering and inference-label checks of R9, and its self-test. Why: docs/agents/pull-requests.md rule 4 requires counts and totals by script; AGENTS.md "Verdict integrity" makes any disagreement with a candidate a finding, which only a recomputation can detect; decision 7126257034 requires the plain words before any verifier term; and docs/agents/pull-requests.md rule 2 requires each unmeasured causal step to be labelled, which only a check can hold.

(c) Dependencies neither on `main` nor in the pull request: the run's artifacts and job logs, recorded output pinned by hash in section 4.1 and retained until 2027-02-21T06:31:39Z; the mutation summary of run 45555996117, recorded output pinned by hash in section 4.1 and retained until 2027-02-23T05:47:27Z, checked by G1, G10 and acceptance step 6 (section 4.9); the librdkafka sources, pinned by tag and hash; the Microsoft page, fetched 2026-11-24 and not versioned. No deployment assumption exists (section 8).

## 15. Open questions for Hari

None. Decision 7168528766 settles how decision 7126257034 is recorded (option A). Approving or rejecting the verdict stays with Hari on the verdict pull request; this plan proposes it and decides nothing about it.
