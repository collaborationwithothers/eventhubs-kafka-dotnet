# Probe image readback repair

The authorized smoke stopped after publishing its worker images because the verifier digest lookup failed. This repair lets the V2 smoke recover from one failed registry lookup without republishing images or repeating client work. A valid immutable digest remains required before any infrastructure or client work starts.

## Execution card

- Task brief: Hari asked this session to fix the failed implementation after the unsuccessful smoke and cost complaint. This is a newly directed maintenance repair, not an extension of the earlier binding plan's one-PR approval. The originating issue is [probe batch 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240); the [direct repair authorization](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6510539920) records his instruction separately from the earlier plan.
- Source: `af2e56a538372ab91a789f7bac2fef2a427de040`.
- Evidence: [failed batch job](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40967041587/job/123774053261) and [diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6510343515). Both images were uploaded; the verifier lookup failed at the existing command deadline. The cause inside Azure CLI remains unproven. The caller also discards the failed command's status.
- Deliverable: a bounded V2-only registry-read recovery and actionable failure diagnostics in `infra/scripts/phase/phase.py`, targeted regressions in its existing test module, and the policy in `docs/operations/probe-v2-smoke-contract.md`. No workflow, identity, network, expected-outcome, verifier or results change.
- Policy choice: a V2 registry digest read exiting with status 1 or 124 gets at most two attempts, each retaining the existing 60-second command bound. Exhaustion fails the phase and retains its final command status in the message. Other command statuses and a successful command returning an invalid or mismatched digest fail immediately. Publication/login failures are not retried. Existing scenarios keep their single read. These are execution-policy bounds, not measured Azure timings.
- Mandatory gates: a red reproduction through the real publishing entry point before changing runtime code; focused recovery/exhaustion/digest tests; existing phase and command-runner suites; documentation check and warning disposition; cold review; final-head CI and hosted Codex review. Merge follows the current protected-path rules.
- Exact commands: `python3 -m unittest infra.scripts.phase.tests.test_phase.PhaseTests.test_v2_image_readback_timeout_recovers_without_republishing`; focused test names will be listed below; `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `python3 -m unittest infra.scripts.tests.test_command`; `npm run check:docs`; `git diff --check`.
- Work bound: the fixed files above, one local subprocess reproduction, and a fixed regression matrix. No Azure dispatch, retry of the smoke, approval change, broad .NET rerun or discretionary delegation. Required fresh-context cold review remains necessary.
- Earliest publication: the first locally verified, cold-reviewed revision. New PR budget: at most two comprehensive and two focused rounds under AGENTS.md rule 8; exhaustion saves unpublished work on `-wip` and hands off to Hari. Prior merged PR review counts do not describe this different repair.

## Verification preparation

This changes workflow transport recovery, not a verifier predicate. The regression map covers initial success, failed first read followed by a valid read, repeated failure and exhaustion, invalid successful output, immutable digest mismatch, publication failure, and unchanged legacy behavior. A real local subprocess fixture must exercise `publish-images`, `Phase.run` and the command runner, with synthetic executables and a deliberately stalled first verifier lookup. It must assert the final image map and exact publish/read counts.

## Results

The real subprocess reproduction failed before the runtime change with `AssertionError: 0 != 1`. It published both synthetic images, then stalled the first verifier read through the actual `Phase.run` and command runner. The fixture shortens only its synthetic lookup deadline to one second; production retains 60 seconds. No Azure or real registry is contacted. The focused matrix also failed before the change in the recovery and missing-status cases; the invalid-digest controls already rejected their inputs.

After the change:

- The same subprocess reproduction passed. It checks two total publish commands, identical repeated verifier read arguments, the final expected digest, absence of an incomplete marker, and final image outputs. The timed-out command's valid-looking partial digest cannot satisfy the final result.
- The four focused tests passed, covering the subprocess replay plus exit 1/124 recovery, bounded exhaustion and immediate failures. Their explicit status rows include output-limit, missing executable and cancellation failures; these do not receive another read.
- `PATH=/tmp/issue-240-tools:$PATH PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` passed all 110 tests, including the existing legacy and immutable-digest controls.
- `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest infra.scripts.tests.test_command` passed all five command-runner tests. No local .NET rerun is needed for this Python-only runtime change; the standard final-head CI remains mandatory.
- `npm run check:docs` completed with eight advisory warnings in unchanged files; neither changed Markdown file has a warning. `git diff --check` passed.

| Contract case | Test |
|---|---|
| Initial success and unchanged legacy command arguments | Existing `test_publish_images_argv` |
| Real stalled first verifier process followed by success | `test_v2_image_readback_timeout_recovers_without_republishing` |
| Failed read exit 1 or 124 followed by valid digest; same reference, no republish | `test_v2_image_readback_failure_retries_only_the_read` |
| Persistent exit 1/124; no final image map or output publication | `test_image_readback_exhaustion_preserves_status_and_never_publishes_partial_map` |
| Exit 125/127/130/143; no retry; A01 and legacy batch keep one attempt | Same exhaustion matrix |
| Empty or malformed successful output; publication failure | `test_v2_invalid_digest_or_publication_failure_does_not_retry` |
| Valid-looking but mismatched immutable digest | Existing `test_use_images`; equality remains after the retry loop |

This is recovery from a failed read, not a diagnosis of the internal Azure CLI stall. Persistent registry failures still fail. A new live smoke remains unauthorized and unperformed. Complete outputs stay outside the repository. The final cold report and final-head CI belong in the PR description; they are not appended to their own reviewed receipt.


## Frozen review input

Comprehensive pass one compares the local immutable tag `issue-240-image-readback-review-1` with `af2e56a538372ab91a789f7bac2fef2a427de040`. The tag will be created on the commit containing this receipt before review. The reviewer receives the complete authored PR diff and this receipt only, then reads the cited sources under the repository-owned `repo-review` skill. No completed comprehensive or focused round precedes this input. Complete reports and cumulative counts belong in the PR self-review record. No correction is made until every started review reports completely.
