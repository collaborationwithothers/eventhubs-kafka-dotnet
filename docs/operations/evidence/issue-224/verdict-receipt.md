# Trace batch verdict receipt

This receipt records validation of the trace batch verdict reports for issue #224. Nothing ran for these items.

## Sources

- Plan: [Trace batch implementation-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438840070), `Revision: 2.`, PR 2. It was approved under standing approval by [plan check 6442677677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442677677).
- Decisions, in creation order: [6438353573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438353573), [amendment 6442095091](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442095091), [6442153525](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442153525), and Hari's decision records from the session chat, [6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624), [6450806971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450806971) and [6452561082](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6452561082).
- Base: `main` at `fd49816`, the merge of [PR #227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/227), which carries the thirteen write-ups.
- K09 variant: B. `origin/main:infra/cluster/aks.tf` has no `local_dns` text, because #225 has not merged.
- The plan script was taken unmodified from the posted plan comment and saved outside the repository.

## Mutation publication gate

The newest completed run of `verifier-c5-mutation.yml` on `main` is [40415248275](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40415248275), conclusion success, `head_sha` `7b6834c5e8668e89376597bc2b524c0725326daf`. No `mutate` target in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` changed between that commit and `origin/main`. No verifier judges a traced item. The gate applies because docs/agents/factory.md "Evidence pack" lets a missing, failed or incomplete latest applicable run block publication.

## Validation

All commands ran in the cloud session workspace on 2026-10-27, on branch `claude/issue-224-trace-batch-verdict` based on `origin/main`. The files were staged and not yet committed.

- `python3 trace_224.py verdict --write` exited 0 and printed `mutation gate: run 40415248275 at 7b6834c5e8668e89376597bc2b524c0725326daf` and `trace batch verdict write: ok`. It ran once, writing the thirteen reports dated 2026-10-27.
- `python3 trace_224.py verdict --check` exited 0 and printed the same gate line and `trace batch verdict check: ok`.
- `git diff --cached --check --merge-base origin/main` exited 0 and printed nothing.
- `npm run check:docs` exited 0. It reported the same 8 warnings as on `main`, all in files this pull request does not change.

## Review edits

Edits to the written reports, before the first commit:
- Plan check P2 marked `apply in implementation`: the H01 report's Authority line now also links decision (amendment) 6442095091, which added H01 to the batch.
- Found in the session's own sweep: the K06 to K10 reports now name section 12 "Inventory amendment 1", where those rows sit, instead of "Frozen inventory". It is the same correction PR #227 made in the factory.md traced-item rule.

## Review

The cold review result and its dispositions are recorded in the pull request description.
