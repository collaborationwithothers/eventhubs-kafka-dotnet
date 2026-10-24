# Runner initial PodList verification

The initial producer list now uses the raw Kubernetes API so its server resourceVersion reaches the first watch. This local evidence verifies the transport repair for [issue #142](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/142); it does not establish a successful live AKS run.

The [standing-approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/142#issuecomment-6373803756) and [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/142#issuecomment-6373839864) define the scope. Production and test code was verified at `be001c82604cf22c93e6eae66794259b148f5787` with the pinned .NET SDK 10.0.402.

## Validation

`PATH="/tmp/runner-142-dotnet:$PATH" timeout 600s ./scripts/verify-dotnet.sh` exited successfully. The [complete log](raw/verification.log) records locked restore, the Release solution build, architecture and selected project tests, report validation, Kafka documentation validation and the warning-as-error controls. The [build log](raw/build.log) records zero warnings and errors.

Test counts below were computed from the machine-readable reports; all 4147 selected tests passed with no failures or skips. The existing Enrichment test project is an empty scaffold, as the verification log states.

| Test assembly | Passed | Raw report |
|---|---:|---|
| Parchwell.ArchitectureTests | 8 | [XML](raw/Parchwell.ArchitectureTests.xml) |
| Parchwell.Kafka.Tests | 306 | [XML](raw/Parchwell.Kafka.Tests.xml) |
| Parchwell.LoadGen.Tests | 309 | [XML](raw/Parchwell.LoadGen.Tests.xml) |
| Parchwell.Runner.Tests | 1134 | [XML](raw/Parchwell.Runner.Tests.xml) |
| Parchwell.Scenarios.Tests | 96 | [XML](raw/Parchwell.Scenarios.Tests.xml) |
| Parchwell.Verifier.Tests | 2294 | [XML](raw/Parchwell.Verifier.Tests.xml) |

The Runner report includes synthetic tests for the exact encoded raw list request, opaque version continuity, the `missing_resourceVersion` rejection for a generic List with an empty version, and initial-list process failure/timeout. Existing admission, watch, budget and failure-preservation assertions remain intact.

`timeout 600s npm run check:docs` completed; [its log](raw/docs.log) retains the advisory warnings. The unchanged upstream snapshots under `../issue-114/raw/review-10-fixes/` have 36 warnings: `long.md` has 9, `tolong-function.md` has 13, and `union-operator.md` has 14, counted from the scan output. They concern links into the upstream documentation tree and upstream punctuation; changing retained source snapshots is outside this repair.

The [raw manifest](raw-manifest.json) records each file's producer, source commit, byte size and SHA-256. All test workloads are synthetic. No live workflow was dispatched.
