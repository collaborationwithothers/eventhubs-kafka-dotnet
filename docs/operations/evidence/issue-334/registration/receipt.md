# Idle-connection observation registration

This pull request registers the approved idle-connection observation question in the v1 inventory. It adds no experimental evidence and leaves the investigation for later planning.

## Execution card

- Deliverable and authorization: [registration plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6600210556), Candidate and authority, Exact changes and Acceptance; [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6600267391).
- Source commit: `69102fe874698ab16839fe789b0bb54f158d535a`.
- Implementer: Codex; model and effort not exposed to the authoring session. Planning, plan checking and cold-review subagents use high effort.
- Mandatory gates and publication checkpoint: docs/agents/pull-requests.md rules 3, 5, 7, 8, 9 and 10; AGENTS.md GOVERNANCE "Merge rule"; docs/agents/factory.md "Escalation". Publish after the plan's acceptance commands and applicable cold-review coverage. Hari merges the protected blueprint path.
- Review budget and exhaustion action: docs/agents/pull-requests.md rule 8 "Cold-review protocol and cumulative budget". Other finding dispositions follow the plan's Bounded work section and AGENTS.md rules 11 and 12.
- Predicate-to-test map: Not applicable: documentation registration only; no verifier predicates change.

## Specification comparison

[Decision point 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315) maps to the blueprint introduction and section 12 amendment. The new A07 row records the issue's question; existing item rows, classifications, verdicts and expected outcomes are unchanged. Decision points 1 and 3 through 7 remain authority for the investigation and are indexed in `items/334/state.md`.

No new hypothesis, verdict, capture implementation or live run is included. The original batch [41534423570](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41534423570) and its candidate and pod artifacts were read as background evidence, along with the [silent-drop decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596426514) and [keepalive-timer decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596527645). They are not adopted as authority for new findings or claims in this PR.

## Verification

- `npm ci --ignore-scripts`: completed successfully using the lockfile; npm reported the existing glob deprecation notice.
- `npm run check:docs`: completed successfully; 8 warnings, all in unchanged files. Those pre-existing warnings are outside this PR; the changed files have none.
- `git diff --check`: exit 0, no output.
- The exact Python command in the plan's Acceptance section: exit 0; verified 76 existing inventory rows plus one observation, total 77, byte-for-byte restoration of the base blueprint outside the two edits, and exactly the three allowed paths.
- Verbatim comparison: the new block and introduction match the plan. State links and timestamps were checked against the saved issue, plan and independent-check REST responses.
- Allowed paths: `docs/blueprint.md`, `items/334/state.md` and this receipt. The source commit for these checks is recorded above; no runtime behavior changed.

## Review record

Review mode: initial comprehensive pass (round 1). Input revision: staged registration diff R1 against the source commit above. Before this review, cumulative completed counts are 0 comprehensive and 0 focused. The complete report and dispositions belong in the PR self-review record. The review input is the staged diff against the source commit above, including this receipt. Complete reports and final-head hosted review/CI evidence will be linked from the PR description; no final review report is added to its own reviewed receipt.
