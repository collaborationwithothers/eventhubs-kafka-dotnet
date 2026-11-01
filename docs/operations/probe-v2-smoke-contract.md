# Live probe batch 2 smoke

This smoke runs the new partition, null-value and permission-scope clients through the real evidence path. It checks whether the infrastructure and harness can produce complete evidence. It does not produce a scientific verdict.

The one exception is the published F01 result: a later verdict pull request judged the retained handler rows of authentication-controls smoke run 41201284963 with a pinned script, under [decision 6558920636](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6558920636) on issue 302 and [decision 6561458447](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/306#issuecomment-6561458447) on issue 306.

The [build instruction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494085278) authorizes this prerequisite. The [smoke implementation plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494160519) limits its scope. The completed smoke remains the operational prerequisite. The approved scientific implementation follows the [scientific contract](single-action-contract.md#scientific-assessment-and-permission-evidence); dispatch remains separately authorized.

## Dispatch and lifecycle

The full V2 smoke uses the Scenario workflow on main with `scenario=probe-batch-2`, `run_kind=smoke`, `items=all` and `capture_authentication_metadata=false`. Unset items retains `all`. The separately authorized authentication metadata route uses the same scenario and run kind with `items=authentication-controls` and `capture_authentication_metadata=true`; it selects only F06 C6 and C9. Other V2 smoke selections fail before private inputs or Azure access. Scientific batch selection accepts `run_kind=batch`, capture=false, and `items=all` or an ordered B08,E01,F06 subset; assessment follows the [scientific contract](single-action-contract.md#scientific-assessment-and-permission-evidence). The legacy probe batch retains its existing selectors and behavior with capture=false.

The stop job retains unconditional dispatch-end cleanup only for admitted routes. Both legacy routes require capture=false. The V2 smoke route admits all/false or authentication-controls/true; the V2 scientific batch route admits only its validated item selections with capture=false. Rejected selectors reach neither workload preparation nor cleanup Azure access. The exact workflow expression remains covered by the dispatch contract tests.

The V2 smoke runs real `--single-action-v2` and `--single-action-v2-judge` Jobs, with `image_smoke=false`. An image startup marker cannot satisfy this smoke. The workflow owns all infrastructure commands, admission, collection and cleanup under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Workers do not orchestrate Kubernetes or processes.

The [phase bridge](../../infra/scripts/phase/phase.py) publishes each worker image once. A registry digest read that exits with status 1 or 124 gets at most one additional read of the same reference, with the existing 60-second command limit on each attempt. Other command failures and successful reads with invalid or mismatched digests fail immediately. Exhaustion fails the phase and reports the worker, attempt and command exit status. A failed read's partial output cannot supply a digest, and image outputs are published only after every required worker has a valid digest. This recovery does not repeat publication, infrastructure work or client slots. Legacy scenarios retain one read attempt.

For `az acr repository show`, the [Azure CLI session wrapper](../../scripts/azure-cli-session/az) flushes `azure_cli_session_stage` records to stderr, retained in the Actions log. Records identify the process, fixed stage name, event and elapsed milliseconds since invocation; terminal stage events also report stage duration. Lock acquisition, authentication refresh, token request, login, account selection, forwarded command and exceptional child cleanup have separate markers. Returned child statuses have an `exit` record; `end` means the stage returned, not that its status was zero. Timeout, interruption and exception events use fixed labels. An unmatched start marker shows the last logged stage if the process is forcibly killed. Fresh sessions skip refresh stages. Marker writes are best effort: a closed or failing log sink must not bypass cleanup or replace the command outcome, but may leave missing markers. On a write/flush failure, the wrapper disables markers and attempts to redirect the failed stderr descriptor to the null device, so pending marker bytes can drain without another failed flush at interpreter shutdown. The new records contain no command arguments, resource names, environment values, credentials, response bodies or exception text. With a working sink, existing CLI output is unchanged; a failed stderr sink is discarded. No raw CLI debug output is enabled. These markers locate time spent in the wrapper; they do not reveal which internal HTTP or DNS operation stalled inside Azure CLI.

The matrix has five action slots per configuration and no replacement repeats. Independent helper clients use the same ordinal as their source action. An unadmitted helper constructs no client and retains the explicit admission reason in its receipt. The report records `admitted_client_slots` only when every actor capture was transported with successful final completion; otherwise the count is null. This counts declared admissions, not proven successful native client constructions. The stage/dependency matrix is owned by [the V2 evidence contract](single-action-contract.md#schema-two-live-probes).

Worker deadlines, byte limits and queue bounds come from [probe-v2-runtime-limits.json](probe-v2-runtime-limits.json). They are chosen execution limits, not performance measurements. A bound or cleanup failure leaves incomplete evidence and fails smoke. It never silently replaces a slot or authorizes another dispatch. One live smoke is authorized by the interactive instruction once the support is merged and runnable; a failed smoke retains evidence for diagnosis.

## Smoke acceptance

This section governs the full items=all smoke. The selected authentication-controls route does not establish full-smoke acceptance; its metadata acceptance, bounds and separate execution authorization are owned by the Short metadata collection card in [authentication-lifecycle-observation.md](authentication-lifecycle-observation.md#short-metadata-collection-card).

The verifier owns the following evidence decisions. Workflow transport consumes the candidate and completion receipt; it does not reinterpret Kafka observations.

- Every declared configuration must be complete, valid and deterministic across its five action slots, with its required independent helper evidence.
- Partition-selection controls require an acknowledged send, matching independent readback and actual partition metadata for both configurations. Observed partition placement remains evidence; the smoke does not publish a partition hypothesis verdict.
- Null-transport controls require independent seed delivery/read, target delivery and independent target readback. Null and empty bytes remain distinct. Observed target bytes remain evidence rather than a fabricated shape error when they differ from expectation.
- Permission-scope positive controls require send/read success in C0/C1/C3/C5 and exact seeded reads in C6/C7/C8/C9. Negative errors or the allowed bounded absence in C10/C11 remain observations; absence does not mean denial.
- A failed required positive control adds `smoke_controls_failed`. Any such diagnostic, invalid/incomplete evidence, nondeterminism or `security_finding=true` prevents acceptance. Unexpected narrow-identity success remains a security finding even when another evidence component is incomplete.
- Every V2 candidate contains `item_assessment:null` and diagnostic `smoke_only`. Transport rejects a non-null assessment. No expected-outcome or results file is generated by this smoke.
- Candidates require their matching successful final judge receipts, exact source/run/coordinates and raw-byte hashes. Successful infrastructure teardown and cluster stop are required too.

Actual main source commit, immutable image digests, owner-bound pod evidence, registrations, captures, candidates, final receipts and cleanup outcomes form the smoke evidence. Missing evidence cannot be replaced with successful fixture data, logs, or zero counts. Applicable hosted mutation acceptance remains required before a later scientific evidence pack under [the factory policy](../agents/factory.md#evidence-pack).

## Historical smoke permission evidence

The conditional namespace grants are the [ADR-003 test-only exception](../blueprint.md#adr-003-entra-only-authentication-with-aks-workload-identity-and-a-custom-oauthbearer-refresh-handler). The probe identity receives namespace Sender and Receiver only for the explicitly selected V2 environment. Existing control identities, verifier permissions, Blob grants and network isolation stay unchanged.

Terraform's sensitive `probe_v2_role_scope` output records the actual namespace, topic resource IDs, workload identities and managed assignments. It cannot prove the absence of inherited, group or additional direct access. The protected scenario secret `PARCHWELL_PROBE_V2_EFFECTIVE_SCOPE` supplies Hari's reviewed confirmation for the narrow controls:

```json
{
  "schema_version": 2,
  "namespace_id": "<actual namespace resource ID>",
  "accounts": {
    "loadgen": {"principal_id": "<actual principal>", "no_additional_data_access": true},
    "enrichment-consumer": {"principal_id": "<actual principal>", "no_additional_data_access": true}
  }
}
```

The confirmation covers both controls on all three targets, including direct, inherited, group and custom-role data access beyond the managed assignments. It is a trusted reviewed assertion, not a conclusion manufactured from Terraform. Missing or mismatched confirmation prevents permission-scope acceptance.

The confirmation is bound to the namespace and the two principals it describes, not to a source commit, so a merge to `main` does not invalidate it. Hari re-issues it when either narrow identity, the namespace or the intended access of the narrow controls changes. It does not detect access added in Azure after it is signed. Version 1 confirmations, which carried `source_commit`, are rejected.

Only smoke preparation receives that secret as `S_PROBE_V2_EFFECTIVE_SCOPE`. It keeps raw bytes private, validates the schema version, namespace and principals against deployed outputs, and records their SHA-256. The workflow materializes the bounded private role snapshot after namespace reset. Only judges mount it. Public candidates contain aliases and scope classes, never raw role material, tenant, subscription or principal identifiers.

## Completion and subsequent work

Protected workflow, identity, verifier and design changes reach main through Hari's merge. Dispatch uses main and retains the existing environment approval for the `batch` job; the stop-only `batch-stop` job runs without one ([single-action contract](single-action-contract.md#mode-selection-and-completeness)). The full experiment has its own revision-specific implementation approval; it inherits no scientific dispatch authorization from this prerequisite.

The [completed smoke receipt](evidence/issue-240/live-smoke-receipt.md) records the successful run on 2026-10-30, its candidate/final-receipt and pod evidence, and successful teardown and cluster stop. It completes this smoke prerequisite. Scientific item assessments remain null; the scientific implementation has its own approval, while live execution still needs separate authorization.

The [statistics repair](evidence/issue-240/statistics-repair-receipt.md) does not revise acceptance of the original failed smoke. Its local native and transport regressions establish harness feasibility, not Event Hubs acceptance. No replacement dispatch is authorized by that repair's task brief; another live smoke requires separate authorization and the existing protected environment approval.

Scientific F06 uses the [measured permission inventory](single-action-contract.md#measured-permission-inventory-v8); the historical confirmation above is not a scientific fallback. Platform policy and the scoped Ubuntu exception belong to [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates).
