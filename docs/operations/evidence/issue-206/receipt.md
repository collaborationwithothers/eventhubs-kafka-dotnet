# B04 path smoke implementation receipt

This receipt records the implementation and local checks for the B04 path smoke. The checks below ran on the staged implementation working tree based on commit `858c912c510a41d35b9f33dc53f9c6f721909533`, before the cold-review corrections described below; no live dispatch or verdict is included.

Authority: [Revision 1 implementation-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/206#issuecomment-6410165759), updated `2026-10-26T06:59:28Z`, and [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/206#issuecomment-6410248839), updated `2026-10-26T07:04:43Z`. The check states `Blocking findings: none` and the plan states `Open questions for Hari: None`. The plan-check reviewer identified that the pinned installer verifies artifact hashes internally but prints versions and installation success, not hashes; this receipt records the observed output accurately.

The workflow and mirror add dispatch-only C0/C1 path smoke using the existing smoke Job and lifecycle. The phase bridge accepts A01 probe/C0/C3 by default and B04 C0/C1 when selected; the sweeper includes B04 history in its existing cleanup lookup. No B04 worker measurement, expected outcome, verdict, result, or live behavior is claimed.

## Validation

| Check | Command | Result |
| --- | --- | --- |
| Python lifecycle and workflow contracts | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Before the cold-review correction, a run exited 0 with 88 tests passed. There were 3 total runs: the first exposed test-fixture mistakes and failed; the next two passed. |
| Pinned offline checks | `python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh` | Before the cold-review correction, exit 0; ShellCheck, workflow mirrors, actionlint, chart checks (21 positive and 32 negative cases, plus 4 worker-setting regressions), and two strict kubeconform smoke renders passed. |
| Locked .NET restore | `dotnet restore Parchwell.slnx --locked-mode` | Exit 0 using SDK 10.0.402. |
| Release build | `dotnet build Parchwell.slnx --configuration Release --no-restore` | Exit 0; 0 warnings and 0 errors. |
| Full .NET verification | `./scripts/verify-dotnet.sh` | Exit 0. Architecture 11, Kafka 349, LoadGen 359, Scenarios 91, and Verifier 2633 tests passed. Enrichment remains an empty scaffold with no unit-test coverage claimed. The synthetic Kafka spike cleanup succeeded. Logs and reports: `/tmp/parchwell-dotnet-verification.SA6KnDwZ`. |
| Documentation scan | `npm run check:docs` | Exit 0. No warning was reported in an authored path. Three advisory warnings remain in unchanged historical files: one in `docs/operations/evidence/issue-124/README.md` and two in `docs/operations/evidence/issue-140/README.md`. |

The five sweeper request-count assertions are preserved or adjusted as planned. The 3599-second completion case now makes 4 requests; the old-created rerun with paginated jobs makes 6; unknown run status makes 3; the 20-completed-attempt cap makes 23; and the run-pagination cap remains 3 because scenario pagination exhausts the lookup before the third workflow is queried. The B04 response is included in every case's response map where that workflow lookup is reachable.

The existing installer verifies downloaded artifacts against its pinned SHA-256 values internally; its output reports tool versions and installation success. The pinned offline tools were installed outside the repository with `bash infra/scripts/lifecycle/install-tools.sh /tmp/206-implementation/tools ci`; it exited 0 and reported Terraform 1.16.6, Helm 4.3.1, actionlint 1.7.12, ShellCheck 0.11.0, kubeconform 0.8.0, TFLint 0.65.0, PyYAML 6.0.3, and successful installation. The .NET 10.0.402 SDK was installed outside the repository at `/tmp/issue-206-dotnet` using the official `dotnet-install.sh` source; `bash /tmp/206-dotnet-install.sh --version 10.0.402 --install-dir /tmp/issue-206-dotnet --no-path` completed successfully and `/tmp/issue-206-dotnet/dotnet --version` reported `10.0.402`. `npm ci` installed the locked documentation dependencies; npm reported an existing deprecated `glob@10.5.0` package.

The cold reviewer completed round 1 and reported two P1 findings. One found that the incorrectly quoted `WORKLOAD` in both B04 workflow copies parses as `"'true'"`, which `Phase.report()` rejects. The other found a non-ASCII apostrophe in the PR description draft. Those findings were corrected in commit `71148c9`: the workflow uses `WORKLOAD: 'true'`, the workflow contract expects parsed string `true`, and the PR draft uses ASCII punctuation. During this continuation, offline validation found that `WORKLOAD` was still indented two spaces too far in the two B04 report environment mappings. The current working tree corrects that indentation in the workflow and its mirror. Cold-review round 3 re-reviewed the corrected implementation and reported `Findings: none at P0 or P1`, with no reportable P2.

## Continuation validation

The continuation source commit is `71148c95d2a7cdc2cf7760f11fd2b0eb60fc7346`, with the two indentation corrections above applied in the working tree. These results describe that corrected working tree:

| Check | Command | Result |
| --- | --- | --- |
| Python lifecycle and workflow contracts | `PATH=/tmp/206-implementation/tools:$PATH python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Exit 0; 88 tests passed. |
| Pinned offline checks | `PARCHWELL_TOOLS_DIRECTORY=/tmp/206-implementation/tools python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh` | Exit 0; ShellCheck, workflow mirrors, actionlint, chart checks (21 positive, 32 negative, and 4 worker-setting regressions), and two strict kubeconform smoke renders passed. |
| Documentation scan | `npm run check:docs` | Exit 0. Three advisory warnings remain in unchanged historical files: one in `docs/operations/evidence/issue-124/README.md` and two in `docs/operations/evidence/issue-140/README.md`; no warning was reported in an authored path. |
| Whitespace and scope | `git diff --check`; approved plan Python scope guard against staged diff and `origin/main...HEAD` | Exit 0; whitespace is clean and the guard reports `scope: passed` for exactly the nine approved paths. |

`npm ci` was run to restore the locked documentation dependencies; it exited 0 and reported the existing deprecated `glob@10.5.0` package. The pinned tool installation command was `bash infra/scripts/lifecycle/install-tools.sh /tmp/206-implementation/tools ci`; it exited 0 and reported pinned versions and installation success. The installer verifies downloaded artifacts against pinned SHA-256 values internally; its output reports tool versions and installation success.

The first continuation Python discovery attempt failed because the pinned tool directory was absent from `PATH`; the second failed because the `WORKLOAD` indentation defect made the workflow invalid YAML. The successful run was the third attempt (the first run plus two reruns, the plan cap). The offline check first lacked `PARCHWELL_TOOLS_DIRECTORY`, then reported the same YAML indentation defect; its successful run followed tool setup and the indentation correction. The documentation scan first found `remark` unavailable, then passed after `npm ci`. No further acceptance rerun was performed after the successful capped runs. The earlier .NET evidence remains applicable because no .NET source changed. The continuation has made no live dispatch and claims no B04 measurement or verdict.

The implementation session was GPT-6 Luna at high effort. Planning, independent plan check, and cold-review rounds 1 and 2 were SOL 6.1 at high effort. Round 1 reported two P1 findings, corrected and re-reviewed in round 2. Round 2 found no implementation findings and one P1 in the PR draft: it omitted the blanket disposition for unchanged historical documentation warnings. The exact blanket line is now in the PR draft. Round 3 was performed by GPT-6 Astra at high effort and reported `Findings: none at P0 or P1`, with no reportable P2. Codex PR review is pending.

The offline checks and source review establish the local contract only. The path smoke has not been dispatched, and Hari merges the protected workflow path and authorizes any live dispatch under the child scope.
