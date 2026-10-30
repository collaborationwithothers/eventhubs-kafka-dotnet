# Live probe batch 2 implementation receipt

This implements scientific assessment and bounded permission evidence for the existing partition, null-value and permission-scope probes. It reuses the completed smoke and publishes no live result. Protected merge, hosted mutation acceptance and scientific dispatch remain separate gates.

## Execution card

- Deliverable: the single PR named Live probe batch 2, implementing the exact remaining changes and access-check sections of the [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145), updated_at 2026-10-30T14:53:01Z.
- Authorization: [Hari's revision-specific approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6525928916), following the [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6525858845). Recognized earlier decisions are listed in the plan's Selection and authority section, including the workflow-check choice and scoped Ubuntu exception.
- Source: main c4a1724f9165397dd04f4e8df22acd9c8498ebd8; branch codex/240-live-probe-batch-2.
- Mandatory gates: all plan acceptance commands, predicate coverage, cold review under AGENTS.md, required final-head CI and hosted Codex review; Hari merges protected paths. Mutation acceptance after merge requires its separately authorized hosted campaign. No Azure credential, permission grant, secret replacement or dispatch is authorized here.
- Earliest publication: tested implementation after applicable cold coverage; independent CI and hosted review may run together. No scientific results publication in this PR.
- Review budget: AGENTS.md Before opening a pull request rule 8 owns the cumulative two comprehensive and two focused passes, including P2 correction rules. Exhaustion or unsafe focus saves unpublished work to the backup branch and hands off to Hari. The original command-attempt cap and exhaustion are preserved below. The [resume decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6526358036) authorizes necessary verification reruns to finish; the primary session owns the shared command ledger. Review budgets are unchanged.

## Verification preparation

The plan's Enforced checks and acceptance fixtures and Required regression and actual-producer coverage sections define the required predicates and exact commands. The implementation map below will identify concrete tests before the first cold review. New Python collector bytes must pass the real .NET bounded reader; real .NET actor/smoke/batch bytes must pass Python transport. Synthetic fixtures establish harness behavior, never live Azure availability.

## Resumed implementation and verification

Hari's [resume decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6526358036) resolves the prior verification-exhaustion stop and authorizes necessary reruns to finish the approved implementation. It does not authorize Azure reads, new grants, a dispatch, mutation campaign or scientific publication. The historical attempts remain below.

The compact-entry count now measures serialized compact JSON. A regression preserves acceptance despite wire whitespace. Isolated cross-account group-union fixtures cover 63, 64 and 65 groups with unrelated per-account bounds valid. The final collector source regenerates its actual producer bytes during the full phase suite.

Resumed work starts from backup commit 34568450786bc5c70fcabc41488ec24185d1c982. The first comprehensive review covered c5372b0aa05b4fe5fbb9893ac84adcbc436f9c3b. Current review mode: consolidated comprehensive re-review of the full authored diff against main c4a1724f9165397dd04f4e8df22acd9c8498ebd8; the frozen input commit is supplied with that diff. Cumulative completed comprehensive/focused counts before this re-review: 1/0. Hosted review requests before publication: 0.

### Acceptance ledger

Commands run with SDK10.0.402 on PATH and PARCHWELL_TOOLS_DIRECTORY selecting the committed installer-pinned tools. Temporary export paths hold actual serialized producer output and remain outside the repository.

```sh
PARCHWELL_V2_ACCESS_CONTRACT_EXPORT=/tmp/access240-export python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_probe_v2_access.py'
PARCHWELL_V2_ACCESS_CONTRACT_EXPORT=/tmp/access240-export python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'
PARCHWELL_V2_ACCESS_CONTRACT_IMPORT=/tmp/access240-export PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY=/tmp/probe240-export/actor PARCHWELL_V2_CONTRACT_EXPORT=/tmp/probe240-export/smoke-judge PARCHWELL_V2_BATCH_CONTRACT_EXPORT=/tmp/probe240-export/batch-judge ./scripts/verify-dotnet.sh
python3 infra/scripts/phase/tests/check_probe_v2_producers.py /tmp/probe240-export/actor /tmp/probe240-export/smoke-judge --batch-judge-export /tmp/probe240-export/batch-judge
python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'
python3 infra/scripts/lifecycle/command.py 600 -- bash infra/scripts/lifecycle/check-offline.sh
python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh
npm run check:docs
cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt
git diff --check
```

- Collector targeted command: latest run passed 31 tests, with actual serialized exports regenerated after the tenant-ID correction. The latest full phase suite passed all 156 tests, including collector, strict cleanup admission and partial-selection reporting.
- Lifecycle acceptance: earlier 183 tests passed; unaffected by resumed .NET corrections.
- Offline acceptance: earlier passed for the current workflow/chart/phase source; unaffected by resumed .NET corrections.
- Scoped verifier build: passed, zero warnings/errors. After the scientific correction, all 1670 SingleAction tests passed, including actual Python access import and all 24 added positive-control cases.
- Full .NET acceptance after the scientific correction passed on the correction working tree based on c5372b0aa05b4fe5fbb9893ac84adcbc436f9c3b: architecture 12, enrichment 56, Kafka 460, loadgen 674, scenarios 239 and verifier 4423 tests, total 5864 computed from the checked reports. Locked restore/build, analyzers, XML inventory, reports and warning-as-error controls passed. An earlier scoped unfiltered verifier run timed out while pulling the pinned Kafka image; the successful full runs used the cached image.
- Rendered host acceptance: first resumed run passed the existing 49 V2 smoke cases; the test was extended to include scientific mode through the real host validator. The necessary rerun passed 98 V2 smoke/batch environments and 14 rejected-input cases, retaining all existing assertions.
- Actual .NET actor/smoke/batch host bytes passed Python transport with probe_v2_actual_producer_transport_valid=true after the correction. Actual Python collector bytes passed the .NET bounded reader and access judge in the full suite. No hand-invented future payload substitutes for these producer exports.
- Documentation scans completed with eight existing warnings in unchanged files, outside this PR's scope. The required pre-review scan includes this tracked receipt. Workflow mirror equality and whitespace checks are repeated before the frozen review checkpoint.

### Review preparation

The maps below identify changed predicates and their isolated boundary/empty/precedence tests before cold review. The cross-account union fixtures now add the missing independent 63/64/65 coverage. charts/parchwell/tests/test_loadgen_host.py retains the 49-case smoke assertion, adds all 49 batch environments, and isolates missing/empty/unknown/wrong-case RunKind rejection through the published verifier host. Valid compact entries cannot reach the arbitrary 96-byte per-string or 1024-byte per-entry boundary because the supported fixed aliases/enums are shorter; the outer projection cap makes the largest collection caps unreachable with other fields valid. These are explicitly unreachable cases, not passing masked tests. Whitespace in an otherwise valid entry separately tests compact-versus-raw accounting.

## Historical verification and stop record

Implementation is saved as unreviewed work in progress. Verification stopped under the approved command-attempt exhaustion action; no PR or scientific publication is eligible.

### Stop and process breach

The exact listed command `git diff --check` was independently invoked by the primary session, the verifier agent and its scientific-test helper. All three invocations failed on the same newly introduced trailing blank line in single-action-contract.md. This exceeds the plan's shared two-attempt allowance. The blank line was removed, but there is no authorized passing final check; the primary session did not make a further invocation before its backup handoff. The primary session failed to allocate this command centrally before delegation. A later phase-agent report also records a passing diff check; the overlapping prior ledger is preserved rather than treated as a new allowance. This is a process breach, not a scientific result or a review finding.

Revision 7, Finite work and later execution gates, states: "Implementation verification: at most two attempts per listed command including diagnosis/retry; after exhaustion record failure and hand off without publication." AGENTS.md Before opening a pull request rule 12 allows the backup push without cold review. The protected files and expected specifications require Hari's decision on continuation under rule 11. No allowance resets on another session.

### Results available at stop

- Installed SDK 10.0.402 and the committed installer-pinned offline tools; npm dependencies installed with npm ci. No repository dependency/version change.
- Exact collector command: attempt 1 failed its new group-budget test; attempt 2 passed 18 tests and exported actual producer bytes. Later focused boundary tests: first failed a newly written synthetic harness; corrected harness and second focused run passed 11 tests. Later collector corrections remain unverified by the full suite.
- Lifecycle acceptance command: passed 183 tests. These use stub commands, not Azure credentials.
- Offline acceptance command: passed, including shellcheck, actionlint, workflow mirrors, Kubernetes schemas and chart suites. V2 chart suite passed 148 positive and 117 negative cases.
- .NET full acceptance attempt 1: cancelled with SIGTERM (exit 143) at the governed stop while Kafka tests were running; the whole acceptance is incomplete. Locked restore, solution build, Kafka XML inventory, architecture tests (12) and enrichment tests (56) passed before stop was recognized. This is not a passing whole-suite claim.
- Documentation command: first failed before dependencies were installed; second completed with eight warnings in unchanged files. Those existing warnings are left unchanged. The new untracked receipt was not included in that tracked-file scan; no later documentation scan is claimed.
- Scoped phase transport suite passed 34 tests; workflow suite passed 13 tests. Additional selection, freshness and preparation tests passed as described in the predicate map. No full phase acceptance, chart/host acceptance or final actual-producer transport acceptance command was started by the primary session.
- At the prior stop, no cold review, hosted review, final-head CI, mutation campaign, Azure read or workflow dispatch had occurred; review counts were 0/0/0.

### Work outstanding at the prior stop

At that prior stop, compact-entry accounting, cross-account union coverage and final collector verification were recorded as outstanding. The resumed verification section records their correction and test results. The complete implementation/specification comparison has not been performed, so this list is not a cold-review clearance.

After a decision on the exhausted verification allowance, finish the specified implementation and predicate coverage, regenerate actual producer exports, run all outstanding acceptance gates, then cold review before a non-backup push/PR. Preserve the command/review ledger across sessions. Protected merge, postmerge mutation acceptance and any live dispatch still need their existing authorization.

### Source checks

The session fetched the 18 official source URLs in the approved plan on 2026-10-30 and checked the relevant passages: pinned serializer and CRC32/Murmur2 selection; Microsoft null tombstones and role scopes; Graph transitive membership permission/paging; ARM principal-filter scope, role definitions and authorization operations; supported built-in role IDs/data operations; deny, propagation and cache limits. A scripted replay of the pinned hash algorithms produced CRC32 2046850398 and Murmur2 2245682434, with the planned partitions 30 and 2. These are computed expectations, not Event Hubs measurements. Live reader availability and actual service outcomes remain unknown.

Authority was refreshed before the backup push: issue body, all comment IDs/revisions and all six external linked comment revisions. Revision 7 and its specific approval remained current. The source branch still starts from c4a1724f9165397dd04f4e8df22acd9c8498ebd8.

Models: Codex implementation sessions; exact model names and effort settings were not exposed. At the prior stop no reviewer had started; the current cold reviews explicitly request high effort. Exact implementation/reviewer model names were not exposed.


## Scientific predicate map

- Partition decision boundaries: expected C0=30/C1=2 confirmed; C1=1 and 3 contradicted; observed metadata counts 31/33 invalid. Existing matrix tests provide 32 at-boundary smoke coverage and the new confirmed batch test includes that input.
- Wrong partition readback value: exact d3Jvbmc= bytes at C0 partitions30/29 and C1 partitions2/1/3, affecting one/all five rows. Assert per-repeat v2_location/incomplete, aggregate incomplete, null determinism/outcome, final literal undecided/no-verdict/incomplete_configuration and no smoke_controls_failed.
- Transport: present null/empty/nonempty independent bytes, C0 contrary null/nonempty failed-control, absent record incomplete, missing value property malformed wire, complete action errors untestable, construction precedence, failed seed/dependency skip incomplete.
- Permission science: all positive controls with negative native errors and no_outcome observations produce undocumented with recorded_only negatives; exact narrow target produce/read success remains a security finding before statistics/control gaps; complete positive send failure is untestable.
- Aggregate/evidence: four/five/six ordinals; empty/missing configurations; distinct ordinal offsets/markers remain equal; one changed partition produces unequal class; invalid repeat before incomplete repeat; missing/hash-mismatched/failed/source-forged receipts or wrong mode rejected; exact final receipt permits transport.
- Nonfinal candidates have null assessments; final-only proposals tested. Handler-input override tests belong to parent verifier work.


## Access judge predicate map

- Missing audit and null scope -> missing audit test; parent integration covers scope problems and security-before-gap precedence.
- Exact run/source/namespace/principals/targets, envelope/projection hash/binding -> WrongBinding theory covers each independently; RequiredProjectionMember and MalformedOrContradictoryProjection cover missing wire fields, unknown keys and closed aliases.
- Ordered nonnegative times and inclusive 600000ms duration -> DurationBoundary -1,0,1,599999,600000,600001; StartTimestampBoundary -1,0,1; envelope/projection equality remains independently exercised by wrong wire tests.
- Projection decoded byte limit -> valid complete padded payloads at32767,32768,32769. Oversize is rejected before typed materialization.
- Status literals complete/incomplete/mismatch -> CompleteMeasuredSnapshot, TypedIncompleteAudit, AdditionalSupportedGrant; unknown status/empty/wrong-case, unknown/duplicate reasons and contradictory completed coverage -> explicit wire tests.
- Requests measured counter lower/upper bounds and retained hashes -> -1,0,1,5,6,7,255,256,257 with six hashes otherwise valid. Boolean request and incorrect API version explicit fixtures.
- Reasons22,23,24 -> sorted known reasons at22/23 accepted as typed gaps (both capture/evidence classes),24 rejected before materialization. Fixed reasons map each cap to capture-limit and membership-unavailable to access-evidence.
- Membership/assignment page arrays ->7,8,9; groups, directory roles and definitions ->63,64,65 with required related collections/counts valid. Group fixtures also exercise assignment collection size64,65,66. Cross-account group union is independently tested at 63/64/65 by Given_GroupUnionAcrossBothAccounts_When_Checked_Then_UnionCapIsIndependentOfPerAccountCaps.
- Supported table exact vs extra -> complete exact and extra supported grant mismatched status. Unknown subject, malformed assignment alias, duplicate tuple and missing entries -> wire failures.
- Empty coverage/page/completion state -> explicit empty membership, false completion, absent required fields, empty entries; missing membership-complete and collection-complete fields are rejected by required constructor wire parameters.
- Entry string95/96/97 and compact byte1023/1024/1025 are unreachable as valid supported entries: fields are fixed aliases/enums with exact lengths strictly below those string/entry bounds. Structural guards precede schema materialization and closed alias checks; padding a fixed alias to those sizes would introduce an unrelated invalid field. These are recorded unreachable rather than claiming isolated valid boundary fixtures. Entries8191/8192/8193, unsupported4095/4096/4097 and assignment4095/4096/4097 cannot fit the enclosing32768-byte projection with all mandatory valid fields; outer byte guard must fire first. Private file/raw response/query/admission limits are collector/phase tests, not this judge's runtime responsibilities.


## Collector predicate map

- Binding ID/run/source and cross-source identity: test_binding_input_rejected_before_query; production main validates deployed-vs-cluster IDs. Exact fixture IDs match ProbeV2Fixture and are consumed through the real .NET reader using exported bytes.
- Authorized empty collections, nested group closure and directory-role separation: test_actual_producer_export, test_nested_group_memberships_and_directory_roles.
- Complete pagination, final empty page, link scheme/host/path/userinfo/fragment/repetition and seven/eight/nine pages: test_page_links_and_final_empty_page, test_invalid_and_repeated_continuations, test_multipage_collection_boundaries.
- Strict duplicate/nonfinite JSON and wrong collection shapes: test_unavailable_and_malformed_membership; missing/unknown object kinds and duplicate IDs: test_duplicate_objects_unknown_type_injection.
- Reader denial, unresolved definitions, no retries: test_read_denied_no_retries. Definition type/ID/operation/condition/delegation/NotDataActions and irrelevant storage behavior: test_unsupported_grants_and_definition_spoof, test_storage_irrelevant_and_notdata_not_global_deny.
- Parent namespace/RG/subscription/management-group and descendant consumer-group scopes: test_supported_extra_grant_and_broader_scopes. Other hubs ignored only by validated path; unknown path remains unsupported: test_unrelated_scope_and_unknown_scope.
- Extra supported grants retain evidence and mismatch, missing expected union mismatch: test_supported_extra_grant_and_broader_scopes; missing/bad assignment properties: test_conflicting_assignment_copies_and_missing_principal. Alias collisions fail closed: test_alias_collision_rejected.
- GET/page/group/definition/assignment workload below/at/above declared caps: test_budgets_requests_pages_groups_definitions_assignments, test_runtime_counts_below_at_above_policy, test_groups_below_at_above, test_multipage_collection_boundaries. Audit time just below/at/above 600 s: test_time_boundary.
- Both output stream limits, stdout 1023/1024/1025 bytes, file budget below/at/above and raw-byte budget preflight: test_both_streams_capped, test_stdout_byte_boundary, test_private_file_admission_boundary, test_file_and_byte_budgets_checked_before_launch.
- Timeout and real nested cancellation cleanup: test_deadline_nested_subprocess_cleanup, test_signal_propagates_to_nested_command_and_child_cleanup. Real failing stderr device: test_real_failing_stderr_sink_remains_private. Raw identifiers/argv/errors remain private: test_raw_identifiers_not_in_public_projection, test_actual_subprocess_output_stays_private.
- Projection overflow never truncates: test_projection_limit_gap_not_truncation. Normalizer-produced strings have closed enums, aliases and SHA lengths, so arbitrary 95/96/97 byte strings and 1023/1024/1025 byte entries are not reachable from valid raw input; the judge map records these fixed-format sizes as unreachable valid inputs; no isolated 95/96/97-string or 1023/1024/1025-compact-entry test is claimed. Large collection combinations exceeding the 32768-byte projection cap are gap-only and never materialized as complete; judge structural tests must isolate their own predicates per the approved plan.


## Phase predicate map

- All seven nonempty canonical subsets + all accepted; duplicate, reverse order, empty, whitespace, lowercase, mixed legacy, trailing separator rejected. Smoke partial/missing/unknown remains rejected.
- Selected role flag B08-only false, E01/F06/subset true; preparation checks five mode/selector cases and copies confirmation only in smoke.
- Closed assessment shape: exact final labels/allowed verdict and values; unknown keys/labels/verdict, wrong reason type, nonfinal and smoke assessment rejected. Root full producer acceptance checks serialized .NET batch bytes.
- Same-host permission clock: missing/mismatched boot/run/clock, boolean/string completion, bad hash and incomplete/mismatch blocked. Admission after actual run_stage render measured at 59999ms and 60000ms permits; 60001ms and negative blocks before apply. Expired first admission sets sticky block; consumed valid submission allows later stages without inventing continued permission stability.
- All seven subsets compute expected jobs/action slots/max client/admission counts from real stages; not-selected items produce no stages. F06 collector called exactly once only when selected. Incomplete/mismatch blocks all 21 F06 actor jobs while preserving diagnostic judges.
- Chart exact env/mount validator retains all 98 existing smoke cases; adds every batch stage. Extra private ConfigMap only full F06 judges.
- Existing smoke rejection expectations retained; old batch=all rejection changed to acceptance because plan explicitly adopts scientific batch mode. This is an intentional changed contract, requiring protected review as the approved plan provides.

Additional verification after integrated environment setup:
- PATH=/tmp/parchwell-tools240:$PATH python charts/parchwell/tests/test_probe_v2_chart.py: passed 148 positive and 117 negative cases; all existing smoke assertions retained plus batch cases.
- Actual batch phase-produced values rendered through real Helm: 49 stages, judge RunKind exact and actors receive no RunKind. Alongside invalid-selection preparation test, 2 tests passed.
- Batch transport/mode/receipt diagnostic-gap and exact artifact hash-reference tests: 2 passed. Generic transport tests bind bytes/hashes and do not duplicate scientific predicates.
- Workflow mirror cmp and git diff --check passed.
- New test_phase.py rejects invalid selection before private directory creation/Azure commands; preparation smoke-only secret/conditional-role regression passed.
- Safety-stop reasons from the collector and outer helper timeout status retain phase safety_stop and block subsequent admission.

## Consolidated corrections after comprehensive review 1

The first comprehensive report was complete for c5372b0aa05b4fe5fbb9893ac84adcbc436f9c3b against main c4a1724f9165397dd04f4e8df22acd9c8498ebd8. It covered all 33 touched files under repo-review, the approved Revision 7 and the recognized decisions. Its raw result was six P0/P1 findings: three protected expected-file additions retained under the approved-plan exception and three substantive P1 findings. It also reported one P2 coverage overstatement. No edits began until its complete report was available.

| Finding | Disposition and regression evidence |
| --- | --- |
| P0: scenarios/B08/expected.yaml, scenarios/E01/expected.yaml, scenarios/F06/expected.yaml | Retained because Revision 7 Exact remaining changes item 1 explicitly requires these assessment specifications. List these escalations in the PR under AGENTS.md rule 8 and leave merge to Hari. No result or scientific verdict is published. |
| P1: F06 successful outcome alone could mark a wrong-value positive control as holds | Fixed V2JudgeItem to require success and exact ValueMatch=true for every repeat in each of the eight positive configurations. Twenty-four added cases cover all-five wrong values (complete failed_control), one wrong among four exact values (unequal-class precedence), and missing records (incomplete precedence). Existing smoke and E01/B08 behavior is retained. Scoped SingleAction suite passed 1670 tests after a zero-warning build. |
| P1: ROLE_ID rejected documented tenant-level definition IDs | The collector now accepts and resolves `/providers/Microsoft.Authorization/roleDefinitions/<guid>`, in addition to the existing scope-prefixed form. test_tenant_level_builtin_definition_ids_are_resolved requires actual definition GETs, complete inventory, six requests and the exact four grant entries. test_tenant_definition_path_suffix_injection_is_rejected_before_get retains query/fragment/extra-path rejection. Official Get By ID and List For Scope sources already cited by the contract document this form. |
| P1: case-insensitive GitHub expression admitted rejected selectors to cleanup Azure access | Retained the exact approved job condition. prepare_stop now reuses the strict selector validator before creating private stop state, so the existing default-success login step is skipped on rejection; the final stop also requires successful preparation. This fixes the semantic requirement without a plan-expression amendment. Workflow tests model case-insensitive comparisons/contains; phase tests reject lowercase/mixed-case selectors, reversed selection, wrong-case mode/scenario and smoke ALL before state writes or commands, and admit every valid selection independently of workload failure. Source: https://docs.github.com/en/actions/reference/workflows-and-actions/expressions, read 2026-10-30. |
| P2: collector map claimed malicious fixed-format boundary tests that do not exist | Corrected the map to agree with the actual unreachable-case explanation; no 95/96/97-string or 1023/1024/1025-compact-entry fixtures are claimed. |

The author also found the new V2 not_selected string aliases were passed to the legacy dictionary formatter in Phase.report. The V2 report now retains item aliases without changing legacy configuration reporting; test_v2_subset_report_retains_unselected_item_aliases exercises the report and preserves the state. This completes the plan's partial-selection reporting requirement.

Correction verification ledger: the first phase run failed newly added stop-test assumptions about pre-existing empty fixture files and an incorrect fake-command member; the second failed a new tenant-ID test that expected lowercased rather than preserved URI spelling. Those fixture expectations were corrected without changing old assertions. The third run passed 155 tests. After adding the subset-report regression, the fourth full phase run passed 156 tests and regenerated actual collector exports. One scoped .NET build encountered MSB4166 worker termination; an isolated serial build with node reuse disabled passed, followed by all 1670 SingleAction tests. These necessary reruns use the recorded resume authorization.

Full .NET verification after the scientific correction passed (5864 tests); actual-producer transport passed with its expected marker. The final source is frozen only after the documentation and whitespace/mirror checks. The complete review reports are retained in the PR self-review record, with this table providing the first report's full finding dispositions. Cumulative comprehensive/focused counts are 1/0 completed; the next frozen input is the second and final comprehensive allowance. No hosted review has been requested yet.

The added scientific test members are Given_CompleteDeterministicWrongPositiveControlValue_When_FinalScopeJudged_Then_FailedControlIsUntestable, Given_MixedWrongAndExactPositiveControlValues_When_FinalScopeJudged_Then_UnequalClassesPrecedeFailedControl, and Given_MissingPositiveControlRecord_When_FinalScopeJudged_Then_IncompleteEvidencePrecedesFailedControl. Each has one literal case for every positive configuration; unrelated location, settings, seed, statistics and access evidence remain valid. The Phase.report correction is covered by test_v2_subset_report_retains_unselected_item_aliases; cleanup admission is covered by test_v2_stop_rejects_case_variants_and_invalid_selection_before_private_or_azure_work and test_v2_stop_accepts_each_admitted_selector_independently_of_workload_failure, with workflow step ordering/default-success checks.
