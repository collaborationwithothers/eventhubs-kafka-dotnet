# C5 measured span rebalance receipt

This receipt records the second implementation pull request for the mutation runtime item. It preserves the investigation as historical evidence and records no measured optimized speedup.

## Execution card

- Deliverable: the seven authored paths and exact allocation in the [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703130075).
- Authorization: [Revision 2 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703427658) after its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703401722), retaining [Hari's narrow approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703287109), adopting both exact corrections in the [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703193224). Bookkeeping expectations and legacy fixtures may change; judging assertions and gates may not.
- Baseline: main `8e2ab2fbb1ca7fe1a02396af4a07ac9626c13c73`. The [investigation receipt](receipt.md) belongs to merged PR 398 and is not this PR's review record.
- Mandatory evidence: full helper unittest discovery, explicit retained-inventory suite, documentation check with dispositions, actionlint, whitespace and exact changed-path review. Coverage off, break 60, static isolation, every judging test, native identity reconciliation and acceptance remain required. No dispatch or diagnostic mutation run is authorized.
- Exact verification commands: the six commands in the plan's Acceptance and publication section, from repository root. Their outcomes are recorded below after execution.
- Publication checkpoint: after those commands and applicable cold review pass, publish the tested PR. Hosted review and CI may run together. Hari merges the workflow change after the required code/security/Claude reviews; this session never merges it.
- Review budget: at most two comprehensive and two focused cold rounds for this PR under docs/agents/pull-requests.md rule 8. On exhaustion or unsafe focus, push this branch with `-wip` appended and hand off to Hari. Documentation warning and diagnostic correction caps/exhaustion actions are exactly the approved plan's, not a new allowance.

## Verification and predicate coverage

The two mandatory Python commands passed on the corrected helper/test source recorded in commit `cee0113e745c25e779cd11ca742c4122de51e455`. They ran immediately before that local source commit, with identical helper/test bytes. No .NET verifier predicate is changed. No existing test was skipped or deleted; the only changed existing assertion expressions are the explicitly approved production shard-contract expectations. The committed-span test retains its original assertions and adds layout checks.

## Report-inclusion finding and approved correction

During implementation, the source read behind the runbook's report-inventory claim exposed a second Stryker behavior beyond per-mutant filtering. At pinned commit `6e77a3451bac4793e9c839c3ff4055c1b30ca3af`, [ProjectComponentsExtensions.IsComponentExcluded](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponentsExtensions.cs) flattens all matching negative patterns, reduces their intervals together and removes that union from the included intervals. [JsonReport.GenerateFileReportComponents](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/JsonReport.cs) then emits [SourceFile.Ignored](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/SourceFiles/SourceFile.cs), with no mutant inventory, when the remainder is empty. Separate negative strings do not prevent this report-level reduction.

Revision 1's complement excludes the full interval `0..2147483647`. Its mutant ownership calculation is valid, but its report would be incomplete. All implementation and Python tests stopped when this was found. The original partial selectors were saved on backup commit `49d4194de0be76fb84442f47cb78cef4eb7bf8ea`; no passing end-to-end proof was claimed for them.

The approved amendment ends only the final complement exclusion at the actual UTF-16 source length, keeping each negative string separate. This leaves an unoccupied suffix in the report-inclusion geometry while retaining all mutant-level decisions. A read-only calculation against all 6636 retained positive native node spans confirmed unchanged selection for all eight complements; every node ends at or before its source length. This is source/data verification, not an instrumented run or a new mutation measurement. The amended tests now independently exercise both reporter inclusion and mutant ownership; both suites passed as recorded below.

Before the pause, actionlint, syntax and an assertion-subsequence audit were preliminary checks only. On the corrected source, actionlint 1.7.12 with ShellCheck 0.11.0 passed again, and the final audit preserved all existing assertion expressions outside the approved administrative contract. It also checked the approved test rename without treating added assertions as removals. The required corrected-source checks are recorded below; final-head CI and hosted reviews remain separate gates.

## Required verification

From repository root, using the retained campaign downloaded from GitHub as uncommitted inputs:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py --measured-inventory /workspace/scratch/397-rebalance/baseline /workspace/scratch/397-rebalance/summary/summary.json
npm run check:docs
PATH=/workspace/scratch/397-rebalance/tools:$PATH actionlint .github/workflows/verifier-c5-mutation.yml
git diff --check
git diff --name-only origin/main
```

The first command passed 104 tests with no skips. The second passed its explicit complete retained-input proof: 6636 identities, 5329 weighted tested identities, 61 reportable jobs and raw numerator/denominator 5000/5329. All original statuses and identity fields reconcile exactly; the report reference rejects the original unbounded complement and accepts each corrected selector. Exact conserved amortized work is `26015784566608281/156250000000` seconds. The maximum unrounded dated runtime estimate remains `445371390747/80000000`, rounded upward to 5568 seconds; this is an estimate, not measured optimized runtime.

Documentation scan exited zero: eight advisory warnings are confined to unchanged files and are left outside this PR's scope under docs/agents/pull-requests.md rule 8. No changed document has a warning. Actionlint, whitespace, ASCII and exact changed-path checks passed; the authored set is exactly the seven approved files, with no C5 workflow mirror present. The workflow diff changes only matrix entries, leaving triggers, permissions and all action pins intact. The code/test source SHA-256 values are `d419b8dd5c461ec517a8b48a7529e664751c608c9a503e06f5140bc7d7cbb23e` for c5_workflow.py and `09928861fbaec255ef0a056c1d6e654e3cf43f3f275918fef465fd001104e7a4` for test_c5_workflow.py. No diagnostic implementation correction or affected-command rerun was needed for these initial Python commands. Native reports, logs and calculated program output stay outside git.

## Orchestration predicate coverage

| Predicate or contract | Tests and boundaries |
| --- | --- |
| Cut count, integer type, ordering and source bounds | PrimaryLayoutInputTests checks mismatch, invalid type, duplicate/reversed cuts and cuts below/at/above source edges. SpanTests retains empty input and the generic member algorithm. |
| Static region exclusion at a cut | PrimaryLayoutInputTests permits touching field edges and rejects an interior cut; production and committed static-region tests require exactly one isolation owner. |
| Whole-node containment versus crossing ownership | ComplementSelectionTests checks both sides and exact cut contacts, nesting, multi-cut bodies and distinct identities sharing a span. Existing ReconcileTests retains gap, overlap, identity and global disposition failures. |
| Reporter union/subtraction versus independent mutation filters | ReporterSelectionTests checks empty residual, nonempty residual, no negatives, a fully covered primary and adjacency across separate patterns, including the original unbounded failure. |
| Source length in UTF-16 after BOM removal | ReporterSelectionTests checks plain and BOM-prefixed astral input, exact length 9, nodes ending at length and rejection of zero-length or past-end nodes. No valid native node can occupy the reporting-only suffix because node_span rejects it. |
| Zero tested mutants in a complement, target completeness and exact layout | ProductionAggregationTests accepts zero-tested complements with positive target population and retains the original missing/duplicate-owner failure paths; ProductionLayoutTests pins all 61 selectors, target identities and reporter residuals. |
| Complete measured population, configuration and arithmetic | The explicit measured suite validates all 28 retained inputs and bindings, isolated results, all 6636 node identities, exact status/score equality and all 61 model values, with independent selection and report references. |

## Review record

Cold review input: comprehensive round 1 of this PR, comparing the full authored diff against origin/main `8e2ab2fbb1ca7fe1a02396af4a07ac9626c13c73`. The code/test source is commit `cee0113e745c25e779cd11ca742c4122de51e455`; staged documentation and state changes complete the reviewed input. The frozen diff path and digest identify the input outside git. Cumulative focused count is zero. The complete final report will be linked in the PR description rather than appended to its own reviewed input. Hosted code/security review, final-head CI and the separate Claude review remain required and are recorded in that description; no merge readiness is claimed here.

The [Revision 2 independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703401722) supported the correction. [Hari's Revision 2 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703427658) resolved its sole blocking decision and authorized implementation to resume. The earlier bookkeeping approval remains in force.
