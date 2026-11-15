# Probe diagnostics contract-grade plan

Revision: 1. Base main: `50bfed766f1cd3a7bc1bf1a2f5a82ad031a1e497`.

## Goal and authority

Make a failed configuration isolation smoke identify the completed and failing private connectivity operations without exposing private data. The [handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6896141906) records why the first run was inconclusive; [Hari's authorization](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6896787852) defines this follow-up. The [retained-storage plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/52be904ea8e4d6e25a77fdc5f051ced88933b6e7/items/491/plan-retained-storage.md) and its [Entry A approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6893734659) remain in force. This is one coherent diagnostic PR. Estimate, 2026-11-15: 3 files and 250-450 changed lines, based on the duplicated inline probe scripts, receipt and existing test seams; not a cap.

## Scope

Only `.github/workflows/configuration-isolation-smoke.yml`, its byte-identical `docs/operations/workflows/configuration-isolation-smoke.yml.txt` mirror, and `infra/scripts/phase/tests/test_configuration_isolation_workflow.py` may change. No scientific checks, expectations, results, triggers, networking, infrastructure, production Entry B, retries, Azure dispatch or protected-path merge. Preserve review history and counters; apply the applicable cumulative review budget and existing decisions, without resetting them by calling this a follow-up.

## External contract

The public receipt adds exactly one line per combination in this ordered set: phase `C0`, `C1`; context `settings`, `idle`, `active`; perspective `runner`, `cluster`; service `eventhubs`, `blob`; operation `dns`, `tcp`. Only C1/settings/runner exists; C0/settings and cluster/settings combinations are omitted. For each other combination, use `PHASE CONTEXT PERSPECTIVE SERVICE OPERATION: STATUS`, replacing capitals with the fixed values above. Status is `passed`, `failed`, `timeout`, `not_attempted`, or `unavailable`. `timeout` is used only when the operation reliably identifies a timeout; a nonzero `nc` result is `failed`. Missing unattempted observations are `not_attempted`; a missing or invalid collection after an attempted cluster probe is `unavailable`, never a guessed success. Receipt output contains only these fixed aliases and statuses, not raw probe output, addresses, hosts, IDs, command arguments, or exception text. Private intermediate data is not uploaded.

## Requirements

1. WHEN each runner operation starts in C0 or C1 idle/active or the separate C1 settings probe THEN record its fixed phase/context/perspective/service/operation and outcome, preserving earlier results if a later operation fails. Label the settings probe C1 explicitly; it precedes the C1 phase-file update. Preserve fail-fast order: runner Event Hubs DNS then TCP, Blob DNS then TCP; C1 settings has the same order. Preserve exact DNS answer-set equality and exact peer-IP equality and 15-second TCP timeout. DNS mismatch, lookup error or peer mismatch is `failed`; socket timeout is `timeout`; other connection errors are `failed`. Record before returning a sanitized failure and retain nonzero step outcome.
2. WHEN each in-cluster idle or active operation runs THEN preserve the existing order: Event Hubs DNS, Blob DNS, Event Hubs TCP, Blob TCP. Preserve its exact DNS equality and `nc -z -w 15` checks and failure gate. Emit only fixed per-operation outcomes; `nc` failure is `failed` unless a reliable timeout distinction is available without changing its timeout or adding retries. Capture completed and failing rows on a failed Job before deletion, using bounded existing command mechanisms. A failed wait, log collection, parsing or deletion must never be converted into success; delete the probe Job in the existing finally path. Do not print raw logs or exceptions. A missing or invalid log after an attempted probe is `unavailable`; no output supplied by the pod is interpolated into the receipt without strict validation.
3. WHEN either phase or an operation fails THEN the always-run receipt retains all previously completed and failing observations, including C0 when C1 fails, and reports not attempted or unavailable for the rest. It validates exact allowed keys, fixed identities and status values before emitting a line. Invalid, duplicate or injected records cannot leak their contents or become a pass. A failure in diagnostics recording or collection remains a failing step and does not suppress cleanup. Preserve the current worker, infrastructure and cluster cleanup conditions, ordering and final gate.
4. WHEN tests exercise a changed probe or receipt THEN run actual extracted workflow code or shell, not a duplicate model. Keep every existing assertion and boundary. If an existing fixture is rejected by a new check, either update it to pass while preserving every assertion and boundary, or, when invalid input is its purpose, retain input and change expected values to exactly the new checks' outputs. Never remove a case, loosen an assertion or change a check. PR implementation notes list each fixture change and its forcing check; cold and Codex reviews check each.

## Test map and acceptance

- `test_runner_probe_diagnostics`: exercise both phases and both services, idle and active plus C1 settings. Exact synthetic DNS sets containing only expected IP pass; empty, wrong and mixed sets fail. Inject lookup exception, timeout, other socket exception and wrong peer, with sentinel private text. Assert fixed status, 15-second timeout and no later call after failure; preceding successes remain, later records are `not_attempted`; captured public output excludes sentinel, addresses and exception.
- `test_cluster_probe_diagnostics`: execute shell with stub DNS and TCP for each phase/context/service, successful and failing DNS and TCP, including mixed DNS; assert exact expected-IP calls, `-w 15`, ordering, no retry, fixed success/failure lines and no raw text. Execute failed wait and log-fetch path to show earlier rows and failing row survive before deletion; missing, malformed and injected output yields `unavailable` without leaking it, failure remains nonzero, and deletion is attempted even on failure.
- `test_receipt_probe_diagnostics`: execute receipt with C0 successes and failure, C1 successes and failure, missing, malformed, duplicate and injected records. Assert literal line format and statuses above, prior C0 and C1 observations retained, `not_attempted` vs `unavailable` as specified, and no injected address, resource ID or exception appears. Verify receipt upload and final report still fail closed and cleanup guards remain intact.
- Existing workflow tests continue to test exact private DNS, binding, worker delivery, settings, cleanup, mirror, concurrency and trigger. Run `python3 -m unittest infra.scripts.phase.tests.test_configuration_isolation_workflow` and the existing offline workflow checker/tests as identified by the test suite; require success. Run `npm run check:docs` and apply `docs/agents/pull-requests.md` rule 8. Follow its verification gates and review budget; report final-head CI and required hosted review. No live execution is authorized here.

## Invariants and deployment assumptions

No probe changes its connection target, port, timeout, ordering, private-IP equality, fail-fast gate, worker checks or cleanup. The receipt is diagnostic, not proof of network cause or scientific success. The in-cluster shell and log collection are tested offline against the workflow's rendered commands. Existing deployed shell/tool behavior and connectivity remain unproven by these tests; no change to their assumed capabilities is required. Fresh connections are checked at idle and while the loadgen Job is active; exact overlap with delivered traffic remains unproven under [decision 6894099185](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6894099185).

## Deferred work

The failed smoke's storage preservation, private connectivity and LAW ingestion gates remain unproven ([handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/491#issuecomment-6896141906)). A successor control session needs separately authorized paid main-only smoke after this protected PR is merged by Hari; its receipt and ingestion evidence must satisfy the retained-storage plan before production Entry B approval. No extra dispatch is authorized by this plan. The previous one-dispatch bound is exhausted; obtain new authority for any further run and its bound and exhaustion action. Production Entry B remains blocked until that evidence and approval.

## Sources

[Current smoke probes and receipt](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/50bfed766f1cd3a7bc1bf1a2f5a82ad031a1e497/.github/workflows/configuration-isolation-smoke.yml#L156); [existing tests](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/50bfed766f1cd3a7bc1bf1a2f5a82ad031a1e497/infra/scripts/phase/tests/test_configuration_isolation_workflow.py#L85); [blueprint network design](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/50bfed766f1cd3a7bc1bf1a2f5a82ad031a1e497/docs/blueprint.md#L116). This plan makes no new Azure, Kafka or librdkafka behavior claim.

## Plan-check questions

The finite probe matrix and test cases above bound execution; no runtime-sized work is authorized without the first-measurement rule. This plan adds diagnostics and failure-path tests to Entry A under Hari's follow-up authorization, without tightening its private-network or scientific gates. Tests execute the workflow code in this PR and synthetic stubs; no test depends on unmerged code or live Azure behavior.

## Open questions for Hari

None.

## Stop rule

Stop and hand off under AGENTS.md GOVERNANCE "Handoffs" if a contract, requirement or scope must change. Apply the review-budget and first-measurement rules without exception.
