# B04 item implementation receipt

The B04 shared-workflow implementation is saved for continuation. Python and pinned offline checks passed, but the Release build exhausted its approved rerun allowance with verifier-test compilation errors. No pull request, cold review, live B04 measurement or verdict was produced.

The source commit is `1eabb09ae166e078d8b9c793dbc3172411ba4b27`, tree `358f2863c0497e9933c46cd720569278f728de39`, based on `main` at `b68ba0bc62e15d361fa8a3fc2502e2aac87cb638`. The validation below ran on the working tree subsequently saved unchanged in that source commit. This receipt describes that source commit, rather than its own later receipt commit.

Authority is [implementation-grade plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411873981), updated `2026-10-26T08:45:07Z`, and its [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6412023660), updated `2026-10-26T08:53:31Z`. The check has no blocking findings and the plan has no open questions. Both marked documentation corrections are implemented. Child decisions adopt the [shared workflow](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411483242) and the [single boolean-test migration](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411671441).

The predecessor [S1 smoke run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40291069526) passed both smoke jobs, both ephemeral cleanup paths and the final cluster stop. The earlier [C5 run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40279869275), on `00845be9bbff97df9566dd88e214f606ff6e12dd`, has a summary with `measurement_complete: true` and `break_passed: true`. Those results do not replace S2's own required post-merge C5 run or B04 measurement.

## Validation

| Check | Command | Observed result |
| --- | --- | --- |
| Locked restore | `dotnet restore Parchwell.slnx --locked-mode` | Exit 0 with SDK 10.0.402. A separate earlier restore warmed the base dependency cache and is not claimed as implementation verification. |
| Python lifecycle and workflow contracts | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | First run exited 0; 81 tests passed. |
| Pinned offline checks | `python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh` | First run exited 0. ShellCheck, actionlint, workflow mirrors, 25 positive and 32 negative chart cases, 4 worker-setting regressions and both strict smoke-schema renders passed. |
| Release build | `dotnet build Parchwell.slnx --configuration Release --no-restore` | Three runs, all nonzero. The first found duplicate resource identity and enum naming issues. The second found a missing import and nullable-flow issues. After corrections, the third compiled the production projects and failed compiling the verifier tests with 2 CS0121 errors and 6 CA1861 errors. No passing build is claimed. |
| Scope and literal data | `/tmp/207-implementation/tools/venv/bin/python /tmp/issue-207-authority/scope.py` | Exit 0 before the receipt was added: 64 changed source paths within the 65-path allowance, including this receipt's reserved path. Both B04 YAML files match the approved literals. Both retired workflow files are absent and the shared workflow mirrors match. |
| Whitespace | `git diff --check` | Passed on each implementation section and before the source commit. |

Counts were computed from the source and command results. The chart matrix has 25 positive cases, including 7 LoadGen environments. The five existing HTTP call-count assertions changed from 4 to 3, 6 to 5, 3 to 3, 23 to 22 and 3 to 2 for the completion-boundary, paginated-old-rerun, pagination-cap, completed-attempt-cap and unknown-status cases respectively. The scope script and command logs are session-local files; no program output is committed here.

The full `./scripts/verify-dotnet.sh`, Kafka coverage, worker-image smoke, rendered LoadGen host check, documentation scan and cold review have not run on this implementation. Required final-head CI and hosted Codex reviews do not exist because no pull request was opened.

## Setup and implementation details

The existing installer verifies downloaded artifacts against its pinned SHA-256 values internally; its output reports tool versions and installation success. `bash infra/scripts/lifecycle/install-tools.sh /tmp/207-implementation/tools ci` exited 0. The official installer placed SDK 10.0.402 at `/tmp/issue-207-dotnet`; `dotnet tool install dotnet-coverage --version 18.12.0 --tool-path /tmp/issue-207-coverage` and `npm ci --ignore-scripts` also exited 0. The managed local Docker daemon reported version 28.4.0. Setup and acceptance logs are under `/tmp/issue-207-authority/`.

