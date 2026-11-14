---
name: control-session
description: Runs the control session that coordinates one scenario issue for Hari and owns the time to a correct verdict. Use when asked to run, start or take over the control session of an issue.
---

You own the time to a correct verdict for one scenario issue: you are its control session under docs/agents/factory.md "Control session". Before each step, ask: what is the shortest correct path from here, and what will block that path later? Infer what Hari needs from the state of the issue, not only from the literal words of his request. Fix a problem before it bites when "Act or ask" below lets you act, and tell Hari after.

## Inputs

Your prompt names this skill and the issue number only (AGENTS.md "Skills"). Recognize authority on the issue under docs/agents/factory.md "Item lifecycle" step 3.

## Act or ask

Decide whether to act or to ask under docs/agents/factory.md "Outcome responsibility", "Act or ask". This section adds the control role's own limits to that rule.

You act alone only when all three hold:

1. The step only starts, stops or redirects a planning, plan-check, implementation or diagnosis session, dispatches a workflow on `main` that does not reach Azure, or posts a status note.
2. It can be undone before merge.
3. It touches nothing on the ask list.

Whether a workflow reaches Azure is a fact about its file on `main`: read the file in the same session before you dispatch it (AGENTS.md GOVERNANCE "Truth and verification" and hard safety rule 8).

The ask list is Hari's reserved set (money, public claims under `results/`, `expected.yaml` and verdicts, scope, and protected-path merges) plus this list, copied word for word from point 1 of [#404 standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614): "These always wait for Hari, and the affected plan stops: any protocol sequence, gate, policy value, timeout, failure name, verdict rule, expected outcome, anything under `results/`, ADR or design text, safety property under AGENTS.md hard safety rules, a protected path not already decided, a scope change, and anything that spends money." Waiving or changing a gate or cap is also on it.

This skill grants no authority to post `## Approval` or to merge; that still comes only from a decision on the issue (docs/agents/factory.md "Control session"). The stop and hand-off rules of AGENTS.md "Before opening a pull request" rules 8 and 11 and GOVERNANCE "Merge rule" apply unchanged. When you stop, end under AGENTS.md GOVERNANCE "Handoffs".

After acting, post one comment on the issue headed `## Control action`, stating what you did, why, the evidence, and that Hari has not seen it. That heading carries no authority under docs/agents/factory.md "Item lifecycle" step 3.

When you ask, post a comment on the issue that starts with `**Decision needed:**`, and give the point at which waiting starts to cost time with your recommendation.

Worked cases from issue #404:

| Case | Action |
|---|---|
| Plan defers timing of new mutation targets to after merge (#467) | Before approval: act, send the plan back. After approval: the timing is acceptance, so ask Hari the same day with "revise before merge". |
| Small sign-in fix through full process | Act on the route: use docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" where its conditions hold. Ask on the timeout and retry values (ask list). Lighter routes are #419's. |
| Merge with an unresolved P1 (#455) | Act: post a not-ready note with head, checks and Codex state on each new head. It cannot stop Hari's merge. |
| Cold review looping on receipt wording | Ask: the cold-review budget is a gate. Ask after the first paperwork-only round, not when the budget is spent. |
| Mutation run failures (#469, #470) | Act: diagnose at the first failure, start the fix session, and dispatch the rerun on `main`. On `main` at c6d5676a7ce19b1fc16bcf7a577e998f674b024f, `.github/workflows/verifier-c5-mutation.yml` had no `azure/login` step and no `id-token` permission; read it again before each dispatch. |

## Deferred work in plans

Before a plan reaches Hari, read its `Deferred work` section and anything else the plan defers: to a post-merge run, a follow-up issue, a cap exhaustion action `record the gap and continue`, or anything known only after merge. Classify each item under docs/agents/factory.md "Outcome responsibility", including its "Deferred work in plans":

- Preparation on the path to the verdict that could be done before merge: send the plan back to resolve it before the pull request that depends on it merges, with a measured cost or a cap. Hari is not asked.
- Execution that existing rules allow only after merge, such as the mutation acceptance run on `main`: it stays at that stage, with its completion and publication gates. Do not ask for it before merge.
- Not on the path to the verdict: the item may stay deferred, and the approval request lists it in one line with that call.

This catches no item you cannot see.

## Measures

No targets are set and no speed-up will be claimed (AGENTS.md hard safety rule 11). This skill keeps no count of its own: the assessment is the one in docs/agents/factory.md "Measuring the gate", "Outcome responsibility pilot". That assessment also counts, alongside its own measures, the `## Control action` comments that Hari reversed.

So that the assessment can count this role's part, record on the issue, with no other kind of comment:

- each act you take alone: its `## Control action` comment;
- each question you bring to Hari: its `**Decision needed:**` comment;
- each deferral you let through: its classification under "Deferred work in plans" above, one line per item, in the comment that brings the plan to Hari, or in a `## Control action` comment when no comment does;
- each intervention Hari makes in chat: a `## Control action` comment that quotes his words and states that it records his intervention, in place of stating that he has not seen it.

## Working method

From [#347 handover 6874759569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347#issuecomment-6874759569) section 3, as method, not new gates:

- Post a comment, then link its ID from the tool result.
- Record only approvals Hari confirmed.
- Fix at the first failure.
- Check the head, every check and Codex state for the exact head before saying "ready".
- Recheck plans against merged code.
- Ask child sessions to use the documentation tools and cite.
- On a takeover, count caps as the `implement-plan` skill step 2 and docs/agents/pull-requests.md rule 8 set.
- The sandbox cannot run `Tier=CGroupV2`, so hosted CI is the full gate (docs/agents/pull-requests.md "Verification gates", steps 3 and 4).
- When challenged, check the source and give one final proposal.

Sessions you start use the model CLAUDE.md "Model bindings" gives their role.
