# B10 restart execution part 2 receipt

This pull request implements the approved trial orchestration plan. It adds the ordinary timeout Job before restart, proves its evidence stays diagnostic, and updates the four operations contracts. Hari merges the pull request; no trial or workflow dispatch is authorized here.

## Execution card

- Deliverable and scope: [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0c73e5139e1670d5b675a51fc52dbd99c4b1af49/items/404/plan-b10-execution-trial.md), sections 2 through 8.
- Current authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788005281), [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464153), and decisions [6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492), [6787982173](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787982173) and [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), point 3. The pinned file's SHA-256 was recomputed as `7339208e039f61ce1e41d4e057751aa5fba8e496cc5f5f676265a36396d41cc2`, matching the pointer, approval and task authorization.
- Mandatory gates: plan section 9; exact-head hosted Codex code and security reviews; authority refresh before publication; scope and protected-file checks. The trial remains gated by the separate offline test on main and Hari's dispatch decision.
- Exact verification commands: `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `bash charts/parchwell/tests/check-loadgen-host.sh`; `./scripts/verify-dotnet.sh`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; `cmp .github/workflows/b10-abrupt-trial.yml docs/operations/workflows/b10-abrupt-trial.yml.txt && git diff --quiet origin/main -- .github src`.
- Earliest authorized publication checkpoint: all applicable local acceptance checks pass and the complete authored diff has clean applicable cold-review coverage under pull-request rule 8. Publish then, with CI and hosted reviews allowed to run together.
- Review budget: at most two comprehensive and two focused cold-review rounds cumulatively for this pull request, under [pull-request rule 8](../../docs/agents/pull-requests.md). Exhaustion or unsafe focus saves a backup branch and hands off to Hari. No earlier review exists for this new pull request.
- Runtime-sized work: plan section 11(a) lists fixed synthetic inputs. Unplanned runtime-sized work follows AGENTS.md "Before opening a pull request" rule 11; no retry or finding-disposition allowance is invented here.
- Predicate map: plan section 8, with the exact P2-A and P2-B test additions below. No verifier predicate changes.

## Implementation choices required by the plan check

The [exact-revision plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788427229) states `Blocking findings: none`. Apply all three findings as directed:

- P2-A: T-O1 records the timeout pod read before the restart render/apply, with event order `['metadata', 'loadgen', 'timeout-termination', 'loadgen-restart', 'termination', 'publication', 'loadgen-successor']`; the timeout payload has the specified ordinary owner, image, container and terminated status.
- P2-B: T-O4 includes `OSError` on the timeout pod read and expects outcome `unknown`, a safety stop, the timeout failure name and no restart.
- P2-C: both affected operations pages use the exact producer-pod wording from the check, including retained finished Jobs until cleanup. This describes the enforced sequencing accurately.

## Verification and specification comparison

Pending implementation and acceptance commands. Baseline: `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`.

## Cold-review inputs and record

No cold review started. Cumulative comprehensive/focused counts: 0/0. Complete reports and dispositions will be linked from the pull request self-review record.
