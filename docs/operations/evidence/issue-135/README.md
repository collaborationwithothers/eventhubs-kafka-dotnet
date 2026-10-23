# Runner failure diagnostics validation

The checked [Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/135#issuecomment-6363791696) adds a safe failure line and opt-in unredacted diagnostics. Local validation passes for source commit `4dc018e`. This evidence uses synthetic test inputs and includes no live Azure run.

The [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/135#issuecomment-6363857282) reported no blocking findings. The [debug-output decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/135#issuecomment-6363721031) authorizes the diagnostic exposure; the workflows remain protected for Hari's merge.

## Local acceptance

SDK 10.0.402 was installed at `/tmp/runner135-dotnet` in this fresh workspace. Every command below used that directory first on PATH and a timeout of at most 600 seconds, as the plan requires. Exact commands, source commits, sizes and SHA-256 values for the raw files are in [raw-manifest.json](raw-manifest.json).

| Check | Result and evidence |
|---|---|
| Locked solution restore | Passed at unchanged dependency baseline `6aa9935`; [restore log](raw/restore.log) records SDK 10.0.402. |
| Release solution build with `--no-restore -warnaserror` | Passed at `4dc018e`, zero warnings and errors; [build log](raw/build.log). |
| Runner tests with `--no-build --minimum-expected-tests 1` and xUnit XML reporting | All 1104 passed, none skipped; [test log](raw/runner-tests.log), [xUnit report](raw/runner-results.xml). |
| Diagnostics and workflow classes | The xUnit report records 58 passing `FailureDiagnosticsTests`, 2 passing `WorkflowAuthenticationContractTests` and 5 passing `WorkflowBudgetContractTests`. |
| Workflow copies | Both planned `timeout 60 cmp` commands exited 0. The final workflow-budget test also compares installed/copy bytes and exact forwarding blocks, counts and timeouts. |
| Trigger and concurrency preservation | Comparing each workflow's bytes before `jobs:` against baseline `6aa9935` found no changes. |
| Documentation scan | Completed successfully; [scan log](raw/check-docs.log). The 36 warnings are confined to unchanged upstream Markdown snapshots in issue-114 raw evidence. They remain because rewriting archived source evidence would misrepresent that evidence. |

The diagnostics tests assert exact safe lines, whole-message admission, nested and aggregate traversal, exact-true enablement, suppression after disabling debug, owned Data fields and the 49/50/51-line boundaries. A bounded real Runner child verifies stderr and exit behavior without replacing global console streams. Fake command boundaries verify attachment without invoking Azure or Terraform.

The first [build](raw/build-initial.log) found analyzer errors in new test declarations. They were corrected without changing assertion values, and the [next build](raw/build-before-contract-fix.log) passed. The first [test run](raw/runner-tests-initial.log) passed 1103 tests and failed the new workflow fact because its YAML selector also matched shell parameter expansion; [its report](raw/runner-results-initial.xml) retains that failure. Restricting the selector to a YAML key fixed it, followed by the final successful build and test run above. No existing assertion was removed, skipped or loosened.

The [initial documentation scan](raw/check-docs-initial.log) also ran before the raw manifest existed and reported its missing link. Creating the manifest fixes that new warning; the final scan leaves only the archived-source warnings explained above.

## Review and deployment boundary

The pull request's collapsed self-review record records the cold review and documentation scan. Full required .NET verification remains the existing pull request workflow at the head Hari merges; local Runner tests do not replace it. No workflow was dispatched, and no Azure operation, image publication or Terraform apply/destroy was performed.
