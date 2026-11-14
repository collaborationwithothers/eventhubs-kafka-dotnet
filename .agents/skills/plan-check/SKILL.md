---
name: plan-check
description: Independently checks a plan, a contract-grade plan file or an item plan comment, before it is approved. Use when asked to check, review or verify a plan.
---

You are the independent plan check of docs/agents/factory.md "Item lifecycle" step 1. You did not draft the plan.

## Inputs

The inputs are the issue number, plus the plan file path and commit SHA when a planning session spawns you under docs/agents/factory.md "Item lifecycle" step 3, or the words "standing approval" when you run as a subagent under its "Standing approval". With a path and SHA, check the plan file at that commit. Otherwise derive the candidate from the issue under docs/agents/factory.md "Item lifecycle" step 3, including its candidate-selection and handoff rules.

Apply factory.md "Item lifecycle" step 3 for authority, decision recognition, decisions linked from elsewhere and approval recognition. Its decision and approval markers are conventions, not proof: all comments post as Hari's account.

## Steps

Apply docs/agents/factory.md "Outcome responsibility" to this role.

1. Read the selected plan comment and the item state file, and catch up on the issue under factory.md "Authority reads", using its plan-check reading list, including any parent items for a child issue. Use reader subagents at high effort, under factory.md "Authority reads" "Reader subagents". Done when the initial read and snapshot are complete, and candidate selection under factory.md "Item lifecycle" step 3 is complete.
2. Read every file and source the plan changes or cites, at the version it names. Do not build, run or test code, except the dry run that factory.md "Item lifecycle" step 1 allows. For each file the plan changes, read it on `main` at each place the plan changes it, under factory.md "Item lifecycle" step 1. Done when each one is read and each change is checked against the text around it.
3. Check the selected plan against factory.md "Plan template" for an item plan, or "Item lifecycle" step 3 for a contract-grade plan, including its deployment-assumption list under step 1. Check every claim against its source. The rules 1, 2 and 4 that factory.md "Item lifecycle" step 1 applies to plan text are in docs/agents/pull-requests.md. For a contract-grade plan, check also that each rule under the plan-pr skill's "Plan rules" is met where the plan has the case it names. An applicable rule that is missing or not met is a blocking finding. For every plan, judge whether each pull request is one coherent, reviewable piece under factory.md "Item lifecycle" step 3 "Split by scope"; a bundle that is not is a blocking finding. Done when every field or requirement, every claim and every applicable plan rule has a verdict with evidence.
4. Answer the three plan-check questions in factory.md "Item lifecycle" step 1. Done when each answer cites the plan line and the evidence.
5. Refresh authority under factory.md "Authority reads". Repeat steps 1 to 4 for work affected by a change. Done when the check reflects the current candidate and authority.
6. Post the findings and the three answers as one comment on the issue, using the check-comment format in factory.md "Item lifecycle" step 1. Include the `Blocking findings:` line that format requires. Quote the prompt you received, as factory.md "Item lifecycle" step 3 requires. For a contract-grade plan file, name the plan file path and commit SHA you checked; for a plan held as a comment, identify it as that format requires. Mark a blocking finding `apply in revision` only as factory.md "Item lifecycle" step 1 allows, with what that step requires for it.
7. When a planning session spawned you, or your prompt includes the words "standing approval", return the comment link to the calling session. Otherwise hand off under AGENTS.md GOVERNANCE "Handoffs", asking Hari to approve or reject the checked plan in light of the findings and the three answers.
