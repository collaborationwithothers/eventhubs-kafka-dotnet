# state-file-retire contract-grade plan (#472)

Revision: 1

This plan answers the questions of issue #472 with measurements and proposes one change: stop writing and reading the committed item state file, and add a script that prints an issue's decision and approval comments verbatim, in creation order. The binding record stays Hari's comments, with his quoted words, in the order "Item lifecycle" step 3 already defines. Nothing here is decided: the choice of option is open question Q1 for Hari, and the change is a governance change that only Hari merges.

Base commit: `08c708aae6a8cc27c87110013540089bb5553a43` (`main`, read 2026-11-18).

## 1. Goal and authority

Goal: answer the four questions in the #472 issue body with evidence, and, if Hari chooses the recommended option, retire the committed state file without weakening how authority is recognized.

Authority, in creation order:
- The #472 issue body (created 2026-11-14T08:43:55Z, `updated_at` 2026-11-14T08:50:13Z). It states "Nothing here is decided."
- Comment [6874785569](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/472#issuecomment-6874785569), "Tool: Codex", created 2026-11-14T08:50:13Z. It is not a `## Decision` or `## Approval`. See Q2.
- No `## Decision` or `## Approval` comment exists on #472 at this revision. There is no item state file for #472 and no parent item.
- `ready-for-agent` was applied at 2026-11-14T08:50:03Z (issue events).

Governance sources this plan changes or relies on: docs/agents/factory.md "Authority reads", "Item state file" and "Item lifecycle" steps 1 and 3; AGENTS.md GOVERNANCE "Truth and verification" (a summary is not a source); [#419 decision 6873248830](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/419#issuecomment-6873248830) rules 6 (decisions given in chat are posted quoting Hari) and 7 (light child issues); [#471 decision 6875571153](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/471#issuecomment-6875571153), which left "state redesign" out of its scope.

## 2. Evidence (measured 2026-11-18 by script in this session)

Method: REST reads of issue comments with `per_page=100&page=N`; classification by the first non-empty line of each body:
- decision: starts `## Decision`, and the next non-empty line does not start `This issue adopts`;
- adoption copy: starts `## Decision`, and the next non-empty line starts `This issue adopts`;
- approval: starts `## Approval`;
- plan: ends `contract-grade plan`, or starts `Revision: ` (the two #499 proposal comments);
- plan check: starts `## Plan check`;
- handoff: starts `**Decision needed:**`, `**Continuation needed:**`, `## Handoff` or `## Completion`;
- control action: starts `## Control action`; tool: starts `Tool:`; other: anything else.

The rule is mechanical. It does not judge whether a comment records Hari's decision; "Item lifecycle" step 3 makes that judgement.

### 2.1 Consumer batch (#499 and children)

| Issue | Comments | Decisions | Approvals | Adoption copies | Plans | Plan checks | Handoffs | Control actions | Tool | Other | Bytes |
|---|---|---|---|---|---|---|---|---|---|---|---|
| #499 | 16 | 11 | 0 | 0 | 2 | 0 | 2 | 0 | 1 | 0 | 39891 |
| #504 | 20 | 5 | 2 | 4 | 1 | 3 | 3 | 1 | 1 | 0 | 67336 |
| #505 | 19 | 3 | 1 | 6 | 1 | 2 | 5 | 0 | 0 | 1 | 61772 |
| #506 | 32 | 8 | 1 | 5 | 1 | 5 | 9 | 0 | 1 | 2 | 188063 |
| #507 | 22 | 6 | 1 | 3 | 1 | 4 | 7 | 0 | 0 | 0 | 91179 |
| #508 | 28 | 6 | 1 | 3 | 1 | 5 | 7 | 5 | 0 | 0 | 107305 |
| #509 | 13 | 2 | 1 | 3 | 1 | 2 | 4 | 0 | 0 | 0 | 38890 |
| #510 | 6 | 0 | 0 | 5 | 0 | 0 | 0 | 1 | 0 | 0 | 2347 |
| #511 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 365 |
| #512 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 365 |
| #513 | 1 | 0 | 0 | 1 | 0 | 0 | 0 | 0 | 0 | 0 | 365 |
| #521 | 15 | 3 | 1 | 2 | 1 | 3 | 5 | 0 | 0 | 0 | 64180 |
| #531 | 15 | 3 | 2 | 1 | 1 | 3 | 5 | 0 | 0 | 0 | 63799 |
| Total | 189 | 47 | 10 | 35 | 10 | 27 | 47 | 7 | 3 | 3 | 725857 |

Comment IDs per kind are in the appendix at the end of this file.

Observations:
- Adoption copies are 35 of 189 comments (18.5 percent). Each holds only links to #499 decisions; none adds words of Hari's.
- 53 of the 57 decision and approval comments on #499 and its children #504 to #509, #521 and #531 contain a quoted span (text between straight or curly double quotes of at least 3 characters).
- Across all 78 committed state files on `main`, the "Decisions in force" sections hold 521 lines; 20 of them contain a double quote. The state file paraphrases. The comments keep Hari's words.

### 2.2 Reading saved per session on the batch children

A session with no state file reads every decision, approval and adoption comment in full, plus each #499 comment they link (factory.md "Authority reads" "Catch-up"). Measured bytes:

| Child | Decision, approval and adoption comments | Their bytes | #499 comments they link | Linked bytes | Total read | state.md bytes |
|---|---|---|---|---|---|---|
| #504 | 11 | 10934 | 5 | 9808 | 20742 | 5447 |
| #505 | 10 | 7076 | 6 | 11001 | 18077 | 4098 |
| #506 | 14 | 13397 | 6 | 11001 | 24398 | 5349 |
| #507 | 10 | 10197 | 4 | 8197 | 18394 | 4341 |
| #508 | 10 | 11062 | 4 | 5890 | 16952 | 4111 |
| #509 | 6 | 4354 | 4 | 7657 | 12011 | 2606 |
| #510 | 5 | 1648 | 5 | 8097 | 9745 | none |
| #521 | 6 | 5707 | 3 | 4955 | 10662 | 2963 |
| #531 | 6 | 6022 | 3 | 4209 | 10231 | 2822 |

The state file saves between 7.4 and 19.0 KB of reading per session on these children. For comparison, each plan check on these children is between 10.5 and 18.3 KB (section 2.1 bytes, plan-check comments), and a planning session reads the newest one.

Large threads, for contrast:

| Issue | Comments | Decision and approval comments | Their bytes | state.md bytes |
|---|---|---|---|---|
| #404 (closed) | 395 | 197 | 404430 | 33917 |
| #494 (open) | 70 | 34 | 78044 | 28770 |
| #474 (open) | 48 | 26 | 64097 | 6967 |

The #472 body says #404 had "about 300 comments" and 170 decisions; the counts above are current and include later comments. On #404 a planning session reported reading the state file's lines instead of all 170 decisions ([6846390215](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846390215), "Disclosure"). That is the one recorded case of the file replacing reading.

### 2.3 Upkeep and review cost

- 78 state files exist on `main`; 39 first-parent commits on `main` changed them, adding 1447 lines and deleting 91. #494's file changed in 30 commits and #474's in 13.
- Codex inline review comments on the pull requests behind those 39 commits: 74 in total, 2 on a state file, both on [pull request 546](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/546): one P1 finding (uncited counts in `items/494/state.md`) and its reply.
- Cold-review findings are not posted inline, so they were not counted. The #472 body cites [6873097154](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6873097154) for a P1 about the state file listing an unlinked chat instruction. That comment reports rounds 17 to 19 as "stale authority text", "Authority and count text" and "stale statements". It does not name the state file. The claim is not confirmed by its cited source; it may be true in a report this session did not read.
- Drift (a session acting on a stale or wrong state-file line) was not measured. No recorded incident was found in the comments read; that is not evidence of absence.

### 2.4 Answers to the issue's questions

1. Does the file save more reading than it costs? On the batch children it saves 7.4 to 19.0 KB per session (measured above). Its measured Codex review cost is small (1 finding in 39 merges). Its cold-review cost is not measured. Inference: on light child issues the saving is small next to the plan and plan-check reading every session already does; on threads of #404's size it is large, but the file then paraphrases 197 comments and the "read the source when work depends on exact text" rule (factory.md "Authority reads") sends the session back to the comments.
2. Committed, pinned comment, or generated? See section 3.
3. Non-blocking wording in cold review? Under option B only. The recommended option removes the reviewed text, so the question no longer arises.
4. Interaction with #419 rule 7: light child issues cap the thread a session reads. The largest batch child has 14 authority comments and needs 24.4 KB of authority reading without a state file. Adoption copies make every child's authority reachable from the child's own comments, which is what a generated log needs.

## 3. Options and recommendation

| Option | Binding record | Words of Hari kept | Drift | Review cost |
|---|---|---|---|---|
| A. Keep as is | Comments | In comments; file paraphrases | Possible | Each implementation PR |
| B. Keep, wording non-blocking in cold review | Comments | As A | Possible, and less reviewed | Lower |
| C. Pinned issue comment edited by sessions | Comments | As A | Possible; edits are unreviewed and all comments post as Hari's account | None |
| D. Retire the file; add `scripts/decision-log.py`, which prints decision and approval comments verbatim in creation order with the comments they link | Comments | Printed verbatim | None: generated from the comments each time | None for the log; one-time review of the script |
| E. Retire the file with no replacement | Comments | In comments | None | None |

Recommendation: D. The log is the comments themselves, so it keeps Hari's quoted words and the order of decisions, and it cannot drift or need review. It adds nothing a session must trust beyond the script, which has tests.

Strongest counterargument: the state file's value is judgement, not text. It tells a session which decisions still apply and what to do next. A verbatim log cannot say that a later decision superseded an earlier one, so on a thread like #404 (197 authority comments, 404 KB) a session must read everything, which one session already could not do. Removing the file also removes the "Covers comment" anchor, so #494's sessions read 78 KB of decisions instead of 28.8 KB plus new comments. Against that: the measured Codex review cost of the file is low (section 2.3), so D solves a cost that the available data does not show to be large. Answer to the counterargument: #404 is closed, rule 7 children keep threads small, and the supersession judgement the file records is a paraphrase that AGENTS.md "Truth and verification" already says is not a source. Whether that answer holds for #494 and #474 is Hari's call (Q1).

## 4. Scope

Pull request: `state-file-retire`, one coherent piece. No split.

Files allowed to change:
- `docs/agents/factory.md`: the places in section 6, requirements R10 to R15.
- `.agents/skills/plan-pr/SKILL.md`, `.agents/skills/plan-check/SKILL.md`, `.agents/skills/implement-plan/SKILL.md`: requirements R16 to R18.
- `scripts/decision-log.py` (new).
- `scripts/tests/test_decision_log.py` (new).
- Only if Q3 is answered "delete": every `items/*/state.md` on `main` at the implementation base, deleted and not edited.
- Only if Q4 is answered "wire into CI": `.github/workflows/dotnet-verification.yml`, one step (R19).

Paths that must not change: `results/`, every `expected.yaml`, `src/`, `tests/`, `infra/`, `.claude/hooks/`, `.claude/settings.json`, AGENTS.md, CLAUDE.md, every other workflow, `docs/blueprint.md`, and `items/*/state.md` unless Q3 says delete.

Size estimate (estimate, basis: the script and test shapes in sections 5 and 9 and the text replacements in section 6, 2026-11-18): 6 files and about 380 changed lines; about 150 for the script, 170 for the tests, 50 in factory.md, 10 in the skills. If Q3 is "delete", add 78 deleted files and about 1356 deleted lines. Size is reported, not enforced.

## 5. External contracts

### 5.1 Command line

```
python3 scripts/decision-log.py --issue <N> [--issue <M> ...] [--offline <DIR>]
```

- `--issue` is required at least once. Each value is a positive integer. A session passes the issue and each parent item its body names.
- Without `--offline`, the script fetches with the `gh` command line only: `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/issues/<N>/comments?per_page=100&page=<k>` for k = 1, 2, ... until a page has fewer than 100 items, and `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/issues/comments/<id>` for each linked comment. It passes no `-X`, `-f`, `-F`, `--input` or `--paginate` argument.
- With `--offline DIR`, it reads `DIR/<N>.json` (a JSON array of comment objects) for each issue and `DIR/linked/<id>.json` (one comment object) for each linked comment, and runs no process.
- A comment object needs `id`, `created_at`, `updated_at`, `html_url` and `body`, as the GitHub REST issue comment returns them.

### 5.2 Recognition

- Authority comment: the first non-empty line of `body`, with leading spaces removed, starts with `## Decision` or `## Approval`. Kind is `Decision` or `Approval`. This is the heading test of factory.md "Item lifecycle" step 3; whether the comment records Hari's decision stays the reading session's judgement.
- Linked comment: every match, in an authority comment's body, of the regular expression `https://github\.com/collaborationwithothers/eventhubs-kafka-dotnet/(?:issues|pull)/(\d+)#issuecomment-(\d+)`. One level only: links inside linked comments are not followed.

### 5.3 Output (stdout, UTF-8)

```
# Decision log: #<N>[, #<M> ...]

Read <C> comments. Binding record: the comments below, verbatim, in creation order. This log is not a source; cite the comment URLs.
```

Then, for each authority comment across all given issues, sorted by `created_at` and then by `id`, numbered from 1:

```

---

### <s>. <Kind> #<issue> comment <id>

Created <created_at>; updated <updated_at>; <html_url>

<body, verbatim>
```

`<issue>` is the number in `html_url`. Then, for each distinct linked comment ID in order of first appearance in that body, numbered `<s>.1`, `<s>.2`, ...:

```

#### <s>.<k> Linked from comment <id>: #<issue> comment <linked id>

Created <created_at>; updated <updated_at>; <html_url>
Adoption is judged by the reading session under docs/agents/factory.md "Item lifecycle" step 3.

<body, verbatim>
```

If the linked comment is itself an authority comment in this log, the entry is the heading line followed by one line `Listed as entry <t>.` and no body. If it cannot be read, the entry is the heading line followed by one line `Not available.`

If no authority comment exists, the output after the header is one line `No decision or approval comments.`

`<C>` is the number of comments read from the given issues, linked comments excluded.

### 5.4 Exit status

| Status | When | stdout | stderr |
|---|---|---|---|
| 0 | Every comment and linked comment was read | The log | Empty |
| 1 | A `gh` call for an issue's comment page exits non-zero, or a `DIR/<N>.json` file is missing or not a JSON array | Empty | `decision-log: cannot read comments of #<N>` |
| 2 | Invalid arguments | Empty | argparse usage message |
| 3 | At least one linked comment could not be read | The log, with `Not available.` entries | `decision-log: <n> linked comment(s) not available` |

## 6. Requirements

Script:
- R1. WHEN the script runs with valid inputs THEN it prints the header of 5.3 and one entry per authority comment, sorted by `created_at` then `id`, and exits 0.
- R2. WHEN a comment's first non-empty line does not start with `## Decision` or `## Approval` after leading spaces are removed THEN it has no entry. `### Decision` and `**Decision needed:**` have no entry.
- R3. WHEN an entry prints a body THEN the body is byte-identical to the `body` field, including quotes, curly quotes, trailing spaces and line endings.
- R4. WHEN an authority body links comments THEN each distinct linked ID is printed once, in order of first appearance, as 5.3 sets.
- R5. WHEN a linked comment is an authority comment in the log THEN its entry is `Listed as entry <t>.` with no body.
- R6. WHEN a linked comment cannot be read THEN its entry is `Not available.`, the full log is printed, stderr holds the status-3 message and the exit status is 3.
- R7. WHEN an issue's comments cannot be read THEN nothing is printed to stdout, stderr holds the status-1 message and the exit status is 1.
- R8. WHEN fetching THEN pages are requested with the exact `gh api` arguments of 5.1, starting at page 1, and stop after the first page with fewer than 100 items.
- R9. WHEN no authority comment exists THEN the output is the header and `No decision or approval comments.`, exit 0.

docs/agents/factory.md (literal replacement text; places named by section):
- R10. Status line (paragraph starting "Status: approved by Hari on 2026-10-12."): append ` Amended in the pull request for issue #472: "Authority reads", the "Item state file" section replaced by "Decision log", and "Item lifecycle" step 3; Hari's merge of that pull request is the approval.`
- R11. "Authority reads", first paragraph: replace `The item state file ("Item state file" below) is an index to that authority, not a source of it.` with `The decision log ("Decision log" below) is a reading aid for that authority, not a source of it.`
- R12. "Authority reads" table: in the Planning row replace `The item state file and its catch-up,` with `The catch-up,`; in the Plan check row replace `the item state file and its catch-up,` with `the catch-up,`; in the Implementation row replace `the item state file and its catch-up,` with `the catch-up,`. Delete the sentence `Where its work depends on the exact text of a decision the state file lists, it reads that decision in full.`
- R13. "Catch-up" paragraphs:
  - Replace `A comment is new when it was created after the comment the state file covers, or edited after the file's read time. With no state file, every comment is new.` with `At a session's initial read every comment is new. A session may print the decision and approval comments it must read with the decision log ("Decision log").`
  - Replace `- the issue body, if there is no state file or the body was edited after the file's read time;` with `- the issue body;`
  - Replace `Planning and implementation sessions also read the full body of every other new comment when a state file exists. With no state file they read only the newest comment that is not a decision, approval, plan or plan check, because a handoff to the next session carries the continuation (AGENTS.md GOVERNANCE "Handoffs").` with `Planning and implementation sessions also read the newest comment that is not a decision, approval, plan or plan check, because a handoff to the next session carries the continuation (AGENTS.md GOVERNANCE "Handoffs").`
  - Replace `As one exception, every session reads the full body of each comment listed under the state file's "Decisions in force", or already read as authority in this session, when that comment was edited after the last read, whatever its heading now.` with `As one exception, every session reads the full body of each comment already read as authority in this session when that comment was edited after the last read, whatever its heading now.`
  - Replace `If such a comment no longer qualifies as a decision or approval under "Item lifecycle" step 3, treat its line as void. Other comments the state file links, such as plans, plan checks, tool-naming comments and open-question sources, are not covered by this exception; they are read as the rest of this catch-up and the session's row name them. If any comment the state file links no longer exists, treat its line as void. Whenever a line is void, or a comment read as authority is deleted or no longer qualifies, read every decision and approval comment on the issue and every decision they link and adopt, as with no state file.` with `Whenever a comment read as authority is deleted or no longer qualifies as a decision or approval under "Item lifecycle" step 3, read every decision and approval comment on the issue and every decision they link and adopt again.`
  - Replace `For a child issue of a split, its state file lists the parent items the child's body names.` with `For a child issue of a split, the child's body names its parent items.`
- R14. Replace the whole "## Item state file" section, from its heading to the line before "## Item lifecycle", with:

  ```
  ## Decision log

  `scripts/decision-log.py` prints, for the issues it is given, every comment whose heading starts "## Decision" or "## Approval", verbatim and in creation order, each followed by the comments it links, verbatim. It adds no words and decides nothing: whether a comment records Hari's decision, whether a link adopts, and which decision a later one supersedes stay the reading session's judgement under "Item lifecycle" step 3. It is never committed. The binding record is the comments. Run it as `python3 scripts/decision-log.py --issue <issue> [--issue <parent item> ...]`. An exit status other than 0 means the log is incomplete; the session reads the missing comments itself or hands off under AGENTS.md GOVERNANCE "Handoffs".

  Files at `items/<issue number>/state.md` written before the pull request for issue #472 merged are historical. No session reads them as authority, writes them or updates them. Basis: [#472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/472) and Hari's decision that adopts this plan.
  ```

  If Q3 is answered "delete", the second paragraph is instead: `The item state files that earlier sessions wrote at items/<issue number>/state.md were deleted in the pull request for issue #472. Basis: [#472](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/472) and Hari's decision that adopts this plan.`
- R15. "Item lifecycle" step 3, the bullet starting "After Hari approves the plan": delete the sentence `It writes the item state file under "Item state file".`

Skills:
- R16. `.agents/skills/plan-pr/SKILL.md` step 1: replace `Read the item state file and catch up on the issue` with `Catch up on the issue`.
- R17. `.agents/skills/plan-check/SKILL.md` step 1: replace `Read the selected plan comment and the item state file, and catch up on the issue` with `Read the selected plan comment and catch up on the issue`.
- R18. `.agents/skills/implement-plan/SKILL.md`: in step 1 replace `Read the item state file and catch up on the issue` with `Catch up on the issue`; in step 2 delete the sentence `Create or update the item state file at the time factory.md "Item state file" sets.`

CI (only if Q4 is "wire into CI"):
- R19. In `.github/workflows/dotnet-verification.yml`, after the step named `Test the aggregate checker`, add a step named `Test the decision log` that runs `python3 -m unittest discover -s scripts/tests -p test_decision_log.py -v`. Triggers do not change. There is no committed copy of this workflow under `docs/operations/workflows/` at the base commit, so hard safety rule 8's copy clause does not apply.

## 7. Invariants and safety rules

- The recognition of authority in factory.md "Item lifecycle" step 3 is not edited. Its order rule ("Apply those comments in creation order; a later one wins where they differ") stays the binding order.
- The script only reads. It runs `gh api` with GET arguments only (5.1) and posts nothing.
- No change to `results/`, expected outcomes, verdicts, workflow triggers, hooks or AGENTS.md (AGENTS.md "Verdict integrity", hard safety rule 8).
- The pull request touches protected paths (`/docs/`, `/.*/`, `/scripts/` in `.github/CODEOWNERS`), so it waits for Hari's merge under GOVERNANCE "Merge rule".
- Every rule is stated once, in factory.md "Decision log"; the skills cite it.

## 8. Deployment assumptions

- DA1. Sessions have `gh` authenticated for REST reads of this repository. Evidence: this planning session ran `gh api` reads of issue and comment routes on an already-open session, idle, 2026-11-18; AGENTS.md "GitHub issues and pull requests" requires `gh api`. Covered offline by the fetch tests with a mocked process runner (T8, T9); the live read is not tested in the pull request. Listed under Q5 until Hari accepts it by name.
- DA2. The REST comment objects carry `id`, `created_at`, `updated_at`, `html_url` and `body`. Evidence: the same reads above returned these fields for 703 issue comments. Covered offline by fixtures of that shape (all tests).

## 9. Test map

All tests in `scripts/tests/test_decision_log.py`, `unittest`, offline mode unless stated. Fixture comment `c(id, created, body, issue)` has `updated_at` equal to `created_at` and `html_url` `https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/<issue>#issuecomment-<id>`.

| Test | Requirement | Inputs | Expected |
|---|---|---|---|
| T1 `test_recognizes_only_decision_and_approval_headings` | R1, R2 | Issue 7: c(1,"2026-11-01T00:00:00Z","## Decision\n\nHari: \"yes\"",7), c(2,"2026-11-01T00:00:01Z","**Decision needed:** x",7), c(3,"2026-11-01T00:00:02Z","  \n  ## Approval\nok",7), c(4,"2026-11-01T00:00:03Z","### Decision\nno",7), c(5,"2026-11-01T00:00:04Z","Decision: no",7) | Exit 0; entries `### 1. Decision #7 comment 1` and `### 2. Approval #7 comment 3`; no entry for 2, 4, 5; header line `Read 5 comments. ...` |
| T2 `test_orders_by_created_then_id_across_issues` | R1 | Issue 7: c(30,"2026-11-02T00:00:00Z","## Decision\na",7); issue 8: c(20,"2026-11-02T00:00:00Z","## Decision\nb",8), c(40,"2026-11-01T00:00:00Z","## Approval\nc",8) | Order 40, 20, 30, numbered 1, 2, 3; header `# Decision log: #7, #8` |
| T3 `test_prints_body_verbatim` | R3 | c(1,...,"## Decision\n\nHari said: \u201cfor 531 implement using claude\u201d.  \r\nend",7) | stdout contains that body exactly, with `\u201c` and `\u201d` as the two curly quote characters |
| T4 `test_lists_each_link_once_in_order` | R4 | c(1,...,"## Decision\nadopts https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-11 and https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/413#issuecomment-12 and again https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-11",7); linked 11 (issue 499) and 12 (issue 413) | `#### 1.1 Linked from comment 1: #499 comment 11` then `#### 1.2 Linked from comment 1: #413 comment 12`; no `1.3`; each linked body printed once |
| T5 `test_link_to_listed_entry_has_no_body` | R5 | c(1,"2026-11-01T00:00:01Z","## Decision\nfirst",7), c(2,"2026-11-01T00:00:02Z","## Decision\nsee https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/7#issuecomment-1",7) | Entry `#### 2.1 Linked from comment 2: #7 comment 1` followed by `Listed as entry 1.`; body `## Decision\nfirst` appears once in stdout |
| T6 `test_missing_link_exits_3` | R6 | c(1,...,"## Decision\nhttps://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-99",7); no `linked/99.json` | Exit 3; stdout contains `#### 1.1 Linked from comment 1: #499 comment 99` and `Not available.`; stderr `decision-log: 1 linked comment(s) not available` |
| T7 `test_unreadable_issue_exits_1` | R7 | `--issue 7`, no `7.json` | Exit 1; stdout empty; stderr `decision-log: cannot read comments of #7` |
| T8 `test_fetch_pages_with_exact_arguments` | R8, DA1 | Fetch mode, process runner mocked: page 1 returns 100 comments without authority headings, page 2 returns 1 | Exactly two calls: `["gh","api","repos/collaborationwithothers/eventhubs-kafka-dotnet/issues/7/comments?per_page=100&page=1"]` and the same with `page=2`; header `Read 101 comments. ...` |
| T9 `test_fetch_failure_exits_1` | R7, R8 | Fetch mode, runner returns exit 1 for page 1 | Exit 1; stdout empty; stderr `decision-log: cannot read comments of #7` |
| T10 `test_no_authority_comments` | R9 | c(1,...,"plain",7) | stdout ends with `No decision or approval comments.\n`; exit 0 |

Boundaries: T1 covers the heading boundary (`## Decision` vs `### Decision`, leading blank line and spaces); T8 covers the page boundary at exactly 100 items.

Fixture rule (plan rule 2): this plan adds a new script and tests and changes no existing check, so no existing fixture can be rejected. If one is, the implementer does exactly one of: (a) changes the fixture so it passes, keeping every assertion and boundary; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. No case is removed, no assertion loosened, no check changed. The PR implementation notes list each such change with the check that forces it.

## 10. Acceptance commands

1. `python3 -m unittest discover -s scripts/tests -p test_decision_log.py -v`: 10 tests run, all pass.
2. `python3 scripts/decision-log.py --issue 509 --issue 499 > "$TMP/log.md"; echo $?`: prints `0`. At the base commit, measured 2026-11-18, the log has 17 `### ` entries (6 on #509, 11 on #499); more if comments were added since, which the PR records.
3. `grep -nE 'state\.md|[Ss]tate file' docs/agents/factory.md .agents/skills/plan-pr/SKILL.md .agents/skills/plan-check/SKILL.md .agents/skills/implement-plan/SKILL.md`: matches only inside the "Decision log" section's second paragraph.
4. `npm ci --ignore-scripts && npm run test:docs && npm run check:docs`: passes.
5. ASCII scan of the changed text files: `grep -nP '[^\x00-\x7F]' <changed files>` prints nothing. The test file writes T3's curly quotes as `\u201c` and `\u201d` escapes.
6. The verification gates of docs/agents/pull-requests.md "Verification gates", in its order, with the cold review under AGENTS.md "Before opening a pull request" rule 8.

## 11. Plan-check questions (factory.md "Item lifecycle" step 1)

- (a) Effort that scales with a number unknown at planning time: none in the implementation. The script's run time scales with an issue's comment count, but no step of this pull request iterates over a runtime-sized set; acceptance command 2 runs once. If Q3 is "delete", the file count is known: 78 at the base commit.
- (b) Stricter than a parent plan: no parent plan exists. The issue body is the only authority.
- (c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: the GitHub REST comment shape and `gh` (DA1, DA2). DA2 is checked against recorded output of 703 issue comments read in this session. DA1 is not settled by an offline test and is Q5.

## 12. Sources

- docs/agents/factory.md at `08c708aae6a8cc27c87110013540089bb5553a43`: "Authority reads", "Item state file", "Item lifecycle" steps 1 and 3.
- AGENTS.md at the same commit: GOVERNANCE "Truth and verification", "Hard safety rules" 8 and 11, "GitHub issues and pull requests".
- `.github/CODEOWNERS` and `.github/workflows/dotnet-verification.yml` at the same commit.
- The comments cited in sections 1 and 2, read 2026-11-18.
- External behaviour: none from Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs. The only external behaviour is the GitHub REST comment shape, taken from recorded responses (DA2), not from documentation; the script sorts by itself and does not rely on the API's order.

## 13. Open questions for Hari

- Q1. Which option of section 3: A, B, C, D or E? Recommended: D. If not D, this plan is withdrawn and a new revision plans the chosen option.
- Q2. Which tool implements? `ready-for-agent` was applied at 08:50:03Z and "Tool: Codex" was posted at 08:50:13Z, ten seconds later. Under factory.md "Item lifecycle" step 2 the literal reading is Claude Code, because no tool comment existed when the label was applied. The likely intent is Codex. A `## Decision` naming the tool settles it.
- Q3. The 78 existing state files: keep as historical (recommended, R14 first text; no churn, links to them stay valid) or delete in this pull request (R14 alternative text)?
- Q4. Run the new tests in CI by adding one step to `dotnet-verification.yml` (R19), or only as acceptance command 1? Recommended: wire into CI, so a later edit cannot break the script unseen. It touches a workflow, which only Hari merges.
- Q5. Accept by name deployment assumption DA1 (sessions have `gh` authenticated for REST reads), which no test in the pull request covers live.

## 14. Deferred work

- D1. `docs/operations/factory-overview/generate-diagrams.py` lines 112, 241 and 253, and the SVGs generated from it, describe the state file. Not on the verdict path. Deferral is permitted because the diagrams are an overview, not a rule; a reader of the overview could be misled that the file is still kept. Responsible: a later documentation pull request after this one merges. Completion evidence: the three strings no longer name the state file and the SVGs are regenerated. Blocks no gate.
- D2. If Q4 is answered "acceptance only", wiring the tests into CI stays deferred. Not on the verdict path. Responsible: Hari's choice of a later workflow change. Blocks no gate.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 5, a requirement in section 6, or the scope in section 4.

## Appendix: comment IDs by kind (measured 2026-11-18)

- #499:
  - decisions: 6894534441, 6894564479, 6894587233, 6894614673, 6895299617, 6895328446, 6895343227, 6895535291, 6899939624, 6926376436, 6958521790
  - plan or proposal comments: 6894160372, 6894560135
  - handoffs and completions: 6894161963, 6894562095
  - tool comments: 6895573282
- #504:
  - decisions: 6895560872, 6895700580, 6897936768, 6898138748, 6898372130
  - approvals: 6897525876, 6898353773
  - adoption copies: 6895299838, 6895328621, 6895343446, 6895535732
  - plan or proposal comments: 6895443887
  - plan checks: 6895667441, 6895877604, 6898284435
  - handoffs and completions: 6895894202, 6898297794, 6898647530
  - control actions: 6897727203
  - tool comments: 6897558239
- #505:
  - decisions: 6899404704, 6902733333, 6908194622
  - approvals: 6900161937
  - adoption copies: 6895300019, 6895328783, 6895343691, 6895536338, 6899939867, 6900154637
  - plan or proposal comments: 6897683461
  - plan checks: 6899350281, 6900138509
  - handoffs and completions: 6897721563, 6899393180, 6900150041, 6900242910, 6908908823
  - other: 6903715635
- #506:
  - decisions: 6899542333, 6899696796, 6900142861, 6900831355, 6907709590, 6910185218, 6910866264, 6913542907
  - approvals: 6901013311
  - adoption copies: 6895300183, 6895329001, 6895343921, 6895536845, 6899940103
  - plan or proposal comments: 6899351414
  - plan checks: 6899498280, 6899671680, 6900124778, 6900322312, 6900997522
  - handoffs and completions: 6897548629, 6899689719, 6900135454, 6900332177, 6901006414, 6902887819, 6909946582, 6912660235, 6914465064
  - tool comments: 6901627643
  - other: 6909598419, 6914071895
- #507:
  - decisions: 6915070211, 6925863575, 6926375790, 6926743261, 6928719509, 6939648300
  - approvals: 6928714258
  - adoption copies: 6895300348, 6895329189, 6895537201
  - plan or proposal comments: 6914731922
  - plan checks: 6914958306, 6926192587, 6926714233, 6927086737
  - handoffs and completions: 6915055673, 6926216145, 6926732349, 6927104759, 6929897151, 6941358791, 6942987877
- #508:
  - decisions: 6955995326, 6956513318, 6958439968, 6960769232, 6969598217, 6970923557
  - approvals: 6960689247
  - adoption copies: 6895537629, 6899940416, 6958522350
  - plan or proposal comments: 6943430187
  - plan checks: 6943741664, 6944037617, 6956419369, 6956863086, 6958651724
  - handoffs and completions: 6944066714, 6956441774, 6956883878, 6958668809, 6962565399, 6972667701, 6973117302
  - control actions: 6897727430, 6900154862, 6900831605, 6926377393, 6943512666
- #509:
  - decisions: 6909196149, 6910955423
  - approvals: 6909334509
  - adoption copies: 6895300516, 6895344090, 6895538069
  - plan or proposal comments: 6909051898
  - plan checks: 6909175276, 6909301380
  - handoffs and completions: 6909186946, 6909314757, 6913540375, 6914733140
- #510:
  - adoption copies: 6895329362, 6895344271, 6895538516, 6899940730, 6958522754
  - control actions: 6900155071
- #511:
  - adoption copies: 6895538971
- #512:
  - adoption copies: 6895539372
- #513:
  - adoption copies: 6895539750
- #521:
  - decisions: 6915213551, 6925863975, 6926693475
  - approvals: 6926201546
  - adoption copies: 6899940947, 6899941170
  - plan or proposal comments: 6914668094
  - plan checks: 6914897314, 6915163770, 6926177160
  - handoffs and completions: 6915184294, 6926190557, 6929234863, 6941311182, 6943031778
- #531:
  - decisions: 6943488058, 6943512296, 6955981131
  - approvals: 6943704305, 6956497218
  - adoption copies: 6926376950
  - plan or proposal comments: 6943199291
  - plan checks: 6943451375, 6943683105, 6956475996
  - handoffs and completions: 6943476137, 6943695162, 6956107567, 6956490346, 6957931544
