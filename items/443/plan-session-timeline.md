# Plan: session timeline record

Issue: [#443](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443). Pull request short name: `session-timeline`. Revision 3.

This plan adds a recorder, `scripts/timeline/event.sh`, that the implementing session calls at fixed points to append UTC events to a log outside the repository, and a renderer, `scripts/timeline/render.sh`, that merges that log with `summary.json`, commit times, comment and review times and CI job steps into a table for the pull request description. Durations come only from recorded start and end pairs. Revision 2 applies Hari's decision on Revision 1: no hooks, `render.sh` fails visibly when the pre-PR local gate has no recorded start and end, and a missing or empty event log is reported explicitly. Revision 3 applies Hari's decision on Revision 2: the skill section cites the verification-gates rule instead of restating it, and the plan check's P2 fixes are folded in.

## 1. Goal and authority

Goal: each pull request's description carries a machine-stamped timeline of its session or sessions, recorded the same way by Claude Code and Codex, so that where the time went can be read without a separate reconstruction.

Authority, in creation order:
- The issue body of #443. The issue's `updated_at` is 2026-11-13T16:36:44Z, the creation time of the second decision below (the REST API gives no separate body edit time; the body is unchanged from the text Revision 1 read). `ready-for-agent` was applied at 2026-11-13T07:13:04Z (issue events). No comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- [Decision 6858249662](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858249662), created 2026-11-13T16:04:32Z, headed "## Decision", answering the three open questions of Revision 1 (commit `5902134611d394688da394fefa954845a48acb90`): (1) leave hooks out, "Explicit events are the only data source for both tools"; (2) drop the cold-review check, and instead "`render.sh` fails visibly when the pre-PR local gate has no recorded start and end; the hosted gate's times come from the CI API"; (3) D3 and D4 "accepted by name", and "`render.sh` must report a missing or empty event log explicitly, never an empty table". It directs a revision, a fresh plan check, then implementation under standing approval if the check states `Blocking findings: none`.
- [Decision 6858982719](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858982719), created 2026-11-13T16:36:44Z, headed "## Decision", answering [handoff 6858512661](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858512661) on Revision 2 (commit `e9aa641d3b8ff4981a11229969a8ac5e74e9864f`): "Revise to Revision 3: replace the section 3.7 sentence with the plan check's suggested citation text ([check 6858500969](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858500969)), and fold in its P2 fixes. Get a fresh plan check; if it states `Blocking findings: none`, implement under standing approval." It adopts the check's suggested sentence and its P2 fixes; Revision 3 carries the P2s that the check marks `apply in implementation` and the two it marks for Revision 3, and leaves the exit-2 ordering P2 to the implementer, as the check states.
- There is no other comment headed "## Decision" or "## Approval" on the issue (catch-up of 2026-11-13, 8 comments). There is no split, no parent item and no item plan template entry; the whole issue is one pull request.

Evidence, not authority (docs/agents/factory.md "Item lifecycle" step 3): [comment 6848185690](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6848185690) reports one Codex desktop cloud task in which neither a repository `SessionStart` nor a `PostToolUse` hook ran; [comment 6848208909](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6848208909) adds that no hook trust prompt was shown. The [Revision 1 plan comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858179739) and the [handoff 6858192659](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858192659) carry the questions the decision answers.

Proposal mapping:
- Proposal 1 (experiment): done, per comment 6848185690. No work in this pull request.
- Proposal 2 (recorder and vocabulary): sections 3.1 to 3.3, requirements 1 to 9.
- Proposal 3 (renderer): sections 3.4 to 3.6, requirements 10 to 19 and 22 to 25. The `hook data` line it names stays, and is `absent` for every log `event.sh` writes (requirement 25).
- Proposal 4 (hooks as a secondary path): not planned, under decision 6858249662 point 1.
- Proposal 5 (template line): section 3.8, requirement 21.
- Proposal 6 (cold review flags a receipt whose final gate has no recorded start and end): dropped and replaced by the pre-PR local gate check in `render.sh` (3.6, requirement 22), under decision 6858249662 point 2. The hosted gate's times come from `--jobs` (3.5).

Protected paths: `/scripts/` (`.github/CODEOWNERS` line 13) and `/.*/`, which covers `.github/` and `.agents/` (line 7). This pull request merges only through Hari (AGENTS.md GOVERNANCE "Merge rule"). It changes no file under `.github/workflows/`.

## 2. Scope

Files this pull request may change:
- `scripts/timeline/event.sh` (new, committed with mode `100755`, as `scripts/verify-dotnet.sh` is on `main`)
- `scripts/timeline/render.sh` (new, committed with mode `100755`)
- `scripts/timeline/render.mjs` (new; the renderer's program, which `render.sh` runs)
- `scripts/tests/timeline.test.mjs` (new)
- `.agents/skills/implement-plan/SKILL.md`, by adding the section of 3.7 only. `.claude/skills/implement-plan` is a symlink to that folder and does not change.
- `.github/pull_request_template.md`, by adding the line of 3.8 only.
- `items/443/` (this plan, the receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change: `.github/workflows/` and `docs/operations/`; `.claude/settings.json`, `.claude/hooks/` and `.codex/` (decision 6858249662 point 1); `.agents/skills/repo-review/` and every other skill (decision 6858249662 point 2); `AGENTS.md`, `CLAUDE.md`, `docs/`; `scripts/verify-dotnet.sh` and every other existing file under `scripts/`; `package.json` and `package-lock.json`; `src/`, `tests/`, `results/`, `scenarios/`; any `.csproj`, `Directory.*.props` or lock file.

Tools: `event.sh` uses bash, the coreutils `date`, and `git`, whose failure gives `null` fields (3.2). `render.sh` runs `node` on `render.mjs`, which imports only Node built-in modules (`node:fs`, `node:child_process`, `node:process` and similar), so it needs no `npm ci`; it calls `git log` only for `--git-range`. The test uses `node:test` and `node:assert`. No `jq`, no `python3`, no `gh` inside these scripts: the session fetches GitHub data with `gh api` as 3.7 states, under AGENTS.md "GitHub issues and pull requests".

## 3. External contracts

UTC timestamps in this plan have the form `YYYY-MM-DDTHH:MM:SSZ`, as `date -u +%Y-%m-%dT%H:%M:%SZ` prints and as `summary.json` uses (`scripts/verify-dotnet.sh` lines 202, 266, 278 and 280 on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90).

### 3.1 `event.sh` command line and exit statuses

`scripts/timeline/event.sh <event> [<detail>]`

- One or two arguments. Any other count: exit 2, standard error `Usage: scripts/timeline/event.sh <event> [<detail>]`.
- Log path: `PARCHWELL_TIMELINE_LOG` when set and non-empty, else `${TMPDIR:-/tmp}/parchwell-timeline.jsonl`.
- Exit 0: one line appended. Exit 2: refused, nothing written, nothing created. Exit 1: the append failed; standard error `The timeline event could not be written.`
- No message ever echoes the detail or the log path.

### 3.2 Log line format

One JSON object per line, UTF-8, keys in this order, no spaces:

`{"v":1,"ts":"<UTC>","event":"<event>","detail":"<detail>","branch":<string or null>,"head":<string or null>}`

- `ts`: the time `event.sh` runs, from `date -u`.
- `detail`: the second argument, or `""` when absent.
- `branch`: output of `git rev-parse --abbrev-ref HEAD` run in the repository that holds the script, when that command succeeds, its output is not `HEAD`, and it matches `^[A-Za-z0-9._/-]{1,200}$`; otherwise `null`.
- `head`: output of `git rev-parse HEAD` in the same repository when it succeeds and matches `^[0-9a-f]{40}$`; otherwise `null`.
- A `git` failure never changes the exit status.

### 3.3 Enforced checks of `event.sh`

Checked in this order; the first that fails gives exit 2 with the message shown.

1. Argument count is 1 or 2. Message: the usage line of 3.1.
2. The event is one of the eleven in the table below. Message: `Unknown event: <event>` only when the event matches `^[a-z.]{1,40}$`, else `Unknown event.`
3. The detail matches the event's pattern in the table (the whole string, anchored). An absent detail is tested as `""`. Message: `Invalid detail for <event>.`
4. The log path is absolute (starts with `/`). Message: `The timeline log path must be absolute.`
5. The log path is not the repository root and does not start with the repository root followed by `/`, where the root is the directory two levels above the script (lexical comparison, symlinks not resolved). Message: `The timeline log must be outside the repository.`

| Event | Detail pattern | Meaning |
|---|---|---|
| `gate.start` | `sandbox\|full\|docs` | A gate command starts: `./scripts/verify-dotnet.sh --tier sandbox` (docs/agents/pull-requests.md "Verification gates" step 3), `./scripts/verify-dotnet.sh` with no options, or `npm run check:docs` |
| `gate.pass` | same as `gate.start` | That command ended with exit 0 |
| `gate.fail` | same as `gate.start` | It ended with a nonzero exit |
| `gate.stop` | same as `gate.start` | It ended without a result: cancelled, timed out or cut off |
| `focused.run` | `fast\|project` | One focused test run of "Verification gates" step 1: `--tier fast`, or `--project` |
| `review.start` | `comprehensive\|focused\|p2-correction` | Cold-review inputs frozen under docs/agents/pull-requests.md rule 5, by review mode of rule 8 |
| `review.end` | `(comprehensive\|focused\|p2-correction) P0=[0-9]{1,6} P1=[0-9]{1,6} P2=[0-9]{1,6}` | The complete cold-review report arrived, with its finding counts by severity |
| `env.interrupted` | empty only | The session found that the environment cut off a command or restarted |
| `agent.resumed` | empty only | The session continues after an interruption or a handoff |
| `handoff` | `hari\|next-session\|completion` | Just before a handoff or completion comment under AGENTS.md GOVERNANCE "Handoffs" |
| `decision.received` | `[0-9]{1,20}` | A new decision or approval comment found by a catch-up or refresh; the detail is its comment ID |

`event.sh` is the one owner of this vocabulary: a comment block at its top lists each event, its detail pattern (written without the table's `\` before `|`) and its meaning, as above. The skill section of 3.7 cites that block and does not restate the patterns.

### 3.4 `render.sh` command line and exit statuses

`scripts/timeline/render.sh --branch <name> --log <file> [--local-gate required|not-applicable|earlier-session] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]`

- `render.sh` runs `node "<its directory>/render.mjs" "$@"` and exits with node's status.
- `--local-gate` says whether this session's block must show the pre-PR local gate, step 3 of docs/agents/pull-requests.md "Verification gates". Default `required`. `not-applicable`: the pull request changes no .NET source or tests, so that section's last paragraph does not apply step 3 to it. `earlier-session`: the pull request was opened before this session, so under that section step 3 need not run again. The value is the session's declaration; the block shows it (3.6), so the cold review and Codex review can check it.
- Output: the block of 3.6 on standard output. Exit 0, except as the next item states.
- Exit 3, the full block of 3.6 on standard output, and on standard error exactly `The pre-PR local gate has no recorded start and end.`, when `--local-gate` is `required` and the gate check of 3.6 finds no recorded pair. This is the visible failure of decision 6858249662 point 2.
- Exit 2, nothing on standard output, one line on standard error, for the cases below. They are checked before the gate check, so an input error never gives exit 3:
  - an unknown flag, a flag without a value, or a repeated `--branch`, `--log`, `--local-gate`, `--git-range` or `--since`: `Usage: scripts/timeline/render.sh --branch <name> --log <file> [--local-gate required|not-applicable|earlier-session] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]`;
  - no `--branch`, or no `--log`: the same usage line;
  - `--local-gate` with a value other than the three above: `--local-gate must be required, not-applicable or earlier-session.`;
  - `--since` not in the UTC form: `--since must be a UTC time like 2026-11-13T10:00:00Z.`;
  - a file that exists but cannot be read: `Cannot read the file given to --<flag>.`;
  - a `--summary`, `--comments`, `--reviews` or `--jobs` file that is missing or not valid JSON of the shape in 3.5: `The file given to --<flag> is not valid input.`;
  - a `--log` line that is not a JSON object with keys `v` (1), `ts` (UTC form), `event` (a string), `detail` (a string), `branch` (string or null), `head` (string or null): `Line <n> of the --log file is not a timeline event.` (`<n>` counts from 1; empty lines are skipped and counted);
  - `git log` failing for `--git-range`: `git log failed for --git-range.`
- A `--log` file that does not exist is not an input error: the block reports `event log: missing` (3.6). This covers a container replaced during the session.
- No message echoes a path or a file's content.

### 3.5 Inputs and the rows they give

Each row has: time (UTC form), source, event, detail, seconds (a whole number or empty).

- `--log`: one row per line whose `branch` equals `--branch` exactly; other lines, `null` branch included, give no row. Time `ts`; source `log`; event and detail as recorded. Seconds, by pairing in time order (ties in line order): a `gate.pass`, `gate.fail` or `gate.stop` with detail `d` pairs with the latest earlier unpaired `gate.start` with detail `d`; a `review.end` whose detail starts with mode `m` pairs with the latest earlier unpaired `review.start` with detail `m`. A paired end row's seconds are its time minus the start's time. Every other log row has empty seconds.
- `--summary`: a `summary.json` of `scripts/verify-dotnet.sh` (lines 199 to 249 on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90), with `schema_version` 1. One row per `stages` entry, at its `end_utc`: source `summary`, event `verify.stage`, detail `<label> exit=<exit_code>`, seconds `end_utc` minus `start_utc`. Then one row at `ended_utc`: event `verify.run`, detail `tier=<tier> outcome=<outcome> exit=<exit_code>`, seconds `ended_utc` minus `started_utc`. `elapsed_s` is not used. Another `schema_version` is not valid input.
- `--git-range <base>..<head>`: one row per commit that `git log --format=%H%x09%ct <base>..<head>` prints, run in the current directory: time the committer time `%ct` in UTC; source `git`; event `commit`; detail the first 12 characters of the hash; seconds empty. No commit message is read.
- `--comments`: a JSON array from `GET /repos/{owner}/{repo}/issues/{issue_number}/comments`, which also lists pull request comments. One row per element: time `created_at`; source `comment`; event `comment`; detail the decimal `id`; seconds empty. No body or login is read.
- `--reviews`: a JSON array from `GET /repos/{owner}/{repo}/pulls/{pull_number}/reviews`. One row per element whose `submitted_at` is present and not null (pending reviews omit it, per the documentation of D1): time `submitted_at`; source `review`; event `review`; detail `<state> by <user.login> at <first 12 characters of commit_id>`, with `user.login` read as `unknown` when `user` is null and the `at` part left out when `commit_id` is null; seconds empty.
- `--jobs`: a JSON object from `GET /repos/{owner}/{repo}/actions/runs/{run_id}/jobs`, with key `jobs`. Per job whose `started_at` is present and not null: one row per step whose `started_at` is present and not null, in `number` order, then one row for the job. A step or job whose `completed_at` is present and not null is placed at `completed_at` with seconds `completed_at` minus `started_at`; with `completed_at` absent or null, at `started_at` with empty seconds. Source `ci`; event `ci.step` with detail `<job name> / <step name> <conclusion>`, or `ci.job` with detail `<job name> <conclusion>`; a null `conclusion` is written `pending`.
- GitHub times may carry fractional seconds or an offset; they are converted to the UTC form, truncating fractions. A time that cannot be parsed makes the file not valid input.
- `--since <t>` drops every row from every source whose time is before `t`. Without `--since`, `t` is the earliest time among the log rows; with no log rows, nothing is dropped. Log pairing (above) runs before the drop.

### 3.6 Output block

Rows are sorted by time; equal times keep source order `log`, `summary`, `git`, `comment`, `review`, `ci`, then input order. Within one summary file, its stage rows come before its run row at equal times.

In the detail cell, `&` becomes `&amp;` first, then `<` becomes `&lt;`, `>` becomes `&gt;` and `|` becomes `\|`. Nothing else is changed.

The block, literal apart from the bracketed parts:

```
<details><summary>Timeline <first row time> to <last row time> (UTC)</summary>

hook data: <present|absent>
event log: <present|missing|empty|no events for this branch>
pre-PR local gate: <recorded|MISSING, no recorded start and end|not applicable (declared)|run in an earlier session (declared)>
Rows: log <n|absent>, summary <n|absent>, git <n|absent>, comment <n|absent>, review <n|absent>, ci <n|absent>.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| <time> | <source> | <event> | <detail> | <seconds or empty> |

</details>
```

- `hook data: present` when at least one log row's event starts with `hook.`; otherwise `absent`. `event.sh` writes no `hook.` event (3.3 rejects it; decision 6858249662 point 1), so the line is `absent` for every log `event.sh` wrote.
- `event log`, decided on the `--log` file before `--since` is applied: `missing` when the file does not exist; `empty` when it exists and holds no non-empty line; `no events for this branch` when it holds at least one line and no line's `branch` equals `--branch`; otherwise `present`.
- `pre-PR local gate`: with `--local-gate not-applicable` or `earlier-session`, the declared text shown above, whatever the log holds. With `required`, `recorded` when the table holds at least one row with source `log`, event `gate.pass`, `gate.fail` or `gate.stop`, detail `sandbox` and non-empty seconds, that is, an end paired with a recorded `gate.start sandbox` under 3.5; otherwise `MISSING, no recorded start and end`, and the exit status is 3 (3.4). Only detail `sandbox` counts, because "Verification gates" step 3 names `./scripts/verify-dotnet.sh --tier sandbox`. A `gate.fail` or `gate.stop` pair counts as recorded, because the decision asks for a recorded start and end, not a pass; the table shows the outcome.
- `Rows`: per source, the number of rows in the table, or `absent` when its flag was not given, or for `--log`, when the file does not exist.
- With no rows, the summary line is `<summary>Timeline: no rows</summary>`, and in place of the table and its two header lines the block has the single line `No rows from any source.` The table appears, with its header lines, only when it holds at least one row (decision 6858249662 point 3, "never an empty table").
- The output ends with `</details>` and one newline.

### 3.7 Skill section

`.agents/skills/implement-plan/SKILL.md` gains this section at the end of the file, after the "## Steps" section (after line 19 on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90), separated by one empty line, literal:

```
## Timeline record

Record this session's events with `scripts/timeline/event.sh <event> [<detail>]`, whose header lists each event, the detail it takes and when it applies. Record each event when it happens, not afterwards: `gate.start` before, and `gate.pass`, `gate.fail` or `gate.stop` after, each gate command it names; `focused.run` for each focused test run; `review.start` when cold-review inputs are frozen under docs/agents/pull-requests.md rule 5 and `review.end` when the complete report arrives; `env.interrupted` and `agent.resumed` as they occur; `decision.received` for each new decision or approval comment the catch-up or a refresh finds; `handoff` just before a handoff or completion comment. A failed `event.sh` call does not stop the work; note it in the authored receipt.

Before each edit of the pull request description, fetch the issue's and the pull request's comments, the pull request's reviews and the jobs of each workflow run on its head with `gh api`, under AGENTS.md "GitHub issues and pull requests", into files outside the repository. Then run `scripts/timeline/render.sh` with the branch, the timeline log, each `summary.json` this session produced, the commit range from the merge base with `origin/main` to the head, and those files. Pass `--local-gate not-applicable` or `--local-gate earlier-session`, the value that names the reason, only when docs/agents/pull-requests.md "Verification gates", its last two paragraphs, does not require this session to run its step 3; otherwise leave the default. Put its output under the self-review record's "Timeline record" line, as this session's block, also when it exits 3: that exit means the pre-PR local gate has no recorded start and end, and the block shows it. State any exit 3 in the authored receipt. Each session adds or replaces only its own block and leaves earlier sessions' blocks unchanged. Commit no timeline output (docs/agents/pull-requests.md rule 8).
```

### 3.8 Template line

`.github/pull_request_template.md` gains one line directly after line 15 (`- Hosted Codex review and final-head CI (separate from cold review):`), literal:

```
- Timeline record (one block per session from `scripts/timeline/render.sh`; implement-plan "Timeline record"):
```

## 4. Requirements

1. WHEN `event.sh` gets a listed event and a detail matching its pattern THEN it appends exactly one line in the format of 3.2 and exits 0.
2. WHEN the argument count is 0 or more than 2 THEN exit 2 with the usage line, nothing written (check 1).
3. WHEN the event is not listed THEN exit 2 with the message of check 2, nothing written.
4. WHEN the detail does not match the event's pattern, including any detail on an empty-only event and a missing detail on an event whose pattern rejects `""` THEN exit 2 with `Invalid detail for <event>.`, nothing written, and the detail not echoed.
5. WHEN the log path is relative THEN exit 2 (check 4); WHEN it is inside the repository THEN exit 2 (check 5); in both cases no file is created.
6. WHEN `PARCHWELL_TIMELINE_LOG` is unset or empty THEN the line goes to `${TMPDIR:-/tmp}/parchwell-timeline.jsonl`.
7. WHEN `git` fails THEN `branch` and `head` are `null` and the exit status is 0.
8. WHEN `git rev-parse --abbrev-ref HEAD` prints `HEAD` (detached) THEN `branch` is `null`.
9. WHEN `event.sh` runs twice THEN the file holds both lines in call order.
10. WHEN `render.sh` gets the fixtures of section 7 THEN its output equals the literal block of section 7 exactly.
11. WHEN a log line's branch differs from `--branch` or is `null` THEN it gives no row and takes no part in pairing.
12. WHEN two `gate.start` with the same detail precede one end THEN the end pairs with the later start, and the earlier start keeps empty seconds.
13. WHEN an end event has no earlier unpaired start with its detail or mode THEN its seconds are empty.
14. WHEN `--log` names a file that does not exist THEN the block has `event log: missing` and `Rows: log absent`, and the exit status is not 2.
15. WHEN an input fails a check of 3.4 THEN exit 2 with that message and no standard output, also when the gate check would fail.
16. WHEN `--since` is given THEN rows before it are dropped from every source; WHEN it is not THEN the earliest log row's time is used.
17. WHEN a detail holds `&`, `<`, `>` or `|` THEN the cell holds the escaped form of 3.6.
18. WHEN a CI step or job has null `started_at` THEN it gives no row; WHEN only `completed_at` is null THEN its row is at `started_at` with empty seconds and conclusion `pending` if null.
19. WHEN no source yields a row THEN the block has `<summary>Timeline: no rows</summary>` and the line `No rows from any source.`, and no table header line.
20. WHEN the implementing session works under `implement-plan` THEN the skill carries the section of 3.7 verbatim.
21. The pull request template carries the line of 3.8 at the place stated.
22. WHEN `--local-gate` is `required` or not given AND the table holds no `log` row with event `gate.pass`, `gate.fail` or `gate.stop`, detail `sandbox` and non-empty seconds THEN standard output is the full block with `pre-PR local gate: MISSING, no recorded start and end`, standard error is `The pre-PR local gate has no recorded start and end.`, and the exit status is 3. WHEN such a row exists THEN the line is `pre-PR local gate: recorded` and the exit status is 0.
23. WHEN `--local-gate` is `not-applicable` or `earlier-session` THEN the line shows the declared text of 3.6, the exit status is 0, and the log's gate rows do not change the line.
24. WHEN the `--log` file exists and holds no non-empty line THEN the block has `event log: empty`; WHEN it holds lines but none for `--branch` THEN `event log: no events for this branch`; otherwise, for an existing file, `event log: present`.
25. WHEN a log row's event starts with `hook.` THEN the block has `hook data: present`; otherwise `hook data: absent`.

## 5. Invariants and safety rules

- The timeline log path never lexically equals the repository root or starts with the repository root followed by `/` (check 5, which does not resolve symlinks or `..`, so a path such as `<root>/../<repo name>/x.jsonl` passes it), and no timeline output is committed (docs/agents/pull-requests.md rule 8).
- The log holds only the fixed vocabulary, branch names matching 3.2's pattern, commit hashes and comment IDs: no free-text paths or host names (AGENTS.md hard safety rule 7; issue body proposal 2). The rendered block adds only GitHub step and job names, review states, reviewer logins and `summary.json` labels.
- Seconds are only differences of recorded start and end pairs, computed by the script (docs/agents/pull-requests.md rule 4; AGENTS.md hard safety rule 11). No row's seconds come from a gap between unrelated rows.
- No workflow changes (AGENTS.md hard safety rule 8). No hook configuration changes (decision 6858249662 point 1).
- `render.sh` never prints a table without rows, and never hides a missing or empty event log or a missing pre-PR local gate record (decision 6858249662 points 2 and 3).
- `event.sh` failing never blocks a gate or a review (3.7).

## 6. Standing fixture rule

This plan adds new validators and a new test file; no existing test exercises these scripts, so no existing fixture can be rejected. The rule stands for the implementer all the same: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Test map

All cases are in `scripts/tests/timeline.test.mjs`, run with `node --test scripts/tests/timeline.test.mjs`. Each case uses its own temporary directory from `fs.mkdtemp(os.tmpdir() + "/timeline-")`, outside the repository, and sets `PARCHWELL_TIMELINE_LOG` to a file in it unless the case says otherwise. `event.sh` cases run the script from the repository checkout. Every case runs `event.sh` and `render.sh` by path, not through `bash`, so a missing executable bit fails a test. `UTC` below means the regular expression `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$`.

### 7.1 `event.sh`

| Req | Case | Inputs | Literal expectations |
|---|---|---|---|
| 1 | `event_line_format` | `gate.start sandbox` | exit 0; file has 1 line; `Object.keys` equals `["v","ts","event","detail","branch","head"]`; `v` 1; `ts` matches UTC; `event` `"gate.start"`; `detail` `"sandbox"`; `head` equals `git rev-parse HEAD`; `branch` equals `git rev-parse --abbrev-ref HEAD` unless that prints `HEAD`, then `null` |
| 1 | `event_vocabulary_accepts` | one call each: `gate.start full`, `gate.pass docs`, `gate.fail sandbox`, `gate.stop full`, `focused.run fast`, `focused.run project`, `review.start p2-correction`, `review.end comprehensive P0=0 P1=0 P2=0`, `review.end focused P0=999999 P1=1 P2=12`, `env.interrupted`, `agent.resumed`, `handoff hari`, `handoff next-session`, `handoff completion`, `decision.received 6848185690`, `decision.received 12345678901234567890` | each exit 0; 16 lines; each line's `event` and `detail` equal the call's (detail `""` for the two one-argument calls) |
| 2 | `event_arg_count` | no arguments; `handoff hari extra` | each exit 2; stderr `Usage: scripts/timeline/event.sh <event> [<detail>]`; log file does not exist |
| 3 | `event_unknown` | `gate.begin sandbox`; `GATE.START sandbox` | exit 2; stderr `Unknown event: gate.begin`; then `Unknown event.`; no file |
| 4 | `event_bad_detail` | `gate.start /home/user`; `gate.start`; `review.end comprehensive P0=1 P1=2`; `review.end comprehensive P0=-1 P1=0 P2=0`; `review.end comprehensive P0=0 P1=0 P2=1234567`; `review.end fast P0=0 P1=0 P2=0`; `env.interrupted x`; `decision.received ""` (empty string argument); `decision.received 123a`; `decision.received 123456789012345678901`; `focused.run sandbox` | each exit 2; stderr `Invalid detail for <event>.` with the call's event; stderr does not contain the detail; no file |
| 5 | `event_relative_path` | `PARCHWELL_TIMELINE_LOG=timeline.jsonl`, `handoff hari` | exit 2; stderr `The timeline log path must be absolute.`; no `timeline.jsonl` in the working directory |
| 5 | `event_inside_repo` | `PARCHWELL_TIMELINE_LOG=<repo root>/timeline-test.jsonl`; then `=<repo root>` | exit 2; stderr `The timeline log must be outside the repository.`; `<repo root>/timeline-test.jsonl` does not exist |
| 5 | `event_root_prefix_sibling` | the test creates `<tmp>/repo` as a git repository holding a copy of `event.sh` at `<tmp>/repo/scripts/timeline/event.sh` and runs that copy with `handoff hari`: first with `PARCHWELL_TIMELINE_LOG=<tmp>/repo-sibling.jsonl`, then with `=<tmp>/repo/x.jsonl` | first: exit 0 and 1 line in `<tmp>/repo-sibling.jsonl` (a shared string prefix without `/` is not inside); second: exit 2, stderr `The timeline log must be outside the repository.`, no `<tmp>/repo/x.jsonl` |
| 6 | `event_default_path` | `PARCHWELL_TIMELINE_LOG` set to `""`, `TMPDIR=<tmp>`, `handoff hari` | exit 0; `<tmp>/parchwell-timeline.jsonl` has 1 line |
| 7 | `event_no_git` | `GIT_DIR=/nonexistent`, `handoff hari` | exit 0; `branch` `null`; `head` `null` |
| 8 | `event_detached` | `GIT_DIR` and `GIT_WORK_TREE` pointing to a temp repo with one commit and `git checkout --detach` | exit 0; `branch` `null`; `head` that commit's hash |
| 9 | `event_append_order` | `handoff hari` then `agent.resumed` | 2 lines; events in that order |
| 3.1 | `event_write_failure` | `PARCHWELL_TIMELINE_LOG=<tmp>/ro/x.jsonl` with `<tmp>/ro` mode `0500`; skipped when `process.getuid() === 0`, with the skip reason printed | exit 1; stderr `The timeline event could not be written.` |

### 7.2 `render.sh`

Fixtures, written by the test into its temp dir:

`log.jsonl`, one line each, `v` 1 and `head` `null` throughout, `branch` `claude/443-session-timeline` unless stated:
1. `2026-11-13T10:00:00Z` `gate.start` `sandbox`
2. `2026-11-13T09:00:00Z` `gate.start` `sandbox`, branch `claude/999-other`
3. `2026-11-13T10:05:30Z` `gate.pass` `sandbox`
4. `2026-11-13T10:06:00Z` `review.start` `comprehensive`
5. `2026-11-13T09:30:00Z` `handoff` `hari`, branch `null`
6. `2026-11-13T10:20:00Z` `review.end` `comprehensive P0=0 P1=1 P2=2`
7. `2026-11-13T10:21:00Z` `gate.fail` `docs`
8. `2026-11-13T10:30:00Z` `focused.run` `project`
9. `2026-11-13T11:00:00Z` `gate.start` `sandbox`
10. `2026-11-13T11:01:00Z` `gate.start` `sandbox`
11. `2026-11-13T11:02:00Z` `gate.stop` `sandbox`

`summary.json`: `{"schema_version":1,"started_utc":"2026-11-13T10:00:05Z","ended_utc":"2026-11-13T10:05:25Z","tier":"sandbox","outcome":"partial_passed","exit_code":0,"stages":[{"label":"Restore","elapsed_s":99,"start_utc":"2026-11-13T10:00:06Z","end_utc":"2026-11-13T10:00:16Z","exit_code":0}]}` plus the other keys of `summary.json` with any valid values. `elapsed_s` 99 shows it is not used.

Git: a temp repository with an empty initial commit `A`, then commits `B` and `C` made with `GIT_COMMITTER_DATE` `@1794564600 +0000` and `@1794564900 +0000` (Git's internal date format; `date -u -d 2026-11-13T10:10:00Z +%s` and `date -u -d 2026-11-13T10:15:00Z +%s` print these numbers), that is 2026-11-13T10:10:00Z and 2026-11-13T10:15:00Z; `--git-range A..C`, run with the temp repository as the current directory. `<B12>` and `<C12>` are the first 12 characters of their hashes.

`comments.json`: `[{"id":1,"created_at":"2026-11-13T10:25:00Z","body":"x | y"},{"id":2,"created_at":"2026-11-13T08:00:00Z"}]`.

`reviews.json`: `[{"id":7,"user":{"login":"chatgpt-codex-connector[bot]"},"state":"COMMENTED","submitted_at":"2026-11-13T10:40:00.123Z","commit_id":"0123456789abcdef0123456789abcdef01234567"},{"id":8,"user":null,"state":"PENDING","commit_id":null},{"id":9,"user":null,"state":"PENDING","submitted_at":null,"commit_id":null}]`. Review 8 omits `submitted_at`, as the documentation of D1 says pending reviews do; review 9 has it null.

`jobs.json`: `{"total_count":1,"jobs":[{"name":"dotnet-tests (A|B) <x> & y","started_at":"2026-11-13T10:35:00Z","completed_at":"2026-11-13T10:39:30Z","conclusion":"success","steps":[{"name":"Run","number":1,"started_at":"2026-11-13T10:35:10Z","completed_at":"2026-11-13T10:39:00Z","conclusion":"success"},{"name":"Skipped","number":2,"started_at":null,"completed_at":null,"conclusion":"skipped"},{"name":"Upload","number":3,"started_at":"2026-11-13T10:39:05Z","completed_at":null,"conclusion":null},{"name":"Queued","number":4,"conclusion":null}]}]}`. Step 4 omits `started_at` and `completed_at`, which the documentation of D1 does not mark required for a step.

Command for `render_full`: `render.sh --branch claude/443-session-timeline --log log.jsonl --summary summary.json --git-range A..C --comments comments.json --reviews reviews.json --jobs jobs.json`. No `--local-gate`, so it is `required`. Expected standard output, exactly (with `<B12>` and `<C12>` substituted), empty standard error, exit 0:

```
<details><summary>Timeline 2026-11-13T10:00:00Z to 2026-11-13T11:02:00Z (UTC)</summary>

hook data: absent
event log: present
pre-PR local gate: recorded
Rows: log 9, summary 2, git 2, comment 1, review 1, ci 3.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| 2026-11-13T10:00:00Z | log | gate.start | sandbox |  |
| 2026-11-13T10:00:16Z | summary | verify.stage | Restore exit=0 | 10 |
| 2026-11-13T10:05:25Z | summary | verify.run | tier=sandbox outcome=partial_passed exit=0 | 320 |
| 2026-11-13T10:05:30Z | log | gate.pass | sandbox | 330 |
| 2026-11-13T10:06:00Z | log | review.start | comprehensive |  |
| 2026-11-13T10:10:00Z | git | commit | <B12> |  |
| 2026-11-13T10:15:00Z | git | commit | <C12> |  |
| 2026-11-13T10:20:00Z | log | review.end | comprehensive P0=0 P1=1 P2=2 | 840 |
| 2026-11-13T10:21:00Z | log | gate.fail | docs |  |
| 2026-11-13T10:25:00Z | comment | comment | 1 |  |
| 2026-11-13T10:30:00Z | log | focused.run | project |  |
| 2026-11-13T10:39:00Z | ci | ci.step | dotnet-tests (A\|B) &lt;x&gt; &amp; y / Run success | 230 |
| 2026-11-13T10:39:05Z | ci | ci.step | dotnet-tests (A\|B) &lt;x&gt; &amp; y / Upload pending |  |
| 2026-11-13T10:39:30Z | ci | ci.job | dotnet-tests (A\|B) &lt;x&gt; &amp; y success | 270 |
| 2026-11-13T10:40:00Z | review | review | COMMENTED by chatgpt-codex-connector[bot] at 0123456789ab |  |
| 2026-11-13T11:00:00Z | log | gate.start | sandbox |  |
| 2026-11-13T11:01:00Z | log | gate.start | sandbox |  |
| 2026-11-13T11:02:00Z | log | gate.stop | sandbox | 60 |

</details>
```

This one case covers requirements 10, 11 (lines 2 and 5), 12 (lines 9 to 11), 13 (line 7), 16 without `--since` (comment 2 dropped), 17, 18 (steps 2, 3 and 4; reviews 8 and 9), 22 (recorded, from lines 1 and 3 and from lines 10 and 11), 24 (`present`) and 25 (`absent`).

In the cases below, every `render.sh` call has `--branch claude/443-session-timeline` unless the case says otherwise, and `<missing>` is `<tmp>/none.jsonl`, which the test never creates. "Gate line" means the `pre-PR local gate:` line.

| Req | Case | Inputs | Literal expectations |
|---|---|---|---|
| 10 to 13, 16 to 18, 22, 24, 25 | `render_full` | above | output above, empty stderr, exit 0 |
| 16, 22 | `render_since` | `--since 2026-11-13T10:30:00Z`, `--log log.jsonl`, `--comments comments.json` only | exit 0; rows: `10:30:00Z focused.run`, `11:00:00Z gate.start`, `11:01:00Z gate.start`, `11:02:00Z gate.stop` with seconds `60`; `Rows: log 4, summary absent, git absent, comment 0, review absent, ci absent.`; gate line `recorded` (the `gate.stop` row keeps its seconds after its start at 11:01:00Z). A row at exactly the `--since` time is kept |
| 11, 16 | `render_since_default_branch_only` | `--local-gate not-applicable`, `--log` file holding line 2 and then line 1 of `log.jsonl`, `--comments` file `[{"id":3,"created_at":"2026-11-13T09:30:00Z"}]` | exit 0; empty stderr; `Rows: log 1, summary absent, git absent, comment 0, review absent, ci absent.` (the default `--since` is 10:00:00Z from the one row on `--branch`, not 09:00:00Z from line 2, so comment 3 is dropped) |
| 14, 22 | `render_missing_log` | `--log <missing> --comments comments.json` | exit 3; stderr `The pre-PR local gate has no recorded start and end.`; stdout is the full block with `event log: missing`, gate line `MISSING, no recorded start and end`, `Rows: log absent, summary absent, git absent, comment 2, review absent, ci absent.`, and both comment rows (no log rows, so nothing dropped) |
| 19, 22 | `render_empty` | `--branch x --log <missing>` | exit 3; stderr as in `render_missing_log`; stdout equals `<details><summary>Timeline: no rows</summary>\n\nhook data: absent\nevent log: missing\npre-PR local gate: MISSING, no recorded start and end\nRows: log absent, summary absent, git absent, comment absent, review absent, ci absent.\nSeconds are computed only from recorded start and end pairs.\n\nNo rows from any source.\n\n</details>\n` |
| 19, 23, 24 | `render_empty_declared` | `--branch x --log <missing> --local-gate not-applicable` | exit 0; empty stderr; stdout equals the `render_empty` text with its gate line replaced by `pre-PR local gate: not applicable (declared)` |
| 24 | `render_log_empty_file` | `--local-gate not-applicable` and `--log` naming, in turn, a file of 0 bytes and a file holding `"\n\n"` | each exit 0; `event log: empty`; `Rows: log 0, ...`; `No rows from any source.`; no line starting `| UTC` |
| 24 | `render_log_other_branch` | `--local-gate not-applicable`, `--log` file holding only line 2 of `log.jsonl` (branch `claude/999-other`) and line 5 (branch `null`) | exit 0; `event log: no events for this branch`; `Rows: log 0, ...` |
| 22 | `render_gate_boundaries` | `--log` file, each line on the branch, one file per sub-case: (i) only `gate.start sandbox`; (ii) only `gate.pass sandbox`; (iii) `gate.start full` at 10:00:00Z, `gate.pass full` at 10:01:00Z; (iv) `gate.start sandbox` at 10:00:00Z, `gate.fail sandbox` at 10:01:00Z; (v) `gate.start sandbox` at 10:00:00Z, `gate.stop sandbox` at 10:01:00Z; (vi) as (iv) with `--since 2026-11-13T10:00:30Z`; (vii) `gate.start sandbox` at 10:00:00Z, `gate.pass sandbox` at 10:01:00Z, both with branch `claude/999-other` | (i), (ii), (iii), (vii): exit 3, gate line `MISSING, no recorded start and end`; (iv), (v): exit 0, gate line `recorded`; (vi): exit 0, gate line `recorded`, the `gate.fail` row has seconds `60` and the `gate.start` row is dropped |
| 23 | `render_gate_declared` | `--log` file holding only `gate.start sandbox`, with `--local-gate earlier-session`; then `log.jsonl` with `--local-gate not-applicable` | first: exit 0, gate line `pre-PR local gate: run in an earlier session (declared)`; second: exit 0, gate line `pre-PR local gate: not applicable (declared)` although `log.jsonl` holds a recorded pair |
| 15 | `render_bad_inputs` | each separately: no `--branch`; no `--log`; `--frobnicate 1`; `--branch a --branch b`; `--local-gate required --local-gate required`; `--local-gate maybe`; `--since "2026-11-13 10:00:00"` (one argument); `--comments <tmp>/missing.json`; `--comments` file `{"a":1}`; `--summary` file with `"schema_version":2`; `--log` file whose line 2 is `{"v":1}` (line 1 valid); `--git-range nope..nada`. Every case that does not omit `--log` or give its own `--log` file has `--log <missing>`, so the gate check alone would give exit 3 | each exit 2, empty stdout, stderr exactly the message of 3.4 for that input (`Line 2 of the --log file is not a timeline event.` for the log case) |
| 15 | `render_unreadable` | `--log` file with mode `000`, skipped when the test runs as root (`process.getuid() === 0`), with the skip reason printed | exit 2; `Cannot read the file given to --log.` |
| 25 | `render_hook_present` | `--local-gate not-applicable`, a log line with event `hook.post`, detail `""`, on the branch | `hook data: present` |

### 7.3 Text checks

| Req | Case | Literal expectation |
|---|---|---|
| 20 | `skill_section` | `.agents/skills/implement-plan/SKILL.md` contains the block of 3.7 verbatim, after the line `## Steps` |
| 21 | `template_line` | line 16 of `.github/pull_request_template.md` equals the line of 3.8 and line 15 is unchanged |
| 3.3 | `vocabulary_header` | `event.sh` contains, for each of the eleven events of 3.3, the event name and its detail pattern, with no backslash before its vertical bars |

Boundaries not covered by an automated case, with reason:
- Exit 1 when running as root: every path is writable for root, so `event_write_failure` and `render_unreadable` skip there. The receipt states the code path and whether the cases ran or skipped.
- The live `gh api` fetches of 3.7: they need network and a pull request; acceptance command 3 covers them.

## 8. Deployment assumptions

None involves a network connection to Azure or Kafka; plan rule 3's open-or-fresh connection and idle-or-load conditions do not apply, and no evidence was taken under load.

- D1: the GitHub REST responses have the fields 3.5 reads. Evidence: the GitHub REST documentation pages for "List issue comments", "List reviews for a pull request" and "List jobs for a workflow run", read with WebFetch on 2026-11-13 for Revision 1 and again for Revision 2; on the second read the pages showed API version 2026-03-10. They state: comment `id` and `created_at` required; review `id`, `state` and `commit_id` (string or null) and `user` (null or a user) required, `submitted_at` not required, and pending reviews "do not include the submitted_at property"; jobs response key `jobs` required; job `name` and `started_at` required, `completed_at` and `conclusion` string or null; step `name`, `number` and `conclusion` (string or null) required, step `started_at` and `completed_at` string or null and not required. 3.5 treats every not-required or nullable field as possibly absent or null. Offline tests use fixtures of that shape (7.2); acceptance command 3 renders real responses.
- D2: `summary.json` has the shape `scripts/verify-dotnet.sh` lines 199 to 249 write on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90. This is on `main`, not an assumption; acceptance command 2 renders a real file.
- D3: Node, with only built-in modules, runs in every session that publishes a pull request. Evidence: this Claude Code cloud sandbox has `node` v22.23.4 (`node --version`, 2026-11-13, idle); docs/agents/pull-requests.md rule 8 already requires `npm run check:docs`, a Node command, before every cold review; `items/404/burst-adr-receipt.md` line 32 records `npm run check:docs` completing in a Codex-implemented session, which does not record whether that was a cloud task. Not verified for Codex cloud tasks by any source this plan read. Risk accepted by name in [decision 6858249662](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858249662) point 3, on the evidence that PR 439's Codex cloud session ran `npm run check:docs`. `render.mjs` must also run on Node 22, the version measured here (`node --version` printed v22.23.4 again on 2026-11-13 in the Revision 2 session, idle), although `package.json` line 5 declares `>=24`.
- D4: `${TMPDIR:-/tmp}` keeps a file across tool calls within one session. Evidence: in the Revision 1 Claude Code cloud session a file written to `/tmp` by one Bash call was read back by the next (2026-11-13T15:57:06Z, idle). Not measured in a Codex cloud task. Risk accepted by name in decision 6858249662 point 3, on the evidence that PR 439's Codex cloud session cited `/tmp` verification results across turns. A replaced container loses the log, which requirement 14 reports as `event log: missing`, and requirement 22 then fails visibly unless the session declares `--local-gate`.

## 9. Acceptance commands

1. `node --test scripts/tests/timeline.test.mjs`: exits 0; every case of section 7 passes or is skipped only for the root reasons stated. Evidence: output with pass, fail and skip counts in the receipt.
2. In the implementing session: `scripts/timeline/event.sh gate.start sandbox`, `./scripts/verify-dotnet.sh --tier sandbox`, then `gate.pass` or `gate.fail`; then `render.sh` with `--log` and that run's `summary.json`. Evidence: the rendered block in the receipt, showing the `verify.run` row and the paired gate row.
3. The implementing session renders its own block under 3.7 and puts it in the pull request description. Evidence: the description shows the block with `hook data: absent`, `event log: present`, a `pre-PR local gate:` line, and rows from every source given; the receipt records the exit status and the `--local-gate` value used.
4. `npm run check:docs`: no warning in a changed file, or each disposed of under docs/agents/pull-requests.md rule 8.
5. Hosted `.NET verification` on the final head passes (this plan changes no .NET source; the check runs as for every pull request). Evidence: run link in the description.

## 10. Sources

- Issue #443 body, comments 6848185690, 6848208909, 6858179739, 6858192659, 6858512661, decision 6858249662, plan check 6858500969 and decision 6858982719, issue events (label `ready-for-agent` at 2026-11-13T07:13:04Z), read with `gh api` on 2026-11-13.
- Revision 1 of this plan: `items/443/plan-session-timeline.md` at commit `5902134611d394688da394fefa954845a48acb90`. Revision 2: the same file at commit `e9aa641d3b8ff4981a11229969a8ac5e74e9864f`.
- `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90: `scripts/verify-dotnet.sh` lines 199 to 249 (`summary_json`), 202, 266, 278 and 280 (`date -u +%Y-%m-%dT%H:%M:%SZ`) and 252 to 270 (results directory, exit trap); `.github/pull_request_template.md` lines 8 to 18; `.agents/skills/implement-plan/SKILL.md` lines 1 to 19; `.agents/skills/repo-review/SKILL.md` lines 37 to 41; `.claude/settings.json`; `.github/CODEOWNERS` lines 5 to 13; `docs/agents/pull-requests.md` rules 4 and 8 and "Verification gates" (lines 24, 48 to 59); `package.json` lines 5 to 9.
- GitHub REST API documentation: https://docs.github.com/en/rest/issues/comments, https://docs.github.com/en/rest/pulls/reviews, https://docs.github.com/en/rest/actions/workflow-jobs, read with WebFetch on 2026-11-13 (the pages showed API version 2026-03-10 on the Revision 2 read).
- https://git-scm.com/docs/git-commit, "DATE FORMATS", read with WebFetch on 2026-11-13: `GIT_COMMITTER_DATE` accepts the Git internal format `<unix-timestamp> <time-zone-offset>`, "safer" with `@` before the timestamp (for example `@0 +0000`), and ISO 8601 shown without a zone (`2005-04-07T22:13:13`); the section does not mention a `Z` suffix. So the git fixture of 7.2 uses the internal format. In this session, git 2.43.0 committed with `GIT_COMMITTER_DATE='@1794564600 +0000'` and `TZ=Asia/Kolkata git log --format=%ct` printed `1794564600`.
- https://learn.chatgpt.com/docs/environments/cloud-environments.md, read with WebFetch on 2026-11-13: it does not name a default image or list preinstalled tools; it says Codex "identifies runtimes, package versions, tools, and services from your repositories".
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of log lines, comments, reviews, jobs and summary stages is known only when `render.sh` runs; each is one pass over its input, and pairing is one pass over the log. No cap is set, because the output is one table per session and its length is the work done. Implementation effort has no runtime-sized step: the test cases are listed in section 7.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body, the plan adds: a refusal of log paths inside the repository (check 5); per-event detail patterns, where the issue says only "fixed vocabulary"; a `head` field; the `Rows` line and the `absent` marker for each source; GitHub reviews as a source, which the issue's "PR comment times" does not name explicitly; one block per session. It interprets `focused.run` as one focused test run of "Verification gates" step 1 and `gate.*` as the sandbox, full and documentation gate commands; the issue does not define them. Proposal 4 is not planned and proposal 6 is replaced, both under decision 6858249662.

Against decision 6858249662, the plan states how points 2 and 3 are carried out, and each choice is visible in the block. Point 2: "fails visibly" is exit 3 with a fixed message on standard error and a `MISSING` gate line in a block that is still written (3.4, 3.6), so the gap shows in the pull request description too. A recorded pair is a `gate.start sandbox` followed by any end (`gate.pass`, `gate.fail` or `gate.stop`) with detail `sandbox`, because the decision asks for a recorded start and end. The `--local-gate` declarations cover the two cases in which docs/agents/pull-requests.md "Verification gates" itself runs no pre-PR local gate: a pull request with no .NET source or test change (its last paragraph) and a session after the pull request opened (its rule that step 3 need not run again). The declaration is shown in the block, not hidden, and defaults to `required`. Point 3: "explicitly" is the `event log:` line with four states, and "never an empty table" is the `No rows from any source.` line in place of the header. `--log` becomes a required flag, so the log's state is always reported.

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: the GitHub REST response shapes (D1), checked against the official documentation; Node and `/tmp` persistence in Codex cloud tasks (D3, D4), which no source settles and whose risk decision 6858249662 point 3 accepts by name. Git's reading of a committer date with a `Z` suffix is no longer relied on: the git fixture of 7.2 uses Git's internal date format with an explicit `+0000` offset.

## 12. Open questions for Hari

None.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in section 7.
