# Authored receipt: backup branch cleanup

This pull request adds the manual cleanup path for redundant backup branches. It implements the checked main-only plan and retains backups when ancestry or required protection cannot be established.

## Execution card

- Deliverable: the workflow and mirror, standard-library cleanup script, offline Git and protection tests, rule 12 amendment and item state.
- Authorization: [plan r2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6848363584), file `items/448/plan-backup-cleanup.md` at `ba3f821b9099cad4fd6f4d74acc22af5f0bc177b`; [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849302112), with no blocking findings; [Hari's explicit approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849369765). [The main-only decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849056421) is incorporated by that revision. The [later decision addressing r1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6849532735) repeats that direction and the check/approval sequence. It changes no r2 contract; the existing r2 check and SHA-specific approval fulfill that sequence for the revision replacing r1.
- Mandatory gates: the plan's acceptance commands, documentation warning disposition, cold review under docs/agents/pull-requests.md rule 8, hosted CI and final-head Codex review. Protected merge belongs to Hari; the single live dispatch follows his merge.
- Verification commands: `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v`; `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh` with pinned tools; `cmp .github/workflows/cleanup-wip-branches.yml docs/operations/workflows/cleanup-wip-branches.yml.txt`; `git diff --check`; `npm run check:docs`. Use the pinned tool environment where required by the plan.
- Earliest publication checkpoint: acceptance checks pass, warning dispositions are recorded, and the reviewed authored diff has no unresolved P0 or P1. Open the tested pull request then; hosted CI and Codex review may run together.
- Cumulative cold-review budget: at most two comprehensive and two focused rounds across all sessions. Exhaustion or unsafe focus: save unpublished work on the branch with `-wip` appended and hand off to Hari, as rule 8 requires. The initial comprehensive review completed; the consolidated re-review below is the second comprehensive pass.
- Predicate map: the approved plan's Test map covers cleanup and protection predicates. This change contains no verifier work.

## Implementation choices and specification comparison

- `Git` and `Protection` are private dependency seams for tests. The production entry point accepts no arguments or alternate remote and checks both fetch and push URLs. Only the fixed read-only protection endpoints are used.
- The HTTP deadline bounds the entire request and body read with a Linux interval timer, in addition to the socket timeout. Each request and the overall run retain the plan's policy bounds.
- Git fetches immutable observed heads and walks their parent graph before ancestry comparison. Every deletion uses the full backup ref and its explicit expected head. The mapped tests exercise the actual commands against temporary bare repositories.
- The report distinguishes unmerged, changed, errored and deferred backups. Its mapped test covers escaping, totals, summary-file output and credential redaction, including apostrophes without re-encoding generated entities.
- The new workflow and mirror match byte for byte. A script extracted the approved rule amendment from the pinned plan and compared the entire modified AGENTS.md with the original plus that exact insertion; they match. Existing workflows are unchanged.
- AST comparison of the plan's named test map with the new test module found all 18 named cases, with none missing or added. No existing fixture or assertion was changed. No plan-check finding was marked `apply in implementation`; the r1 blocker was resolved in approved r2 before implementation.
- Hosted visibility of `bypass_actors` is deliberately conditional. Missing evidence fails closed; offline success does not establish the workflow token's hosted access.

## Commands and evidence

Base source: `92b81289b8c3fdc28883bb90bd9b56b48d2ba679`. Initial runtime implementation and test source: `e975344d7ab6baf65b924815f2f1d1013fb33d63`. The staged revision adds the HTTP protocol-error correction and regression cases below, plus item state and receipt metadata. Generated output is retained outside Git.

- The implementation subagent ran `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v` and reported 18 tests, all OK. The root's complete lifecycle run independently includes those same named cases.
- `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-448-tools PATH="/tmp/parchwell-448-tools:/tmp/parchwell-448-tools/venv/bin:$PATH" python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: exit 0; 380 tests in 106.470 s; OK. The suite's synthetic failure-path diagnostics are expected fixture output, not live Azure calls.
- `bash infra/scripts/lifecycle/install-tools.sh /tmp/parchwell-448-tools ci`: exit 0; installed the repository's pinned offline tools.
- `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-448-tools PATH="/tmp/parchwell-448-tools:/tmp/parchwell-448-tools/venv/bin:$PATH" bash infra/scripts/lifecycle/check-offline.sh`: exit 0; workflow parsing, actionlint, shell checks, chart acceptance and schema validation passed.
- `cmp .github/workflows/cleanup-wip-branches.yml docs/operations/workflows/cleanup-wip-branches.yml.txt` and `git diff --check`: passed.
- `npm run check:docs`: exit 0 before each comprehensive review; both scans completed with 9 advisory warnings, all in files this pull request does not change. No warning was reported in AGENTS.md or the item records. The unchanged-file warnings take the blanket disposition in the pull request under rule 8.

## Cold-review record

Current review label: `cleanup-r2`. Mode: consolidated comprehensive re-review of the entire authored diff after one correction. Input revision: source commit `e975344d7ab6baf65b924815f2f1d1013fb33d63` plus the frozen staged correction, regression tests, state and receipt. Comparison revision: `92b81289b8c3fdc28883bb90bd9b56b48d2ba679`. The supplied diff and its index tree identify the complete input. Starting this round consumes comprehensive 2, focused 0. Its complete final report belongs in the pull request's self-review record rather than being appended to its own reviewed receipt.

### Initial comprehensive report and disposition

Report: complete

Mode: initial comprehensive review, `cleanup-r1`. Reviewed tree `034b2aca00c5658e71d34438bf6813479351f866` (implementation commit `e975344d7ab6baf65b924815f2f1d1013fb33d63` plus frozen staged records), compared with `92b81289b8c3fdc28883bb90bd9b56b48d2ba679`.

Reviewed all seven touched files: `.github/workflows/cleanup-wip-branches.yml`, `AGENTS.md`, `docs/operations/workflows/cleanup-wip-branches.yml.txt`, `infra/scripts/tests/test_cleanup_wip_branches.py`, `items/448/receipt.md`, `items/448/state.md` and `scripts/cleanup-wip-branches.py`. Applied the rubric's general rules to every file, governance rules to the rule amendment, factory rules to item records, and workflow/script rules and blueprint ADRs to implementation and tests. Applied origin/main's AGENTS.md because that document changes.

The pinned approved plan is r2 at `ba3f821b9099cad4fd6f4d74acc22af5f0bc177b`. Its independent check and SHA-specific approval identify that revision. The later decision addresses r1 and repeats the main-only direction already incorporated into r2. Workflow/context restrictions, ancestry, exact leases, races, caps, protection predicates, literal rule wording and reporting are implemented within the allowed scope, except for the failure-report gap below. The post-merge dispatch remains pending as the plan requires. No scope creep or verifier changes found.

P1 `scripts/cleanup-wip-branches.py:198`: HTTP protocol failures escape the protection-error handler and prevent the required report, contrary to the plan's Protection check and requirements 6, 8 and 12. A truncated chunked HTTP 200 response raises `http.client.IncompleteRead`, which inherits `HTTPException`, not an exception caught there. It escapes cleanup and main, leaving a traceback instead of remaining `retained_error` rows, protection reason and totals. A later failure also loses the summary of prior deletions. The reviewer reproduced it with Python 3.12's real HTTPResponse reading truncated chunked bytes and through an injected protection response. Handle protocol exceptions through ProtectionError and cover the API error test.

Reviewer verification: all 18 cleanup tests passed; mirror comparison and whitespace check passed. The protocol-error reproduction fails the report contract independently of those passing tests.

Findings: 1 at P0 or P1

Pre-existing (not blocking): None identified.

Disposition: fixed by catching the `HTTPException` family through the existing ProtectionError path. The existing mapped API error test now reads a real truncated chunked HTTPResponse and verifies both initial failure and failure after one successful deletion, preserving totals and the remaining backup. This changes no contract or scope. `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v` on the corrected working tree passed: 18 tests in 23.433 s, OK. Whitespace validation also passed.

After Hari's time complaint, discretionary expansion and delegation stopped. The smallest remaining work is the mandatory correction check, documentation scan, this consolidated review, publication and required hosted gates, then the protected-merge handoff. Existing broader local checks are not repeated for this isolated HTTP exception correction.

## Post-merge measurement

Pending Hari's protected merge. The sole authorized dispatch is the plan's REST POST with `ref=main`; dispatch acceptance alone is not cleanup success. Missing protection visibility must remain a visible failure with no deletion.
