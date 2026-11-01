# Issue 286 receipt

This receipt records the amendment of CLAUDE.md for issue 286. It binds models for sessions started with `create_session`, corrects the subagent alias sentence, and adds a Haiku role. No other file changes. Hari merges it, because CLAUDE.md is a protected path.

Source commit for the commands below: `origin/main` at `08f081c` plus the commits of this branch.

## Task brief and authorization

No approved item plan exists. Hari directly authorized this governance change in an interactive session on 2026-11-01, as the session prompt and [issue 286](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/286) record. Origin: two implementation cloud sessions were started from an Opus session without `model`, inherited `claude-opus-5-5` and broke CLAUDE.md "Model bindings". Hari's decisions:

1. A `create_session` call sets `model` from the Model bindings table, citing the tool's own default and its kind of source.
2. Correct the "Subagents" alias sentence if the Agent tool accepts more aliases.
3. Add a `claude-haiku-5-5` row for tool-verified mechanical work, named task list, stated as a policy choice without measurement, with the Sonnet-or-Opus fallback for judgement work, the "Subagents" sentence made consistent, and the status line kept accurate.

Limit: change only CLAUDE.md, this receipt and the self-review record. Dispatch no workflow.

## Execution card

- Deliverable: the three CLAUDE.md changes above.
- Authorization: the brief above; no plan approval is claimed.
- Mandatory gates: `npm run check:docs`; cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review. CLAUDE.md is protected, so only Hari merges.
- Verification commands: see "Commands and results".
- Publication checkpoint: open a draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; exhaustion action is its review-budget handoff to Hari.

## Predicate map

Not applicable; no verifier predicate changes.

## Sources and observations

| Claim in CLAUDE.md | Source | Kind | Date |
|---|---|---|---|
| `create_session` defaults `model` to the calling session's model | The `model` parameter of the `create_session` tool definition in this session: "Model ID for the new session. Defaults to the calling session's model." | Tool metadata of the harness, not official documentation | 2026-11-01 |
| No official documentation of `create_session` | I did not find any; I read only the sub-agents page, which covers the Agent tool, not `create_session` | Absence of evidence, stated as such | 2026-11-01 |
| The Agent tool's `model` parameter accepts `sonnet`, `opus`, `haiku`, `fable` | Observed in the Agent tool definition in this session (enum of those four). The sub-agents page, "Choose a model", says the per-invocation parameter "takes a model alias: `sonnet`, `opus`, `haiku`, or `fable`" (https://code.claude.com/docs/en/sub-agents, fetched 2026-11-01, first 100000 of 116219 characters read) | Tool definition plus official documentation | 2026-11-01 |
| An alias selects the latest model of its family | Carried over unchanged from the earlier sentence; I did not re-verify it in this change | Inherited, unverified here | 2026-11-01 |
| Haiku tier is a policy choice without measurement | Hari's decision in the task brief | Hari's decision | 2026-11-01 |

The previous sentence said the tool "accepts only the aliases `opus` and `sonnet`". That was stale: the tool definition lists four.

## Commands and results

| Command | Literal result |
| --- | --- |
| `npm ci --ignore-scripts && npm run check:docs` | `8 warnings`, none in a changed file (CLAUDE.md and this receipt report no issues). Blanket line: the 8 baseline warnings are in unchanged files |
| `grep -nP '[^\x00-\x7F]' CLAUDE.md docs/operations/evidence/issue-286/receipt.md` | no output (ASCII only) |
| `git diff --check` | no output |
| `git diff --name-only origin/main...HEAD` | `CLAUDE.md`, `docs/operations/evidence/issue-286/receipt.md` |

The cold-review record is in the pull request description.
