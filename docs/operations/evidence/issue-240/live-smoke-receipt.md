# Probe batch 2 completed smoke

The live smoke completed successfully on 2026-10-30, including evidence collection, ephemeral destruction and cluster stop. This records readiness of the smoke evidence path under the existing acceptance contract. Scientific item assessments remain unissued.

## Execution card and authority

- Task: Hari requested a PR, then reported completion of the smoke. The [direct documentation instruction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6517648693) records this task on the [originating issue](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240); it does not reuse the earlier binding plan's one-PR approval.
- Governing acceptance: [smoke contract](../../probe-v2-smoke-contract.md#smoke-acceptance), [smoke implementation plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6494160519) and the [single-run dispatch decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6516247285). The implementation and repairs are already on main.
- Source: `0fd8d85a8d112d1cef1de9ed3d8a360dc2fcdf33`, the actual run source and this documentation branch's base.
- Deliverable: this single authored receipt and a completion link in the existing smoke contract. Preserve historical implementation receipts. Raw artifacts, logs and analysis output remain outside the repository. No runtime, verifier, expected-outcome or results change.
- Verification scope: the fixed configuration matrix in the source's `infra/scripts/phase/probe_v2.py`, its candidate/receipt pairs and corresponding actual pod evidence, plus the two workflow jobs' cleanup outcomes. Fetch only the run's candidate and pod archives, bounded to 2 MiB and 8 MiB downloaded respectively, at most 250 pod-archive members and 32 MiB expanded. A bound or evidence mismatch blocks the acceptance record and is handed off without inventing missing evidence.
- Commands: `PYTHONDONTWRITEBYTECODE=1 python3 /tmp/issue-240-smoke-evidence/verify.py` replays the source's existing `ProbeV2.candidate` and `ProbeV2.pod` against downloaded actual bytes; `npm run check:docs`; `git diff --check`. Analysis code and output are temporary, not new repository tooling. No local .NET rerun is needed for this documentation-only change; required hosted CI remains mandatory.
- Publication and review: publish the first verified, cold-reviewed revision. This new PR has at most two comprehensive and two focused cold rounds under AGENTS.md rule 8; exhausted or unsafe review saves unpublished work on `-wip` and hands off to Hari. Final-head hosted review and required CI remain mandatory. No additional Azure or mutation dispatch is authorized.

This receipt changes no verifier predicate, so predicate-to-test preparation is not applicable.

## Run and retained evidence

[Scenario run 41012354641](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41012354641) completed with conclusion `success`, using `scenario=probe-batch-2`, `run_kind=smoke`, `items=all` on the source above. Its evidence run ID is `probe-41012354641-1`. The [batch job](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41012354641/job/123920844370) passed image publication/readback, every action/judge path, both artifact uploads, workload deletion and ephemeral destruction. The [dispatch-end stop job](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41012354641/job/123946717863) passed and logged `aks_stopped`. No environment approval remains pending for this run.

The public Actions archives retain the candidate/final-receipt bytes and actual pod logs/JSON. The SHA-256 values below were computed from the downloaded ZIP bytes and match GitHub's artifact metadata. The metadata gives expiry `2027-01-28T06:08:39Z` for both artifacts.

| Archive | Artifact and metadata | SHA-256 |
|---|---|---|
| `probe-batch-candidates-41012354641-1` | [Download](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41012354641/artifacts/12960983894), [metadata](https://api.github.com/repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/12960983894) | `30d6bba1a83a3bbbb7b0ce6cc7b1c131ac3e9a527668112dc0c5a90fd29bfdf9` |
| `probe-batch-pods-41012354641-1` | [Download](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41012354641/artifacts/12960794818), [metadata](https://api.github.com/repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/12960794818) | `7c6583ecadd00768be5e9f510244b834e7810dfc0d7d53a94cc7332746ad5127` |

The candidates and `probe-v2-smoke.json` in those archives bind these immutable worker digests:

| Worker | Digest |
|---|---|
| LoadGen | `sha256:761d054281e4083fe56e18c9953356df639c69beaa10239f7a891f96fc53cdc5` |
| Verifier | `sha256:94aa5f5539c3381cc4973d8209211e6b9af37355bec5f6368e08d8fcfa84e37f` |

## Checked acceptance

The offline replay used the existing [transport and pod-binding implementation at the run source](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0fd8d85a8d112d1cef1de9ed3d8a360dc2fcdf33/infra/scripts/phase/probe_v2.py). Downloads were replaced only with reads of the actual archived bytes. Expected source, run, matrix and judge Job names came from the run metadata and source; pod identities and final receipts were the archived originals. No synthetic pod or candidate fixture supplied missing evidence.

All 16 candidate/final-receipt pairs passed the existing transport check, including source/run/configuration, immutable digests, raw candidate hash, final handler success/cleanup and actual owner-bound judge pod identity. Every candidate has `status=complete_valid`, `deterministic=true`, `security_finding=false`, `item_assessment=null` and only the diagnostic `smoke_only`. All 80 repeats are `complete_valid` with empty reason lists. Counts below were computed from the candidate bytes; the full expected configuration set matched exactly.

| Smoke question | Configurations | Complete valid repeats |
|---|---|---|
| Partition selection (B08) | C0, C1 | 10 |
| Null-value transport (E01) | C0, C1 | 10 |
| Permission scope (F06) | C0 through C11 | 60 |

The archived `probe-v2-smoke.json` reports `evidence_passed=true`. Its 49 stage Jobs match the source matrix and archived phase state. Their collection records have no gaps, their pod identities/images match the run, and their worker exit codes are zero. The candidates record managed Confluent.Kafka version `2.16.0.0` and native librdkafka `2.16.0` consistently. These are the versions reported by the actual workers, not a separate library-version inference.

The batch log's new registry stage markers report command exit zero for both worker digest reads, with no readback retry marker. The original Azure CLI stall did not recur in this run; the live retry branch was not exercised. Its internal cause remains unproven.

## Verification and limits

The offline command in the execution card passed, using actual archives and the unchanged production transport functions. This checks transport bindings and reports the verifier's recorded smoke decisions; it does not independently rerun the scientific judge. The workflow's successful teardown and stop outcomes were checked through job metadata and logs. Documentation and whitespace checks precede the frozen cold review; final-head CI and completed review reports belong in the PR description.

This completes the live smoke prerequisite under the existing contract. The reviewed effective-scope confirmation remains trusted operator evidence; this record does not independently discover inherited or group access. The candidates retain null item assessments, and no `results/` file or scientific verdict is proposed. A full experiment plan must be refreshed against the merged smoke support, retain the permission-evidence decision, and satisfy applicable mutation acceptance before later scientific evidence-pack publication. A full run or replacement smoke needs its own authorization.

## Frozen review input

The first comprehensive cold review compares immutable tag `issue-240-smoke-evidence-review-1` with source `0fd8d85a8d112d1cef1de9ed3d8a360dc2fcdf33`. The tag is created on the commit containing this receipt before review. The reviewer receives the entire authored PR diff and this receipt, then reads the cited authority and actual evidence independently. No cold round precedes this new PR's input. This documentation PR is separate from the merged repair PR's exhausted review budget. All started reports must complete before corrections; cumulative counts and complete reports are recorded in the PR description.
