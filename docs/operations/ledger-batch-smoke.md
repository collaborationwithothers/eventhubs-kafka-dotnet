# Ledger batch smoke (B06 and B07)

The Scenario workflow choice `ledger-batch` runs one repeat of every B06 and B07 configuration in one job. It is a smoke: it checks that both items run end to end and records what the producer's statistics and ledger show, so the B06 and B07 decision rules can be written from observed evidence. Its output is not a result and is not published under `results/`.

## Dispatch

Dispatch on `main` with `scenario=ledger-batch`, `run_kind=smoke` and `items=all`, with the three boolean inputs false. `phase.py validate-batch` rejects any other selection before login with `unsupported ledger batch selection`. The existing `batch` job runs the smoke and `batch-stop` stops the cluster at dispatch end.

## What runs

Configurations run in this order: B06 C0, B06 C1, B07 C0, B07 C1. Each runs the start-line, loadgen and verify Jobs for repeat 1. There is no judge, because the judge reads five repeats. Each item has its own run identity, `<dispatch run>-b06` or `<dispatch run>-b07`, because ledger and verifier blob paths start with the run identity and the phase, and each phase equals its configuration.

## Output

For each configuration the job writes `<item>-<configuration>-smoke-summary.json` to the `ledger-batch-candidates-<run>-<attempt>` artifact. The phase script downloads that configuration's `statistics.jsonl` and `ledger.jsonl` from the `ledger` container into its private directory, at most 268435456 bytes each, and publishes only the summary:

- `statistics.rows`: per statistics report, `ts`, the sum of each broker's `req.Produce` counter, and each topic's `batchcnt` fields `cnt`, `sum`, `min`, `max`, `avg` and `p50` as reported.
- `ledger`: row count, delivered rows, and the count, minimum, nearest-rank 50th and 95th percentiles and maximum of `delivery_latency_ms`.

A missing, oversized or malformed file gives `complete: false` with a reason, and the other configurations still run. The step fails if any Job or summary is incomplete.

Field sources are in [Statistics bridge](statistics-bridge.md).
