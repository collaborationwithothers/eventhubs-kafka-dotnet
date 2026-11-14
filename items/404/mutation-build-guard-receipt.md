# Mutation-only build fix and guard receipt

This change makes the C5 mutation-only build of the Verifier test project compile again and makes pull request verification build it, so the same break cannot pass unnoticed. It deletes and loosens no test, changes no csproj, workflow, trigger, scenario, result or expected outcome, and dispatches nothing. Hari merges it.

## Execution card

- Deliverable: both parts of [Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6873340821) (no item plan; directly authorized small fix). Part 1 fixes the mutation-only build. Part 2 adds the guard.
- Authorization: that decision. Implementing tool: Claude Code. Branch `claude/404-mutation-build-guard`, from `main` at `5ab9e4a2`.
- Mandatory gates, in the order of docs/agents/pull-requests.md "Verification gates": focused tests, one comprehensive cold review and at most one focused round, then `./scripts/test-verify-dotnet.sh` (the regression check for `verify-dotnet.sh`) and `./scripts/verify-dotnet.sh --tier sandbox` once on the cleared source, then the hosted `.NET verification` on the final head and hosted Codex code and security reviews.
- Earliest authorized publication checkpoint: after the cold review has no open P0 or P1 and the pre-PR local gate has passed, publish the tested pull request ready for review, then request hosted Codex code and security review. No merge and no dispatch by this session.
- Review budget: one comprehensive pass and at most one focused round. When another required review would exceed it, or focus is unsafe, save the work under rule 12 and hand off to Hari (docs/agents/pull-requests.md rule 8).
- Fix caps: 20 verification-failure and 20 review-finding root causes, exhaustion action `push -wip and hand off to the next session`.
- Predicate map: not applicable. No verifier predicate or test body changed. The change moves two helper classes between folders and adds one build stage.

## Reproduction on main

On `main` at `5ab9e4a2`, `dotnet build tests/Parchwell.Verifier.Tests -c Release -p:ParchwellVerifierMutationOnly=true` failed with these two errors and no others:

```
tests/Parchwell.Verifier.Tests/Host/VerifierKafkaTokenProviderHostTests.cs(8,32): error CS0234: The type or namespace name 'Component' does not exist in the namespace 'Parchwell.Verifier.Tests'
tests/Parchwell.Verifier.Tests/Judge/B10FinalJudgeTests.cs(8,32): error CS0234: The type or namespace name 'Component' does not exist in the namespace 'Parchwell.Verifier.Tests'
```

## Part 1: choice and reason

I removed the dependency instead of adding the two files to the `Compile Remove` list at `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj` line 21.

- Both files import `Parchwell.Verifier.Tests.Component` only to reach `B10AbruptVerifyEvidence` (a partial class in `Component/B10AbruptVerifyEvidence.cs` and `Component/B10AbruptVerifyFixtureBuilder.cs`). Nothing else from that namespace is used: both files built after the import was removed.
- `Judge/B10FinalJudgeTests.cs` drives the real `JudgeHandler` and so exercises `Judge/PhaseJudge.cs`, `Judge/B10JudgeChecks.cs` and `Common/Phase/B10FinalProjection*.cs`, all of which are in `mutate` in `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`. Removing the file from the mutation build would take its tests out of the run that kills those mutants, and could report survivors that the full test suite kills. A wrong published mutation score costs more than a larger diff.
- Excluding the host file would have been harmless for mutation, but moving the evidence class fixes both files by one change, in one place, and needs no protected project file change.

The two evidence classes moved from `Component/` to `Support/`, which the mutation-only build keeps, with the namespace changed to `Parchwell.Verifier.Tests.Support`. The ten `Component` files that use the class gained `using Parchwell.Verifier.Tests.Support;`. The two files from the decision lost their `Component` import. The Component folder is still excluded from the mutation-only build; the class it no longer holds is not needed there.

Results on the tree of `c44467c`, whose C# and build content `a28ddef` leaves unchanged: the mutation-only and normal builds both succeed with 0 warnings and 0 errors. In the partial run `--tier fast --project Parchwell.Verifier.Tests --shard rest`, the Verifier tests report 8018 passed both with unchanged main's tests (the guard run below) and with the fix. That run leaves out the two `b10-verify` shards and the Docker, Slow and CGroupV2 tests, so it shows no loss only in that slice. The change edits no test body: the C# differences are `using` and `namespace` lines and two file moves.

## Part 2: guard

`scripts/verify-dotnet.sh`, function `mutation_only_build` and its call before the warning control, adds the stage `Mutation-only Verifier test build`. It builds the Verifier test project with `-p:ParchwellVerifierMutationOnly=true` and `-p:RestoreLockedMode=true` into its own `--artifacts-path` directory under the results directory. The separate directory matters: sharing `bin` and `obj` would let this build replace the assemblies that the test stages run with `--no-build`.

It runs when the Verifier test project is selected and the run has no shard or the shard `rest`. The hosted workflow runs `--project Parchwell.Verifier.Tests --shard rest` once, so each pull request builds it once; the other two Verifier shards skip it. `scripts/test-verify-dotnet.sh` has new cases M1 to M4 for the stage: one build in a full run and in the Verifier `rest` leg, none in the named Verifier shards or in another project's run, and a failed build fails the run after all tests with the stage label in `failed_stages`. A failure is recorded like a test project failure, the run continues, and the exit status is non-zero. `docs/engineering/dotnet-verification.md` states the stage. No workflow file changed, so no trigger changed and hard safety rule 8's committed-copy rule has nothing to update.

Evidence, with `./scripts/verify-dotnet.sh --tier fast --project Parchwell.Verifier.Tests --shard rest` (partial runs, not the full gate):

- Unchanged main tests with the new script: `Mutation-only Verifier test build: failed (exit 1)`, both CS0234 errors above, the run exits 1, and the listed failed stage is that one only.
- With the fix: the stage passes in 8 s and the run exits 0.

## Other checks

- `./scripts/test-verify-dotnet.sh`: passed, with the new cases, on the tree of `a28ddef` (the script content is as committed there).
- `python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py`: 47 tests, OK.
- `bash -n scripts/verify-dotnet.sh`: no syntax error.
- `npm run check:docs`: 9 warnings, all in files this change does not touch (docs/agents/factory.md, docs/operations/evidence/issue-124, issue-140 and issue-211, and items/404/pr2-receipt.md). The change's own files have none.

## Review record

The commit named as the head of each round is the one whose tree the reviewer read. Reports are in the pull request's self-review record.

- Comprehensive pass 1: mode comprehensive, input revision `c44467c` (the staged tree on `main` at `5ab9e4a2`, byte-identical to that commit's diff), no comparison revision, specification source [the decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6873340821). Result: 2 P1 and 4 P2, all fixed in `a28ddef`.
- Focused round 1: mode focused, input revision `a28ddef`, comparison revision `c44467c`, the same specification source. Result: 1 P1 and 3 P2.
- Cumulative: 1 comprehensive pass and 1 focused round, the budget of the decision. The open P1 was this record. [Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6873578055) ended the cold review under rule 8 and had the session fix the receipt without another round, then go to Codex review of the final head.
- Dispositions of the other three focused-round P2s: the results-source P2 is fixed in this receipt. The two script P2s (the comment at `scripts/verify-dotnet.sh` line 468 says the test stages "build", and test case M3 does not check `$status`) are left unfixed because that decision limits the remaining work to the receipt; Codex review of the final head covers them.
