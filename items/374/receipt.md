# B10 phase-row implementation receipt

This change adds B10's phase identities so fixtures can reach existing B10 code. Workflow admission and scenario configuration remain with the later scenario work.

## Execution card

- Deliverable: the six-file change and item state specified by [plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/374#issuecomment-6633582685), updated 2026-11-04T14:43:37Z.
- Authorization: the [standing-approval plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/374#issuecomment-6633900449), with no blocking findings, under the [adopted amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/374#issuecomment-6633792801). Apply its exact state-file P2 correction. The plan links the earlier adopted split and design decisions.
- Mandatory gates: plan acceptance steps 1-9, documentation checks, cold review, final-head CI, hosted Codex review and the separate Claude review. The control session owns merge under [the adopted implementation decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/374#issuecomment-6633532516).
- Verification commands: plan steps 1-8, using `/tmp/issue374-dotnet` on PATH; focused builds use `DOTNET_PROCESSOR_COUNT=2` and full verification uses `DOTNET_PROCESSOR_COUNT=1 MSBUILDDISABLENODEREUSE=1`. Focused tests include the plan's negative controls with the phase table temporarily restored to baseline. Run `npm ci` and `npm run check:docs` with the installed Node 24 runtime, which satisfies package.json's `>=24` requirement.
- Earliest publication: after the tested diff clears its applicable cold review. Hosted review and final-head CI can then run together.
- Review budget: at most two comprehensive and two focused cold-review rounds across sessions, under docs/agents/pull-requests.md rule 8. Exhaustion or unsafe focus means save to the branch's `-wip` backup and hand off to Hari. Other runtime caps and exhaustion actions remain those in the plan's question (a).

## Review inputs

Mode: consolidated comprehensive re-review. Comparison revision: `2a92098067b4a93ba478c41a9b43a9570a0ab097`. Input revision: the staged diff named `374-cold-review-2.diff` against that comparison revision, frozen after the verification evidence below was recorded. One comprehensive round has completed; no focused round has run. The pull request self-review record will retain its complete report. That round reviewed diff SHA-256 `a305e8defd4e45488db8e23d372f46bdc7baef7d839de83308c85ad8c507418c` and found one P1: this receipt incorrectly said the plan's Node 22 note predated the Node 24 requirement. The chronology claim is removed. The other receipt provenance statements were checked against the linked authority and recorded logs; no similar claim remained. No code changed after verification.

## Predicate coverage

No verifier implementation changes. The shared phase table is covered by the plan's positive `B10/C0/C0` and `B10/C1/C1` cases, rejection of `probe` and `C3`, and ordinal scenario-case rejection. Existing null cases remain. The load-generator tests exercise both permitted Job names and reject `loadgen3` at the Job-name boundary and `C3` at the phase boundary. Verifier-specific B10 fixtures belong to the later intake-reader child.

## Verification evidence

Source baseline: `2a92098067b4a93ba478c41a9b43a9570a0ab097`. The plan's baseline-difference guard exited 0. The extracted edit script matched SHA-256 `a2f83e41b5187acc16d8e8107a16769b3a748ed458ef03b560f46f3cd2dc439d`; its nine replacements produced the six changed implementation files with combined SHA-256 `54130c073c9232c3f60e7f2f91d643bb0122067dac5092ffce953f8c32e2409e`, 43 additions and 6 deletions, all measured by script before state creation.

- Focused ScenarioPhasesTests: 24 passed, 0 failed. With the table restored to baseline: 2 failed, 22 passed, exactly the two B10 positive cases.
- Focused RestartJobIdentityTests: 11 passed, 0 failed. With the table restored to baseline: 5 failed, 6 passed, exactly the planned B10 cases other than the invalid C3 phase.
- The negative controls restored the changed table in a finally block before full verification.
- Whole-project Release execution: Scenario tests 336 passed; LoadGen tests 1169 passed; Kafka tests 486 passed; architecture tests 15 passed; enrichment tests 56 passed; verifier tests 5005 passed. The script validated each project's report. The service-dependent failures seen by the planning session did not occur here. `PATH=/tmp/issue374-dotnet:$PATH DOTNET_PROCESSOR_COUNT=1 MSBUILDDISABLENODEREUSE=1 ./scripts/verify-dotnet.sh` passed, including locked restore, the solution build, XML inventory consistency, effective project-setting checks and the warning-as-error negative control.
- Initial baseline and focused builds encountered MSB4166 child-worker exits. A single-worker build with `--disable-build-servers -m:1` succeeded with 0 warnings and 0 errors; focused execution then used `--no-build`. Full verification uses `DOTNET_PROCESSOR_COUNT=1 MSBUILDDISABLENODEREUSE=1` to avoid worker reuse. The baseline full-suite attempt was stopped during Kafka tests after its build, XML inventory, architecture and enrichment checks passed; it is not claimed as a passing baseline suite.
- Initial `npm run check:docs`: exit 0, eight warnings in unchanged files. No warning from the three changed contracts. The final scan, including the staged receipt and state, exited 0 with the same eight warnings in unchanged files.

The implementation checksum was independently recomputed after the negative controls and matched the plan. `git diff --cached --check` passed, and a scripted ASCII scan found no non-ASCII characters in the eight authored files.

All measurements above were made on 2026-11-04. Tests establish local harness behavior only; no live run or verdict was produced.
