# B10 intake readers: implementation receipt

This change reads the second B10 producer registration and restart manifest. It exposes both producer Job names for later verification work and does not establish final B10 counts.

## Execution card

- Deliverable and scope: [approved plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/375#issuecomment-6635344464), changes 1 to 7 and its complete Enforced checks list.
- Authorization: [standing-approval plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/375#issuecomment-6635457968), which identifies Revision 2 updated 2026-11-04T15:58:00Z and reports no blocking findings. The child's [split adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/375#issuecomment-6633310596), [Codex and Claude-review adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/375#issuecomment-6633532984) and [standing-approval amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/375#issuecomment-6633793279) remain in force.
- Source commit: `e844587dfbcb4493b89eb844ec916097c31ae5ed` (the merged predecessor, PR 378). Verification below describes the authored changes based on that commit.
- Mandatory gates: the plan's exact verification commands; applicable cold review under [pull-request rules](../../docs/agents/pull-requests.md), rule 8; final-head CI and hosted Codex review; the adopted separate Claude review; Hari merges the workflow-path change. Post-merge main C5 measurement remains pending.
- Exact commands: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py`; `npm run test:docs`; `npm run check:docs`; `git diff --check`; `actionlint .github/workflows/verifier-c5-mutation.yml`. The plan's YAML and arithmetic assertions are also required.
- Earliest publication: tested diff with applicable cold-review coverage, followed by authority refresh. Parent session owns publication.
- Budget: the plan's Runtime-sized work and exhaustion actions cap changed-file warning dispositions at 8 and implementation failure root causes at 12; exhaustion means push -wip and hand off to the next session. Cold review follows rule 8: at most two comprehensive and two focused correction rounds cumulatively; exhaustion or unsafe focus means backup push and Hari handoff. No local mutation campaign is authorized.

## Verification of the initial B10 revision

Verification used SDK 10.0.402 and the pinned actionlint 1.7.12 through `/tmp/issue375-dotnet` and `/tmp/issue375-tools` on PATH. Generated logs and reports remain outside the repository. The passing full run is `/tmp/issue375-implementation-verify-final.log`, with checked reports in `/tmp/parchwell-dotnet-verification.OuUFRdtR`; the failed pre-correction run remains `/tmp/issue375-implementation-verify.log` for traceability.

| Command | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Passed on the corrected diff: locked restore, Release build/analyzers, XML consistency, architecture and all participating suites, plus the warning-as-error negative control. 7288 tests passed, none failed or skipped (including 5215 verifier tests). |
| `dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --no-restore -c Release` | Passed with zero warnings/errors after the lower-bound additions and again after correcting nullable Int64 fixture arguments. |
| `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 -- --filter-class '*RestartManifestReaderTests'` | Passed after the fixture-type correction: 148 tests, no failures or skips. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py` | Passed: 78 tests. |
| `npm run test:docs` | Passed: 28 tests, no failures or skips. |
| `npm run check:docs` | Passed, including staged state/receipt: eight warnings, all in unchanged files. |
| `actionlint .github/workflows/verifier-c5-mutation.yml` | Passed. |
| `python3 /tmp/issue375-mutation-calculations.py` | Passed: eight targets, 19 shards, one new shard; synthetic score fractions 10/14, 7/14, 11/14, 8/59 and 14/17. Full YAML comparison permits only the appended row. |
| `git diff --check` and `git diff --cached --check` | Passed. |

The implementation encountered two failure causes within the planned files: a concurrent test edit referenced the restart projection before that source edit landed, then the first full run rejected eight new fixture rows whose suffix arguments were boxed as Int32 for nullable Int64 parameters. Rebuilding after the projection landed resolved the first. The fixture correction uses long literals with identical values and assertions; focused and full verification after that correction are recorded above. This consumes two of the plan's 12 failure-cause slots. Changed-file documentation warning dispositions used: zero. The unchanged-file warnings take the rule 8 blanket disposition in the PR description.

### Calculation command

`python3 /tmp/issue375-mutation-calculations.py` ran the following authored assertions. They calculate configuration and synthetic test populations; they are not mutation evidence. The script is reproduced here so the verification is reviewable without a local temporary file.

```python
import ast, copy, json, subprocess, sys
from collections import Counter
from pathlib import Path
import yaml
sys.path.insert(0,'tests/Parchwell.Verifier.Tests/Mutation')
import c5_workflow as workflow
assert len(workflow.TARGETS)==8
assert sum(count for _,count in workflow.TARGETS.values())==19
assert workflow.TARGETS['b10-restart-manifest']==('VerifyRun/Input/RestartManifestReader.cs',1)
print('target_count=8 shard_count=19 new_target_count=1')
p='.github/workflows/verifier-c5-mutation.yml'
a=yaml.safe_load(subprocess.check_output(['git','show','e844587dfbcb4493b89eb844ec916097c31ae5ed:'+p],text=True));b=yaml.safe_load(Path(p).read_text())
assert a[True]==b[True]=={'workflow_dispatch':None}
assert a['permissions']==b['permissions']=={'contents':'read'}
old=a['jobs']['mutation']['strategy']['matrix']['include'];new=b['jobs']['mutation']['strategy']['matrix']['include']
assert old==new[:-1]
assert new[-1]=={'shard':'b10-restart-manifest','target':'VerifyRun/Input/RestartManifestReader.cs'}
for job in b['jobs'].values():assert "github.ref == 'refs/heads/main'" in job['if']
b['jobs']['mutation']['strategy']['matrix']['include'].pop();assert a==b
print('YAML unchanged apart from appended matrix row; dispatch only; both jobs main-only')
source=ast.parse(Path('tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py').read_text())
expected={
 'test_combined_threshold_keeps_below_threshold_shards':(10,14),
 'test_complete_below_threshold_stays_complete':(7,14),
 'test_timeout_and_no_coverage_score_populations':(11,14),
 'test_survivor_sampling_does_not_change_raw_score':(8,59),
 'test_span_shards_combine_into_one_target_population':(14,17),
}
for node in ast.walk(source):
 if isinstance(node,ast.FunctionDef) and node.name in expected:
  assignment=node.body[0]
  value=assignment.value
  expr=value.args[1] if isinstance(value,ast.Call) else value
  population=eval(compile(ast.Expression(expr),'<literal synthetic populations>','eval'),{'__builtins__':{}})
  assert len(population)==8
  counts=Counter(status for rows in population for status in rows)
  score=(counts['Killed']+counts['Timeout'],sum(counts[x] for x in ['Killed','Timeout','Survived','NoCoverage']))
  assert score==expected[node.name]
  print(node.name,score,dict(counts))
assert 1844674407370955161*5+2==9223372036854775807
print('checked-slot boundary:',[1844674407370955161*5+s for s in (1,2,3)])
```

## Predicate coverage

The new registration tests are in [B10RegistrationTests.cs](../../tests/Parchwell.Verifier.Tests/Phase/B10RegistrationTests.cs), options tests in [B10ExpectedProducerJobsTests.cs](../../tests/Parchwell.Verifier.Tests/Host/B10ExpectedProducerJobsTests.cs), and manifest tests in [RestartManifestReaderTests.cs](../../tests/Parchwell.Verifier.Tests/Input/RestartManifestReaderTests.cs). Method fragments below identify their Given/When/Then test names. Fixtures keep unrelated earlier checks valid; logical planned-field mutations also update inventory when needed.

| Predicate or boundary | Tests and evidence |
| --- | --- |
| First and second registration roles | `B10FirstOrSecondRole` accepts both roles at C0/C1 and repeats 1/5; `InvalidRegistrationRole` rejects missing predecessor, first Job with predecessor, A01/B04/B06/B07 second Jobs and wrong phase/repeat/suffix. Reader and writer both reject malformed roles. |
| Optional string and closed registration schema | Literal producer-shaped JSON and round trips; malformed-field cases cover null, number, unknown and missing original field; duplicate predecessor is literal JSON. First-instance output omits the property. |
| Distinct predecessor segment | `InvalidPredecessor` covers empty/blank/interior whitespace, slash/backslash/control/period and self-reference; lengths 0/1/2 cover the lower bound, and 127/128/129 cover the upper bound. |
| Restart Job coordinate validation | Exact C0/r1 and C1/r5 names; repeat 0/1/5/6; run length 28/29/30; uppercase run and foreign phase. Existing phase/run validation owns these boundaries. |
| First-Job completion compatibility | New second-Job completion rejection, with existing non-B10 registration/completion tests retained unchanged. |
| Options after validation | Both start-line and verify at C0/r1 and C1/r5 return first then second Job; A01/B04/B06/B07 return only first; supplied second Job still fails with the original diagnostic; repeat 0/6 are tested through validation. Judge/probe evaluation is outside this property's contract. |
| Optional restart and structural schema | Golden reader asserts absence as null. `NonObjectRestart`, `MissingRestartField`, `UnknownRestartField`, `WrongKindRestartField`, `NonObjectPlannedEntry` and `DuplicateRestartProperty` cover root/entry shape, every required field, nulls, numeric strings/fractions/Int64 overflow and exact paths. |
| Projection and list ordering | `SparseRestartPlan` asserts every literal projected field under original and reversed object property order. Empty planned/inventory is covered by `ValidInventoryPrefix` and `EmptyListsAndZeroInventoryLimit`. |
| Manifest check 1: B10 | `OtherScenario` rejects A01 and lowercase b10; valid fixtures accept B10. `MultipleSemanticProblems` establishes precedence over invalid predecessor. |
| Check 2: predecessor identity | `InvalidPredecessor` covers empty, blank, whitespace/control and self-reference. `MultipleSemanticProblems` establishes precedence over count mismatch. |
| Check 3: declared planned count | `MismatchedPlannedCount` rejects -1, 1 and 3 against count 2; `ValidInventoryPrefix` accepts equality at 0, 1 and 2. |
| Check 4: ordinals, IDs and text | `InvalidPlannedEntry` covers first ordinal -1/0/1 and second 0/1/2, duplicate predecessor attempt, empty/blank/control-bearing fields. `InteriorSpacesInControlText` accepts interior spaces in tenant/key and predecessor attempt. |
| Check 4: document, sequence, event and key | `InvalidPlannedEntry` covers empty/short/noncanonical/overflow document text, sequence -1/5, wrong event/key; `ValidSequenceEventPair` accepts all 0..4 mappings with minimal document d0. |
| Check 4: nonnegative planned time | `InvalidPlannedEntry` rejects -1; `NonnegativePlannedTime` accepts 0, 1 and Int64 maximum. |
| Check 4: increasing logical slots | `NonIncreasingLogicalSlots` rejects equal and descending slots; sparse fixture accepts increasing slots. `ValidSequenceEventPair` includes initial slot 0 above the -1 sentinel. |
| Check 4: checked arithmetic | `LogicalSlotAtInt64Boundary` accepts 9223372036854775806/9223372036854775807 and rejects 9223372036854775808, with valid event/key/sequence on each side. |
| Check 5: inventory prefix | `InventoryLogicalFieldDiffers` changes each of the six logical fields independently; `InventoryLongerThanPlan` rejects index equal to planned Count; `SwappedInventoryLogicalFields` rejects reordering. Valid empty/partial/full prefixes cover below/at length. |
| Check 6: suffix and completion | `InvalidSuffixOrCompletion` covers partial suffix null/0/2 around expected 1, full suffix 0/2, partial complete, complete with false bookkeeping and empty/full suffix 0. `ValidInventoryPrefix` accepts empty/partial/full and complete/incomplete/failed combinations allowed by the contract. |
| Structural precedence and unreachable states | `StructuralAndSemanticProblems` prevents semantic evaluation after bad types. Missing/null semantic record fields, non-object entries and invalid suffix kinds cannot reach semantic checks after successful strict parsing. Inventory longer than the planned list cannot reach suffix checks because check 5 rejects it. List lengths cannot be negative; NumberStyles.None cannot parse a negative document ordinal; max Int64 has no representable Int64 successor, so overflow is exercised as JSON text and checked arithmetic. |
| Shared materialization limit | `SharedEntryBudgetBoundary` succeeds at pre-consumed 9996 plus four entries and throws at 9997 plus four, while JSON preflight retains its 10000 limit. |
| Planned-list independent limit | `PlannedArrayLimitBoundary` tests limits 1/2/3 for two planned entries with empty inventory; zero entries accept zero limit. Existing inventory-cap tests remain unchanged. |
| Ordinary-manifest compatibility | Existing StrictParserTests parse-only assertions are unchanged; the new golden null assertion does not alter the artifacts. |
| C5 target/part/shard consistency | The Python suite adds the killed singleton target to synthetic aggregate populations and preserves failure assertions, including shifted below/at/above expected-file projection counts. YAML comparison preserves all existing matrix rows, trigger and permissions. |


## Review record

The first comprehensive cold review was clean on staged tree `ee3d19a9a7a05d38242347f01d48c36e88c58dc7`, published as `e61bef91a2f04fa570ce75ab3a98263385f61242`. Its complete report is in the [PR 380 self-review record](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380). Cumulative comprehensive/focused counts before the merge correction: 1/0.

Hosted [code review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380#issuecomment-6636014142) and both [automatic security review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380#issuecomment-6636049285) and [requested security review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380#issuecomment-6636057789) completed on `e61bef91a2` without findings. All reports completed before merge edits began. These reviews do not cover the later merged head.

## Compatibility and continuation

Ordinary manifests retain parse-only treatment. Registration intake does not authorize second-Job completion or publication acceptance. Two-instance verify and binding belong to issue 376, judge to issue 377, per-repeat judgement to issue 373 and the late-write scan to issue 365. Post-merge C5 acceptance must measure main containing this target with `measurement_complete: true` and `break_passed: true`; no mutation success is claimed here.

## Merge compatibility with the batching target

Main advanced to `aca33589a3c69056dcbad3677698a53da7487bf8` through [PR 379](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/379), adding batching evidence and its mutation target. The merge combines that main revision with the reviewed B10 commit `e61bef91a2f04fa570ce75ab3a98263385f61242`. Incoming main changes retain their own authority; this correction preserves both targets and changes no B10 intake predicate or scientific rule.

The workflow matrix, Stryker configuration and target/part/shard maps retain main's `batching` entry before the appended `b10-restart-manifest` entry. They now contain nine targets and 20 shards. Literal aggregate fixtures include both singleton populations; their expected scores are 11/15, 8/15, 12/15, 9/60 and 15/18. Per-target status totals and projection boundaries move with the added target, while failure assertions, scoring thresholds and survivor-sampling bounds stay in force. Current-method documentation includes both additions; the earlier verification and calculation above remain evidence for the initial B10 revision only.

| Merge verification command | Result |
| --- | --- |
| `./scripts/verify-dotnet.sh` | Passed on the combined source: 7404 tests, including 5331 verifier tests, with no failures or skips; locked restore, Release build/analyzers and warning-as-error negative control passed. Log: `/tmp/issue375-merge-verify.log`; checked reports: `/tmp/parchwell-dotnet-verification.rzMJAUqV`. |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p test_c5_workflow.py` | Passed: 78 tests. |
| `actionlint .github/workflows/verifier-c5-mutation.yml` | Passed. |
| `python3 /tmp/issue375-merge-calculations.py` | Passed: nine targets, 20 shards, both singleton assignments and all listed aggregate expectations. The YAML comparison with pinned main permits only the added B10 row. |
| `npm run test:docs` | Passed: 28 tests, no failures or skips. |
| `npm run check:docs` | Passed: eight advisory warnings, all outside the PR diff against the new main. |
| `git diff --check` and `git diff --cached --check` | Passed after conflict resolution. |

The four conflict sites were the known overlapping matrix, configuration, target-map and test changes. No new implementation failure cause has occurred during resolution; the cumulative failure-cause count remains two of 12. The next review is the second comprehensive cold review of the full PR diff against `aca33589a3c69056dcbad3677698a53da7487bf8`, with the comparison against `e61bef91a2f04fa570ce75ab3a98263385f61242` supplied as context. It and final-head CI/hosted review remain pending; no additional completed cold-review round is claimed here. Post-merge C5 acceptance remains pending.

The merge calculation uses this authored script, retained here for reproducibility:

```python
import ast, copy, json, subprocess, sys
from collections import Counter
from pathlib import Path
import yaml
sys.path.insert(0,'tests/Parchwell.Verifier.Tests/Mutation')
import c5_workflow as workflow
assert len(workflow.TARGETS)==9
assert sum(count for _,count in workflow.TARGETS.values())==20
assert workflow.TARGETS['batching']==('VerifyRun/LedgerBatch/BatchingEvidenceRecorder.cs',1)
assert workflow.TARGETS['b10-restart-manifest']==('VerifyRun/Input/RestartManifestReader.cs',1)
print('target_count=9 shard_count=20 new_target_count=1')
p='.github/workflows/verifier-c5-mutation.yml'
a=yaml.safe_load(subprocess.check_output(['git','show','aca33589a3c69056dcbad3677698a53da7487bf8:'+p],text=True));b=yaml.safe_load(Path(p).read_text())
assert a[True]==b[True]=={'workflow_dispatch':None}
assert a['permissions']==b['permissions']=={'contents':'read'}
old=a['jobs']['mutation']['strategy']['matrix']['include'];new=b['jobs']['mutation']['strategy']['matrix']['include']
assert old==new[:-1]
assert new[-1]=={'shard':'b10-restart-manifest','target':'VerifyRun/Input/RestartManifestReader.cs'}
for job in b['jobs'].values():assert "github.ref == 'refs/heads/main'" in job['if']
b['jobs']['mutation']['strategy']['matrix']['include'].pop();assert a==b
print('YAML unchanged apart from appended matrix row; dispatch only; both jobs main-only')
source=ast.parse(Path('tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py').read_text())
expected={
 'test_combined_threshold_keeps_below_threshold_shards':(11,15),
 'test_complete_below_threshold_stays_complete':(8,15),
 'test_timeout_and_no_coverage_score_populations':(12,15),
 'test_survivor_sampling_does_not_change_raw_score':(9,60),
 'test_span_shards_combine_into_one_target_population':(15,18),
}
for node in ast.walk(source):
 if isinstance(node,ast.FunctionDef) and node.name in expected:
  assignment=node.body[0]
  value=assignment.value
  expr=value.args[1] if isinstance(value,ast.Call) else value
  population=eval(compile(ast.Expression(expr),'<literal synthetic populations>','eval'),{'__builtins__':{}})
  assert len(population)==9
  counts=Counter(status for rows in population for status in rows)
  score=(counts['Killed']+counts['Timeout'],sum(counts[x] for x in ['Killed','Timeout','Survived','NoCoverage']))
  assert score==expected[node.name]
  print(node.name,score,dict(counts))
assert 1844674407370955161*5+2==9223372036854775807
print('checked-slot boundary:',[1844674407370955161*5+s for s in (1,2,3)])
```

### Focused prose correction review input

The second comprehensive review of tree `870244d481c944a07836dfe00630779de44d0113` completed with no P0 or P1. It reported one P2: the mutation helper docstring still said eight populations after the merge added a ninth target. The correction changes that word to nine; executable code and test expectations are unchanged. The complete report and disposition are retained in the [PR self-review record](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380).

The next review is the single focused P2-correction review under pull-requests.md rule 8. Its comparison revision is that reviewed tree, its scope is this correction and this receipt addition, and it reports only P0 or P1. The full PR diff against `aca33589a3c69056dcbad3677698a53da7487bf8` remains context. Comprehensive rounds used: two; focused rounds before this review: zero.

### Claude review test-isolation corrections

The user requested fixes for the two inline Claude review comments on head `d8f80dfb7f8801e73804e4a34c6b03fa0c4814ad`: [tenant isolation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380#discussion_r4470736786) and [document isolation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/380#discussion_r4470737723). Both concern the approved plan's existing requirement that unrelated checks cannot mask the predicate under test. No production predicate or existing assertion changes.

`Given_InvalidTenantWithMatchingKey_When_Read_Then_RejectsTenant` adds empty and blank tenants with keys `-d0` and ` -d0`. `Given_InvalidDocumentWithMatchingKey_When_Read_Then_RejectsDocument` adds the empty, noncanonical and overflowing document cases with keys computed as `t-` plus the supplied document. `SetLogical` copies each identity field and its matching key into both planned and inventory entries. Every case requires the sole `invalid_restart_planned_entry` problem at `restart.planned[0]`; existing cases remain unchanged. The valid document/sequence cases and interior-space tenant case remain the acceptance counterparts.

This supplements the predicate-map rows for check 4 tenant text and document identity: a key mismatch cannot reject these new fixtures. A control character in a tenant necessarily appears in its matching key, so the tenant and key no-control predicates cannot be isolated from one another with a consistent key; the existing control-character rejection cases remain intact. The new empty/blank tenants do isolate their nonblank requirement because their matching keys are nonblank and contain no control character.

Verification of this test-only correction: `dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --no-restore -c Release` passed with zero warnings/errors. `dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 -- --filter-class '*RestartManifestReaderTests'` passed all 158 tests, with no failures or skips. Full final-head CI remains required after pushing. This does not claim a mutation campaign.

Review input: focused correction review against `d8f80dfb7f8801e73804e4a34c6b03fa0c4814ad`, covering these two test additions, their parser/fixture context and this receipt addition. Full PR diff is context only. Prior cumulative cold-review usage is two comprehensive and one focused round; this is the second and final focused round under pull-requests.md rule 8. No comprehensive allowance is reset. Final-head hosted and Claude review gates remain in force.
