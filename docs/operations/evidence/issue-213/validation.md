# Probe batch S2 validation

The six-item single-action batch is implemented in the existing workers and shared scenario workflow. Local verification checks capture, judgement and orchestration; it does not establish Event Hubs outcomes. Protected merge, hosted mutation acceptance and an authorized live batch remain separate gates.

## Authority and source

- [S2 plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/213#issuecomment-6425997798), updated 2026-10-26T20:00:27Z, and [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/213#issuecomment-6426097434), with no blocking findings.
- Both `apply in implementation` dispositions are applied: ten required legacy declaration fields plus `single_action`, and the exact ordinary F04 C1/C3 keys while only C0 uses the seed key.
- Final executable source: `2204a0fb7e16ed1e6d145f64cddae8d1ab21551e`. This receipt may be committed afterward; it does not claim to validate its own containing commit. The final head CI and cold-review round belong in the PR description.
- Baseline: `0cd9b9ddf7d6cdef468db95bf00ad00ad67cdc43`. Parent/predecessor and later decision recognition are recorded by the plan and check. Authority was refreshed after implementation; issue bodies and the recognized decisions were unchanged.

Commands run from the repository root on Linux with SDK 10.0.402, dotnet-coverage 18.12.0, Node 24.19.0 and the repository's pinned offline tools. `PATH` included `/tmp/parchwell-dotnet`, `/tmp/parchwell-tools`, `/tmp/parchwell-lifecycle-tools` and its `venv/bin`; `DOTNET_ROOT=/tmp/parchwell-dotnet` and `PARCHWELL_TOOLS_DIRECTORY=/tmp/parchwell-lifecycle-tools`. The final .NET run disabled MSBuild node reuse with `MSBUILDDISABLENODEREUSE=1` and `DOTNET_CLI_USE_MSBUILD_SERVER=0` after stale worker-node failures. No check or assertion was disabled.

## Verification

`./scripts/verify-dotnet.sh` passed on the final executable source: locked restore, Release build/analyzers, setting XML inventory, architecture checks and warning-as-error control. Counts below were computed from the per-assembly XML under `/tmp/parchwell-dotnet-verification.bBzXEeRV/`; all failures and skips are zero.

| Assembly | Passed |
| --- | ---: |
| Parchwell.ArchitectureTests | 12 |
| Parchwell.Enrichment.Tests | 55 |
| Parchwell.Kafka.Tests | 402 |
| Parchwell.LoadGen.Tests | 484 |
| Parchwell.Scenarios.Tests | 149 |
| Parchwell.Verifier.Tests | 3342 |
| Total | 4444 |

| Command | Result and retained local evidence |
| --- | --- |
| `./scripts/verify-kafka-coverage.sh /tmp/parchwell-tools/dotnet-coverage` | Passed on `fa5b6e1`; 165 configuration tests and all 400 instrumented mapping lines. `/tmp/parchwell-kafka-coverage.sn45lC8z/{results.xml,coverage.xml}`. |
| `bash charts/parchwell/tests/check-loadgen-host.sh` | Passed on `fa5b6e1`: 7 legacy rendered environments and 1 missing-setting regression; 33 single-action environments and 7 missing/unknown/legacy regressions; published Enrichment native smoke. `/tmp/issue213-final-hosts.log`. |
| `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Passed, 117 tests. `/tmp/issue213-review4-infra.log`. |
| `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | Passed, 74 tests. `/tmp/issue213-review4-phase.log`. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_*.py'` | Passed, 25 tests, including the independent six-target/configuration/matrix checks. `/tmp/issue213-review4-c5.log`. |
| `bash infra/scripts/lifecycle/check-offline.sh` | Passed shell/workflow/chart checks, including 25 positive/32 negative legacy renders and 66 positive/14 negative new renders. `/tmp/issue213-review4-offline.log`. |
| `terraform fmt -check -recursive infra tests/terraform` | Passed. `/tmp/issue213-final-fmt.log`. |
| `bash infra/scripts/lifecycle/check-offline.sh --terraform-test-paths` | Passed. `/tmp/issue213-final-terraform-paths.log`. |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Passed; the copies are byte-identical. |
| Expected-file YAML walk and malformed mapping controls from the plan | Passed: `ok`; each of the six synthetic temporary replacement positions was rejected. No repository expectation was changed for a negative control. |
| `npm run check:docs` | Passed with 8 pre-existing advisory warnings in untouched files; changed contracts had no warnings. `/tmp/issue213-review4-docs.log`. |
| `git diff --check` | Passed. |

Coverage/host checks ran on `fa5b6e1`; their covered configuration and host-option code is unchanged by the subsequent verifier repairs. The Python and offline checks were repeated on `2204a0f`. Terraform and expected-file YAML controls ran on `cf8a82f`; those checked files are unchanged. Documentation checks were repeated after this receipt update. The Python command populations overlap and are not added together as a unique test total. These fixtures are synthetic.

Real native construction tests exercise all 16 resolved configurations and 6 paired rejection controls using librdkafka 2.16.0, including all codecs, B05, C08's three omissions and C12's two group lengths. They are local constructor checks, not broker observations. Token-scope tests verify the requested audience. Completion tests cover missing receipts, false cleanup and nonzero exits. Later capture overflow retains an earlier measured terminal tuple for security reporting, remains incomplete, and suppresses a successful capture completion receipt.

The first whole-solution locked restore found the architecture project's stale Enrichment dependency entry. SDK regeneration changed only that project-lock dependency entry, preserving package versions. An intermediate full run on `4be2998` had one failure in the unchanged native partition-added test, `Local: Unknown partition`; `/tmp/parchwell-dotnet-verification.LSfOaOUp/Parchwell.Verifier.Tests/results.xml` retains it. Its one unchanged isolated rerun passed (`/tmp/s2-verifier-native-partition-rerun.log`), and the final full run above passed. No retry was added to the test, and no assertion was weakened.

## Seed container and portability

The existing Enrichment project was restored in locked `linux-x64` mode using `-p:NuGetLockFilePath=packages.linux-x64.lock.json`. SDK `PublishContainer` built a local archive from the committed pinned base image with `ContainerRegistry` empty, repository `parchwell/enrichment-s2-local`, tag `issue-213-fa5b6e1` and `ContainerArchiveOutputPath=/tmp/issue213-enrichment-image.tar.gz`. Nothing was pushed to a registry.

The archive was loaded into the managed local Docker daemon. Inspection recorded image ID `sha256:ff5305ca92e1d34f0371cd44337f00510e9be8db68d607360ec826c35a07db72` and configured user `1654`. `docker run --rm --network=none parchwell/enrichment-s2-local:issue-213-fa5b6e1 --image-smoke` exited zero and reported the distinct seed-smoke marker with `native_loaded: true`. Logs are `/tmp/issue213-local-container-{restore,build,load,smoke}.log` and `/tmp/issue213-local-container-identity.txt`. This local image ID is not a deployed registry digest or a seed delivery receipt.

Linux retains the plan's bounded `/proc/sys/kernel/random/boot_id` read. The portable macOS path uses the read-only `kern.bootsessionuuid` scalar through a fixed-size native call; the [pinned Apple XNU definition](https://github.com/apple-oss-distributions/xnu/blob/f6217f891ac0bb64f3d375211650a4c1ff8ca1ea/bsd/kern/kern_sysctl.c#L2921) verifies that scalar. Linux, mocked Darwin ABI and unavailable-identity paths pass; no native macOS execution was available. Admission and orchestration budgets are unchanged.

## Cold-review dispositions

Round 1 reviewed the full diff and this authored receipt. It found two substantive P1s: null matched-record strings could abort the judge instead of producing check-7 diagnostics, and the registration pod-name maximum lacked the required neighboring tests. Both are fixed in `b078ef2`: strict field-kind readers return `p7_outcome_record`, handler fixtures prove an invalid diagnostic candidate is published, and matching registration fixtures cover empty and 252/253/254-character names. The focused 338-test suite and 3073-test noncomponent regression run passed; logs are `/tmp/s2-verifier-review-fixes-tests.log` and `/tmp/s2-verifier-review-fixes-regression.log`. The final full run above also includes these fixes.

The six expected-file P0s and four reference P1s from that review are retained solely under the approved-plan exception below, with merge reserved for Hari. There were no P2 findings in that round.

Round 2 found two further substantive P1s: rejected registration metadata could still drive dependent Blob reads, and some statistics/terminal-error boundaries lacked isolated fixtures. These are fixed in `2799ae8`: marker paths are checked before reads, rejected registrations cannot select capture/completion paths, and real fixed-path validation fixtures prove five invalid diagnostic slots are published. A bounded audit of the two judgement targets completed the remaining comparison and empty-input fixtures. Encoded-length cases preserve valid bytes and pad only ignored whitespace, so each length guard determines the outcome. The final focused suite passed 588 tests (`/tmp/s2-verifier-r2-final-focused.log`); the full run above includes those cases. No existing assertion was weakened. Round 2 had no P2 findings.

Round 3 found two further substantive P1s: deeply nested transport JSON could abort orchestration, and invalid/incomplete judgements returned zero despite ADR-017. Both are fixed in `2204a0f`. New transport reads enforce a string-aware depth bound and convert decoder recursion into diagnostic gaps; regression batches continue through every slot. The verifier writes its diagnostic candidate before returning nonzero for invalid/incomplete runs and emits no successful judgement receipt for those failures. Complete valid judgements, including nondeterministic assessments, retain zero exits and post-disposal success receipts. ADR-017 is the design authority over the lower-level plan's conflicting exit detail. Focused verifier checks passed 595 tests, noncomponent regression passed 3330 tests (`/tmp/s2-verifier-r3-{focused,regression}.log`), and the full run above includes the changes. Round 3 had no P2 findings. The next fresh-context round reviews all three disposition sets and the full diff; its result belongs in the PR's self-review record.

## Review and pending gates

The approved plan requires new protected `expected.yaml` files, verifier/workflow/project changes, existing centrally pinned Enrichment package references and its Kafka project reference. Actual resolved-construction testing also requires the Kafka test project's Scenarios reference. These are retained under AGENTS.md "Before opening a pull request" rule 8's approved-plan exception and must be listed for Hari's merge; they are not an agent merge authorization.

Current official Microsoft and pinned v2.16.0 sources are cited by the scenario expectations, override policy and [wire contract](../../single-action-contract.md). The [factory](../../../agents/factory.md#evidence-pack) owns the ledger-count exception. Authorship used Codex; the exact model identifier is unavailable. Planning, independent check and delegated implementation used high effort. The cold review applies the repository's `repo-review` rubric in a fresh context, with its result and final head CI recorded in the PR.

No Azure infrastructure command, live batch or hosted mutation campaign was run in this session. After protected merge, publication still requires a main-only six-shard C5 campaign with `measurement_complete: true` and `break_passed: true`. Actual batch spend needs Hari's authorization, and live outcomes, role enforcement and timing remain unmeasured. Local logs remain temporary; link the final head CI run from the PR when available.
