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

1. Run the full build and test suite locally in the session. Warnings are errors.
2. Review the diff with the self-review model, using the `code-review` skill. Fix findings before opening the pull request; do not list them as known issues.

## Subagents

- Research and design subagents run on `opus`; mechanical work (bulk edits, renames, checks) on `sonnet`. Set the model explicitly on every spawn.
- A subagent's output is unverified input. Check its claims against their sources before anything built on them is committed.

## Attribution

Commits and pull requests carry `Co-Authored-By` only. Never include a session link.
