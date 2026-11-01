# Unattended dispatch-end stop: authored receipt

The scenario workflow's stop-only job `batch-stop` now uses the `parchwell-sweeper` environment instead of `parchwell-scenarios`. It no longer waits for a second approval before stopping AKS. Nothing else in the workflow changes.

## Task brief and authorization

Directly authorized governance and workflow work for issue #240. It amends the approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145), whose item 8 keeps scenario.yml's "environment protection" unchanged, for `batch-stop` only. Hari's [decision #240/6556930056](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6556930056) records the amendment. Hari's instruction in an interactive session on 2026-11-01: "open a PR for that", meaning make the dispatch-end AKS stop run without waiting for an environment approval. After the governance conflict below was put to him, he answered "I approve the change just do it" (same session, 2026-11-01). Hari alone merges, because `.github/workflows/` is a protected path (AGENTS.md hard safety rule 8).

## Execution card

- Deliverable: `batch-stop` environment change, its byte-identical mirror, the matching phase workflow test, and the two docs that describe the gate.
- Mandatory gates: `cmp` of the workflow mirror, the phase workflow tests, `check-offline.sh`, `npm run check:docs`, one comprehensive cold review, Codex review, Hari's merge.
- Earliest publication checkpoint: draft pull request after the first cold review with no P0 or P1 other than approval-only findings.
- Review budget: AGENTS.md "Before opening a pull request" rule 8. Exhaustion action: hand off to Hari.
- Predicate-to-test map: not applicable. No verifier predicate changes.

## Findings that shaped the change

1. Only `batch-stop` runs the stop alone. Its step ids are `guard checkout stop_prepare login azure_session stop report`. `prepare-stop` validates the selection and the AKS resource ID and makes a private directory (`infra/scripts/phase/phase.py`, `prepare_stop`). The job references no secrets. `full-C3` also stops AKS, but it carries the C3 workload steps, so it stays on `parchwell-scenarios`.
2. The sweeper does not stop AKS while `batch-stop` waits. `dispatch-state.py` `lookup` defers both actions when any `scenario.yml` run has a status other than `completed` or `queued`. GitHub's run status values include `waiting` ([workflow runs API](https://docs.github.com/en/rest/actions/workflow-runs), read 2026-11-01). Inferred, not observed: a run whose job waits on a required reviewer reports `waiting`.
3. Governance conflict, resolved by Hari's approval above. Decision [#114/6332413257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257) point 4 says the sweeper runs under the second environment and that the sweeper workflow is destroy-only. The blueprint section 9 bullet said "Destroy-only recovery also trusts" that environment. Neither named a scenario-workflow job. This change amends the blueprint bullet to name `batch-stop`.
4. No external setting should change. GitHub's default OIDC subject for an environment job is `repo:ORG-NAME/REPO-NAME:environment:ENVIRONMENT-NAME`, with no workflow name ([OIDC reference](https://docs.github.com/en/actions/reference/security/oidc), read 2026-11-01). The existing `parchwell-sweeper` federated credential should therefore cover this job. Inferred: this assumes the repository uses the default subject format.

## Commands and results

Source commit: `6ebaed0` (main). Tools: pinned `ci` set from `infra/scripts/lifecycle/install-tools.sh`, run under Python 3.12 for its PyYAML wheel.

| Command | Result |
| --- | --- |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Passed |
| `python3 -m unittest infra/scripts/phase/tests/test_workflow.py` | 16 tests passed |
| `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | 185 tests ran. One unrelated test, `test_probe_v2_access.ProductionReaderTests.test_both_streams_capped`, recorded one failure and one error. This change does not touch that script. Not investigated, after Hari asked for no further time on this change. |
| `PARCHWELL_TOOLS_DIRECTORY=<tools> bash infra/scripts/lifecycle/check-offline.sh` | Exit 0 |
| `npm run check:docs` | Both changed docs have no issues. 8 warnings, all in files this change does not touch. |

The .NET build and tests were not run. No .NET file changed, and no architecture test reads job environments. CI runs them on the pull request.

## Security trade-off

Every job on `parchwell-sweeper` gets the workflow's Azure identity without a human gate. `batch-stop` now gets that identity whenever a probe batch dispatch on `main` reaches it. Its steps only stop AKS, but the token is the same app's token, with the same Azure permissions as an apply. Under AGENTS.md "Code Review Rules" this may count as a change that weakens identity scope. Hari approved it on 2026-11-01 knowing that.

## Not changed

`full-C3` keeps its approval wait. A01 and B04 dispatches still need approval before the stop runs.

## Cold-review record

Round 1, comprehensive, on the staged diff against `6ebaed0` (backup commit `2bb00ac`). Report complete. Findings and dispositions:

- P0, identity scope (`scenario.yml:882`, blueprint section 9). The finding is correct: `batch-stop` now gets the apply-capable app's token without a human gate. Disposition: not fixed, because removing the scope change removes the requested behaviour. Hari approved this trade-off on 2026-11-01. Escalated to Hari in the pull request description; it is not claimed invalid or cleared.
- P1, unrecorded plan amendment. Fixed: on Hari's instruction the session posted [decision #240/6556930056](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6556930056), and the receipt and blueprint cite it.
- P2, no test pins "reads no secrets". Fixed: `test_batch_topology_and_exact_steps` asserts that `batch-stop` has no `secrets.` reference.
- Pre-existing: `probe-v2-smoke-contract.md` said a dispatch keeps "the existing environment approval". Fixed to name the `batch` job. The failing `test_both_streams_capped` is unchanged by this pull request.

Cumulative count after round 1: 1 comprehensive pass, 0 focused rounds.

Round 2, consolidated comprehensive re-review, of commit `823db1d` against `6ebaed0`, with changes since `2bb00ac`. Report complete. Findings and dispositions:

- P0, identity scope: the same finding as round 1, counted once. Disposition: approval-only under rule 8, third paragraph, by Hari's [decision #240/6557004111](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6557004111). Kept, listed in the pull request description, excluded from round limits; Hari merges.
- P1, the round 1 P0 disposition was not one rule 8 allows. Fixed: Hari's decision #240/6557004111 makes the P0 approval-only, as #114/6336121859 did.
- P2, the no-secrets test caught only `secrets.`. Fixed: it now matches the bare `secrets` context name.
- P2, round 2's input named no revision. Fixed: this entry names `823db1d`.

Cumulative count after round 2: 2 comprehensive passes, 0 focused rounds.

Round 3, focused, of the changes after `823db1d`, with the full diff against `6ebaed0` as context. Report complete, no P0 or P1. Its two P2s touch only this receipt's prose and are fixed: round 1's entry is restored to its text at `823db1d`, and the test re-run below is recorded. A focused P2-correction round covers those fixes.

After the round 2 fixes, `python3 -m unittest infra/scripts/phase/tests/test_workflow.py` passed again: 16 tests, on the tree reviewed in round 3.

Cumulative count after round 3: 2 comprehensive passes, 1 focused round.
