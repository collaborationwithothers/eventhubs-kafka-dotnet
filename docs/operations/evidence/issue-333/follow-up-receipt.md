# Issue 333 follow-up receipt

This pull request adds one test that refuses each invalid idle-observation `items` value on its own, so the rejection coverage the merged pull request #336 removed for `A02` and `A02,A03,A04` is backed by separate refusals. It changes tests only.

## Execution card

- Deliverable: `test_idle_observation_refuses_invalid_items_before_side_effects` in `infra/scripts/phase/tests/test_phase.py`, plus `items/333/state.md` and this receipt.
- Authorization: [decision 6598714347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/333#issuecomment-6598714347) on #333, which resolves the Codex P1 thread on #336 with a follow-up. The acceptance assertions for `A02`, `A02,A04` and `A02,A03,A04` stay under [silent-drop decision 6596426514](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6596426514), point 3(a).
- Mandatory gates: the repository checks, the cold review under AGENTS.md "Before opening a pull request" rule 8, Codex review of the head.
- Publication checkpoint: after the first cold review reports no P0 or P1.
- Review budget: rule 8 as written. Exhaustion action: its review-budget handoff to Hari.

## Specification comparison

The decision lists out of order, unknown, duplicate, empty or stray separators and wrong letter case. The test covers each, for run kinds `smoke` and `batch`, with these cases (24 labelled values) plus an unset `PARCHWELL_BATCH_ITEMS`. Each case must make `validate-batch`, `prepare-batch` and `prepare-stop` exit 2, write the error `unsupported idle observation selection`, create no file and leave the environment and output files unchanged. `phase.py` is unchanged: `idle_items()` implements exactly these refusals (value not `all` and not an ordered subset of `A02`, `A03`, `A04`). No admission defect was found.

## Verification

Run on source commit c878a9a plus this change.

- `python3 -m unittest tests.test_phase -k invalid_items` in `infra/scripts/phase`: 1 test, OK.
- `python3 -m unittest tests.test_workflow` in `infra/scripts/phase`: 17 tests, OK.
- `python3 -m unittest tests.test_phase`: 66 tests, 17 failures, all in chart rendering because `helm` is not installed in this session. The same 17 fail on `main` without this change (65 tests).
- `npm run check:docs`: 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, `docs/operations/evidence/issue-124`, `issue-140`, `issue-211` and others); none in the files it changes.
