# Trace batch verdict receipt

This receipt records validation of the trace batch verdict reports for issue #224. Nothing ran for these items.

## Sources

- Plan: [Trace batch implementation-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438840070), `Revision: 2.`, PR 2. It was approved under standing approval by [plan check 6442677677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442677677).
- Decisions, in creation order: [6438353573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438353573), [amendment 6442095091](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442095091), [6442153525](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442153525), and Hari's decision records from the session chat, [6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624), [6450806971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450806971), [6452561082](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6452561082) and [6453763954](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6453763954), which removes K09.
- Base: `main` at `fd49816`, the merge of [PR #227](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/227), which carries the thirteen write-ups.
- K09 variant: B. `origin/main:infra/cluster/aks.tf` has no `local_dns` text, because #225 has not merged.
- The plan script was taken unmodified from the posted plan comment and saved outside the repository.

## Mutation publication gate

The newest completed run of `verifier-c5-mutation.yml` on `main` is [40415248275](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40415248275), conclusion success, `head_sha` `7b6834c5e8668e89376597bc2b524c0725326daf`. No `mutate` target in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json` changed between that commit and `origin/main`. No verifier judges a traced item. The gate applies because docs/agents/factory.md "Evidence pack" lets a missing, failed or incomplete latest applicable run block publication.

## Validation

All commands ran in the cloud session workspace on 2026-10-27, on branch `claude/issue-224-trace-batch-verdict` based on `origin/main` `fd49816`. The first four results below ran with the reports staged and not yet committed. The checks after the K09 removal ran on commit `55aa24f` with the deletion staged, before commit `9cebf3e`, and again after the round 2 receipt edits, with those staged.

- `python3 trace_224.py verdict --write` exited 0 and printed `mutation gate: run 40415248275 at 7b6834c5e8668e89376597bc2b524c0725326daf` and `trace batch verdict write: ok`. It ran once, writing the thirteen reports dated 2026-10-27. K09's report was later removed (see `## Review edits`).
- `python3 trace_224.py verdict --check` exited 0 and printed the same gate line and `trace batch verdict check: ok`. After the K09 removal, it ran on the copy of the script with K09 taken out of `IDS` but kept in `D_SET`, as the removal rule says. It again exited 0 with the same two lines. `git diff --cached --check --merge-base origin/main` and `npm run check:docs` were rerun at the same points, with the same results: nothing, and the same 8 warnings.
- `git diff --cached --check --merge-base origin/main` exited 0 and printed nothing.
- `npm run check:docs` exited 0. It reported the same 8 warnings as on `main`, all in files this pull request does not change.

## Review edits

Edits to the written reports, before the first commit:
- Plan check P2 marked `apply in implementation`: the H01 report's Authority line now also links decision (amendment) 6442095091, which added H01 to the batch.
- Found in the session's own sweep: the K06, K07, K08 and K10 reports (and K09's, before its removal) now name section 12 "Inventory amendment 1", where those rows sit, instead of "Frozen inventory". It is the same correction PR #227 made in the factory.md traced-item rule.

Removal under decision 6, after Hari disputed K09 on PR #228:
- `results/K09/2026-10-27.md` is deleted. K09's section 12 reason and its merged write-up describe a `Disabled` pin that [#225 decision 6452847121](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/225#issuecomment-6452847121) reversed to `Preferred`. Hari adopted that decision on #224 in [decision record 6453763954](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6453763954). The #225 pull request corrects the K09 write-up and row, and K09's verdict follows in its own pull request.

## Review

The cold review result and its dispositions are recorded in the pull request description.
