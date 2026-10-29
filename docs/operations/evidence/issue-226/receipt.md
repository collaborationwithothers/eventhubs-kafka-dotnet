# Error origin implementation receipt

This pull request implements the approved error-origin plan for issue #226. Code -1 becomes a broker error in both load-generator captures, matching the seed recorder.

## Execution card

- Deliverable: changes 1 through 9 of the [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/226#issuecomment-6506146682), plus this authored receipt.
- Authorization: issue #226, its [scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/226#issuecomment-6506097848), the [approval and recognized later decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/226#issuecomment-6507916885), and the [Codex assignment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/226#issuecomment-6507937405). Approval identifies Revision 2 updated at `2026-10-29T19:20:06Z`, accepts plan-check P2-3 for C12 and adopts P2-1 and P2-2's exact command replacements. The issue has `ready-for-agent`; the assignment follows the approval's earlier Claude Code assignment. The user's direct instruction to this Codex session is "Run the implement-plan skill for issue 226."
- Source: `995bf5111a2ab71f98d83ab7b43863268eaf6d2e` on `origin/main`. This includes the prerequisite statistics-cap fix. Branch: `codex/issue-226-error-origin`.
- Mandatory gates: plan Acceptance with the adopted replacements below; documentation warning dispositions; cold repo-review under AGENTS.md "Before opening a pull request" rule 8; hosted Codex review and final-head CI. Protected trace and result paths require Hari's merge. Apply `verdict` before requesting hosted review; the H03 verdict stays `untestable`.
- Exact verification commands: Acceptance 1 and 2, `dotnet test tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -c Release --filter "FullyQualifiedName~SingleActionCaptureTests|FullyQualifiedName~ProbeV2CaptureTests"`; Acceptance 3, `dotnet test tests/Parchwell.Enrichment.Tests/Parchwell.Enrichment.Tests.csproj -c Release`; Acceptance 4, `./scripts/verify-dotnet.sh`; Acceptance 5, `git grep -n "IsBrokerError" -- src`; Acceptance 6, `grep -rhoE '"error_code": ?-?[0-9]+' results | sort -u`; Acceptance 7, `npm run check:docs`.
- Adopted P2-1 replacement: `5. git grep -n "IsBrokerError" -- src prints nothing and exits 1.`
- Adopted P2-2 replacement: `4. ./scripts/verify-dotnet.sh. "Locked restore" and "Solution build" must print "passed" (warnings are errors). If the script stops at a test suite, every failed test in that suite must need Docker, a local Kafka broker or a Blob emulator; then run dotnet test -c Release for each later suite in tests/Parchwell.LoadGen.Tests, tests/Parchwell.Scenarios.Tests and tests/Parchwell.Verifier.Tests and record the same condition for each. The CI .NET job on the final head is the binding run.`
- Earliest publication: after the required initial acceptance evidence and applicable cold-review coverage. Publish then; CI and hosted review may run together. No Azure run is required or authorized by this plan.
- Review budget: AGENTS.md rule 8 allows two comprehensive passes and two focused correction rounds cumulatively. Freeze each input and wait for its complete report before editing. Exhaustion or unsafe focus saves work on the branch's `-wip` backup and hands off to Hari with findings, revisions, cumulative counts and proposed next action.

## Specification comparison and predicate map

All nine numbered plan changes are implemented: the two classifier expressions, unchanged reference seed expression, seed/single-action theory rows, probe boundary and construction tests, canonical contract rule and reference, and the two exact public-file corrections. Exact-old-text counts were scripted before edits and each was one. The two historical URLs were generated and checked by the plan's script. No verifier, expected outcome, wire record, project, package, chart, workflow or infrastructure file changes.

No verifier code is edited. The affected origin input and existing C12 predicates have this map before cold review:

| Predicate or case | Tests and evidence |
|---|---|
| Below -1 is local | Existing single-action rows -196 and -187; new probe row -195; existing seed row -192. |
| At -1 is broker | New `Unknown` row in each of `SingleActionCaptureTests`, `ProbeV2CaptureTests` and `SeedF04Tests`. |
| Above -1 is broker | Existing single-action and seed rows 29; new probe row 29. |
| Construction takes precedence | Existing `Given_ConstructionFailure_When_Capturing_Then_OnlyKafkaExceptionHasMeasuredErrorTuple` and seed `Given_native_construction_failure_When_executed_Then_actual_error_has_construction_origin`; new probe fact at -1. |
| Empty input | Inapplicable to the enum classifier. Existing verifier `Given_MissingIncompleteUnequalOrConstruction_When_ItemJudged_Then_ExactDisposition` covers absent candidates, incomplete evidence, unequal outcomes and construction precedence. |
| C12 first-configuration broker/local distinctions | Existing `Given_C12FirstConfigurationError_When_Judged_Then_LiteralOriginSensitiveConclusions` in `SingleActionJudgementBoundaryAuditTests` covers broker/error, local/error and local/success cases, keeping other candidates complete. |
| C12 second-configuration broker error, success and local error | Existing `Given_AllDeterministicConfigurations_When_ItemJudged_Then_LiteralMatrix` in `SingleActionSeedAndItemTests` covers confirmed, contradicted and untestable respectively. |

C12's effect is expressly accepted by [the approval, P2-3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/226#issuecomment-6507916885): code -1 counts as broker-origin input to the existing consumer-group name-length decision rules. No verifier hardening or new test outside the plan is added.

Code 0 is not an error (`Error.IsError` in pinned v2.16.0 `Error.cs`). Success paths pass no error; delivery paths check `IsError`. The plan-check correctly calls the assumption that `KafkaException` never carries `NoError` an inference; this change does not add a guard. The schema-two verifier already rejects code 0. This distinguishes the measured boundary tests from the unreachable-case inference.

## Claims and engineering checks

Read official Confluent.Kafka v2.16.0 [Error.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Error.cs), lines 195 to 210, and [ErrorCode.cs](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/ErrorCode.cs), lines 349 to 352. Confirmed: `IsError` excludes zero, `IsLocalError` means below -1, `IsBrokerError` means above zero, and `Unknown = -1` is named "Unknown broker error". The repository pins package 2.16.0 in `Directory.Packages.props`.

Read both historical recorder files at `39e2cbba73f28855d3d0bbc3511d977294b8ba09`: the trace's line 109 and line 71 descriptions match. `git log -1 origin/main -- docs/traces/H03.md` confirmed the prior trace commit `fa6237c618e6561dffb6550d2f616c9fe80f0d75`.

Checked the implementation against the blueprint ADRs, particularly ADR-014 (expected outcomes remain assessment-only), ADR-016 (local component evidence), ADR-018 (Codex assignment and review), ADR-019 (capture versus authoritative judgement) and ADR-020 (workflow-owned infrastructure), the .NET engineering rules and the merge rule. The change only classifies captured errors and adds assertions. It changes no setting, dependency, infrastructure action or judgement implementation.

## Verification

All tests use SDK 10.0.402 installed outside the repository from Microsoft's official `dotnet-install.sh` on `builds.dotnet.microsoft.com`. Commands run at source `995bf5111a2ab71f98d83ab7b43863268eaf6d2e` with this pull request's working-tree changes. The initial red run contains only test changes 4 to 6; the green and full runs contain the complete approved implementation. Program output remains outside the repository.

| Acceptance | Measured result |
|---|---|
| 1, focused tests before source fixes | 141 total, 139 passed, exactly two failed: the `Unknown, -1, broker` theory rows named by the plan. Both reported actual origin `local`; no other failures or skips. |
| 2, same focused command after source fixes | 141 passed, zero failed or skipped. The construction-precedence fact passed in both runs. |
| 3, complete Enrichment project | 56 passed, zero failed or skipped. |
| 4, `./scripts/verify-dotnet.sh` | Exit 0. Locked restore, warnings-as-errors Release build, XML inventory and all reports passed. Architecture 12, Enrichment 56, Kafka 405, LoadGen 648, Scenarios 239 and Verifier 4252 passed: 5612 total, zero failed, skipped or errors, counted from XML reports by script. Warning control built the valid counterpart and rejected the intended CS1030 diagnostic. No environment exception or fallback was needed for P2-2. |
| 5, adopted tracked-source command | No output; exit 1, as required by P2-1. |
| 6, published error-code scan | Exactly five distinct values: -169, -192, 24, 29 and 43; no -1. Confirmed with a scripted set assertion. |
| 7, documentation scan | Exit 0; eight advisory warnings, all in unchanged files. Changed files have no warnings. |

The documentation warnings in unchanged files are outside the approved scope and remain with one blanket disposition. `git diff --check` passed. A scripted scope comparison found exactly the approved changed files plus this receipt, and confirmed that the H03 trace and report change only their specified lines. No test assertion was loosened, deleted or skipped.

## Cold review input

Apply the repository-owned `.agents/skills/repo-review/SKILL.md` to these inputs. Mode: comprehensive round 1. Input revision: frozen staged snapshot `226-cold-1`, containing all authored changes against comparison/source `995bf5111a2ab71f98d83ab7b43863268eaf6d2e`. The external diff file identifies this snapshot; no edits are made while its reviewer runs. Cumulative review counts including this invocation: one comprehensive, zero focused. The complete report and dispositions live in the pull request's collapsed self-review record, so the report is not added to its own reviewed input.
