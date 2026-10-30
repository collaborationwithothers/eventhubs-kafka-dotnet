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

### Directly authorized diagnostic extension

Before another live run, Hari asked what would explain another stall and instructed this session to add the missing instrumentation. The [later diagnostic decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6515792898) extends this same PR; it does not authorize dispatch. The extension starts from `e6924e35623caaf6bc99058afc81efcfea635a9e`.

- Deliverable: fixed-name, flushed stderr stage timing in `scripts/azure-cli-session/az` for registry digest lookups, regression tests in `scripts/tests/test_azure_cli_session.py`, and the existing smoke contract and this receipt. Stage markers distinguish lock wait, authentication/token request/login/account selection, forwarded command and child cleanup. They contain process correlation, measured elapsed time and controlled outcomes, never arguments, identifiers, credentials, response bodies or exception messages.
- Invariants: preserve existing deadlines, lock ownership, authentication, command output, cancellation, child cleanup and retry policy. No CLI debug output, workflow change or Azure execution. The script paths are protected, so Hari merges the finished PR.
- Verification: first run new diagnostics regressions red against the unchanged wrapper; then `PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s scripts/tests -p test_azure_cli_session.py -v`, the phase and command-runner suites above, `npm run check:docs` and `git diff --check`. Use synthetic issuers/commands only. Include failure, timeout, cancellation, no credential leakage and flushed real-subprocess evidence.
- Scope and review budget: the fixed files above, a fixed test matrix and one additional comprehensive cold review of the full PR, using the remaining comprehensive pass (cumulative limit two). Two focused correction rounds remain available under rule 8; exhaustion saves unpublished work on `-wip` and hands off. Hosted final-head review and CI remain mandatory. No new live smoke is authorized.

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

### Diagnostic extension verification

The new `test_registry_diagnostics_separate_auth_and_command` regression failed against the unchanged wrapper with `registry lookup emitted no stage diagnostics`. After instrumentation, the complete Azure CLI session suite passed all 28 tests, including its existing authentication, lock, deadlines and process-tree cleanup assertions. The phase suite passed all 110 tests and the command-runner suite passed all five tests again. Commands are those listed in the diagnostic execution card above. These are local synthetic tests, not Azure evidence.

The added tests cover successful refresh followed by a lookup, held-lock timeout, failures during token request/login/account selection, fresh-session command failure and cancellation, and credential-bearing synthetic exception messages that must not reach diagnostics. Real subprocess tests read a flushed command-start marker while the wrapper is still alive, then exercise both its deadline and external SIGTERM, observing cleanup markers and the preserved exit status. A successful real synthetic CLI also preserves digest stdout and existing stderr. Other commands retain their existing diagnostic behavior. No old assertion was weakened or removed.

The permanent operator markers use fixed labels and numeric timing/status fields, not temporary debug dumps. They distinguish wrapper stages but cannot identify an HTTP or DNS sub-operation inside the CLI. No new Azure run or direct registry call was performed. Documentation and whitespace checks precede the frozen review; final-head hosted checks and the completed second cold report are recorded in the PR description.

The completed second comprehensive review found that a failed cleanup-start log write could skip cleanup. Before correcting it, the new sink-failure regressions failed with write/flush exceptions and a replaced lookup exit status. Diagnostic writes now suppress log-sink `OSError` and `ValueError` failures. The fixed test matrix preserves the original timeout/cancellation exception, calls cleanup once, and preserves a nonzero forwarded-command status when marker output fails. The complete session suite then passed all 30 tests, and the command-runner suite passed all five tests again. No old assertion was weakened or removed. Missing markers remain possible when the log sink fails; the smoke contract states that limit.

## Frozen review input

Comprehensive pass one compares the local immutable tag `issue-240-image-readback-review-1` with `af2e56a538372ab91a789f7bac2fef2a427de040`. The tag will be created on the commit containing this receipt before review. The reviewer receives the complete authored PR diff and this receipt only, then reads the cited sources under the repository-owned `repo-review` skill. No completed comprehensive or focused round precedes this input. Complete reports and cumulative counts belong in the PR self-review record. No correction is made until every started review reports completely.

Comprehensive pass two compares immutable tag `issue-240-image-readback-review-2` with the same main source `af2e56a538372ab91a789f7bac2fef2a427de040`. This is a full-PR review because the new direct instruction adds wrapper instrumentation and protected script paths. The tag is created on the commit containing this extension before review. Cumulative use before this input is comprehensive one, focused zero; this pass uses the remaining comprehensive allowance. Its reviewer receives only the complete authored PR diff and this receipt, and reads the linked decisions independently. The existing [first complete report](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/252#issuecomment-6510655418) retains its earlier scope. Any later corrections require applicable remaining focused coverage under rule 8, or the budget handoff described above.

Focused correction pass one compares immutable tag `issue-240-image-readback-focused-1` with `3bbbccb3d801ca860d20603b4824b34d7c6799bf`. It reviews the log-sink correction, its regressions and documentation, and the affected stage/cleanup/caller context. The full authored PR diff against `af2e56a538372ab91a789f7bac2fef2a427de040` is context. Cumulative use before this input is comprehensive two, focused zero; no comprehensive pass remains. The unresolved finding at the comparison revision is the cleanup-start write failure described above. Its complete report arrived before corrections began. The focused tag is created on the commit containing the correction before review. A further necessary correction uses the remaining focused allowance or the stated budget handoff; a new session does not reset it.
