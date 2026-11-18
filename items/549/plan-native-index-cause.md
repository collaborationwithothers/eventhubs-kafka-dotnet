# native-index-cause contract-grade plan (issue 549)

Revision: R1. Base: `main` at `08c708aae6a8cc27c87110013540089bb5553a43`.

The 25 "native index unavailable" gaps per phase in the C03 smoke run are a consequence of the archive deadline, not of missing indexes: the archive spent its 600-second budget before it reached the native stage, so it never read any index. The fix for that cause is issue 548, which Hari already chose. This pull request makes the receipt name the real cause when an index was not read, pins the failure with a regression test, and states the evidence that closes issue 549.

## 1. Goal and authority

Goal: find why every unit reports `monitoring_incomplete` "native index unavailable", fix it, and show the fix with evidence (issue 549 body). Retrying, or loosening the archive's gap rule, is not a fix.

Authority, in creation order:
- Issue 549 body (updated 2026-11-18T08:25:04Z).
- [Decision 6973160092](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/549#issuecomment-6973160092): Claude Code implements; Codex reviews; the plan needs Hari's approval.

No item plan or parent plan exists for this issue. No item state file exists (`items/549/state.md` is absent on the base commit, which is the normal case before a first implementation merge).

Related, not authority here: issue 548 body ("Archive transfers objects one by one and overruns its deadline on a consumer phase") and its decision 6973159782; [issue 531 decision 6943512296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296) point 2, which accepted the deadline overrun risk by name and sends a `deadline` gap to Hari before the C03 batch; [smoke report 6973117302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/508#issuecomment-6973117302), check 8.

## 2. Diagnosis

Method: the diagnosing-bugs loop. Evidence was read by script from the run's artifacts `c03-C0-archive-44248514485-1`, `c03-C1-archive-44248514485-1`, `c03-C0-pods-44248514485-1` and `c03-C1-pods-44248514485-1`, and from the job log of job 134613218455.

### 2.1 Feedback loop

A scratch script outside the repository built the existing C03 phase fixture (`Phase('C03', 'C0')` in `infra/scripts/phase/tests/test_archive_consumer.py`), patched `time.monotonic` in `archive.py` and its `_common` module to a fake clock, and advanced the clock by a fixed cost on each storage CLI call. Results, run 2026-11-18 on the base commit:

| Cost per CLI call | Exit | CLI calls | Verified objects | Native index objects | Gaps |
|---|---|---|---|---|---|
| 0 s | 0 | 1181 | 216 of 216 | 25 `verified` | none |
| 0.7 s | 1 | 858 | 160 of 216 | 25 `failed`, gap code `deadline` | 62 `deadline` "global deadline", 25 `monitoring_incomplete` "native index unavailable" |

The 0.7 s row reproduces the smoke run's symptom exactly. The 0.7 s figure is an estimate (2026-11-18), not a measurement: it is about 600 s divided by the roughly 850 CLI calls that 141 verified objects need at about 6 calls each in `Transfer.copy` (`archive.py:261-300`).

### 2.2 Root cause (confirmed from the receipts and the code)

1. The C1 step "Archive phase evidence" ran 08:02:07 to 08:12:07 UTC and the C0 step 06:51:07 to 07:01:07 UTC, each exactly `ArchiveDeadlineSeconds` (600, `archive.py:8`). The C0 manifest verified 141 of 202 objects and the C1 manifest 128 of 202.
2. `execute` calls `collect_native_monitoring` after `collect_ordinary` (`archive.py:2213-2214`), so native indexes are the last objects requested.
3. In both manifests, every one of the 25 `native_monitoring_index` objects has `state: failed` and `gap_code: deadline`. The C0 manifest records a `deadline` "global deadline" gap for each of them; the C1 manifest records 18 of them, because the collector keeps at most 100 gaps and then records `diagnostics_truncated` "diagnostic count" (`archive.py:955-961`). `Transfer.az` raises the deadline gap before it starts any command (`archive.py:73-74`), so no index was read.
4. `collect_native_monitoring` then records `monitoring_incomplete` "native index unavailable" for each unit whenever `collector.source` returns `None` (`archive.py:2016-2020`), whatever the reason the object failed. That second gap names a cause, absence, that the evidence does not show.

### 2.3 Hypotheses tested

| Hypothesis | Prediction | Result |
|---|---|---|
| H1. The deadline ran out before the native stage | Every index object failed with `deadline`; removing the per-call cost makes all 25 verify | Confirmed (2.1, 2.2) |
| H2. The index is written somewhere else | The workers' configured index path differs from the archive's derived path | Falsified. In both phases, for all 25 units, `NativeMonitoring__RunId`, `Phase`, `JobName` and the pod UID from the pod spec in `pods.json` build exactly the 25 paths that phase's manifest requested |
| H3. Expected where it does not apply (a self-killed subject writes no index) | Only subject units would lack an index | Not applicable. `scenarios/C03/scenario.yaml` has `fault: live: {kind: none}`, all 52 pods in the two pod artifacts ended `Completed` with exit code 0, and the gaps cover loadgen, start-line and verify units too |
| H4. The index is never written | The object is `missing` (gap code `source_missing`) once the archive reaches it | Not testable from this run, because no index was read. See deployment assumption DA1 |

## 3. Scope

One pull request, short name `native-index-cause`.

Files allowed to change:
- `infra/scripts/phase/archive.py`, function `collect_native_monitoring` only.
- `infra/scripts/phase/tests/test_archive_consumer.py`, adding tests to `ConsumerArchiveTests`.
- `docs/operations/archive-smoke.md`, the native-monitoring paragraph (base line 103) only.
- `items/549/state.md`, created by the implementing session under docs/agents/factory.md "Item state file".

Paths that must not change: every other file, including `ArchiveDeadlineSeconds`, the transfer code, the gap codes, `collect_ordinary`, every C# project, the chart, the workflows, `scenarios/` and `results/`. The transfer speed is issue 548's scope and is not touched here.

`docs/` is a protected path, so this pull request waits for Hari's merge.

## 4. External contracts

- C1. Archive gap codes stay exactly the set in the [approved specification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/f96eab914d00c06484431ff67a1808fb331c7e13/items/494/plan-observability-evidence.md) (line 236). `boundary` is diagnostic text there; this plan adds one boundary form.
- C2. New unit-gap boundary form: `native index failed: <gap code>`, where `<gap code>` is the `gap_code` the collector recorded on that unit's `native_monitoring_index` object. Examples: `native index failed: deadline`, `native index failed: transport`, `native index failed: monitoring_incomplete`.
- C3. The existing boundary `native index unavailable` keeps its current meaning for an index that is absent (object state `missing`) and for an index that has no object because the object limit was reached.
- C4. The unit gap's `code` (`monitoring_incomplete`), `object_name` (null) and `expected_unit` (the job name) are unchanged. `archive_complete` and the completion marker follow the existing rule: any gap keeps the archive incomplete.

## 5. Requirements

- R1. WHEN `collect_native_monitoring` gets no index document for a started native-monitoring unit, AND the collector holds that unit's `native_monitoring_index` object with `state` `failed`, THEN it records one gap `{code: monitoring_incomplete, boundary: "native index failed: <gap_code>", object_name: null, expected_unit: <job>}` and no `native index unavailable` gap for that unit.
- R2. WHEN the index object's `state` is `missing`, OR the collector holds no object for that index, THEN it records `{code: monitoring_incomplete, boundary: "native index unavailable", object_name: null, expected_unit: <job>}`, as on the base commit.
- R3. WHEN the index document is read, THEN behaviour is unchanged, including the `diagnostics_truncated`, `native capture incomplete` and raw-chunk gaps.
- R4. Every object-level gap recorded on the base commit is still recorded with the same code, boundary and object name.
- R5. `docs/operations/archive-smoke.md`, in the native-monitoring paragraph, gains this sentence after "Missing, partial or mismatched monitoring evidence records a gap and prevents a completion marker without changing the scientific judgement.": "When the archive could not read an index, for example because its deadline had passed, the unit's gap boundary is `native index failed:` followed by the index object's gap code; `native index unavailable` means the index is absent."

## 6. Invariants and safety rules

- No gap is removed or downgraded; the number of unit gaps per affected unit stays one. The archive stays incomplete in every case where it is incomplete on the base commit.
- No scientific judgement, expected outcome, verdict or `results/` file changes.
- No Azure, identity, network or workflow change.

## 7. Test map

All tests go in `ConsumerArchiveTests` in `infra/scripts/phase/tests/test_archive_consumer.py` and use the existing `Phase` fixture (run `c03-42-1`, pod UID `uid`). The 25 jobs are `c03-42-1-<phase>-r<1..5>-<consumer-subject|consumer-witness|loadgen|start-line|verify>` with `<phase>` lower case.

- T1 (R1, C2, C4, root cause). `test_native_stage_after_deadline_names_the_deadline`, subtests C0 and C1. A fake clock starts at 1000.0 and is patched into `time.monotonic` of the archive module and of its `_common` module. The test wraps `collect_native_monitoring` so the clock moves to 1000.0 + `ArchiveDeadlineSeconds` + 1 just before the native stage runs. Expected: exit code 1; receipt `archive_complete` false and `completion` null; the 25 `native_monitoring_index` objects all have `state` `failed` and `gap_code` `deadline`; manifest gaps are exactly 50: 25 `{deadline, "global deadline", <index path>, null}` (the index path is `c03-42-1/<PHASE>/<job>/uid/native-monitoring/index.json`) and 25 `{monitoring_incomplete, "native index failed: deadline", null, <job>}`; no gap has boundary `native index unavailable`. On the base commit the same test fails: the 25 unit gaps read `native index unavailable` (observed with the scratch script, 2026-11-18).
- T2 (R1, transport). `test_native_index_transport_failure_names_transport`. The CLI returns `(1, b'', b'AuthorizationPermissionMismatch')` for `show` of `c03-42-1/C0/c03-42-1-c0-r2-consumer-witness/uid/native-monitoring/index.json` only. Expected: exit code 1; manifest gaps are exactly `{transport, properties, <that path>, null}` and `{monitoring_incomplete, "native index failed: transport", null, c03-42-1-c0-r2-consumer-witness}`.
- T3 (R1, contract). `test_native_index_contract_failure_names_its_code`. The source blob at `c03-42-1/C0/c03-42-1-c0-r3-verify/uid/native-monitoring/index.json` is replaced with `b'{}'`. Expected: exit code 1; that object has `state` `failed` and `gap_code` `monitoring_incomplete`; manifest gaps are exactly `{monitoring_incomplete, "native index contract", <that path>, null}` and `{monitoring_incomplete, "native index failed: monitoring_incomplete", null, c03-42-1-c0-r3-verify}`.
- T4 (R2, boundary of the rule). Existing `test_killed_subject_without_completion` (absent index, object state `missing`) keeps its exact expected set, including `('monitoring_incomplete', 'native index unavailable', None, job)`. Existing `test_b10_missing_first_child_native_index_prevents_completion` in `test_archive_inventory_integration.py` keeps its assertion. Neither file's existing assertions change.
- T5 (R3). Existing `test_complete_consumer_phase` stays green unchanged: zero gaps, exit code 0.

Standing fixture rule (plan-pr "Plan rules" 2, modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2): when an existing test's fixture is rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; or (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. This plan adds no input check, so no change under this rule is expected; an existing test whose expected boundary was `native index unavailable` for a `failed` index object would fall under (b).

## 8. Deployment assumptions

- DA1. Each started C03 worker (load generator, consumer subject, consumer witness, start-line and verify) writes its native index in the live deployment. Evidence: none direct, because the smoke run read no index. Indirect: the 10 load-generator logs in the two pod artifacts contain no `native_monitoring_completion_failed` line, which `src/Parchwell.LoadGen/Program.cs:200` writes when index completion throws. The logs were taken by `kubectl logs` after each pod completed, so they cover the whole process, which ran under load. The consumer host (`src/Parchwell.Enrichment/Program.cs:222-227`) and the verifier host (`src/Parchwell.Verifier/Program.cs:529-535`) catch a completion failure without writing any line, so the logs give no evidence for 20 of the 25 units. No offline test in this plan covers DA1. It is listed under "Open questions for Hari" until he accepts the risk by name. It does not affect this pull request's tests; it affects only the closing evidence in section 11.

## 9. Acceptance commands

- `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_archive_*.py'` passes, including T1 to T5. Hosted CI runs the phase suite through `infra/scripts/tests/test_phase_bridge.py` in `dotnet-verification.yml` line 207.
- `npm run check:docs` reports no new warning in `docs/operations/archive-smoke.md`.
- The PR description records the pre-fix result of T1: on the base commit, T1 fails with 25 `native index unavailable` gaps.

## 10. Plan-check questions

- (a) Steps whose effort scales with a number unknown at planning time: none in this pull request. The tests use the fixed 25-unit fixture. The closing smoke in section 11 is one run under issue 508's existing plan and caps.
- (b) Where the plan is stricter than its parent: there is no parent plan. The plan is narrower than the approved specification's gap contract only in that it adds one diagnostic boundary form; codes and completeness are unchanged.
- (c) Checks that depend on code, data or behaviour neither on `main` nor in the pull request: none of T1 to T5. The closing evidence depends on issue 548's change, which is not on `main`, and on DA1.

## 11. Deferred work

- D1. Closing evidence for issue 549. Evidence that it is needed: the root cause is the deadline (section 2.2), whose fix is issue 548. On the verdict path: yes, a `monitoring_incomplete` gap holds publication. Deferral is permitted because the transfer change is issue 548's approved route, not this issue's scope. Responsible: the session that runs the next C03 smoke under issue 508 after issue 548's pull request merges. Completion evidence: in both phases' receipts, every `native_monitoring_index` object is `verified`, and no gap boundary starts with `native index`. If any index object is then `missing`, that is a new finding under H4 and DA1, reported on this issue, not a pass. Gate it blocks: closing issue 549 and the C03 batch dispatch under issue 531 decision 6943512296 point 2. Workload bound: one smoke run, under issue 508's existing exhaustion action.

## 12. Size estimate

Estimate, 2026-11-18, from the scope above, not measured: about 15 changed lines in `archive.py`, about 90 in the test file, 1 sentence in `archive-smoke.md`, and the state file; 4 files.

## 13. Sources

- Run [44248514485](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44248514485) on `main` `730581540a7225a3a27b27a626f03fe2ea78de83`: artifacts named in section 2; job 134613218455 step timings; job 134637543521 ("full-C3") step timings.
- `infra/scripts/phase/archive.py` at the base commit: lines 8, 73-77, 261-300, 955-961, 963-1030, 1984-2073, 2154-2214.
- `src/Parchwell.Kafka/Statistics/KafkaNativeMonitoringCapture.cs:319-334` (index path and write) at the base commit.
- `charts/parchwell/templates/_consumer.tpl:75-91` and the pod specs in the pod artifacts (native-monitoring environment).
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on, so no documentation tool was needed (plan-pr "Plan rules" 1). The estimate of CLI cost per call is derived from the receipts, not from Azure documentation.

## 14. Open questions for Hari

- Q1. Scope. Recommendation: approve this pull request. Alternative: no code change; close issue 549 on the D1 evidence alone. Effect: without this change, a later deadline overrun again labels every unread index "unavailable", which sent this smoke report to the wrong cause.
- Q2. DA1. Recommendation: accept DA1 by name, to be tested by the D1 smoke. Alternative: before the smoke, add the `native_monitoring_completion_failed` stderr line to the consumer and verifier hosts' completion catch blocks, as the load generator already does. That needs a new plan revision, touches `src/Parchwell.Verifier/Program.cs` (a protected path), and whether that file is a verifier mutation input is not established. Effect: with the alternative, a missing index at D1 can be told apart from a write failure without another smoke run.

## 15. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 4, a requirement in section 5, or the scope in section 3, including any change to the transfer code or the deadline.
