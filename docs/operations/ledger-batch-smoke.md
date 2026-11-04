# Ledger batch smoke and batch (B06 and B07)

The Scenario workflow choice `ledger-batch` runs every B06 and B07 configuration in one job, as a smoke or a [batch](#batch). The smoke runs one repeat of each: it checks that both items run end to end and records what the producer's statistics and ledger show, so the B06 and B07 decision rules can be written from observed evidence. Smoke output is not a result and is not published under `results/`.

## Dispatch

Dispatch on `main` with `scenario=ledger-batch`, `run_kind=smoke` (or `run_kind=batch`, below) and `items=all`, with the three boolean inputs false. `phase.py validate-batch` rejects any other selection before login with `unsupported ledger batch selection`. The existing `batch` job runs either and `batch-stop` stops the cluster at dispatch end.

## What runs

Configurations run in this order: B06 C0, B06 C1, B07 C0, B07 C1. Each runs the start-line, loadgen and verify Jobs for repeat 1. A smoke has no judge, because the judge reads five repeats. Each item has its own run identity, `<dispatch run>-b06` or `<dispatch run>-b07`, because ledger and verifier blob paths start with the run identity and the phase, and each phase equals its configuration.

## Output

For each configuration the job writes `<item>-<configuration>-smoke-summary.json` to the `ledger-batch-candidates-<run>-<attempt>` artifact. The phase script downloads that configuration's `statistics.jsonl` and `ledger.jsonl` from the `ledger` container into its private directory, at most 268435456 bytes each (a cap chosen for this harness, not a measured or vendor limit: `BLOB_MAXIMUM` in `infra/scripts/phase/ledger_batch.py` and the approved plan's answer (a)), and publishes only the summary:

- `statistics.rows`: per statistics report, `ts`, the sum of each broker's `req.Produce` counter, and each topic's `batchcnt` fields `cnt`, `sum`, `min`, `max`, `avg` and `p50` as reported.
- `ledger`: row count, delivered rows, and the count, minimum, nearest-rank 50th and 95th percentiles and maximum of `delivery_latency_ms`.

A missing, oversized or malformed file gives `complete: false` with a reason, and the other configurations still run. The step fails if any Job or summary is incomplete.

Field sources are in [Statistics bridge](statistics-bridge.md).

## Batch

Dispatch on `main` with `scenario=ledger-batch`, `run_kind=batch`, `items=all` and the three boolean inputs false. Per configuration, in the order above, the `batch` job runs start-line, loadgen and verify for repeats 1 to 5, then the judge, then downloads `<item run>/<configuration>/candidate.json` from the `verifier` container and checks its identity, source commit, image digests and five repeats. It writes `<item>-<configuration>-candidate.json`, or `<item>-<configuration>-gap.json` with reason `start_cap`, `orchestration_deadline`, `not_attempted` (the judge did not start, as after a safety stop), `download_failed` or `candidate_contract`.

Each configuration has 3300 s, the last 870 s reserved for the judge and download, and starts only while 3300 s remain of a 14400 s window (`infra/scripts/phase/ledger_batch.py`); these are harness caps, not measured or vendor limits. The step fails on a bad Job outcome, an unaccepted candidate or a safety stop.
