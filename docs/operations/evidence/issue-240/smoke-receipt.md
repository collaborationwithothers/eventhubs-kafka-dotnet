# Probe batch 2 smoke implementation receipt

This change builds the real smoke prerequisite for partition selection, null-value transport and permission scope. It preserves the existing batch and prevents scientific batch dispatch or verdict publication through the new path. Live acceptance remains a separate main-only run after protected merge.

## Authority and source

- Source main: `dcf10d9bd0be311dedf8b36c62e542bb7854f5c2`.
- [Direct build instruction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494085278).
- [Smoke plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494160519), updated 2026-10-29T10:25:43Z.
- [Independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494293256): Blocking findings: none.
- [Full technical plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145), updated 2026-10-29T08:14:11Z, supplies the inherited wire/matrix/runtime contract; smoke-only deltas take precedence.

The build instruction authorizes this prerequisite without waiting for a further drafting answer. It does not authorize an agent to apply ready-for-agent, merge protected paths, run a branch workflow, or publish a scientific result.

## Implementation and source verification

The worker records bounded raw observations and final cleanup receipts. The verifier owns evidence, scope, terminal precedence and positive-control judgement. Workflow transport checks owner/source/digest/hash bindings and retains the verifier's smoke-only decision. Invalid V2 selectors are rejected by both batch admission and the stop-job routing before Azure access; every admitted smoke retains cleanup.

The native API, serializer/null distinction, partition functions, OAuth polling and statistics units were checked against the pinned Confluent.Kafka/librdkafka 2.16.0 sources linked in the evidence contract and both independent plan checks. Current Microsoft source revisions for configuration, compaction and role scope are linked there. These sources establish API behavior, not live Event Hubs outcomes.

The Terraform tests compare the entire existing assignment map with and without the two probe namespace grants. The actual managed map cannot establish absent inherited/group access; the private reviewed confirmation remains a deployment prerequisite. Blueprint ADR-003 owns the agreed exception; section 9 cites it instead of retaining a contradictory absolute.

Existing test assertions are retained. The generic inventory-acceptance fixture now supplies the valid F06 declaration for that existing case. The override metadata inventory adds the exact partitioner entry and retains all previous entries. The first full verification caught the architecture test's stale scenario inventory; its exact collection now includes all three added scenarios and retains every previous entry. New tests cover malformed/incomplete dependencies, terminal precedence, actual null versus empty, postterminal evidence, cleanup and positive controls.

## Validation

Pinned tools used: .NET SDK 10.0.402, Terraform 1.16.6, Helm 4.3.1, tflint 0.65.0 with azurerm plugin 0.32.0, kubeconform 0.8.0, actionlint 1.7.12, shellcheck 0.11.0 and PyYAML 6.0.3. The SDK archive matched official SHA-512 metadata; the repository installer verified the other pinned tools. Tools were installed outside the repository. No Azure credentials or apply/destroy commands were used in this workspace.

Completed checks on the working change:

- `./scripts/verify-dotnet.sh` passed: locked restore, Release solution build, analyzers, report validation, Kafka XML inventory and warning-as-error controls. Per-project passing counts were architecture 12, enrichment 55, Kafka 405, LoadGen 603, scenarios 239 and verifier 3609. The first acceptance attempt found the stale architecture inventory described above; the second passed.
- Scenario tests: 239 passed; Kafka metadata/native construction tests: 67 passed. Both Release project builds had zero warnings/errors.
- V2 actor tests: 47 passed, including real native unassigned OAuth polling with a synthetic token and local Kafka null/empty/location component tests. This proves local harness behavior only.
- Lifecycle Python discovery: 144 tests passed.
- Phase Python discovery: 99 tests passed, including actual runtime-to-Helm rendering of all 49 stages, rejected-input routing and declared admission counts. A launcher invocation without the pinned-tool PATH failed to find Helm; rerunning with the required environment passed.
- `check-offline.sh` passed, including shellcheck, actionlint, workflow mirror equality and legacy/new chart suites.
- Chart suites: existing 25 positive/32 negative/4 environment regressions and 66 positive/14 negative single-action cases passed; V2 99 positive/117 negative cases passed with kubeconform.
- Terraform fmt/init/validate passed; 12 mock tests passed. Tflint initialization/check passed. The pinned AVM emits existing namespace_name deprecation warnings; no module/provider version changed.
- Fresh exports from that full standard .NET verification passed `check_probe_v2_producers.py`: actual worker stdout and actual verifier candidate/final-receipt bytes passed Python transport, including `smoke_valid=true`. No generated export is committed.
- Documentation scan passed with eight pre-existing advisory warnings outside changed documentation. External URLs are not checked by that scanner.

