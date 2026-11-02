# Issue 318 receipt

This pull request adds the choice `idle-observation` to the Scenario workflow and its byte copy, and the matching admission in `phase.py`. It runs the idle capture once per configuration, as a smoke only. It changes no verifier, `expected.yaml`, chart, worker or trigger, and it dispatches nothing.

## Execution card

- Deliverable: one pull request with changes 1 to 9 of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/318#issuecomment-6571101366), Revision 1 (`updated_at` 2026-11-02T06:57:48Z).
- Authorization: the plan was approved under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval". The [plan check 6571272561](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/318#issuecomment-6571272561) came from a fresh subagent, was posted after the plan's latest edit, and states `Blocking findings: none`; the plan states `None` for open questions. Item approval is the `ready-for-agent` label on parent #301 (2026-11-01T20:30:48Z); no comment names a tool, so Claude Code. Later decisions: [6563545683](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/318#issuecomment-6563545683) and [6569916507](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/318#issuecomment-6569916507); neither changes the plan.
- Mandatory gates: plan acceptance 1 to 7, the cold review under AGENTS.md "Before opening a pull request" rule 8, hosted CI, Codex review and security review. The pull request changes the protected path `.github/workflows/scenario.yml`; the merge is Hari's or the #301 control session's under the [override decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563515099), point 1.
- Publication checkpoint: open the pull request after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Plan deviations and choices

One deviation. The plan's five edit scripts ran as posted and each applied cleanly. Afterwards the second paragraph of the contract's "Workflow admission" (change 7) was reworded, because the plan's sentence said `validate-batch` and `prepare-batch` route to the idle module and that `single-action-seed` does nothing; both statements are wrong against `phase.py` (see the Review record). The plan check's one P2 (the `items` input description at `scenario.yml` line 25 lists only probe-batch formats) is not applied: the check judged it non-blocking and a fix would change acceptance 1's count and the pinned input map. `phase.py` rejects any value but `all` for this choice before login.

## Predicate map

- `run_kind`: `smoke` admitted; `batch` and `Smoke` rejected.
- `items`: `all` admitted; `A02`, `A02,A03,A04`, `ALL`, empty and unset rejected.
- Each of the three booleans: false admitted; `true` rejected; `yes` rejected for the capture flag.
- Scenario case variant `IDLE-OBSERVATION` rejected.
- Precedence: each rejected row keeps the other fields valid, so only the field under test can fail. Covered by `test_idle_observation_admits_only_the_smoke_tuple_before_side_effects` and `test_idle_observation_routes_only_to_batch_and_batch_stop`.

## Review record

- Comprehensive pass 1 on 940ed72 plus the receipt: one P1 (the state file's read time was later than its commit; now the real refresh time 2026-11-02T07:28:13Z) and one P2 (a wrong sentence in the contract's "Workflow admission" about which commands call the idle module; reworded). Both fixed. The plan's own text for change 7 carried the wrong sentence; the reworded text follows `phase.py` lines 304, 329, 833 and 866.

## Commands and results

Run on commit 940ed7275b63285d3bbe4087e950073f1518e91c (all code, tests and docs; this receipt is added after it), with CPython 3.12.3 and the pinned tools from `install-tools.sh ... ci`, and .NET SDK 10.0.402.

1. Acceptance 1: `cmp` of the workflow and its copy exits 0; the changed-line count prints `15`.
2. Acceptance 2: prints `triggers unchanged`.
3. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: 252 tests, OK.
4. `python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py`: 38 tests, OK.
5. `bash infra/scripts/lifecycle/check-offline.sh`: exit 0, including actionlint on the edited workflow.
6. `./scripts/verify-dotnet.sh`: locked restore, solution build, Kafka XML inventory, `Parchwell.ArchitectureTests` (14 passed) and `Parchwell.Enrichment.Tests` (56 passed) passed. `Parchwell.Kafka.Tests` failed: the session has no Docker daemon and its Testcontainers tests fail on the Docker connection. This change touches no Kafka code. The same stop is recorded in the #317 receipt. Not run on a clean `main` checkout.
7. `npm run check:docs`: 8 warnings, all in files this pull request does not change; no warning in a changed file.
8. Acceptance 6: `git diff --name-only origin/main...HEAD` plus this receipt lists exactly the ten files the plan names.
