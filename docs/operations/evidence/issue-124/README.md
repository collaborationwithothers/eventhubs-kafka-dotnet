# A01 renewable CLI session evidence

The renewable CLI wrapper and workflow templates passed the offline checks recorded here. These checks use synthetic assertions and fake CLI processes; workflow installation and live Azure validation remain outstanding.

The [approved r3 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/124#issuecomment-6351953474), [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/124#issuecomment-6357751311) and [implementation approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/124#issuecomment-6357767571) define the scope. The [runbook](../../a01-runner.md#renewable-azure-cli-session) owns the authentication contract and source citations.

Implementation source commit: `5da2925425e05cc445adcb1e37f14ee6dbdc8f39`. Each raw receipt records its command, source commit, environment overrides and exit code. Restore ran on the baseline before authoring; project, package and lock files are unchanged.

| Check | Observed result | Evidence |
| --- | --- | --- |
| Locked restore | Passed | [Log](raw/restore.log), [receipt](raw/restore.json) |
| Release solution build | Passed with no warnings or errors | [Log](raw/build-1.log), [receipt](raw/build-1.json) |
| Python suite | All 20 fixed cases passed | [Log](raw/python-1.log), [receipt](raw/python-1.json) |
| Planned wildcard contract selection | Exit 5; zero tests selected | [Log](raw/contracts-1.log), [receipt](raw/contracts-1.json) |
| Explicit contract class selection | All 6 tests passed, including the Python bridge | [Log](raw/contracts-2.log), [receipt](raw/contracts-2.json) |
| Documentation advisory | See recorded output; external links are not checked | [Log](raw/docs-2.log) |
| Whitespace check | Passed | [Log](raw/whitespace-1.log), [receipt](raw/whitespace-1.json) |

The first documentation scan ran before the raw manifest was written and reported that missing link; the next scan includes the manifest. Remaining warnings concern unchanged upstream-document snapshots under `docs/operations/evidence/issue-114/raw/review-10-fixes/`, whose relative links and source punctuation are preserved as evidence.

The planned filter `Parchwell.Runner.Tests.Contracts.Workflow*ContractTests` selected no tests. The corrected invocation names `WorkflowAuthenticationContractTests` and `WorkflowBudgetContractTests` explicitly, preserving the intended test scope and timeout. The failed command is retained; no test was changed to obtain a pass.

The Python cases cover freshness boundaries, exact ACR-login forwarding, secret suppression, binary output, shared-process locking, cancellation and process-group cleanup, and the literal setup/fallback conditions. Synthetic clock advances exercise the phase duration without a live long-running job. The .NET contract guards the configured API budget and executes the Python suite through the existing bounded process runner.

[Cold-review findings and dispositions](cold-review.md) remain authored evidence. The [raw manifest](raw-manifest.json) records exact producers, source commits, byte sizes and SHA-256 hashes. Existing PR CI must supply full verification at the reviewed head; local results do not substitute for it. CLI lock contention and the actual installed tool versions remain operational uncertainties.
