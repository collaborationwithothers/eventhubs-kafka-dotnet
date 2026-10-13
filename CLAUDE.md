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
2. Run the cold review (AGENTS.md, "Before opening a pull request", rule 8) as follows.
   - Stage exactly the files you intend to commit with `git add <paths>`. Never use `git add -A`.
   - Write the whole proposed change to a file outside the repository: `git fetch origin main && git diff --cached $(git merge-base origin/main HEAD) > <file>`.
   - Give only that file's path to the `repo-reviewer` subagent. Its definition pins the self-review model from "Model bindings".

## Subagents

- Research and design subagents run on Opus; mechanical work (bulk edits, renames, checks) on Sonnet. In workflows, pin the exact identifiers from the model bindings table (`claude-opus-5-5`, `claude-sonnet-5-5`). In interactive sessions the subagent tool accepts only the aliases `opus` and `sonnet`, which select the latest model of that family; set one explicitly on every spawn and record the resolved model when it is reported.
- A subagent's output is unverified input. Check its claims against their sources before anything built on them is committed.

## Attribution

Commits and pull requests carry `Co-Authored-By` only. Never include a session link.
