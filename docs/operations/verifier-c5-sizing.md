# C5 hosted timing diagnostic

`verifier-c5-sizing.yml` is a manual, CPU-only diagnostic. Dispatch it on `main` only, after Hari merges the workflow. The source input is a full lowercase commit SHA; its default is the feature commit containing the diagnostic helpers. The workflow checks out that source while retaining the actual `main` workflow context and verifies both target file hashes. Each of the six named physical shards runs on a separate `ubuntu-24.04` runner with a 1,800-second local-shard limit. There is no retry or aggregate acceptance step.

Each job uploads its complete diagnostic directory, including raw output and logs, even on failure where the runner can perform the upload. `status.txt` starts as `incomplete`; `completed_unverified` means only that the local-shard command completed its own checks. Inspect the receipt, logs and source and runner metadata before interpreting timings. A missing artifact or receipt is incomplete evidence, not a zero or a pass. These measurements do not qualify a shard or replace the complete, main-only C5 acceptance workflow.

Artifacts expire after one day. Download and preserve the evidence needed for review before expiry. The workflow has read-only contents permission, does not use an environment, secrets or Azure, and does not persist checkout credentials.

On 2026-11-15, [GitHub's billing documentation](https://docs.github.com/en/billing/concepts/product-billing/github-actions) stated that standard hosted runners in public repositories are free. That establishes the compute rule, not an unconditional exemption for artifact storage. This diagnostic does not use a larger runner.
