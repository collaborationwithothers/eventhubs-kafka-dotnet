# Receipt: issue 412 governance pull request

This is the single authored receipt for the governance pull request of [issue 412](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/412). It applies the issue's five amendments to the factory rules and the planning skills. There is no approved item plan: Hari authorized the work directly in an interactive session.

## Execution card

- **Deliverable:**
  - docs/agents/factory.md "Authority reads" and "Item lifecycle" steps 1 and 3 amended for amendments 1 to 5;
  - the plan-pr, plan-check and implement-plan skills updated to match;
  - a new `plan-checker` agent definition;
  - one sentence in docs/agents/pull-requests.md on the predicate map;
  - `__pycache__/` in `.gitignore`;
  - the evidence folder docs/operations/evidence/issue-412/.
- **Authorization:** the task brief is issue 412's body and [decision 6740819145](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/412#issuecomment-6740819145). Hari asked in the interactive session of 2026-11-09 for the draft pull request and the pilot to run alongside. No approved item plan exists, so none is claimed.
- **Mandatory gates:**
  - `npm run check:docs`, with each warning disposed;
  - cold review under AGENTS.md "Before opening a pull request" rule 8;
  - hosted Codex review;
  - Hari's merge, because protected paths change;
  - Hari sees the pilot result before merging (decision 6740819145, point 3).
- **Verification commands:**
  - `npm run check:docs`;
  - `python3 docs/operations/evidence/issue-412/summarize.py`, which must print the counts in the issue body;
  - `git diff --check`.
- **Earliest publication checkpoint:** a draft pull request after the first complete cold review and its corrections.
- **Review budget:** rule 8's cumulative budget of two comprehensive and two focused rounds. On exhaustion: push to `-wip` and hand off to Hari under rule 8.
- **Predicate map:** not applicable. No verifier logic changes.

## Review identification

- Round 1: comprehensive cold review of the staged index, compared with origin/main 4163cd1afcfb4a3564c80f0eda9aff76cb0a9fe0. Complete; 5 at P0 or P1, 3 at P2.
- Round 2: focused correction review of the staged index containing the round 1 corrections (committed as 0b08998), compared with commit ea978170 (the round 1 content), with the full pull request diff as context. Complete; 2 at P0 or P1, 4 at P2.
- Round 3: focused correction review of the staged index containing the round 2 corrections, compared with commit 0b08998, with the full pull request diff as context. This is the last focused round in the budget.

## Specification comparison sources

The issue 412 body, amendments 1 to 5 and "Also"; decision 6740819145, points 1 to 4.

## Round 2 dispositions

1. P1 plan-pr published before the authority refresh: fixed. Step 3 now requires a refresh before every push to the plan branch and every post or edit of the plan comment; the separate trailing refresh step is removed.
2. P1 SHA approval applied to item plans: fixed. Step 1 limits the SHA to contract-grade plans and keeps "names the revision" for item plans.
3. P2 transition rule left the approval check open: fixed. Old implementation-grade plans keep approval by posting time; a revision becomes a contract-grade file with a pointer comment.
4. P2 rule 8 scope stated only in factory.md: fixed. The scope sentence is in pull-requests.md rule 8; factory.md cites it.
5. P2 accepted deployment assumption had no place: fixed. After Hari accepts it, a non-design revision moves it back to the deployment-assumptions section with a link to his decision.
6. P2 receipt input revision unnamed: fixed in "Review identification".
Pre-existing noted: plan-checker.md now cites the skill's step 2 instead of restating the dry-run limit. The factory.md step 1 first sentence is about item plans, which stay comments; unchanged.
Departure from issue wording: amendment 3 says deployment assumptions may be marked "unverified, risk accepted by Hari". The rules instead list each under "Open questions for Hari" until Hari accepts it by name, which meets amendment 4 and is stricter.

## Results

| Command | Source commit | Result |
|---|---|---|
| `npm run check:docs` | working tree on 4163cd1afcfb4a3564c80f0eda9aff76cb0a9fe0 | 0 errors, 8 warnings, all on lines this pull request does not change |
| `python3 docs/operations/evidence/issue-412/summarize.py` | same | 143 findings; table equal to the issue body |

The 8 documentation warnings are in files the pull request does not change, or on unchanged lines of docs/agents/factory.md (two undefined-identifier warnings at lines 7 and 152). They take one blanket line under rule 8.

## Changes to earlier decisions

- **Plan-check question (c).** Under #85 decision 2, question (c) required checking dependencies "against the real output of the code that produces it". It now accepts recorded output or the code's source, and lists anything neither settles as a deployment assumption. This follows amendment 1 and decision 6740819145, point 4.
- **Read-back paragraph removed.** The #215 paragraph on anchored blob URLs inside plan comments is removed from factory.md step 3. Plan text no longer lives in comments: the comment is a pointer, which is read back by path and SHA.

## Round 1 dispositions

1. P1 plan-pr check before plan comment exists: fixed. plan-pr step 4 posts the pointer comment before step 5 spawns the check; factory step 3 says the comment is posted or edited before the check; check comments for contract-grade plans record path and commit SHA.
2. P1 approval names revision, not SHA; updated_at kept: fixed. Approval names the commit SHA (step 1 and step 3 approval sentence); standing approval and implementation verification use the commit SHA for contract-grade plans; catch-up extraction lists commit SHA.
3. P1 risk accepted marker without Hari: fixed. Uncovered assumptions are blocking findings until a decision or approval by Hari accepts the risk by name; plan section lists them under Open questions for Hari.
4. P1 plan-check skill paraphrased the dry-run gate: fixed; the skill now cites the factory rule without restating it.
5. P1 receipt lacked review mode and revisions: fixed; "Review identification" section added.
6. P2 reader exclusion list: fixed; the planning session's plan-check subagent is excluded from readers.
7. P2 no transition rule: fixed; plans headed implementation-grade posted before the amendment count as contract-grade for selection and keep their rules.
8. P2 plan-branch push and rule 8: fixed; factory step 3 states rule 8 does not apply to plan-branch pushes and the plan check is their review.
Pre-existing noted: plan-check description updated (file in diff). Issue body "14 caught" corrected on the issue (12 caught, 2 unclear, 1 slipped); not in the diff.
