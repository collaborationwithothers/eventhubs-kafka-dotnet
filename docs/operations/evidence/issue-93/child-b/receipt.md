This receipt records child B's completed ordinary verifier regressions and the approved callable-domain proofs. Mutation evidence for A and B and residual parent dispositions remain pending at #93 closure under #89.

## Authority and tested inputs

[Plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301492532), updated `2026-10-20T21:19:10Z`, was [independently checked](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595) at `2026-10-20T21:25:04Z` and [approved for Codex](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6306182797) at `2026-10-21T02:03:26Z`. The [child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) adopts the parent split, approved item Revision 2 and the two transfers. [Predecessor #98](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/98) merged at `2026-10-20T20:57:20Z`, commit `09a70930e471353b97ad07f9560e9734e562294e`, the selected source baseline.

The suite [tested-source manifest](raw/suite-1/tested-source.json) records the actual tested commit/tree and every source/test/configuration input hash. The [suite controller](raw/suite-1/controller.json), [discovery controller](raw/discovery-1/controller.json) and [focused controller](raw/focused-1/controller.json) record exact commands, caps, outcomes and input comparisons. Documentation and authored evidence were completed after execution; they are not represented as additional tested production inputs.

The [setup receipt](raw/setup-1/setup.json), [SDK selection](raw/setup-1/sdk-selection.json) and retained setup logs record the actual SDK, runtime, effective NuGet sources and Docker prerequisites. One setup invocation is permitted, capped at 600 seconds. B's own child decision permits the Linux agent suite; A's additional suite allowances do not transfer.

Actual tested-source metadata (verbatim JSON):

```json
{
  "tested_commit": "80de425d403d844c34e865f264da256890b7211c",
  "tested_commit_tree": "65fea100fc7105473c8f51c1b18828cbabe40c89",
  "comparison_commit": null,
  "dirty_diff_sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "dirty_diff_bytes": 0,
  "dirty_diff_scope": "tracked HEAD diff excluding this PR's retained evidence directory",
  "source_input_manifest_sha256": "8045ebc3e9b9e1b93c212e561b43e75a6877abb6e964bd27de33d14b82372b80",
  "config": null,
  "controller": {
    "path": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-92-pr6b/controller.py",
    "sha256": "dd596cbc108bea213c33ae170b5eb75fc30a6d0aa838684cef2f5703052b2fe5"
  }
}
```

## Ordinary execution and consumed allowances

The fixed [case reconciliation](raw/case-reconciliation.json) contains exact discovered and executed identifiers and outcomes. All selected identities must appear exactly once and pass. No mutation run or exact replay is part of this acceptance.

| Measure | Actual value |
| --- | --- |
| baseline_count | `453` |
| added_count | `24` |
| expected_count | `477` |
| class_counts | `{"A01.A01EvidenceRecorderTests": 64, "Judgement.ConfigurationFidelityTests": 43, "Judgement.LateWriteChecksTests": 75, "Judgement.RunJudgementTests": 40, "Judgement.StatisticsChecksTests": 156, "Window.MessageIdentityReaderTests": 34, "Window.RunWindowTests": 30, "Window.WindowConsumerTests": 35}` |

The actual full-suite temporary directory and exact retention copy command are recorded in [retention.json](raw/suite-1/retention.json):

```json
{
  "source_directory": "/tmp/parchwell-dotnet-verification.qdNoOQy6",
  "destination": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification",
  "copy_command": "python3 -; shutil.copytree('/tmp/parchwell-dotnet-verification.qdNoOQy6', '/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification', ignore=shutil.ignore_patterns('bin', 'obj', 'Allowed.cs', 'Forbidden.cs', 'WarningControl.csproj', 'packages.lock.json'))",
  "original_producer": "bash ./scripts/verify-dotnet.sh",
  "source_commit": "80de425d403d844c34e865f264da256890b7211c",
  "retention_scope": "Suite XML, logs, manifests and program-written spike evidence; copied warning fixture source and build intermediates are omitted.",
  "compressions": [
    {
      "original_source": "/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/test.log",
      "retained_path": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/test.log.gz",
      "original_size_bytes": 515,
      "original_sha256": "1c0608fd9addfa9df29155e6662b30633d017ba81569971cd8a45bc349b501c1",
      "stored_size_bytes": 274,
      "stored_sha256": "8b20a715333f8564a2d82e8270990add8632f1bf3ecfede30a4684b2086c6793",
      "command": "python3 -; data=pathlib.Path('/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/test.log').read_bytes(); pathlib.Path('/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/test.log.gz').write_bytes(gzip.compress(data,mtime=0))"
    },
    {
      "original_source": "/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/trial-records.json",
      "retained_path": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/trial-records.json.gz",
      "original_size_bytes": 1649011,
      "original_sha256": "af06ce0db2ab8abdb0734e997ae985d3f18ee6a9b897d85e302291d4ad7c5b32",
      "stored_size_bytes": 65753,
      "stored_sha256": "ee1c93e61d8c8e4fae633525ce1a9d03be02dfcf9a517c50fbf971e983d885c2",
      "command": "python3 -; data=pathlib.Path('/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/trial-records.json').read_bytes(); pathlib.Path('/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/trial-records.json.gz').write_bytes(gzip.compress(data,mtime=0))"
    },
    {
      "original_source": "/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/experiment.json",
      "retained_path": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/experiment.json.gz",
      "original_size_bytes": 25931332,
      "original_sha256": "3ee7ce2b56943c7e1bcadc870b9c095019be6d8b583c52c799679389d1f2ac52",
      "stored_size_bytes": 1833553,
      "stored_sha256": "747c476d171b60ce8ef979ba5a6e7fc312958a886bd5dadc6cf43fd8f5f32363",
      "command": "python3 -; data=pathlib.Path('/tmp/parchwell-dotnet-verification.qdNoOQy6/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/experiment.json').read_bytes(); pathlib.Path('/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/verification/Parchwell.Kafka.Tests/spike/phantom-spike-148551d49f68416299a7663863bc5b34/experiment.json.gz').write_bytes(gzip.compress(data,mtime=0))"
    }
  ]
}
```

All consumed controller invocations, including failures or incomplete attempts, are retained below. Controller output is evidence of its own recorded status; ordinary success never establishes a mutant kill.

```json
[
  {
    "mode": "suite",
    "ordinal": 1,
    "reserved_utc": "2026-10-21T02:06:33.373987+00:00",
    "receipt": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/suite-1/controller.json"
  },
  {
    "mode": "discovery",
    "ordinal": 1,
    "reserved_utc": "2026-10-21T02:13:46.434821+00:00",
    "receipt": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/discovery-1/controller.json"
  },
  {
    "mode": "baseline",
    "ordinal": 1,
    "reserved_utc": "2026-10-21T02:13:47.927902+00:00",
    "receipt": "/workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-b/raw/focused-1/controller.json"
  }
]
```

Approved maxima are one SDK setup at 600 seconds, two suites at 1800 seconds each, one discovery at 300 seconds, two focused executions at 300 seconds each, two XML validations at 120 seconds each and two documentation passes at 300 seconds each. The same 14400-second investigation/proof/reconciliation allowance covers the expanded B scope; no resumed-session reset or additional retry is implied.

## Assigned inventory

The fixed B assignment contains 29 original line keys and 43 variants. Literal spans, mutators and replacements below are copied from the frozen report records; original IDs are report locators, not future report IDs. Each case ID points to the exact discovered/executed name in the case appendix and [raw reconciliation](raw/case-reconciliation.json); the execution source is [focused XML](raw/focused-1/results.xml).

| Original line key | Frozen variants: span, mutator, literal replacement | Exact case IDs / proof | Ordinary regression status | Mutation / proof / gap status and decision |
| --- | --- | --- | --- | --- |
| `VerifyRun/A01/A01EvidenceRecorder.cs:22` | [2657 22:24-22:84](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L23) `Logical mutation` `request.Ledger.IsSuccess \|\| request.Ledger.Value is not null` => `B0003` | `B0003` | Pass; exact cases B0003 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) Historical exact-OR replay remains CS8604 compile-limited. |
| `VerifyRun/Judgement/ConfigurationFidelityCheck.cs:45` | [4875 45:26-45:62](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L1553) `Logical mutation` `selectedOverride \|\| observedOverride` => `P1` |  [P1 for 4875](proofs.md) | Not applicable; callable-domain proof only | Listed proof(s) accepted under Q3 by [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595); only their named variants have proof status; no observed kill. Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/LateWriteChecks.cs:12` | [4899 12:9-12:46](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L1661) `String mutation` `""` => `B0148` | `B0148` | Pass; exact cases B0148 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) Existing-test/historical-Survived conflict pending actual mutant execution; no runner discrepancy inferred. |
| `VerifyRun/Judgement/LateWriteChecks.cs:13` | [4900 13:9-13:48](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L1679) `String mutation` `""` => `B0154` | `B0154` | Pass; exact cases B0154 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) Existing-test/historical-Survived conflict pending actual mutant execution; no runner discrepancy inferred. |
| `VerifyRun/Judgement/RunJudgement.cs:88` | [5076 88:36-89:72](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2237) `Logical mutation` `a.Disposition == AssessmentDisposition.Fail \|\|\n                a.Classification == AssessmentClassification.Incomplete` => `B0220`, `B0221` | `B0220`, `B0221` | Pass; exact cases B0220, B0221 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/RunJudgement.cs:107` | [5095 107:27-107:55](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2255) `Linq method mutation (OrderBy() to OrderByDescending())` `fidelity.Differences.OrderByDescending` => `B0102`, `B0103`, `B0104` | `B0102`, `B0103`, `B0104` | Pass; exact cases B0102, B0103, B0104 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:43` | [5147 43:31-43:43](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2471) `Equality mutation` `number < 26` => `P2` |  [P2 for 5147](proofs.md) | Not applicable; callable-domain proof only | Listed proof(s) accepted under Q3 by [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595); only their named variants have proof status; no observed kill. Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:118` | [5224 118:17-118:77](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2777) `Statement mutation` `;` => `B0352`; [5225 118:69-118:75](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2795) `String mutation` `""` => `B0352` | `B0352` | Pass; exact cases B0352 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:119` | [5226 119:17-119:81](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2813) `Statement mutation` `;` => `B0353`; [5227 119:71-119:79](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2831) `String mutation` `""` => `B0353` | `B0353` | Pass; exact cases B0353 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:120` | [5229 120:71-120:79](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2849) `String mutation` `""` => `B0358` | `B0358` | Pass; exact cases B0358 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:121` | [5230 121:17-121:79](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2867) `Statement mutation` `;` => `B0359`; [5231 121:70-121:77](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2885) `String mutation` `""` => `B0359` | `B0359` | Pass; exact cases B0359 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:124` | [5233 124:21-124:74](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2903) `Statement mutation` `;` => `B0351`, `B0354`, `B0357`, `B0362`, `B0363` | `B0351`, `B0354`, `B0357`, `B0362`, `B0363` | Pass; exact cases B0351, B0354, B0357, B0362, B0363 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:130` | [5245 130:17-130:97](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2993) `Statement mutation` `;` => `B0360`, `B0361`; [5246 130:70-130:80](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3011) `String mutation` `""` => `B0360`, `B0361` | `B0360`, `B0361` | Pass; exact cases B0360, B0361 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:131` | [5248 131:17-131:98](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3029) `Statement mutation` `;` => `B0355`, `B0356`; [5249 131:75-131:80](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3047) `String mutation` `""` => `B0355`, `B0356` | `B0355`, `B0356` | Pass; exact cases B0355, B0356 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:174` | [5315 174:17-174:53](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3299) `Logical mutation` `selected is null && selectionUnknown` => `P3` |  [P3 for 5315](proofs.md) | Not applicable; callable-domain proof only | Listed proof(s) accepted under Q3 by [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595); only their named variants have proof status; no observed kill. Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:191` | [5338 191:37-191:55](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3335) `String mutation` `""` => `B0350` | `B0350` | Pass; exact cases B0350 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:204` | [5350 204:10-204:24](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3371) `String mutation` `""` => `B0354`; [5351 204:52-204:63](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3389) `String mutation` `""` => `B0363` | `B0354`, `B0363` | Pass; exact cases B0354, B0363 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:205` | [5352 205:10-205:18](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3407) `String mutation` `""` => `B0362`; [5353 205:49-205:57](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3425) `String mutation` `""` => `B0357`; [5354 205:83-205:96](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3443) `String mutation` `""` => `B0351` | `B0351`, `B0357`, `B0362` | Pass; exact cases B0351, B0357, B0362 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:211` | [5366 211:24-211:42](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3533) `String mutation` `""` => `B0351`, `B0352`, `B0353`, `B0354`, `B0355`, `B0356`, `B0357`, `B0358`, `B0359`, `B0360`, `B0361`, `B0362`, `B0363`; [5367 211:49-211:76](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3551) `String mutation` `$""` => `B0351`, `B0352`, `B0353`, `B0354`, `B0355`, `B0356`, `B0357`, `B0358`, `B0359`, `B0360`, `B0361`, `B0362`, `B0363` | `B0351`, `B0352`, `B0353`, `B0354`, `B0355`, `B0356`, `B0357`, `B0358`, `B0359`, `B0360`, `B0361`, `B0362`, `B0363` | Pass; exact cases B0351, B0352, B0353, B0354, B0355, B0356, B0357, B0358, B0359, B0360, B0361, B0362, B0363 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:216` | [5369 216:9-216:106](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3569) `Statement mutation` `;` => `B0355`, `B0360`; [5372 216:98-216:104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3587) `String mutation` `""` => `B0355`, `B0360` | `B0355`, `B0360` | Pass; exact cases B0355, B0360 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:217` | [5373 217:9-218:62](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3605) `Statement mutation` `;` => `B0356`, `B0361` | `B0356`, `B0361` | Pass; exact cases B0356, B0361 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Judgement/StatisticsChecks.cs:218` | [5380 218:34-218:59](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3623) `Conditional (false) mutation` `(false?".max" :".p99")` => `B0361`; [5379 218:34-218:59](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3641) `Conditional (true) mutation` `(true?".max" :".p99")` => `B0356`; [5381 218:44-218:50](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3659) `String mutation` `""` => `B0361`; [5382 218:53-218:59](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3677) `String mutation` `""` => `B0356` | `B0356`, `B0361` | Pass; exact cases B0356, B0361 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/MessageIdentityReader.cs:11` | [5531 11:59-11:64](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3803) `Boolean mutation` `true` => `B0382`, `B0386`, `B0390`, `B0411`, `B0412`; [5532 11:66-11:70](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3821) `Boolean mutation` `false` => `B0382`, `B0386`, `B0390`, `B0411`, `B0412` | `B0382`, `B0386`, `B0390`, `B0411`, `B0412` [P4 for 5531](proofs.md) | Pass; exact cases B0382, B0386, B0390, B0411, B0412 | Listed proof(s) accepted under Q3 by [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595); only their named variants have proof status; no observed kill. Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) Existing-test/historical-Survived conflict pending actual mutant execution; no runner discrepancy inferred. |
| `VerifyRun/Window/RunWindow.cs:22` | [5568 22:55-23:80](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4001) `Conditional (true) mutation` `(true?new Dictionary<int, long>() :new Dictionary<int, long>(endHighs))` => `B0438` | `B0438` | Pass; exact cases B0438 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/RunWindow.cs:28` | [5574 28:13-28:55](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4037) `Logical mutation` `!parsedStartNote.IsSuccess && note is null` => `B0414` | `B0414` | Pass; exact cases B0414 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/RunWindow.cs:33` | [5580 33:46-33:92](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4055) `Conditional (false) mutation` `(false?[Failure(7)] :failures)` => `B0414` | `B0414` | Pass; exact cases B0414 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/WindowConsumer.cs:23` | [5638 23:13-23:62](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4073) `Statement mutation` `;` => `B0466` | `B0466` | Pass; exact cases B0466 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/WindowConsumer.cs:28` | [5643 28:17-28:66](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4091) `Statement mutation` `;` => `B0477` | `B0477` | Pass; exact cases B0477 | Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |
| `VerifyRun/Window/WindowConsumer.cs:42` | [5661 42:47-42:68](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4109) `Equality mutation` `pollSlice <= remaining` => `B0460`, `B0461`, `B0462` | `B0460`, `B0461`, `B0462` [P5 for 5661](proofs.md) | Pass; exact cases B0460, B0461, B0462 | Listed proof(s) accepted under Q3 by [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595); only their named variants have proof status; no observed kill. Mutation evidence deferred; no accepted gap. [Child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641) |

## Exact case identifiers

| ID | Normalized discovered and executed name | Result |
| --- | --- | --- |
| B0003 | `Parchwell.Verifier.Tests.A01.A01EvidenceRecorderTests.Given_FailedLedgerWithRetainedValue_When_Recorded_Then_DeliveryCodesRemainUnavailable` | Pass |
| B0102 | `Parchwell.Verifier.Tests.Judgement.ConfigurationFidelityTests.Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives(differenceCount: 1)` | Pass |
| B0103 | `Parchwell.Verifier.Tests.Judgement.ConfigurationFidelityTests.Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives(differenceCount: 2)` | Pass |
| B0104 | `Parchwell.Verifier.Tests.Judgement.ConfigurationFidelityTests.Given_SettingDifferencesAtDiagnosticCap_When_RunJudgementRetains_Then_OrdinalOrderSurvives(differenceCount: 3)` | Pass |
| B0148 | `Parchwell.Verifier.Tests.Judgement.LateWriteChecksTests.Given_ManifestMarkersAndCompleteness_When_Evaluated_Then_OnlyStandingMarkersAndTrueCompletenessPass(markers: "broker_lifecycle_inventory_unproven", complete: True, pass: True)` | Pass |
| B0154 | `Parchwell.Verifier.Tests.Judgement.LateWriteChecksTests.Given_ManifestMarkersAndCompleteness_When_Evaluated_Then_OnlyStandingMarkersAndTrueCompletenessPass(markers: "request_lifecycle_capture_unsupported", complete: True, pass: True)` | Pass |
| B0220 | `Parchwell.Verifier.Tests.Judgement.RunJudgementTests.Given_SyntheticNoncanonicalWindowAssessment_When_Judging_Then_CurrentStatusPredicateIsPreserved(failedWithoutClassification: False)` | Pass |
| B0221 | `Parchwell.Verifier.Tests.Judgement.RunJudgementTests.Given_SyntheticNoncanonicalWindowAssessment_When_Judging_Then_CurrentStatusPredicateIsPreserved(failedWithoutClassification: True)` | Pass |
| B0350 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticBrokerAdditionAtBoundary_When_Evaluated_Then_Check26EmitsExactDiagnosticTuple` | Pass |
| B0351 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "disconnects")` | Pass |
| B0352 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "name")` | Pass |
| B0353 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "nodeid")` | Pass |
| B0354 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "req_timeouts")` | Pass |
| B0355 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "rtt.cnt")` | Pass |
| B0356 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "rtt.p99")` | Pass |
| B0357 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "rxerrs")` | Pass |
| B0358 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "source")` | Pass |
| B0359 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "state")` | Pass |
| B0360 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "throttle.cnt")` | Pass |
| B0361 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "throttle.max")` | Pass |
| B0362 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "txerrs")` | Pass |
| B0363 | `Parchwell.Verifier.Tests.Judgement.StatisticsChecksTests.Given_SyntheticRequiredBrokerFieldMissingWithoutParserProblem_When_Evaluated_Then_Check21EmitsExactFallbackDiagnostic(field: "txretries")` | Pass |
| B0382 | `Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.Given_BrokenIdentityHeader_When_Reading_Then_CheckSixOwnsProblem(field: "attempt-id", mode: "utf8")` | Pass |
| B0386 | `Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.Given_BrokenIdentityHeader_When_Reading_Then_CheckSixOwnsProblem(field: "instance-id", mode: "utf8")` | Pass |
| B0390 | `Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.Given_BrokenIdentityHeader_When_Reading_Then_CheckSixOwnsProblem(field: "sequence", mode: "utf8")` | Pass |
| B0411 | `Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.Given_NullOrInvalidKey_When_Reading_Then_CheckSixOwnsProblem(invalid: False)` | Pass |
| B0412 | `Parchwell.Verifier.Tests.Window.MessageIdentityReaderTests.Given_NullOrInvalidKey_When_Reading_Then_CheckSixOwnsProblem(invalid: True)` | Pass |
| B0414 | `Parchwell.Verifier.Tests.Window.RunWindowTests.Given_EmptyProblemFailure_When_Creating_Then_FallbackCheckSevenFails` | Pass |
| B0438 | `Parchwell.Verifier.Tests.Window.RunWindowTests.Given_SuppliedEndHighs_When_Creating_Then_RetainsIndependentSnapshot` | Pass |
| B0460 | `Parchwell.Verifier.Tests.Window.WindowConsumerTests.Given_PollSliceAroundRemainingSafety_When_Draining_Then_NativeTimeoutUsesSmallerDuration(slice: 100, expected: 100)` | Pass |
| B0461 | `Parchwell.Verifier.Tests.Window.WindowConsumerTests.Given_PollSliceAroundRemainingSafety_When_Draining_Then_NativeTimeoutUsesSmallerDuration(slice: 101, expected: 100)` | Pass |
| B0462 | `Parchwell.Verifier.Tests.Window.WindowConsumerTests.Given_PollSliceAroundRemainingSafety_When_Draining_Then_NativeTimeoutUsesSmallerDuration(slice: 99, expected: 99)` | Pass |
| B0466 | `Parchwell.Verifier.Tests.Window.WindowConsumerTests.Given_PreCancelledWork_When_Draining_Then_NoNativeOperationStarts` | Pass |
| B0477 | `Parchwell.Verifier.Tests.Window.WindowConsumerTests.Given_WorkCancelledDuringAssignment_When_Draining_Then_NoPositionOrPollStarts` | Pass |

