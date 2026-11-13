# Plan: session window for the timeline block

Issue: [#465](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465). Pull request short name: `session-window`. Revision 2.

This plan adds a `session.start` event to the timeline recorder and makes the renderer start each block at the last `session.start` line in the log for the branch, in file order, so a block shows only its own session's rows and its own pre-PR local gate, whatever times the log's lines carry. Log pairing then runs only inside that window, so an earlier session's gate can neither appear as `recorded` nor pair with this session's end event. A new `window start:` line in the block shows where the window began and why, so a session that never recorded `session.start` is visible.

All line references are on `main` at `8e8d5715b89e610ec221bfb0c997c0ea1cb952b7` unless stated otherwise. Revision 1 read `main` at `d2a592f4da0f337a45abea61f9d61e48f9772db5`, the merge of pull request #463. Between the two, of the files this plan changes, only `.agents/skills/implement-plan/SKILL.md` changed, at line 16 (step 1); its "Timeline record" section (lines 21 to 25) and every other line this plan cites are unchanged (`git diff d2a592f4 8e8d5715 -- scripts .agents .github/CODEOWNERS`, 2026-11-13).

Changes since Revision 1, all from [decision 6860837873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860837873):
- 3.3 case 2: the window starts at the last `session.start` line in file order and keeps every later line, whatever its time (decision point 1); rows of other sources are cut at that line's time (point 2). Revision 1 ordered branch log entries by time, then line.
- 3.1: the option comparison records the decision; per-session logs are not chosen (point 5).
- 3.2: the header's description line says "last one in the log", not "latest one".
- Section 4: requirement 4 restated; requirement 13 added (point 4).
- 7.2: new case `render_session_start_line_order` with block C (point 4). 7.1: two assertions added to `render_log_empty_file` and `render_log_other_branch`, the P2 of [plan check 6860690311](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860690311).
- Section 8: deployment assumption D1, one writer per log, accepted by name in point 3; the sentence "This plan adds no assumption about the deployed container, image or runtime" is corrected (point 3); the clock comparison of other sources is stated.
- Sections 1, 6, 10 and 11 updated to match.

## 1. Goal and authority

Goal: each session's rendered timeline block covers only that session, as `.agents/skills/implement-plan/SKILL.md` line 25 already requires ("Each session adds or replaces only its own block").

Authority, in creation order:
- The issue body of #465. `ready-for-agent` was applied at 2026-11-13T17:40:41Z (issue events, read with `gh api` on 2026-11-13). There is no item state file (`items/465/` does not exist on `main`), no split, no parent item and no item plan; the whole issue is one pull request. No comment names an implementing tool: the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- [Decision 6860837873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860837873) (created 2026-11-13T17:59:39Z, heading `## Decision`), answering the handoff [6860699829](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860699829) on Revision 1 and blocking finding 1 of plan check [6860690311](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860690311). Its five points set the window by line order (point 1), the time cutoff for other sources (point 2), the accepted single-writer risk (point 3), a test (point 4), and reject per-session logs (point 5). This revision applies each point; the map is in "Changes since Revision 1" above.
- Other comments, evidence only: the Revision 1 plan comment [6860599034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860599034), the plan check 6860690311 and the handoff 6860699829. Catch-up of 2026-11-13: these four comments, none edited after creation; the issue's `updated_at` is 2026-11-13T17:59:39Z.
- The issue body says "Depends on #463 merging". Pull request #463 merged at 2026-11-13T17:38:56Z with merge commit `d2a592f4da0f337a45abea61f9d61e48f9772db5` (`gh api .../pulls/463`), which is the `main` this plan reads.
- The issue body's "Proposal (for the plan to decide)" leaves the choice between a session-start event and a per-session log path to this plan, and asks the plan to compare them. Section 3.1 compares them and chooses the session-start event.

Evidence, not authority: the Codex finding on #463, [review comment 4566476190](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/463#discussion_r4566476190), and the reply [4566478347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/463#discussion_r4566478347), both read with `gh api` on 2026-11-13. The reply states the weakness is real and that the #443 plan (Revision 3, `items/443/plan-session-timeline.md` at `1e34eaf6d5e5a5bf1bbef07974618dae205f31c6`) pins the current behaviour.

This plan amends these contracts of that #443 plan, and keeps every other contract of its sections 3.1 to 3.8: the event vocabulary of its 3.3 (one event added), the `--since` and pairing rules of its 3.5 (the window and the pairing order), the block of its 3.6 (one line added) and the skill section of its 3.7 (one sentence added). The #443 plan is the source of the current behaviour only; this plan is the contract for the changed parts.

Proposal mapping:
- Proposal 1 (session-start event; renderer default start at the latest one; `--since` stays an override): sections 3.2 and 3.3, requirements 1 to 7 and 13. "Latest" is the last in file order (decision 6860837873 point 1).
- Proposal 2 (gate and exit 3 only from rows inside the window): requirements 8 and 9. The renderer's gate check already reads only table rows (`scripts/timeline/render.mjs` lines 364 to 370); the change is that pairing, which gives those rows their seconds, also runs only inside the window.
- Proposal 3 (two-session test): test map 7.2, `render_two_sessions` and `render_two_sessions_recorded`.
- Proposal 4 (skill section): section 3.4, requirement 11.

Protected paths: `/scripts/` (`.github/CODEOWNERS` line 13) and `/.*/`, which covers `.agents/` (line 7). This pull request merges only through Hari (AGENTS.md GOVERNANCE "Merge rule"). It changes no file under `.github/workflows/`.

## 2. Scope

Files this pull request may change:
- `scripts/timeline/event.sh`: the vocabulary header (lines 16 to 40) and the event check (lines 54 to 70), for the event of 3.2 only.
- `scripts/timeline/render.mjs`: the window, pairing and output of 3.3. Its header comment (lines 1 to 10) may be updated to describe them.
- `scripts/tests/timeline.test.mjs`: the changes and new cases of section 7.
- `.agents/skills/implement-plan/SKILL.md`: the "Timeline record" section (lines 21 to 25), by the sentence of 3.4 only. `.claude/skills/implement-plan` is a symlink to that folder and does not change.
- `items/465/` (this plan, the receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change: `scripts/timeline/render.sh`; `.github/` (the pull request template line of the #443 plan 3.8 stays); `.github/workflows/` and `docs/operations/`; `.claude/settings.json`, `.claude/hooks/` and `.codex/`; every other skill; `AGENTS.md`, `CLAUDE.md`, `docs/`; every other file under `scripts/`; `package.json` and `package-lock.json`; `src/`, `tests/`, `results/`, `scenarios/`; any `.csproj`, `Directory.*.props` or lock file; `items/443/`.

Tools: unchanged from the #443 plan section 2. `render.mjs` keeps importing only Node built-in modules.

## 3. External contracts

UTC times have the form `YYYY-MM-DDTHH:MM:SSZ`, as in the #443 plan section 3.

### 3.1 Choice: session-start event, not a per-session log

The issue asks the plan to compare two options.

Option A, a session-start event (chosen; decision 6860837873 point 5 rejects option B):
- One default log path stays (`scripts/timeline/event.sh` lines 77 to 80). The session makes one extra call, `event.sh session.start`, and `render.sh` needs no new flag.
- The boundary is visible: the `session.start` row appears in the table, and the `window start:` line (3.3) names where the window began. A session that never recorded `session.start` shows the fallback text, not a silent mix.
- Inside the log, the window starts at the `session.start` line itself, in file order (3.3). `event.sh` appends each line with `>>` (line 110), so file order is call order, and neither the one-second resolution of `ts` (line 107) nor a host clock that disagrees between sessions or steps back moves the window for log entries.
- Limitation: rows of the other sources carry only times, so they are cut at the `session.start` line's `ts` (decision 6860837873 point 2). A comment, commit, review, job or summary row in the same second as `session.start` is kept, and a host clock that differs from GitHub's or git's moves that cutoff for those rows (section 8).
- Limitation: one log serves one session at a time (section 8, D1). Two sessions appending to the same log at once would interleave their lines.

Option B, a per-session log path:
- `event.sh` reads the path from `PARCHWELL_TIMELINE_LOG` on each call (lines 77 to 80). A per-session path would have to be given on every `event.sh` call and to `render.sh --log`. A call that omits it writes to the default log and exits 0, and that row is then missing from the session's block with no sign in the block. In this Claude Code cloud session, the Bash tool's description states "Shell state (env vars, functions) does not persist" (tool metadata read on 2026-11-13, not official documentation; not verified for Codex), so an exported variable would not carry the path from one call to the next.
- A unique path per session needs a session name that the vocabulary does not have.
- Advantage: the logs are physically separate, so the window would not depend on line order or on one writer per log.

Option A is chosen because it keeps one log and one default, as the issue body notes, and because a missed call shows in the block, while a missed path under option B does not. Decision 6860837873 point 5 confirms that option B is not chosen.

### 3.2 New event in `event.sh`

`event.sh` accepts one more event, `session.start`, whose detail pattern is "empty only" (`^$`), as for `env.interrupted` and `agent.resumed`. Every other check of the #443 plan 3.3 is unchanged, with the event list extended by this one event. A non-empty detail gives exit 2 and `Invalid detail for session.start.`, nothing written.

The vocabulary header gains this entry, literal, placed directly after the line `#   agent.resumed     empty only` and its description line (line 36), before `#   handoff`:

```
#   session.start     empty only
#       The session's first event on its pull request branch, recorded once.
#       render.sh starts the block at the last one in the log for this branch.
#       A resume within the same session records agent.resumed instead.
```

The name field is padded to the same column as the other entries: `session.start` followed by five spaces.

### 3.3 Window, pairing and output of `render.sh`

The command line, exit statuses, input checks, error messages and row shapes of the #443 plan 3.4 and 3.5 are unchanged, except for the window and pairing rules below, which replace the last bullet of its 3.5 ("`--since <t>` drops every row ... Log pairing (above) runs before the drop.").

Branch log entries: the `--log` lines whose `branch` equals `--branch` exactly, as today (`render.mjs` line 155). File order is the order of their lines in the `--log` file. A `session.start` entry is any branch log entry whose `event` is `session.start`, whatever its `detail`.

The window start `T` and the kept log entries, decided in this order:
1. With `--since t`: `T` is `t`. Kept log entries: those with time at or after `T`. `session.start` entries play no part.
2. Otherwise, when at least one `session.start` entry exists: `S` is the last `session.start` entry in file order, and `T` is its time. Kept log entries: `S` and every branch log entry after it in file order, whatever its time. Every entry before `S` in file order is dropped, whatever its time: at, before or after `T`. Times play no part in choosing `S` or the kept log entries (decision 6860837873 point 1).
3. Otherwise, when at least one branch log entry exists: `T` is the earliest entry's time. Every branch log entry is kept.
4. Otherwise: there is no `T`, and nothing is dropped from any source.

Rows of `--summary`, `--git-range`, `--comments`, `--reviews` and `--jobs` are kept when their time is at or after `T`, or always when there is no `T`, as today (decision 6860837873 point 2 for case 2). The kept log entries are not filtered again by `T`: in case 2 a kept entry whose time is before `T` stays in the table, sorted by time with the other rows as today (`render.mjs` line 361), and the block's summary line then starts at that entry's time.

Pairing: the pairing rule of the #443 plan 3.5 (an end pairs with the latest earlier unpaired start with the same detail or mode, in time order, then line order, as `pairLog` does today at `render.mjs` lines 167 to 198) runs only over the kept log entries. An end whose start was dropped has empty seconds. Log rows are the kept entries after pairing.

The pre-PR local gate check of the #443 plan 3.6 is unchanged in wording; because it reads table rows and their seconds, it now sees only rows and pairs inside the window.

Output: the block of the #443 plan 3.6 gains one line, directly after the `event log:` line and before the `pre-PR local gate:` line, literal apart from `<T>`, which is `T` in UTC form:

| Case | Line |
|---|---|
| 1 | `window start: <T> (--since)` |
| 2 | `window start: <T> (session.start)` |
| 3 | `window start: <T> (earliest log row; no session.start recorded)` |
| 4 | `window start: none (no log rows; nothing dropped)` |

The line appears in every block, the no-rows block included. Every other line of the block is unchanged. The `event log:` line is still decided on the whole `--log` file, before the window. `Rows: log <n>` counts the log rows in the table, `session.start` rows included.

### 3.4 Skill section

In `.agents/skills/implement-plan/SKILL.md`, the first paragraph of "## Timeline record" (line 23) gains one sentence, inserted directly after its first sentence, which ends "the detail it takes and when it applies.", and before "Record each event when it happens, not afterwards:". The inserted sentence, literal, with one space before and after it:

```
Record `session.start` once, as this session's first event on its pull request branch, as soon as that branch is checked out; a resume within this session records `agent.resumed` instead.
```

The rest of the section stays byte for byte. The full section after the change, literal:

```
## Timeline record

Record this session's events with `scripts/timeline/event.sh <event> [<detail>]`, whose header lists each event, the detail it takes and when it applies. Record `session.start` once, as this session's first event on its pull request branch, as soon as that branch is checked out; a resume within this session records `agent.resumed` instead. Record each event when it happens, not afterwards: `gate.start` before, and `gate.pass`, `gate.fail` or `gate.stop` after, each gate command it names; `focused.run` for each focused test run; `review.start` when cold-review inputs are frozen under docs/agents/pull-requests.md rule 5 and `review.end` when the complete report arrives; `env.interrupted` and `agent.resumed` as they occur; `decision.received` for each new decision or approval comment the catch-up or a refresh finds; `handoff` just before a handoff or completion comment. A failed `event.sh` call does not stop the work; note it in the authored receipt.

Before each edit of the pull request description, fetch the issue's and the pull request's comments, the pull request's reviews and the jobs of each workflow run on its head with `gh api`, under AGENTS.md "GitHub issues and pull requests", into files outside the repository. Then run `scripts/timeline/render.sh` with the branch, the timeline log, each `summary.json` this session produced, the commit range from the merge base with `origin/main` to the head, and those files. Pass `--local-gate not-applicable` or `--local-gate earlier-session`, the value that names the reason, only when docs/agents/pull-requests.md "Verification gates", its last two paragraphs, does not require this session to run its step 3; otherwise leave the default. Put its output under the self-review record's "Timeline record" line, as this session's block, also when it exits 3: that exit means the pre-PR local gate has no recorded start and end, and the block shows it. State any exit 3 in the authored receipt. Each session adds or replaces only its own block and leaves earlier sessions' blocks unchanged. Commit no timeline output (docs/agents/pull-requests.md rule 8).
```

The section stays at the end of the file, and the file ends with its last line and one newline, as the `skill_section` test requires today (`scripts/tests/timeline.test.mjs` lines 511 to 518).

## 4. Requirements

1. WHEN `event.sh session.start` runs with no detail THEN it appends one line in the format of the #443 plan 3.2 with `"event":"session.start","detail":""` and exits 0.
2. WHEN `event.sh session.start <non-empty detail>` runs THEN it exits 2 with `Invalid detail for session.start.`, writes nothing and does not echo the detail.
3. The vocabulary header of `event.sh` carries the entry of 3.2.
4. WHEN `--since` is absent AND the branch log entries include a `session.start` THEN the window is case 2 of 3.3: log entries before the last `session.start` in file order are dropped, same-second ones included; rows of other sources before its time are dropped; the block has `window start: <T> (session.start)`.
5. WHEN `--since t` is given THEN the window is case 1, whatever `session.start` entries exist, earlier or later than `t`; the block has `window start: <t> (--since)`.
6. WHEN `--since` is absent AND no branch log entry is a `session.start` AND at least one branch log entry exists THEN the window is case 3; the block has `window start: <earliest> (earliest log row; no session.start recorded)`.
7. WHEN `--since` is absent AND no branch log entry exists (the log is missing, empty, or has no line for the branch) THEN nothing is dropped and the block has `window start: none (no log rows; nothing dropped)`.
8. WHEN an end event's matching start lies outside the window THEN the end row's seconds are empty.
9. WHEN `--local-gate` is `required` or absent AND the only recorded `gate.start sandbox` and end pair lies before the window THEN the gate line is `MISSING, no recorded start and end`, standard error is `The pre-PR local gate has no recorded start and end.` and the exit status is 3.
10. WHEN a `session.start` line's `branch` differs from `--branch` or is `null` THEN it does not set the window.
11. The skill section equals the literal of 3.4.
12. Every other behaviour pinned by the #443 plan sections 3.1 to 3.8 is unchanged; the existing cases of `scripts/tests/timeline.test.mjs` keep passing, changed only as section 7.1 lists.
13. WHEN `--since` is absent AND a branch log entry before the last `session.start` line in file order carries a later time than that line THEN it is dropped, and a `gate.start sandbox` and `gate.pass sandbox` pair among such entries does not make the gate `recorded`; WHEN a branch log entry after that line carries an earlier time THEN it is kept; WHEN an earlier `session.start` line carries a later time than the last one THEN the last one in file order still sets the window.

## 5. Invariants and safety rules

- Seconds are only differences of recorded start and end pairs inside one window, computed by the script (docs/agents/pull-requests.md rule 4; AGENTS.md hard safety rule 11). No pair crosses the window start.
- `render.sh` never hides where its window began: every block has the `window start:` line (3.3).
- The log path rules, the fixed vocabulary and the "no free text in the log" invariant of the #443 plan section 5 are unchanged; `session.start` carries no detail.
- No timeline output is committed (docs/agents/pull-requests.md rule 8). No workflow changes (AGENTS.md hard safety rule 8). No hook configuration changes.
- `event.sh` failing never blocks a gate or a review (skill section, unchanged sentence).

## 6. Standing fixture rule

When an existing test's fixture is rejected by a new check of this plan, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

The changes to existing cases that this plan itself requires are pinned in 7.1 and are not fixture-rule changes. A prototype of this revision's 3.3 and 3.2 in a scratch clone outside the repository, at `main` `8e8d5715` (2026-11-13, Node v22.23.4), made exactly four existing cases fail: `render_full`, `render_empty`, `render_empty_declared` and `render_gate_boundaries`; 7.1 pins the change to each. With every 7.1 change applied, the existing file passed 28 cases, failed none and skipped two as root. Any other existing case that fails is handled under this rule, or stops the implementer under section 13 when neither (a) nor (b) fits.

## 7. Test map

All cases are in `scripts/tests/timeline.test.mjs`, run with `node --test scripts/tests/timeline.test.mjs`, under the conventions of the #443 plan section 7 (own temporary directory per case, scripts run by path). `BRANCH` is the test file's constant `claude/443-session-timeline` (line 15). A log fixture line below written `(ts, event, detail)` is `{"v":1,"ts":ts,"event":event,"detail":detail,"branch":BRANCH,"head":null}`; one written `(ts, event, detail, branch)` uses that branch value instead. `GATE_ERR` is the test file's constant (line 38). The literal blocks and literal lines below were produced by the Revision 2 prototype of section 6 on these fixtures, not written by hand.

### 7.1 Changes to existing cases

| Req | Case (line) | Change |
|---|---|---|
| 1 | `event_vocabulary_accepts` (59) | Add the call `["session.start"]` to `calls`, after `["agent.resumed"]`. The expected line count becomes the length of `calls`, 17; each line's event and detail are checked as today. |
| 2 | `event_bad_detail` (98) | Add the call `["session.start", "x"]`; it gets exit 2, `Invalid detail for session.start.` and no `x` in standard error, as the loop checks for every call. |
| 3 | `vocabulary_header` (526) | Add `["session.start", "empty only"]` to `vocab`. |
| 6 | `render_full` (286) | The expected block gains the line `window start: 2026-11-13T10:00:00Z (earliest log row; no session.start recorded)` between `event log: present` and `pre-PR local gate: recorded`. Nothing else in the block changes. |
| 7 | `render_empty` (370), `render_empty_declared` (378) | The `emptyBlock` helper (line 368) gains the line `window start: none (no log rows; nothing dropped)` between `event log: missing` and `pre-PR local gate: <gate>`, so both cases expect it. |
| 7 | `render_log_empty_file` (386) | Inside the loop, after `assert.match(r.stdout, /Rows: log 0, /);`, add `assert.match(r.stdout, /window start: none \(no log rows; nothing dropped\)\n/);`. |
| 7 | `render_log_other_branch` (398) | After `assert.match(r.stdout, /Rows: log 0, /);`, add the same assertion as `render_log_empty_file`. |
| 8, 9 | `render_gate_boundaries` (406), sub-case 6 (line 417) | Rows `(10:00:00, gate.start, sandbox)`, `(10:01:00, gate.fail, sandbox)`, `--since 2026-11-13T10:00:30Z`, input unchanged. Expected changes from exit 0, `recorded` and `\| gate.fail \| sandbox \| 60 \|` to: exit 3; `pre-PR local gate: MISSING, no recorded start and end`; the row `\| 2026-11-13T10:01:00Z \| log \| gate.fail \| sandbox \|  \|` (empty seconds); still no `\| gate.start \|`. The other sub-cases are unchanged. |
| 11 | `skill_section` (511) | `SKILL_SECTION` (line 504) becomes the literal section of 3.4. The assertions are unchanged. |

Sub-case 6 inverts because of requirement 8: the #443 plan paired before the drop, so a pair could cross the window start; this plan pairs after it. The new expectation is stricter, not looser: a row that showed seconds now shows none, and the gate that showed `recorded` now fails visibly.

### 7.2 New cases

| Req | Case | Inputs | Literal expectation |
|---|---|---|---|
| 4, 8, 9 | `render_two_sessions` | Log, in this line order: `(2026-11-13T09:00:00Z, session.start, "")`, `(09:10:00, gate.start, sandbox)`, `(09:15:00, gate.pass, sandbox)`, `(09:16:00, gate.start, sandbox)`, `(09:20:00, handoff, next-session)`, `(10:00:00, session.start, "")`, `(10:05:00, focused.run, fast)`, `(10:06:00, gate.pass, sandbox)` (all on 2026-11-13). `--comments` file `[{"id":11,"created_at":"2026-11-13T09:30:00Z"},{"id":12,"created_at":"2026-11-13T10:10:00Z"}]`. No `--since`, no `--local-gate`. | exit 3; standard error `GATE_ERR`; standard output exactly block A below. The 10:06:00 `gate.pass` has empty seconds although the earlier session left `gate.start sandbox` at 09:16:00 unpaired, and the earlier session's 09:10 to 09:15 pair does not make the gate `recorded`. |
| 4, 9 | `render_two_sessions_recorded` | The first seven lines of `render_two_sessions`, then `(10:06:00, gate.start, sandbox)`, `(10:07:00, gate.pass, sandbox)`; the same `--comments` file. | exit 0; standard error empty; standard output exactly block B below. |
| 4 | `render_session_start_same_second` | Log, in this line order: `(2026-11-13T10:00:00Z, gate.start, sandbox)`, `(2026-11-13T10:00:00Z, session.start, "")`, `(2026-11-13T10:00:00Z, gate.pass, sandbox)`. | exit 3; standard output contains `window start: 2026-11-13T10:00:00Z (session.start)\n`, `pre-PR local gate: MISSING, no recorded start and end\n`, `Rows: log 2, summary absent, git absent, comment absent, review absent, ci absent.\n` and `\| 2026-11-13T10:00:00Z \| log \| gate.pass \| sandbox \|  \|\n`, and does not contain `\| gate.start \|`. |
| 5 | `render_since_overrides_session_start` | Log, in this line order: `(2026-11-13T09:30:00Z, handoff, next-session)`, `(10:00:00, session.start, "")`, `(10:05:00, gate.start, sandbox)`, `(10:06:00, gate.pass, sandbox)`. Two runs. Run 1: `--since 2026-11-13T10:05:00Z`. Run 2: `--since 2026-11-13T09:00:00Z --local-gate not-applicable`. | Run 1: exit 0; contains `window start: 2026-11-13T10:05:00Z (--since)\n`, `pre-PR local gate: recorded\n`, `Rows: log 2, ` and `\| 2026-11-13T10:06:00Z \| log \| gate.pass \| sandbox \| 60 \|\n`; does not contain `\| session.start \|`. Run 2: exit 0; contains `window start: 2026-11-13T09:00:00Z (--since)\n`, `Rows: log 4, ` and `\| 2026-11-13T09:30:00Z \| log \| handoff \| next-session \|  \|\n`. |
| 4, 9, 13 | `render_session_start_line_order` | Log, in this line order: `(2026-11-13T10:40:00Z, session.start, "")`, `(10:41:00, gate.start, sandbox)`, `(10:42:00, gate.pass, sandbox)`, `(10:00:00, session.start, "")`, `(09:55:00, focused.run, fast)` (all on 2026-11-13). `--comments` file `[{"id":21,"created_at":"2026-11-13T09:58:00Z"},{"id":22,"created_at":"2026-11-13T10:30:00Z"}]`. No `--since`, no `--local-gate`. | exit 3; standard error `GATE_ERR`; standard output exactly block C below. The first three lines are dropped although their times are after `T`; the 09:55:00 line is kept although its time is before `T`; comment 21 is dropped by the time cutoff. Under Revision 1's time-then-line rule the 10:40:00 `session.start` would set the window and the 10:41 to 10:42 pair would make the gate `recorded` with exit 0 (derived by hand from Revision 1's 3.3, not run). |
| 6, 10 | `render_session_start_other_branch` | Log, in this line order: `(2026-11-13T09:00:00Z, gate.start, sandbox)`, `(09:30:00, session.start, "", "claude/999-other")`, `(09:35:00, session.start, "", null)`, `(09:40:00, gate.pass, sandbox)`. | exit 0; contains `window start: 2026-11-13T09:00:00Z (earliest log row; no session.start recorded)\n`, `pre-PR local gate: recorded\n`, `Rows: log 2, ` and `\| 2026-11-13T09:40:00Z \| log \| gate.pass \| sandbox \| 2400 \|\n`. |

Block A (`render_two_sessions`):

```
<details><summary>Timeline 2026-11-13T10:00:00Z to 2026-11-13T10:10:00Z (UTC)</summary>

hook data: absent
event log: present
window start: 2026-11-13T10:00:00Z (session.start)
pre-PR local gate: MISSING, no recorded start and end
Rows: log 3, summary absent, git absent, comment 1, review absent, ci absent.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| 2026-11-13T10:00:00Z | log | session.start |  |  |
| 2026-11-13T10:05:00Z | log | focused.run | fast |  |
| 2026-11-13T10:06:00Z | log | gate.pass | sandbox |  |
| 2026-11-13T10:10:00Z | comment | comment | 12 |  |

</details>
```

Block B (`render_two_sessions_recorded`):

```
<details><summary>Timeline 2026-11-13T10:00:00Z to 2026-11-13T10:10:00Z (UTC)</summary>

hook data: absent
event log: present
window start: 2026-11-13T10:00:00Z (session.start)
pre-PR local gate: recorded
Rows: log 4, summary absent, git absent, comment 1, review absent, ci absent.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| 2026-11-13T10:00:00Z | log | session.start |  |  |
| 2026-11-13T10:05:00Z | log | focused.run | fast |  |
| 2026-11-13T10:06:00Z | log | gate.start | sandbox |  |
| 2026-11-13T10:07:00Z | log | gate.pass | sandbox | 60 |
| 2026-11-13T10:10:00Z | comment | comment | 12 |  |

</details>
```

Block C (`render_session_start_line_order`):

```
<details><summary>Timeline 2026-11-13T09:55:00Z to 2026-11-13T10:30:00Z (UTC)</summary>

hook data: absent
event log: present
window start: 2026-11-13T10:00:00Z (session.start)
pre-PR local gate: MISSING, no recorded start and end
Rows: log 2, summary absent, git absent, comment 1, review absent, ci absent.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| 2026-11-13T09:55:00Z | log | focused.run | fast |  |
| 2026-11-13T10:00:00Z | log | session.start |  |  |
| 2026-11-13T10:30:00Z | comment | comment | 22 |  |

</details>
```

Each block ends with one newline after `</details>`.

Boundaries, mapped:
- Window start from `session.start`: below (09:16:00 and 09:30:00 rows, dropped), at (same-second rows before and after the `session.start` line, `render_session_start_same_second`) and above (10:05:00 onwards, kept).
- Two `session.start` entries: the later one in file order wins, with times rising (`render_two_sessions`) and with the earlier line carrying the later time (`render_session_start_line_order`).
- Log entries out of time order around `S`: before `S` with a later time, dropped; after `S` with an earlier time, kept (`render_session_start_line_order`).
- `--since` before and after the last `session.start` (`render_since_overrides_session_start`).
- No `session.start` on the branch, with ones on another branch and on `null` (`render_session_start_other_branch`); none at all (`render_full`).
- No log rows (`render_empty`, `render_empty_declared`).
- Pair inside the window (`render_two_sessions_recorded`), pair across it (`render_two_sessions`, `render_gate_boundaries` sub-case 6).

Not covered by an automated case, with reason: two `session.start` entries with the same time; the rule (last in file order) is the one `render_session_start_line_order` exercises, and times play no part in it.

## 8. Deployment assumptions

None involves a network connection to Azure or Kafka; plan rule 3's open-or-fresh connection and idle-or-load conditions do not apply.

- D1: one session writes to a given timeline log at a time; concurrent sessions appending to the same log are out of scope. Risk accepted by name in [decision 6860837873](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/465#issuecomment-6860837873) point 3. No test covers concurrent writers. Evidence: none taken; this is a scope limit, not an observation, so the open-or-fresh and idle-or-load conditions of plan rule 3 do not apply. Effect if it fails: a `session.start` of one session lands among another session's lines, and that other session's later block starts at it.

Revision 1 stated "This plan adds no assumption about the deployed container, image or runtime". That sentence was wrong (decision 6860837873 point 3) and is replaced by D1 above.

The window for log entries does not depend on the host clock: it is set by file order (3.3 case 2), and `render_session_start_line_order` covers lines whose times run backwards. The cutoff for rows of other sources compares the `session.start` line's `ts`, from the host clock (`event.sh` line 107), with GitHub's and git's times; this is the rule decision 6860837873 point 2 sets, and the same comparison `main` already makes for the default start and `--since` (`render.mjs` lines 348 and 354). A difference between the clocks moves comment, review, commit, job and summary rows near `T` into or out of the block. It cannot change the gate line or the exit status, which read only log rows (`render.mjs` lines 364 to 370). Its offline test is `render_session_start_line_order` (comment 21 dropped, comment 22 kept, by time).

The behaviour does not depend on whether the timeline log survives within a session or across sessions; each outcome is covered offline:
- The earlier session's log survives (the case the issue describes): `render_two_sessions`.
- The log is lost and the session's `session.start` with it: the block shows `event log: missing` and `window start: none (no log rows; nothing dropped)` (`render_empty`), or, when later rows exist, `window start: ... (earliest log row; no session.start recorded)` (`render_full`).

The Node runtime and `/tmp` persistence assumptions D3 and D4 of the #443 plan section 8 are unchanged by this plan; `render.mjs` already depends on them on `main`, and this plan adds no Node API beyond those it already uses.

## 9. Acceptance commands

1. `node --test scripts/tests/timeline.test.mjs`: exits 0; every case of section 7 and every existing case passes, except `event_write_failure` and `render_unreadable`, which skip only when run as root. Evidence: the pass, fail and skip counts in the receipt.
2. End to end with the real scripts, in a temporary directory outside the repository: with `PARCHWELL_TIMELINE_LOG` set to a file there, run `scripts/timeline/event.sh session.start`, `event.sh gate.start sandbox`, `event.sh gate.pass sandbox`, `event.sh session.start`, `event.sh focused.run fast`, then `scripts/timeline/render.sh --branch <current branch> --log <that file>`. Evidence in the receipt: exit 3, a `window start: <UTC> (session.start)` line, `pre-PR local gate: MISSING, no recorded start and end`, and only the second `session.start` and the `focused.run` rows in the table. The calls may share one UTC second, which exercises the line-order rule of 3.3.
3. The implementing session renders its own block under the skill's "Timeline record" section and puts it in the pull request description, with `--local-gate not-applicable`, because this pull request changes no file that docs/agents/pull-requests.md "Verification gates" last paragraph names. Evidence: the block in the description, with a `window start:` line; the receipt records the exit status and the `window start:` case shown.
4. `npm run check:docs`: no warning in a changed file, or each disposed of under docs/agents/pull-requests.md rule 8.
5. Hosted `.NET verification` on the final head passes (this plan changes no .NET source; the check runs as for every pull request). Evidence: run link in the description.

## 10. Sources

- Issue #465 body and events (label `ready-for-agent` at 2026-11-13T17:40:41Z), and its four comments (6860599034, 6860690311, 6860699829 and decision 6860837873), read with `gh api` on 2026-11-13.
- Pull request #463 (`merged_at` 2026-11-13T17:38:56Z, merge commit `d2a592f4da0f337a45abea61f9d61e48f9772db5`) and its review comments 4566476190 and 4566478347, read with `gh api` on 2026-11-13.
- `items/443/plan-session-timeline.md` at `1e34eaf6d5e5a5bf1bbef07974618dae205f31c6` (branch `claude/443-plan`), sections 3.1 to 3.8, 5, 7 and 8.
- `main` at `8e8d5715b89e610ec221bfb0c997c0ea1cb952b7` (re-read for Revision 2; Revision 1 read `d2a592f4da0f337a45abea61f9d61e48f9772db5`): `scripts/timeline/event.sh` lines 1 to 113, in particular 107 (`ts`) and 110 (the append); `scripts/timeline/render.mjs` lines 1 to 413, in particular 127 to 164 (`readLog`), 167 to 198 (`pairLog`), 331 to 348 (`--since` and its default) and 363 to 382 (gate check and status lines); `scripts/timeline/render.sh`; `scripts/tests/timeline.test.mjs` lines 1 to 539; `.agents/skills/implement-plan/SKILL.md` lines 1 to 25 (21 to 25 for the section); `.github/CODEOWNERS` lines 1 to 19; `docs/agents/pull-requests.md` rules 1, 2, 4 and 8 and "Verification gates".
- The Bash tool description of this Claude Code cloud session, read on 2026-11-13 (tool metadata, not official documentation), for the sentence quoted in 3.1.
- Prototype run for Revision 2: a scratch clone of `main` at `8e8d5715`, outside the repository, with `render.mjs` and `event.sh` changed to 3.2 and 3.3, run with Node v22.23.4 on 2026-11-13. Against the unchanged test file: 24 passed, the four listed in section 6 failed, two skipped as root. With the 7.1 changes and the 3.4 skill sentence applied: 28 passed, none failed, two skipped. The 7.2 fixtures, run through `render.sh`, gave the literal blocks and lines of 7.2.
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 11. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the renderer's work grows with the number of log lines and other input rows, one pass each, as in the #443 plan; the window adds one scan of the branch log entries. No cap is set, because the output is one table per session. Implementation effort has no runtime-sized step: the changed and new cases are listed in section 7.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body, the plan adds: pairing after the window, not before, which the issue's proposal 2 implies but does not state, and which inverts `render_gate_boundaries` sub-case 6 (7.1); the file-order rule for log entries, which decision 6860837873 point 1 sets; the `window start:` line, which makes a missing `session.start` visible instead of silently falling back; and `--since` applying the same pairing rule as `session.start`, so there is one pairing rule.

(c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: none. Every new case uses fixed fixtures and the scripts in the pull request. The Bash tool's statement about shell state, cited in 3.1, supports only the option comparison; no test or requirement depends on it. Outside the tests, correct use depends on one writer per log, D1 in section 8, accepted by name in decision 6860837873 point 3; the window no longer depends on clock order for log entries (section 8).

## 12. Open questions for Hari

None.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in section 7.
