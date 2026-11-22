# Pace cross-run C5 artifact downloads and retry throttled requests

Revision R1. Main base `717c5a75e3f0a56ad5378a0e7551caa832ed63d8`.

## Goal and authority

The final C5 pair completed all 324 physical jobs, while the coordinator's cross-run download began 146 artifact requests and received GitHub's HTTP 403 secondary rate limit with `Retry-After: 1`. All 146 exact archives existed in GitHub. The official aggregate contains 178 coordinator physical receipts and zero companion physical receipts. Implement paced companion downloads with bounded backoff, then dispatch one replacement pair after the protected workflow merge. [Owner decision and measured evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7078519785) · [Independent plan for existing qualification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7076211777) · [Paired acceptance decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-7001549347)

## Scope

One PR, about seven files and 430 to 600 changed lines. The allowed files are `.github/workflows/verifier-c5-mutation.yml`, `docs/operations/verifier-mutation.md` and `tests/Parchwell.Verifier.Tests/Mutation/{c5_companion_artifacts.py,test_c5_companion_artifacts.py,c5_runtime.py,test_c5_runtime.py,test_c5_workflow.py}`. The current main has no versioned copy of the coordinator workflow under `docs/operations/workflows/`. The companion workflow, all worker and Stryker commands, trigger, runner, tests executed by mutation jobs, all 324 selectors, qualification ledger, schema, static cohorts, application and scientific verdicts stay outside the change.

## External contracts and invariants

The coordinator still validates both exact same-main runs and supplies its schema 1 campaign manifest. The new companion step invokes `c5_companion_artifacts.py --repository OWNER/REPO --commit SHA --manifest CAMPAIGN.json --output-dir DIR` with the job's existing Actions-read token as `GH_TOKEN`. It writes verified contents under `DIR/c5-mutation-RUN_ID-ATTEMPT-SHARD`. Coordinator evidence downloads and the aggregate's always-run behavior stay in place. The paired run keeps 178 coordinator and 146 companion physical jobs, the full native population, the 60% score threshold and the elapsed gate at 3,000 seconds including artifact transfer and aggregation. Both workflows dispatch on exact `main`.

## Requirements

1. WHEN cataloguing companion evidence, THEN validate the current repository, commit, run and attempt against the campaign manifest and companion matrix. Follow at most ten 100-row artifact pages with a consistent count. Select exactly the expected physical artifact names and unique positive IDs for the pinned attempt. Expired, foreign, missing, duplicate, inconsistent or malformed evidence fails closed. Select no Stryker log archives.
2. WHEN fetching the catalogue or a ZIP, THEN make one request at a time, with at least 0.2 seconds between API request starts. Authenticate only the fixed GitHub API request. Follow HTTPS redirects without forwarding its authorization. Make at most four attempts per request, and enforce a 240-second transport deadline before requests and body reads. Use a maximum 15-second per-request operation timeout. Retry transient network errors, HTTP 408, 429, 5xx and confirmed 403 rate limits. Follow `Retry-After` and exhausted primary-limit reset when available. For a rate-limit response with no usable timing header wait at least 60 seconds on the first retry and increase the delay exponentially. Other transient errors use increasing 1-, 2- and 4-second delays unless a later provider header applies. Ordinary permission failures stop immediately. Exhaustion stops further requests and retains the previously verified inputs.
3. WHEN receiving a ZIP, THEN match published size and SHA-256, validate unique safe relative paths, reject symlinks and require `receipt.json`. Publish the extracted directory under the exact name only after verification, with existing verified directories preserved on failure. The aggregate still checks each physical receipt and emits incomplete evidence if any are missing. Record safe error classes and artifact names while excluding tokens and signed URLs.
4. WHEN qualifying the changed coordinator workflow, THEN normalize only the exact new companion transfer block to the previously measured block in the timing fingerprint. Preserve the earlier reviewed action transition and reject unrelated aggregate/worker, source and owner changes. Existing timing records, assessments, receipts, target source and physical matrix stay as recorded. Update the former companion action fixture for the new transport and preserve its exact attempt/output binding and the separate-log and partial-evidence assertions.
5. WHEN the protected workflow change merges with final-head CI and code/security reviews complete, THEN verify the merged tree, worker inputs and qualification ledger before one exact-main paired dispatch. Verify both terminal runs, official archived evidence and the existing terminal checker. Close #494 only with complete official evidence, score at least 60% and elapsed time at most 3,000 seconds. This replacement attempt measures the existing layout. The unchanged PhaseCandidateCodec p3 exceeded the time gate in the prior attempt and can vary again. [Slow shard evidence](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45012520104/job/137177750226)

