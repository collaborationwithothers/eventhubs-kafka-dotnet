# Issue 245 startup session diagnostics

This receipt records the offline startup diagnostics repair for issue 245. The synthetic tests confirm wrapper behavior only, and the original timeout cause remains unknown.

## Execution card

- **Deliverable:** Enable the existing bounded session-stage diagnostics for exactly `aks show`, `aks start` and `aks stop`, add fixed synthetic wrapper tests, and record this receipt.
- **Authorization:** [Startup-session-stage-diagnostics plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6542841137), SHA-256 `f44d5aa73d75a5f00f46d7a7808bf78afd18d26492d6de45aeda0ee898c58981`, matched by [plan check 6542876818](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6542876818) (no blocking findings), under [continuation decision 6542787150](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/245#issuecomment-6542787150).
- **Source baseline:** `e76c7602b948946eb4e37c56d91ab79fbcaf3ba5`.
- **Mandatory gates:** Exact offline wrapper and lifecycle unittest commands, staged `npm run check:docs`, and `git diff --check`; preserve current authentication, timeout, output, status, cleanup, and privacy behavior.
- **Exact commands:**
  - `python3 infra/scripts/lifecycle/command.py 60 -- python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v`
  - `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`
  - `npm run check:docs`
  - `git diff --check`
- **Publication checkpoint:** First checkpoint after the offline gates and applicable cold coverage pass. Coordinator owns publication. No live run is authorized; the original timeout cause remains unknown.
- **Review budget:** This PR starts at 0 comprehensive / 0 focused reviews. Apply AGENTS.md "Before opening a pull request" rule 8 cumulative limits and exhaustion handoff. The merged handler PR history remains 2 comprehensive / 1 focused. Runtime-sized corrections are capped at ten root-cause dispositions; use the plan's WIP handoff if unresolved at the cap.

## Implementation and test map

The only production predicate change is `argv[:3] == ["acr", "repository", "show"] or argv[:2] in (["aks", "show"], ["aks", "start"], ["aks", "stop"])`. Existing stages remain `lock`, `authentication`, `token_request`, `login`, `account_select`, `command`, `cleanup`.

- Exact AKS verbs `show`, `start`, `stop`: cold and fresh states check stage order, bounds, quiet flags, request/launch counts, forwarded argv, and status.
- Lock contention: each verb checks `lock/start`, `lock/timeout`, 60-second timeout, no auth or child launch, and unchanged timeout output.
- Authentication timeout at token request, login, and account selection: each verb checks timeout stage and enclosing authentication, no command/later auth/freshness file, and secret-text suppression.
- Fresh child outcomes (status 1, status 124, timeout, SIGINT): each verb checks status and command stage events.
- Real binary stdout/stderr and statuses 0/17: each verb checks exact stream preservation, stage starts, and absence of cleanup.
- Disabled selectors include partial and near-match AKS forms, uppercase, resource, and ACR list; each checks forwarded status/argv and absence of markers. Existing ACR positive cases remain.
- Real subprocess timeout and SIGTERM: each verb checks flushed command marker while alive, cleanup markers, final command event, status, and empty stdout.
- Existing diagnostic-sink-failure and cleanup tests remain unchanged and mandatory.

All values are synthetic. Diagnostics are checked against synthetic group/name/subscription, bearer, assertion, endpoint and exception strings. No real credential or token contents are inspected.

## Verification record

Implementation source was baseline `e76c7602b948946eb4e37c56d91ab79fbcaf3ba5` plus this uncommitted diff. At final offline verification the wrapper SHA-256 was `b02106e9f6cf0fa7389ebe943e13c9ca41035841b6b3dbb2c2edeb1c2b63dd3f` and the test SHA-256 was `ce87d7fd2cbfc2b42b331954a1f2d7f6042fa3d726a4e096bf1c571ba8ed43cd`.

- Wrapper acceptance command: `python3 infra/scripts/lifecycle/command.py 60 -- python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v`. Final result: **38 tests, 0 skipped, passed** in 11.038 s. Log: `/tmp/issue245/startup-diagnostics/session-tests.log`.
- Earlier wrapper invocations failed while test assertions were being aligned to existing output. Their logs were overwritten by the final passing run and are not retained, so their counts are unavailable. The observed failure classes were event-order/status assertions, timeout-output ordering, and the disabled-selector fake-launch behavior. Corrections were limited to test expectations and synthetic launch fixture behavior; the final log above is the authoritative acceptance result.
- Lifecycle acceptance command: `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`. First run lacked Helm on PATH: **224 tests, 0 skipped, 24 failures and 100 errors**; log `/tmp/issue245/startup-diagnostics/lifecycle-tests.log`. Errors reported missing `helm`; phase rendering tests consequently failed. Reran with the existing pinned Helm `v4.3.1+gc0d08d0` from `/tmp/issue245/tools` on PATH: **224 tests, 0 skipped, passed** in 26.388 s; log `/tmp/issue245/startup-diagnostics/lifecycle-tests-pinned-tools.log`.
- Bounded correction dispositions: four root causes (align assertions to emitted event ordering/status location; make the disabled-selector fake launch record and return the child status; preserve real forwarded fixture behavior; add the existing pinned Helm tool directory to PATH after the initial lifecycle run exposed this environment setup omission). The privacy, authentication-prefix, and returned-124 status assertions were added as plan-required coverage. Total remains within the plan's ten-disposition cap.
- `npm run check:docs`: completed, exit 0, all three changed files have no issues. It reported eight warnings, all in unchanged files: `docs/agents/factory.md` (two undefined `S0` identifiers), `docs/operations/evidence/issue-124/README.md` (missing heading), `docs/operations/evidence/issue-140/README.md` (two missing-file links), and `docs/operations/evidence/issue-211/receipt.md` (three undefined `S0` identifiers). These unchanged-file warnings are covered by one blanket disposition under AGENTS.md "Before opening a pull request" rule 8. Log: `/tmp/issue245/startup-diagnostics/check-docs-final.log`.
- `git diff --check`: exit 0 after the final source and receipt updates. Output: `/tmp/issue245/startup-diagnostics/diff-check.log`.

All test values are synthetic. Offline fixtures establish wrapper behavior only. There was no live retry, and the original timeout cause is unknown.

## Review record

Complete cold-review reports and dispositions, input/comparison revisions, and cumulative review counts will be recorded here or linked after review. Current cumulative count: **0 comprehensive / 0 focused**. The next review is comprehensive R1 with full comparison against baseline `e76c7602b948946eb4e37c56d91ab79fbcaf3ba5`; the coordinator freezes the review input SHA externally. This receipt must not contain a report authored against the revision that includes that report.