The approved `Integer` enum member has a narrowly scoped CA1720 naming suppression. The scenario test project embeds the shared physical profile under the existing A01 and new B04 logical resource names; the B04 item uses an absolute MSBuild item identity to avoid the SDK's duplicate-item rejection. Its evaluated `FullPath` still names the unchanged shared profile. No physical copy, package, project or assembly reference was added.

The only moved existing assertion is `request.timeout.ms: true`. Its replacement resolver test retains that input and asserts `setting_out_of_range` and its original source location. The other loader rejection cases stay intact, and new tests cover accepted boolean overrides. The future pull request must name the migration and link the child exception, as that decision requires.

Implementation used GPT-6 Luna at high effort. Planning and the independent plan check used fresh Codex agents at high effort with the inherited model, whose identifier was not exposed to the authoring session. No cold reviewer or hosted Codex reviewer ran in this session.

## Continuation

The plan's allowance of two build reruns after the first run is exhausted. Under AGENTS.md rule 11 and the plan's exhaustion action, this session saves a backup and hands off without another repair or acceptance run.

The remaining measured compilation errors are confined to new verifier tests:

- In `tests/Parchwell.Verifier.Tests/B04/B04EvidenceRecorderTests.cs`, the two `Failure(new(...))` calls are ambiguous between a single `StructuralProblem` and a list. The continuation can make the intended single problem type explicit.
- The same file has CA1861 errors for constant expected arrays at source lines 56, 73 and 107. `tests/Parchwell.Verifier.Tests/Judge/PhaseCandidateCodecTests.cs` has the same error at source lines 28, 59 and 64. Move those expected constants to suitable static readonly fields while preserving the assertions.

A continuation refreshes child, parent and linked-decision authority, restores the backup as the ordinary branch, and uses Luna high for implementation fixes. It then runs the approved build, full .NET verification, coverage, image smoke, rendered-host check and remaining documentation/scope checks; obtains the required fresh cold review; and carries the planned pull request to protected merge handoff. The ordinary push deletes the WIP backup under rule 12. No ordinary push should substitute incomplete validation for the approved commands.

One known documentation mismatch remains outside the plan's permitted paths: `docs/engineering/dotnet-verification.md` still describes the old `scenario_files:2` smoke output. Preserve scope and disposition it under the review rules rather than silently editing that protected file. There is no follow-up issue for it yet.

Estimate dated 2026-10-26: 30-60 minutes of continuation work, excluding hosted CI and review queues, with low confidence. The basis is the measured 8 test-compilation errors and the still-required full validation and cold-review steps. Hari retains the protected merge; the adopted post-merge mutation and measurement requirements remain in force.

## Continuation validation (2026-10-26)

The continuation restored WIP commit `bbc40e2`; the validated source is commit `3e16265de4465a336f53665b1be3b43b14c6e0ee`. Setup paths were recreated successfully, with setup logs under `/tmp/issue207/`. Subsequent source changes were test-only. This section records the continuation and does not revise the historical results above.

