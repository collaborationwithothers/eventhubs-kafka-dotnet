# C5 measured span rebalance

This allocation redistributes the existing C5 mutant population across 61 jobs. Its dated model estimates every job below 7200 seconds with margin, but no optimized campaign has run. The same tests, raw score and acceptance gates remain required.

## Authority and baseline

The [measured span rebalancing plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703130075), its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703401722) and [Hari's amendment approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703427658) authorize this allocation and its source-length clamp for crossing selectors. The [original check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703193224) and [administrative assertion/fixture approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6703287109) remain authority for the named shard-contract values and legacy fixture bindings. Every judging assertion, raw score, coverage-off setting, static-field isolation, completeness gate and threshold remains required. The [first investigation receipt](receipt.md) describes the earlier investigation; this change has its own [authored execution receipt](rebalance-receipt.md).

The measured input is [hosted run 42274873194](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194), attempt 1. Its source commit is `8e2ab2fbb1ca7fe1a02396af4a07ac9626c13c73`, with tree `04d03d7870529148f7b61838aa3db07917428611`. The [summary artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13546839407) and all 28 shard artifacts supply the native reports, receipts, retained source and isolated-invocation reports. The original artifacts are linked by shard in the rate table below. Preserve these retained inputs before artifact expiry; the scratch paths below are local input locations, not permanent evidence links.

The native reports contain 6636 distinct target/mutant identities. Each identity retains its target, id, mutator, replacement and location; its reconciled status and status reason are preserved. Reaggregation of all retained shard reports and isolated results agrees with the downloaded summary: `measurement_complete: true`, `break_passed: true`, numerator 5000 and tested denominator 5329. This conclusion uses the native evidence and receipt bindings, not the workflow badge. The independent plan check records the same reaggregation and source verification.

| Native status | Reconciled count |
| --- | ---: |
| Killed | 4999 |
| Timeout | 1 |
| Survived | 329 |
| NoCoverage | 0 |
| CompileError | 1089 |
| RuntimeError | 0 |
| Ignored | 218 |

The raw numerator is `Killed + Timeout = 5000`. The tested denominator is `Killed + Timeout + Survived + NoCoverage = 5329`. Compile errors and matching non-filter Ignored outcomes count once globally under the existing reconciliation rules; they do not require a tested shard owner. They remain in the full inventory. This allocation changes neither those rules nor the per-target positive denominator and break-60 gates.

## Old measured rates

The retained receipts measure whole-shard elapsed time, including setup and static isolation. For each old shard, divide that elapsed time by its reconciled tested denominator. This amortized rate is not a measured duration for an individual mutant or assertion. Displayed elapsed values are rounded to milliseconds and rates to six decimal places; the allocation uses the original JSON decimal text as exact rational input.

| Old shard | Retained elapsed / s | Tested denominator | Amortized s / tested mutant |
| --- | ---: | ---: | ---: |
| [phase](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13540764743) | 8660.106 | 282 | 30.709596 |
| [publication](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13540410865) | 6954.231 | 205 | 33.923078 |
| [a01](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13535945551) | 2448.427 | 71 | 34.484886 |
| [workload](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13536143693) | 3973.714 | 95 | 41.828567 |
| [single-action-repeat-p1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13542610545) | 8886.983 | 257 | 34.579699 |
| [single-action-repeat-p2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13543768808) | 11492.623 | 384 | 29.928706 |
| [single-action-repeat-p3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13538407326) | 5573.237 | 159 | 35.051805 |
| [single-action-configuration-p1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13538062502) | 5279.152 | 114 | 46.308351 |
| [single-action-configuration-p2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13533459722) | 1887.937 | 51 | 37.018377 |
| [single-action-configuration-p3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13543970669) | 10278.373 | 360 | 28.551036 |
| [single-action-configuration-p4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13547766552) | 14729.056 | 347 | 42.446846 |
| [single-action-configuration-p5](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13541215918) | 7888.346 | 353 | 22.346589 |
| [single-action-configuration-p6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13545713168) | 13185.995 | 373 | 35.351194 |
| [single-action-configuration-p7](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13539884511) | 7671.314 | 356 | 21.548636 |
| [single-action-configuration-p8](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13533934386) | 2038.222 | 57 | 35.758281 |
| [idle-observation-p1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13536095782) | 2915.854 | 62 | 47.029909 |
| [idle-observation-p2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13539065905) | 5891.445 | 175 | 33.665401 |
| [idle-observation-p3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13539834510) | 7604.585 | 210 | 36.212309 |
| [batching](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13534118167) | 2041.239 | 100 | 20.412390 |
| [b10-restart-manifest](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13537623466) | 5141.500 | 133 | 38.657892 |
| [b10-intake](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13535819486) | 3834.057 | 122 | 31.426699 |
| [b10-judge](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13534203714) | 2348.188 | 111 | 21.154845 |
| [b10-repeat](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13539791784) | 6532.498 | 177 | 36.906767 |
| [b10-phase-registrations](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13534345787) | 1262.300 | 80 | 15.778753 |
| [b10-late-write](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13534719211) | 2594.615 | 99 | 26.208234 |
| [b10-sweep](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13533828522) | 1686.481 | 71 | 23.753260 |
| [b10-sweep-judge](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13536766611) | 3589.224 | 107 | 33.544149 |
| [b10-sweep-codec](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/42274873194/artifacts/13543076827) | 10111.319 | 418 | 24.189757 |

## Dated model and assumptions

**Estimate, dated 2026-11-07.** For a tested native mutant `m`, let `o(m)` be its old owning shard after isolated results are merged. Let `r_s = retained_receipt.elapsed_seconds / reconciled_shard.denominator`. For a new job `j`, compute the following using rational arithmetic:

```text
W_j = sum(r_o(m) for each tested mutant assigned to j)
E_j = ceil(1.25 * W_j + 600) seconds
```

Tested means Killed, Timeout, Survived or NoCoverage. Compile errors and early non-filter Ignored outcomes have no execution weight. They remain reconciled exactly once. The 25 percent multiplier and additional 600-second reserve are chosen estimate margins for variability and duplicated setup. They are not measured confidence bounds.

The allocation sweeps equal tested-node start-offset groups in ascending UTF-16 order. It places a cut before a group when adding it would exceed 3500 amortized seconds, provided the cut is outside a static field interior. A target of total weight at most 4320 retains a whole-file singleton. After assigning full node spans to primaries or crossing, the model recomputes every weight and checks the chosen 6000-second estimate target. Production uses fixed literal cuts; it does not download artifacts or recalculate rates during a run.

The model assumes comparable standard hosted runners, unchanged tools and configuration, every judging test retained, and no material source or test cost change. All 61 unrounded estimates are below 6000 seconds. The exact maximum is `445371390747/80000000 = 5567.1423843375` seconds. Its ceiling is 5568 seconds, leaving 1632 seconds below 7200. These are modeled job estimates, not an observed speedup, scheduling guarantee or total campaign duration.

Changing the mutants that share an invocation can change timeout classifications. Retaining the population and judging tests does not promise identical future Killed/Timeout results. The model does not predict queue delays or aggregate-job overhead. After source or test changes, these estimates are historical and require remeasurement before any new runtime claim.

## Literal cuts and ownership

Offsets count UTF-16 code units after removal of a UTF-8 byte order mark. For each split target, adjacent primary ranges are `(0, cut1)`, `(cut1, cut2)`, through `(last_cut, 2147483647)`. Ends are exclusive. The fixed cuts are:

```python
PRIMARY_CUTS = {
    'phase': (4376, 6570),
    'publication': (4719,),
    'single-action-repeat': (3555, 7222, 10692, 13977, 16262, 19347, 22533),
    'single-action-configuration': (2314, 4053, 6613, 9284, 10952, 13113, 14461, 16935, 19779, 25473, 28510, 32364, 34949, 37376, 39225, 44139, 48960, 53161),
    'idle-observation': (5062, 8804, 12323, 14813),
    'b10-restart-manifest': (6681,),
    'b10-repeat': (8428,),
    'b10-sweep-codec': (5934, 10787),
}
```

The primary counts are 3, 2, 8, 19, 5, 2, 2 and 3 in that order. Each split target has one additional crossing job. The remaining nine targets retain one whole-file job each. The complete ordered job table below defines the 61 mutation jobs; the aggregate job is dependent and separate.

A primary selects `target{L..R}` and retains the existing static-region exclusion strings. A crossing job selects the bare target followed by one separate negative string for every primary range. Only the final negative endpoint is clamped to the decoded source's actual UTF-16 length. The exact configuration is:

```python
length = utf16_length(target_text(source))
config["stryker-config"]["mutate"] = [
    target,
    *(f"!{target}{{{low}..{min(high, length)}}}"
      for low, high in primary_spans(key, source)),
]
```

The existing layout validation keeps every cut strictly inside the source and its length below 2147483647. The clamp therefore changes only the final negative endpoint. The bare positive target and final primary inclusion still extend to 2147483647. Use the retained original target bytes with `target_text` and `utf16_length`; UTF-8 byte count, Python code-point count and projected pragma length give the wrong endpoint.

Under pinned Stryker [FilePattern.cs Parse/IsMatch](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Configuration/FilePattern.cs) and [FilePatternMutantFilter.cs constructor/FilterMutants](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/MutantFilters/FilePatternMutantFilter.cs), each pattern tests whole-node containment independently. A contained node belongs to one primary and is excluded from crossing. A whole-method or block-removal node crossing a cut belongs only to the complement. Distinct identities sharing a node span remain distinct.

Keep each negative pattern a separate string. `FilePattern.Parse` reduces spans within one pattern, so coalescing adjacent exclusions can also exclude a parent node crossing the cuts. An excluded child does not exclude a larger parent that the negative pattern does not fully contain. The retained inventory contains no zero-length node span. The unchanged `node_span` validator requires `0 <= start < end <= length`. For every such node, containment in the old final negative and its clamped replacement is identical. The cuts, valid mutant ownership, static isolation, 6636 identities, 5329 tested identities and all weights and estimates therefore remain unchanged.

### Keep the crossing target in the native report

Native reporting applies a different predicate from per-node mutation selection. Pinned [ProjectComponentsExtensions.cs IsComponentExcluded](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponentsExtensions.cs) flattens matching spans across separate positive and negative strings, then reduces each union. [TextSpanHelper.cs Reduce/RemoveOverlap](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Utilities/Helpers/TextSpanHelper.cs) merges intersecting or touching spans and subtracts the exclusion union. Separate negative strings still merge for this reporting predicate.

The original unbounded crossing negatives cover 0..2147483647. Their union removes the whole positive target even though independent per-node filtering correctly selects crossing nodes. [JsonReport.cs GenerateFileReportComponents](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/JsonReport.cs) then serializes [SourceFile.Ignored](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/SourceFiles/SourceFile.cs): language `none`, placeholder source and zero mutants. It retains a file key, but loses the native target inventory needed for reconciliation. The implementing session found this source error before publication or any optimized campaign.

The approved final-negative clamp leaves exactly the nonempty reporting interval `(length, 2147483647)`. The reporter consequently retains the actual C# source and native mutant set. This suffix contains no source code or valid mutant node. Its sole purpose is to keep the target included by the reporter while leaving the valid-node selection unchanged. An all-Ignored or zero-tested crossing complement still needs the real native inventory.

The required independent reporter reference merges intervals across strings, including adjacency, and subtracts the entire negative union. With primary ranges `(0,10)`, `(10,20)` and `(20,2147483647)` and decoded length 30, the original negatives leave no report interval. Clamping the final negative to 30 leaves exactly `(30,2147483647)`. Separate exclusions `(0,10)` and `(10,2147483647)` must also leave no interval; a reference that merges only within one pattern misses the defect. Fully included and fully excluded primary cases exercise both reporter outcomes.

The source-length regressions cover plain, UTF-8 BOM and astral text. The literal `"ab\U0001f600cdefg"` has decoded UTF-16 length 9. With cuts `(2,4)`, its crossing negatives are separate `(0,2)`, `(2,4)` and `(4,9)` strings for both plain and BOM-prefixed UTF-8 bytes. Its reporting suffix is `(9,2147483647)` while primary endpoints stay unchanged. Boundary-touching, multi-cut crossing and source-end nodes retain exactly one owner; zero-length or past-source nodes remain invalid.

Every static field region fits exactly one primary. Its static-field mutants retain that primary's existing isolated invocations. The crossing job has no static regions or isolated static-field owner. A complement is a disconnected selection, so its retained span and rebalance metadata use `span: None`. A singleton also retains its existing whole-file `None`. Publication and B10 repeat complements have zero tested mutants in this baseline. Their complete inventories still participate in reconciliation; no per-shard positive-denominator gate is introduced.

## Complete job estimates

`W` is amortized work, displayed to milliseconds. `E` is the estimate rounded upward to whole seconds. All counts and weights below use the retained baseline statuses; they are not results from an optimized campaign.

| Proposed shard | Tested mutants | W / s | E / s, estimate 2026-11-07 |
| --- | ---: | ---: | ---: |
| phase-p1 | 112 | 3439.475 | 4900 |
| phase-p2 | 111 | 3408.765 | 4861 |
| phase-p3 | 56 | 1719.737 | 2750 |
| phase-crossing | 3 | 92.129 | 716 |
| publication-p1 | 103 | 3494.077 | 4968 |
| publication-p2 | 102 | 3460.154 | 4926 |
| publication-crossing | 0 | 0.000 | 600 |
| a01 | 71 | 2448.427 | 3661 |
| workload | 95 | 3973.714 | 5568 |
| single-action-repeat-p1 | 101 | 3492.550 | 4966 |
| single-action-repeat-p2 | 101 | 3492.550 | 4966 |
| single-action-repeat-p3 | 102 | 3308.533 | 4736 |
| single-action-repeat-p4 | 116 | 3471.730 | 4940 |
| single-action-repeat-p5 | 113 | 3381.944 | 4828 |
| single-action-repeat-p6 | 113 | 3448.544 | 4911 |
| single-action-repeat-p7 | 98 | 3435.077 | 4894 |
| single-action-repeat-p8 | 46 | 1612.383 | 2616 |
| single-action-repeat-crossing | 10 | 309.533 | 987 |
| single-action-configuration-p1 | 75 | 3473.126 | 4942 |
| single-action-configuration-p2 | 83 | 3434.834 | 4894 |
| single-action-configuration-p3 | 111 | 3219.969 | 4625 |
| single-action-configuration-p4 | 122 | 3483.226 | 4955 |
| single-action-configuration-p5 | 122 | 3483.226 | 4955 |
| single-action-configuration-p6 | 84 | 3412.681 | 4866 |
| single-action-configuration-p7 | 81 | 3438.195 | 4898 |
| single-action-configuration-p8 | 78 | 3310.854 | 4739 |
| single-action-configuration-p9 | 81 | 3438.195 | 4898 |
| single-action-configuration-p10 | 129 | 3465.617 | 4933 |
| single-action-configuration-p11 | 155 | 3463.721 | 4930 |
| single-action-configuration-p12 | 130 | 3334.209 | 4768 |
| single-action-configuration-p13 | 98 | 3464.417 | 4931 |
| single-action-configuration-p14 | 99 | 3499.768 | 4975 |
| single-action-configuration-p15 | 78 | 2757.393 | 4047 |
| single-action-configuration-p16 | 136 | 3496.519 | 4971 |
| single-action-configuration-p17 | 155 | 3340.039 | 4776 |
| single-action-configuration-p18 | 137 | 3477.920 | 4948 |
| single-action-configuration-p19 | 20 | 715.166 | 1494 |
| single-action-configuration-crossing | 37 | 1249.320 | 2162 |
| idle-observation-p1 | 76 | 3387.170 | 4834 |
| idle-observation-p2 | 103 | 3467.536 | 4935 |
| idle-observation-p3 | 93 | 3220.024 | 4626 |
| idle-observation-p4 | 96 | 3476.382 | 4946 |
| idle-observation-p5 | 72 | 2607.286 | 3860 |
| idle-observation-crossing | 7 | 253.486 | 917 |
| batching | 100 | 2041.239 | 3152 |
| b10-restart-manifest-p1 | 86 | 3324.579 | 4756 |
| b10-restart-manifest-p2 | 45 | 1739.605 | 2775 |
| b10-restart-manifest-crossing | 2 | 77.316 | 697 |
| b10-intake | 122 | 3834.057 | 5393 |
| b10-judge | 111 | 2348.188 | 3536 |
| b10-repeat-p1 | 90 | 3321.609 | 4753 |
| b10-repeat-p2 | 87 | 3210.889 | 4614 |
| b10-repeat-crossing | 0 | 0.000 | 600 |
| b10-phase-registrations | 80 | 1262.300 | 2178 |
| b10-late-write | 99 | 2594.615 | 3844 |
| b10-sweep | 71 | 1686.481 | 2709 |
| b10-sweep-judge | 107 | 3589.224 | 5087 |
| b10-sweep-codec-p1 | 143 | 3459.135 | 4924 |
| b10-sweep-codec-p2 | 143 | 3459.135 | 4924 |
| b10-sweep-codec-p3 | 131 | 3168.858 | 4562 |
| b10-sweep-codec-crossing | 1 | 24.190 | 631 |

## Claude review correction

The [Claude review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/402#issuecomment-6704105328) found a future-layout guard missing from the original enforced checks. [Hari requested the fix](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6704285300). A declared split now requires at least two integer primary spans before PARTS is constructed, preventing a crossing entry from overwriting a singleton primary. Direct primary-span selection also rejects that invalid layout before a mutant process can launch. An unsplit singleton remains valid. The current 61-job layout and every rate, cut and estimate above are unchanged.

The report-coordinate assumptions in that review are now traced to pinned sources. Stryker's [JsonMutant serializer](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/SourceFiles/JsonMutant.cs#L39) calls `OriginalNode.GetLocation().GetMappedLineSpan()`, while its [file-pattern filter](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/MutantFilters/FilePatternMutantFilter.cs#L48-L49) tests `OriginalNode.Span`. The [Location constructor](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/Reporters/Json/Location.cs#L18-L29) adds one to both line and character, giving one-based JSON coordinates.

Stryker [pins Roslyn 5.9.0](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Directory.Packages.props#L11-L13); that version's [official NuGet metadata](https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.common/5.9.0/microsoft.codeanalysis.common.nuspec) identifies source commit `35d9211b841e7613c1d2f8f5af6d628ace696c4c`. Its [SyntaxNode.GetLocation](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/Syntax/SyntaxNode.cs#L674-L676) starts with the same node span, and [SourceLocation](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/Diagnostic/SourceLocation.cs#L94-L105) passes it to mapped-line conversion. [LineDirectiveMap](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/Syntax/LineDirectiveMap.cs#L49-L74) converts its start and end through source lines. [TextLineCollection](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/Text/TextLineCollection.cs#L46-L57) computes the character as position minus line start; [StringText](https://github.com/dotnet/roslyn/blob/35d9211b841e7613c1d2f8f5af6d628ace696c4c/src/Compilers/Core/Portable/Text/StringText.cs#L47-L63) uses string length and char indexing. A [.NET char represents a UTF-16 code unit](https://learn.microsoft.com/en-us/dotnet/api/system.char?view=net-10.0).

Mapped coordinates can differ from physical source coordinates when `#line` directives are present, including column changes with enhanced mappings. All 17 current configured targets were inspected and contain no such directive; their assigned target bytes remain unchanged by projection. For these targets, the JSON coordinates therefore represent the same original-node span in one-based UTF-16 line/column form. A future target with line mappings needs this assumption rechecked; no new line-mapping guard or broader parser is introduced by this correction.

## Source verification

The retained target bytes match the current repository target bytes before applying the estimates. The proof checks these hashes against the retained source, native report source and receipt/projection bindings. The source commit above identifies the full unchanged test and configuration inputs. These target hashes identify the exact source geometry used for the cuts:

| Target key | Target path under `src/Parchwell.Verifier/` | Source SHA-256 |
| --- | --- | --- |
| `phase` | `Judge/PhaseJudge.cs` | `c5bb7044391429f605eecd9bbcaaa4a8cbfe43e86672d248015efb5235636677` |
| `publication` | `VerifyRun/Publication/PublicationAcceptance.cs` | `3864df8fc2eec5985aaea2033ecb63667e1f24ef81facaa64039836c3f20c137` |
| `a01` | `VerifyRun/A01/A01EvidenceRecorder.cs` | `d01f8995692a84cd8bfd908b9874bd72eba08d2f737529c05c1a5d8973780806` |
| `workload` | `VerifyRun/A01/WorkloadRecorder.cs` | `065cde4de7cd27b41946b665121589f9e5b333f0d6c4814346fd4a2cc082f808` |
| `single-action-repeat` | `VerifyRun/SingleAction/SingleActionRepeatJudgement.cs` | `5c0788ac841b4397610f602435cd7ef923b1b4caa36d701c454891f79b2706a8` |
| `single-action-configuration` | `VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | `e266b1d73da52fe84fbb583bac42e8bd5e2118af445d8a2f3c4bf7c4ff67f661` |
| `idle-observation` | `VerifyRun/IdleObservation/IdleObservationJudgement.cs` | `53f985dbeb9664775180d8b12bf3b42c6c0895ace9e1990d3b56bf3dc280e475` |
| `batching` | `VerifyRun/LedgerBatch/BatchingEvidenceRecorder.cs` | `b2e9391b5b0d324b6e65f281bcb356dc0c8d8ae1d06fe039f57ad90d362adf5b` |
| `b10-restart-manifest` | `VerifyRun/Input/RestartManifestReader.cs` | `d48a59bcbe7af3a60b00555b2b34f0faf40c671dbbfbce951ad835cc17000598` |
| `b10-intake` | `VerifyRun/Judgement/B10IntakeChecks.cs` | `07d318b98bc9670d53e2eb27536b52a092e107f4779e3c0409b5e2575b3535d6` |
| `b10-judge` | `Judge/B10JudgeChecks.cs` | `b365656c412f8415f760af8256bcb23c755d340cff843e288d730c18f07c44da` |
| `b10-repeat` | `VerifyRun/Judgement/B10RepeatChecks.cs` | `c34be5717c08a3b5757d750eeccaebcf92755870aefa041b08eb85142068089b` |
| `b10-phase-registrations` | `VerifyRun/Input/B10PhaseRegistrationReader.cs` | `303ab396430505d6b84f2583f93ca9ef7741f958e16825a6f0267d8115446711` |
| `b10-late-write` | `VerifyRun/Judgement/B10LateWriteChecks.cs` | `6584efcc6d3c2b7a5c696b9cf23ec22145237e1e635bcdcd79d4044da08501a8` |
| `b10-sweep` | `VerifyRun/B10SweepHandler.cs` | `e35d51681b080e2ffde5f49345824b8c786775018874e5cb950df4edbd22c4ce` |
| `b10-sweep-judge` | `Judge/B10SweepChecks.cs` | `9b6d8a178b52b5d23ea877ab46209177159b2f3a30d9734885c8a366e0af71b0` |
| `b10-sweep-codec` | `Common/Phase/B10SweepEvidenceCodec.cs` | `9083ae9eb499d4b22fab69bc9086e465e8e020ce650d23da27cb5dec642caccd` |

## Reproduce the retained-input proof

Retain the summary at `/workspace/scratch/397-rebalance/summary/summary.json`. Extract exactly the 28 `c5-mutation-42274873194-1-SHARD` artifacts into `/workspace/scratch/397-rebalance/baseline/`, preserving each artifact-named directory and all native/isolated reports, receipts, configuration, projection source and command logs. The `c5-logs-` artifacts are not aggregation inputs. Missing or corrupt native inputs fail the proof; generated replacement identities are not a substitute.

Run from the repository root:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py --measured-inventory /workspace/scratch/397-rebalance/baseline /workspace/scratch/397-rebalance/summary/summary.json
```

Ordinary discovery requires no retained artifact and skips no tests. The measured suite is created only by the explicit command. That suite validates all receipt/report/source/projection/configuration/command-log bindings and isolated reports, reconstructs the 28 baseline owners, and compares target/global populations with the downloaded summary. Its independent selector evaluator checks one geometric primary/complement owner for all 6636 identities and one primary isolation owner for each static region. It projects the original tested statuses into the new selectors and reconciles them with the unchanged aggregator, retaining global compile/non-filter-Ignored dispositions. It checks identity/status equality, numerator/denominator 5000/5329, no missing or repeated tested identities, crossing bodies, equal-span identities and zero-tested complements.

Both the production-layout tests and measured suite run independent mutation-selection and reporter-inclusion references on all 61 configurations. The reporter must retain every assigned target, including zero-tested complements. Every crossing residual must be exactly `(length,2147483647)`; restoring its old unbounded final negative must exclude the target from reporting. The measured suite compares old and clamped negative containment for every real native node and requires identical selection. This reporting proof supplements the complete native reconciliation; it changes no acceptance predicate.

The same suite recomputes all 28 old-owner rational rates and all 61 job weights and estimates. It checks 5329 weighted identities, exact weight-sum conservation, the literal cuts, the exact maximum above and both estimate bounds. Its printed tables are untracked verification output. This report is the authored explanation of that evidence; native reports and generated JSON are not committed.

Both required Python commands passed on the corrected source in commit `cee0113e745c25e779cd11ca742c4122de51e455`: default discovery ran 104 tests without a skip, and the explicit retained suite passed its complete 6636-identity, 5329-tested, 61-report and score/model checks. These tests prove the static allocation and report-retention model against the retained inputs, not actual optimized Stryker timing. The [authored execution receipt](rebalance-receipt.md) records the commands, source revision, other checks and remaining publication gates.

## Concurrency and remaining measurement

Hari confirmed that this public repository uses Enterprise in the implementing-session brief. GitHub's [Actions limits](https://docs.github.com/en/actions/reference/limits), read 2026-11-07, documents 500 concurrent standard hosted jobs for Enterprise and a maximum of 256 jobs generated by a matrix. This layout has 61 mutation jobs and one dependent aggregate job. It uses standard `ubuntu-24.04` runners and adds no `max-parallel` setting or larger runner. Available shared concurrency and scheduling remain uncertain, so these limits establish no campaign duration.

Hari merges the workflow change after the required final-head checks and reviews. This implementation authorizes no workflow dispatch. Control owns one fresh main-only C5 measurement after merge. That run must establish actual runtime improvement and its resulting mutation acceptance evidence. The same coverage-off judging population, static isolation, raw score and complete/pass gates remain required; the allocation proof supplies no new mutation acceptance.

Claude correction verification on source commit `8fe6e65147634f956503fa147be4726a9d9abcc2` passed 107 helper tests without skips and the complete retained-inventory suite. The three added regressions cover construction-time rejection, direct span validation and no child launch for a singleton split. The full proof still records all 6636 identities, 5329 tested identities, 61 configurations and unchanged score/model values.
