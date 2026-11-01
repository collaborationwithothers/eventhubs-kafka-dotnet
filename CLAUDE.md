@AGENTS.md

# CLAUDE.md

Claude Code entry point. The first line imports AGENTS.md, which holds every tool-neutral rule. Do not repeat AGENTS.md rules here; this file holds only Claude-specific bindings.

## Model bindings

Status: approved by Hari on 2026-10-12. The pull request for issue #286 amends this section (spawned sessions, and the Haiku row); Hari's merge of that pull request is the approval of the amendment. Each workflow pins its model with `claude_args: --model <id>`.

| Role | Model |
|---|---|
| Plan drafting | `claude-opus-5-5` |
| Implementation | `claude-sonnet-5-5` |
| Self-review against docs/engineering/ before opening a pull request | `claude-opus-5-5` |
| Tool-verified mechanical work (task list below) | `claude-haiku-5-5` |

The Haiku row covers only these tasks, and only when a deterministic tool or script verifies the output afterwards: running the repository's checks and reporting their output; byte-equality checks between workflow files and their committed copies; ASCII and punctuation scans; counts and totals computed by script; fetching run logs and artifacts; polling run status. Work that needs judgement, or whose output no tool verifies, stays on Sonnet or Opus under the other rows. The Haiku row is a governance policy choice by Hari, decided on 2026-11-01 without measurement; it is not a measured result (AGENTS.md hard safety rule 11).

A session started with the `create_session` tool sets `model` to the identifier in this table for the role it will perform. Without `model`, the tool gives the new session the calling session's model, so an Opus session would start an implementation session on Opus. Source: the `model` parameter description of the `create_session` tool as it appeared in one cloud session, read 2026-11-01: "Model ID for the new session. Defaults to the calling session's model." It is tool metadata, not official documentation. No official page on `create_session` was read.

Codex's review model is set in Codex's own settings, not in this repository.

## Identity

In GitHub Actions, Claude acts through the factory's GitHub App, using a token minted per run with `actions/create-github-app-token`. Branch names start with `claude/` and include the issue number.

## Before opening a pull request

AGENTS.md's "Before opening a pull request" applies in full. In addition:

1. Run the full local verification suite appropriate to the change: build and tests for runtime changes, documentation checks for documentation-only changes. Warnings are errors for builds; documentation warning dispositions follow AGENTS.md "Before opening a pull request" rule 8. Verification repetition follows that section's rule 13.
2. In an interactive session, run the cold review (AGENTS.md, "Before opening a pull request", rule 8) as follows. The steps for GitHub Actions are not written yet (issue #27).
   - Stage exactly the files you intend to commit with `git add <paths>`. Never use `git add -A`.
   - Write the review diff defined by AGENTS.md "Before opening a pull request" rule 8 to a file outside the repository: `git fetch origin main && git diff --cached --merge-base origin/main -- ':(top)' > <file>`. Prepare the authored receipt required by that rule, including the review mode, revisions and specification sources. For focused review, include the changes since the last reviewed revision and supply the full PR diff as context under that rule. Check that the diff command succeeded before going on.
   - Give only the review-diff path (including the focused change diff when applicable) and authored receipt path to the `repo-reviewer` subagent. Spawn it without a `model` parameter, as the one exception to "Subagents" below. A per-invocation `model` overrides the `claude-opus-5-5` pin in its definition (https://code.claude.com/docs/en/sub-agents, "Choose a model"). Record the resolved model, as "Subagents" requires.

## Subagents

- Research and design subagents run on Opus. Mechanical work that needs judgement or that no tool verifies (bulk edits, renames) runs on Sonnet. Mechanical work in the Haiku row's task list, verified by a tool afterwards, runs on Haiku. Pin the exact identifiers from the model bindings table (`claude-opus-5-5`, `claude-sonnet-5-5`, `claude-haiku-5-5`) wherever a full ID is accepted: `--model`, a subagent definition's `model` frontmatter, or `CLAUDE_CODE_SUBAGENT_MODEL`. An Agent tool call cannot pass a full ID (the sub-agents page, "Choose a model"); there its `model` parameter accepts the aliases `sonnet`, `opus`, `haiku` and `fable` (observed in the tool definition on 2026-11-01; the official page lists the same four, https://code.claude.com/docs/en/sub-agents, "Choose a model"). An alias resolves to the version it points to, except when the main conversation's model belongs to the same family: then the subagent runs on the main conversation's exact model (same page). An Opus session that passes `opus` therefore gets its own Opus version. Set one explicitly on every spawn and record the resolved model when it is reported.
- A subagent's output is unverified input. Check its claims against their sources before anything built on them is committed.

## Handoffs

Start both handoff and completion comments under AGENTS.md GOVERNANCE "Handoffs" with `**Decision needed:**`. For completion, follow the marker with `None;` and the completion report required there. This keeps the report recognizable by the Stop hook.

A Stop hook is a script Claude Code runs each time the agent finishes responding (https://code.claude.com/docs/en/hooks). The one in `.claude/settings.json` looks for that comment on the session's issue or on its branch's open pull request. The comment counts only if the marker is the first text in its body and it was created after the session started. A SessionStart hook records the start time. Both hooks act only when `GITHUB_ACTIONS=true` or the session was launched with `PARCHWELL_HANDOFF=enforce`. While the comment is missing, the Stop hook blocks the stop and tells the agent what to post. After three blocks in one session, it lets the session stop with a warning. It also lets the session stop with a warning when it cannot check, or when it finds more than one marked comment.

Whether `claude-code-action` loads the hook is not yet verified (issue #31). Tests: `.claude/hooks/tests/require-handoff.test.sh` (issue #29).

## Attribution

AGENTS.md "Implementer and reviewer" defines the commit trailer and bans session links and generated-by footers. Claude Code's trailer is `Co-Authored-By: Claude <model name> <noreply@anthropic.com>`.
