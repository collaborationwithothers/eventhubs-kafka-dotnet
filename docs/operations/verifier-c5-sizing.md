# C5 hosted timing diagnostic

`verifier-c5-sizing.yml` is a manual, CPU-only diagnostic. Dispatch it on `main` only, after the workflow change merges. For the issue 494 timing margin repair, use the default source `5d70de86ca7655549bd3a8f5f8c98910385c492a`. The workflow rejects other sources and verifies the checked-out commit and each matrix row's target SHA-256.

The matrix measures the 38 physical selections whose effective configuration changes across nine targets, including each affected crossing selection. The study used the slower observed per-mutation durations from matching archived selectors. The previous full campaign exceeded 50 minutes and lost one receipt when a hosted runner shut down. [Terminal evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7115998357) · [Repair decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7123218757)

Each shard has a separate standard `ubuntu-24.04` job. `local-shard` uses the committed selector and required isolation for that physical shard. Its inner deadline is 2,650 seconds, the outer deadline including the termination grace is 2,700 seconds, and the job limit is 50 minutes. The diagnostic has no aggregate acceptance step.

Each job uploads its diagnostic directory, including raw reports, command logs, configuration, projection, and receipts, even on failure where the runner can perform the upload. Detailed test-server RPC logs upload separately with the same one-day retention. `status.txt` starts as `incomplete`; `completed_unverified` means only that the local-shard command completed its own checks. Inspect the receipt, logs and source and runner metadata before interpreting timings. A missing artifact or receipt is incomplete evidence, not a zero or a pass. These measurements do not qualify a shard or replace the complete, main-only C5 acceptance workflow.

Artifacts expire after one day. Download and preserve the evidence needed for review before expiry. The workflow has read-only contents permission, does not use an environment, secrets or Azure, and does not persist checkout credentials.

On 2026-11-15, [GitHub's billing documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions) stated that standard hosted runners in public repositories are free. That establishes the compute rule, not an unconditional exemption for artifact storage. This diagnostic does not use a larger runner.

## Earlier observed-tail timing repair

The previous pin `0130ccc4cea9f3c41ba0b681b27aeefdbb06d5cd` measured 31 physical selections across PhaseCandidateCodec, SingleActionConfigurationJudgement, VerifierResultCodec, B10FinalRepeatEvidence and B10SweepEvidenceCodec. The diagnostic completed all 31 jobs and supplied receipts for the [qualified product change](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/590). [Diagnostic run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45117421809)

## Earlier timeout recovery

The [previous workflow](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/89e75f39a2a281afc8942f4af280776e40e38520/.github/workflows/verifier-c5-sizing.yml) used source `4275b634f14c1f81fcfe91dfcd7deb260a0c8a7f`. The matrix measures 18 changed selections across the result codec, B10 final judge and B10 final projection codec. It includes the crossing selection for each target. The recovery keeps 25 successful receipts from the previous diagnostic only where the physical configurations and timing inputs match. [Previous diagnostic](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44995993132) and [recovery plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7076174614).

## Earlier sweep timing campaign

The [earlier workflow](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/08c708aae6a8cc27c87110013540089bb5553a43/.github/workflows/verifier-c5-sizing.yml) measured the two outstanding sweep shards, `b10-final-sweep-handler-p2` and `b10-sweep`, on separate `ubuntu-24.04` runners. It used the [2,700-second diagnostic limit and 50-minute job limit](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909923442). Four completed source-bound results from [run 43816213193](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43816213193) were retained. The earlier attempts under the 1,800-second limit remained incomplete evidence.
