# B10 timeout-and-resend expectations contract-grade plan

Revision: 5. Base commit: `main` at `1e01aea24a457fffe0dc970703af2de608d524e3`.

This plan adds `scenarios/B10/expected.yaml`, which adopts the two B10 decision rules that docs/blueprint.md ADR-021 already fixes, and the item state file. It needs Hari's own `## Approval`, and Hari merges the pull request. Hari answered every open question of Revision 4 in [decision 6889976034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034); none remains.

Item: B10, timed-out sends with application resend, and a producer restart with resend [issue #356; split entry 5 of #348]. No B10 run result, artifact or log was read for this plan, including smoke run 42189889469, any diagnostic trial and the governed diagnostic run 43635822755. Every number below is a cited configuration literal, a policy value from ADR-021 or a decision predicate. None is a measurement.

## 1. What changed

Revision 5 applies [decision 6889976034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034) (Hari, 2026-11-15, "Yes" to the five recommendations of [handoff 6883802237](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6883802237)) and the non-blocking findings of the [plan check of Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6883789075):

- Points 1 to 3: open questions 1 to 3 are answered as Revision 4 recommended, so the two-verdict text, option B of the duplicate sentence and the `held` mapping for a null `b10_final` stay as written. Section 14 now states `None`. Option A's line and digest are removed.
- Point 4 (finding B1): `known_risk.in_flight_timeout_validity` takes the replacement value quoted in handoff 6883802237 item 4, which adds the C1 consequence.
- Point 5 (finding B2): `c1_inference` takes the replacement value quoted in item 5; section 10 cites "Guarantees".
- `verdict.decision_rules_authority` names decision 6889976034, and `verdict.decision` links it.
- Check N1: the state-file template in section 4.2 says Revision 5 and lists decision 6889976034. Check O1: deferred item 3 adds per-repeat broker connection counts.
- Base moved from `dbc25c2005c3409ffd5f079c40b061d82c4b02aa` to `1e01aea24a457fffe0dc970703af2de608d524e3`. Between them only `docs/operations/b10-workflow.md` changed among the files the plan cites, in its sweep-line timing paragraph, which no rule in the file reads (checked by `git diff --stat`). The cited line numbers are unchanged.
- The digest changes to `35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f`.

Revision 4 applied the [plan check of Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6883666912):

- Finding 3: option A of open question 1 now names "restart attribution was not tested" as the one publication statement it drops. `H_restart_duplicate` carries `written_after_smoke`, as decision 6703447017 point 1, last bullet, requires, with the dates of the ADR-021 commits that added its rules. `expectations_basis` now states only what this session verified.
- Finding 4: both verdicts' `held_when`, and the two `otherwise`/`held_when` hypothesis lines, cover a candidate with `b10_final` null (verifier-contract.md line 293). Open question 3 asks Hari to confirm this rule wording.
- Finding 5: `known_risk` scopes the connection-close sentence to the idempotent producer, where INTRODUCTION.md places it, and states that no cited source covers C0. A new `source_scope` line records that "Message persistence status" also sits under "Idempotent Producer" and that ADR-021 labels its use for C0 an inference.
- P2s: rule 8's "fourth paragraph" (N1); the acceptance script checks the state-file field order (N2); deferred item 4 names the mutation-acceptance gate (N3).
- Both digests change.

Revision 3 changed, from Revision 2:

Revision 2 ([comment 6693074361](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6693074361), 2026-11-07) predates the B10 restart redesign. Since then, merged changes on `main` replaced ADR-021's restart protocol and added its "Restart decision rules" (docs/blueprint.md lines 691 to 699), required two labelled B10 verdicts (docs/agents/factory.md line 192 and "Item lifecycle" step 5), and added the scientific final projection and judgement (docs/operations/b10-abrupt-contract.md "Final sweep projection"; docs/operations/verifier-contract.md "Scientific B10 final judgement", lines 287 to 314).

- The file now carries two verdicts, `timeout/application retry` from C0 and `restart recovery` from C1, each with ADR-021's own rule. Revision 2 had one C0 verdict and recorded restart attribution as `not_tested`. Open question 1 asks Hari to confirm that change.
- The timeout rule reads only the checked `timeout` cohort, as ADR-021 rule 1 requires ("The expected outcomes must adopt this cohort projection before publication"). Revision 2's line that the second Job's records count in the six counts is removed: under ADR-021 rules 1 and 12, probe records never enter timeout totals.
- Rules point at the published fields `b10_final.timeout_rule` and `b10_final.restart_rule`. Revision 2's `b10_intake` and `broker_count` lines are removed: scientific candidates omit both (verifier-contract.md line 295).
- The `not_tested` block becomes `not_claimed`, listing only what ADR-021 says the restart verdict does not claim.
- The plan is now a contract-grade plan file with a pointer comment (docs/agents/factory.md "Item lifecycle" step 3).
- Sources were re-read on 2026-11-14.

## 2. Goal and authority

Goal: merge B10's expected outcomes before any scientific B10 result exists, so the scientific run can follow the governed diagnostic run without another wait ([#404 decision 6883475587](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6883475587) point 3, cited as context; it is not adopted on this issue).

Authority, in creation order. No item state file exists for #356 on `main`.

1. [Issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356): split entry 5, adds `scenarios/B10/expected.yaml`; decision rules and verdict word go to Hari. Predecessors: the pull request of #355 and its smoke run.
2. [Decision 6617624003](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6617624003): adopts the [#347 standing authority](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6617257796) points 1 to 6, the [B10 split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468) and [restart decision 6617344472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617344472). A decision rule or verdict word goes to Hari.
3. [Decision 6633535614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633535614): adopts [decision 6633528258](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633528258) points 1 to 4. Codex implements; a Claude review gates the Codex pull request. [Comment 6633653241](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633653241) names Codex.
4. [Decision 6633795942](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633795942): adopts [decision 6633791790](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6633791790). Point 2: a plan that sets a decision rule or verdict word, "such as #356's", needs Hari's own `## Approval`.
5. [Decision 6703447017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6703447017): decision rules (point 1), restart attribution `not_tested` (point 2), smoke status (point 3), client versions (point 4); "Merge of the expected-outcome pull request stays with Hari."
6. [Decision 6889976034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034): adopts ADR-021's two decision rules, superseding decision 6703447017 points 1 and 2 where they conflict and dropping only the publication statement "restart attribution was not tested"; option B of the duplicate sentence; `held` for a null `b10_final`; findings B1 and B2 with the quoted replacement values. "This is not plan approval."

Design authority on `main`: docs/blueprint.md ADR-021, "Restart decision rules" 1 to 7 and protocol rule 15 ("Eligibility threshold, pre-acceptance statistics gate, control gate, verdict rules, restart subject and expectations may not change"). Process authority: docs/agents/factory.md "Evidence pack" and "Item lifecycle" step 5. Decision 6889976034 settles where decision 6703447017 and those texts disagree: ADR-021's rules apply.

Candidate selection: the parent split names one pull request for entry 5. The current plan comment is 6693074361, headed `implementation-grade plan`, posted before the #412 amendment; it counts as a contract-grade plan comment for selection and is not approved, so it is eligible for revision. Predecessors: [#393](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/393) and [#396](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/396), from the #355 branch, are merged; their merge commits `68b82dfcd2a5fb5f4daede5778565b48e5ad464f` and `90e4cc454861a9137f2451c2488772e6fc88f0a7` are ancestors of the base commit (checked by `git merge-base --is-ancestor` on 2026-11-14). The smoke run is named by decision 6703447017 point 3; this session did not read it.

## 3. Scope

Files allowed to change, and only these:

- `scenarios/B10/expected.yaml`, created with exactly the text in section 4.1.
- `items/356/state.md`, created as section 4.2 sets.

Paths that must not change: everything else, including `scenarios/B10/scenario.yaml`, `results/**`, `docs/**`, `src/**`, `tests/**`, `.github/**`, `charts/**` and every other `scenarios/*/expected.yaml`. No verdict, workflow, verifier, load generator, test, project or package change. The implementing session reads no B10 run result, artifact or log.

`scenarios/B10/expected.yaml` matches the protected `expected.yaml` pattern in `.github/CODEOWNERS`. The pull request description lists the Code Review Rules P0 on expected outcomes as kept because this approved plan requires it (docs/agents/pull-requests.md rule 8, fourth paragraph). Hari merges (decision 6703447017). The Claude review of decision 6633528258 point 2 and Codex review under GOVERNANCE "Merge rule" both apply.

Size estimate, 2026-11-14, by line count of the texts below: 2 files, about 100 lines in `expected.yaml` and about 20 in `state.md`. This is an estimate, reported, not enforced.

## 4. External contracts

### 4.1 `scenarios/B10/expected.yaml`

Create it with exactly the text below, ending in one newline. No code reads this file (`src/Parchwell.Scenarios/Loading/ScenarioLoader.cs` line 9; ADR-014). Its readers are the verdict pull request author, the control session under #347 point 2, and Hari.

```yaml
schema_version: 1
id: B10

assessment_rules:
  authoritative_verifier_counts: complete_valid_runs_only
  invalid_or_incomplete_run_counts: absent
  missing_counts_are_zero: false
  missing_counts_refute_hypotheses: false
  diagnostic_evidence: retain
  changed_expectations_require_human_approval: true
  repeats_per_configuration: 5
  replacement_repeats: 0
  rules_source: "docs/blueprint.md ADR-021, Restart decision rules 1 to 7; this file adopts them and adds none"
  judgement_contract: docs/operations/verifier-contract.md#scientific-b10-final-judgement
  projection_contract: docs/operations/b10-abrupt-contract.md#final-sweep-projection
  admissible_runs: "scientific runs only, with run IDs matching ^b10-[0-9]+-[0-9]+$, dispatched on main after this file merges; the smoke run, every diagnostic trial and the governed diagnostic run are never published as a result"
  timeout_cohort_projection: "the timeout verdict reads only the independently checked timeout cohort: each repeat's timeout counts ok, loss, phantom, reported_failure, duplicate and reorder in b10_final.repeats; probe and warm-up records never enter these counts (ADR-021, Restart decision rules, rule 1)"
  residual_risk: "A late append after the phase sweep line is not detected."
  expectations_basis: "written before any scientific B10 result exists. The timeout rule is the one recommended before smoke run 42189889469 was read (decision 6703447017 points 1 and 3). The restart rule is copied from ADR-021, whose restart decision rules entered main on 2026-11-07 and 2026-11-10, after that smoke run of 2026-11-06; how ADR-021's authors derived them was not traced. The author of this file read no B10 run result."

hypotheses:
  H_timeout_duplicate:
    question: "Does a send that times out under message.timeout.ms 100, followed by an application resend with the same key and sequence number in the same producer instance, put a duplicate record in document-events?"
    configurations: [C0, C1]
    environments: [live]
    evidence: "b10_final.timeout_rule of the C0 phase candidate; C1 timeout counts are a comparison only"
    holds_when: "all five C0 repeats are complete_valid with classification_final true, and the checked sum of their duplicate counts is at least 1"
    not_observed_when: "all five C0 repeats are complete_valid with classification_final true, and the checked sum of their duplicate counts is 0"
    otherwise: "held: fewer than five complete_valid final C0 repeats, an unavailable or overflowed sum, or a C0 candidate with b10_final null; no replacement repeats"
    absence_refutes: false
    attribution: "not attributed; duplicates are counted by logical key and sequence within the timeout cohort, so a duplicate from the application resend and one from librdkafka's own retry cannot be told apart"
    source_scope: "librdkafka v2.16.0 INTRODUCTION.md places Message persistence status under Idempotent Producer; C0 runs without idempotence, and ADR-021 Context labels the same late append without idempotence an inference"
    c1_comparison_limit: "C1 adds enable.idempotence true to C0. The four properties that idempotence adjusts already hold the adjusted values in C0: max.in.flight 5 from scenario.yaml, message.send.max.retries 2147483647 and acks -1 from the baseline catalog, and queuing.strategy fifo as the librdkafka v2.16.0 default (CONFIGURATION.md, enable.idempotence and queuing.strategy). The effective values in a run were not observed when this file was written."
    c1_inference: "librdkafka v2.16.0 states that with idempotence a message is only written to the log once (INTRODUCTION.md, Idempotent Producer, Guarantees); that this covers only librdkafka's own retries, and does not de-duplicate a new application Produce call with the same key and sequence number, is an inference from the sources' silence, not a stated fact"
  H_restart_duplicate:
    question: "With idempotence enabled, can application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message leave both the original and the resend in the log?"
    configurations: [C1]
    environments: [live]
    written_after_smoke: "yes; this hypothesis and its rules were written after smoke run 42189889469 (ADR-021 restart decision rules, main commits a6169c24 of 2026-11-07 and d1966c9d of 2026-11-10)"
    evidence: "b10_final.restart_rule of the C1 phase candidate and each repeat's restart object in b10_final.repeats"
    eligibility: "at least 3 of the 5 declared C1 repeats eligible under ADR-021, Restart decision rules, rule 2; all five outcomes, the eligible count and every ineligibility reason are published"
    qualifying_pair: "an eligible repeat whose cross_restart arm has pair_count at least 1 and unreported_seen at least 1 (ADR-021, Restart decision rules, rule 3)"
    holds_when: "at least three eligible repeats and at least one qualifying pair"
    not_observed_when: "at least three eligible repeats, no qualifying pair and no lost resend in an eligible repeat; reason no_cross_restart_duplicate_observed"
    held_when: "fewer than three eligible repeats, reason fewer_than_three_eligible; or at least three eligible repeats, no qualifying pair and a lost resend in an eligible repeat, reason eligible_loss_without_pair; or a C1 candidate with b10_final null, with the candidate's root reasons, because missing evidence fails eligibility (ADR-021, Restart decision rules, rule 2)"
    absence_refutes: false
    comparison: "per repeat, both, cross_restart_only, same_instance_only, neither or unavailable with reasons; both prevents crediting restart for same-instance duplication, and cross_restart_only does not prove causation (ADR-021, Restart decision rules, rule 5)"
    lost_resend_findings: "a lost resend in any C1 repeat, eligible or ineligible, is a separate published finding, except after a no-kill end, which produces no finding; only loss in eligible repeats affects this verdict (ADR-021, Restart decision rules, rules 3 and 4)"

known_risk:
  in_flight_timeout_validity: "librdkafka v2.16.0 INTRODUCTION.md, Idempotent Producer, Message timeout considerations: for the idempotent producer, on an in-flight timeout the broker connection will be closed by the client. No cited source states what the non-idempotent C0 producer does. ADR-021 still invalidates a repeat with non-zero txerrs or rxerrs or an error-level native error callback. Whether that close raises any of them is not verified. If it does in C0, a repeat with a possibly persisted timeout cannot be complete_valid, and the timeout verdict is held. If it does in the idempotent C1 timeout instance, that instance is not complete_valid, its repeat is ineligible with b10_restart_timeout_bookkeeping_unavailable (docs/operations/b10-abrupt-contract.md, check 18 and eligibility reason 7), and fewer than three eligible repeats hold the restart verdict."

not_claimed:
  internal_retry_state: "survival or loss of the producer's internal retry or idempotence state across the restart (ADR-021, Context and rule 7)"
  pod_or_node_loss: "the restart is a process self-kill followed by pod replacement, not pod or node loss (ADR-021, Restart decision rules, rule 7)"
  wire_inflight_state: "whether an original was on the wire at the kill; the child cannot observe it (ADR-021, Restart protocol, amendment of 2026-11-11)"
  universal_causation: "fixed arm order and differing native and broker state are confounds; no measured rate is implied (ADR-021, Consequences)"

verdict:
  verdicts: [timeout/application retry, restart recovery]
  basis: "docs/agents/factory.md Evidence pack: for B10, two labelled proposed verdicts, each selected by its own approved decision rule in ADR-021; Hari approves or rejects each separately"
  timeout_application_retry:
    label: timeout/application retry
    subject: "librdkafka's documented risk that an application retry after a timed-out, possibly persisted send risks duplication (librdkafka v2.16.0 INTRODUCTION.md, Message persistence status)"
    deciding_configuration: C0
    confirmed_when: "C0 H_timeout_duplicate holds; b10_final.timeout_rule.rule_selection confirmed"
    untestable_when: "C0 H_timeout_duplicate not observed; b10_final.timeout_rule.rule_selection untestable"
    held_when: "b10_final.timeout_rule.rule_selection held, or the C0 candidate has b10_final null; no verdict word is proposed"
    contradicted_when: "never; absence of duplicates never selects contradicted"
    may_publish_while_restart_held: true
  restart_recovery:
    label: restart recovery
    subject: "With idempotence enabled, application recovery that replaces a killed producer and resends a durably identified but unconfirmed logical message can leave both the original and the resend in the log."
    deciding_configuration: C1
    confirmed_when: "H_restart_duplicate holds; b10_final.restart_rule.rule_selection confirmed; published only with the full subject, never a bare word or K-53 confirmed"
    untestable_when: "H_restart_duplicate not observed; rule_selection untestable, selection_reason no_cross_restart_duplicate_observed"
    held_when: "rule_selection held, with selection_reason, eligible count and reasons, or the C1 candidate has b10_final null, with its root reasons; shown as held, never blank, a dash or not_tested, and never copied from the timeout verdict"
    contradicted_when: "never"
    loss_side_by_side: "a qualifying pair plus loss publishes the confirmed full subject and every lost-resend finding side by side, without waiving any gate"
  precedence: "smoke, diagnostic or non-scientific run -> no verdict; then each verdict's own rule above, independently"
  smoke_run: "scenario run 42189889469 is B10's smoke run; it is smoke only and never the verdict run"
  verdict_run: "a fresh scientific scenario run dispatched on main after this file merges"
  no_verdict_when: "smoke run, diagnostic trial or governed diagnostic run; no pooling, favourable subset or replacement repeats"
  text_must_state:
    - "Microsoft's librdkafka configuration guidance has no message.timeout.ms or idempotence row"
    - "when the C0 duplicate sum is at least 1, duplicates appeared under timeout plus application resend; in every publication, the verifier does not attribute a duplicate to the application resend rather than librdkafka's own retry"
    - "A late append after the phase sweep line is not detected."
    - "workload data is synthetic"
    - "the restart is a process self-kill followed by pod replacement, with an unknown native tail, a fixed cut position at CutOrdinal 4, a fixed probe rate of 100 originals per second and a fixed arm order, same_instance then cross_restart; these are policy values, not measurements"
    - "the restart verdict claims neither pod or node loss nor survival of internal retry state"
  decision_rules_authority: "ADR-021 Restart decision rules, adopted for B10's expected outcomes by Hari's decision 6889976034, which supersedes decision 6703447017 points 1 and 2 where they conflict; Hari's approval of this file's plan"
  decision: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034

sources:
- url: https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations
  version: current
  read_date: "2026-11-14"
- url: https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md
  version: v2.16.0
  read_date: "2026-11-14"
- url: https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md
  version: v2.16.0
  read_date: "2026-11-14"
```

SHA-256 of the file, computed by script: `35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f`.

### 4.2 `items/356/state.md`

Create the item state file under docs/agents/factory.md "Item state file", after the catch-up and before the cold review. Use exactly the text below. Replace each `<...>` placeholder with the value from GitHub, and nothing else. If the approval comment says something these lines do not cover, or a later decision exists, record it under that section's rules and list it in the PR implementation notes.

```text
# State: #356 B10 expected outcomes (#348 entry 5)
Covers: comment <ID of the newest comment on #356 at your catch-up> created <its created_at>; read at <UTC timestamp of that catch-up>
Parent items: [#348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348), [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [B10 split, handoff 6617284257 on #348](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617284257), entry 5, adopted in [decision 6617607468](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/348#issuecomment-6617607468)
Implementing tool: Codex. Source: [comment 6633653241](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633653241)
Approved plan: [B10 timeout-and-resend expectations contract-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6693074361), Revision 5, commit <SHA that Hari's approval names>
Decisions in force:
- Issue body: this child adds scenarios/B10/expected.yaml; its decision rules and verdict word go to Hari. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356)
- Adopts the #347 standing authority, points 1 to 6, the B10 split and restart decision 6617344472; decision rules and verdict words stay with Hari. Source: [decision 6617624003](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6617624003)
- Codex implements; a Claude review gates the Codex pull request before the control session merges. Source: [decision 6633535614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633535614)
- Codex sessions may run standing approval, except for a plan that needs Hari's own approval, such as this issue's. Source: [decision 6633795942](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6633795942)
- B10 decision rules, restart attribution, smoke run 42189889469 smoke only, client versions fixed in a separate change, and Hari merges the expected-outcome pull request. Source: [decision 6703447017](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6703447017)
- Two ADR-021 verdicts, superseding decision 6703447017 points 1 and 2 where they conflict; duplicate sentence option B; held for a null b10_final; check findings B1 and B2 adopted. Source: [decision 6889976034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/356#issuecomment-6889976034)
- <one line stating what Hari's approval of Revision 5 says>. Source: <Markdown link to that approval comment, in the form of the lines above>
Open questions:
- None
Next action: Hari reviews and merges the expected-outcome pull request; then a fresh scientific B10 run on main and the verdict pull request.
```

## 5. Requirements

- R1. WHEN the pull request diff is computed against `origin/main` THEN it changes exactly `scenarios/B10/expected.yaml` and `items/356/state.md`, both added.
- R2. WHEN `scenarios/B10/expected.yaml` is read THEN its SHA-256 equals the digest in section 4.1. WHEN it differs THEN the acceptance check fails and the implementer corrects the file to the approved text; it never edits the digest.
- R3. WHEN the file is parsed as YAML THEN `id` is `B10`, `hypotheses.H_timeout_duplicate.configurations` equals the configuration set of `scenarios/B10/scenario.yaml` (`C0`, `C1`), `hypotheses.H_restart_duplicate.configurations` is `[C1]`, `verdict.timeout_application_retry.deciding_configuration` is `C0` and `verdict.restart_recovery.deciding_configuration` is `C1`.
- R4. WHEN the file is read THEN `verdict.restart_recovery.subject` equals byte for byte the quoted full restart subject in docs/blueprint.md ADR-021 line 691, and `assessment_rules.residual_risk` equals `A late append after the phase sweep line is not detected.`
- R5. WHEN the file is read THEN every `selection_reason` literal it names (`fewer_than_three_eligible`, `eligible_loss_without_pair`, `no_cross_restart_duplicate_observed`) appears in docs/operations/verifier-contract.md "Scientific B10 final judgement".
- R6. WHEN `items/356/state.md` is read THEN it starts with `# State: #356 B10 expected outcomes (#348 entry 5)` and a newline, contains no `<` character, and holds the fields of factory.md "Item state file" in that order.
- R7. WHEN either new file is read THEN every character is ASCII, and `git diff --check origin/main...HEAD` reports nothing.
- R8. WHEN a check fails twice after one consolidated correction THEN the implementer stops under section 11.

## 6. Invariants and safety rules

- No expected outcome is derived from a B10 run result (AGENTS.md "Verdict integrity"; decision 6703447017 point 1, last bullet; #404 decision 6883475587, the control session's explanation).
- No rule in the file adds to, narrows or relaxes ADR-021's decision rules or protocol rule 15's fixed values.
- No `results/` or verdict file. No workflow change or dispatch. Hard safety rules 1 to 11 unchanged.
- Hari merges. No control-session merge, despite #347 point 1, because decision 6703447017 reserves it.

## 7. Test map

No code reads `expected.yaml` and the pull request adds no code, so the acceptance check in section 9 is the test. Plan rule 2's standing fixture rule does not apply: the plan adds or tightens no check in repository code, so no existing test fixture can be rejected.

| Requirement | Check in section 9 | Input | Literal expected value |
|---|---|---|---|
| R1 | `changed == {...}` | `git diff --name-only origin/main...HEAD` | `{'scenarios/B10/expected.yaml', 'items/356/state.md'}` |
| R2 | digest assertion | file bytes | `35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f` |
| R3 | YAML assertions | parsed file and `scenario.yaml` | `B10`; `{C0, C1}`; `[C1]`; `C0`; `C1` |
| R4 | subject and residual-risk assertions | file and docs/blueprint.md | the ADR-021 quoted subject; `A late append after the phase sweep line is not detected.` |
| R5 | reason-literal assertion | file and verifier-contract.md | each of the three literals present in both |
| R6 | state-file assertions | `items/356/state.md` | the header line; no `<`; the nine field prefixes in factory.md order |
| R7 | ASCII assertion and `git diff --check` | both files | no non-ASCII byte; empty output |

Boundaries: R2 is exact equality, so a one-byte change fails. R1 fails on a third file and on a missing file.

## 8. Deployment assumptions

None for this pull request: it changes no runtime file. The live assumptions of the scientific run are outside it; section 12 records them as deferred work, with how their earlier evidence was taken.

## 9. Acceptance

Run from the repository root on the pull request branch. Each command must exit 0, and the Python check must print its final line. Record the commands, commit and output in the PR implementation notes.

```sh
git fetch origin main
git diff --check origin/main...HEAD
python3 - <<'PY_CHECK'
import hashlib, pathlib, re, subprocess, yaml
approved = '35c49404219eabe5be38479703817805c19401a091256a69bf64f5959beb0c1f'
changed = set(subprocess.check_output(
    ['git', 'diff', '--name-only', 'origin/main...HEAD'], text=True).split())
assert changed == {'scenarios/B10/expected.yaml', 'items/356/state.md'}, changed
data = pathlib.Path('scenarios/B10/expected.yaml').read_bytes()
assert hashlib.sha256(data).hexdigest() == approved, 'expected.yaml differs from the approved text'
x = yaml.safe_load(data)
d = yaml.safe_load(pathlib.Path('scenarios/B10/scenario.yaml').read_text())
assert x['id'] == d['id'] == 'B10'
assert set(x['hypotheses']['H_timeout_duplicate']['configurations']) == set(d['configurations']) == {'C0', 'C1'}
assert x['hypotheses']['H_restart_duplicate']['configurations'] == ['C1']
assert x['verdict']['timeout_application_retry']['deciding_configuration'] == 'C0'
assert x['verdict']['restart_recovery']['deciding_configuration'] == 'C1'
adr = pathlib.Path('docs/blueprint.md').read_text()
subject = re.search(r'The full restart subject is: "([^"]+)"', adr).group(1)
assert x['verdict']['restart_recovery']['subject'] == subject
assert x['assessment_rules']['residual_risk'] == 'A late append after the phase sweep line is not detected.'
vc = pathlib.Path('docs/operations/verifier-contract.md').read_text()
for r in ('fewer_than_three_eligible', 'eligible_loss_without_pair', 'no_cross_restart_duplicate_observed'):
    assert r in data.decode() and r in vc, r
s = pathlib.Path('items/356/state.md').read_text()
assert s.startswith('# State: #356 B10 expected outcomes (#348 entry 5)\n')
assert '<' not in s, 'unfilled placeholder'
fields = ['# State:', 'Covers:', 'Parent items:', 'Item plan:', 'Implementing tool:', 'Approved plan:', 'Decisions in force:', 'Open questions:', 'Next action:']
lines = s.splitlines()
idx = [next(i for i, l in enumerate(lines) if l.startswith(f)) for f in fields]
assert idx == sorted(idx), 'state-file fields out of order'
assert all(ord(c) < 128 for c in data.decode() + s)
print('B10 expected-outcome scope, digest and state-file checks passed')
PY_CHECK
npm run check:docs
```

PyYAML is used only by this check; add no dependency. Fix each `npm run check:docs` warning in `items/356/state.md`, or explain it in the pull request under docs/agents/pull-requests.md rule 8; warnings in other files take one blanket line. Then run the cold review under rule 8, with its budget and exhaustion action.

## 10. Sources

Read with this session's tools on 2026-11-14; the "Guarantees" passage re-read on 2026-11-14 for Revision 5 from the same fetched text.

- librdkafka v2.16.0 [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md), fetched as raw text at tag `v2.16.0` with `curl`: "Message persistence status" (`RD_KAFKA_MSG_STATUS_POSSIBLY_PERSISTED`: "the message was transmitted to the broker, but no acknowledgement was received. Application retry will risk ordering and duplication."); "Idempotent Producer", "Guarantees" ("Exactly-once - a message is only written to the log once."); "Idempotent Producer", "Message timeout considerations" ("the broker connection will be closed by the client"). "Message persistence status" also sits under "Idempotent Producer".
- librdkafka v2.16.0 [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), same tool: `transactional.id` ("used to identify the same transactional producer instance across process restarts"); `enable.idempotence` (default `false`; adjusts `max.in.flight.requests.per.connection=5`, `retries=INT32_MAX`, `acks=all`, `queuing.strategy=fifo`); `queuing.strategy` default `fifo`; `acks` default -1.
- Microsoft [Apache Kafka client configurations for Azure Event Hubs](https://learn.microsoft.com/en-us/azure/event-hubs/apache-kafka-configurations), fetched with the Microsoft Learn `microsoft_docs_fetch` tool: the librdkafka section lists `socket.keepalive.enable`, `metadata.max.age.ms`, `request.timeout.ms`, `compression.codec`, three consumer rows, `retries` and `partitioner`, and no `message.timeout.ms` or `enable.idempotence` row. `enable.idempotence` and `delivery.timeout.ms` appear only in the Java table.
- On `main` at the base commit: docs/blueprint.md ADR-021 (lines 659 to 733); docs/agents/factory.md "Evidence pack" (line 192 and the following paragraph) and "Item lifecycle" step 5; docs/operations/verifier-contract.md lines 287 to 314; docs/operations/b10-abrupt-contract.md "Final sweep projection"; docs/operations/b10-workflow.md "Scientific execution"; `scenarios/B10/scenario.yaml` lines 10 to 18; `src/Parchwell.Kafka/Configuration/BaselineSettingsCatalog.cs` lines 103, 107 and 109; `scenarios/B07/expected.yaml` for shape.

Behaviour the file relies on, each with its source: possibly persisted retry risks duplication (INTRODUCTION.md); in-flight timeout closes the connection (INTRODUCTION.md); only `transactional.id` identifies an instance across restarts (CONFIGURATION.md); idempotence-adjusted values and defaults (CONFIGURATION.md); the Microsoft page's rows (Microsoft Learn). That idempotence does not de-duplicate a new application `Produce` call is labelled an inference in the file.

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope, including any byte of `expected.yaml`. It also stops when the acceptance check fails after one consolidated correction (`push -wip and hand off to the next session`), and when cold review exhausts rule 8's budget (handoff to Hari).

## 12. Deferred work

Each item is after merge, on the verdict path, and outside this pull request's files; none can be completed before this merge without a run or a decision this plan cannot make.

1. **Live assumptions for the scientific run.** Evidence: #404 decision 6883475587 point 2 accepts the listed live assumptions "for this one run only", the governed diagnostic run. ADR-021 amendment of 2026-11-11 records Hari's earlier acceptance for "the next diagnostic trial only". How their evidence was or will be taken (on an already-open or a fresh connection, idle or under load) is not stated in the texts this session read, and this session read no run; the request to Hari states it for each assumption. Required action: Hari accepts by name the live assumptions that still apply before a scientific dispatch. Responsible: the #347 control session, bringing it to Hari. Gate it blocks: the scientific dispatch and therefore both B10 verdicts.
2. **Governed diagnostic run.** Evidence: run 43635822755, authorized by #404 decision 6883475587 point 1, was in progress when this plan was written; this session did not read it. Responsible: the #404 sessions. Gate: the scientific dispatch, per [#404 decision 6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) point 5 as cited by decision 6883475587.
3. **Client versions.** Decision 6703447017 point 4 requires a fix before the fresh run. Evidence: [#403](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/403), "Restore B10 candidate producer versions and broker counts", merged 2026-11-07, and verifier-contract.md line 314, which records producer versions from authenticated ordinary manifests and adds `version_unavailable` when they are missing. Whether a scientific candidate carries non-null versions is not observed. The evidence pack also needs per-repeat broker connection counts (factory.md "Evidence pack", line 190), which scientific candidates omit (verifier-contract.md line 295); the verdict pull request records them as unavailable, never zero. Gate: the evidence pack's version and broker-count lines, checked in the verdict pull request.
4. **Scientific run and verdict pull request.** After this merges and items 1 to 3 clear, a fresh scientific run on `main`, then one verdict pull request with both labelled verdicts, which Hari approves or rejects separately (factory.md "Item lifecycle" step 5). Workload bound: one dispatch, five repeats per configuration, no replacement repeats (ADR-021 rule 2; scenario.yaml `repeats: 5`). Exhaustion: a held verdict is published as held with reasons, not rerun automatically (ADR-021 rule 4). The verdict pull request also needs the mutation-acceptance gate of factory.md "Evidence pack" (line 198): a passing `verifier-c5-mutation.yml` run on `main` that includes every merged change to a configured mutation target; #404 decision 6883475587 records that gate as met for #455 and #467, which says nothing about later target changes. `main` has since merged C5 mutation rebalancing pull requests #481 to #488; whether they changed configured targets was not checked here.

## 13. Plan-check answers

- **(a) Effort that scales with a runtime count.** Only the `npm run check:docs` warnings in `items/356/state.md`. Cap: 10 dispositions; exhaustion action `push -wip and hand off to the next session`. The Python check gets one run and one consolidated correction; on a second failure, `push -wip and hand off to the next session`. Cold review keeps rule 8's budget and its handoff to Hari.
- **(b) Stricter than the parent.** The `held` mapping for a null `b10_final` goes beyond ADR-021's text; decision 6889976034 point 3 accepts it. Hari's own approval (decision 6633791790 point 2) where #347 point 5 allows standing approval; Hari's merge (decision 6703447017) where #347 point 1 lets the control session merge. Both rest on recorded decisions. The rules themselves are not stricter than ADR-021; they restate it.
- **(c) Checks that depend on code, data or behaviour not on `main` or in the pull request.** None. The check reads `scenarios/B10/scenario.yaml`, docs/blueprint.md and docs/operations/verifier-contract.md on `main`, and the two new files. The field names the file cites are defined on `main` in verifier-contract.md lines 297 to 312 and b10-abrupt-contract.md "Closed output". The scientific run is outside this pull request.

## 14. Open questions for Hari

None. Decision 6889976034 answers the three questions of Revision 4.