## Proofs, limitations and parent closure

P1-P5 are proof arguments accepted for their actual callable domains by the [current independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595) under the child decision's Q3 condition. Their premises and conclusions appear in [proofs.md](proofs.md). They are not observed mutant kills, exclusions or denominator changes. P4 concerns UTF-8 preamble emission; 5532 concerns invalid-byte fallback. The selected runtime must match P4's pinned 10.0.13 source premise.

Variant 2657 retains its historical CS8604 compilation limitation for the exact OR replacement. A passing ordinary W1 regression does not complete an exact mutant comparison. Parent closure also retains 4585 and 5014 as historical compilation-limited comparisons until later evidence or an explicit decision settles them. No nullable suppression, mutant repair, relaxed warning setting or fresh runner-discrepancy conclusion is introduced.

[Child A's receipt](../child-a/receipt.md) records its ordinary completion separately from mutation evidence still required at closure. Statistics:43 is shared with disjoint variant ownership: A retains 5146; B owns transferred 5147 and Statistics:174/5315. The combined parent recount is deferred to Q5 closure; this B receipt does not perform or claim that recount. OutcomeClassifier:38/5025's existing proof remains outside this assignment.

Parent closure status: `pending #93 closure run under #89 and remaining Q5 dispositions`. Owner: the #93 closure run under #89, per the [child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641). No #93 completion, live A01 authorization or verdict is produced by B ordinary acceptance.

## Evidence provenance

Program-written evidence remains under raw/. The sibling [raw-manifest.json](raw-manifest.json) records each retained raw file's path, exact producer/source commit, byte size and SHA-256. Authored receipt, proofs and review judgements remain outside raw/. Source directories and copy commands are retained rather than substituting a predetermined temporary path.





## Additional execution details

The original report SHA-256 is `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492`, with tested production source `a2001534d94cd1135b9af53dc0e9d9816b00ce3d`, as recorded in the [frozen inventory](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/lines.json). No production source moved or changed from the merged predecessor, so no source remap applies. The eight selected classes retain the entire baseline identity set; only the literal additions are new.

Case reconciliation parses XML, then decodes only escaped backslash/backslash and escaped backslash/double-quote once with `re.sub(r'\\([\\"])', r'\1', name)`. It preserves whitespace and case. Discovery and focused XML each match the measured baseline plus the literal additions; their exact sets, duplicate checks and per-case outcomes are retained in [case-reconciliation.json](raw/case-reconciliation.json).

| Full ordinary project | Passed | Failed | Skipped | Not run | Errors |
| --- | --- | --- | --- | --- | --- |
| Parchwell.ArchitectureTests | 8 | 0 | 0 | 0 | 0 |
| Parchwell.Kafka.Tests | 306 | 0 | 0 | 0 | 0 |
| Parchwell.LoadGen.Tests | 262 | 0 | 0 | 0 | 0 |
| Parchwell.Scenarios.Tests | 94 | 0 | 0 | 0 | 0 |
| Parchwell.Verifier.Tests | 2167 | 0 | 0 | 0 | 0 |

The unchanged scaffold selector reported `Parchwell.Enrichment.Tests` as an empty scaffold; no test coverage is claimed for it. The suite passed locked restore, Release build, Kafka XML consistency, report validation, and the valid/intentional-CS1030 warning control. The [build log](raw/suite-1/verification/build.log) records zero warnings and errors. Synthetic local Kafka/Toxiproxy component workloads remain harness evidence, not Event Hubs findings.

| Invocation | Exit | Measured elapsed seconds | Evidence |
| --- | --- | --- | --- |
| setup-1 | 0 | 8.379430 | [receipt](raw/setup-1/setup.json) |
| suite-1 | 0 | 426.725253 | [receipt](raw/suite-1/controller.json) |
| discovery-1 | 0 | 1.409234 | [receipt](raw/discovery-1/controller.json) |
| focused-1 | 0 | 1.720164 | [receipt](raw/focused-1/controller.json) |
| focused XML validation 1 | 0 | 0.885570 | [receipt](raw/focused-1/validation.json), [log](raw/focused-1/validation.log) |

All invocations above exited successfully. Suite, discovery and focused execution report unchanged source inputs, no termination and complete execution evidence. There were no failed or incomplete B attempts and no source/test/configuration repairs after the successful suite. No second suite or focused attempt and no mutation invocation was consumed.

Compressed logs below retain the exact command-output byte stream. The table measures both that decompressed stream and the stored gzip bytes. Copied spike evidence compression has original/stored hashes and exact commands in [retention.json](raw/suite-1/retention.json).

| Controller log | Original SHA-256 | Stored SHA-256 |
| --- | --- | --- |
| [suite-1](raw/suite-1/command.log.gz) | `da5c3a2e69fa8a0bd98aec4555fa49073a34a9bedd2c9d91489d2988606b6dc6` | `29129794579a12c61013b8cd091ef6f8bb9678e86371c9967f61d02ca78b9457` |
| [discovery-1](raw/discovery-1/command.log.gz) | `c31c567241070e8ae099025d507d5ef439debc12f2edf2d1daa9ba7a58d4ee50` | `e725aa8f8c884ef948057c0ca176f02dc8cc9163d9c616ed0bd6ca9e6c9f1826` |
| [focused-1](raw/focused-1/command.log.gz) | `99d0474d00e4b735032c7405f82b722c78ac76d39d653988625df3219b9c0dc5` | `41caa48ce0d0a874f0255715048cc0e586aa4cb8f3231c3a2de60b6691dc6af3` |

The [investigation accounting](raw/investigation-accounting.json) conservatively charges the entire wall interval from this session's current Approval through completion of the fixed assignment/proof/reconciliation work, including setup, execution and waiting. It does not deduct exempt authority reads or reset prior work. The measured interval remains within the approved shared allowance; later governance review does not renew that allowance.

Documentation acceptance receipts retain the exact command, source commit, elapsed time and outcome. A successful scan is advisory and does not prove factual correctness.

| Documentation pass | Exit | Measured elapsed seconds | Evidence |
| --- | --- | --- | --- |
| 1 | 0 | 14.982847 | [receipt](raw/docs-1.json), [log](raw/docs-1.log) |
| 2 | 0 | 14.105887 | [receipt](raw/docs-2.json), [log](raw/docs-2.log) |

Original HTTP response-header bytes are gzip-retained to preserve their CRLF line endings. The [header compression record](raw/proof-sources/compression.json) lists original/stored hashes, byte lengths and producer commands; no source body was normalized.