| Check | Observed result |
| --- | --- |
| Locked restore and Release build | Locked restore passed. Three resumed Release builds passed with zero warnings and zero errors. |
| Kafka configuration mapping coverage | Passed: 132 tests and 374 instrumented mapping lines. Coverage output is under `/tmp/parchwell-kafka-coverage.7RNQbRNz`. |
| Worker image smoke | Both worker images passed with `scenario_files:3`. Output is under `/tmp/parchwell-worker-images.JJic7aTm`. |
| Python lifecycle and workflow contracts | The final rerun passed all 82 tests; log: `/tmp/issue207/python-2.log`. |
| Pinned offline checks | All passed: 25 positive chart cases, 32 negative chart cases, 4 worker-setting regressions, and strict smoke-schema checks. |
| Rendered LoadGen host | All 7 environments and the missing-setting regression passed. |
| Documentation scan | Reported 3 warnings, all in unchanged files. |
| Scope | `/tmp/issue207/scope.py` passed its 65-path allowance check. |
| Independent Kafka suite | 360 of 360 passed. |
| Independent LoadGen suite | 371 of 371 passed. |
| Independent Scenarios suite | First run passed 109 of 117 and failed because the B04 fixture named a missing profile. After correcting the fixture to use the profile supplied by its in-memory source, all 117 passed; final log: `/tmp/issue207/scenarios-final.log`. |
| Independent Verifier suite | Final run passed all 2746 tests with exit 0 and no skips; logs: `/tmp/issue207/verifier-final.log` and `/tmp/issue207/suites/Verifier-final/results.xml`. The earlier 2742-of-2746 run exposed a new invalid-pair assertion checking the writer instead of the read boundary; that assertion was corrected. |
| Full .NET verification | The architecture suite remains 10 of 11. `ChartContractTests.cs` line 18 requires `scenario_id.const` to be `A01`, while approved F1 permits `A01` and `B04`. The architecture path is outside the approved plan scope; replacing its assertion also needs an explicit exception to the .NET testing policy. |

The source commit contains the B04 test fixture indentation and profile-path fixes, the corrected codec assertion through the read boundary, and restored workflow assertions for the old forbidden text, `FULL_C0_RESULT`, the bracketed `needs` expression, and H2's exact report `WORKLOAD_STEPS`. Existing A01/B04 workflow checks remain. The three warnings from the documentation scan are in unchanged files and receive the blanket disposition. The pre-existing `scenario_files:2` mismatch in `docs/engineering/dotnet-verification.md` remains outside scope; it is deferred, with no follow-up issue yet.

Cold-review round 1 is frozen in `/tmp/issue207/review.diff` and is complete. It reported six P0/P1 findings: the `expected.yaml` P0 is retained because approved plan section C2 requires the change and Hari merges; the `ChartContractTests.cs` P1 awaits the explicit plan and assertion-policy exception below; the fixture-indentation P1, missing-profile P1, invalid writer-boundary P1 and removed workflow-assertions P1 are fixed. No second round or final clean review exists.

The architecture change requires a decision under AGENTS.md rule 11 because it is outside the approved plan paths and replaces an existing assertion contrary to the testing policy. The exact proposed amendment is:

> Proposed amendment to #207 implementation-grade plan Revision 1:
>
> Add tests/Parchwell.ArchitectureTests/DeploymentContracts/ChartContractTests.cs to the permitted paths. In its chart-schema test, rename Given_ChartSchema_When_Read_Then_OnlyA01AndClosedRuntimeAreAccepted to Given_ChartSchema_When_Read_Then_OnlyA01AndB04AndClosedRuntimeAreAccepted. Replace only the scenario_id const assertion with Assert.Collection over the scenario_id enum array, asserting first string A01 and second string B04 exactly. Preserve every remaining assertion, including additionalProperties, closed runtime, required endpoint inputs and absence of service_tags.
>
> Authorize this one assertion replacement as an exception to docs/engineering/dotnet.md Testing and AGENTS.md Code Review Rules, because the old A01-only assertion conflicts with approved F1. Acceptance remains the full ./scripts/verify-dotnet.sh with no skips and all other approved commands. Enforced checks, baseline, verdict rules and live execution stay as approved. Hari merges.

No pull request, final-head CI, hosted Codex review, security review or Azure dispatch exists. Three resumed Release builds passed; full verification remains blocked by the unapproved architecture amendment. Estimate dated 2026-10-26: 15-30 minutes of next-session work, excluding hosted queues, with low confidence; the basis is the pending architecture amendment, remaining full acceptance and fresh cold-review round. The next session runs the remaining acceptance and obtains a fresh cold review after the amendment is resolved. Hari retains the protected merge and post-merge mutation requirements.
