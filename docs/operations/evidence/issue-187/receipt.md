# Hosted C5 mutation tooling receipt

This change restores the approved shared-helper repair, commits coverage off for the four C5 targets, and adds a manual hosted measurement workflow. Local validation covers the repair and workflow tooling. No complete mutation result, hosted artifact or survivor disposition is claimed.

## Authority and tested source

The implementation follows [Revision 5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6398272547), updated 2026-10-25T17:55:56Z, its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6401162461), and [Hari's landing decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400987342). The [mutation record](../../verifier-mutation.md#issue-187-hosted-c5-measurement) owns the current publication policy and accepted scope limitation.

Validation uses main commit `6f55b96f3dcf14b81fcb11f750af4d513c2ae322` plus this PR's uncommitted authored changes. The five helper files come verbatim from saved commit `9a0fbd5f4f5924b7d46095120e58b0e6d5f4aca6`. The historical controller, summarizer and Python-test changes on that saved branch are omitted. Production verifier files, test-project exclusions, full-scope configuration, SDK and Stryker pins remain unchanged.

The historical [diagnostic and helper proof](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400229379), [retained-output diagnosis](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400381734), and [short coverage-off measurement](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400700903) retain their original input identities and consumed allowances. Their raw evidence remains local and is not independently downloadable. This PR starts no Stryker campaign, dispatches no mutation workflow and waits on or polls no mutation run.

## Validation commands

The pinned main tree was exported with `git archive` to `/tmp/issue-187-r5-main`. In that export, using SDK 10.0.402:

```bash
dotnet restore Parchwell.slnx --locked-mode
dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-restore
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --list-tests
```

In the implementation checkout:

```bash
dotnet restore Parchwell.slnx --locked-mode
ParchwellVerifierMutationOnly=true dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-restore
dotnet build Parchwell.slnx --configuration Release --no-restore -t:Rebuild
dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --list-tests
python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py'
./scripts/verify-dotnet.sh
python3 /tmp/issue-187-r5-structure.py
actionlint .github/workflows/verifier-c5-mutation.yml
git diff --check
npm run check:docs
```

The mutation-only property was absent from ordinary commands. The explicit ordinary rebuild replaced the mutation-only assembly before discovery. Each build/verification command has an 1800-second bound; the export, structural pass and lint have 600-second bounds. The existing pinned offline-tool installer ran once, successfully, in CI mode under a 600-second bound, without Azure login. It supplied actionlint 1.7.12, shellcheck 0.11.0 and PyYAML 6.0.3. Existing npm dependencies needed no setup.

## Observed results and limitations

All three pinned-main commands and the implementation restore, mutation-only build, ordinary rebuild, ordinary discovery and full verification exited 0 on their first execution. The explicit builds recorded zero warnings and zero errors. Full verification passed 4534 tests: Architecture 8, Kafka 349, LoadGen 355, Runner 1163, Scenarios 96 and Verifier 2563, with no failed or skipped test records. Enrichment remains an empty scaffold; no test coverage is claimed for it. Counts were computed from retained XML under `/tmp/parchwell-dotnet-verification.a15m1Dii`.

The synthetic workflow command passed 22 tests in 1.053 seconds on its first execution. Fixtures cover completed exits 0/2, error/failed-launch/timeout cleanup, raw merged output, source/config drift, missing or malformed shards, scope/status/identity failures, exact 6/10, 5/10 and 7/10 combined scoring, and the stable 50-survivor sample. The log is `/tmp/issue-187-r5-synthetic-1.log`; these are synthetic tooling observations only.

Actionlint 1.7.12 and `git diff --check` each exited 0 without findings on their first execution. The corrected structural pass confirmed all five saved helper blobs, all 70 production verifier files unchanged, unchanged test project/exclusions and SDK/tool/full-scope configuration, the exact coverage-off configuration, and the prescribed workflow structure. Ordinary discovery measured 2563 tests in each tree, difference 0.

Raw command logs and generated evidence remain outside git under `/tmp/issue-187-r5-validation`; this authored record does not commit their output.

The first structural pass exposed an error in the plan's discovery parser. It matched only `Given_` names: 2399 rows against a final total of 2563. The retained baseline has 2563 qualified test rows, including 164 existing names such as `GivenSecondRegisteredProducerOnLastSdkListingPage_WhenListing_ThenRetainsBothMarkers` without the underscore after `Given`. This is a parser limitation, not a missing-test finding. The [independent check supplement](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6401162461) specified the exact `Given` selector as a P2 `apply in implementation`, preserving every equality assertion and enforced check. The second structural pass passed with that sole correction. Both passes and the failed result remain retained; the two-pass allowance is consumed, and discovery was not rerun.

Synthetic child/report fixtures exercise workflow execution and aggregation, not production mutation effectiveness. A complete hosted measurement on the required final verifier commit remains future evidence. Workflow artifacts become downloadable only after that dispatch, with the configured retention period.

## Review

The first revision-5 `npm run check:docs` scan exited 0 and reported no issues in either changed Markdown file. Warnings in unchanged files receive the blanket disposition in the PR description under AGENTS.md rule 8. This consumed one of four revision-5 docs executions; no warning in a changed file required a disposition.

The final PR description records its head CI run, cold-review rounds and dispositions. The test/workflow paths are protected and Hari merges. No generated output, additional package, production verifier change or assertion change is part of this PR.

## CI integration follow-up

[Run 40220401445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40220401445) passed its .NET and Terraform jobs at `2d83fa36466e953be532e19f1f5a271e09b744b1`. Its Lifecycle verification job failed with exit 127 because `charts/parchwell/tests/check-loadgen-host.sh` was absent. PR #193 had added that check to the base workflow after this branch's pinned baseline, while the workflow checks out the PR head explicitly. Rerunning that unchanged head would still omit the script.

The fix merges existing main `403900f0ae476cbfeca4cdecbba3001e38422306` into the PR branch, bringing the already merged workflow, chart, LoadGen code and its check together. It changes none of this PR's C5 implementation or the original pinned comparison baseline. The original verification results above retain their original source identity; they are not promoted to a full verification of the merged source.

On `2d83fa3` plus the staged merge of `403900f`, the existing failing CI command was exercised once with its existing 900-second bound:

```bash
python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh
```

Using the existing pinned SDK and tools, it exited 0 and reported six rendered environments and one missing-setting regression passed. Its log is local at `/tmp/issue-194-ci-fix/loadgen-host.log`, outside git. No structural comparison, discovery, diff check, tool installation or mutation campaign was rerun; their earlier allowances remain consumed. New-head CI and the latest cold-review result are recorded in the PR description.