## Test map

| Requirement | Check and literal expected result |
| --- | --- |
| 1 | With an exact separately paged attempt, the result contains the expected names only. Zero, missing, stale, unexpected or duplicate records reject the catalogue. Compare the already saved 146-name GitHub catalogue against the exact companion manifest and complete physical ownership. |
| 2 | An observed-style 403 with `Retry-After: 1` waits at least 1 second before retrying. Repeated failures use increasing delays and stop after four requests or at the deadline. A secondary failure without a usable header initially waits at least 60 seconds. A primary response with reset at epoch 150 at epoch 100 waits 50 seconds. Authenticated first request and unauthenticated HTTPS redirect preserve the credential boundary. A plain 403 stops after one request. |
| 3 | A correct ZIP appears only under its exact name. Changed size or digest, missing receipt or unsafe/duplicate paths fail without publishing a directory. Give the aggregate its existing partial inputs after a download step fails. Pending archive files never substitute for verified directories. |
| 4 | The normalized fingerprint of the committed before-and-after workflows matches. Changing the helper reference, manifest flags, unrelated aggregate parameters or worker command changes the hash. Source comparison verifies both role matrices and the complete worker input maps on the repaired draft. |
| 5 | Run required hosted CI and code/security reviews. Both terminal runs, the complete score-bearing summary and the unchanged terminal checker must pass. An elapsed time of 3,001 seconds or an incomplete score report blocks closure. |

## Deployment assumptions and bounds

The reader uses the standard library and GitHub's existing Actions read scope. The prior artefact catalogue shows the provider's digest and size fields and actual pagination. The official docs document retry and reset semantics. The paced request cost and exact fresh-runner campaign duration are measured by the replacement pair. Concurrency, job counts, gate, scientific outputs and infrastructure stay fixed. Use hosted CI for covered tests, documentation and lint. [Pinned GitHub REST guidance](https://github.com/github/docs/blob/d7487f5debb5a5a2a6e65e6f9fa539266f5f8db6/content/rest/using-the-rest-api/rate-limits-for-the-rest-api.md#exceeding-the-rate-limit) · [CI-only direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/494#issuecomment-6909056894)

The companion catalogue work is bounded to one run identity check, at most ten pages and at most 146 ZIPs. Each request uses the limits above. A transport bound preserves completed evidence and exits nonzero, and the aggregate records the remaining gaps. Final hosted execution is one pair of 324 physical jobs on exact main. On exhaustion preserve outputs, push -wip and hand off to the next session with the measured failure. No automatic retry of the full pair.

## Independent plan check

1. The only runtime-sized work is the catalogue, at most 146 archives and one exact full pair. The bounds and exhaustion actions are stated above.
2. This plan adds stricter exact catalogue and integrity checks and bounded pacing, preserving the final score and wall-time gates.
3. Existing artifacts and published summaries establish the provider contract and observed throttling. Source and normal CI check offline behavior. Runtime duration on a fresh pair is deliberately unresolved and measured by the authorized rerun.

## Open questions for Hari

None.

## Stop rule

Stop and preserve evidence if a required timing or aggregate contract cannot be met within these limits. Stop for a new scope decision before changing layout, score or elapsed gates. Only the repository owner merges protected paths.
