---
name: plan-pr
description: Writes the contract-grade plan for one pull request, or one ordered set of pull requests, of an issue as a file on the plan branch, gets it checked, and posts a pointer comment on the issue. Use when asked to plan a pull request or an issue scope before implementation.
---

You are the planning session of docs/agents/factory.md "Item lifecycle" step 3. The plan file on the plan branch and its pointer comment are your whole output; `main` stays as it is.

## Inputs

The only input is the issue number, plus the words "standing approval" when you run as a subagent under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". Derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account. Record a decision Hari gives in your chat under the implement-plan skill's "Decisions given in chat".

## Steps

1. Read the item state file and catch up on the issue under factory.md "Authority reads", using its planning reading list, including any parent items for a child issue. Use reader subagents at high effort, under factory.md "Authority reads" "Reader subagents". Done when the initial read and snapshot are complete, the authorities are listed in creation order, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Name the selected pull request, or give a short name to the set of pull requests of a split under factory.md "Item lifecycle" step 3 "Split by scope", and retain the name for a revision; and read on `main` every file the plan will change. Done when each change you plan has its current text in hand.
3. Write the plan to factory.md "Item lifecycle" step 3's definition of contract-grade, with a split under its "Split by scope" where the work is more than one coherent piece, as the file and on the plan branch that step names. Before each push to the plan branch and each post or edit of the plan comment in this and later steps, refresh authority under factory.md "Authority reads", and recompute candidate selection and the plan for every addition, edit or deletion. Then push it. Plan text follows docs/agents/pull-requests.md rules 1, 2 and 4, under factory.md "Item lifecycle" step 1. Done when the file is pushed, every part of that definition is met, each rule under "Plan rules" that applies is met, each plan-check question in step 1 has an answer, and every point the authorities leave open is under "Open questions for Hari", which states `None` when nothing is open.
4. Post the plan comment that factory.md "Item lifecycle" step 3 describes, or edit the selected plan comment for a revision. Head it exactly `<PR name> contract-grade plan`, with the short name from step 2. Read it back as that step requires.
5. Unless your prompt includes the words "standing approval", get the plan check that factory.md "Item lifecycle" step 3 describes: spawn the `plan-checker` agent in Claude Code, or the `plan_checker` agent in Codex, with only the issue number, the plan file path and the pushed commit SHA. Treat its findings under factory.md "Item lifecycle" step 1, revising the plan and its plan comment where that step requires, and checking again only where it requires a new check. Done when the newest check covers the current revision under factory.md "Item lifecycle" step 1. Run at most two fresh checks per plan, counting every fresh check of any revision of it, including those under "Standing approval". When the second still reports a blocking plan-text finding, run no third rewrite-and-check: list each remaining finding whose fix changes only plan text and is not design-changing under factory.md "Item lifecycle" step 1 as `apply in implementation` with its exact replacement text, and put each other finding, open question and deployment assumption in the same list for step 6. The label in that handoff takes its authority from Hari's adopting approval, not from step 1.
6. Hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to review the plan. After the cap in step 5, send one batched handoff: the plan-text findings with their replacement text, which the implementer applies only when Hari's approval adopts them, and the design findings, open questions and deployment assumptions for his decision. When your prompt includes the words "standing approval", return the plan link to the calling session instead.

## Plan rules

Each rule applies when the plan has the case it names. The plan-check skill verifies them.

1. **External behaviour.** Verify any Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour the plan relies on against official documentation, with the documentation tools your session has, under AGENTS.md GOVERNANCE "Truth and verification". The plan names the tool used and cites the source for each behaviour. With no documentation tool, list each such behaviour under "Open questions for Hari" as unverified.
2. **Existing tests.** When the plan adds or tightens a check, it includes a standing fixture rule: when an existing test's fixture is rejected by a new check, the implementer does exactly one of:
   - (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
   - (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

   The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. Model the rule on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.
3. **Live assumptions.** Each deployment assumption under factory.md "Item lifecycle" step 3 states how its evidence was taken: on an already-open or a fresh connection, and idle or under load.
4. **Size estimate.** For each pull request, estimate the changed lines and files, labelled an estimate under AGENTS.md hard safety rule 11. Size is reported, not enforced: no session trims code to meet a number.
5. **Base commit.** The plan names the full SHA of the `main` commit it was written against.