`python3 infra/scripts/lifecycle/command.py 900 -- bash charts/parchwell/tests/check-loadgen-host.sh` passed against the published hosts: legacy environments and rejection regressions, plus all 49 V2 rendered environments and 10 V2 rejected-input regressions. No live result is claimed.

## Cold-review corrections

The first cold review found two blocking issues: exceeded capture bounds were classified as malformed evidence, and several new verifier boundaries lacked below/at/above and empty-input tests. The correction distinguishes `incomplete/v2_capture_limit` from malformed `invalid/v2_wire` evidence across records, errors, statistics, diagnostics, settings and payloads. It also retains limit diagnostics for scope and previous-candidate reads. Independent identity mismatches and malformed location fields remain invalid. The nine-error test had asserted the wrong classification; its expectation now requires incomplete evidence and the exact capture-limit reason. Rejected evidence is not promoted to complete.

After those corrections, the verifier test-project Debug build passed with zero warnings/errors, all 401 focused V2 tests passed, and the complete SingleAction regression passed 1088 tests with none failed or skipped. The regression exported a fresh actual judge candidate and final receipt; `check_probe_v2_producers.py` passed using that export and the unchanged actor export from the full solution verification. The full solution check above predates these verifier-only corrections; these focused and regression checks cover the changed code. Commands after loading the pinned tool environment:

```sh
dotnet build tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --no-restore --nologo -v minimal -m:1 -p:UseSharedCompilation=false
dotnet tests/Parchwell.Verifier.Tests/bin/Debug/net10.0/Parchwell.Verifier.Tests.dll -class '*ProbeV2*' -noLogo
PARCHWELL_V2_CONTRACT_EXPORT="$fresh_judge_export" dotnet tests/Parchwell.Verifier.Tests/bin/Debug/net10.0/Parchwell.Verifier.Tests.dll -namespace 'Parchwell.Verifier.Tests.SingleAction' -noLogo
```

The second cold review found that a missing envelope suppressed identity mismatches in remaining evidence, and that a nonterminal Persisted delivery incorrectly required readback. The verifier now checks every available identity and pairwise pod binding before applying completion gaps. Helper admission requires a terminal source with its request; late delivery after an earlier terminal error remains readback and security evidence. Added regression cases cover missing-file combinations, partial identity mismatches and absent-terminal admission. The same verifier build and test commands above passed with zero build warnings/errors, 463 focused V2 tests and 1150 SingleAction tests, none failed or skipped. A fresh actual judge export again passed transport and receipt-hash validation with the unchanged actor export.

## Repeating producer/transport integration

Set `PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY` and `PARCHWELL_V2_CONTRACT_EXPORT` to separate fresh temporary directories before running the current .NET test suites. The actor lifecycle test writes actor.stdout, registration.json, capture.json and pod.json from actual worker execution; the verifier lifecycle test writes candidate.json and judge-completion.json from the actual handler/host. Then run:

```sh
python3 infra/scripts/phase/tests/check_probe_v2_producers.py "$PARCHWELL_PROBE_V2_FIXTURE_DIRECTORY" "$PARCHWELL_V2_CONTRACT_EXPORT"
```

The command requires both exports, preserves raw bytes, checks coordinate and hash bindings and requires a smoke-valid judge export. Fresh directories prevent reuse of prior-build fixtures. The final whole-solution verification uses the repository's standard report-producing command with these variables set.

## Review and live prerequisites

The cold review and hosted head review are pending. The final PR description records the reviewed head, CI run and last cold-review result. This receipt describes the source and work checked; it does not attempt to name its own containing commit.

Hari merges the protected workflow/identity/verifier/design change. The existing environment gate and reviewed private effective-scope input remain prerequisites for the authorized main-only smoke dispatch. Successful smoke requires complete evidence and teardown/cluster stop. Full scientific implementation is refreshed against the merged prerequisite afterward; no expected.yaml or results file is part of this change.
