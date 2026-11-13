# Authored receipt: #443 session timeline record

## Specification and authority

- Issue: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443 (body, as read 2026-11-13).
- Approved plan: [session-timeline contract-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858179739), Revision 3, `items/443/plan-session-timeline.md` at commit `1e34eaf6d5e5a5bf1bbef07974618dae205f31c6`. Approval: standing approval under docs/agents/factory.md "Item lifecycle" step 3. The newest [plan check 6859132474](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6859132474) came from a fresh-context check subagent, names that commit SHA, states `Blocking findings: none`, and was posted after the plan comment's last edit (2026-11-13T16:39:23Z).
- Later decisions applied: [6858249662](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858249662) (no hooks; render.sh fails when the pre-PR local gate has no recorded start and end; missing or empty log reported) and [6858982719](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/443#issuecomment-6858982719) (Revision 3). Both are already carried by the plan. No other decision or approval comment exists.
- The plan file lives on `claude/443-plan` and is not part of this pull request.
- Implementing tool: Claude Code (no tool-naming comment before `ready-for-agent`).

## Execution card

- Deliverable: the files of plan section 2, nothing else.
- Authorization: the plan above; Hari merges (it touches `/scripts/` and `/.*/`).
- Mandatory gates: docs/agents/pull-requests.md "Verification gates" steps 1 and 3 do not apply (the change touches no file under `src/` or `tests/`, `scripts/verify-dotnet.sh`, any `*.csproj`, `Directory.*.props`, `packages*.lock.json` or `global.json`); rule 8 cold review; `npm run check:docs`; step 4, the hosted `.NET verification` on the final head.
- Verification commands: plan section 9, commands 1 to 5.
- Earliest authorized publication checkpoint: after a clean cold review.
- Review budget: two comprehensive passes and two focused rounds; exhaustion action: push `-wip` and hand off to Hari (rule 8).

## Predicate map

The plan's test map (section 7) is the predicate map; every requirement 1 to 25 has a named test in `scripts/tests/timeline.test.mjs`. Not applicable beyond that.

## Choices the plan left to the implementer

1. Order among exit-2 checks of `render.sh` (plan check 6858500969, P2 left to the implementer): usage errors in argument order, then the required `--branch` and `--log`, then `--local-gate`, then `--since`, then `--log` content, then `--summary`, `--comments`, `--reviews`, `--jobs` in that order, then `--git-range`. Reason: cheap checks first, and git runs last.
2. Bare arguments are a usage error. A `--git-range` value that starts with `-` is a usage error, and the range reaches git after `--end-of-options`. Reason: `--output=<file>` must never reach `git log` as an option. The shape `<base>..<head>` is not checked, so `nope..nada` gives `git log failed for --git-range.` as plan 7.2 states.
3. `hook data` looks at the rows in the table (after `--since`), not at log lines dropped by `--since`. Reason: it describes the displayed rows, as the other lines do.
4. `render_full` passes fixture files by absolute path and runs with the temporary git repository as the current directory (plan check note).
5. Plan check P2 `apply in implementation` for a job without `steps`: a missing `steps` is an empty list, with test `render_jobs_no_steps` and the check's literal expectations. The check's P2 on a usage text comment in `render.mjs` is met by not repeating the `--local-gate` rule in `render.mjs`; the usage line names only the values.

## Standing fixture rule

No existing test or fixture exercises these scripts; no fixture change was needed.

## Commands and results

Source commit: `1c2ed5e618a0a108e69ca73f4591059e68619fe2` (`origin/main`) plus the working tree under review.

1. `node --test scripts/tests/timeline.test.mjs`: 30 tests, 28 pass, 0 fail, 2 skipped. The skipped cases are `event_write_failure` and `render_unreadable`, both skipped because the session runs as root (`process.getuid() === 0`), as plan section 7.3 allows. The exit-1 path of `event.sh` and the `Cannot read` path of `render.mjs` therefore did not run here.
2. `scripts/timeline/event.sh gate.start sandbox`, `./scripts/verify-dotnet.sh --tier sandbox`, `gate.pass sandbox`: exit 0, outcome `partial_passed` (a partial run that excludes `Tier=CGroupV2`, not the full gate; the plan makes no gate mandatory, see the execution card). `scripts/timeline/render.sh --branch claude/443-session-timeline --log <log> --summary <summary.json>` exit 0: block with `hook data: absent`, `event log: present`, `pre-PR local gate: recorded`, the `verify.run` row `tier=sandbox outcome=partial_passed exit=0` with 1411 seconds and the paired `gate.pass sandbox` row with 1417 seconds.
3. `npm run check:docs`: exit 0, 9 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, `docs/operations/evidence/issue-211/receipt.md`, `items/404/pr2-receipt.md`). No warning in a changed file.
4. Acceptance command 3 (the block in the pull request description) and command 5 (hosted `.NET verification` on the final head) are recorded in the pull request description.

## Cold review record

- Round 1: mode comprehensive (pass 1 of at most 2; focused rounds used 0 of 2). Input revision: the staged tree on `origin/main` `1c2ed5e618a0a108e69ca73f4591059e68619fe2`; comparison revision: none (first review).
- Complete reports, dispositions and later rounds are recorded in the pull request's self-review record.
