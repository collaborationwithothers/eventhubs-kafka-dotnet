# B10 workflow

The B10 workflow runs the committed synthetic timeout scenario with two sequential producer Jobs per repeat, followed by a phase sweep. The sweep captures late-record frequencies and locations for diagnosis. The second worker validates predecessor evidence against that predecessor's authenticated client identity; the [implementation receipt](../../items/355/receipt.md) records the regression checks. Live expiry frequency, persistence and runtime viability remain unmeasured until Hari's later smoke.

## Inputs and execution

The [scenario](../../scenarios/B10/scenario.yaml) selects phases C0 and C1, with five repeats each. Both use `message.timeout.ms=100` and `max.in.flight=5`; C0 inherits `enable.idempotence=false`, and C1 overrides it to true. The [synthetic timeout profile](../../scenarios/profiles/b10-timeout.yaml) retains the saturation profile's seed, tenant mix, payload distributions and stage rates, with four 10-second stages. Its configured event and attempt bounds, rather than observed live counts, are checked in [the implementation receipt](../../items/355/receipt.md).

Each repeat runs start-line, the first producer, the second producer and verify, in that order. The first producer permits its existing single application retry for a permitted timeout. The second producer discovers the complete predecessor publication before registration or Kafka construction and resends the selected failed attempts once, without application retry. The workflow observes actual first-pod termination before launching the second Job; a successful process exit alone cannot authenticate a publication. The existing worker owns its 30-second predecessor discovery budget.

The metadata-only `b10-sweep-line` Job follows all repeat slots. The workflow takes the latest actual terminated `finishedAt` from the ten retained producer pod observations and the latest completion time from the five bound verify receipts. Each observation is attempted once. Retrieval and parsing share a 120-second budget, with a 65536-byte limit per receipt. Missing, malformed or unbound lifecycle evidence leaves an explicit gap and no invented sweep note.

The workflow waits until the later of the last verifier completion and the last producer termination plus 60000 ms. This is an explicit harness settle budget, not a bound on Event Hubs append latency. Capture receives `B10LastVerifierCompletedUtc`, `B10LastLoadgenTerminatedUtc` and `B10SettleIntervalMilliseconds`; it does not wait or orchestrate. It captures document-events partition highs, cleans up its Kafka session, and creates the note once. The existing [sweep-note contract](verifier-contract.md#b10-phase-sweep-finalization) owns timing, schema, identity and byte validation. The host's final completion receipt follows host disposal; uncertain create-only publication is not retried or overwritten.

The `b10-sweep` Job then authenticates and reconciles the repeats. Judge remains the final separate step. B10 verify's nonfinal failure exit remains a diagnostic failure; the workflow does not relabel it as successful scientific evidence. An unknown live pod prevents subsequent launches until existing deletion and confirmation establish safety.

All repeat modes, lifecycle observations, settling, capture, sweep and judge share one absolute 18000-second orchestration deadline. Operations use only its remaining time. Exhaustion records unattempted modes and gaps, stops new producers, preserves available diagnostics and leaves cleanup to the workflow. This cap is shorter than the conservative configured worst-case envelope; it does not guarantee successful completion. Missing or failed sweep evidence cannot authorize finality or counts.

## Diagnostic events

Sweep pod logs contain structured events with `scenario_id`, `run_id`, `phase`, `source_repeat` and `observation_complete`:

| Event | Additional fields |
| --- | --- |
| `B10LateObservationSummary` | `observation_available`, `total_selected_late_records`, `retained_samples`, `omitted_samples` |
| `B10LateObservationGroup` | `instance_id`, `partition`, `landing_repeat`, `selected_late_records` |
| `B10LateObservationLocation` | `instance_id`, `attempt_id`, `partition`, `offset`, `landing_repeat` |

A complete observation requires the whole phase scan to succeed and that source repeat to reconcile successfully. Unavailable totals remain absent/null rather than fabricated zeros. Partial observations are explicitly marked incomplete and cannot support a complete frequency claim. A failed diagnostic provider cannot change finalization or outcome counts.

Diagnostic projection and log emission still run before the host publishes its completion receipt and inside the process-lifetime watchdog window. Slow diagnostic work can therefore delay the receipt or prevent its publication if the watchdog terminates the process.

Only reconciled own late records are selected, with each topic/partition/offset counted once. The location sample retains the first 100 records per source repeat in partition/offset order. `omitted_samples` records the excess while complete grouped frequencies still cover every selected record. With two instances, 32 partitions and six landing labels, the group bound is 384 per source repeat. These are configured diagnostic limits from the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/355#issuecomment-6669538298), not measured frequencies. Existing phase record and byte limits still control input work.

`landing_repeat` is a later repeat number only when the offset lies inside that repeat's recorded half-open original window; otherwise it is `gap`. An offset at a window's start is inside, and one at its end is outside. Logs carry producer instance and attempt identifiers, not tenant/document payloads or private Azure identities. A late record is not automatically a duplicate. Authenticated classifier totals and diagnostic frequencies answer different questions; logs cannot authorize the former.

## Retrieval and later smoke record

Public Actions artifacts retain the `b10-C0` and `b10-C1` pod-artifact prefixes, including per-Job logs and `pods.json`. `phase-status.json` records attempted and unattempted workers and gap reasons, with B10 lifecycle observations under `b10_lifecycle`. Private workflow inputs are separate from public evidence.

During the run, the verifier container holds these fixed phase-root objects, where `U` is the run ID and `P` retains the original phase case:

- `U/P/sweep-line.json`: captured partition highs and supplied lifecycle timestamps.
- `U/P/b10-sweep-line-completion.json`: capture completion receipt, bound to `<U>-<lowercase-P>-b10-sweep-line`, cleanup success and the note's actual length/hash.
- `U/P/b10-finalization.json` and `U/P/b10-sweep-completion.json`: existing authenticated sweep output and receipt.
- `U/P/rN/verify-completion.json`: immutable repeat completion evidence.
- `U/P/candidate.json`: the judge's candidate; the workflow downloads the phase candidates under their B10 prefixes.

These objects remain in ephemeral run storage only for the run. Public pod logs are diagnostic artifacts, not replacement ledger evidence or authoritative outcome totals. Retrieval and retention follow the [verifier contract](verifier-contract.md) and [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates).

After the implementation merges, Hari may start the scenario workflow on `main`, selecting B10 and approving its environment. The current implementation session starts no workflow or smoke. For that later smoke, retain each phase/repeat's permitted-timeout count and persistence status, first-instance retry count, second-instance resend count, late-record frequency and sampled locations including gaps, omitted sample count, guard failures, scan completion, worker gap/exit evidence and candidate authentication. Missing observations stay unavailable. A clean local harness test does not establish live behavior.

The scientific rule remains [ADR-021](../blueprint.md#adr-021-b10-accepts-timed-out-sends-application-retries-and-one-producer-restart-in-a-second-job-and-classifies-late-appends-up-to-a-sweep-line). Every later B10 publication must state: **A late append after the phase sweep line is not detected.** Expected outcomes remain successor work. Mutation acceptance is not met; a later passing main measuring run covering the changed target remains required before evidence publication.
