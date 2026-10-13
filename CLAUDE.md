@AGENTS.md

# CLAUDE.md

Claude Code entry point. The first line imports AGENTS.md, which holds every tool-neutral rule. Do not repeat AGENTS.md rules here; this file holds only Claude-specific bindings.

## Model bindings

Status: approved by Hari on 2026-10-12; the cold-review row changed on 2026-10-13 and is approved by merging the pull request that changed it. Each workflow pins its main model with `claude_args: --model <id>`; the cold reviewer's model is set in its definition's `model` field.

| Role | Model |
|---|---|
| Plan drafting | `claude-opus-5-5` |
| Implementation | `claude-sonnet-5-5` |
| Cold review before every push (`repo-reviewer` subagent) | `claude-opus-5-5` |

Codex's review model is set in Codex's own settings, not in this repository.

## Identity

In GitHub Actions, Claude acts through the factory's GitHub App, using a token minted per run with `actions/create-github-app-token`. Branch names start with `claude/` and include the issue number.

## Before opening a pull request

AGENTS.md's "Before opening a pull request" applies in full. In addition:

1. Run the full build and test suite locally in the session. Warnings are errors.
2. For the cold review (AGENTS.md, "Before opening a pull request", rule 8), write the diff to a file and give only that path to the `repo-reviewer` subagent (`.claude/agents/repo-reviewer.md`). Repeat after each fix until it reports `Findings: none at P0 or P1`, within the three-round limit in that rule.

## Subagents

- Research and design subagents run on Opus; mechanical work (bulk edits, renames, checks) on Sonnet. In workflows, pin the exact identifiers from the model bindings table (`claude-opus-5-5`, `claude-sonnet-5-5`). A subagent definition's `model` field (an exact identifier such as `claude-opus-5-5`) applies unless a per-invocation model parameter or the `CLAUDE_CODE_SUBAGENT_MODEL_FORCE` setting overrides it (Claude Code subagent documentation, read 2026-10-13). So spawn `repo-reviewer` without a model parameter. An ad hoc spawn with no definition may only accept the aliases `opus` and `sonnet`, which select the latest model of that family; set one explicitly and record the resolved model when it is reported.
- A subagent's output is unverified input. Check its claims against their sources before anything built on them is committed.

## Attribution

Commits and pull requests carry `Co-Authored-By` only. Never include a session link.
