@AGENTS.md

# CLAUDE.md

Claude Code entry point. The first line imports AGENTS.md, which holds every tool-neutral rule. Do not repeat AGENTS.md rules here; this file holds only Claude-specific bindings.

## Model bindings

Status: approved by Hari on 2026-10-12. Each workflow pins its model with `claude_args: --model <id>`.

| Role | Model |
|---|---|
| Plan drafting | `claude-opus-5-5` |
| Implementation | `claude-sonnet-5-5` |
| Self-review against docs/engineering/ before opening a pull request | `claude-opus-5-5` |

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

- Research and design subagents run on Opus; mechanical work (bulk edits, renames, checks) on Sonnet. In workflows, pin the exact identifiers from the model bindings table (`claude-opus-5-5`, `claude-sonnet-5-5`). In interactive sessions the subagent tool accepts only the aliases `opus` and `sonnet`, which select the latest model of that family; set one explicitly on every spawn and record the resolved model when it is reported.
- A subagent's output is unverified input. Check its claims against their sources before anything built on them is committed.

## Handoffs

Start both handoff and completion comments under AGENTS.md GOVERNANCE "Handoffs" with `**Decision needed:**`. For completion, follow the marker with `None;` and the completion report required there. This keeps the report recognizable by the Stop hook.

A Stop hook is a script Claude Code runs each time the agent finishes responding (https://code.claude.com/docs/en/hooks). The one in `.claude/settings.json` looks for that comment on the session's issue or on its branch's open pull request. The comment counts only if the marker is the first text in its body and it was created after the session started. A SessionStart hook records the start time. Both hooks act only when `GITHUB_ACTIONS=true` or the session was launched with `PARCHWELL_HANDOFF=enforce`. While the comment is missing, the Stop hook blocks the stop and tells the agent what to post. After three blocks in one session, it lets the session stop with a warning. It also lets the session stop with a warning when it cannot check, or when it finds more than one marked comment.

Whether `claude-code-action` loads the hook is not yet verified (issue #31). Tests: `.claude/hooks/tests/require-handoff.test.sh` (issue #29).

## Attribution

AGENTS.md "Implementer and reviewer" defines the commit trailer and bans session links and generated-by footers. Claude Code's trailer is `Co-Authored-By: Claude <model name> <noreply@anthropic.com>`.
