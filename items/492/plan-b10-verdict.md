# B10 verdict contract-grade plan

Revision: 5. Base commit: `main` at `2bb71e04c17f73d08c2fcb73e85165b787cf2c99`.

This plan writes the B10 evidence pack and the verdict pull request from scientific run 43689822806. Both verdicts that the merged decision rules select are `confirmed`: `timeout/application retry` from C0, with 56 duplicates in five valid repeats, and `restart recovery` from C1, with a qualifying cross-restart pair in each of the 4 eligible repeats of 5. The C1 candidate's root status is `invalid` because repeat 1's timeout evidence failed, so open question 2 asks Hari to confirm that this does not hold the restart verdict. Three decisions of 2026-11-15 require the opening of the write-up to state plainly that idempotence did not remove a duplicate created by an application resend, whether or not the producer restarted, and to give practical advice. The source check in section 5, R13, supports the restart half but not the no-restart half: the same-instance control arm resent messages that were already confirmed. Hari's decision 6892588990 narrows the text to the cross-restart result and adds one labelled inference with measured C0 backing; section 4.3 carries it, and open question 5 asks Hari to choose the practical-advice wording. Hari names the implementing tool, approves this plan, approves or rejects each verdict, and merges.

Item: B10, message timeout with application resend (C0) and producer restart with resend (C1) [issue #492; parent entry 5 of #347]. Workload data is synthetic. Every number below is copied from a pinned run artifact, a pinned log or a cited source, or computed from them by script on 2026-11-15; no number is an estimate unless labelled.

## 1. What changed

Revision 5 applies the [plan check of Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892727231), round 1 of the 3 that decision 6892599294 allows:

- Finding 1: the report title no longer ends "and whether or not the producer restarted". Decision 6892588990 point 1 says "Narrow every affected text to the cross-restart result". The title now uses the narrowed wording the check suggests. Open question 6 lets Hari change it.
- Finding 2: the change record after the diagnostic run now uses the check's literal replacement texts. They name the other files that pull requests 481, 485, 486, 487 and 489 changed. These are factual fixes, made under decision 6892599294 point 2.
- Findings 3 and 4 (`apply in revision`): a self-test case `missing_opening` covers R5 and R13, R4 is mapped to `c1_subject`, the case count becomes 20, and the Confluent.Kafka `PersistenceStatus` source is cited.

Revision 4 changed, from Revision 3:

Revision 4 applies [decision 6892588990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892588990) (Hari, 09:51:45Z), which answers blocking finding 1 of plan check 6892505222 and open question 4. Its check was stopped before it posted, because this decision superseded Revision 3.

- Point 1, option A: the plain-words paragraph keeps only the cross-restart result, and states that the same-instance resend came about 120 s after the original's successful confirmation. Options B and C of Revision 3 are removed. The summary's second sentence gives the same timing.
- Points 2 and 3: a new paragraph carries Hari's explanation, labelled as reasoning and an inference, with the C0 `phantom` 70 and `reported_failure` 100 counts as measured backing for its second half. R13 records the check of each point.
- Open question 4 is answered. Open question 5 stays: decision 6892344763 stands, and its advice does not rest on the same-instance arm.

Revision 3 changed, from Revision 2:

Revision 3 applies the [plan check of Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892505222):

- Finding 1: the same-instance control arm resent messages that were already confirmed. In every C1 repeat the journal records the cut original's delivery report, delivered with `persistence_status` 2 (Persisted), before the resend 120 s after the cut, as ADR-021 protocol rule 7 requires ("even if a member has since completed"). The summary, both plain-words options, the README row, the plan's opening and R13 now say so. The no-restart half of the decisions' claim is not supported by that arm. Open question 4 gives three wordings, and option C rests a no-restart statement on the C1 timeout comparison only. The verdict selection does not change.
- Finding 2: the change record after the diagnostic run is corrected. Pull request 484 changed `phase.py` and `test_phase.py`. Pull requests 481, 485, 486 and 487 changed only `test_workflow.py`, updating the pinned hash of `.github/workflows/verifier-c5-mutation.yml`. G12 now checks the change list per pull request.
- Finding 3, `apply in revision`: the size estimate text is inserted in section 3, as the check gives it.
- Finding 4: the open questions remain for Hari.
- The three P2s are applied: exit codes come from `b10_lifecycle` or `pods.json`; self-test value cases change parsed data after G1; and the inference paragraph places the persistence-status text in the idempotent producer's error-handling section.

Revision 2 changed, from Revision 1:

Revision 2 applies [decision 6892344763](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892344763), posted at 09:29:04Z, after Revision 1's plan comment (09:27:19Z). No check of Revision 1 was posted; its check was stopped before it posted because this decision superseded the revision.

- Section 2 lists the decision. Section 4.3 adds the practical-advice paragraph in two options. R13 records the source check of its points, with one correction and one narrowing. Open question 5 asks Hari to choose.
- The inference paragraph of section 4.3 now also quotes librdkafka's "Message persistence status" text, which sits in the same "Idempotent Producer" section. The R13 point 4 bullet explains why.

Revision 1 was the first revision.

## 2. Goal and authority

Goal: publish B10's two proposed verdicts with the evidence pack that docs/agents/factory.md "Evidence pack" requires, from the one scientific run that the expected outcomes admit, so Hari can approve or reject each verdict.

Authority, in creation order. No item state file exists for #492 on `main`.

1. [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492), created 2026-11-15T09:09:13Z: write the B10 evidence pack and verdict pull request from run 43689822806; the pull request is labelled `verdict` and goes to Hari; Hari names the implementing tool and approves the plan. It lists the known points that section 5 answers.
2. [Comment 6892130196](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892130196), 09:09:13Z, is a `## Control action` record, not a decision.
3. [Decision 6892258228](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892258228), 09:21:03Z: the write-up states plainly, in its opening sentences, five points (idempotence does not remove a duplicate created by an application resend, whether or not the producer restarts; C0 duplicates are not attributed; what is not shown; the inference about idempotence; transactions as a pointer). "Every claim above must be checked against its source by the plan and the plan check; the plan corrects any point the sources do not support, and says so." Section 5, R13, gives the check and the three corrections.
4. [Decision 6892276924](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892276924), 09:22:46Z: the write-up carries Hari's sentence ("If your application sends the same message again because it didn't get a confirmation, you get a duplicate. Turning on idempotence doesn't stop that. It doesn't matter whether the producer restarted in between.") in plain words near the top, scoped to the conditions tested; "If the sources need a narrower wording, the plan proposes it to Hari instead of changing it silently." Open question 4 does that.

5. [Decision 6892344763](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892344763), 09:29:04Z: the write-up carries, in plain words, the practical consequence: with idempotence off consumers must be prepared for duplicates; the source of a duplicate matters for the fix; and advice, "labelled as the repository's guidance drawn from these results and not as a Microsoft statement", with transactions pointed to issue 493. "The plan checks each point against its source and proposes a narrower wording to Hari if needed." Open question 5 does that.

6. [Decision 6892588990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892588990), 09:51:45Z: answers blocking finding 1 of [plan check 6892505222](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892505222). It amends decision 6892258228 point 1 and replaces the sentence of decision 6892276924. Point 1: narrow to the cross-restart result, and state that the same-instance resend came after the original's successful confirmation. Point 2: one plain explanation, labelled as reasoning. Point 3: the C0 `phantom` and `reported_failure` counts, verified against the candidate, as C0-only backing. "Decision 6892344763 stands, subject to the same narrowing where it relies on the same-instance arm."

7. [Decision 6892599294](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892599294), 09:52:43Z: up to 3 more rounds of revision and plan check, beyond the `plan-pr` skill's cap, counted from the revision that applies decision 6892588990 (this Revision 4). Exhaustion action: hand the latest revision and its check to Hari. Within those rounds the planning session revises, without asking Hari, findings that only correct plan text against its sources. Choices about scientific or published wording, a verdict rule, a policy value, scope, and plan approval stay with Hari.

The decisions change no verdict rule, count or expected outcome.

Parent: #347. The child's body names the parent plan: the [B10 split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617284257), adopted by [decision 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468), says "The batch run and the verdict pull request stay parent entry 5 of #347"; the [#347 body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347) entry 5 is "One verdict pull request each for ... B10". Parent decisions read: [6617257796](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796) (standing authority), [6633528258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633528258), [6633791790](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633791790) and [6671722866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6671722866). Under docs/agents/factory.md "Item lifecycle" step 3, a parent decision changes this child's plan only when a decision on #492 links and adopts it; none does. This plan therefore relies on none of them. In particular the standing authority's point 2 (control session may merge verdict pull requests) is not adopted here, and AGENTS.md GOVERNANCE "Verdict integrity" ("Verdict pull requests always go to Hari") and factory.md "Item lifecycle" step 5 ("Hari approves or rejects each B10 verdict separately") apply: Hari merges.

Design and process authority on `main` at the base commit: `scenarios/B10/expected.yaml` (SHA-256 `35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f`, checked by `sha256sum` on 2026-11-15); docs/blueprint.md ADR-021 "Restart decision rules" 1 to 7 (lines 693 to 699) and protocol rules 9 and 13 (lines 713 and 717); docs/operations/verifier-contract.md "Scientific B10 final judgement" (lines 287 to 314); docs/agents/factory.md "Evidence pack" (lines 183 to 202).

Candidate selection: no split into pull requests exists on #492 and no plan comment exists, so the candidate is the whole issue as one pull request, named `B10 verdict`. It is one coherent piece: one evidence pack for one run. The issue body names no `Predecessor PRs:`. The expected outcomes it depends on merged in [pull request 490](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/490) as the base commit itself.

## 3. Scope

Files allowed to change, and only these six:

| Path | Change | Protected |
|---|---|---|
| `results/B10/2026-11-15.json` | new | yes, `/results/` |
| `results/B10/2026-11-15.md` | new | yes, `/results/` |
| `README.md` | line 28, the B10 row, only | yes, `/*.md` |
| `docs/operations/evidence/issue-492-b10-verdict/make_results.py` | new | no |
| `docs/operations/evidence/issue-492-b10-verdict/receipt.md` | new | no |
| `items/492/state.md` | new | no |

The date is the run's UTC date, 2026-11-15, as in every earlier run item (for example `results/A03/2026-11-02.md` for a run created 2026-11-02T22:07:00Z and committed the next day).

Size estimate (estimate, 2026-11-15; basis: line counts by `wc -l` of the A04 precedent at the base commit: `docs/operations/evidence/issue-322-a04-verdict/make_results.py` 1193 lines, its `receipt.md` 62, `results/A04/2026-11-03.md` 318): about 1200 lines for `make_results.py`, 60 for `receipt.md`, 350 for `results/B10/2026-11-15.md`, 30 for `items/492/state.md` and 1 changed line in `README.md`; `results/B10/2026-11-15.json` is generator output whose size follows the two pinned candidates and is not estimated by hand. Six files.

Paths that must not change: `scenarios/B10/` (both files), `src/`, `tests/`, `charts/`, `infra/`, `scripts/`, `.github/`, `docs/blueprint.md`, `docs/agents/`, `docs/operations/` outside the new evidence folder, every other path under `results/` and `items/`. No workflow is dispatched. No expected outcome, decision rule or verifier output is edited (AGENTS.md GOVERNANCE "Verdict integrity").

The results files are generator output. docs/agents/pull-requests.md rule 8 says "Agents commit no program output"; its verdict paragraph keeps work that "the verdict step requires", and every earlier verdict pull request commits its results files the same way (for example `results/A04/2026-11-03.json`, written by `docs/operations/evidence/issue-322-a04-verdict/make_results.py`). The pull request description lists this, with the protected-path findings, under that paragraph.

## 4. External contracts

### 4.1 Inputs

The generator reads only these inputs, each pinned by SHA-256 (all computed by `sha256sum` on 2026-11-15 from downloads through `gh api .../actions/artifacts/<id>/zip` and `.../actions/jobs/<id>/logs`):

| Input | Source | SHA-256 |
|---|---|---|
| C0 candidate artifact ZIP | artifact 14152258019, `b10-C0-candidate-43689822806-1` | `0017cda383d6b9c14b2519356d73329d7e6b38b70bf064159e4ccb27b2f2b7de` |
| its member `candidate.json` | | `8bf46cd85488e567802f00e92202b8e4120b283b96842b95a4091d7bb1c9a40f` |
| C1 candidate artifact ZIP | artifact 14155072618, `b10-C1-candidate-43689822806-1` | `f31b157c30bdf3536e8b44dd482c1d252cf191cbb02c8a51a5b4f19acccb9c3d` |
| its member `candidate.json` | | `871a3b891544792e32cdba4fce0434216194a04086f3bfeb3ee0fad02f5be3f3` |
| C0 pods artifact ZIP | artifact 14152018841, `b10-C0-pods-43689822806-1` | `7072f2cbe9436156a52e69b5257ca9ffb19ef820fea3b0e2cca892aa5590bbde` |
| C1 pods artifact ZIP | artifact 14154817943, `b10-C1-pods-43689822806-1` | `e804d6a5f600bb15a535afaf2afba5fe061575a8e8aa8211bad4f48ea583b582` |
| C1 pods member `b10-abrupt/r1/nonfinal-result.json` | | `fc488ee46631fbecb046e5307be4421c3a8f6dd52fc7c89a82a4394ba5681726` |
| C0 job log | job 132737914930 (`probe`) | `ddf996cd9c86abd9fcb86159ec4c0092500a53b35b9f286b5b7d69acc8ce349f` |
| C1 job log | job 132743948785 (`full-C3`) | `1e13e925f62bd6608cc94b50387d7501777b067937a7f68fd952990f3dab82bf` |
| C5 summary artifact ZIP | artifact 14145501472, `c5-summary-43670258319-1` | `8c0ce121878d714203bac000450b0052648b571c0a02a94cb378720a5278be75` |
| its member `summary.json` | | `d78dfef1ca86652632c7a6f2ec3c69e32749355df6455ee8ef78577fb1112e2f` |
| `scenarios/B10/expected.yaml` | `git show 2bb71e04c17f73d08c2fcb73e85165b787cf2c99:` | `35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f` |
| `scenarios/B10/scenario.yaml` | `git show 2bb71e04c17f73d08c2fcb73e85165b787cf2c99:` | pinned by the implementer from that commit |

The C1 candidate member hash `fc488ee4...` of `nonfinal-result.json` equals the `source.sha256` of C1 repeat 1 in the C1 candidate's `b10_final.repeats`, so the retained file is the one the final sweep bound. Each other pods-artifact member the report links (each `pods.json`, container log and `collection-status.json`, and both `phase-status.json` files) is pinned by the implementer by hash in the generator. Artifacts expire 2027-02-13T07:24:40Z (artifact API `expires_at`); the report states that date.

The job names `probe` and `full-C3` are the workflow's job keys (`.github/workflows/scenario.yml` lines 55 and 449); the logs show which phase each ran: `probe` ran C0 (its `ephemeral_state_empty` at 07:51:50.8422656Z, log line 3610) and `full-C3` ran C1 (`ephemeral_state_empty` at 09:06:36.3740584Z, line 4262; `aks_stopped` at 09:07:11.4252371Z, line 4321).

If any pinned hash differs from a fresh download, the implementer stops under section 11.

### 4.2 `results/B10/2026-11-15.json`

Written as `json.dumps(document, indent=2, ensure_ascii=True) + "\n"`. Top-level keys exactly `publication` and `candidates`, in that order. `candidates` has exactly `C0` and `C1`, each JSON-equal to its pinned `candidate.json` member. `publication` has exactly these keys, in this order, with these literal values; `<...>` marks a value the generator copies from the named input.

```
item: "B10"
workload: "synthetic"
run: {workflow_run_id: 43689822806, attempt: 1, event: "workflow_dispatch", ref: "main",
      head_sha: "2bb71e04c17f73d08c2fcb73e85165b787cf2c99", created_at: "2026-11-15T07:24:39Z",
      conclusion: "success", run_id: "b10-43689822806-1"}
expected_outcome: {path: "scenarios/B10/expected.yaml", sha256: "35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f"}
verdicts:
  "timeout/application retry":
    proposed_verdict: "confirmed"
    deciding_configuration: "C0"
    rule: "docs/blueprint.md ADR-021, Restart decision rules, rule 1; scenarios/B10/expected.yaml verdict.timeout_application_retry.confirmed_when"
    subject: <expected.yaml verdict.timeout_application_retry.subject, verbatim>
    complete_valid_final_repeats: 5
    duplicate_total: 56
    duplicates_by_repeat: [47, 9, 0, 0, 0]
    attribution: <expected.yaml hypotheses.H_timeout_duplicate.attribution, verbatim>
  "restart recovery":
    proposed_verdict: "confirmed"
    deciding_configuration: "C1"
    rule: "docs/blueprint.md ADR-021, Restart decision rules, rules 2 to 5; scenarios/B10/expected.yaml verdict.restart_recovery.confirmed_when"
    subject: "With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log."
    declared_repeats: 5
    eligible_repeats: 4
    eligible_repeat_numbers: [2, 3, 4, 5]
    ineligible_repeats: [{repeat: 1, reasons: ["b10_restart_timeout_bookkeeping_unavailable"]}]
    qualifying_pair_repeats: [2, 3, 4, 5]
    lost_resend_finding_repeats: []
    eligible_loss_repeats: []
    comparison_by_repeat: {"1": "both", "2": "both", "3": "both", "4": "both", "5": "both"}
    c1_candidate_status: "invalid"
    c1_candidate_reasons: ["b10_final_timeout_evidence_invalid", "j7_counts_unavailable"]
c1_timeout_comparison:
  role: "comparison only; no verdict"
  repeats_with_counts: [2, 3, 4, 5]
  repeat_1: "absent"
  sum_repeats_2_to_5: {ok: 123915, loss: 0, phantom: 40, reported_failure: 443, duplicate: 27, reorder: 34}
c1_repeat_1:
  status: "invalid"
  reasons: ["b10_final_timeout_evidence_invalid"]
  nonfinal_problem: "b10_abrupt_verify_timeout_evidence_invalid"
  nonfinal_detail: "timeout_judgement"
  abrupt_check: 18
  failed_per_instance_check: "not_recorded"
broker_connection_counts: "unavailable"
broker_connection_counts_reason: "scientific candidates omit broker_count (docs/operations/verifier-contract.md, Scientific B10 final judgement)"
environment: {namespace_sku: "Premium", processing_units: 1, region: "uksouth",
              node_size: "Standard_D4ds_v5", node_size_source: "inferred from infra/cluster/aks.tf at the base commit; not captured"}
versions: <C0 candidate versions object>
mutation_acceptance: {workflow_run_id: 43670258319, commit: "1e01aea24a457fffe0dc970703af2de608d524e3",
                      measurement_complete: true, break_passed: true,
                      summary_sha256: "d78dfef1ca86652632c7a6f2ec3c69e32749355df6455ee8ef78577fb1112e2f"}
unpublished_runs: [{workflow_run_id: 43686555201, reason: "earlier attempt of the same authorization; its C1 phase failed before any repeat; nothing from it is published"},
                   {workflow_run_id: 43635822755, reason: "governed diagnostic run; never published as a result"}]
changes_after_diagnostic_run: [
  {pull_request: 484, merge_commit: "26329ed64a6414a48526107566909675f6ee1c87",
   paths: ["infra/scripts/phase/phase.py", "infra/scripts/phase/tests/test_phase.py"],
   kind: "implementation bug", approval: "https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/483#issuecomment-6884548840"},
  {pull_requests: [481, 485, 486, 487], merge_commits: ["4aa6218f9c0750f80b072e0bab1e26e754de8f94", "57406da8e3964399ef7bf81351e5f57542fb6836", "3c4df3d040f55356597f83f1cf460956adb49197", "336f4e53f3f8329fae297c7438eef3e86df40120"],
   paths: ["infra/scripts/phase/tests/test_workflow.py"],
   kind: "within src, charts, infra and the scenario workflow, test only: pinned SHA-256 of .github/workflows/verifier-c5-mutation.yml; no B10 runtime effect"}]
inputs: <one object per row of section 4.1: name, source, sha256>
```

Absent never becomes zero: no key in `publication` holds a 0 or an empty list for an unavailable measurement. `broker_connection_counts` is the string `"unavailable"`.

### 4.3 `results/B10/2026-11-15.md`

ASCII only. Title line, exactly:

`# B10: without idempotence, timeout resends left duplicates; with idempotence, a replacement producer's resend of an unconfirmed message still left both copies`

Then this three-sentence summary paragraph, exactly:

> In scientific run 43689822806 on Event Hubs Premium, with idempotence enabled and no transactions (C1), a fresh producer that replaced a self-killed one resent each message the killed producer had sent without receiving a delivery report, as a new `Produce` call with the same key and sequence number, and in each of the 4 eligible repeats of 5 both the original and the resend were in the log, so the restart recovery verdict is `confirmed` for the full subject below. In the same-instance control arm, the producer resent a message about 120 s after its delivery report had confirmed it as written, and both copies were in the log in every repeat too, so the comparison is `both` and the duplicate is not credited to the restart; that arm does not test a resend of an unconfirmed message without a restart. Without idempotence (C0), a `message.timeout.ms` of 100 and an application resend of each timed-out send left 56 duplicate records in five valid repeats (47, 9, 0, 0 and 0), so the timeout/application retry verdict is `confirmed`; the verifier does not attribute these duplicates between the application resend and librdkafka's own retry.

Then these two paragraphs, exactly (decision 6892588990):

> In plain words: with idempotence on, when the producer was killed before a send was confirmed and its replacement resent that message, both copies were in the log, in each of the 4 eligible repeats of 5. In this test the same-instance resend came about 120 s after the original's successful confirmation, so it does not test a resend of an unconfirmed message without a restart. Tested on Event Hubs Premium with Confluent.Kafka 2.16.0 and librdkafka 2.16.0, without transactions, with five repeats per configuration and a synthetic workload.

> Reasoning, not measurement: idempotence only removes librdkafka's own internal retries. If your application calls Produce again for a message it thinks failed, you get a duplicate whenever the first one was actually written, and a timeout doesn't tell you whether it was. The first sentence is an inference, consistent with the expected outcomes' `c1_inference`. The second half of the second sentence has measured backing in C0 only, with idempotence off: 70 sends were reported failed but consumed (`phantom`), and 100 were reported failed and not consumed (`reported_failure`), in five repeats with a synthetic workload, and no rate is implied. librdkafka says of a send that timed out in flight that "the producer can't know if the message reached the broker" (INTRODUCTION.md v2.16.0).

Then these three paragraphs, exactly:

> Not shown: pod or node loss; survival or loss of the producer's internal retry or idempotence state across the restart; whether an original was on the wire at the kill; and any rate, since there were five repeats per configuration with a synthetic workload and no measured rate is implied.

> librdkafka v2.16.0 states that the idempotent producer guarantees that "a message is only written to the log once" (INTRODUCTION.md, Idempotent Producer, Guarantees). That this guarantee covers only librdkafka's own retries, and not a new application `Produce` call with the same key and sequence number, is not stated there; the expected outcomes label it an inference. In the same "Idempotent Producer" section, under "Message persistence status", librdkafka says of a message that "was transmitted to the broker, but no acknowledgement was received": "Application retry will risk ordering and duplication." That text sits in the Idempotent Producer's error-handling section, which describes "the Idempotent Producer specific errors"; it does not say in words whether its retry warning holds with idempotence enabled. Of a message that was written and acknowledged, the same list says: "No reason for application to retry." This run observed the duplicates; it did not test why.

> A pointer, not tested here: librdkafka's `transactional.id` "is used to identify the same transactional producer instance across process restarts" and lets the producer "guarantee that transactions corresponding to earlier instances of the same producer have been finalized prior to starting any new transactions, and that any zombie instances are fenced off" (CONFIGURATION.md v2.16.0). No cited source states that transactions prevent a duplicate created by an application resend; that is an inference. Microsoft's transactions guide says Kafka transactions are "currently in Public preview in Premium, and Dedicated tier", while its configuration page lists transactions as not currently supported (docs/blueprint.md section 12, conflict 6). B05 tested only that InitTransactions succeeds on Premium ([B05 result](../B05/2026-10-27.md)); begin, commit, abort, and whether transactions prevent this duplicate, were not tested.

Then the practical-advice paragraph that Hari's answer to open question 5 selects, exactly. Option B (recommended):

> What this means in practice. This is the repository's guidance drawn from these results, not a Microsoft statement. With idempotence off, consumers must be prepared for duplicates, whatever their source. The source matters for which fix works. librdkafka documents idempotence as the guard against duplicates from its own retries: with it, "a message is only written to the log once", and a broker's duplicate-sequence error "occurs upon retrying a previously successful send that was not acknowledged" (INTRODUCTION.md v2.16.0). A duplicate from an application resend was not stopped by idempotence in this run. So enable idempotence to stop internal-retry duplicates. If the application itself resends unconfirmed messages, consumers must still de-duplicate. Transactions are a candidate, but no cited source states that they prevent this duplicate, and they are untested on Event Hubs; issue 493 proposes that test. C0's duplicates are not attributed between librdkafka's retries and the application resend, so this report does not say how many of them idempotence would have removed.

Option A (the decision's advice as worded):

> What this means in practice. This is the repository's guidance drawn from these results, not a Microsoft statement. With idempotence off, consumers must be prepared for duplicates, whatever their source. The source matters for which fix works. Duplicates from librdkafka's own retries are what idempotence is built to stop. Duplicates from an application resend are not stopped by idempotence: B10 C1 still showed them with idempotence on. Enable idempotence to stop internal-retry duplicates; if the application itself resends unconfirmed messages, consumers must still de-duplicate, or use transactions. Whether transactions work for this on Event Hubs is untested; issue 493 proposes the test. C0's duplicates are not attributed between librdkafka's retries and the application resend, so this report does not say how many of them idempotence would have removed.

Then exactly these two lines:

`Proposed verdict, timeout/application retry: confirmed.`

`Proposed verdict, restart recovery: confirmed: With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log.`

Then these sections, in this order, with these headings:

1. `## Approved inputs and run`: the scenario and expected outcome as approved, linked at the base commit with the expected.yaml SHA-256; the run, its attempt, event, ref, head SHA, created time and conclusion; the four artifacts with IDs, names, ZIP SHA-256 and expiry; the two job logs with IDs and SHA-256; the [dispatch authorization 6890385963](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6890385963) and its [repeat 6891036629](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6891036629); and this sentence, exactly: `This report uses only run 43689822806; nothing is published from run 43686555201, an earlier attempt of the same authorization whose C1 phase failed before any repeat.` It also links the generator and the receipt.
2. `## Timeout/application retry (C0)`: a table, one row per C0 repeat, columns `Repeat | Status | classification_final | ok | loss | phantom | reported_failure | duplicate | reorder`, then a total row; values from C0 `b10_final.repeats[].timeout`. Total row: `154978 | 0 | 70 | 100 | 56 | 78`. The rule and its selection.
3. `## Restart recovery (C1)`: a repeat table, columns `Repeat | Status | Eligibility | Reasons | Comparison | Termination | Native errors (count, fatal, tail)`; an arm table, one row per repeat and arm, columns `Repeat | Arm | Cut attempt | Unconfirmed | Resends invoked | Originals seen | Resends seen | Pairs | Unreported attempts | Unreported seen | Unreported unseen | Lost resends`; a witness table, one row per repeat and arm, columns `Repeat | Arm | Original attempt | Resend attempt | Original (partition, offset) | Resend (partition, offset)`. All from C1 `b10_final.repeats[].restart`. Then the restart rule fields of section 4.2, the eligible count and every ineligibility reason, and the lost-resend finding lists, which are empty.
4. `## C1 timeout counts, comparison only`: a table for repeats 2 to 5 with the six counts, repeat 1 shown as `absent`, and the sum row `123915 | 0 | 40 | 443 | 27 | 34`.
5. `## C1 repeat 1`: the text of requirement R6.
6. `## Versions, environment and timing`: Confluent.Kafka, librdkafka, .NET, verifier and workload generator versions; image digests; PU count, SKU, region and node size with their log lines and the inference label; worker start, judge completion and duration per phase from `timings`; teardown lines of section 4.1; and `Per-repeat broker connection counts: unavailable. Scientific B10 candidates omit them; they are not zero.`
7. `## Pod evidence`: one row per Job folder of both pods artifacts, with Job name, repeat, mode and outcome from `phase-status.json` `modes[]`, and exit code from `b10_lifecycle.producers[]` or `verifiers[]` `exit_code` where the Job is listed there, otherwise from that Job folder's `pods.json` `status.containerStatuses[0].state.terminated.exitCode`; and links to the artifacts. Expected exit codes: each C0 verify Job 1, the C1 judge 1, every other Job 0. It states, exactly: `Each C0 verify Job records outcome failed and exit code 1 by design: the scientific C0 verify handler exits 1 even when its window checks pass (docs/operations/verifier-contract.md, Scientific C0 verify). The C1 judge Job records outcome failed because the judge exits 1 for any root status other than complete_valid.`
8. `## Changes after the diagnostic run`: this sentence, exactly: `Between the governed diagnostic run 43635822755 (main at dbc25c2005c3409ffd5f079c40b061d82c4b02aa) and this run, the only runtime change under src, charts, infra or the scenario workflow was pull request 484, which raises whole-second verifier pod times before the B10 final sweep line in infra/scripts/phase/phase.py, with tests in infra/scripts/phase/tests/test_phase.py; Hari decided it in decision 6884548840 on issue 483. Within those paths, pull requests 481, 485, 486 and 487 changed only infra/scripts/phase/tests/test_workflow.py, updating its pinned SHA-256 of the C5 mutation workflow; outside them they changed .github/workflows/verifier-c5-mutation.yml, files under tests/Parchwell.Verifier.Tests/Mutation, docs/operations/verifier-mutation.md and items/474/state.md. The diagnostic run is never published as a result.` Pull request 489 added tests under tests/Parchwell.Verifier.Tests/Component and changed docs/operations/b10-workflow.md; it changed no file under src, charts, infra or the scenario workflow.
9. `## Mutation acceptance`: run 43670258319 on `1e01aea24a457fffe0dc970703af2de608d524e3`, `measurement_complete` true, `break_passed` true, summary SHA-256; and this sentence, exactly: `No file under src/Parchwell.Verifier changed between that commit and the run commit 2bb71e04c17f73d08c2fcb73e85165b787cf2c99.`
10. `## Statements required by the expected outcomes`: the sentences of requirement R8, verbatim.
11. `## Not claimed`: the four `not_claimed` entries of expected.yaml, verbatim, and the two SIGKILL limits of ADR-021 protocol rule 9, as worded in R8.

### 4.4 `README.md` line 28

Replaced by exactly:

```
| B10 | Message timeout, application retry and restart recovery | With idempotence and no transactions, a replacement producer's resend of each message a self-killed producer had sent without confirmation left both copies in the log in each of 4 eligible repeats of 5. A same-producer resend of an already confirmed message also left both copies, so the duplicate is not credited to the restart. Without idempotence, with `message.timeout.ms=100` and an application resend of each timed-out send, five valid repeats left 56 duplicate records (47, 9, 0, 0, 0); the verifier cannot tell such a duplicate from one caused by librdkafka's own retry. Synthetic workload. | timeout/application retry: confirmed; restart recovery: confirmed: With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log. | [Report](results/B10/2026-11-15.md) |
```

### 4.5 `items/492/state.md`

The fields of docs/agents/factory.md "Item state file", in that order. `Parent items:` links #347. `Item plan:` links the B10 split 6617284257 and the #347 body entry 5. `Implementing tool:` and `Approved plan:` follow Hari's tool-naming comment and approval. `Decisions in force:` starts with the issue body, then each decision or approval on #492 in creation order.

## 5. Requirements

R1. WHEN the generator runs, THEN it checks every input against section 4.1 and fails with `FAIL G1` on any mismatch, before writing anything.

R2. WHEN the C0 candidate is read, THEN the generator recomputes from `b10_final.repeats` that all five repeats are `complete_valid` with `classification_final` true and that the duplicate sum is 56, and requires `timeout_rule` to equal `{complete_valid_final_repeats: 5, duplicate_total: 56, rule_selection: "confirmed"}` and the root `counts` to equal the per-repeat sum. Any difference is `FAIL G2` or `FAIL G3` and a finding, not a fix.

R3. WHEN the C1 candidate is read, THEN the generator recomputes eligibility (repeats whose `restart.eligibility` is `eligible`), qualifying pairs (eligible repeats whose `cross_restart` arm has `pair_count >= 1` and `unreported_seen >= 1`), lost-resend findings (any repeat with an arm `lost_resends >= 1`) and eligible loss, and selects by the table in verifier-contract.md "Scientific B10 final judgement". It requires the result to equal every field of `restart_rule`, including `rule_selection` `confirmed`, `selection_reason` null and the subject of section 4.2. Any difference is `FAIL G4` and a finding.

R4. WHEN the restart verdict is proposed, THEN it is the restart rule's selection, proposed only with the full subject, never a bare word or "K-53 confirmed" (expected.yaml `verdict.restart_recovery.confirmed_when`), and C1 repeat 1's pair is shown but never counted toward the verdict (ADR-021 rule 2, "no replacement repeats or favorable subset selection"). This holds only under Hari's answer A to open question 2; answer B needs a plan revision before implementation.

R5. WHEN the restart verdict is published, THEN the report states that every repeat's comparison is `both`, and that `both` prevents crediting restart for same-instance duplication (ADR-021 rule 5), in the words of section 4.3.

R6. WHEN C1 repeat 1 is reported, THEN the report states exactly: `Repeat 1 is invalid with reason b10_final_timeout_evidence_invalid, and its restart evidence is ineligible with reason b10_restart_timeout_bookkeeping_unavailable. Its retained nonfinal result (b10-abrupt/r1/nonfinal-result.json in the C1 pods artifact, SHA-256 fc488ee46631fbecb046e5307be4421c3a8f6dd52fc7c89a82a4394ba5681726, the file the final sweep bound) records problem b10_abrupt_verify_timeout_evidence_invalid with detail timeout_judgement, which the verifier raises when the timeout producer's evidence fails abrupt check 18: one of per-instance checks 1 to 6 or 12 to 27, or the retry rule. The verifier does not record which; the timeout producer's stored evidence was destroyed with the ephemeral storage, and its pod log has no warning or error line. Its timeout counts are absent, not zero. Its restart arms are shown below for completeness and do not count toward the verdict.` The generator requires the nonfinal file's `problems` and `problem_details` to equal those values (`FAIL G5`).

R7. WHEN a measurement is unavailable (C1 root counts, C1 repeat 1 timeout counts, broker connection counts, node SKU capture), THEN it is shown as `absent` or `unavailable` with its reason, never 0 (factory.md "Evidence pack"; expected.yaml `missing_counts_are_zero: false`). The generator requires the C1 candidate to have no root `counts` and to list `j7_counts_unavailable` (`FAIL G6`), and fails `G7` if any rendered cell for an unavailable measurement is `0`.

R8. WHEN the report is rendered, THEN item 10 of section 4.3 contains each of these sentences verbatim, and the generator fails `G8` if one is missing:
- `Microsoft's librdkafka configuration guidance has no message.timeout.ms or idempotence row.`
- `Duplicates appeared under timeout plus application resend: the C0 duplicate sum is 56. The verifier does not attribute a duplicate to the application resend rather than librdkafka's own retry.`
- `A late append after the phase sweep line is not detected.`
- `Workload data is synthetic.`
- `The restart is a process self-kill followed by pod replacement, with an unknown native tail, a fixed cut position at CutOrdinal 4, a fixed probe rate of 100 originals per second and a fixed arm order, same_instance then cross_restart; these are policy values, not measurements.`
- `The restart verdict claims neither pod or node loss nor survival of internal retry state.`
- `The chain of termination evidence cannot exclude an external SIGKILL indistinguishable from the intended one, or a crash between kill_intent and the signal (ADR-021 protocol rule 9).`
- `C1 timeout counts are a comparison only. Under idempotence, repeats 2 to 5 of C1 recorded 27 duplicates; this report draws no conclusion from them.`

R9. WHEN `--check` runs, THEN the generator re-renders both results files and README line 28 and requires byte equality with the committed files (`FAIL G9`), and requires the working-tree `scenarios/B10/expected.yaml` and `scenario.yaml` to equal the base commit's (`FAIL G10`).

R10. WHEN any rendered file contains a byte outside ASCII, or a leftover template token, THEN the generator fails `G11`.

R12. WHEN the generator runs, THEN it requires `git diff --name-only dbc25c2005c3409ffd5f079c40b061d82c4b02aa 2bb71e04c17f73d08c2fcb73e85165b787cf2c99 -- src charts infra .github/workflows/scenario.yml` to list exactly `infra/scripts/phase/phase.py`, `infra/scripts/phase/tests/test_phase.py` and `infra/scripts/phase/tests/test_workflow.py`, and for each path `git log --first-parent --format=%H dbc25c2005c3409ffd5f079c40b061d82c4b02aa..2bb71e04c17f73d08c2fcb73e85165b787cf2c99 -- <path>` to list exactly the merge commits that `changes_after_diagnostic_run` gives for it: `26329ed6...` for the first two, and `336f4e53...`, `3c4df3d0...`, `57406da8...`, `4aa6218f...` (full SHAs as in section 4.2) for the third (`FAIL G12`). So `changes_after_diagnostic_run` is complete and correctly attributed (factory.md "Evidence pack": "The pack records any Hari-approved implementation-bug or mechanical-budget change after the diagnostic trial").

R13. WHEN the opening of section 4.3 is rendered, THEN it contains the summary, the two paragraphs of decision 6892588990, the three paragraphs that follow and the practical-advice paragraph Hari selects, verbatim, before the proposed-verdict lines (`FAIL G8` otherwise). Their check against the sources, done for this plan on 2026-11-15 under decisions 6892258228, 6892276924, 6892344763 and 6892588990:
- Point 1 is corrected: supported for the cross-restart arm, not for the no-restart case. C1 sets `enable.idempotence: true` (`scenarios/B10/scenario.yaml`) and the baseline omits `transactional.id`, which only B05 may set (`src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` line 133). In the 4 eligible C1 repeats, `cross_restart` had 8 resends of 8 originals that had no terminal record at the kill, and 8 pairs; `same_instance` had 4 resends and 4 pairs. But in every C1 repeat the `same_instance` journal (`b10-abrupt/rN/*-loadgen-restart-*/journal.jsonl` in the C1 pods artifact) records the cut original's `terminal` record, `delivered` true, `error_code` 0, `persistence_status` 2, before the resend. The load generator writes the Confluent.Kafka `PersistenceStatus` value (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptProducer.cs` line 316), and 2 is `Persisted` (Confluent.Kafka v2.16.0 `src/Confluent.Kafka/PersistenceStatus.cs`: `NotPersisted = 0`, `PossiblyPersisted = 1`, `Persisted = 2`; read 2026-11-15). ADR-021 protocol rule 7 (docs/blueprint.md line 711) resends each member 120 s after the cut "even if a member has since completed". So the same-instance arm resent a confirmed message. Every repeat's comparison is still `both`, and ADR-021 rule 5 still applies: `both` "prevents crediting restart for same-instance duplication". The only same-producer evidence of a resend after a missing confirmation, with idempotence on, is the C1 timeout cohort: 27 duplicates in repeats 2 to 5, comparison only and not attributed (ADR-021 rule 1). Decision 6892588990 point 1 adopts this narrowing (option A of plan check 6892505222); the summary and the plain-words paragraph follow it. The journals put the resend 119.995 to 119.997 s after the original's terminal record (plan check 6892505222, computed by script), so "about 120 s".
- Point 2 is supported: expected.yaml `H_timeout_duplicate.attribution`.
- Point 3 is supported: expected.yaml `not_claimed`, all four entries; "no measured rate is implied" is from `universal_causation`.
- Point 4 is corrected. The decision says that idempotence de-duplicating librdkafka's own retries is the inference; decision 6892344763, "Limits", repeats it. expected.yaml `c1_inference` says the reverse: the guarantee ("a message is only written to the log once", INTRODUCTION.md v2.16.0 line 496) is stated, and the inference is that it covers only librdkafka's own retries and not a new application `Produce` call. The paragraph follows expected.yaml. It also quotes INTRODUCTION.md lines 755 to 767, "Message persistence status", a subsection of "Idempotent Producer" (heading at line 477), whose `POSSIBLY_PERSISTED` entry says "Application retry will risk ordering and duplication". expected.yaml `source_scope` already records that placement. That text sits under "Error handling" (line 641), which describes "the Idempotent Producer specific errors". Inference: it partly states what `c1_inference` calls silence, though it does not say in words whether its warning holds with idempotence enabled. The paragraph says so and changes no expected-outcome label. The same list's `RD_KAFKA_MSG_STATUS_PERSISTED` entry, "No reason for application to retry" (lines 768 to 770), is quoted for the same-instance arm.
- Point 5 is corrected twice. CONFIGURATION.md v2.16.0 line 160 supports the `transactional.id` quotation, and Microsoft's transactions page, read 2026-11-15 with `microsoft_docs_fetch`, says "Kafka Transactions is currently in Public preview in Premium, and Dedicated tier". But no source says transactions close this gap, so "what closes the gap" becomes "a pointer", with the closing labelled an inference. And B05 does not test transactions in general: `results/B05/2026-10-27.md` says "Only InitTransactions was tested in these synthetic repeats; begin, commit, abort and exactly-once semantics were not."
- Decision 6892344763, points 1 and 2, are supported. C0 had 56 duplicates with idempotence off. Idempotence as the guard against internal-retry duplicates: INTRODUCTION.md v2.16.0 line 496 (Guarantees) and lines 692 to 693 (`RD_KAFKA_RESP_ERR_DUPLICATE_SEQUENCE_NUMBER`: "This error is typically benign and occurs upon retrying a previously successful send that was not acknowledged"); "is built to stop" in option A paraphrases these, and option B quotes them. C1 duplicates with idempotence on: the counts of point 1 above.
- Decision 6892344763, point 3, is narrowed in option B, for open question 5. "Or use transactions" presents transactions as a fix for this duplicate. No cited source states that they prevent a duplicate created by an application resend: CONFIGURATION.md line 160 covers finalizing earlier instances' transactions and fencing zombies, and Microsoft's transactions page covers atomic writes. The decision's own "Whether transactions work for this on Event Hubs is untested" leaves the general claim unsourced. Issue 493 exists (open, created 2026-11-15T09:25:26Z, "Proposal (v1.1): do Kafka transactions prevent the application-resend duplicate that B10 found?").
- Hari's sentence of decision 6892276924 is replaced by decision 6892588990, because the no-restart half was unsupported.
- Decision 6892588990 point 2: "Idempotence only removes librdkafka's own internal retries" is not stated by a cited source, so it is labelled an inference, as the decision requires; expected.yaml `c1_inference` labels the same scope limitation an inference. "You get a duplicate whenever the first one was actually written" is reasoning: it assumes the resend is also written, and no count in this run tests it. The paragraph labels the whole as reasoning, not measurement.
- Decision 6892588990 point 3 is supported. The C0 candidate's root `counts` and the sum of `b10_final.repeats[].timeout` both give `phantom` 70 and `reported_failure` 100 (computed by script; repeat phantoms 65, 5, 0, 0, 0; reported failures 46, 12, 7, 22, 13). docs/blueprint.md lines 399 and 400 define `phantom` as ledger "failed", consumed "yes", and reported failure as ledger "failed", consumed "no". These counts back only "a timeout doesn't tell you whether it was" written. They do not test "whenever": the outcome classes count produce attempts, while duplicates count a key and sequence number consumed more than once (blueprint lines 393 and 405), and the verifier does not link one to the other. librdkafka INTRODUCTION.md v2.16.0 lines 376 to 385 ("Timed out in flight to/from broker"): "At this point the producer can't know if the message reached the broker".

R11. WHEN `--self-test` runs, THEN it runs the real inputs once (must pass), then each case of section 7 on an in-memory copy. The cases `hash_c0_candidate` and `hash_nonfinal_r1` change bytes before G1; every other case changes the parsed objects, or for `report_edit`, `non_ascii` and `expected_yaml_edit` the in-memory text, after G1 has passed on the unmodified bytes. Each case each of which must fail at its named check; it prints `PASS <case>` per case and exits 1 if any case passes unexpectedly or fails at another check.

Enforced checks of the generator, the complete list: G1 input hashes; G2 C0 timeout rule recomputation; G3 C0 root counts equal the per-repeat sum; G4 C1 restart rule recomputation; G5 C1 repeat 1 nonfinal problem and detail; G6 C1 root counts absent with `j7_counts_unavailable`; G7 no zero for an unavailable measurement; G8 required sentences present; G9 byte equality in `--check`; G10 scenario files equal the base commit's; G11 ASCII and no leftover token; G12 the post-diagnostic change list.

## 6. Invariants and safety rules

- No edit to an expected outcome, a verdict rule, a verifier output or a candidate (AGENTS.md GOVERNANCE "Verdict integrity"). A recomputation that disagrees with the candidate is a finding: stop under section 11.
- No dispatch, no Azure access, no secret, tenant ID or subscription ID (hard safety rules 2, 3 and 7). The job logs show the subscription as `***`; the generator copies no other log text than the lines section 4.3 cites.
- No number that was not measured or computed by script (hard safety rule 11; docs/agents/pull-requests.md rule 4).
- The pull request is labelled `verdict`, touches protected paths and is merged by Hari only.
- The earlier attempt 43686555201 publishes no data (AGENTS.md GOVERNANCE "Verdict integrity": "A published result shows the final run only").

## 7. Test map

The tests are the generator's `--self-test` cases. Each changes one value in memory and must fail at the named check. No existing test changes, so the standing fixture rule has no case to apply; if one did, the implementer would follow the `plan-pr` skill's plan rule 2 exactly.

| Requirement | Case | Change | Expected |
|---|---|---|---|
| R1 | `hash_c0_candidate` | one byte of the C0 `candidate.json` | `FAIL G1` |
| R1 | `hash_nonfinal_r1` | one byte of `b10-abrupt/r1/nonfinal-result.json` | `FAIL G1` |
| R2 | `c0_duplicate_r1` | C0 repeat 1 `timeout.duplicate` 47 to 46 | `FAIL G2` |
| R2 | `c0_repeat_status` | C0 repeat 5 `status` to `invalid` | `FAIL G2` |
| R2 | `c0_root_ok` | C0 root `counts.ok` 154978 to 154977 | `FAIL G3` |
| R3 | `c1_eligibility_r2` | C1 repeat 2 `eligibility` to `ineligible` | `FAIL G4` |
| R3 | `c1_pair_r3` | C1 repeat 3 `cross_restart.unreported_seen` 1 to 0 | `FAIL G4` |
| R3 | `c1_lost_resend_r1` | C1 repeat 1 `cross_restart.lost_resends` 0 to 1 | `FAIL G4` |
| R3, R4 | `c1_subject` | `restart_rule.subject` with its last word removed | `FAIL G4` |
| R3 | `c1_selection_boundary` | repeats 2 and 3 `ineligible`, leaving 2 eligible | `FAIL G4` (recomputed `held`, `fewer_than_three_eligible`, candidate says `confirmed`) |
| R6 | `nonfinal_detail` | r1 `problem_details[0].detail` to `timeout_evidence` | `FAIL G5` |
| R7 | `c1_root_counts_present` | add root `counts` of six zeros to C1 | `FAIL G6` |
| R7 | `broker_zero` | `broker_connection_counts` to 0 | `FAIL G7` |
| R8 | `missing_sentence` | delete the residual-risk sentence | `FAIL G8` |
| R5, R13 | `missing_opening` | delete the second sentence of the summary paragraph, the one that states the comparison is `both` | `FAIL G8` |
| R9 | `report_edit` | change one table cell of the committed md | `FAIL G9` |
| R9 | `expected_yaml_edit` | one byte of working-tree `expected.yaml` | `FAIL G10` |
| R10 | `non_ascii` | an en dash in the rendered md | `FAIL G11` |
| R12 | `extra_change` | the expected path list without `infra/scripts/phase/tests/test_workflow.py` | `FAIL G12` |
| R12 | `wrong_attribution` | `test_workflow.py` attributed to `26329ed64a6414a48526107566909675f6ee1c87` | `FAIL G12` |

Boundary: `c1_selection_boundary` is the three-eligible threshold of ADR-021 rule 2 (3 of 5 must be eligible); with the real data 4 are eligible and the selection is `confirmed`.

## 8. Deployment assumptions

None. The pull request deploys nothing and runs no code against a container, image, chart or runtime. All live evidence is recorded output of the completed run, pinned by hash in section 4.1; no assumption about an open or fresh connection, or idle or loaded state, is relied on.

## 9. Acceptance

Run from the repository root, with the artifacts and logs downloaded to a scratch directory `$IN` outside the repository.

1. `sha256sum` of each ZIP, member and log equals section 4.1. Evidence: the output in the receipt.
2. `python3 -I docs/operations/evidence/issue-492-b10-verdict/make_results.py --inputs "$IN"` writes the two results files and prints the README row. Evidence: exit 0.
3. `... make_results.py --inputs "$IN" --check`. Evidence: exit 0 and `PASS check`.
4. `... make_results.py --inputs "$IN" --self-test`. Evidence: `PASS real_inputs` and one `PASS <case>` line for each of the 20 cases of section 7, exit 0.
5. `python3 -m json.tool results/B10/2026-11-15.json > /dev/null`. Evidence: exit 0.
6. `npm run check:docs`. Evidence: no warning on a changed file, or each warning explained in the pull request.
7. `LC_ALL=C grep -nP '[^\x00-\x7F]' results/B10/ docs/operations/evidence/issue-492-b10-verdict/ items/492/state.md README.md`. Evidence: no output.
8. `git diff --name-only origin/main...HEAD` lists exactly the six paths of section 3.
9. `git diff origin/main...HEAD -- README.md` changes only line 28.
10. Cold review under AGENTS.md "Before opening a pull request" rule 8, Codex review and Codex security review requested as hard safety rule 10 allows; the pull request labelled `verdict`.

`scripts/verify-dotnet.sh` is not required: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates").

## 10. Sources

- Run [43689822806](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43689822806), its artifacts and job logs, read 2026-11-15 through the GitHub REST API.
- C5 run [43670258319](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43670258319), `summary.json`: commit `1e01aea24a457fffe0dc970703af2de608d524e3`, `measurement_complete` true, `break_passed` true, score 6373 of 8860. It is the newest `verifier-c5-mutation.yml` run (runs list, 2026-11-15). `git diff --stat 1e01aea2 2bb71e04 -- src/Parchwell.Verifier tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` shows no change, so no configured mutation target changed after the measured commit (factory.md "Evidence pack", mutation paragraph). The issue body's run 43625662806 on `dbc25c20` also passed, but is not the latest applicable run.
- [Transactions in Apache Kafka for Azure Event Hubs](https://learn.microsoft.com/azure/event-hubs/apache-kafka-transactions), read 2026-11-15 with `microsoft_docs_fetch`: "Kafka Transactions is currently in Public preview in Premium, and Dedicated tier."
- Confluent.Kafka v2.16.0 [src/Confluent.Kafka/PersistenceStatus.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/PersistenceStatus.cs), read 2026-11-15 from raw.githubusercontent.com: `Persisted = 2`.
- docs/blueprint.md lines 393 to 405 at the base commit (ADR-012 outcome classes and duplicates).
- librdkafka v2.16.0 files as fetched from `raw.githubusercontent.com` on 2026-11-15: INTRODUCTION.md (SHA-256 `109a00994a9c8eed91564b11af7b95251363dac6e514740563fb4f69b378de31`), line 477, Idempotent Producer; line 496, Guarantees; lines 376 to 385, Timed out in flight; lines 692 to 693, DUPLICATE_SEQUENCE_NUMBER; lines 755 to 767, Message persistence status; lines 808 to 814, Old producer fencing. CONFIGURATION.md (SHA-256 `b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`), line 160, `transactional.id`.
- [Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), read 2026-11-15 with the Microsoft Learn `microsoft_docs_fetch` tool: its librdkafka tables list `socket.keepalive.enable`, `metadata.max.age.ms`, `request.timeout.ms`, `compression.codec`, the three consumer settings, `retries` and `partitioner`, and no `message.timeout.ms` or `enable.idempotence` row. This supports the first R8 sentence.
- librdkafka v2.16.0 [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md) and [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md): beyond the quotations of R13, cited only through expected.yaml's `subject`, `attribution`, `source_scope` and `c1_inference` text. No other Kafka or librdkafka behaviour is stated.
- `results/B05/2026-10-27.md` line 3 at the base commit, for B05's scope.
- Issue [493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/493), title and state read 2026-11-15 through the REST API.
- Verifier source at the base commit, for R6: `src/Parchwell.Verifier/VerifyRun/Abrupt/B10AbruptVerifyHandler.cs` lines 251 to 252 (`if (timeout is null) Problem(6, "timeout_judgement");`) and `VerifyRun/Abrupt/TimeoutEvidenceReader.cs` lines 142 to 150 (`Judge` returns null when `RunJudgement.Evaluate` is not `CompleteValid` or the retry rule fails); `src/Parchwell.Verifier/Judge/B10FinalJudge.cs` lines 196 to 206 and 234 (root status, `j7_counts_unavailable`, exit code).
- [Decision 6884548840](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/483#issuecomment-6884548840) on issue 483 (Hari, 2026-11-14) and [pull request 484](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/484), merged 2026-11-14T22:18:17Z as `26329ed64a6414a48526107566909675f6ee1c87`, an ancestor of the base commit; governed diagnostic run 43635822755 ran on `dbc25c2005c3409ffd5f079c40b061d82c4b02aa` (runs API). Hari made run 43635822755 count as the governed diagnostic run ([approval 6890174272](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6890174272)).
- `infra/cluster/aks.tf` lines 35, 44 and 51 at the base commit (`vm_size = "Standard_D4ds_v5"` for all three pools); the cluster plan reports `No changes.` at C0 log line 886 and C1 log line 719.
- Namespace: C0 log lines 1378 (`capacity = 1`), 1387 (`location = "uksouth"`), 1393 (`sku = "Premium"`); C1 log lines 1206, 1215 and 1221.

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope; when a pinned hash in section 4.1 differs from a fresh download; when a recomputation of R2 or R3 disagrees with a candidate (a finding under "Verdict integrity", not a fix); or when Hari's answer to open question 2 is B; or when G12 finds another change.

## 12. Deferred work

1. Optional, off the verdict path: record the failed per-instance check names in the B10 nonfinal result, so a future invalid timeout instance can be diagnosed from retained evidence. Evidence: `TimeoutEvidenceReader.cs` lines 142 to 143 discard `RunJudgementResult.FailedChecks`. Deferral is permitted because no verdict rule reads the reason, and R6 discloses the gap. Responsible: a separate issue, only if Hari wants it; it touches a protected verifier path and a mutation target. Gate blocked: none.

## 13. Plan-check answers

(a) No step's effort scales with a number unknown at planning time. The inputs are fixed: two candidates, two pods artifacts, two job logs and one C5 summary. The self-test has exactly 20 cases plus the real-input run.

(b) The parent plan, #347 body entry 5, says only "One verdict pull request each for ... B10". This plan is stricter: it adds the generator's recomputation of both rules, its self-test and its byte-equality check. Why: docs/agents/pull-requests.md rule 4 requires counts and totals by script, and AGENTS.md "Verdict integrity" makes any disagreement with a candidate a finding, which only a recomputation can detect.

(c) Dependencies neither on `main` nor in the pull request: the run's artifacts and job logs, which are recorded output pinned by hash in section 4.1 and retained until 2027-02-13T07:24:40Z; and the Microsoft page, current content read 2026-11-15. No test depends on behaviour outside these. No deployment assumption exists (section 8).

## 14. Open questions for Hari

1. **Implementing tool.** The issue body asks Hari to name it. Decision 6633528258 on #347 named Codex only for nine other issues. No recommendation; this plan is tool-neutral.

2. **Does the C1 candidate's root status `invalid` hold the restart verdict?** Facts: the root is `invalid` because one repeat is invalid (`B10FinalJudge.cs` line 196; verifier-contract.md line 314: "Root status is `invalid` for untrusted projection, incomplete scan or any invalid repeat"), and the judge exited 1 (line 234). The projection is trusted and `scan_complete` is true. Repeat 1's invalid timeout evidence reaches the restart rule only by making repeat 1 ineligible (b10-abrupt-contract.md line 476). expected.yaml holds the restart verdict at candidate level only for `b10_final` null, and decides each verdict "independently". The open point: ADR-021 rules 2 to 4 say "Independent timeout and phase gates remain" and "After eligibility and phase gates pass", and no cited text defines "phase gates". This plan reads them as the projection's trust and scan completion, which pass.
   - Option A (recommended): propose `restart recovery: confirmed` with the full subject, as the restart rule selects, and publish repeat 1 as ineligible. Effect: both verdicts go to Hari in this pull request.
   - Option B: hold the restart verdict because the C1 candidate is not `complete_valid`. Effect: a plan revision; the restart verdict publishes as `held` with the root reasons; a new scientific run would be needed to decide it, which needs a new dispatch authorization.

3. **Correction to the issue body, for the record.** The body says the per-repeat objects were destroyed. The C1 pods artifact does retain repeat 1's `nonfinal-result.json` (hash in section 4.1, equal to the candidate's bound source); it names the failure as abrupt check 18 (`timeout_judgement`) but not the per-instance check. R6 publishes that. No decision is needed unless Hari disagrees with R6's wording.

4. Answered by [decision 6892588990](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/492#issuecomment-6892588990): option A, the cross-restart result only, with the same-instance timing stated, and one labelled inference. Section 4.3 carries it.

5. **Practical advice: as worded or narrowed?** Decision 6892344763 asks the plan to propose a narrower wording if needed. It is needed for one phrase, for the reasons in R13. Neither option rests on the same-instance arm, so decision 6892588990's narrowing does not change them.
   - Option B (recommended): transactions are "a candidate, but no cited source states that they prevent this duplicate, and they are untested on Event Hubs; issue 493 proposes that test", and librdkafka's own words are quoted for what idempotence stops. Effect: no unsourced fix is recommended.
   - Option A: the decision's wording, including "or use transactions". Effect: the report recommends a fix that no cited source supports for this duplicate.
   Both texts are pinned in section 4.3; either answer needs no plan revision.

6. **Report title.** Decision 6892588990 point 1 narrows every affected text to the cross-restart result, so Revision 5 pins the title `# B10: without idempotence, timeout resends left duplicates; with idempotence, a replacement producer's resend of an unconfirmed message still left both copies`, as plan check 6892727231 suggests. Revision 4's title ended "and whether or not the producer restarted", which that decision withdrew. Recommended: keep the new title. Hari may give another; a different title needs no other change.
