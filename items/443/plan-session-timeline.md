# Plan: session timeline record

Issue: [#443](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443). Pull request short name: `session-timeline`. Revision 1.

This plan adds a recorder, `scripts/timeline/event.sh`, that the implementing session calls at fixed points to append UTC events to a log outside the repository, and a renderer, `scripts/timeline/render.sh`, that merges that log with `summary.json`, commit times, comment and review times and CI job steps into a table for the pull request description. Durations come only from recorded start and end pairs. Three points are open for Hari (section 12), so this revision cannot be approved under standing approval.

## 1. Goal and authority

Goal: each pull request's description carries a machine-stamped timeline of its session or sessions, recorded the same way by Claude Code and Codex, so that where the time went can be read without a separate reconstruction.

Authority, in creation order:
- The issue body of #443. Its `updated_at` is 2026-11-13T08:41:10Z (the REST API gives no separate body edit time). `ready-for-agent` was applied at 2026-11-13T07:13:04Z (issue events). No comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- There is no comment headed "## Decision" or "## Approval" on the issue (catch-up of 2026-11-13, 2 comments). There is no split, no parent item and no item plan template entry; the whole issue is one pull request.

Evidence, not authority (docs/agents/factory.md "Item lifecycle" step 3): [comment 6848185690](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6848185690) reports one Codex desktop cloud task in which neither a repository `SessionStart` nor a `PostToolUse` hook ran; [comment 6848208909](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6848208909) adds that no hook trust prompt was shown and says proposal 4 "applies to Claude Code only". Neither is headed as a decision, so section 12 question 1 asks Hari to decide it.

Proposal mapping:
- Proposal 1 (experiment): done, per comment 6848185690. No work in this pull request.
- Proposal 2 (recorder and vocabulary): sections 3.1 to 3.3, requirements 1 to 9.
- Proposal 3 (renderer): sections 3.4 to 3.6, requirements 10 to 19.
- Proposal 4 (hooks as a secondary path): the issue body makes it conditional on the experiment showing that Codex cloud runs hooks. The experiment showed it does not, so this revision does not plan it. Open question 1.
- Proposal 5 (template line): section 3.8, requirement 21.
- Proposal 6 (cold review flags a receipt whose final gate has no recorded start and end): not planned; it conflicts with the current gate order. Open question 2.

Protected paths: `/scripts/` (`.github/CODEOWNERS` line 13) and `/.*/`, which covers `.github/` and `.agents/` (line 7). This pull request merges only through Hari (AGENTS.md GOVERNANCE "Merge rule"). It changes no file under `.github/workflows/`.

## 2. Scope

Files this pull request may change:
- `scripts/timeline/event.sh` (new)
- `scripts/timeline/render.sh` (new)
- `scripts/timeline/render.mjs` (new; the renderer's program, which `render.sh` runs)
- `scripts/tests/timeline.test.mjs` (new)
- `.agents/skills/implement-plan/SKILL.md`, by adding the section of 3.7 only. `.claude/skills/implement-plan` is a symlink to that folder and does not change.
- `.github/pull_request_template.md`, by adding the line of 3.8 only.
- `items/443/` (this plan, the receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change: `.github/workflows/` and `docs/operations/`; `.claude/settings.json`, `.claude/hooks/` and `.codex/` (open question 1); `.agents/skills/repo-review/` and every other skill (open question 2); `AGENTS.md`, `CLAUDE.md`, `docs/`; `scripts/verify-dotnet.sh` and every other existing file under `scripts/`; `package.json` and `package-lock.json`; `src/`, `tests/`, `results/`, `scenarios/`; any `.csproj`, `Directory.*.props` or lock file.

Tools: `event.sh` uses bash, the coreutils `date`, and `git`, whose failure gives `null` fields (3.2). `render.sh` runs `node` on `render.mjs`, which imports only Node built-in modules (`node:fs`, `node:child_process`, `node:process` and similar), so it needs no `npm ci`; it calls `git log` only for `--git-range`. The test uses `node:test` and `node:assert`. No `jq`, no `python3`, no `gh` inside these scripts: the session fetches GitHub data with `gh api` as 3.7 states, under AGENTS.md "GitHub issues and pull requests".

## 3. External contracts

UTC timestamps in this plan have the form `YYYY-MM-DDTHH:MM:SSZ`, as `date -u +%Y-%m-%dT%H:%M:%SZ` prints and as `summary.json` uses (`scripts/verify-dotnet.sh` lines 201 and 228 on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90).

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

`scripts/timeline/render.sh --branch <name> [--log <file>] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]`

- `render.sh` runs `node "<its directory>/render.mjs" "$@"` and exits with node's status.
- Output: the block of 3.6 on standard output. Exit 0.
- Exit 2, nothing on standard output, one line on standard error, for:
  - an unknown flag, a flag without a value, or a repeated `--branch`, `--log`, `--git-range` or `--since`: `Usage: scripts/timeline/render.sh --branch <name> [--log <file>] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]`;
  - no `--branch`: the same usage line;
  - `--since` not in the UTC form: `--since must be a UTC time like 2026-11-13T10:00:00Z.`;
  - a file that exists but cannot be read: `Cannot read the file given to --<flag>.`;
  - a `--summary`, `--comments`, `--reviews` or `--jobs` file that is missing or not valid JSON of the shape in 3.5: `The file given to --<flag> is not valid input.`;
  - a `--log` line that is not a JSON object with keys `v` (1), `ts` (UTC form), `event` (a string), `detail` (a string), `branch` (string or null), `head` (string or null): `Line <n> of the --log file is not a timeline event.` (`<n>` counts from 1; empty lines are skipped and counted);
  - `git log` failing for `--git-range`: `git log failed for --git-range.`
- A `--log` file that does not exist is not an error: the log source is `absent` (3.6). This covers a container replaced during the session.
- No message echoes a path or a file's content.

### 3.5 Inputs and the rows they give

Each row has: time (UTC form), source, event, detail, seconds (a whole number or empty).

- `--log`: one row per line whose `branch` equals `--branch` exactly; other lines, `null` branch included, give no row. Time `ts`; source `log`; event and detail as recorded. Seconds, by pairing in time order (ties in line order): a `gate.pass`, `gate.fail` or `gate.stop` with detail `d` pairs with the latest earlier unpaired `gate.start` with detail `d`; a `review.end` whose detail starts with mode `m` pairs with the latest earlier unpaired `review.start` with detail `m`. A paired end row's seconds are its time minus the start's time. Every other log row has empty seconds.
- `--summary`: a `summary.json` of `scripts/verify-dotnet.sh` (lines 199 to 249 on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90), with `schema_version` 1. One row per `stages` entry, at its `end_utc`: source `summary`, event `verify.stage`, detail `<label> exit=<exit_code>`, seconds `end_utc` minus `start_utc`. Then one row at `ended_utc`: event `verify.run`, detail `tier=<tier> outcome=<outcome> exit=<exit_code>`, seconds `ended_utc` minus `started_utc`. `elapsed_s` is not used. Another `schema_version` is not valid input.
- `--git-range <base>..<head>`: one row per commit that `git log --format=%H%x09%ct <base>..<head>` prints, run in the current directory: time the committer time `%ct` in UTC; source `git`; event `commit`; detail the first 12 characters of the hash; seconds empty. No commit message is read.
- `--comments`: a JSON array from `GET /repos/{owner}/{repo}/issues/{issue_number}/comments`, which also lists pull request comments. One row per element: time `created_at`; source `comment`; event `comment`; detail the decimal `id`; seconds empty. No body or login is read.
- `--reviews`: a JSON array from `GET /repos/{owner}/{repo}/pulls/{pull_number}/reviews`. One row per element with a non-null `submitted_at`: time `submitted_at`; source `review`; event `review`; detail `<state> by <user.login> at <first 12 characters of commit_id>`, with `user.login` read as `unknown` when `user` is null and the `at` part left out when `commit_id` is null; seconds empty.
- `--jobs`: a JSON object from `GET /repos/{owner}/{repo}/actions/runs/{run_id}/jobs`, with key `jobs`. Per job with non-null `started_at`: one row per step with non-null `started_at`, in `number` order, then one row for the job. A step or job with non-null `completed_at` is placed at `completed_at` with seconds `completed_at` minus `started_at`; with null `completed_at`, at `started_at` with empty seconds. Source `ci`; event `ci.step` with detail `<job name> / <step name> <conclusion>`, or `ci.job` with detail `<job name> <conclusion>`; a null `conclusion` is written `pending`.
- GitHub times may carry fractional seconds or an offset; they are converted to the UTC form, truncating fractions. A time that cannot be parsed makes the file not valid input.
- `--since <t>` drops every row from every source whose time is before `t`. Without `--since`, `t` is the earliest time among the log rows; with no log rows, nothing is dropped. Log pairing (above) runs before the drop.

### 3.6 Output block

Rows are sorted by time; equal times keep source order `log`, `summary`, `git`, `comment`, `review`, `ci`, then input order. Within one summary file, its stage rows come before its run row at equal times.

In the detail cell, `&` becomes `&amp;` first, then `<` becomes `&lt;`, `>` becomes `&gt;` and `|` becomes `\|`. Nothing else is changed.

The block, literal apart from the bracketed parts:

```
<details><summary>Timeline <first row time> to <last row time> (UTC)</summary>

hook data: <present|absent>
Rows: log <n|absent>, summary <n|absent>, git <n|absent>, comment <n|absent>, review <n|absent>, ci <n|absent>.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| <time> | <source> | <event> | <detail> | <seconds or empty> |

</details>
```

- `hook data: present` when at least one log row's event starts with `hook.`; otherwise `absent`. Under this revision no recorder writes a `hook.` event (3.3 rejects it), so the line is `absent`; open question 1 decides whether a hook path is added.
- `Rows`: per source, the number of rows in the table, or `absent` when its flag was not given, or for `--log`, when the file does not exist.
- With no rows, the summary line is `<summary>Timeline: no rows</summary>` and the table keeps its two header lines.
- The output ends with `</details>` and one newline.

### 3.7 Skill section

`.agents/skills/implement-plan/SKILL.md` gains this section after "## Steps", literal:

```
## Timeline record

Record this session's events with `scripts/timeline/event.sh <event> [<detail>]`, whose header lists each event, the detail it takes and when it applies. Record each event when it happens, not afterwards: `gate.start` before, and `gate.pass`, `gate.fail` or `gate.stop` after, each gate command it names; `focused.run` for each focused test run; `review.start` when cold-review inputs are frozen under docs/agents/pull-requests.md rule 5 and `review.end` when the complete report arrives; `env.interrupted` and `agent.resumed` as they occur; `decision.received` for each new decision or approval comment the catch-up or a refresh finds; `handoff` just before a handoff or completion comment. A failed `event.sh` call does not stop the work; note it in the authored receipt.

Before each edit of the pull request description, fetch the issue's and the pull request's comments, the pull request's reviews and the jobs of each workflow run on its head with `gh api`, under AGENTS.md "GitHub issues and pull requests", into files outside the repository. Then run `scripts/timeline/render.sh` with the branch, the timeline log, each `summary.json` this session produced, the commit range from the merge base with `origin/main` to the head, and those files. Put its output under the self-review record's "Timeline record" line, as this session's block. Each session adds or replaces only its own block and leaves earlier sessions' blocks unchanged. Commit no timeline output (docs/agents/pull-requests.md rule 8).
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
14. WHEN `--log` names a file that does not exist THEN the log source is `absent` and the exit status is 0.
15. WHEN an input fails a check of 3.4 THEN exit 2 with that message and no standard output.
16. WHEN `--since` is given THEN rows before it are dropped from every source; WHEN it is not THEN the earliest log row's time is used.
17. WHEN a detail holds `&`, `<`, `>` or `|` THEN the cell holds the escaped form of 3.6.
18. WHEN a CI step or job has null `started_at` THEN it gives no row; WHEN only `completed_at` is null THEN its row is at `started_at` with empty seconds and conclusion `pending` if null.
19. WHEN no source yields a row THEN the block has `<summary>Timeline: no rows</summary>` and the two header lines only.
20. WHEN the implementing session works under `implement-plan` THEN the skill carries the section of 3.7 verbatim.
21. The pull request template carries the line of 3.8 at the place stated.

## 5. Invariants and safety rules

- The timeline log is never inside the repository (check 5), and no timeline output is committed (docs/agents/pull-requests.md rule 8).
- The log holds only the fixed vocabulary, branch names matching 3.2's pattern, commit hashes and comment IDs: no free-text paths or host names (AGENTS.md hard safety rule 7; issue body proposal 2). The rendered block adds only GitHub step and job names, review states, reviewer logins and `summary.json` labels.
- Seconds are only differences of recorded start and end pairs, computed by the script (docs/agents/pull-requests.md rule 4; AGENTS.md hard safety rule 11). No row's seconds come from a gap between unrelated rows.
- No workflow changes (AGENTS.md hard safety rule 8). No hook configuration changes.
- `event.sh` failing never blocks a gate or a review (3.7).

## 6. Standing fixture rule

This plan adds new validators and a new test file; no existing test exercises these scripts, so no existing fixture can be rejected. The rule stands for the implementer all the same: when an existing test's fixture is rejected by a new check, the implementer does exactly one of (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## 7. Test map

All cases are in `scripts/tests/timeline.test.mjs`, run with `node --test scripts/tests/timeline.test.mjs`. Each case uses its own temporary directory from `fs.mkdtemp(os.tmpdir() + "/timeline-")`, outside the repository, and sets `PARCHWELL_TIMELINE_LOG` to a file in it unless the case says otherwise. `event.sh` cases run the script from the repository checkout. `UTC` below means the regular expression `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$`.

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

Git: a temp repository with an empty initial commit `A`, then commits `B` and `C` made with `GIT_COMMITTER_DATE` `2026-11-13T10:10:00Z` and `2026-11-13T10:15:00Z`; `--git-range A..C`, run with the temp repository as the current directory. `<B12>` and `<C12>` are the first 12 characters of their hashes.

`comments.json`: `[{"id":1,"created_at":"2026-11-13T10:25:00Z","body":"x | y"},{"id":2,"created_at":"2026-11-13T08:00:00Z"}]`.

`reviews.json`: `[{"id":7,"user":{"login":"chatgpt-codex-connector[bot]"},"state":"COMMENTED","submitted_at":"2026-11-13T10:40:00.123Z","commit_id":"0123456789abcdef0123456789abcdef01234567"},{"id":8,"user":null,"state":"PENDING","submitted_at":null,"commit_id":null}]`.

`jobs.json`: `{"total_count":1,"jobs":[{"name":"dotnet-tests (A|B) <x> & y","started_at":"2026-11-13T10:35:00Z","completed_at":"2026-11-13T10:39:30Z","conclusion":"success","steps":[{"name":"Run","number":1,"started_at":"2026-11-13T10:35:10Z","completed_at":"2026-11-13T10:39:00Z","conclusion":"success"},{"name":"Skipped","number":2,"started_at":null,"completed_at":null,"conclusion":"skipped"},{"name":"Upload","number":3,"started_at":"2026-11-13T10:39:05Z","completed_at":null,"conclusion":null}]}]}`.

Command for `render_full`: `render.sh --branch claude/443-session-timeline --log log.jsonl --summary summary.json --git-range A..C --comments comments.json --reviews reviews.json --jobs jobs.json`. Expected standard output, exactly (with `<B12>` and `<C12>` substituted), exit 0:

```
<details><summary>Timeline 2026-11-13T10:00:00Z to 2026-11-13T11:02:00Z (UTC)</summary>

hook data: absent
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

This one case covers requirements 10, 11 (lines 2 and 5), 12 (lines 9 to 11), 13 (line 7), 16 without `--since` (comment 2 dropped), 17 and 18 (steps 2 and 3).

| Req | Case | Inputs | Literal expectations |
|---|---|---|---|
| 10 to 13, 16 to 18 | `render_full` | above | output above, exit 0 |
| 16 | `render_since` | `--since 2026-11-13T10:30:00Z`, `--log log.jsonl`, `--comments comments.json` only | rows: `10:30:00Z focused.run`, `11:00:00Z gate.start`, `11:01:00Z gate.start`, `11:02:00Z gate.stop` with seconds `60`; `Rows: log 4, summary absent, git absent, comment 0, review absent, ci absent.` A row at exactly the `--since` time is kept |
| 14 | `render_missing_log` | `--log <tmp>/none.jsonl --comments comments.json` | exit 0; `Rows: log absent, ..., comment 2, ...`; both comment rows present (no log rows, so nothing dropped) |
| 19 | `render_empty` | `--branch x` only | exit 0; output equals `<details><summary>Timeline: no rows</summary>\n\nhook data: absent\nRows: log absent, summary absent, git absent, comment absent, review absent, ci absent.\nSeconds are computed only from recorded start and end pairs.\n\n| UTC | Source | Event | Detail | Seconds |\n|---|---|---|---|---|\n\n</details>\n` |
| 15 | `render_bad_inputs` | each separately: no `--branch`; `--frobnicate 1`; `--branch a --branch b`; `--since 2026-11-13 10:00:00`; `--comments <tmp>/missing.json`; `--comments` file `{"a":1}`; `--summary` file with `"schema_version":2`; `--log` file whose line 2 is `{"v":1}` (line 1 valid); `--git-range nope..nada` | each exit 2, empty stdout, stderr exactly the message of 3.4 for that input (`Line 2 of the --log file is not a timeline event.` for the log case) |
| 15 | `render_unreadable` | `--log` file with mode `000`, skipped when the test runs as root (`process.getuid() === 0`), with the skip reason printed | exit 2; `Cannot read the file given to --log.` |
| 11 | `render_hook_present` | a log line with event `hook.post`, detail `""`, on the branch | `hook data: present` |

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

- D1: the GitHub REST responses have the fields 3.5 reads. Evidence: the GitHub REST documentation pages for "List issue comments", "List reviews for a pull request" and "List jobs for a workflow run" (API version 2022-11-28), read with WebFetch in this planning session on 2026-11-13. Offline tests use fixtures of that shape (7.2); acceptance command 3 renders real responses.
- D2: `summary.json` has the shape `scripts/verify-dotnet.sh` lines 199 to 249 write on `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90. This is on `main`, not an assumption; acceptance command 2 renders a real file.
- D3: Node, with only built-in modules, runs in every session that publishes a pull request. Evidence: this Claude Code cloud sandbox has `node` v22.23.4 (`node --version`, 2026-11-13, idle); docs/agents/pull-requests.md rule 8 already requires `npm run check:docs`, a Node command, before every cold review; `items/404/burst-adr-receipt.md` line 32 records `npm run check:docs` completing in a Codex-implemented session, which does not record whether that was a cloud task. Not verified for Codex cloud tasks; open question 3. `render.mjs` must also run on Node 22, the version measured here, although `package.json` line 5 declares `>=24`.
- D4: `${TMPDIR:-/tmp}` keeps a file across tool calls within one session. Evidence: in this Claude Code cloud session a file written to `/tmp` by one Bash call was read back by the next (2026-11-13T15:57:06Z, idle). Not measured in a Codex cloud task; open question 3. A replaced container loses the log, which requirement 14 handles as `log absent`.

## 9. Acceptance commands

1. `node --test scripts/tests/timeline.test.mjs`: exits 0; every case of section 7 passes or is skipped only for the root reasons stated. Evidence: output with pass, fail and skip counts in the receipt.
2. In the implementing session: `scripts/timeline/event.sh gate.start sandbox`, `./scripts/verify-dotnet.sh --tier sandbox`, then `gate.pass` or `gate.fail`; then `render.sh` with `--log` and that run's `summary.json`. Evidence: the rendered block in the receipt, showing the `verify.run` row and the paired gate row.
3. The implementing session renders its own block under 3.7 and puts it in the pull request description. Evidence: the description shows the block with `hook data: absent` and rows from every source given.
4. `npm run check:docs`: no warning in a changed file, or each disposed of under docs/agents/pull-requests.md rule 8.
5. Hosted `.NET verification` on the final head passes (this plan changes no .NET source; the check runs as for every pull request). Evidence: run link in the description.

## 10. Sources

- Issue #443 body (`updated_at` 2026-11-13T08:41:10Z), comments 6848185690 and 6848208909, issue events (label `ready-for-agent` at 2026-11-13T07:13:04Z), read with `gh api` on 2026-11-13.
- `main` at 662685a0d54ced8fc97a438bf6fac56023fa0b90: `scripts/verify-dotnet.sh` lines 199 to 249 (`summary_json`) and 252 to 270 (results directory, exit trap); `.github/pull_request_template.md` lines 8 to 18; `.agents/skills/implement-plan/SKILL.md` lines 1 to 19; `.agents/skills/repo-review/SKILL.md` lines 37 to 41; `.claude/settings.json`; `.github/CODEOWNERS` lines 5 to 13; `docs/agents/pull-requests.md` rules 4 and 8 and "Verification gates" (lines 24, 48 to 59); `package.json` lines 5 to 9.
- GitHub REST API documentation, version 2022-11-28: https://docs.github.com/en/rest/issues/comments, https://docs.github.com/en/rest/pulls/reviews, https://docs.github.com/en/rest/actions/workflow-jobs, read with WebFetch on 2026-11-13.
- https://learn.chatgpt.com/docs/environments/cloud-environments.md, read with WebFetch on 2026-11-13: it does not name a default image or list preinstalled tools; it says Codex "identifies runtimes, package versions, tools, and services from your repositories".
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of log lines, comments, reviews, jobs and summary stages is known only when `render.sh` runs; each is one pass over its input, and pairing is one pass over the log. No cap is set, because the output is one table per session and its length is the work done. Implementation effort has no runtime-sized step: the test cases are listed in section 7.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body, the plan adds: a refusal of log paths inside the repository (check 5); per-event detail patterns, where the issue says only "fixed vocabulary"; a `head` field; the `Rows` line and the `absent` marker for each source; GitHub reviews as a source, which the issue's "PR comment times" does not name explicitly; one block per session. It interprets `focused.run` as one focused test run of "Verification gates" step 1 and `gate.*` as the sandbox, full and documentation gate commands; the issue does not define them. Proposals 4 and 6 are not planned (open questions 1 and 2).

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: the GitHub REST response shapes (D1), checked against the official documentation; Node and `/tmp` persistence in Codex cloud tasks (D3, D4), which no source settles and which open question 3 lists.

## 12. Open questions for Hari

1. Proposal 4, hooks as a secondary data source. The issue body says to add hooks to `.claude/settings.json` and `.codex/hooks.json` "only if the experiment shows Codex cloud runs them"; the experiment showed it does not. Comment 6848208909 says proposal 4 "applies to Claude Code only", but it is not headed as a decision. Should this pull request add a Claude Code hook path (which would add `hook.*` events to the vocabulary, a `PostToolUse` entry in `.claude/settings.json`, and its tests), or leave proposal 4 out, as this revision does?
2. Proposal 6, "Cold review flags a receipt whose final gate has no recorded start and end." Under docs/agents/pull-requests.md "Verification gates" (lines 52 to 57, from issue #440's decisions), the cold review (step 2) runs before the pre-PR local gate (step 3), and the final gate is the hosted check on the final head (step 4). So no receipt a cold reviewer sees can hold the final gate's start and end. Should proposal 6 be dropped, or changed, for example to the focused-test runs before the review, or to a check at the merge rule's final-head verification?
3. Node and `/tmp` persistence in Codex cloud tasks (D3, D4). No official page or recorded Codex cloud output shows that `node` is installed or that a file in `/tmp` survives between tool calls. Do you accept this risk by name, or should the implementing session first run a check in a Codex cloud task?

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in section 7.
