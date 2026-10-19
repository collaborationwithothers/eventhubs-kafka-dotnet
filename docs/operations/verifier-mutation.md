# Verifier mutation record

## PR 5 window scope

PR 5 measures the existing judgement code and the new Kafka window decisions. The selected globs are `VerifyRun/Judgement/*.cs` and `VerifyRun/Window/*.cs`; orchestration, Blob/auth lifetime, host code and passive codecs remain outside judgement scope under [the approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6259391036) and [Q6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6259492117). PR 6 retains final survivor dispositions and the measured break threshold; break remains zero.

Toolchain: .NET SDK 10.0.402; x64 .NET runtime 10.0.13; xunit.v3.mtp-v2 4.0.2; Microsoft.Testing.Platform 2.5.1; dotnet-stryker 5.0.0. These versions come from `global.json`, the locked test dependencies, the tool manifest and the retained command logs.

### PR 5 full report

The [retained JSON report](evidence/issue-80-pr5/mutation-report.json.gz), [controller receipt](evidence/issue-80-pr5/controller.json), [command log](evidence/issue-80-pr5/command.log.gz), [single-pass extraction](evidence/issue-80-pr5/extraction-summary.json) and [tested source manifest](evidence/issue-80-pr5/tested-source.json) are committed review evidence. Decompress the report and log with `gzip -dc`; use the report to verify the uncompressed SHA-256 below. These measurements describe the recorded tested commit; subsequent review fixes have not been mutation-tested.

| Measure | Recorded value |
| --- | --- |
| Tested commit | `d85c6bf08c340eefc7fd32c97b6240c19827c5a5` |
| Tested tree | `afd8d6cef0576702af4d5f880121b88e88c357cb` |
| Elapsed seconds | 3409.997 |
| Exit status | 0 |
| Generated-mutant count in log | 5280 |
| JSON-report mutant records | 1487 |
| JSON-report SHA-256 | `f7f4dd647f305fa1997a8dfae00df2eae80fff86cc2e9444562108d217ef5aac` |
| Report path | `.artifacts/issue-80-pr5/mutation-full/reports/mutation-report.json` |
| Score numerator / denominator | 549 / 790 |
| Score | 69.49% |

The command ran from `tests/Parchwell.Verifier.Tests`, under the retained session controller:

```bash
ParchwellVerifierMutationOnly=true python3 /workspace/eventhubs-kafka-dotnet/.artifacts/issue-80-pr5/controllers/run-bounded.py /workspace/eventhubs-kafka-dotnet/.artifacts/issue-80-pr5/mutation-full 3600 10000 dotnet stryker --config-file stryker-config.json --output ../../.artifacts/issue-80-pr5/mutation-full
```

The controller, command, working directory, environment override, observed generated count, elapsed time and exit reason are retained in `.artifacts/issue-80-pr5/mutation-full/controller.json`. The tested source manifest is `tested-source.json` beside it. The generated log count and serialized JSON count are separate measurements.

The mutation-only MSBuild property removes `Component/**/*.cs` and `Support/ComponentEvidenceBuilder.cs`. The retained evaluated Compile inventories contain [30 normal files](evidence/issue-80-pr5/compile-normal.json) and [23 mutation-only files](evidence/issue-80-pr5/compile-mutation.json); their [reconciliation](evidence/issue-80-pr5/compile-reconciliation.json) removes exactly the approved component/helper set. Stryker actually discovered 1810 tests, matching the deterministic preflight. The JSON discovery metadata contains 1810 tests, including 140 window tests and 382 judgement tests, with no component tests. Inventory reconciliation, discovered test metadata and extraction summary are retained under `.artifacts/issue-80-pr5/validation/compile-reconciliation.json`, `mutation-full/discovered-tests.json` and `mutation-full/extraction-summary.json`.

The preview MTP runner warned that target-framework discovery failed and assumed the selected `net10.0`; the evaluated Release project manifests in `.artifacts/issue-80-pr5/validation/full-suite-3/repository-report/manifests/` independently retain that framework. The log contains 66 coverage-relay acknowledgement timeout warnings marking tests Dubious. Stryker also reported a definite-assignment compile fallback in the unselected `FileSystemScenarioSource.RejectSymlink` method. Its records remain CompileError, outside the score denominator. Runner diagnostics are retained in `mutation-full/command.log`; they are not mutant Timeout statuses. PR 6 retains the independent runner sample.

The score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage) * 100`, following the [pinned score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73). CompileError, RuntimeError and Ignored are outside that denominator. Every Killed, Timeout, Survived and NoCoverage record was checked against the two selected directories. Timeout is retained as its own status, not called an assertion kill.

Tables below were generated in one extraction pass over the retained report. Every Survived and NoCoverage row remains `not reviewed`; PR 5 performed zero manual survivor replays and zero manual mutation finding dispositions.

| Mutant status | Count |
| --- | ---: |
| Killed | 540 |
| Timeout | 9 |
| Survived | 241 |
| NoCoverage | 0 |
| CompileError | 123 |
| Ignored | 574 |
| RuntimeError | 0 |
| Pending | 0 |

| File | Mutant ID | Location | Status | Disposition |
| --- | --- | --- | --- | --- |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4183 | 28:15-28:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4187 | 33:26-35:90 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4188 | 33:26-35:48 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4189 | 33:26-34:85 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4190 | 33:26-34:45 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4191 | 33:26-33:95 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4200 | 36:71-36:83 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4204 | 39:40-39:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4210 | 41:67-41:82 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4211 | 41:90-41:119 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4220 | 47:45-47:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4221 | 47:61-47:67 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4223 | 48:63-48:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4224 | 48:79-48:85 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4225 | 48:87-48:108 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4232 | 52:88-52:106 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4233 | 52:108-52:114 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4240 | 54:86-54:111 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4241 | 54:113-54:119 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4244 | 55:92-55:111 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4252 | 59:43-59:53 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4253 | 59:55-59:61 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4254 | 59:63-59:76 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4255 | 60:30-60:35 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4258 | 66:74-66:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4260 | 67:84-67:111 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4262 | 68:64-68:81 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4264 | 69:74-69:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4266 | 70:90-70:121 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4268 | 71:70-71:90 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4281 | 94:41-94:56 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4282 | 94:64-94:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4289 | 100:21-100:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4290 | 100:45-100:60 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4291 | 100:68-100:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4300 | 110:21-110:92 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4301 | 110:45-110:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4302 | 110:61-110:67 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4303 | 110:69-110:89 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4311 | 116:17-116:81 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4312 | 116:41-116:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4313 | 116:63-116:78 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4320 | 124:21-124:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4321 | 124:45-124:60 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4322 | 124:68-124:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4329 | 131:21-131:92 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4330 | 131:45-131:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4331 | 131:61-131:67 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4332 | 131:69-131:89 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4336 | 135:49-135:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4337 | 135:65-135:71 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4338 | 135:73-135:86 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4341 | 137:49-137:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4342 | 137:65-137:71 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4343 | 137:73-137:91 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4355 | 151:45-151:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4356 | 151:61-151:67 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4357 | 151:69-151:89 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4368 | 163:68-163:78 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4369 | 163:80-163:86 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4370 | 163:88-163:101 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4373 | 164:67-164:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4374 | 164:79-164:85 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4375 | 164:87-164:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4378 | 165:61-165:71 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4379 | 165:73-165:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4380 | 165:81-165:86 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4383 | 166:66-166:76 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4384 | 166:78-166:84 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4385 | 166:86-166:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4395 | 169:63-169:73 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4396 | 169:75-169:81 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4397 | 169:83-169:95 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4404 | 173:45-173:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4405 | 173:57-173:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4406 | 173:65-173:70 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4412 | 175:45-175:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4413 | 175:57-175:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4414 | 175:65-175:75 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs | 4416 | 182:20-182:43 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4443 | 18:32-18:56 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4448 | 29:28-29:68 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4458 | 34:26-34:34 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4466 | 43:30-43:41 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4467 | 45:26-45:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4473 | 50:34-50:57 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4477 | 55:34-55:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4481 | 61:13-61:33 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4489 | 69:71-69:90 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs | 4490 | 70:9-70:26 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4491 | 12:9-12:46 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4492 | 13:9-13:48 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4506 | 26:62-26:76 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4507 | 26:78-26:84 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4508 | 26:86-26:97 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4514 | 29:49-29:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4515 | 29:65-29:71 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4520 | 35:37-35:56 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4521 | 35:64-35:93 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4529 | 39:45-39:60 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4530 | 39:68-39:94 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4548 | 48:37-48:42 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4552 | 53:54-53:68 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4554 | 54:58-54:69 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4575 | 71:44-71:69 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4576 | 71:71-71:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4577 | 71:79-71:89 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4580 | 77:45-77:121 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs | 4584 | 84:20-84:43 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs | 4606 | 35:48-35:57 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs | 4617 | 38:42-38:66 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4634 | 67:13-67:32 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4647 | 75:17-77:78 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4649 | 76:35-76:75 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4650 | 76:52-76:75 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4654 | 82:41-82:82 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4666 | 87:36-88:72 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4685 | 106:27-106:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs | 4692 | 116:67-116:112 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4711 | 22:29-22:48 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4712 | 22:56-22:68 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4719 | 27:71-27:89 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4725 | 35:29-35:48 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4726 | 35:56-35:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4730 | 38:58-38:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4731 | 38:85-38:120 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4734 | 40:33-40:52 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4735 | 40:60-40:111 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4736 | 43:31-43:43 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4737 | 43:31-43:43 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4747 | 55:38-55:69 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4757 | 61:24-61:34 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4759 | 63:33-63:51 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4770 | 73:40-73:44 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4771 | 74:21-74:52 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4781 | 83:37-83:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4782 | 83:62-83:66 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4791 | 89:41-89:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4792 | 89:66-89:71 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4793 | 90:21-90:49 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4794 | 92:43-92:47 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4802 | 98:38-98:42 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4806 | 102:21-102:52 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4809 | 108:87-108:91 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4810 | 109:17-109:38 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4811 | 110:17-110:26 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4814 | 118:17-118:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4815 | 118:69-118:75 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4816 | 119:17-119:81 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4817 | 119:71-119:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4819 | 120:71-120:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4820 | 121:17-121:79 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4821 | 121:70-121:77 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4823 | 124:21-124:74 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4826 | 125:87-125:91 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4827 | 125:93-125:102 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4833 | 127:41-127:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4834 | 127:66-127:90 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4835 | 130:17-130:97 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4836 | 130:70-130:80 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4838 | 131:17-131:98 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4839 | 131:75-131:80 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4850 | 136:58-136:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4852 | 139:54-139:58 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4857 | 143:33-143:52 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4858 | 143:60-143:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4866 | 147:46-147:65 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4872 | 149:38-149:56 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4873 | 149:64-149:74 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4885 | 156:29-156:47 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4886 | 156:55-156:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4887 | 157:34-157:38 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4894 | 165:82-165:100 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4896 | 165:114-165:124 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4897 | 167:39-167:43 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4905 | 174:17-174:53 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4920 | 188:17-188:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4928 | 191:37-191:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4933 | 194:22-194:60 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4940 | 204:10-204:24 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4941 | 204:52-204:63 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4942 | 205:10-205:18 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4943 | 205:49-205:57 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4944 | 205:83-205:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4951 | 210:85-210:157 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4952 | 210:85-210:106 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4953 | 210:110-210:157 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4954 | 210:130-210:157 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4956 | 211:24-211:42 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4957 | 211:49-211:76 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4959 | 216:9-216:106 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4962 | 216:98-216:104 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4963 | 217:9-218:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4969 | 218:34-218:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4970 | 218:34-218:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4971 | 218:44-218:50 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4972 | 218:53-218:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4975 | 224:102-224:120 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4976 | 224:102-224:120 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4977 | 224:102-224:120 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs | 4978 | 233:58-233:82 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/ConsumedRecordBudget.cs | 5113 | 35:51-35:117 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/ConsumedRecordBudget.cs | 5120 | 45:51-45:97 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5121 | 11:59-11:64 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5122 | 11:66-11:70 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5134 | 26:51-26:74 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5142 | 36:40-36:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5143 | 36:40-36:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5144 | 36:40-36:59 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5145 | 36:62-36:78 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5146 | 36:81-36:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5151 | 46:41-46:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5155 | 52:41-52:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs | 5156 | 57:89-57:99 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5158 | 22:55-23:80 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5163 | 25:49-25:97 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5164 | 28:13-28:55 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5169 | 31:46-31:114 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5170 | 31:46-31:114 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5171 | 31:46-31:66 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5172 | 31:81-31:101 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5176 | 35:75-35:100 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5177 | 35:102-35:112 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5180 | 36:101-36:123 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5181 | 36:125-36:132 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5185 | 37:72-37:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5186 | 37:98-37:115 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5190 | 41:71-41:98 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5191 | 41:100-41:117 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5196 | 42:59-42:82 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5197 | 42:84-42:101 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5206 | 48:83-48:106 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5207 | 48:108-48:125 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5218 | 56:77-56:95 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5219 | 56:97-56:114 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/RunWindow.cs | 5226 | 67:9-67:21 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs | 5231 | 23:13-23:62 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs | 5236 | 28:17-28:66 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs | 5249 | 37:83-37:96 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs | 5255 | 42:47-42:68 | Survived | not reviewed |
| /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs | 5277 | 61:55-61:82 | Survived | not reviewed |

PR 4 measures the recorded-input judgement scope. Historical PR 3 foundation measurements and dispositions remain below under their own tested commits.

## PR 4 judgement scope

The selected glob is `VerifyRun/Judgement/*.cs`, including every new judgement helper. Parsers, host code and codecs are outside this scope under [#85 decision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85), adopted by [the #80 process decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6249900231). PR 6 owns survivor review and the measured break threshold. The configured break threshold remains zero.

The full report and each incremental report are retained separately. Each retains original Stryker statuses; every Survived and NoCoverage row is marked `not reviewed`, with no manual survivor dispositions or replays in PR 4. The historical foundation score has a different scope.

Toolchain: .NET SDK 10.0.402; x64 .NET runtime 10.0.13; xunit.v3.mtp-v2 4.0.2; Microsoft.Testing.Platform 2.5.1; dotnet-stryker 5.0.0. Versions follow `global.json`, the locked verifier test dependencies, the tool manifest and the retained test/report logs.

Stryker reported a definite-assignment compile fallback for `OutcomeClassifier.Classify` in the full run. Those mutants remain CompileError in that report. The first incremental run uses a nullable-value check with a separate local assignment for classifier instrumentation. It then reported a tuple-pattern definite-assignment fallback in StatisticsChecks.Evaluate; the second incremental run separates that nullable check and assignment as well. These are code-shape corrections, with ordinary regression assertions unchanged. The preview MTP runner also logged coverage-relay acknowledgement warnings; they are not mutant Timeout statuses. PR 6 retains responsibility for the independent runner sample.

### Full report

| Measure | Recorded value |
| --- | --- |
| Tested commit | `1092a7a1775e6780db67a6675a2859a5f66ae8d2` |
| Tested tree | `57b2095473c118069539b4c41698d077391f55da` |
| Elapsed seconds | 409.323 |
| Exit status | 0 |
| First generated-mutant count in log | 4655 |
| JSON-report mutant records | 1224 |
| JSON-report SHA-256 | `f119fca7b77cd7b9318a8b7f3f09c8d1f2c9a4c06ad994dc2db1fc7b3781bbc5` |
| Report path | `.artifacts/issue-80-pr4/mutation-full/reports/mutation-report.json` |
| Score numerator / denominator | 379 / 593 |
| Score | 63.91% |

Executed from `tests/Parchwell.Verifier.Tests`:

```bash
timeout --signal=TERM --kill-after=15s 3600s dotnet stryker --config-file stryker-config.json --output ../../.artifacts/issue-80-pr4/mutation-full
```

| Mutant status | Count |
| --- | ---: |
| Killed | 378 |
| Timeout | 1 |
| Survived | 214 |
| NoCoverage | 0 |
| CompileError | 139 |
| Ignored | 492 |
| RuntimeError | 0 |
| Pending | 0 |

The score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage) * 100`, using the [pinned score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73). CompileError, RuntimeError and Ignored remain outside the denominator. Ignored records include files excluded by the configured glob; every Killed, Timeout, Survived or NoCoverage record was checked against the judgement directory; Stryker also retains CompileError records outside the filter. The retained report has a nonempty tested population. An incremental score applies only to its selected changed-code population; the full-scope score remains in the Full report section.

Survivor table generated by the retained extraction over this report; locations are report line and column values.

| File | Mutant ID | Location | Status | Disposition |
| --- | --- | --- | --- | --- |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3880 | 28:15-28:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3884 | 33:26-35:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3885 | 33:26-35:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3886 | 33:26-34:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3887 | 33:26-34:45 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3888 | 33:26-33:95 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3897 | 36:71-36:83 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3901 | 39:40-39:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3907 | 41:67-41:82 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3908 | 41:90-41:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3917 | 47:45-47:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3918 | 47:61-47:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3920 | 48:63-48:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3921 | 48:79-48:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3922 | 48:87-48:108 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3929 | 52:88-52:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3930 | 52:108-52:114 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3937 | 54:86-54:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3938 | 54:113-54:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3941 | 55:92-55:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3949 | 59:43-59:53 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3950 | 59:55-59:61 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3951 | 59:63-59:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3952 | 60:30-60:35 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3955 | 66:74-66:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3957 | 67:84-67:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3959 | 68:64-68:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3961 | 69:74-69:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3963 | 70:90-70:121 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3965 | 71:70-71:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3978 | 94:41-94:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3979 | 94:64-94:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3986 | 100:21-100:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3987 | 100:45-100:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3988 | 100:68-100:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3997 | 110:21-110:92 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3998 | 110:45-110:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3999 | 110:61-110:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4000 | 110:69-110:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4008 | 116:17-116:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4009 | 116:41-116:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4010 | 116:63-116:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4017 | 124:21-124:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4018 | 124:45-124:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4019 | 124:68-124:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4026 | 131:21-131:92 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4027 | 131:45-131:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4028 | 131:61-131:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4029 | 131:69-131:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4033 | 135:49-135:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4034 | 135:65-135:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4035 | 135:73-135:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4038 | 137:49-137:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4039 | 137:65-137:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4040 | 137:73-137:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4052 | 151:45-151:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4053 | 151:61-151:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4054 | 151:69-151:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4065 | 163:68-163:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4066 | 163:80-163:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4067 | 163:88-163:101 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4070 | 164:67-164:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4071 | 164:79-164:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4072 | 164:87-164:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4075 | 165:61-165:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4076 | 165:73-165:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4077 | 165:81-165:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4080 | 166:66-166:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4081 | 166:78-166:84 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4082 | 166:86-166:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4092 | 169:63-169:73 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4093 | 169:75-169:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4094 | 169:83-169:95 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4101 | 173:45-173:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4102 | 173:57-173:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4103 | 173:65-173:70 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4109 | 175:45-175:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4110 | 175:57-175:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4111 | 175:65-175:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4113 | 182:20-182:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4140 | 18:32-18:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4145 | 29:28-29:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4155 | 34:26-34:34 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4163 | 43:30-43:41 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4164 | 45:26-45:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4170 | 50:34-50:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4174 | 55:34-55:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4178 | 61:13-61:33 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4186 | 69:71-69:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4187 | 70:9-70:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4188 | 12:9-12:46 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4189 | 13:9-13:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4203 | 26:62-26:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4204 | 26:78-26:84 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4205 | 26:86-26:97 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4211 | 29:49-29:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4212 | 29:65-29:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4213 | 29:73-29:93 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4217 | 35:37-35:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4218 | 35:64-35:93 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4226 | 39:45-39:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4227 | 39:68-39:94 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4238 | 46:17-46:113 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4240 | 46:17-46:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4245 | 48:37-48:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4246 | 49:17-49:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4249 | 53:54-53:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4251 | 54:58-54:69 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4253 | 55:57-55:65 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4255 | 56:52-56:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4261 | 62:50-62:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4262 | 62:70-62:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4263 | 62:78-62:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4272 | 71:44-71:69 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4273 | 71:71-71:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4274 | 71:79-71:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4277 | 77:45-77:121 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4281 | 84:20-84:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4331 | 67:13-67:32 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4344 | 75:17-77:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4346 | 76:35-76:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4347 | 76:52-76:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4351 | 82:41-82:82 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4363 | 87:36-88:72 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4382 | 106:27-106:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4389 | 116:67-116:112 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4408 | 22:29-22:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4409 | 22:56-22:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4416 | 27:71-27:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4422 | 35:29-35:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4423 | 35:56-35:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4427 | 38:58-38:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4428 | 38:85-38:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4431 | 40:33-40:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4432 | 40:60-40:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4433 | 43:31-43:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4434 | 43:31-43:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4452 | 60:24-60:34 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4454 | 62:33-62:51 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4462 | 69:17-69:38 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4464 | 70:58-70:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4474 | 78:37-78:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4475 | 78:62-78:66 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4484 | 84:41-84:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4485 | 84:66-84:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4486 | 85:21-85:49 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4487 | 87:43-87:47 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4495 | 93:38-93:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4496 | 94:21-94:37 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4497 | 94:58-94:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4500 | 99:87-99:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4501 | 100:17-100:38 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4502 | 101:17-101:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4506 | 109:17-109:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4507 | 109:69-109:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4508 | 110:17-110:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4509 | 110:71-110:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4510 | 111:17-111:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4511 | 111:71-111:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4512 | 112:17-112:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4513 | 112:70-112:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4515 | 115:21-115:74 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4518 | 116:87-116:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4519 | 116:93-116:102 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4525 | 118:41-118:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4526 | 118:66-118:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4527 | 121:17-121:97 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4528 | 121:70-121:80 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4530 | 122:17-122:98 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4531 | 122:75-122:80 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4534 | 123:84-123:88 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4541 | 126:28-126:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4543 | 127:53-127:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4545 | 130:54-130:58 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4550 | 134:33-134:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4551 | 134:60-134:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4559 | 138:46-138:65 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4565 | 140:38-140:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4566 | 140:64-140:74 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4578 | 147:29-147:47 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4579 | 147:55-147:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4580 | 148:34-148:38 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4587 | 156:82-156:100 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4589 | 156:114-156:124 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4590 | 158:39-158:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4596 | 162:80-162:84 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4601 | 166:70-166:88 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4602 | 166:90-166:100 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4603 | 166:102-166:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4610 | 177:10-177:24 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4611 | 177:52-177:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4612 | 178:10-178:18 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4613 | 178:49-178:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4614 | 178:83-178:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4619 | 183:48-183:158 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4620 | 183:49-183:70 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4621 | 183:85-183:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4622 | 183:85-183:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4623 | 183:110-183:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4624 | 183:130-183:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4625 | 184:13-184:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4626 | 184:24-184:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4627 | 184:49-184:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4629 | 189:9-189:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4632 | 189:98-189:104 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4633 | 190:9-191:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4639 | 191:34-191:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4640 | 191:34-191:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4641 | 191:44-191:50 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4642 | 191:53-191:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4645 | 197:102-197:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4646 | 197:102-197:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4647 | 197:102-197:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4648 | 206:58-206:82 | Survived | not reviewed |

### Incremental since 1 report

| Measure | Recorded value |
| --- | --- |
| Tested commit | `ac30e5bf02703129df8968bae46781478bcd260c` |
| Tested tree | `3dff5ee862b99ccb9f1cb96381872ee506fa8b4e` |
| Comparison commit | `1092a7a1775e6780db67a6675a2859a5f66ae8d2` |
| Elapsed seconds | 329.217 |
| Exit status | 0 |
| First generated-mutant count in log | 4682 |
| JSON-report mutant records | 1251 |
| JSON-report SHA-256 | `b1b066d8afd9df23a752d6dbf116cf2c9222f45aa36fe650fe3b4a241c8e93d5` |
| Report path | `.artifacts/issue-80-pr4/mutation-since/reports/mutation-report.json` |
| Score numerator / denominator | 302 / 450 |
| Score | 67.11% |

Executed from `tests/Parchwell.Verifier.Tests`:

```bash
timeout --signal=TERM --kill-after=15s 1200s dotnet stryker --config-file stryker-config.json --since:1092a7a1775e6780db67a6675a2859a5f66ae8d2 --output ../../.artifacts/issue-80-pr4/mutation-since
```

| Mutant status | Count |
| --- | ---: |
| Killed | 301 |
| Timeout | 1 |
| Survived | 148 |
| NoCoverage | 0 |
| CompileError | 322 |
| Ignored | 479 |
| RuntimeError | 0 |
| Pending | 0 |

The score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage) * 100`, using the [pinned score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73). CompileError, RuntimeError and Ignored remain outside the denominator. Ignored records include files excluded by the configured glob; every Killed, Timeout, Survived or NoCoverage record was checked against the judgement directory; Stryker also retains CompileError records outside the filter. The retained report has a nonempty tested population. An incremental score applies only to its selected changed-code population; the full-scope score remains in the Full report section.

Survivor table generated by the retained extraction over this report; locations are report line and column values.

| File | Mutant ID | Location | Status | Disposition |
| --- | --- | --- | --- | --- |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3880 | 28:15-28:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3884 | 33:26-35:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3885 | 33:26-35:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3886 | 33:26-34:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3887 | 33:26-34:45 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3888 | 33:26-33:95 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3897 | 36:71-36:83 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3901 | 39:40-39:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3907 | 41:67-41:82 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3908 | 41:90-41:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3917 | 47:45-47:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3918 | 47:61-47:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3920 | 48:63-48:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3921 | 48:79-48:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3922 | 48:87-48:108 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3929 | 52:88-52:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3930 | 52:108-52:114 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3937 | 54:86-54:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3938 | 54:113-54:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3941 | 55:92-55:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3949 | 59:43-59:53 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3950 | 59:55-59:61 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3951 | 59:63-59:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3952 | 60:30-60:35 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3955 | 66:74-66:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3957 | 67:84-67:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3959 | 68:64-68:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3961 | 69:74-69:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3963 | 70:90-70:121 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3965 | 71:70-71:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3978 | 94:41-94:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3979 | 94:64-94:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3986 | 100:21-100:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3987 | 100:45-100:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3988 | 100:68-100:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3997 | 110:21-110:92 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3998 | 110:45-110:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 3999 | 110:61-110:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4000 | 110:69-110:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4008 | 116:17-116:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4009 | 116:41-116:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4010 | 116:63-116:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4017 | 124:21-124:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4018 | 124:45-124:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4019 | 124:68-124:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4026 | 131:21-131:92 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4027 | 131:45-131:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4028 | 131:61-131:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4029 | 131:69-131:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4033 | 135:49-135:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4034 | 135:65-135:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4035 | 135:73-135:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4038 | 137:49-137:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4039 | 137:65-137:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4040 | 137:73-137:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4052 | 151:45-151:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4053 | 151:61-151:67 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4054 | 151:69-151:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4065 | 163:68-163:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4066 | 163:80-163:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4067 | 163:88-163:101 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4070 | 164:67-164:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4071 | 164:79-164:85 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4072 | 164:87-164:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4075 | 165:61-165:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4076 | 165:73-165:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4077 | 165:81-165:86 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4080 | 166:66-166:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4081 | 166:78-166:84 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4082 | 166:86-166:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4092 | 169:63-169:73 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4093 | 169:75-169:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4094 | 169:83-169:95 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4101 | 173:45-173:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4102 | 173:57-173:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4103 | 173:65-173:70 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4109 | 175:45-175:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4110 | 175:57-175:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4111 | 175:65-175:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/BookkeepingChecks.cs` | 4113 | 182:20-182:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4140 | 18:32-18:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4145 | 29:28-29:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4155 | 34:26-34:34 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4163 | 43:30-43:41 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4164 | 45:26-45:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4170 | 50:34-50:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4174 | 55:34-55:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4178 | 61:13-61:33 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4186 | 69:71-69:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs` | 4187 | 70:9-70:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4203 | 26:62-26:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4204 | 26:78-26:84 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4205 | 26:86-26:97 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4211 | 29:49-29:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4212 | 29:65-29:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4213 | 29:73-29:93 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4217 | 35:37-35:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4218 | 35:64-35:93 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4226 | 39:45-39:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4227 | 39:68-39:94 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4238 | 46:17-46:113 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4240 | 46:17-46:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4245 | 48:37-48:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4246 | 49:17-49:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4249 | 53:54-53:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4251 | 54:58-54:69 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4253 | 55:57-55:65 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4255 | 56:52-56:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4261 | 62:50-62:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4262 | 62:70-62:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4263 | 62:78-62:119 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4272 | 71:44-71:69 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4273 | 71:71-71:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4274 | 71:79-71:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4277 | 77:45-77:121 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/LateWriteChecks.cs` | 4281 | 84:20-84:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs` | 4303 | 35:48-35:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/OutcomeClassifier.cs` | 4314 | 38:42-38:66 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4331 | 67:13-67:32 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4344 | 75:17-77:78 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4346 | 76:35-76:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4347 | 76:52-76:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4351 | 82:41-82:82 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4363 | 87:36-88:72 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4382 | 106:27-106:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/RunJudgement.cs` | 4389 | 116:67-116:112 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4637 | 202:10-202:24 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4638 | 202:52-202:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4639 | 203:10-203:18 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4640 | 203:49-203:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4641 | 203:83-203:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4648 | 208:85-208:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4649 | 208:85-208:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4650 | 208:110-208:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4651 | 208:130-208:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4653 | 209:24-209:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4654 | 209:49-209:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4656 | 214:9-214:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4659 | 214:98-214:104 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4660 | 215:9-216:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4666 | 216:34-216:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4667 | 216:34-216:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4668 | 216:44-216:50 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4669 | 216:53-216:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4672 | 222:102-222:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4673 | 222:102-222:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4674 | 222:102-222:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4675 | 231:58-231:82 | Survived | not reviewed |

### Incremental since 2 report

| Measure | Recorded value |
| --- | --- |
| Tested commit | `53246e269d890373b1a7d962a1f4968a763c71db` |
| Tested tree | `0b8d69d28c36102fa926c0571eb146d8b1f358fa` |
| Comparison commit | `ac30e5bf02703129df8968bae46781478bcd260c` |
| Elapsed seconds | 217.282 |
| Exit status | 0 |
| First generated-mutant count in log | 4682 |
| JSON-report mutant records | 1251 |
| JSON-report SHA-256 | `23dd2048cee57f350ce15e58f720fb90f8deb5538d42d4248efc4fd78bf268dc` |
| Report path | `.artifacts/issue-80-pr4/mutation-since-2/reports/mutation-report.json` |
| Score numerator / denominator | 148 / 230 |
| Score | 64.35% |

Executed from `tests/Parchwell.Verifier.Tests`:

```bash
timeout --signal=TERM --kill-after=15s 1200s dotnet stryker --config-file stryker-config.json --since:ac30e5bf02703129df8968bae46781478bcd260c --output ../../.artifacts/issue-80-pr4/mutation-since-2
```

| Mutant status | Count |
| --- | ---: |
| Killed | 148 |
| Timeout | 0 |
| Survived | 82 |
| NoCoverage | 0 |
| CompileError | 111 |
| Ignored | 910 |
| RuntimeError | 0 |
| Pending | 0 |

The score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage) * 100`, using the [pinned score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73). CompileError, RuntimeError and Ignored remain outside the denominator. Ignored records include files excluded by the configured glob; every Killed, Timeout, Survived or NoCoverage record was checked against the judgement directory; Stryker also retains CompileError records outside the filter. The retained report has a nonempty tested population. An incremental score applies only to its selected changed-code population; the full-scope score remains in the Full report section.

Survivor table generated by the retained extraction over this report; locations are report line and column values.

| File | Mutant ID | Location | Status | Disposition |
| --- | --- | --- | --- | --- |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4408 | 22:29-22:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4409 | 22:56-22:68 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4416 | 27:71-27:89 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4422 | 35:29-35:48 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4423 | 35:56-35:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4427 | 38:58-38:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4428 | 38:85-38:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4431 | 40:33-40:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4432 | 40:60-40:111 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4433 | 43:31-43:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4434 | 43:31-43:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4444 | 55:38-55:69 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4454 | 61:24-61:34 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4456 | 63:33-63:51 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4467 | 73:40-73:44 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4468 | 74:21-74:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4478 | 83:37-83:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4479 | 83:62-83:66 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4488 | 89:41-89:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4489 | 89:66-89:71 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4490 | 90:21-90:49 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4491 | 92:43-92:47 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4499 | 98:38-98:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4503 | 102:21-102:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4506 | 108:87-108:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4507 | 109:17-109:38 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4508 | 110:17-110:26 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4511 | 118:17-118:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4512 | 118:69-118:75 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4513 | 119:17-119:81 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4514 | 119:71-119:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4516 | 120:71-120:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4517 | 121:17-121:79 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4518 | 121:70-121:77 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4520 | 124:21-124:74 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4523 | 125:87-125:91 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4524 | 125:93-125:102 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4530 | 127:41-127:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4531 | 127:66-127:90 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4532 | 130:17-130:97 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4533 | 130:70-130:80 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4535 | 131:17-131:98 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4536 | 131:75-131:80 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4547 | 136:58-136:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4549 | 139:54-139:58 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4554 | 143:33-143:52 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4555 | 143:60-143:99 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4563 | 147:46-147:65 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4569 | 149:38-149:56 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4570 | 149:64-149:74 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4582 | 156:29-156:47 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4583 | 156:55-156:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4584 | 157:34-157:38 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4591 | 165:82-165:100 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4593 | 165:114-165:124 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4594 | 167:39-167:43 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4602 | 174:17-174:53 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4617 | 188:17-188:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4625 | 191:37-191:55 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4630 | 194:22-194:60 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4637 | 204:10-204:24 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4638 | 204:52-204:63 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4639 | 205:10-205:18 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4640 | 205:49-205:57 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4641 | 205:83-205:96 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4648 | 210:85-210:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4649 | 210:85-210:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4650 | 210:110-210:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4651 | 210:130-210:157 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4653 | 211:24-211:42 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4654 | 211:49-211:76 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4656 | 216:9-216:106 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4659 | 216:98-216:104 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4660 | 217:9-218:62 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4666 | 218:34-218:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4667 | 218:34-218:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4668 | 218:44-218:50 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4669 | 218:53-218:59 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4672 | 224:102-224:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4673 | 224:102-224:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4674 | 224:102-224:120 | Survived | not reviewed |
| `/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs` | 4675 | 233:58-233:82 | Survived | not reviewed |

### Ordinary validation

All listed checks use the judgement code at `53246e269d890373b1a7d962a1f4968a763c71db`. The targeted report was validated with the existing ValidateReport task. Its Report argument uses an absolute path because MSBuild resolves the plan's relative Report path under `scripts/`.

| Evidence | Result | SHA-256 |
| --- | --- | --- |
| Parchwell.ArchitectureTests XML | 8/8 passed | `de1be659176e5a9536db43502be1882dc603ca26087c00f2f1672aecbfe61a1b` |
| Parchwell.Kafka.Tests XML | 306/306 passed | `d434f173ae62e19295e3c934dabf83c94dc213244cdd595be2dafcbe74ff6bdb` |
| Parchwell.LoadGen.Tests XML | 262/262 passed | `e5a8d50868b7b12a01bc0fee966f553ceca75f780ab2ed6c297128e9dc7441a8` |
| Parchwell.Scenarios.Tests XML | 94/94 passed | `fe6ed1693377e66cf2758e60e55c2a1793043d2a4dcc6c22efc4b7453e675e84` |
| Parchwell.Verifier.Tests XML | 1491/1491 passed | `010d1094b8d2337574cd48762e8a2505ee23f1899fe0b4f5e8964b93b39bf1d6` |
| Full verify-dotnet.sh log | passed; includes architecture manifests, XML validation, Kafka fixture and warning controls | `25c6756cb86ad0f0bac32720b8886a7cf122bcd87969328de8d3add3bb6d3d55` |
| Targeted verifier XML | 1491/1491 passed; ValidateReport accepted | `c0c695f0a1e0004aa4a56bd4fe63b2d80534f41f343634128942dd79809f58c9` |
| Release build | zero warnings and errors | `2e410206376ec25d22eb9527c4a710b7ad619e0af3e313d6f32ba02e72237f25` |
| Documentation tests | 27 passed | `1b07fc222f6b8b026835b37bf511610ec62fc6c81a98d763b59f8a8f660eaaf9` |
| Documentation scan | no warnings | `6c7713c963598ca571ee241dc382c4fba20d602f7acd67dca99a012eeadefd87` |

The final full verification ran from an isolated checkout at the same tested commit and passed 2161/2161 tests. Isolation keeps ordinary build/test artifacts separate from Stryker instrumentation. No project, package or lock-file change was required.

## Historical PR 3 foundation record

The historical PR 3 foundation baseline is 80.71% on commit `0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90`. It contains 3877 mutants, including 618 current Survived/NoCoverage rows. Every current survivor is marked `not reviewed`; the earlier replay sample below applies only to its own tested SHA and report.

Hari's decision on 2026-10-18 [accepts the mutant 2134 preview-vs-normal-MTP disagreement and 17 pre-test compilation differences](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728) for PR 3. The Stryker baseline remains as measured; the accepted differences do not alter its score or status counts. Before PR 6 sets a break threshold, PR 6 must replay a small sample of Killed mutants under normal MTP and record the result with the survivor review. The completed 610625f runner-validation sample remains historical context; newly uncovered current survivor source files or mutator types are reported as sample gaps, and every current survivor remains `not reviewed`. No old sample disposition or test outcome transfers to a current row.

The historical sample recorded one preview-vs-normal-MTP assertion disagreement (old mutant 2134, an `ArtifactIndexReader` wrong-kind diagnostic case) and 17 pre-test compilation failures. Hari accepted these differences for PR 3 on 2026-10-18; see the decision linked above. Their raw historical evidence is retained below and their statuses and score remain unchanged.

## Historical PR 3 report summary

| Measure | Current result |
| --- | ---: |
| Tested commit | `0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90` |
| Tested tree | `52706c46059bcab835f2190e228ac70701ab7a89` |
| Stryker report SHA-256 | `782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df` |
| Total mutants | 3877 |
| Survived/NoCoverage rows | 618 |
| Killed + Timeout / tested denominator | 2585 / 3203 |
| Score | 80.71% |
| Current replayed rows in this record | 0 |

| Stryker report status | Count |
| --- | ---: |
| Killed | 2578 |
| Survived | 618 |
| Timeout | 7 |
| CompileError | 257 |
| Ignored | 417 |
| NoCoverage | 0 |
| RuntimeError | 0 |
| Pending | 0 |


Wrapper elapsed time: 2071.492 seconds, from the published `run.json` record. The run record, including timestamps, exit status, command, and report hashes, is reproduced in the current measurement excerpts below.

The score formula is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`, matching the [pinned Stryker score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73). Timeout remains a separate status and is not called an assertion kill. CompileError and Ignored are outside this denominator. The Stryker report and embedded ID/status inventory are independently linked by their SHA-256 values in the following current measurement excerpts.

| Toolchain | Evidence |
| --- | --- |
| .NET SDK 10.0.402 | [`global.json`](../../global.json) |
| .NET test runtime | `64-bit (x64) .NET 10.0.13 [collection-per-class, parallel (collections, 4 threads)]` from the targeted XML assembly metadata; runtime 10.0.13 source revision [6aa72daa24f66c01c3d7b6a32519e9d21396deec](https://github.com/dotnet/runtime/tree/6aa72daa24f66c01c3d7b6a32519e9d21396deec) |
| Microsoft.Testing.Platform 2.5.1 | [`tests/Parchwell.Verifier.Tests/packages.lock.json`](../../tests/Parchwell.Verifier.Tests/packages.lock.json) |
| xunit.v3.mtp-v2 4.0.2 | [`tests/Parchwell.Verifier.Tests/packages.lock.json`](../../tests/Parchwell.Verifier.Tests/packages.lock.json) |
| dotnet-stryker 5.0.0 | [`dotnet-tools.json`](../../.config/dotnet-tools.json) and the current command log version banner below |

## Historical PR 3 ordinary validation

The validation record for `0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90` spans 345.994385 seconds from its recorded start and end timestamps. All 10 recorded commands exited 0; the full verification command took 308.580 seconds. Its five project XML reports total 1763/1763 passed, with no failed, skipped, errored, or not-run tests. The targeted Verifier report records 1093/1093 passed. The Release build reported 0 warnings and 0 errors. The documentation test command passed 27 cases; the documentation check command exited 0.

The current Stryker run captured per-test coverage for 1093 tests across 1 assembly. Its metadata reconciliation matches 1093 test rows against 74 exact source/type/method groups and ordinary XML multiplicities. This is metadata reconciliation, not proof of per-test initial execution outcomes. The 2 coverage relay acknowledgements marked `Dubious` are watchdog warnings for tests; they are distinct from the 7 Stryker mutant `Timeout` statuses.

The single `dotnet stryker --version` probe exited 1 because this CLI requires a value for `--version`; the recorded probe expected that exit, while tool-list output and the pinned manifest identify version 5.0.0. The probe is not counted as a failed validation command.

## Historical PR 3 measurement evidence

This excerpt is rendered from retained files only; it does not run Stryker, build, tests, or the preview runner. Current replay dispositions are not inferred from coverage or mutation status. The embedded inventory below preserves every mutant ID grouped by report status and source.

- Tested commit: `0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90`
- Stryker report: `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json`, SHA-256 `782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df`
- Run metadata: `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/run.json`, SHA-256 `98de441f72fda1d59c7d46ebb7247ab970a4b0c9c73472546a627ca327881b40`
- Mutants: 3877; statuses: `{"CompileError": 257, "Ignored": 417, "Killed": 2578, "Survived": 618, "Timeout": 7}`
- Survived or NoCoverage: 618
- Baseline XML: `.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results.xml`, SHA-256 `47689e2e6bdde0e296ec259ff560af87493039d9d025ab04909e431f4e9849b8`, assembly metadata `[{"environment": "64-bit (x64) .NET 10.0.13 [collection-per-class, parallel (collections, 4 threads)]", "errors": "0", "failed": "0", "finish-rtf": "2026-10-18T10:57:58.1747580+00:00", "id": "d7a6980b-49ef-408b-a30c-5f4205605b91", "name": "/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll", "not-run": "0", "passed": "1093", "run-date": "2026-10-18", "run-time": "10:57:55", "skipped": "0", "start-rtf": "2026-10-18T10:57:55.0250391+00:00", "target-framework": ".NETCoreApp,Version=v10.0", "test-framework": "xUnit.net v3 4.0.2+0c7aa4991f", "time": "3.150", "time-rtf": "00:00:03.1500000", "total": "1093"}]`

### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/command.log` (SHA-256 `e661843940b1efc481b453efae3368a2fe4781d5d808c06158c5b1281cdf8f76`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"\n   _____ _              _               _   _ ______ _______ \u00a0\n  / ____| |            | |             | \\ | |  ____|__   __|\u00a0\n | (___ | |_ _ __ _   _| | _____ _ __  |  \\| | |__     | |   \u00a0\n  \\___ \\| __| '__| | | | |/ / _ \\ '__| | . ` |  __|    | |   \u00a0\n  ____) | |_| |  | |_| |   <  __/ |    | |\\  | |____   | |   \u00a0\n |_____/ \\__|_|   \\__, |_|\\_\\___|_| (_)|_| \\_|______|  |_|   \u00a0\n                   __/ |                                     \u00a0\n                  |___/                                      \u00a0\n\n\nVersion: 5.0.0\n\n[11:03:32 INF] Analysis starting.\n[11:03:32 INF] Using project configuration/platform 'Release|`default`'.\n[11:03:32 INF] Analyzing 1 test project(s).\n[11:03:33 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj. Assuming selected framework (net10.0) is present.\n[11:03:34 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj. Assuming selected framework (net10.0) is present.\n[11:03:34 INF] Found project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj to mutate.\n[11:03:34 INF] Analysis complete.\n[11:03:34 INF] Building test project /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj (1/1)\n[11:03:34 INF] Building project Parchwell.Verifier.Tests.csproj using dotnet build Parchwell.Verifier.Tests.csproj -c Release --property:Platform=AnyCPU (directory /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests.)\n[11:03:39 WRN] The Microsoft Test Platform testrunner is currently in preview. Results should be verified since this feature is still being tested.\n[11:03:39 INF] Number of tests found: 1093 for project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj. Initial test run started.\n[11:04:04 INF] 3877 mutants created\n[11:04:04 INF] Capture mutant coverage using 'CoverageBasedTest' mode.\n[11:04:04 INF] Starting per-test coverage capture for MTP runner\n[11:04:04 INF] Capturing per-test coverage for 1093 tests across 1 assemblies\n[11:04:44 WRN] MtpRunner-0: Timed out waiting for coverage relay ack for test 4fd698566276569298a5d5f0e7c59ab3b89d77919f831d43e0321e0335b3bd58; marking as Dubious\n[11:04:54 WRN] MtpRunner-1: Timed out waiting for coverage relay ack for test a5bc4404fb7cbd1d315169a41e71c21563ab88f526beda546eebf8608f3c087c; marking as Dubious\n[11:04:54 INF] Per-test coverage capture complete: 1093 tests captured\nHint: by passing \"--open-report or -o\" the report will open automatically and \nupdate the report in real-time.\n[11:04:55 INF] 257   mutants got status CompileError. Reason: Mutant caused compile errors\n[11:04:55 INF] 417   mutants got status Ignored.      Reason: Removed by block already covered filter\n[11:04:55 INF] 674   total mutants are skipped for the above mentioned reasons\n[11:04:55 INF] 3203  total mutants will be tested\n\nKilled:   2578\nSurvived:  618\nTimeout:     7\nErrors:     0\n\nYour json report has been generated at:\nfile:///workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-rev\niew-fix-run.pt82vy66/reports/mutation-report.json\n\nYour html report has been generated at:\nfile:///workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-rev\niew-fix-run.pt82vy66/reports/mutation-report.html\nYou can open it in your browser of choice.\n[11:38:03 INF] Time Elapsed 00:34:30.8124456\n[11:38:03 INF] The final mutation score is 80.71 %\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/locked-restore.log` (SHA-256 `b3e03ae18735bb5229ce1346f601c3429e93710ba35f7bfb6be0849356438084`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"  Determining projects to restore...\n  Restored /workspace/eventhubs-kafka-dotnet/src/Parchwell.Kafka/Parchwell.Kafka.csproj (in 1.1 sec).\n  Restored /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj (in 1.15 sec).\n  Restored /workspace/eventhubs-kafka-dotnet/src/Parchwell.Scenarios/Parchwell.Scenarios.csproj (in 1.1 sec).\n  Restored /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj (in 1.16 sec).\n  7 of 11 projects are up-to-date for restore.\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/solution-build.log` (SHA-256 `ab3c3b8a6f31add0a2a4f8853aa1c80ff479fb4dd2bddf60a7caa18888f0184f`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"  Parchwell.Enrichment -> /workspace/eventhubs-kafka-dotnet/src/Parchwell.Enrichment/bin/Release/net10.0/Parchwell.Enrichment.dll\n  Parchwell.Kafka -> /workspace/eventhubs-kafka-dotnet/src/Parchwell.Kafka/bin/Release/net10.0/Parchwell.Kafka.dll\n  Parchwell.Enrichment.Tests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Enrichment.Tests/bin/Release/net10.0/Parchwell.Enrichment.Tests.dll\n  Parchwell.Scenarios -> /workspace/eventhubs-kafka-dotnet/src/Parchwell.Scenarios/bin/Release/net10.0/Parchwell.Scenarios.dll\n  Parchwell.Kafka.Tests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Kafka.Tests/bin/Release/net10.0/Parchwell.Kafka.Tests.dll\n  Parchwell.Verifier -> /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/bin/Release/net10.0/Parchwell.Verifier.dll\n  Parchwell.LoadGen -> /workspace/eventhubs-kafka-dotnet/src/Parchwell.LoadGen/bin/Release/net10.0/Parchwell.LoadGen.dll\n  Parchwell.Scenarios.Tests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Scenarios.Tests/bin/Release/net10.0/Parchwell.Scenarios.Tests.dll\n  Parchwell.ArchitectureTests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.ArchitectureTests/bin/Release/net10.0/Parchwell.ArchitectureTests.dll\n  Parchwell.Verifier.Tests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll\n  Parchwell.LoadGen.Tests -> /workspace/eventhubs-kafka-dotnet/tests/Parchwell.LoadGen.Tests/bin/Release/net10.0/Parchwell.LoadGen.Tests.dll\n\nBuild succeeded.\n    0 Warning(s)\n    0 Error(s)\n\nTime Elapsed 00:00:16.69\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/targeted-tests.log` (SHA-256 `390337b588f784190638d3a6a98430fcd96f4085514edcdd889d91a9fb67e73b`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Running tests from /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll (net10.0|x64)\n/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll (net10.0|x64) passed (3s 658ms)\n\n  In process file artifacts produced:\n    - /workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results/results.xml\n\nTest run summary: Passed!\n  total: 1093\n  failed: 0\n  succeeded: 1093\n  skipped: 0\n  duration: 3s 973ms\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/targeted-report.log` (SHA-256 `ed15cf2622bc3f061246329affc24e28bb2bde4617506d0a781a69d0e6bf4c71`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"  Parchwell.Verifier.Tests: 1093 passed; checked report.\n"
```
### `/workspace/scratch/round2-full-verification-excerpt.log` (SHA-256 `9d60f61615412b01034dc3a805778bb58a514ae7da96e372a4ca3efb442bfe2a`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Selected PR 3 verification summary excerpt; this is not the full verification log.\nSource file: /workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/full-verification.log\nSource SHA-256: 9c50ea07c754103b454c330d3fc967eb91f91f2620c6d3cca3670d127a204c50\nSelected source line numbers: 9, 6760, 6763, 6766, 6769, 6770, 6771, 6772, 6773\nThe lines below are copied verbatim from the retained source log. The complete log remains at the source path above; validation.json retains the command record and original log hash.\n  Parchwell.ArchitectureTests: 8 passed; checked report.\n  Parchwell.Kafka.Tests: 306 passed; checked report.\n  Parchwell.LoadGen.Tests: 262 passed; checked report.\n  Parchwell.Scenarios.Tests: 94 passed; checked report.\n  Parchwell.Verifier.Tests: 1093 passed; checked report.\nWarning control restore: passed\nWarning control valid counterpart: passed\nWarning-as-error control: valid counterpart built; intended CS1030 error rejected.\nVerification passed. Per-project reports identify the tests executed.\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/tool-restore.log` (SHA-256 `f32acacaadbccc1bd8588e6c00105e460719fb5fe6b0b3f4ada628b641c21b89`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Tool 'dotnet-stryker' (version '5.0.0') was restored. Available commands: dotnet-stryker\n\nRestore was successful.\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/tool-list.log` (SHA-256 `7a975b7f4598c1f44134e4a893ffa85058f00efcc35c89705a1610a63cbd5f78`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Package Id          Version      Commands            Manifest                                                   \n----------------------------------------------------------------------------------------------------------------\ndotnet-stryker      5.0.0        dotnet-stryker      /workspace/eventhubs-kafka-dotnet/.config/dotnet-tools.json\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/npm-ci.log` (SHA-256 `127980fa8adb22146136668362a44131e468647ba9b8e56a97197337e8205be4`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"npm warn deprecated glob@10.5.0: Old versions of glob are not supported, and contain widely publicized security vulnerabilities, which have been fixed in the current version. Please update. Support for old versions may be purchased (at exorbitant rates) by contacting i@izs.me\n\nadded 191 packages in 936ms\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/docs-tests.log` (SHA-256 `ac3c32c9fa1880b878df89459c4ea15484855f64e3b784da51e274c68cb4894a`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"\n> test:docs\n> node --test scripts/tests/docs-check.test.mjs\n\n\u2714 recorded mistake classes are reported in an advisory scan (240.099587ms)\n\u2714 valid prose and Markdown examples do not produce false identifier findings (365.564456ms)\n\u2714 document source namespaces are local and exact (354.027386ms)\n\u2714 discovery excludes pinned skills and untracked documents, but includes owned and uppercase Markdown (282.651707ms)\n\u2714 broken configuration fails the scan rather than appearing successful (185.119492ms)\n\u2714 scan is read-only and external links require no successful response (201.19204ms)\n\u2714 image descriptions are checked for punctuation in direct and reference images (246.88487ms)\n\u2714 only autolinks and exactly matching link labels are excluded from prose scans (210.629083ms)\n\u2714 source descriptions are declarations in both tables and lists but still check punctuation (205.940981ms)\n\u2714 split definition identifiers do not resolve references, while whole formatted identifiers do (241.594544ms)\n\u2714 source ID cell S999 ![icon](icon.png) does not define a source (227.848262ms)\n\u2714 source ID cell S999 ![icon][picture] does not define a source (209.180556ms)\n\u2714 source ID cell S999 `extra` does not define a source (213.202129ms)\n\u2714 source ID cell `S999` does not define a source (231.308407ms)\n\u2714 source ID cell **S999 ![icon](icon.png)** does not define a source (232.385174ms)\n\u2714 source ID cell S999 extra does not define a source (210.330166ms)\n\u2714 whole source ID cell S999 resolves its reference (221.037433ms)\n\u2714 whole source ID cell **S999** resolves its reference (202.681059ms)\n\u2714 whole source ID cell *S999* resolves its reference (196.508579ms)\n\u2714 whole source ID cell ~~S999~~ resolves its reference (214.138295ms)\n\u2714 whole source ID cell ***S999*** resolves its reference (198.41771ms)\n\u2714 whole source ID cell [S999](https://example.invalid) resolves its reference (233.863191ms)\n\u2714 ordinary undefined source references are reported without a definition (218.937068ms)\n\u2714 non-text content in link labels cannot make them equal their destinations (211.908803ms)\n\u2714 non-text content separates ADR heading text without hiding a valid prefix (207.411852ms)\n\u2714 non-text content cannot create a source list declaration prefix (210.842444ms)\n\u2714 Unicode word characters do not delimit identifier references or ADR declarations (226.948839ms)\n\u2139 tests 27\n\u2139 suites 0\n\u2139 pass 27\n\u2139 fail 0\n\u2139 cancelled 0\n\u2139 skipped 0\n\u2139 todo 0\n\u2139 duration_ms 6270.060279\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-validation/docs-check.log` (SHA-256 `1fccbd25a0eb178f07601adbe849619b2aee26abe27cdfc26f3162d759d17c5d`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"\n> check:docs\n> node scripts/check-docs.mjs\n\n.agents/skills/repo-review/SKILL.md: no issues found\n.claude/agents/repo-reviewer.md: no issues found\n.github/pull_request_template.md: no issues found\nAGENTS.md: no issues found\nCLAUDE.md: no issues found\nCONTEXT.md: no issues found\ndocs/agents/factory.md: no issues found\ndocs/blueprint.md: no issues found\ndocs/engineering/code-comments.md: no issues found\ndocs/engineering/documentation-checks.md: no issues found\ndocs/engineering/dotnet-verification.md: no issues found\ndocs/engineering/dotnet.md: no issues found\ndocs/engineering/terraform.md: no issues found\ndocs/networking/hub-firewall.md: no issues found\ndocs/operations/kafka-authentication.md: no issues found\ndocs/operations/kafka-baseline.md: no issues found\ndocs/operations/kafka-configuration-explained.md: no issues found\ndocs/operations/loadgen-contract.md: no issues found\ndocs/operations/phantom-spike/README.md: no issues found\ndocs/operations/scenario-contract.md: no issues found\ndocs/operations/statistics-bridge.md: no issues found\ndocs/operations/verifier-contract.md: no issues found\ndocs/operations/verifier-mutation.md: no issues found\ndocs/research/kubernetes-kafka-clients.md: no issues found\nREADME.md: no issues found\ntests/fixtures/verifier-v1/README.md: no issues found\nDocumentation scan completed. Warnings are advisory; external URLs were not checked.\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md` (SHA-256 `fea2426c186d9e3393acccd60c0d859360b6b18cab418442bf79e0194232933e`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"# Stryker test metadata reconciliation\n\nInputs: `/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json`; `/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results.xml`; `/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/command.log`.\n\n## Reconciliation\n\n- Stryker `testFiles` test metadata rows: 1093 across 74 exact source/type/method groups.\n- Ordinary xUnit test rows: 1093; assembly counts: `{\"errors\": 0, \"failed\": 0, \"not-run\": 0, \"passed\": 1093, \"skipped\": 0, \"total\": 1093}`.\n- Exact source path + type + method multiplicities match for all 74 groups; every XML source line falls within its Stryker source span.\n- Stryker `testFiles` entries are inventory metadata; this reconciliation does not infer Stryker test outcomes or coverage from those entries.\n\n## Coverage relay warning IDs from the supplied command log\n\nThe script extracted 2 IDs from the log. Each occurs in the Stryker inventory and reconciles to an exact xUnit source/type/method group.\n\n| ID from command log | Stryker inventory test name | Exact source/type/method group | Stryker span | xUnit rows in group |\n| --- | --- | --- | --- | ---: |\n| `4fd698566276569298a5d5f0e7c59ab3b89d77919f831d43e0321e0335b3bd58` | `Parchwell.Verifier.Tests.Host.VerifierHostTests.Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124(System.String)` | `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs :: Parchwell.Verifier.Tests.Host.VerifierHostTests.Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124` | `414:5\u2013456:6` | 2 |\n| `a5bc4404fb7cbd1d315169a41e71c21563ab88f526beda546eebf8608f3c087c` | `Parchwell.Verifier.Tests.Host.VerifierHostTests.Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124(System.String)` | `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs :: Parchwell.Verifier.Tests.Host.VerifierHostTests.Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124` | `414:5\u2013456:6` | 2 |\n\n## Exact method-group multiplicities and spans\n\n| Source file | Type | Method | Rows | Stryker source span |\n| --- | --- | --- | ---: | --- |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_AnyConfiguredModeWithoutHandler_When_WorkerRuns_Then_ExitsNonzeroWithoutOutput` | 3 | `336:5\u2013347:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_BlankRequiredTextOption_When_Validated_Then_ReportsStartupFailure` | 42 | `105:5\u2013113:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_DuplicateRepeatResultPaths_When_OptionsAreValidated_Then_PreservesThemForCheck32` | 1 | `386:5\u2013396:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ExpectedIdentityCardinalityOtherThanFive_When_OptionsAreValidated_Then_PreservesItForCheck32` | 4 | `398:5\u2013412:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_InvalidBlobContainerName_When_Validated_Then_ReportsStartupFailure` | 24 | `157:5\u2013168:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_LiteralScenarioLoadOptionsConfigurationKeys_When_Bound_Then_UsesAllParserLimits` | 1 | `70:5\u201390:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MalformedDuration_When_Bound_Then_ReportsStartupFailure` | 1 | `245:5\u2013255:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MalformedInactiveBudget_When_HostBindsOptions_Then_IgnoresTheValue` | 8 | `283:5\u2013315:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MissingOrUnknownMode_When_HostStarts_Then_ExitsNonzeroBeforeIo` | 4 | `23:5\u201339:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MissingRequiredOption_When_Validated_Then_ReportsStartupFailure` | 99 | `92:5\u2013103:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_NonpositiveRequiredBudget_When_Validated_Then_ReportsStartupFailure` | 38 | `219:5\u2013230:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_NonpositiveRequiredLimit_When_Validated_Then_ReportsStartupFailure` | 62 | `232:5\u2013243:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProbeGateMode_When_HostBuilds_Then_RequiresNoKafkaOrAzureServices` | 1 | `317:5\u2013334:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProcessLifetimeAtBudgetSum_When_Validated_Then_RejectsEqualityAndAcceptsGreaterValue` | 1 | `257:5\u2013281:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124` | 2 | `414:5\u2013456:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_RelativeBlobServiceUri_When_Validated_Then_ReportsStartupFailure` | 2 | `115:5\u2013124:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_UnexpectedRepeatInputCardinality_When_OptionsAreValidated_Then_PreservesItForFutureGateJudgement` | 4 | `370:5\u2013384:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ValidBlobContainerName_When_Validated_Then_AllowsIt` | 21 | `135:5\u2013146:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_WorkerExceptionCancellationOrHostStop_When_ExecutionEnds_Then_DoesNotReportSuccess` | 3 | `349:5\u2013368:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierWatchdogChildProcessTests` | `Given_ChildWatchdogMarker_When_Run_Then_UsesProductionHardExit` | 1 | `764:5\u2013785:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/GoldenArtifactReaderTests.cs` | `Parchwell.Verifier.Tests.Input.GoldenArtifactReaderTests` | `Given_GoldenArtifacts_When_IndependentReadersParse_Then_PreserveSchemaOneEvidence` | 1 | `11:5\u2013163:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_DuplicatePropertyAtNestedDepth_When_Read_Then_ReportsDuplicateToOwner` | 217 | `426:5\u2013445:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InputExceedsConfiguredLimits_When_Read_Then_ThrowsExecutionLimitWithoutCheckRouting` | 1 | `784:5\u2013853:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InvalidEscapedSurrogateInPropertyOrValue_When_Read_Then_RoutesAsMalformedJson` | 30 | `529:5\u2013545:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InvalidUtf8TruncatedOrBadJsonlFraming_When_Read_Then_RoutesEnvelopeProblem` | 1 | `447:5\u2013475:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_LfOnlyOrUnterminatedJsonl_When_Read_Then_RoutesFramingProblemToEachReader` | 1 | `502:5\u2013527:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MalformedIndex_When_Read_Then_ReturnsExecutionFailure` | 2 | `889:5\u2013903:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MalformedRawJsonInValidStatisticsEnvelope_When_Read_Then_EnvelopeAndNativeProblemsStaySeparate` | 1 | `771:5\u2013782:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingIndex_When_Read_Then_ReturnsExecutionFailure` | 1 | `874:5\u2013887:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingInputSource_When_Read_Then_RoutesAbsenceToItsOwner` | 1 | `855:5\u2013872:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingOrNullCompletionReceipt_When_CapabilitiesAreRead_Then_PreservesPresenceAndFalseValues` | 1 | `34:5\u201376:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_NativeRawReportHasIndependentShape_When_Read_Then_AllowsExtraFieldsAndRoutesNativeProblems` | 1 | `650:5\u2013769:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_NativeWrongKindAndMissingBrokerFields_When_Read_Then_PreservesBothRoutesWithoutValue` | 1 | `623:5\u2013648:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_NullCallbackPersistenceAndNullableLifecycleReason_When_Read_Then_PreservesNullWithoutValidityDecision` | 1 | `78:5\u201396:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_OnlyHarnessOwnedScalarValues_When_Read_Then_UsesExactWriterSetsAndKeepsOrdinaryStringsFree` | 1 | `98:5\u2013146:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_ReorderedPropertiesAndUninterpretedEvidence_When_Read_Then_PreservesDataWithoutSemanticGates` | 1 | `148:5\u2013266:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_RequiredPropertyHasWrongJsonKind_When_Read_Then_RoutesProblemToOwningCheck` | 217 | `24:5\u201332:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_RequiredPropertyMissing_When_Read_Then_RoutesProblemToOwningCheck` | 217 | `14:5\u201322:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_StartLineContainerHasUnknownOrDuplicateProperty_When_Read_Then_RoutesToCheckSeven` | 6 | `382:5\u2013412:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_StartLineRequiredPropertyInvalid_When_Read_Then_RoutesProblemToCheckSeven` | 32 | `304:5\u2013316:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_UnknownHarnessField_When_EnvelopeIsRead_Then_RejectsItAtEachClosedShape` | 1 | `330:5\u2013380:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_UnsupportedHarnessScalarsOrSchema_When_Read_Then_RoutesStructuralProblem` | 1 | `268:5\u2013302:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_ValidEscapedSurrogatePairInValuesAndOpenMapKeys_When_Read_Then_PreservesDecodedText` | 1 | `559:5\u2013574:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_ZeroByteJsonl_When_Read_Then_ReturnsEmptyRowsForEachReader` | 1 | `477:5\u2013500:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_A01RecordedEvidence_When_RoundTripped_Then_PreservesSelectedInt64FieldsAndOmissions` | 1 | `502:5\u2013547:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_AbsentAndObservedEmptyBoundaries_When_RoundTripped_Then_PreservesTheDistinction` | 1 | `228:5\u2013280:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_AssessmentSnippetExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit` | 1 | `628:5\u2013637:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_BrokerCountLowerBoundFalse_When_Read_Then_ReturnsStructuralProblem` | 1 | `452:5\u2013462:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_BrokerCountProjection_When_RoundTripped_Then_PreservesOnlySuppliedMeasurements` | 1 | `411:5\u2013450:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResultWithObservedZeros_When_Serialized_Then_AllSixZeroCountsRemainPresent` | 1 | `62:5\u201373:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResultWithoutCounts_When_Serialized_Then_ThrowsCodecError` | 1 | `75:5\u201381:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResult_When_Serialized_Then_AllSixOutcomeKeysArePresent` | 1 | `37:5\u201360:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompletedDrain_When_RoundTripped_Then_EmitsOnlyDrainTimeInMilliseconds` | 2 | `110:5\u2013127:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ConfigurationFidelityDifferences_When_RoundTripped_Then_EmitsBoundedSnakeCaseProjection` | 1 | `464:5\u2013500:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_DiagnosticAndAssessmentExceedPerCheckLimit_When_Read_Then_ThrowsExecutionLimit` | 1 | `639:5\u2013651:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_DiagnosticsForDifferentChecks_When_Read_Then_JsonlRowLimitDoesNotApply` | 1 | `598:5\u2013614:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_FailedAndUnevaluatedAssessments_When_RoundTripped_Then_PreservesTheirDistinctStates` | 1 | `83:5\u2013108:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_HashAndBoundedDiagnostics_When_Serialized_Then_EmitsOnlyResultProjection` | 1 | `157:5\u2013187:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_InvalidButUsableProbeProjection_When_RoundTripped_Then_PreservesIndependentEvidence` | 1 | `364:5\u2013409:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_InvalidOrIncompleteResult_When_Serialized_Then_AllSixOutcomeKeysAreAbsent` | 2 | `21:5\u201335:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_MalformedStartLineNote_When_Read_Then_ReturnsStructuralProblemForFutureCheckSeven` | 1 | `351:5\u2013362:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_MissingNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem` | 1 | `315:5\u2013325:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_NegativeFractionalDuration_When_Serialized_Then_TruncatesTowardZero` | 1 | `571:5\u2013580:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_NullableDiagnosticFields_When_RoundTripped_Then_EmitsAndReadsExplicitNulls` | 1 | `282:5\u2013301:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_OutputExceedsConfiguredBytes_When_Serialized_Then_ThrowsExecutionLimit` | 1 | `549:5\u2013558:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_PartitionCountExceedsConfiguredMaximum_When_ReadStartLine_Then_ThrowsExecutionLimit` | 1 | `653:5\u2013662:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_PreBoundaryOrOutOfRangeOrIoTermination_When_Serialized_Then_BothDrainFieldsAreAbsent` | 2 | `144:5\u2013155:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ProducerAndVerifierVersionsDiffer_When_RoundTripped_Then_CopiesEachSourceSeparately` | 1 | `189:5\u2013226:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ResultExceedsAggregateEntryLimit_When_Read_Then_ThrowsExecutionLimit` | 1 | `582:5\u2013596:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_SafetyStopDuration_When_RoundTripped_Then_EmitsOnlyLabelledLowerBound` | 1 | `129:5\u2013142:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_StartLineNote_When_RoundTripped_Then_PreservesOffsetsWithoutSelfHash` | 1 | `327:5\u2013349:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_UnicodeDiagnosticExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit` | 1 | `616:5\u2013626:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_UnicodeDiagnosticExceedsUtf8Limit_When_Serialized_Then_ThrowsExecutionLimit` | 1 | `560:5\u2013569:6` |\n| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_WrongKindNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem` | 1 | `303:5\u2013313:6` |\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation-command.log` (SHA-256 `8c33081d0b3b83263264038ff666789f15ffc59cc9a06f57d5612efe05ae1790`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Wrote /workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md\nStryker inventory rows=1093; xUnit rows=1093; exact method groups=74; command-log warning IDs=2\nInput SHA-256: report=782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df xml=47689e2e6bdde0e296ec259ff560af87493039d9d025ab04909e431f4e9849b8 log=e661843940b1efc481b453efae3368a2fe4781d5d808c06158c5b1281cdf8f76\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.log` (SHA-256 `c4dbaf14f8fc877d690ff4ad91bad0d0dd6fc707e0b382135f910029814d9c3d`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"Specify --help for a list of available options and commands.\nMissing value for option 'version'\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.json` (SHA-256 `3316ee96772245f49f72b7a9751f64786615055067d52d2c649ec7588df15065`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"{\n  \"command\": [\n    \"dotnet\",\n    \"stryker\",\n    \"--version\"\n  ],\n  \"cwd\": \"/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests\",\n  \"started_utc\": \"2026-10-18T11:38:03.563800+00:00\",\n  \"ended_utc\": \"2026-10-18T11:38:03.953428+00:00\",\n  \"exit_code\": 1,\n  \"expected_exit_code\": 1,\n  \"log\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.log\",\n  \"log_sha256\": \"c4dbaf14f8fc877d690ff4ad91bad0d0dd6fc707e0b382135f910029814d9c3d\",\n  \"pinned_tool_version\": \"5.0.0\",\n  \"tool_version_evidence\": {\n    \"path\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/tool-list.log\",\n    \"sha256\": \"7a975b7f4598c1f44134e4a893ffa85058f00efcc35c89705a1610a63cbd5f78\"\n  },\n  \"output\": \"Specify --help for a list of available options and commands.\\nMissing value for option 'version'\\n\"\n}\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/run-original-wrapper.json` (SHA-256 `86e6c1ea360e94603ac6fa3dcd80cb123c3b9e165de869e78abca2a2c8f63e04`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"{\n  \"sha\": \"0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90\",\n  \"tree\": \"52706c46059bcab835f2190e228ac70701ab7a89\",\n  \"command\": [\n    \"dotnet\",\n    \"stryker\",\n    \"--config-file\",\n    \"stryker-config.json\",\n    \"--output\",\n    \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66\"\n  ],\n  \"cwd\": \"/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests\",\n  \"started_utc\": \"2026-10-18T11:03:31.662029+00:00\",\n  \"validation_json_sha256\": \"5a8b50d7e958328718296b218e97db100d9ff8f7204445d7d96656ebfc5a1f9e\",\n  \"ended_utc\": \"2026-10-18T11:38:03.154506+00:00\",\n  \"elapsed_seconds\": 2071.492,\n  \"exit_code\": 0,\n  \"command_log_sha256\": \"e661843940b1efc481b453efae3368a2fe4781d5d808c06158c5b1281cdf8f76\",\n  \"report\": {\n    \"json_path\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json\",\n    \"json_sha256\": \"782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df\",\n    \"html_path\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.html\",\n    \"html_sha256\": \"45c5051416b5ddf533011dc8de70835e83202654016f22a3e71172fc395e9d07\",\n    \"source_file_entries\": 18,\n    \"mutant_records\": 3877,\n    \"statuses\": {\n      \"Killed\": 2578,\n      \"Survived\": 618,\n      \"Timeout\": 7,\n      \"CompileError\": 257,\n      \"Ignored\": 417,\n      \"NoCoverage\": 0,\n      \"RuntimeError\": 0,\n      \"Pending\": 0\n    },\n    \"score_percent\": 80.71\n  },\n  \"warnings\": {\n    \"warning_lines\": [],\n    \"h7_dubious_coverage_relay_ids\": []\n  },\n  \"test_metadata_reconciliation\": {\n    \"command\": [\n      \"/opt/codex/runtimes/codex-primary-runtime/dependencies/python/bin/python3\",\n      \"/workspace/scratch/reconcile_verifier_resume.py\",\n      \"--report\",\n      \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json\",\n      \"--xml\",\n      \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results.xml\",\n      \"--log\",\n      \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/command.log\",\n      \"--output\",\n      \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md\"\n    ],\n    \"exit_code\": 0,\n    \"output\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md\",\n    \"output_sha256\": \"fea2426c186d9e3393acccd60c0d859360b6b18cab418442bf79e0194232933e\",\n    \"log\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation-command.log\"\n  },\n  \"version_probe\": {\n    \"metadata\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.json\",\n    \"metadata_sha256\": \"3316ee96772245f49f72b7a9751f64786615055067d52d2c649ec7588df15065\",\n    \"log\": \"/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.log\",\n    \"log_sha256\": \"c4dbaf14f8fc877d690ff4ad91bad0d0dd6fc707e0b382135f910029814d9c3d\"\n  }\n}\n"
```
### `.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/tested-content.txt` (SHA-256 `730927e90dc06d7c3b39be0f42dbedc00ea3373fe7b231de49d3482e91ca6a43`)

Exact UTF-8 text represented as a JSON string; escapes preserve line breaks, trailing whitespace, and non-ASCII characters.

```json
"0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90\n52706c46059bcab835f2190e228ac70701ab7a89\n"
```

### Complete run metadata

The complete final wrapper record is reproduced from `run.json`; its `started_utc`, `ended_utc`, `elapsed_seconds`, `exit_code`, command, tested tree, and report hashes remain visible here.

```json
{
  "sha": "0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90",
  "tree": "52706c46059bcab835f2190e228ac70701ab7a89",
  "command": [
    "dotnet",
    "stryker",
    "--config-file",
    "stryker-config.json",
    "--output",
    "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66"
  ],
  "cwd": "/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests",
  "started_utc": "2026-10-18T11:03:31.662029+00:00",
  "validation_json_sha256": "5a8b50d7e958328718296b218e97db100d9ff8f7204445d7d96656ebfc5a1f9e",
  "ended_utc": "2026-10-18T11:38:03.154506+00:00",
  "elapsed_seconds": 2071.492,
  "exit_code": 0,
  "command_log_sha256": "e661843940b1efc481b453efae3368a2fe4781d5d808c06158c5b1281cdf8f76",
  "report": {
    "json_path": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json",
    "json_sha256": "782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df",
    "html_path": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.html",
    "html_sha256": "45c5051416b5ddf533011dc8de70835e83202654016f22a3e71172fc395e9d07",
    "source_file_entries": 18,
    "mutant_records": 3877,
    "statuses": {
      "Killed": 2578,
      "Survived": 618,
      "Timeout": 7,
      "CompileError": 257,
      "Ignored": 417,
      "NoCoverage": 0,
      "RuntimeError": 0,
      "Pending": 0
    },
    "score_percent": 80.71
  },
  "warnings": {
    "warning_lines": [
      "[11:03:33 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj. Assuming selected framework (net10.0) is present.",
      "[11:03:34 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj. Assuming selected framework (net10.0) is present.",
      "[11:03:39 WRN] The Microsoft Test Platform testrunner is currently in preview. Results should be verified since this feature is still being tested.",
      "[11:04:44 WRN] MtpRunner-0: Timed out waiting for coverage relay ack for test 4fd698566276569298a5d5f0e7c59ab3b89d77919f831d43e0321e0335b3bd58; marking as Dubious",
      "[11:04:54 WRN] MtpRunner-1: Timed out waiting for coverage relay ack for test a5bc4404fb7cbd1d315169a41e71c21563ab88f526beda546eebf8608f3c087c; marking as Dubious"
    ],
    "h7_dubious_coverage_relay_ids": [
      "4fd698566276569298a5d5f0e7c59ab3b89d77919f831d43e0321e0335b3bd58",
      "a5bc4404fb7cbd1d315169a41e71c21563ab88f526beda546eebf8608f3c087c"
    ]
  },
  "test_metadata_reconciliation": {
    "command": [
      "/opt/codex/runtimes/codex-primary-runtime/dependencies/python/bin/python3",
      "/workspace/scratch/reconcile_verifier_resume.py",
      "--report",
      "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/reports/mutation-report.json",
      "--xml",
      "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results.xml",
      "--log",
      "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/command.log",
      "--output",
      "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md"
    ],
    "exit_code": 0,
    "output": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation.md",
    "output_sha256": "fea2426c186d9e3393acccd60c0d859360b6b18cab418442bf79e0194232933e",
    "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/test-metadata-reconciliation-command.log"
  },
  "version_probe": {
    "metadata": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.json",
    "metadata_sha256": "3316ee96772245f49f72b7a9751f64786615055067d52d2c649ec7588df15065",
    "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/stryker-version-probe.log",
    "log_sha256": "c4dbaf14f8fc877d690ff4ad91bad0d0dd6fc707e0b382135f910029814d9c3d"
  },
  "warning_extraction_correction": {
    "reason": "Initial wrapper filter searched for literal [WRN], while Stryker uses timestamped [HH:mm:ss WRN] lines.",
    "source_log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/command.log",
    "regex": "\\bWRN\\]",
    "source_log_sha256": "e661843940b1efc481b453efae3368a2fe4781d5d808c06158c5b1281cdf8f76",
    "original_run_json_backup": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-run.pt82vy66/run-original-wrapper.json",
    "original_run_json_sha256": "86e6c1ea360e94603ac6fa3dcd80cb123c3b9e165de869e78abca2a2c8f63e04",
    "corrected_warning_lines": 5,
    "corrected_dubious_relay_ids": 2
  }
}
```

### Validation records

#### `.artifacts/verifier-mutation/round2-review-fix-validation/validation.json` (SHA-256 `5a8b50d7e958328718296b218e97db100d9ff8f7204445d7d96656ebfc5a1f9e`)

```json
{
  "expected_sha": "0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90",
  "started_utc": "2026-10-18T10:57:33.776143+00:00",
  "commands": [
    {
      "name": "locked-restore",
      "command": [
        "dotnet",
        "restore",
        "Parchwell.slnx",
        "--locked-mode"
      ],
      "started_utc": "2026-10-18T10:57:33.789681+00:00",
      "ended_utc": "2026-10-18T10:57:36.828706+00:00",
      "elapsed_seconds": 3.039,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/locked-restore.log",
      "log_sha256": "b3e03ae18735bb5229ce1346f601c3429e93710ba35f7bfb6be0849356438084"
    },
    {
      "name": "solution-build",
      "command": [
        "dotnet",
        "build",
        "Parchwell.slnx",
        "--configuration",
        "Release",
        "--no-restore"
      ],
      "started_utc": "2026-10-18T10:57:36.829112+00:00",
      "ended_utc": "2026-10-18T10:57:53.789736+00:00",
      "elapsed_seconds": 16.961,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/solution-build.log",
      "log_sha256": "ab3c3b8a6f31add0a2a4f8853aa1c80ff479fb4dd2bddf60a7caa18888f0184f"
    },
    {
      "name": "targeted-tests",
      "command": [
        "dotnet",
        "test",
        "--project",
        "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj",
        "--configuration",
        "Release",
        "--no-build",
        "--minimum-expected-tests",
        "1",
        "--results-directory",
        "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results",
        "--",
        "--report-xunit-xml",
        "--report-xunit-xml-filename",
        "results.xml"
      ],
      "started_utc": "2026-10-18T10:57:53.790292+00:00",
      "ended_utc": "2026-10-18T10:57:58.325743+00:00",
      "elapsed_seconds": 4.535,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-tests.log",
      "log_sha256": "390337b588f784190638d3a6a98430fcd96f4085514edcdd889d91a9fb67e73b"
    },
    {
      "name": "targeted-report",
      "command": [
        "dotnet",
        "msbuild",
        "scripts/dotnet-verification.proj",
        "-t:ValidateReport",
        "-p:Report=/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results/results.xml",
        "-p:ExpectedAssembly=Parchwell.Verifier.Tests",
        "-nologo"
      ],
      "started_utc": "2026-10-18T10:57:58.326206+00:00",
      "ended_utc": "2026-10-18T10:57:59.283825+00:00",
      "elapsed_seconds": 0.958,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-report.log",
      "log_sha256": "ed15cf2622bc3f061246329affc24e28bb2bde4617506d0a781a69d0e6bf4c71"
    },
    {
      "name": "full-verification",
      "command": [
        "./scripts/verify-dotnet.sh"
      ],
      "started_utc": "2026-10-18T10:57:59.297823+00:00",
      "ended_utc": "2026-10-18T11:03:07.877887+00:00",
      "elapsed_seconds": 308.58,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/full-verification.log",
      "log_sha256": "9c50ea07c754103b454c330d3fc967eb91f91f2620c6d3cca3670d127a204c50"
    },
    {
      "name": "tool-restore",
      "command": [
        "dotnet",
        "tool",
        "restore"
      ],
      "started_utc": "2026-10-18T11:03:08.016602+00:00",
      "ended_utc": "2026-10-18T11:03:08.264124+00:00",
      "elapsed_seconds": 0.247,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/tool-restore.log",
      "log_sha256": "f32acacaadbccc1bd8588e6c00105e460719fb5fe6b0b3f4ada628b641c21b89"
    },
    {
      "name": "tool-list",
      "command": [
        "dotnet",
        "tool",
        "list",
        "--local"
      ],
      "started_utc": "2026-10-18T11:03:08.265347+00:00",
      "ended_utc": "2026-10-18T11:03:08.449695+00:00",
      "elapsed_seconds": 0.184,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/tool-list.log",
      "log_sha256": "7a975b7f4598c1f44134e4a893ffa85058f00efcc35c89705a1610a63cbd5f78"
    },
    {
      "name": "npm-ci",
      "command": [
        "npm",
        "ci",
        "--ignore-scripts"
      ],
      "started_utc": "2026-10-18T11:03:08.450198+00:00",
      "ended_utc": "2026-10-18T11:03:09.502557+00:00",
      "elapsed_seconds": 1.052,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/npm-ci.log",
      "log_sha256": "127980fa8adb22146136668362a44131e468647ba9b8e56a97197337e8205be4"
    },
    {
      "name": "docs-tests",
      "command": [
        "npm",
        "run",
        "test:docs"
      ],
      "started_utc": "2026-10-18T11:03:09.503922+00:00",
      "ended_utc": "2026-10-18T11:03:15.926557+00:00",
      "elapsed_seconds": 6.423,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/docs-tests.log",
      "log_sha256": "ac3c32c9fa1880b878df89459c4ea15484855f64e3b784da51e274c68cb4894a"
    },
    {
      "name": "docs-check",
      "command": [
        "npm",
        "run",
        "check:docs"
      ],
      "started_utc": "2026-10-18T11:03:15.928082+00:00",
      "ended_utc": "2026-10-18T11:03:19.769845+00:00",
      "elapsed_seconds": 3.842,
      "exit_code": 0,
      "log": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/docs-check.log",
      "log_sha256": "1fccbd25a0eb178f07601adbe849619b2aee26abe27cdfc26f3162d759d17c5d"
    }
  ],
  "sha": "0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90",
  "tree": "52706c46059bcab835f2190e228ac70701ab7a89",
  "git_status": "clean",
  "lock_review": {
    "reviewed_baseline": "610625f4fa19972c48c8d315442982542b257cd8",
    "baseline_lock_review_json": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/baseline-lock-review.json",
    "baseline_lock_review_diff": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/baseline-lock-review.diff",
    "round2_lock_files_changed_since_baseline": [],
    "restore_mode": "locked-mode"
  },
  "targeted_test_report": {
    "path": "/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/round2-review-fix-validation/targeted-results.xml",
    "sha256": "47689e2e6bdde0e296ec259ff560af87493039d9d025ab04909e431f4e9849b8",
    "counts": {
      "total": 1093,
      "passed": 1093,
      "failed": 0,
      "skipped": 0,
      "errors": 0,
      "not-run": 0
    }
  },
  "full_verification_test_reports": [
    {
      "source_path": "/tmp/parchwell-dotnet-verification.5URrDExk/Parchwell.ArchitectureTests/results.xml",
      "relative_path": "Parchwell.ArchitectureTests/results.xml",
      "sha256": "522214abd1712be512c3574cb4bb60a41a0c592e0b0cbdbe51d131df005cd396",
      "counts": {
        "total": 8,
        "passed": 8,
        "failed": 0,
        "skipped": 0,
        "errors": 0,
        "not-run": 0
      }
    },
    {
      "source_path": "/tmp/parchwell-dotnet-verification.5URrDExk/Parchwell.Kafka.Tests/results.xml",
      "relative_path": "Parchwell.Kafka.Tests/results.xml",
      "sha256": "b8018cbbc2b5e17015be231e1c08555920e65ba7dd1b392a866d7fafe331c62c",
      "counts": {
        "total": 306,
        "passed": 306,
        "failed": 0,
        "skipped": 0,
        "errors": 0,
        "not-run": 0
      }
    },
    {
      "source_path": "/tmp/parchwell-dotnet-verification.5URrDExk/Parchwell.LoadGen.Tests/results.xml",
      "relative_path": "Parchwell.LoadGen.Tests/results.xml",
      "sha256": "afebc6dc5910888bdb7b96df19f794c5eed4dd825633c3bdcb5f321aec4a54ff",
      "counts": {
        "total": 262,
        "passed": 262,
        "failed": 0,
        "skipped": 0,
        "errors": 0,
        "not-run": 0
      }
    },
    {
      "source_path": "/tmp/parchwell-dotnet-verification.5URrDExk/Parchwell.Scenarios.Tests/results.xml",
      "relative_path": "Parchwell.Scenarios.Tests/results.xml",
      "sha256": "667200696d425a915f514d39597078f5ccd8d30cf3461d070f00f03787294ad3",
      "counts": {
        "total": 94,
        "passed": 94,
        "failed": 0,
        "skipped": 0,
        "errors": 0,
        "not-run": 0
      }
    },
    {
      "source_path": "/tmp/parchwell-dotnet-verification.5URrDExk/Parchwell.Verifier.Tests/results.xml",
      "relative_path": "Parchwell.Verifier.Tests/results.xml",
      "sha256": "3a86e7e08710073b0b35e29ac2b2a3c5247fef5dbf4eec7ce396fbffdd55aab7",
      "counts": {
        "total": 1093,
        "passed": 1093,
        "failed": 0,
        "skipped": 0,
        "errors": 0,
        "not-run": 0
      }
    }
  ],
  "full_verification_report_count": 5,
  "ended_utc": "2026-10-18T11:03:19.770528+00:00"
}
```

### Complete status and ID inventory

The following compact JSON projection is embedded in this document, so the report population can be independently recounted without the ignored local artifact JSON. Each row contains one source/status group, its count, and the complete comma-separated ID list. Projection SHA-256: `107cd996ddd52fd9c5d32498604b4bdc9918ea4f2632c8dcc1feec7bfb83cffd`.

```json
{
  "tested_sha": "0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90",
  "report_sha256": "782fba5355bb644133bacdbe608dcc4a3b17cfdb99728b7758aa7534e99063df",
  "mutant_count": 3877,
  "status_counts": {
    "CompileError": 257,
    "Ignored": 417,
    "Killed": 2578,
    "Survived": 618,
    "Timeout": 7
  },
  "mutants_by_file_and_status": [
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs",
      "status": "CompileError",
      "count": 88,
      "ids": "67,70,106,109,136,137,248,249,311,314,355,363,373,393,396,399,420,423,426,429,459,480,483,531,582,583,584,585,586,587,691,696,698,702,704,712,716,720,724,728,732,735,738,741,744,848,851,855,863,864,865,867,869,872,876,879,883,893,894,895,897,899,902,905,908,911,914,915,916,917,918,919,954,955,965,966,969,970,986,998,1009,1016,1026,1033,1043,1070,1072,1074"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs",
      "status": "Ignored",
      "count": 118,
      "ids": "35,42,71,78,102,157,162,173,178,186,202,226,234,241,247,256,264,281,284,287,292,295,303,321,331,344,383,403,412,435,439,447,451,470,473,477,484,488,494,500,506,512,518,523,525,528,530,537,539,542,544,552,556,559,567,573,581,602,606,609,622,628,634,639,650,663,667,677,748,761,767,784,796,800,816,822,828,938,939,947,948,951,953,962,968,971,982,987,991,993,996,1002,1004,1007,1013,1015,1021,1024,1030,1032,1038,1041,1047,1053,1055,1058,1061,1063,1066,1069,1080,1082,1084,1093,1105,1108,1111,1114"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs",
      "status": "Killed",
      "count": 668,
      "ids": "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32,33,34,36,37,38,41,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,68,69,72,73,74,75,76,77,79,80,81,82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,103,104,105,107,108,110,111,114,117,118,119,122,123,124,125,128,129,132,133,138,139,140,143,144,145,146,147,148,149,150,151,152,153,156,158,161,163,167,168,169,172,176,177,179,180,181,182,185,189,190,191,192,193,194,195,196,197,198,199,200,201,203,204,205,206,207,208,209,210,211,212,214,215,216,217,220,221,222,223,224,233,242,244,251,253,257,258,259,260,262,265,266,267,268,269,270,271,272,273,274,275,276,277,278,279,280,282,283,285,286,288,289,290,291,293,294,296,297,298,299,300,301,302,304,305,306,307,308,309,310,312,313,315,316,317,318,319,320,322,323,324,325,326,327,328,329,330,332,333,334,335,336,337,338,339,340,341,342,343,345,346,347,348,349,350,351,352,353,354,356,357,358,360,362,364,365,367,368,369,371,372,374,375,376,377,378,379,380,381,382,384,385,386,387,388,389,390,391,392,394,395,397,398,400,401,402,404,405,406,407,408,409,410,411,413,414,415,416,417,418,419,421,422,424,425,427,428,430,431,432,433,434,436,437,438,440,441,442,443,444,445,446,448,449,450,452,453,454,455,456,457,458,460,461,462,463,464,465,466,467,468,469,471,472,474,475,476,478,479,481,482,486,487,492,493,498,499,504,505,510,511,516,517,522,524,529,535,536,538,543,548,551,553,554,555,561,562,563,565,566,571,572,577,580,588,589,590,591,592,593,594,595,596,597,598,599,601,603,604,605,610,611,612,613,614,615,616,617,618,620,621,626,627,632,633,638,640,642,643,644,646,647,649,651,652,653,654,655,657,658,661,662,669,670,671,672,673,675,676,681,682,684,685,687,690,692,693,694,697,703,708,709,710,711,717,719,725,726,733,736,739,742,745,747,749,750,751,752,753,754,755,756,757,759,760,765,766,771,772,774,775,777,778,780,781,783,785,786,787,788,789,791,792,794,795,802,803,804,805,806,807,808,809,810,811,812,814,815,820,821,826,827,832,833,835,836,838,839,841,842,844,847,849,850,857,858,859,860,861,866,868,875,877,878,884,885,886,887,888,889,890,891,892,896,898,903,904,906,909,912,920,921,922,924,925,926,928,929,930,932,934,937,940,941,942,943,945,949,950,952,956,957,958,959,960,964,967,972,974,975,976,977,978,979,980,981,984,985,988,992,994,995,997,1003,1005,1006,1008,1014,1020,1022,1023,1025,1027,1029,1031,1037,1039,1040,1042,1048,1049,1050,1052,1054,1057,1059,1060,1062,1064,1071,1073,1075,1077,1078,1079,1085,1086,1087,1088,1089,1090,1094,1095,1096,1097,1098,1099,1100,1101,1102,1103,1104,1106,1107,1109,1110,1115,1116,1117,1118,1119,1120,1121,1122"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs",
      "status": "Survived",
      "count": 250,
      "ids": "39,40,112,113,115,116,120,121,126,127,130,131,134,135,141,142,154,155,159,160,164,165,166,170,171,174,175,183,184,187,188,213,218,219,225,227,228,229,230,231,232,235,236,237,238,239,240,243,245,246,250,252,254,255,261,263,359,361,366,370,485,489,490,491,495,496,497,501,502,503,507,508,509,513,514,515,519,520,521,526,527,532,533,534,540,541,545,546,547,549,550,557,558,560,564,568,569,570,574,575,576,578,579,600,607,608,619,623,624,625,629,630,631,635,636,637,641,645,648,656,659,660,664,665,666,668,674,678,679,680,683,686,688,689,695,699,700,701,705,706,707,713,714,715,718,721,722,723,727,729,730,731,734,737,740,743,746,758,762,763,764,768,769,770,773,776,779,782,790,793,797,798,799,801,813,817,818,819,823,824,825,829,830,831,834,837,840,843,845,846,852,853,854,856,862,870,871,873,874,880,881,882,900,901,907,910,913,923,927,931,933,935,936,944,946,961,963,973,983,989,990,999,1000,1001,1010,1011,1012,1017,1018,1019,1028,1034,1035,1036,1044,1045,1046,1051,1056,1065,1067,1068,1076,1081,1083,1091,1092,1112,1113,1123"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/StrictJson.cs",
      "status": "CompileError",
      "count": 13,
      "ids": "1188,1189,1239,1242,1263,1278,1280,1300,1304,1306,1309,1322,1323"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/StrictJson.cs",
      "status": "Ignored",
      "count": 55,
      "ids": "1124,1130,1133,1142,1146,1148,1150,1152,1154,1156,1158,1160,1163,1165,1168,1172,1178,1183,1185,1191,1196,1211,1215,1217,1221,1224,1227,1232,1236,1243,1245,1246,1248,1252,1253,1257,1261,1270,1284,1290,1294,1297,1301,1305,1310,1312,1314,1325,1326,1329,1335,1338,1339,1342,1348"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/StrictJson.cs",
      "status": "Killed",
      "count": 126,
      "ids": "1125,1126,1127,1128,1129,1134,1135,1136,1137,1138,1139,1140,1141,1145,1151,1153,1157,1159,1161,1164,1166,1169,1170,1171,1173,1175,1176,1177,1179,1180,1181,1182,1184,1186,1187,1190,1192,1193,1194,1195,1199,1200,1202,1204,1205,1206,1207,1208,1209,1212,1214,1216,1218,1220,1222,1223,1225,1226,1228,1229,1230,1231,1233,1234,1237,1238,1240,1241,1244,1247,1249,1250,1251,1254,1255,1258,1259,1260,1262,1264,1265,1266,1267,1268,1269,1271,1273,1274,1275,1276,1277,1279,1281,1282,1283,1285,1286,1287,1288,1289,1291,1293,1295,1296,1298,1299,1302,1303,1307,1308,1311,1313,1315,1317,1318,1319,1320,1321,1324,1328,1332,1333,1334,1336,1345,1347"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/StrictJson.cs",
      "status": "Survived",
      "count": 33,
      "ids": "1131,1132,1143,1144,1147,1149,1155,1162,1167,1174,1197,1198,1201,1203,1210,1213,1219,1235,1256,1272,1292,1316,1327,1330,1331,1337,1340,1341,1343,1344,1346,1349,1350"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs",
      "status": "CompileError",
      "count": 8,
      "ids": "1411,1414,1460,1463,1495,1498,1539,1540"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs",
      "status": "Ignored",
      "count": 27,
      "ids": "1370,1376,1379,1389,1407,1418,1422,1449,1455,1458,1475,1492,1499,1503,1509,1515,1521,1527,1533,1538,1541,1552,1554,1556,1559,1562,1565"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs",
      "status": "Killed",
      "count": 156,
      "ids": "1351,1352,1353,1354,1355,1356,1357,1358,1359,1360,1361,1362,1363,1364,1365,1366,1367,1368,1369,1375,1380,1381,1382,1383,1384,1385,1386,1387,1388,1391,1392,1393,1394,1395,1396,1397,1398,1399,1400,1401,1402,1403,1404,1405,1406,1408,1409,1410,1412,1413,1415,1416,1417,1419,1421,1423,1424,1425,1426,1427,1428,1429,1431,1432,1433,1435,1436,1437,1439,1440,1441,1443,1444,1445,1447,1448,1450,1452,1454,1456,1459,1461,1462,1464,1465,1466,1468,1469,1470,1472,1476,1477,1478,1479,1480,1481,1482,1483,1484,1485,1486,1487,1488,1489,1490,1491,1493,1494,1496,1497,1500,1501,1502,1504,1506,1507,1508,1510,1512,1513,1514,1516,1518,1519,1520,1522,1524,1525,1526,1528,1530,1531,1532,1534,1536,1537,1542,1543,1544,1545,1546,1547,1548,1549,1550,1551,1553,1555,1558,1561,1566,1567,1568,1570,1572,1573"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs",
      "status": "Survived",
      "count": 33,
      "ids": "1371,1372,1373,1374,1377,1378,1390,1420,1430,1434,1438,1442,1446,1451,1453,1457,1467,1471,1473,1474,1505,1511,1517,1523,1529,1535,1557,1560,1563,1564,1569,1571,1574"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
      "status": "CompileError",
      "count": 13,
      "ids": "1623,1642,1643,1644,1645,1646,1648,1649,1651,1652,1656,1662,1671"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
      "status": "Ignored",
      "count": 24,
      "ids": "1575,1585,1586,1588,1593,1598,1600,1605,1611,1612,1615,1617,1622,1624,1627,1629,1631,1638,1639,1653,1659,1665,1669,1678"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
      "status": "Killed",
      "count": 32,
      "ids": "1578,1582,1583,1591,1610,1613,1637,1640,1654,1655,1657,1660,1661,1663,1666,1667,1672,1673,1674,1675,1676,1677,1679,1680,1681,1682,1683,1684,1685,1686,1687,1688"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
      "status": "Survived",
      "count": 41,
      "ids": "1576,1577,1579,1580,1581,1590,1592,1594,1595,1596,1597,1599,1601,1602,1603,1604,1606,1607,1608,1609,1614,1616,1618,1619,1620,1621,1625,1626,1628,1630,1632,1633,1634,1635,1636,1641,1658,1664,1668,1670,1689"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
      "status": "Timeout",
      "count": 5,
      "ids": "1584,1587,1589,1647,1650"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs",
      "status": "CompileError",
      "count": 1,
      "ids": "1695"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs",
      "status": "Ignored",
      "count": 1,
      "ids": "1690"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs",
      "status": "Killed",
      "count": 7,
      "ids": "1692,1693,1694,1696,1697,1698,1699"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs",
      "status": "Survived",
      "count": 1,
      "ids": "1691"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs",
      "status": "CompileError",
      "count": 6,
      "ids": "1785,1786,1855,1877,1894,1896"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs",
      "status": "Ignored",
      "count": 32,
      "ids": "1700,1703,1787,1822,1824,1836,1848,1863,1867,1872,1888,1890,1898,1900,1916,1921,1924,1931,1934,1935,1936,1940,1942,1947,1957,1963,1968,1973,1976,1978,1981,1983"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs",
      "status": "Killed",
      "count": 163,
      "ids": "1702,1706,1708,1710,1712,1714,1716,1718,1720,1721,1723,1725,1727,1728,1730,1732,1734,1736,1738,1740,1742,1744,1746,1748,1750,1752,1754,1756,1758,1760,1762,1763,1766,1768,1770,1772,1774,1776,1778,1779,1780,1782,1783,1784,1788,1790,1792,1794,1796,1798,1800,1802,1804,1806,1808,1810,1812,1814,1816,1818,1820,1823,1825,1827,1828,1830,1832,1834,1837,1838,1839,1840,1841,1842,1843,1844,1845,1846,1847,1849,1850,1851,1852,1853,1854,1856,1857,1858,1859,1860,1861,1862,1864,1868,1869,1870,1871,1873,1876,1878,1879,1880,1881,1882,1883,1884,1885,1886,1887,1889,1891,1893,1895,1897,1899,1904,1906,1908,1910,1912,1914,1917,1918,1919,1920,1922,1923,1928,1929,1930,1932,1937,1938,1939,1941,1943,1944,1945,1946,1948,1950,1951,1952,1953,1954,1955,1958,1959,1960,1961,1962,1964,1966,1967,1969,1970,1971,1972,1974,1977,1979,1982,1984"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs",
      "status": "Survived",
      "count": 85,
      "ids": "1701,1704,1705,1707,1709,1711,1713,1715,1717,1719,1722,1724,1726,1729,1731,1733,1735,1737,1739,1741,1743,1745,1747,1749,1751,1753,1755,1757,1759,1761,1764,1765,1767,1769,1771,1773,1775,1777,1781,1789,1791,1793,1795,1797,1799,1801,1803,1805,1807,1809,1811,1813,1815,1817,1819,1821,1826,1829,1831,1833,1835,1865,1866,1874,1875,1892,1901,1902,1903,1905,1907,1909,1911,1913,1915,1925,1926,1927,1933,1949,1956,1965,1975,1980,1985"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs",
      "status": "Ignored",
      "count": 12,
      "ids": "1987,1988,1990,1994,1999,2001,2003,2005,2007,2011,2013,2016"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs",
      "status": "Killed",
      "count": 6,
      "ids": "1986,1989,1992,1993,1995,1998"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs",
      "status": "Survived",
      "count": 12,
      "ids": "1991,1996,1997,2000,2002,2004,2008,2009,2010,2014,2015,2017"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs",
      "status": "Timeout",
      "count": 2,
      "ids": "2006,2012"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs",
      "status": "CompileError",
      "count": 18,
      "ids": "2043,2044,2076,2077,2175,2176,2177,2180,2181,2183,2184,2187,2188,2189,2192,2193,2195,2196"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs",
      "status": "Ignored",
      "count": 18,
      "ids": "2035,2046,2086,2115,2118,2123,2128,2132,2150,2152,2156,2159,2161,2165,2172,2199,2201,2206"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs",
      "status": "Killed",
      "count": 131,
      "ids": "2018,2019,2020,2021,2022,2023,2024,2025,2026,2027,2028,2029,2030,2031,2032,2033,2034,2036,2037,2038,2039,2040,2041,2042,2045,2047,2048,2049,2050,2051,2052,2053,2054,2055,2057,2058,2059,2060,2061,2062,2063,2064,2065,2066,2067,2068,2069,2070,2071,2072,2073,2074,2075,2078,2079,2080,2081,2082,2083,2084,2085,2087,2088,2089,2090,2091,2092,2093,2094,2095,2096,2097,2098,2099,2100,2101,2102,2103,2104,2105,2106,2107,2108,2109,2110,2111,2112,2113,2114,2116,2117,2122,2124,2126,2127,2129,2130,2131,2136,2137,2138,2139,2140,2141,2142,2143,2144,2145,2146,2147,2148,2149,2151,2155,2160,2162,2164,2166,2168,2169,2170,2171,2173,2182,2185,2194,2197,2200,2204,2205,2207"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs",
      "status": "Survived",
      "count": 24,
      "ids": "2056,2119,2120,2121,2125,2133,2134,2135,2153,2154,2157,2158,2163,2167,2174,2178,2179,2186,2190,2191,2198,2202,2203,2208"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedArtifacts.cs",
      "status": "Killed",
      "count": 2,
      "ids": "2209,2210"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs",
      "status": "CompileError",
      "count": 46,
      "ids": "2217,2218,2219,2221,2230,2231,2237,2238,2245,2246,2247,2249,2258,2259,2265,2266,2277,2278,2322,2323,2411,2412,2474,2475,2551,2552,2940,2943,3033,3034,3035,3038,3039,3041,3042,3045,3046,3047,3050,3051,3053,3054,3082,3083,3094,3095"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs",
      "status": "Ignored",
      "count": 61,
      "ids": "2211,2223,2226,2239,2251,2254,2271,2280,2332,2334,2348,2424,2486,2563,2588,2627,2688,2700,2715,2734,2752,2789,2803,2823,2838,2877,2903,2905,2909,2913,2917,2923,2925,2929,2935,2937,2947,2949,2953,2957,2965,2967,2973,2979,3013,3015,3019,3022,3024,3026,3030,3057,3059,3064,3067,3069,3076,3078,3087,3089,3099"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs",
      "status": "Killed",
      "count": 732,
      "ids": "2212,2213,2214,2215,2216,2220,2222,2224,2225,2229,2232,2233,2234,2235,2236,2240,2241,2242,2243,2244,2248,2250,2252,2253,2257,2260,2261,2262,2263,2264,2267,2268,2269,2270,2272,2273,2274,2275,2276,2279,2281,2282,2283,2284,2285,2286,2287,2288,2289,2290,2291,2292,2293,2294,2295,2296,2297,2298,2299,2300,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,2311,2312,2313,2314,2315,2316,2317,2318,2319,2320,2321,2324,2325,2326,2327,2328,2329,2330,2331,2333,2338,2339,2340,2341,2342,2343,2344,2349,2350,2351,2352,2353,2354,2355,2356,2357,2358,2359,2360,2361,2362,2363,2364,2365,2366,2367,2368,2369,2370,2371,2372,2373,2374,2375,2376,2377,2378,2379,2380,2381,2382,2383,2384,2385,2386,2387,2388,2389,2390,2391,2392,2393,2394,2395,2396,2397,2398,2399,2400,2401,2402,2403,2404,2405,2406,2407,2408,2409,2410,2413,2414,2415,2416,2417,2418,2419,2420,2421,2422,2423,2425,2426,2427,2428,2429,2430,2431,2432,2433,2434,2435,2436,2437,2438,2439,2440,2441,2442,2443,2444,2445,2446,2447,2448,2449,2450,2451,2452,2453,2454,2455,2456,2457,2458,2459,2460,2461,2462,2463,2464,2465,2466,2467,2468,2469,2470,2471,2472,2473,2476,2477,2478,2479,2480,2481,2482,2483,2484,2485,2487,2488,2489,2490,2491,2492,2493,2494,2495,2496,2497,2498,2499,2500,2501,2502,2503,2504,2505,2506,2507,2508,2509,2510,2511,2512,2513,2514,2515,2516,2517,2518,2519,2520,2521,2522,2523,2524,2525,2526,2527,2528,2529,2530,2531,2532,2533,2534,2535,2536,2537,2538,2539,2540,2541,2542,2543,2544,2545,2546,2547,2548,2549,2550,2553,2554,2555,2556,2557,2558,2559,2560,2561,2562,2564,2565,2566,2567,2568,2569,2570,2571,2572,2573,2574,2575,2576,2577,2578,2579,2580,2581,2582,2583,2584,2585,2586,2587,2589,2590,2591,2592,2593,2594,2595,2596,2597,2598,2599,2600,2601,2602,2603,2604,2605,2606,2607,2608,2609,2610,2611,2612,2613,2614,2615,2616,2617,2618,2619,2620,2621,2622,2623,2624,2625,2626,2628,2629,2630,2631,2632,2633,2634,2635,2636,2637,2638,2639,2640,2641,2642,2643,2644,2645,2646,2647,2648,2649,2650,2651,2652,2653,2654,2655,2656,2657,2658,2659,2660,2661,2662,2663,2664,2665,2666,2667,2668,2669,2670,2671,2672,2673,2674,2675,2676,2677,2678,2679,2680,2681,2682,2683,2684,2685,2686,2687,2689,2690,2691,2692,2693,2694,2695,2696,2697,2698,2699,2701,2702,2703,2704,2705,2706,2707,2708,2709,2710,2711,2712,2713,2714,2716,2717,2718,2719,2720,2721,2722,2723,2724,2725,2726,2727,2728,2729,2730,2731,2732,2733,2735,2736,2737,2738,2739,2740,2741,2742,2743,2744,2745,2746,2747,2748,2749,2750,2751,2753,2754,2755,2756,2757,2758,2759,2760,2761,2762,2763,2764,2765,2766,2767,2768,2769,2770,2771,2772,2773,2774,2775,2776,2777,2778,2779,2780,2781,2782,2783,2784,2785,2786,2787,2788,2790,2791,2792,2793,2794,2795,2796,2797,2798,2799,2800,2801,2802,2804,2805,2806,2807,2808,2809,2810,2811,2812,2813,2814,2815,2816,2817,2818,2819,2820,2821,2822,2824,2825,2826,2827,2828,2829,2830,2831,2832,2833,2834,2835,2836,2837,2839,2840,2841,2842,2843,2844,2845,2846,2847,2848,2849,2850,2851,2852,2853,2854,2855,2856,2857,2858,2859,2860,2861,2862,2863,2864,2865,2866,2867,2868,2869,2870,2871,2872,2873,2874,2875,2876,2878,2879,2880,2881,2882,2883,2884,2885,2886,2887,2888,2889,2890,2891,2892,2893,2894,2895,2896,2897,2898,2899,2900,2901,2902,2904,2908,2910,2912,2914,2915,2916,2921,2922,2924,2928,2930,2932,2933,2934,2936,2944,2945,2946,2948,2952,2954,2956,2958,2962,2963,2964,2966,2968,2971,2972,2974,2976,2977,2978,2980,2981,2982,2983,2984,2985,2986,2987,2988,2989,2990,2991,2992,2993,2994,2995,2996,2997,2998,2999,3000,3001,3002,3003,3004,3005,3006,3007,3008,3009,3010,3011,3012,3014,3018,3020,3023,3025,3027,3029,3031,3040,3043,3052,3055,3058,3062,3063,3065,3068,3072,3073,3074,3077,3081,3085,3088,3090,3093,3096,3097,3100,3101,3102,3103,3104,3106"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs",
      "status": "Survived",
      "count": 57,
      "ids": "2227,2228,2255,2256,2335,2336,2337,2345,2346,2347,2906,2907,2911,2918,2919,2920,2926,2927,2931,2938,2939,2941,2942,2950,2951,2955,2959,2960,2961,2969,2970,2975,3016,3017,3021,3028,3032,3036,3037,3044,3048,3049,3056,3060,3061,3066,3070,3071,3075,3079,3080,3084,3086,3091,3092,3098,3105"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs",
      "status": "CompileError",
      "count": 34,
      "ids": "3141,3142,3143,3145,3154,3157,3165,3166,3218,3219,3264,3265,3305,3306,3307,3310,3311,3313,3314,3317,3318,3319,3322,3323,3325,3326,3348,3349,3387,3388,3389,3390,3391,3392"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs",
      "status": "Ignored",
      "count": 23,
      "ids": "3134,3147,3150,3158,3161,3167,3176,3229,3276,3278,3283,3288,3290,3294,3297,3302,3329,3331,3336,3339,3345,3353,3359"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs",
      "status": "Killed",
      "count": 207,
      "ids": "3107,3108,3109,3110,3111,3112,3113,3114,3115,3116,3117,3118,3119,3120,3121,3122,3123,3124,3125,3126,3127,3128,3129,3130,3131,3132,3133,3135,3136,3137,3138,3139,3140,3144,3146,3148,3149,3153,3155,3156,3159,3160,3162,3163,3164,3168,3169,3170,3171,3172,3173,3174,3175,3177,3179,3180,3181,3182,3183,3184,3185,3186,3187,3188,3189,3190,3191,3192,3193,3194,3195,3196,3197,3198,3199,3200,3201,3202,3203,3204,3205,3206,3207,3208,3209,3210,3211,3212,3213,3214,3215,3216,3217,3220,3221,3222,3223,3224,3225,3226,3227,3228,3230,3231,3232,3233,3234,3235,3236,3237,3238,3239,3240,3241,3242,3243,3244,3245,3246,3247,3248,3249,3250,3251,3252,3253,3254,3255,3256,3257,3258,3259,3260,3261,3262,3263,3266,3267,3268,3269,3270,3271,3272,3273,3274,3275,3277,3279,3280,3281,3282,3289,3293,3295,3298,3299,3300,3301,3303,3312,3315,3324,3327,3330,3334,3335,3337,3340,3341,3342,3343,3346,3347,3350,3351,3354,3355,3356,3357,3360,3361,3362,3363,3364,3365,3366,3367,3368,3369,3370,3371,3372,3373,3374,3375,3376,3377,3378,3379,3380,3381,3382,3383,3384,3385,3386,3397"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs",
      "status": "Survived",
      "count": 27,
      "ids": "3151,3152,3178,3284,3285,3286,3287,3291,3292,3296,3304,3308,3309,3316,3320,3321,3328,3332,3333,3338,3344,3352,3358,3393,3394,3395,3396"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs",
      "status": "CompileError",
      "count": 20,
      "ids": "3440,3441,3499,3500,3672,3673,3674,3677,3678,3680,3681,3684,3685,3686,3689,3690,3692,3693,3709,3710"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs",
      "status": "Ignored",
      "count": 29,
      "ids": "3432,3443,3516,3544,3546,3550,3559,3563,3608,3610,3612,3616,3620,3628,3630,3634,3640,3646,3648,3652,3655,3657,3661,3664,3669,3696,3698,3703,3706"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs",
      "status": "Killed",
      "count": 229,
      "ids": "3398,3399,3400,3401,3402,3403,3404,3405,3406,3407,3408,3409,3410,3411,3412,3413,3414,3415,3416,3417,3418,3419,3420,3421,3422,3423,3424,3425,3426,3427,3428,3429,3430,3431,3433,3434,3435,3436,3437,3438,3439,3442,3444,3445,3446,3447,3448,3449,3450,3451,3452,3454,3455,3456,3457,3458,3459,3460,3461,3462,3463,3464,3465,3466,3467,3468,3469,3470,3471,3472,3473,3474,3475,3476,3477,3478,3479,3480,3481,3482,3483,3484,3485,3486,3487,3488,3489,3490,3491,3492,3493,3494,3495,3496,3497,3498,3501,3502,3503,3504,3505,3506,3507,3508,3509,3510,3511,3512,3513,3514,3515,3517,3518,3519,3520,3521,3522,3523,3524,3525,3526,3527,3528,3529,3530,3531,3532,3533,3534,3535,3536,3537,3538,3539,3540,3541,3542,3543,3545,3549,3551,3553,3554,3555,3556,3558,3560,3561,3562,3567,3568,3569,3570,3571,3572,3573,3574,3575,3576,3577,3578,3579,3580,3581,3582,3583,3584,3585,3586,3587,3588,3589,3590,3591,3592,3593,3594,3595,3596,3597,3598,3599,3600,3601,3602,3603,3604,3605,3606,3607,3609,3611,3615,3617,3619,3621,3625,3626,3627,3629,3633,3635,3637,3638,3639,3641,3645,3647,3651,3653,3656,3660,3662,3665,3666,3667,3668,3670,3679,3682,3691,3694,3697,3701,3702,3704,3707,3708,3712"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs",
      "status": "Survived",
      "count": 38,
      "ids": "3453,3547,3548,3552,3557,3564,3565,3566,3613,3614,3618,3622,3623,3624,3631,3632,3636,3642,3643,3644,3649,3650,3654,3658,3659,3663,3671,3675,3676,3683,3687,3688,3695,3699,3700,3705,3711,3713"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs",
      "status": "CompileError",
      "count": 10,
      "ids": "3724,3725,3753,3754,3824,3825,3832,3866,3867,3868"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs",
      "status": "Ignored",
      "count": 17,
      "ids": "3714,3727,3729,3731,3759,3762,3767,3779,3781,3788,3826,3828,3835,3855,3857,3863,3865"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs",
      "status": "Killed",
      "count": 119,
      "ids": "3715,3716,3717,3720,3721,3722,3723,3726,3728,3730,3735,3736,3737,3738,3739,3740,3741,3742,3743,3744,3745,3746,3747,3748,3749,3750,3751,3752,3755,3756,3757,3760,3761,3763,3765,3766,3768,3770,3771,3772,3773,3774,3775,3776,3778,3780,3786,3787,3789,3790,3791,3792,3793,3794,3795,3796,3797,3798,3799,3800,3801,3802,3803,3804,3805,3806,3807,3808,3809,3810,3811,3812,3813,3814,3815,3816,3817,3818,3819,3820,3821,3822,3823,3827,3829,3830,3831,3833,3834,3838,3839,3840,3841,3842,3843,3844,3845,3846,3847,3848,3849,3850,3851,3852,3853,3854,3856,3858,3859,3860,3861,3862,3864,3869,3871,3872,3873,3874,3876"
    },
    {
      "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs",
      "status": "Survived",
      "count": 17,
      "ids": "3718,3719,3732,3733,3734,3758,3764,3769,3777,3782,3783,3784,3785,3836,3837,3870,3875"
    }
  ]
}
```

The full machine-readable inventory is `/workspace/scratch/round2-v2-current-evidence-inventory.json` (SHA-256 `2ef15c645ac7de7fea331a01bfc86e93faa2a03d84517ea2ce599d28eccd2e26`).


## Historical PR 3 Survived and NoCoverage rows

These 618 current report rows are all exactly `not reviewed`. The historical ID column is a composite source/span/replacement match only; it is not evidence that current code was replayed or adjudicated. Newly uncovered source/mutator categories requiring sample gap fills: 0 source files and 0 mutator types.

| Current ID | Full source path | Exact span | Mutator | Replacement (JSON string) | Current report status | Historical sample reference only | Current replay state |
| ---: | --- | --- | --- | --- | --- | --- | --- |
| 39 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 38:13-38:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 39 (historical match only) | not reviewed |
| 40 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 38:41-38:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 40 (historical match only) | not reviewed |
| 112 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 106:96-106:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 112 (historical match only) | not reviewed |
| 113 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 106:114-106:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 113 (historical match only) | not reviewed |
| 115 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 107:58-107:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 115 (historical match only) | not reviewed |
| 116 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 107:76-107:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 116 (historical match only) | not reviewed |
| 120 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 110:107-110:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 120 (historical match only) | not reviewed |
| 121 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 110:128-110:145 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 121 (historical match only) | not reviewed |
| 126 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 113:103-113:120 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 126 (historical match only) | not reviewed |
| 127 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 113:122-113:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 127 (historical match only) | not reviewed |
| 130 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 114:126-114:160 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 130 (historical match only) | not reviewed |
| 131 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 114:162-114:179 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 131 (historical match only) | not reviewed |
| 134 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 115:126-115:155 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 134 (historical match only) | not reviewed |
| 135 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 115:157-115:174 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 135 (historical match only) | not reviewed |
| 141 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 117:38-117:46 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 141 (historical match only) | not reviewed |
| 142 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 117:48-117:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 142 (historical match only) | not reviewed |
| 154 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 131:71-131:83 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 154 (historical match only) | not reviewed |
| 155 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 131:85-131:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 155 (historical match only) | not reviewed |
| 159 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 134:108-134:120 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 159 (historical match only) | not reviewed |
| 160 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 134:122-134:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 160 (historical match only) | not reviewed |
| 164 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 140:111-140:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 164 (historical match only) | not reviewed |
| 165 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 140:136-140:153 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 165 (historical match only) | not reviewed |
| 166 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 141:17-141:84 | Logical mutation | <code>&quot;!hasLowerBound &amp;&amp; lowerBoundElement.ValueKind != JsonValueKind.True&quot;</code> | Survived | old ID 166 (historical match only) | not reviewed |
| 170 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 142:42-142:77 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 170 (historical match only) | not reviewed |
| 171 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 142:79-142:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 171 (historical match only) | not reviewed |
| 174 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 147:38-147:73 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 174 (historical match only) | not reviewed |
| 175 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 147:75-147:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 175 (historical match only) | not reviewed |
| 183 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 154:85-154:89 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 183 (historical match only) | not reviewed |
| 184 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 154:91-154:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 184 (historical match only) | not reviewed |
| 187 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 158:38-158:42 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 187 (historical match only) | not reviewed |
| 188 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 158:44-158:75 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 188 (historical match only) | not reviewed |
| 213 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 189:41-189:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 213 (historical match only) | not reviewed |
| 218 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 191:13-191:121 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 218 (historical match only) | not reviewed |
| 219 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 191:41-191:103 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 219 (historical match only) | not reviewed |
| 225 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 193:41-193:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 225 (historical match only) | not reviewed |
| 227 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 198:13-198:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 227 (historical match only) | not reviewed |
| 228 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 199:13-199:65 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 228 (historical match only) | not reviewed |
| 229 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 200:13-200:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 229 (historical match only) | not reviewed |
| 230 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 201:13-201:63 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 230 (historical match only) | not reviewed |
| 231 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 202:13-202:60 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 231 (historical match only) | not reviewed |
| 232 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 203:13-203:62 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 232 (historical match only) | not reviewed |
| 235 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 209:13-209:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 235 (historical match only) | not reviewed |
| 236 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 210:13-210:71 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 236 (historical match only) | not reviewed |
| 237 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 211:13-211:63 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 237 (historical match only) | not reviewed |
| 238 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 212:13-212:60 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 238 (historical match only) | not reviewed |
| 239 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 213:13-213:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 239 (historical match only) | not reviewed |
| 240 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 214:13-214:62 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 240 (historical match only) | not reviewed |
| 243 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 219:17-219:59 | Equality mutation | <code>&quot;item.Value &gt;= limits.MaxDiagnosticsPerCheck&quot;</code> | Survived | old ID 243 (historical match only) | not reviewed |
| 245 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 220:17-220:130 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 245 (historical match only) | not reviewed |
| 246 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 220:55-220:128 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 246 (historical match only) | not reviewed |
| 250 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 226:65-226:76 | Arithmetic mutation | <code>&quot;current - 1&quot;</code> | Survived | old ID 250 (historical match only) | not reviewed |
| 252 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 228:13-228:49 | Equality mutation | <code>&quot;next &gt;= limits.MaxDiagnosticsPerCheck&quot;</code> | Survived | old ID 252 (historical match only) | not reviewed |
| 254 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 229:13-229:126 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 254 (historical match only) | not reviewed |
| 255 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 229:51-229:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 255 (historical match only) | not reviewed |
| 261 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 234:34-234:95 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(value) &gt;= limits.MaxDiagnosticBytes&quot;</code> | Survived | old ID 261 (historical match only) | not reviewed |
| 263 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 235:51-235:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 263 (historical match only) | not reviewed |
| 359 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 322:47-322:106 | Conditional (false) mutation | <code>&quot;(false?\&quot;pass\&quot; :\&quot;fail\&quot;)&quot;</code> | Survived | old ID 359 (historical match only) | not reviewed |
| 361 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 322:91-322:97 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 361 (historical match only) | not reviewed |
| 366 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 324:50-324:127 | Conditional (true) mutation | <code>&quot;(true?\&quot;invalid\&quot; :\&quot;incomplete\&quot;)&quot;</code> | Survived | old ID 366 (historical match only) | not reviewed |
| 370 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 324:115-324:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 370 (historical match only) | not reviewed |
| 485 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 418:69-418:74 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 485 (historical match only) | not reviewed |
| 489 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 419:101-419:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 489 (historical match only) | not reviewed |
| 490 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 419:125-419:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 490 (historical match only) | not reviewed |
| 491 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 419:152-419:157 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 491 (historical match only) | not reviewed |
| 495 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 420:111-420:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 495 (historical match only) | not reviewed |
| 496 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 420:140-420:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 496 (historical match only) | not reviewed |
| 497 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 420:167-420:172 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 497 (historical match only) | not reviewed |
| 501 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 421:91-421:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 501 (historical match only) | not reviewed |
| 502 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 421:110-421:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 502 (historical match only) | not reviewed |
| 503 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 421:137-421:142 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 503 (historical match only) | not reviewed |
| 507 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 422:101-422:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 507 (historical match only) | not reviewed |
| 508 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 422:125-422:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 508 (historical match only) | not reviewed |
| 509 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 422:152-422:157 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 509 (historical match only) | not reviewed |
| 513 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 423:118-423:149 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 513 (historical match only) | not reviewed |
| 514 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 423:151-423:168 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 514 (historical match only) | not reviewed |
| 515 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 423:178-423:183 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 515 (historical match only) | not reviewed |
| 519 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 424:97-424:117 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 519 (historical match only) | not reviewed |
| 520 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 424:119-424:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 520 (historical match only) | not reviewed |
| 521 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 424:146-424:151 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 521 (historical match only) | not reviewed |
| 526 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 435:92-435:109 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 526 (historical match only) | not reviewed |
| 527 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 435:119-435:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 527 (historical match only) | not reviewed |
| 532 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 441:52-441:55 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 532 (historical match only) | not reviewed |
| 533 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 441:73-441:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 533 (historical match only) | not reviewed |
| 534 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 442:24-442:29 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 534 (historical match only) | not reviewed |
| 540 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 455:91-455:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 540 (historical match only) | not reviewed |
| 541 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 455:118-455:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 541 (historical match only) | not reviewed |
| 545 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 460:93-460:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 545 (historical match only) | not reviewed |
| 546 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 460:113-460:130 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 546 (historical match only) | not reviewed |
| 547 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 460:140-460:145 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 547 (historical match only) | not reviewed |
| 549 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 462:13-462:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 549 (historical match only) | not reviewed |
| 550 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 462:13-462:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 550 (historical match only) | not reviewed |
| 557 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 473:89-473:106 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 557 (historical match only) | not reviewed |
| 558 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 473:116-473:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 558 (historical match only) | not reviewed |
| 560 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 478:27-478:45 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 560 (historical match only) | not reviewed |
| 564 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 479:108-479:113 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 564 (historical match only) | not reviewed |
| 568 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 480:103-480:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 568 (historical match only) | not reviewed |
| 569 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 480:117-480:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 569 (historical match only) | not reviewed |
| 570 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 480:144-480:149 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 570 (historical match only) | not reviewed |
| 574 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 481:98-481:107 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 574 (historical match only) | not reviewed |
| 575 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 481:109-481:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 575 (historical match only) | not reviewed |
| 576 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 481:136-481:141 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 576 (historical match only) | not reviewed |
| 578 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 483:13-483:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 578 (historical match only) | not reviewed |
| 579 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 483:13-483:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 579 (historical match only) | not reviewed |
| 600 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 494:118-494:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 600 (historical match only) | not reviewed |
| 607 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 507:92-507:109 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 607 (historical match only) | not reviewed |
| 608 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 507:119-507:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 608 (historical match only) | not reviewed |
| 619 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 515:119-515:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 619 (historical match only) | not reviewed |
| 623 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 516:97-516:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 623 (historical match only) | not reviewed |
| 624 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 516:106-516:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 624 (historical match only) | not reviewed |
| 625 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 516:133-516:138 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 625 (historical match only) | not reviewed |
| 629 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 517:106-517:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 629 (historical match only) | not reviewed |
| 630 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 517:120-517:137 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 630 (historical match only) | not reviewed |
| 631 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 517:147-517:152 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 631 (historical match only) | not reviewed |
| 635 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 518:105-518:119 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 635 (historical match only) | not reviewed |
| 636 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 518:121-518:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 636 (historical match only) | not reviewed |
| 637 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 518:148-518:153 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 637 (historical match only) | not reviewed |
| 641 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 519:79-519:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 641 (historical match only) | not reviewed |
| 645 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 520:107-520:112 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 645 (historical match only) | not reviewed |
| 648 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 521:114-521:119 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 648 (historical match only) | not reviewed |
| 656 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 529:137-529:142 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 656 (historical match only) | not reviewed |
| 659 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 530:75-530:88 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 659 (historical match only) | not reviewed |
| 660 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 530:154-530:159 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 660 (historical match only) | not reviewed |
| 664 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 532:90-532:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 664 (historical match only) | not reviewed |
| 665 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 532:106-532:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 665 (historical match only) | not reviewed |
| 666 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 532:133-532:138 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 666 (historical match only) | not reviewed |
| 668 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 537:31-537:61 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 668 (historical match only) | not reviewed |
| 674 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 538:128-538:133 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 674 (historical match only) | not reviewed |
| 678 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 539:97-539:135 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 678 (historical match only) | not reviewed |
| 679 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 539:137-539:154 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 679 (historical match only) | not reviewed |
| 680 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 539:164-539:169 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 680 (historical match only) | not reviewed |
| 683 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 540:117-540:122 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 683 (historical match only) | not reviewed |
| 686 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 541:113-541:118 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 686 (historical match only) | not reviewed |
| 688 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 543:13-543:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 688 (historical match only) | not reviewed |
| 689 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 543:13-543:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 689 (historical match only) | not reviewed |
| 695 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 552:122-552:127 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 695 (historical match only) | not reviewed |
| 699 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 553:101-553:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 699 (historical match only) | not reviewed |
| 700 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 553:118-553:147 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 700 (historical match only) | not reviewed |
| 701 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 553:157-553:162 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 701 (historical match only) | not reviewed |
| 705 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 554:113-554:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 705 (historical match only) | not reviewed |
| 706 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 554:128-554:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 706 (historical match only) | not reviewed |
| 707 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 554:167-554:172 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 707 (historical match only) | not reviewed |
| 713 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 558:51-558:64 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 713 (historical match only) | not reviewed |
| 714 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 558:66-558:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 714 (historical match only) | not reviewed |
| 715 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 558:105-558:110 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 715 (historical match only) | not reviewed |
| 718 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 559:54-559:60 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 718 (historical match only) | not reviewed |
| 721 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 559:148-559:162 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 721 (historical match only) | not reviewed |
| 722 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 559:164-559:193 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 722 (historical match only) | not reviewed |
| 723 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 559:203-559:208 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 723 (historical match only) | not reviewed |
| 727 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 560:68-560:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 727 (historical match only) | not reviewed |
| 729 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 560:166-560:183 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 729 (historical match only) | not reviewed |
| 730 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 560:185-560:214 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 730 (historical match only) | not reviewed |
| 731 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 560:224-560:229 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 731 (historical match only) | not reviewed |
| 734 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 561:116-561:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 734 (historical match only) | not reviewed |
| 737 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 562:99-562:104 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 737 (historical match only) | not reviewed |
| 740 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 563:106-563:111 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 740 (historical match only) | not reviewed |
| 743 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 564:104-564:109 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 743 (historical match only) | not reviewed |
| 746 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 565:110-565:115 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 746 (historical match only) | not reviewed |
| 758 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 573:191-573:196 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 758 (historical match only) | not reviewed |
| 762 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 574:96-574:105 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 762 (historical match only) | not reviewed |
| 763 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 574:107-574:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 763 (historical match only) | not reviewed |
| 764 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 574:134-574:139 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 764 (historical match only) | not reviewed |
| 768 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 575:104-575:117 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 768 (historical match only) | not reviewed |
| 769 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 575:119-575:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 769 (historical match only) | not reviewed |
| 770 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 575:146-575:151 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 770 (historical match only) | not reviewed |
| 773 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 576:108-576:113 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 773 (historical match only) | not reviewed |
| 776 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 577:118-577:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 776 (historical match only) | not reviewed |
| 779 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 578:110-578:115 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 779 (historical match only) | not reviewed |
| 782 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 579:114-579:119 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 782 (historical match only) | not reviewed |
| 790 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 587:149-587:154 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 790 (historical match only) | not reviewed |
| 793 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 588:121-588:126 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 793 (historical match only) | not reviewed |
| 797 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 590:90-590:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 797 (historical match only) | not reviewed |
| 798 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 590:102-590:119 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 798 (historical match only) | not reviewed |
| 799 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 590:129-590:134 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 799 (historical match only) | not reviewed |
| 801 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 595:31-595:57 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 801 (historical match only) | not reviewed |
| 813 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 598:93-598:98 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 813 (historical match only) | not reviewed |
| 817 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 599:95-599:115 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 817 (historical match only) | not reviewed |
| 818 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 599:117-599:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 818 (historical match only) | not reviewed |
| 819 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 599:144-599:149 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 819 (historical match only) | not reviewed |
| 823 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 600:110-600:140 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 823 (historical match only) | not reviewed |
| 824 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 600:142-600:159 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 824 (historical match only) | not reviewed |
| 825 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 600:169-600:174 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 825 (historical match only) | not reviewed |
| 829 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 601:110-601:140 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 829 (historical match only) | not reviewed |
| 830 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 601:142-601:159 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 830 (historical match only) | not reviewed |
| 831 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 601:169-601:174 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 831 (historical match only) | not reviewed |
| 834 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 602:132-602:137 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 834 (historical match only) | not reviewed |
| 837 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 603:130-603:135 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 837 (historical match only) | not reviewed |
| 840 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 604:125-604:130 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 840 (historical match only) | not reviewed |
| 843 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 605:118-605:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 843 (historical match only) | not reviewed |
| 845 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 607:13-607:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 845 (historical match only) | not reviewed |
| 846 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 607:13-607:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 846 (historical match only) | not reviewed |
| 852 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 618:83-618:103 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 852 (historical match only) | not reviewed |
| 853 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 618:105-618:122 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 853 (historical match only) | not reviewed |
| 854 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 618:132-618:137 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 854 (historical match only) | not reviewed |
| 856 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 623:27-623:57 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 856 (historical match only) | not reviewed |
| 862 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 624:116-624:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 862 (historical match only) | not reviewed |
| 870 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 626:49-626:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 870 (historical match only) | not reviewed |
| 871 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 626:76-626:81 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 871 (historical match only) | not reviewed |
| 873 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 628:13-628:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 873 (historical match only) | not reviewed |
| 874 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 628:13-628:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 874 (historical match only) | not reviewed |
| 880 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 639:83-639:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 880 (historical match only) | not reviewed |
| 881 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 639:98-639:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 881 (historical match only) | not reviewed |
| 882 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 639:125-639:130 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 882 (historical match only) | not reviewed |
| 900 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 648:49-648:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 900 (historical match only) | not reviewed |
| 901 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 648:76-648:81 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 901 (historical match only) | not reviewed |
| 907 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 650:100-650:105 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 907 (historical match only) | not reviewed |
| 910 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 651:107-651:112 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 910 (historical match only) | not reviewed |
| 913 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 652:111-652:116 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 913 (historical match only) | not reviewed |
| 923 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 653:104-653:165 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(artifactName) &gt;= maxDiagnosticBytes&quot;</code> | Survived | old ID 923 (historical match only) | not reviewed |
| 927 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 654:39-654:93 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(field) &gt;= maxDiagnosticBytes&quot;</code> | Survived | old ID 927 (historical match only) | not reviewed |
| 931 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 655:41-655:97 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(snippet) &gt;= maxDiagnosticBytes&quot;</code> | Survived | old ID 931 (historical match only) | not reviewed |
| 933 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 656:55-656:133 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 933 (historical match only) | not reviewed |
| 935 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 658:13-658:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 935 (historical match only) | not reviewed |
| 936 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 658:13-658:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 936 (historical match only) | not reviewed |
| 944 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 669:38-669:92 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(value) &gt;= maxDiagnosticBytes&quot;</code> | Survived | old ID 944 (historical match only) | not reviewed |
| 946 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 670:55-670:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 946 (historical match only) | not reviewed |
| 961 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 696:51-696:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 961 (historical match only) | not reviewed |
| 963 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 702:19-702:59 | Conditional (true) mutation | <code>&quot;(true?null :problems[0])&quot;</code> | Survived | old ID 963 (historical match only) | not reviewed |
| 973 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 716:16-716:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 973 (historical match only) | not reviewed |
| 983 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 725:13-725:133 | Logical mutation | <code>&quot;parent.TryGetProperty(name, out JsonElement element) &#124;&#124; (element.ValueKind is JsonValueKind.True or JsonValueKind.False)&quot;</code> | Survived | old ID 983 (historical match only) | not reviewed |
| 989 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 727:17-727:22 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 989 (historical match only) | not reviewed |
| 990 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 728:16-728:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 990 (historical match only) | not reviewed |
| 999 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 736:58-736:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 999 (historical match only) | not reviewed |
| 1000 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 736:70-736:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1000 (historical match only) | not reviewed |
| 1001 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 736:97-736:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1001 (historical match only) | not reviewed |
| 1010 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 744:58-744:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1010 (historical match only) | not reviewed |
| 1011 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 744:70-744:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1011 (historical match only) | not reviewed |
| 1012 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 744:97-744:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1012 (historical match only) | not reviewed |
| 1017 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 753:48-753:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1017 (historical match only) | not reviewed |
| 1018 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 753:60-753:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1018 (historical match only) | not reviewed |
| 1019 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 754:20-754:25 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1019 (historical match only) | not reviewed |
| 1028 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 758:70-758:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1028 (historical match only) | not reviewed |
| 1034 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 767:48-767:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1034 (historical match only) | not reviewed |
| 1035 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 767:60-767:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1035 (historical match only) | not reviewed |
| 1036 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 768:20-768:25 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1036 (historical match only) | not reviewed |
| 1044 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 772:58-772:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1044 (historical match only) | not reviewed |
| 1045 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 772:70-772:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1045 (historical match only) | not reviewed |
| 1046 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 772:97-772:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1046 (historical match only) | not reviewed |
| 1051 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 779:64-779:69 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1051 (historical match only) | not reviewed |
| 1056 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 783:88-783:92 | Boolean mutation | <code>&quot;false&quot;</code> | Survived | old ID 1056 (historical match only) | not reviewed |
| 1065 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 789:17-789:31 | Equality mutation | <code>&quot;text != second&quot;</code> | Survived | old ID 1065 (historical match only) | not reviewed |
| 1067 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 789:98-789:102 | Boolean mutation | <code>&quot;false&quot;</code> | Survived | old ID 1067 (historical match only) | not reviewed |
| 1068 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 791:16-791:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1068 (historical match only) | not reviewed |
| 1076 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 801:47-801:52 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1076 (historical match only) | not reviewed |
| 1081 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 816:65-816:70 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1081 (historical match only) | not reviewed |
| 1083 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 817:66-817:71 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1083 (historical match only) | not reviewed |
| 1091 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 823:13-823:115 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1091 (historical match only) | not reviewed |
| 1092 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 823:41-823:113 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1092 (historical match only) | not reviewed |
| 1112 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 854:13-854:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1112 (historical match only) | not reviewed |
| 1113 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 855:13-855:35 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1113 (historical match only) | not reviewed |
| 1123 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 861:55-861:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1123 (historical match only) | not reviewed |
| 1131 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 25:13-25:101 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1131 (historical match only) | not reviewed |
| 1132 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 25:51-25:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 1132 (historical match only) | not reviewed |
| 1143 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 41:17-41:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1143 (historical match only) | not reviewed |
| 1144 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 42:66-42:83 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1144 (historical match only) | not reviewed |
| 1147 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 48:17-48:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1147 (historical match only) | not reviewed |
| 1149 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 56:62-56:76 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1149 (historical match only) | not reviewed |
| 1155 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 76:29-76:46 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1155 (historical match only) | not reviewed |
| 1162 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 90:38-90:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1162 (historical match only) | not reviewed |
| 1167 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 98:38-98:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1167 (historical match only) | not reviewed |
| 1174 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 114:51-114:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 1174 (historical match only) | not reviewed |
| 1197 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 146:17-146:125 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1198 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 146:55-146:123 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1201 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 156:13-156:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1203 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 158:23-158:43 | Remove checked expression | <code>&quot;entries + 1&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1210 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 159:56-159:83 | Equality mutation | <code>&quot;entries &gt;= limits.MaxEntries&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1213 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 161:55-161:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1219 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 179:18-179:30 | String mutation | <code>&quot;\&quot;Stryker was here!\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1235 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 211:17-211:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1256 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 243:51-243:58 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1272 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 268:55-268:106 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1292 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 278:59-278:108 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1316 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 316:37-316:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1327 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 425:22-425:45 | Remove checked expression | <code>&quot;_bytes + count&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1330 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 429:13-429:138 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1331 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 429:51-429:136 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1337 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 434:51-434:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1340 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 442:24-442:49 | Remove checked expression | <code>&quot;_entries + count&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1341 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 442:32-442:48 | Arithmetic mutation | <code>&quot;_entries - count&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1343 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 446:13-446:139 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1344 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 446:51-446:137 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1346 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 449:13-449:41 | Equality mutation | <code>&quot;_entries &gt;= limits.MaxEntries&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1349 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 451:13-451:118 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1350 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 451:51-451:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1371 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 29:9-29:49 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1372 (historical match only) | not reviewed |
| 1372 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 30:9-30:51 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1373 (historical match only) | not reviewed |
| 1373 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 31:9-31:58 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1374 (historical match only) | not reviewed |
| 1374 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 32:9-32:64 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1375 (historical match only) | not reviewed |
| 1377 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 35:13-35:102 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1378 (historical match only) | not reviewed |
| 1378 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 35:41-35:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1379 (historical match only) | not reviewed |
| 1390 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 49:17-49:57 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1391 (historical match only) | not reviewed |
| 1420 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 91:56-91:73 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1421 (historical match only) | not reviewed |
| 1430 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 104:93-104:110 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1431 (historical match only) | not reviewed |
| 1434 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 105:110-105:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1435 (historical match only) | not reviewed |
| 1438 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 106:122-106:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1439 (historical match only) | not reviewed |
| 1442 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 107:136-107:153 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1443 (historical match only) | not reviewed |
| 1446 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 108:131-108:148 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1447 (historical match only) | not reviewed |
| 1451 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 113:57-113:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1452 (historical match only) | not reviewed |
| 1453 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 116:13-116:58 | Equality mutation | <code>&quot;highsElement.GetArrayLength() &gt;= maxPartitions&quot;</code> | Survived | old ID 1454 (historical match only) | not reviewed |
| 1457 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 118:51-118:112 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | old ID 1458 (historical match only) | not reviewed |
| 1467 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 129:112-129:129 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1468 (historical match only) | not reviewed |
| 1471 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 130:98-130:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1472 (historical match only) | not reviewed |
| 1473 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 132:13-132:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1474 (historical match only) | not reviewed |
| 1474 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 132:13-132:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | old ID 1475 (historical match only) | not reviewed |
| 1505 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 169:125-169:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1506 (historical match only) | not reviewed |
| 1511 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 170:140-170:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1512 (historical match only) | not reviewed |
| 1517 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 171:110-171:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1518 (historical match only) | not reviewed |
| 1523 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 172:125-172:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1524 (historical match only) | not reviewed |
| 1529 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 173:151-173:168 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1530 (historical match only) | not reviewed |
| 1535 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 174:119-174:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1536 (historical match only) | not reviewed |
| 1557 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 215:13-215:38 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1558 (historical match only) | not reviewed |
| 1560 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 221:13-221:46 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1561 (historical match only) | not reviewed |
| 1563 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 227:13-227:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1564 (historical match only) | not reviewed |
| 1564 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 228:13-228:35 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1565 (historical match only) | not reviewed |
| 1569 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 233:17-233:26 | Equality mutation | <code>&quot;count &lt;= 0&quot;</code> | Survived | old ID 1570 (historical match only) | not reviewed |
| 1571 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 233:30-233:59 | Equality mutation | <code>&quot;Length &gt;= maximumBytes - count&quot;</code> | Survived | old ID 1572 (historical match only) | not reviewed |
| 1574 | `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 234:55-234:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1575 (historical match only) | not reviewed |
| 1576 | `src/Parchwell.Verifier/Program.cs` | 36:9-36:49 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1577 (historical match only) | not reviewed |
| 1577 | `src/Parchwell.Verifier/Program.cs` | 39:9-41:32 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1578 (historical match only) | not reviewed |
| 1579 | `src/Parchwell.Verifier/Program.cs` | 44:9-44:110 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1580 (historical match only) | not reviewed |
| 1580 | `src/Parchwell.Verifier/Program.cs` | 45:9-45:76 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1581 (historical match only) | not reviewed |
| 1581 | `src/Parchwell.Verifier/Program.cs` | 45:39-45:74 | Null coalescing mutation (remove left) | <code>&quot;TimeProvider.System&quot;</code> | Survived | old ID 1582 (historical match only) | not reviewed |
| 1590 | `src/Parchwell.Verifier/Program.cs` | 75:56-75:61 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1591 (historical match only) | not reviewed |
| 1592 | `src/Parchwell.Verifier/Program.cs` | 77:37-77:42 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1593 (historical match only) | not reviewed |
| 1594 | `src/Parchwell.Verifier/Program.cs` | 81:17-81:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1595 (historical match only) | not reviewed |
| 1595 | `src/Parchwell.Verifier/Program.cs` | 82:17-86:54 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1596 (historical match only) | not reviewed |
| 1596 | `src/Parchwell.Verifier/Program.cs` | 85:36-85:57 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1597 (historical match only) | not reviewed |
| 1597 | `src/Parchwell.Verifier/Program.cs` | 86:35-86:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1598 (historical match only) | not reviewed |
| 1599 | `src/Parchwell.Verifier/Program.cs` | 91:17-91:33 | Equality mutation | <code>&quot;host is null&quot;</code> | Survived | old ID 1600 (historical match only) | not reviewed |
| 1601 | `src/Parchwell.Verifier/Program.cs` | 93:17-93:101 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;logger = host.Services.GetService&lt;ILoggerFactory&gt;()?.CreateLogger(nameof(Program))&quot;</code> | Survived | old ID 1602 (historical match only) | not reviewed |
| 1602 | `src/Parchwell.Verifier/Program.cs` | 94:17-94:77 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;configuration = host.Services.GetService&lt;IConfiguration&gt;()&quot;</code> | Survived | old ID 1603 (historical match only) | not reviewed |
| 1603 | `src/Parchwell.Verifier/Program.cs` | 95:17-95:77 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;state = host.Services.GetService&lt;VerifierExecutionState&gt;()&quot;</code> | Survived | old ID 1604 (historical match only) | not reviewed |
| 1604 | `src/Parchwell.Verifier/Program.cs` | 96:21-96:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | old ID 1605 (historical match only) | not reviewed |
| 1606 | `src/Parchwell.Verifier/Program.cs` | 98:21-102:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1607 (historical match only) | not reviewed |
| 1607 | `src/Parchwell.Verifier/Program.cs` | 101:40-101:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1608 (historical match only) | not reviewed |
| 1608 | `src/Parchwell.Verifier/Program.cs` | 102:40-102:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1609 (historical match only) | not reviewed |
| 1609 | `src/Parchwell.Verifier/Program.cs` | 105:17-105:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1610 (historical match only) | not reviewed |
| 1614 | `src/Parchwell.Verifier/Program.cs` | 113:55-113:60 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1615 (historical match only) | not reviewed |
| 1616 | `src/Parchwell.Verifier/Program.cs` | 117:21-117:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | old ID 1617 (historical match only) | not reviewed |
| 1618 | `src/Parchwell.Verifier/Program.cs` | 119:21-123:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1619 (historical match only) | not reviewed |
| 1619 | `src/Parchwell.Verifier/Program.cs` | 122:40-122:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1620 (historical match only) | not reviewed |
| 1620 | `src/Parchwell.Verifier/Program.cs` | 123:40-123:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1621 (historical match only) | not reviewed |
| 1621 | `src/Parchwell.Verifier/Program.cs` | 126:17-126:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1622 (historical match only) | not reviewed |
| 1625 | `src/Parchwell.Verifier/Program.cs` | 133:21-133:80 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1626 (historical match only) | not reviewed |
| 1626 | `src/Parchwell.Verifier/Program.cs` | 133:73-133:78 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1627 (historical match only) | not reviewed |
| 1628 | `src/Parchwell.Verifier/Program.cs` | 137:21-137:36 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1629 (historical match only) | not reviewed |
| 1630 | `src/Parchwell.Verifier/Program.cs` | 142:21-142:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | old ID 1631 (historical match only) | not reviewed |
| 1632 | `src/Parchwell.Verifier/Program.cs` | 144:21-148:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1633 (historical match only) | not reviewed |
| 1633 | `src/Parchwell.Verifier/Program.cs` | 147:40-147:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1634 (historical match only) | not reviewed |
| 1634 | `src/Parchwell.Verifier/Program.cs` | 148:40-148:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1635 | `src/Parchwell.Verifier/Program.cs` | 151:17-151:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | old ID 1636 (historical match only) | not reviewed |
| 1636 | `src/Parchwell.Verifier/Program.cs` | 155:9-155:29 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1641 | `src/Parchwell.Verifier/Program.cs` | 163:101-163:106 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | old ID 1642 (historical match only) | not reviewed |
| 1658 | `src/Parchwell.Verifier/Program.cs` | 222:37-222:45 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1664 | `src/Parchwell.Verifier/Program.cs` | 227:37-227:45 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1668 | `src/Parchwell.Verifier/Program.cs` | 232:21-232:29 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1670 | `src/Parchwell.Verifier/Program.cs` | 234:13-234:69 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1689 | `src/Parchwell.Verifier/Program.cs` | 257:9-258:107 | Logical mutation | <code>&quot;property.Equals(nameof(VerifierHostOptions.ProcessLifetime), StringComparison.OrdinalIgnoreCase)\n        &amp;&amp; property.Equals(nameof(VerifierHostOptions.CleanupTimeout), StringComparison.OrdinalIgnoreCase)&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1691 | `src/Parchwell.Verifier/VerifierHostOptions.cs` | 96:13-96:25 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 1692 (historical match only) | not reviewed |
| 1701 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 13:9-13:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1704 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 18:13-18:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1705 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 18:24-18:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1707 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 22:41-22:62 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1709 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 23:45-23:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1711 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 24:49-24:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1713 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 25:57-25:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1715 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 26:53-26:82 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1717 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 29:45-29:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1719 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 30:46-30:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1722 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 36:58-36:88 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1724 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 37:56-37:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1726 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 38:56-38:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1729 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 43:48-43:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1731 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 44:64-44:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1733 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 45:53-45:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1735 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 46:57-46:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1737 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 47:55-47:82 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1739 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 48:51-48:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1741 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 49:64-49:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1743 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 50:49-50:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1745 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 51:59-51:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1747 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 52:61-52:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1749 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 53:54-53:76 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1751 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 54:61-54:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1753 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 55:62-55:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1755 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 56:56-56:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1757 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 57:56-57:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1759 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 58:63-58:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1761 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 59:59-59:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1764 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 63:21-63:110 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1765 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 63:32-63:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1767 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 69:48-69:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1769 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 70:53-70:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1771 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 71:51-71:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1773 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 72:59-72:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1775 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 73:61-73:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1777 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 74:56-74:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1781 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 77:62-77:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1789 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 90:46-90:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1791 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 91:36-91:52 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1793 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 92:41-92:62 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1795 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 93:49-93:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1797 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 94:39-94:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1799 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 95:47-95:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1801 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 96:44-96:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1803 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 97:52-97:77 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1805 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 98:58-98:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1807 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 99:39-99:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1809 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 100:55-100:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1811 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 101:53-101:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1813 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 102:43-102:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1815 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 103:46-103:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1817 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 104:47-104:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1819 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 105:40-105:60 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1821 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 106:44-106:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1826 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 113:24-113:67 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1829 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 117:55-117:107 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1831 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 118:49-118:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1833 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 119:53-119:103 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1835 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 120:49-120:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1865 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 154:24-154:81 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1866 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 155:13-155:20 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1874 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 162:28-162:85 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1875 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 163:17-163:24 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1892 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 176:24-176:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1901 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 185:17-185:78 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1902 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 185:28-185:76 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1903 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 186:17-186:26 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1905 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 189:44-189:90 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1907 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 190:49-190:100 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1909 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 191:39-191:80 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1911 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 192:44-192:90 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1913 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 193:52-193:106 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1915 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 194:42-194:86 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1925 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 210:13-210:96 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1926 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 210:24-210:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1927 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 211:13-211:20 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1933 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 216:24-216:121 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1949 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 247:24-247:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1956 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 255:34-255:68 | Equality mutation | <code>&quot;value.Ticks &lt; MaximumTimeoutTicks&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1965 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 261:24-261:61 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1975 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 272:24-272:66 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1980 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 280:24-280:46 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1985 | `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 288:24-288:58 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1991 | `src/Parchwell.Verifier/VerifierWorker.cs` | 47:31-47:56 | Linq method mutation (SingleOrDefault() to Single()) | <code>&quot;_handlers.Single&quot;</code> | Survived | old ID 1940 (historical match only) | not reviewed |
| 1996 | `src/Parchwell.Verifier/VerifierWorker.cs` | 50:78-50:83 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 1997 | `src/Parchwell.Verifier/VerifierWorker.cs` | 51:21-51:66 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2000 | `src/Parchwell.Verifier/VerifierWorker.cs` | 56:21-60:47 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2002 | `src/Parchwell.Verifier/VerifierWorker.cs` | 67:13-71:39 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2004 | `src/Parchwell.Verifier/VerifierWorker.cs` | 76:13-81:39 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2008 | `src/Parchwell.Verifier/VerifierWorker.cs` | 124:9-124:57 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2009 | `src/Parchwell.Verifier/VerifierWorker.cs` | 125:9-125:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2010 | `src/Parchwell.Verifier/VerifierWorker.cs` | 129:21-129:57 | Null coalescing mutation (remove left) | <code>&quot;Environment.Exit&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2014 | `src/Parchwell.Verifier/VerifierWorker.cs` | 136:13-136:94 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2015 | `src/Parchwell.Verifier/VerifierWorker.cs` | 136:49-136:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2017 | `src/Parchwell.Verifier/VerifierWorker.cs` | 148:9-148:27 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2056 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 46:56-46:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2119 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 80:77-80:114 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2120 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 80:81-80:99 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2121 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 80:101-80:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2125 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 81:63-81:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2133 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 88:63-88:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2134 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 88:67-88:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2135 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 88:93-88:102 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2153 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 101:76-101:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2154 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 101:80-101:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2157 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 102:60-102:89 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2158 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 102:64-102:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2163 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 111:80-111:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2167 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 112:75-112:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2174 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 113:73-113:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2178 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 119:76-119:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2179 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 119:80-119:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2186 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 120:114-120:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2190 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 126:76-126:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2191 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 126:80-126:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2198 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 127:115-127:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2202 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 133:76-133:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2203 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 133:80-133:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2208 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 134:91-134:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2227 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 28:40-28:77 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2228 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 28:78-28:87 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2255 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 56:40-56:77 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2256 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 56:78-56:87 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2335 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 117:59-117:88 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2336 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 117:63-117:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 2285 (historical match only) | not reviewed |
| 2337 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 117:82-117:85 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2345 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 122:13-122:56 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2346 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 122:17-122:40 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2347 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 122:42-122:54 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2906 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 361:76-361:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2907 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 361:80-361:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2911 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 362:63-362:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2918 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 369:63-369:96 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2919 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 369:67-369:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2920 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 369:97-369:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2926 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 378:74-378:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2927 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 378:78-378:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2931 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 379:62-379:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2938 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 386:69-386:116 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2939 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 386:73-386:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2941 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 386:99-386:102 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2942 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 386:117-386:126 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2950 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 395:76-395:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2951 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 395:80-395:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2955 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 396:63-396:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2959 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 402:70-402:113 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2960 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 402:74-402:91 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2961 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 402:93-402:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2969 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 415:17-415:47 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2970 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 415:21-415:39 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 2975 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 419:64-419:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3016 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 440:76-440:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3017 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 440:80-440:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3021 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 441:64-441:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3028 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 456:80-456:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3032 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 457:75-457:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3036 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 463:76-463:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3037 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 463:80-463:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3044 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 464:114-464:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3048 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 470:76-470:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3049 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 470:80-470:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3056 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 471:115-471:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3060 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 477:76-477:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3061 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 477:80-477:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3066 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 478:91-478:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3070 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 484:76-484:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3071 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 484:80-484:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3075 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 487:17-487:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3079 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 492:76-492:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3080 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 492:80-492:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3084 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 494:17-494:56 | Equality mutation | <code>&quot;value.ValueKind != JsonValueKind.Number&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3086 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 495:17-495:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3091 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 502:36-502:66 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3092 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 502:40-502:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3098 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 507:17-507:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3105 | `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 510:87-510:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3151 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 45:17-45:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3152 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 46:17-46:26 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3178 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 79:21-79:49 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 3127 (historical match only) | not reviewed |
| 3284 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 138:21-138:100 | Conditional (true) mutation | <code>&quot;(true?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3285 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 138:21-138:100 | Conditional (false) mutation | <code>&quot;(false?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3286 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 138:62-138:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3287 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 138:82-138:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3291 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 147:76-147:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3292 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 147:80-147:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3296 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 148:75-148:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3304 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 155:73-155:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3308 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 161:76-161:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3309 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 161:80-161:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3316 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 162:114-162:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3320 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 168:76-168:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3321 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 168:80-168:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3328 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 169:115-169:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3332 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 175:76-175:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3333 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 175:80-175:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3338 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 176:91-176:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3344 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 185:17-185:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3352 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 194:17-194:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3358 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 203:17-203:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3393 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 232:55-232:131 | Conditional (true) mutation | <code>&quot;(true?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3394 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 232:55-232:131 | Conditional (false) mutation | <code>&quot;(false?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3395 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 232:93-232:110 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3396 | `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 232:113-232:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3453 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 50:56-50:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | old ID 3402 (historical match only) | not reviewed |
| 3547 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 94:76-94:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3548 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 94:80-94:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3552 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 95:63-95:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3557 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 97:55-97:114 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3564 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 104:64-104:97 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3565 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 104:68-104:85 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3566 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 104:98-104:107 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3613 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 128:76-128:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3614 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 128:80-128:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3618 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 129:63-129:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3622 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 135:71-135:114 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3623 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 135:75-135:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3624 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 135:94-135:112 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3631 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 144:74-144:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3632 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 144:78-144:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3636 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 145:62-145:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3642 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 152:76-152:122 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3643 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 152:80-152:97 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3644 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 152:99-152:120 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3649 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 160:76-160:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3650 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 160:80-160:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3654 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 161:64-161:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3658 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 170:76-170:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3659 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 170:80-170:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3663 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 171:75-171:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3671 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 178:73-178:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3675 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 184:76-184:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3676 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 184:80-184:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3683 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 185:114-185:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3687 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 191:76-191:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3688 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 191:80-191:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3695 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 192:115-192:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3699 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 198:76-198:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3700 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 198:80-198:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3705 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 199:91-199:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3711 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 207:17-207:56 | Equality mutation | <code>&quot;value.ValueKind != JsonValueKind.Number&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3713 | `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 208:17-208:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3718 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 19:13-19:115 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3719 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 19:51-19:113 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3732 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 47:17-47:51 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3733 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 47:21-47:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3734 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 47:40-47:43 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3758 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 60:13-62:14 | Block removal mutation | <code>&quot;{}&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3764 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 71:21-71:39 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3769 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 76:21-76:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3777 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 83:55-83:118 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3782 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 91:21-91:76 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3783 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 91:25-91:42 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3784 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 91:44-91:54 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3785 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 92:21-92:30 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3836 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 128:17-128:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3837 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 128:21-128:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3870 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 158:63-158:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
| 3875 | `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 164:17-164:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | no exact historical sample match | not reviewed |
## Historical runner-validation evidence

The following complete prior document snapshot retains the original measurement excerpts, pinned-source citations, all 621 survivor rows, and all 359 completed sample proofs. It is scoped only to commit `610625f4fa19972c48c8d315442982542b257cd8` and report SHA-256 `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d`. Of 359 retained rows, 342 reached normal MTP: 341 passes and one complete MTP test failure; 17 compilation failures before MTP are retained as discrepancies but excluded from runner-validation coverage. The normal-MTP sample covers 342 observations across 12 source files and 15 mutator types. Hari accepted the mutant 2134 and 17 compilation differences for PR 3 on 2026-10-18 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728); raw historical evidence and measured results below remain unchanged. This history does not establish current-head replay results.

## Historical record: commit 610625f4fa19972c48c8d315442982542b257cd8

Stryker.NET 5.0.0 measured PR 3's foundation at 80.34% on commit `610625f4fa19972c48c8d315442982542b257cd8` ([measurement excerpts](#measurement-excerpts)). The replay sample found one normal-MTP assertion-result disagreement and 17 compilation failures before MTP. Hari accepted these differences for PR 3 on 2026-10-18 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728); the historical statuses and score remain unchanged. This is a foundation baseline, not a final mutation threshold or evidence of live readiness.

### Authority and tested content

The [PR 3 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6245697297) applies with the later decisions linked in the [verifier contract](verifier-contract.md). Hari's [sampled survivor-review decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6247408154) replaces exhaustive PR 3 survivor review: retain the completed runner-validation sample, fill uncovered survivor files or mutators, and mark every un-replayed survivor `not reviewed`. Full dispositions belong to PR 6 on the final code, before Hari chooses the break threshold on #13 and before live use. The configured break value remains 0.

The tested content tree is `ba7e9aeb7a68b7f565652cda831f412e438d54c8`. This historical report measured commit `610625f4fa19972c48c8d315442982542b257cd8` and tree `ba7e9aeb7a68b7f565652cda831f412e438d54c8`. The current code-bearing commit is `0f9eb0aa50e9a4e2f88e8bee0ac9b254132c9b90`; its current report and validation are documented above. Hari accepted the historical sample's replay differences for PR 3 on 2026-10-18; see https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728. Historical replay outcomes below do not transfer to current code.

| Component | Recorded version and evidence |
| --- | --- |
| .NET SDK | 10.0.402; [global.json](../../global.json), validation logs |
| .NET runtime | 10.0.13; targeted xUnit XML environment |
| Microsoft.Testing.Platform | 2.5.1; [Verifier.Tests lock](../../tests/Parchwell.Verifier.Tests/packages.lock.json) |
| xUnit / xunit.v3.mtp-v2 | 4.0.2; targeted XML and the same lock |
| dotnet-stryker | 5.0.0; tool list and run banner; [pinned NuGet manifest](https://api.nuget.org/v3-flatcontainer/dotnet-stryker/5.0.0/dotnet-stryker.nuspec) names source commit `6e77a3451bac4793e9c839c3ff4055c1b30ca3af` |

### Ordinary validation

All commands below completed successfully on the tested content before mutation; their counters and timestamps are reproduced in the [measurement excerpts](#measurement-excerpts). Evidence is retained under `.artifacts/verifier-mutation/resumed-validation/`; `validation.json` records exact commands, timestamps, exit codes and the SHA/tree.

| Command or check | Actual result |
| --- | --- |
| `dotnet restore Parchwell.slnx --locked-mode` | Passed after deliberate review of the three approved lock changes |
| `dotnet build Parchwell.slnx --configuration Release --no-restore` | Passed; 0 warnings, 0 errors |
| Targeted Verifier test command and existing `ValidateReport` target from the plan | 1044 passed; 0 failed, skipped, not-run or runner errors |
| `./scripts/verify-dotnet.sh` | Passed: Architecture 8, Kafka 306, LoadGen 262, Scenarios 94, Verifier 1044; warning-as-error control passed |
| `dotnet tool restore` and `dotnet tool list --local` | Passed; local tool version 5.0.0 |
| `npm ci --ignore-scripts` | Passed |
| `npm run test:docs` | 27 test cases passed |
| `npm run check:docs` | Passed without warnings on the pre-mutation tracked tree |

The existing Enrichment test scaffold is empty and is not claimed as coverage. The Kafka tests used local Docker containers; no live Azure operation was run. The targeted XML SHA-256 is `fb02ef5dad0df3b15c2b73dcd15c355d62bab57a3a701c43d0d7d661b3745403`. The scripted validation sequence elapsed 349.934415 s, including 310.412146 s for `verify-dotnet.sh`.

The plan's `dotnet stryker --version` command exited 1 because this pinned CLI expects a value for that option. Its retained help identifies the option as a project version, so the executable version is taken from the tool list, NuGet manifest and run banner instead; the failed attempt and help are retained as `stryker-version-attempt.log` and `stryker-help.log` under `.artifacts/verifier-mutation/`.

### Stryker command and measurement

Working directory: `tests/Parchwell.Verifier.Tests`.

```sh
dotnet stryker --config-file stryker-config.json --output /workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/run.y53la5z6
```

The command exited 0. Its wrapper elapsed 2161.186865 s, measured from the timestamps in `run.y53la5z6/run.json`; the tool separately logged `00:36:00.5251622`. The [checked-in configuration](../../tests/Parchwell.Verifier.Tests/stryker-config.json) selects MTP, Release, `net10.0`, JSON/HTML/progress reporters, and display thresholds high 80 / low 60 with break 0. These display thresholds are not acceptance thresholds. The exact mutation globs are `Program.cs`, `VerifierHostOptions*.cs`, `VerifierWorker.cs`, `Common/Contracts/*.cs`, `Common/Serialization/*.cs`, `Common/Window/*.cs`, `Common/Results/*.cs`, and `VerifyRun/Input/*.cs`.

| Report status | Count |
| --- | ---: |
| Killed | 2531 |
| Survived | 621 |
| Timeout | 7 |
| CompileError | 255 |
| Ignored | 411 |
| NoCoverage | 0 |
| RuntimeError | 0 |
| Pending | 0 |
| Generated | 3825 |
| Detected: Killed + Timeout | 2538 |
| Valid: Killed + Timeout + Survived + NoCoverage | 3159 |
| Measured score | 80.34% |

Counts were computed from the fresh schema-2 JSON report, whose complete status/ID projection is in the [measurement excerpts](#measurement-excerpts), which contains 18 source-file entries and 13 files with mutants. The [pinned score implementation](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/ProjectComponents/ProjectComponent.cs#L31-L73) calculates `2538 / 3159 * 100`; Timeout is detected under that convention, not killed by an assertion. CompileError and Ignored are outside the denominator. The report has actual killed/tested mutants and is not an empty run.

| Retained file under `.artifacts/verifier-mutation/` | SHA-256 |
| --- | --- |
| `run.y53la5z6/reports/mutation-report.json` | `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d` |
| `run.y53la5z6/reports/mutation-report.html` | `dc5d1df2663fdc8947d5cb7531417536a76cb372d517e9e7bf4190f52f21074c` |
| `run.y53la5z6/command.log` | `f2f2a8d2402b3ff641a83d5050fd35b801479197d29238da5a6e95c63dc4fc50` |

Full reports, logs, XML and helper scripts are retained locally under that ignored artifact directory. The [measurement excerpts](#measurement-excerpts) commit the raw run counters, timing records and complete status/replay ID projections; the full original artifact files are not committed. This document preserves the measured summary, report digest, coverage tables and full survivor inventory. The summary calculation is retained as `verifier-fresh-mutation-summary.json`.

### Discovery, warnings and non-assertion statuses

Stryker discovered 1044 test IDs and captured coverage records for 1044 tests. Scripted reconciliation with the ordinary XML matched 68 exact source/type/method groups and their case multiplicities; the formats use different literal IDs. Test metadata is not a per-test outcome report. The Stryker log does not print initial-run pass/skip totals, so no such totals are inferred from discovery; the independent ordinary XML supplies the clean baseline. Detailed reconciliation is retained in `verifier-fresh-test-reconciliation.md`.

The log warns that target-framework detection failed for the test and source projects and assumes the selected `net10.0`; the ordinary build and XML confirm that target. It also warns that MTP support is preview. Two H7 watchdog theory cases timed out waiting for coverage-relay acknowledgements and became `Dubious`, separately from the seven mutant Timeout statuses. The [pinned runner](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L674-L687) returns dubious coverage for this event, and the [coverage analyser](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/CoverageAnalysis/CoverageAnalyser.cs#L107-L137) conservatively includes dubious tests in assessment. Zero NoCoverage therefore does not establish useful branch coverage, and `coveredBy` is not treated as proof that a test executed a mutant.

All 255 CompileError statuses report that the mutant caused compile errors; they are not assertion kills. All 411 Ignored records are block-removal mutants with replacement `{}` and the report reason `Removed by block already covered filter`; no source exclusion was added for them. Exact records and the separate timeout review are retained in `verifier-fresh-anomaly-review.json`.

| Timeout ID | Source inference; exact timed-out test is not identified by the report |
| --- | --- |
| 1585 | Removing hosted-worker registration can leave the host waiting for application stop |
| 1588 | Replacing injected time with wall-clock time can delay the synthetic watchdog |
| 1590 | Removing host startup can leave the application-stop wait unfulfilled |
| 1648 | Inverting the lifetime gate can disable the watchdog for a positive lifetime |
| 1651 | Removing watchdog start can leave blocked work without its hard exit |
| 1955 | Removing the worker's stop signal can leave the host waiting |
| 1961 | Inverting the fresh-timer guard can prevent watchdog startup |

These mechanisms are source-based inferences, not observed per-test timeout causes. The report supplies Timeout, an empty `killedBy` list and no individual timeout reason for these rows. The original status and score are preserved.

### Runner-validation sample and disagreement

The two exhaustive batches were interrupted on Hari's decision with 351 completed replay records retained; the [published measurement projections](#measurement-excerpts) identify each completed replay and its recorded outcome. Eight additional survivors filled the eight uncovered survivor-source files and three uncovered mutators; selection and stop provenance are retained in `gap-fill-selection.json` and `survivor-replay-stop.json`. The resulting sample has 359 completed replays: 341 full-suite passes, 17 compilation failures before MTP, and one complete test run with an assertion failure. The remaining 262 survivors are `not reviewed`.

Every source file with a survivor and every mutator type among survivors has a normal-MTP observation in the final sample. `CapturedArtifacts.cs` has two Killed mutants and no survivor available to sample, so the sample does not claim to replay that mutated file. Coverage is by file and mutator separately, not every file/mutator combination, and the sample does not establish all un-replayed outcomes.

Mutant **2134**, `ArtifactIndexReader.cs:120:110-120:139`, removes `Add("wrong_json_kind", path);`. Stryker reported Survived. The isolated normal MTP run built successfully and executed 1044 tests: 1043 passed, one failed, none skipped or not-run, and no runner errors. The index/schema_version wrong-kind case failed with `Xunit.Sdk.FalseException` at `StrictParserTests.ReadIndexProblems`, line 911: removing the diagnostic leaves an empty problem list, so `IsSuccess` becomes true. This is a confirmed preview-versus-normal assertion-result disagreement. Its cause inside Stryker is not established. Hari accepted this difference for PR 3 on 2026-10-18 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728). The JSON report status and 80.34% score are not rewritten.

The other differences are ordinary compiler rejections of mutants Stryker reported as Survived. Their exact IDs, spans and diagnostics are retained in `sample-compile-discrepancies.json` and the full table below. Normal builds retain warnings-as-errors; none of these failures is called an assertion kill. The pinned Stryker [compilation options](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Utilities/Buildalyzer/IAnalyzerResultCSharpExtensions.cs#L17-L39) and [diagnostic-option mapping](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Utilities/Buildalyzer/IAnalyzerResultExtensions.cs#L257-L282) do not map global `TreatWarningsAsErrors` to the in-process Roslyn compilation; this explains a compilation-policy difference without altering the repository setting.

The sample retains 318 missing-test dispositions, 23 equivalent/unreachable dispositions with their specific proofs, 17 compilation discrepancies and the one assertion-result discrepancy. A missing-test disposition describes an unasserted observable detail, including diagnostic messages or multiplicity; it does not add an enforced check or require a production change. Equivalence claims apply only within their stated call-path constraints. Full review of the final code remains PR 6 work.

### Sources used in the sampled proofs

The runtime proofs use .NET 10.0.13 source commit `6aa72daa24f66c01c3d7b6a32519e9d21396deec`, with retained source copies and hashes in `pinned-sources/manifest.json` under the artifact directory.

- Bounded stream dispatch: [Utf8JsonWriter.Flush](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Text.Json/src/System/Text/Json/Writer/Utf8JsonWriter.cs#L401-L424) writes a nonempty pending span. For a derived MemoryStream, [MemoryStream.Write(span)](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/IO/MemoryStream.cs#L621-L629) delegates to [Stream.Write(span)](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L912-L923), which calls the virtual byte-array overload. Both private codec Write overrides are reachable; their capacity guards check the same length/count before any write, so removing either single guard leaves the other. This dispatch path does not call WriteByte; the private WriteByte overrides are unreachable from this codec caller.
- The local JSONL row counter cannot exceed the input's Int32-sized [ReadOnlyMemory.Length](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/ReadOnlyMemory.cs#L123-L126), which bounds the proof for removal of Int64 checked addition.
- [JsonDocument.Dispose](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Text.Json/src/System/Text/Json/Document/JsonDocument.cs#L61-L88) changes resource lifecycle; a removal is not called equivalent merely because parse-result assertions still pass.
- Continuation scheduling uses [Task.ConfigureAwait](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) and its [context/scheduler handling](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516). The sampled equivalence inference is limited to the normal Main console entrypoint; it does not cover internal callers with a custom context or scheduler.
- Host proofs use the pinned [Host implementation](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs#L19), [configuration registration](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs#L305-L306), [logging registration](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Logging/src/LoggingServiceCollectionExtensions.cs#L38), and [category logger cache](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Logging/src/LoggerFactory.cs#L140-L157). Claims are confined to the graph constructed by this executable, as stated in the individual rows.
### Measurement excerpts

These excerpts come from retained run logs/XML. The inventory is generated from the cited historical report and replay CSVs; the generator runs no build, test, Stryker, or replay command.

- Tested commit: `610625f4fa19972c48c8d315442982542b257cd8`; tree: `ba7e9aeb7a68b7f565652cda831f412e438d54c8`.
- Historical report: `.artifacts/verifier-mutation/run.y53la5z6/reports/mutation-report.json`; SHA-256: `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d`.
- Embedded inventory SHA-256: `ff84b00cb09df3e47bd3be6e2900f23d55a45dc6c2fc49753bda5d29379ca0e8` (UTF-8/ASCII bytes of the standalone JSON file, with one trailing newline).
- Baseline XML source: `.artifacts/verifier-mutation/fresh-replays/batch-1/baseline/results/results.xml`; SHA-256: `57956f34061636d7a067448a52b414909e081b19b1691f112aacc5403fc3d9ab`.

#### Fresh Stryker command log excerpt

Source: `.artifacts/verifier-mutation/run.y53la5z6/command.log`; SHA-256: `f2f2a8d2402b3ff641a83d5050fd35b801479197d29238da5a6e95c63dc4fc50`. Each JSON string decodes to the exact source line from `Version:` through the final score line, including whitespace.

```jsonl
"Version: 5.0.0"
""
"[07:03:59 INF] Analysis starting."
"[07:03:59 INF] Using project configuration/platform 'Release|`default`'."
"[07:03:59 INF] Analyzing 1 test project(s)."
"[07:04:00 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj. Assuming selected framework (net10.0) is present."
"[07:04:01 WRN] Failed to identify target frameworks for project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj. Assuming selected framework (net10.0) is present."
"[07:04:01 INF] Found project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj to mutate."
"[07:04:01 INF] Analysis complete."
"[07:04:01 INF] Building test project /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj (1/1)"
"[07:04:02 INF] Building project Parchwell.Verifier.Tests.csproj using dotnet build Parchwell.Verifier.Tests.csproj -c Release --property:Platform=AnyCPU (directory /workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests.)"
"[07:04:07 WRN] The Microsoft Test Platform testrunner is currently in preview. Results should be verified since this feature is still being tested."
"[07:04:08 INF] Number of tests found: 1044 for project /workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Parchwell.Verifier.csproj. Initial test run started."
"[07:04:34 INF] 3825 mutants created"
"[07:04:34 INF] Capture mutant coverage using 'CoverageBasedTest' mode."
"[07:04:34 INF] Starting per-test coverage capture for MTP runner"
"[07:04:34 INF] Capturing per-test coverage for 1044 tests across 1 assemblies"
"[07:05:16 WRN] MtpRunner-1: Timed out waiting for coverage relay ack for test 4fd698566276569298a5d5f0e7c59ab3b89d77919f831d43e0321e0335b3bd58; marking as Dubious"
"[07:05:26 WRN] MtpRunner-1: Timed out waiting for coverage relay ack for test a5bc4404fb7cbd1d315169a41e71c21563ab88f526beda546eebf8608f3c087c; marking as Dubious"
"[07:05:26 INF] Per-test coverage capture complete: 1044 tests captured"
"Hint: by passing \"--open-report or -o\" the report will open automatically and "
"update the report in real-time."
"[07:05:27 INF] 255   mutants got status CompileError. Reason: Mutant caused compile errors"
"[07:05:27 INF] 411   mutants got status Ignored.      Reason: Removed by block already covered filter"
"[07:05:27 INF] 666   total mutants are skipped for the above mentioned reasons"
"[07:05:27 INF] 3159  total mutants will be tested"
""
"Killed:   2531"
"Survived:  621"
"Timeout:     7"
"Errors:     0"
""
"Your json report has been generated at:"
"file:///workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/run.y53la5"
"z6/reports/mutation-report.json"
""
"Your html report has been generated at:"
"file:///workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/run.y53la5"
"z6/reports/mutation-report.html"
"You can open it in your browser of choice."
"[07:39:59 INF] Time Elapsed 00:36:00.5251622"
"[07:39:59 INF] The final mutation score is 80.34 %"
```

#### Fresh replay baseline XML excerpt

Source: `.artifacts/verifier-mutation/fresh-replays/batch-1/baseline/results/results.xml`; the excerpt is its exact `<assembly>` opening element.

```xml
<assembly environment="64-bit (x64) .NET 10.0.13 [collection-per-class, parallel (collections, 4 threads)]" id="e0cb095d-24c1-4107-b61f-1a7125558fca" name="/workspace/scratch/verifier-fresh-replay-batch-1/isolated-checkout/tests/Parchwell.Verifier.Tests/bin/Release/net10.0/Parchwell.Verifier.Tests.dll" run-date="2026-10-18" run-time="07:42:07" start-rtf="2026-10-18T07:42:07.0083630+00:00" test-framework="xUnit.net v3 4.0.2+0c7aa4991f" target-framework=".NETCoreApp,Version=v10.0" errors="0" failed="0" finish-rtf="2026-10-18T07:42:10.6727261+00:00" not-run="0" passed="1044" skipped="0" time="3.666" time-rtf="00:00:03.6660000" total="1044">
```

#### Build, full-suite, and documentation evidence

##### Release build from `resumed-validation/build.log`

```text
    Build succeeded.
        0 Warning(s)
        0 Error(s)
```

##### Full verification project summaries from `resumed-validation/full-verification.log`

```text
      Parchwell.ArchitectureTests: 8 passed; checked report.
      Parchwell.Kafka.Tests: 306 passed; checked report.
      Parchwell.LoadGen.Tests: 262 passed; checked report.
      Parchwell.Scenarios.Tests: 94 passed; checked report.
      Parchwell.Verifier.Tests: 1044 passed; checked report.
    Verification passed. Per-project reports identify the tests executed.
```

##### Documentation test summary from `resumed-validation/docs-tests.log`

The following JSON strings decode to the exact UTF-8 lines in the raw test log.

```jsonl
"\u2139 tests 27"
"\u2139 pass 27"
"\u2139 fail 0"
```

##### Documentation check from `resumed-validation/docs-check.log`

```text

> check:docs
> node scripts/check-docs.mjs

.agents/skills/repo-review/SKILL.md: no issues found
.claude/agents/repo-reviewer.md: no issues found
.github/pull_request_template.md: no issues found
AGENTS.md: no issues found
CLAUDE.md: no issues found
CONTEXT.md: no issues found
docs/agents/factory.md: no issues found
docs/blueprint.md: no issues found
docs/engineering/code-comments.md: no issues found
docs/engineering/documentation-checks.md: no issues found
docs/engineering/dotnet-verification.md: no issues found
docs/engineering/dotnet.md: no issues found
docs/engineering/terraform.md: no issues found
docs/networking/hub-firewall.md: no issues found
docs/operations/kafka-authentication.md: no issues found
docs/operations/kafka-baseline.md: no issues found
docs/operations/kafka-configuration-explained.md: no issues found
docs/operations/loadgen-contract.md: no issues found
docs/operations/phantom-spike/README.md: no issues found
docs/operations/scenario-contract.md: no issues found
docs/operations/statistics-bridge.md: no issues found
docs/operations/verifier-contract.md: no issues found
docs/research/kubernetes-kafka-clients.md: no issues found
README.md: no issues found
tests/fixtures/verifier-v1/README.md: no issues found
Documentation scan completed. Warnings are advisory; external URLs were not checked.
```

The `docs-tests.log` summary lines are JSON-string escaped; decoding each quoted string reproduces the exact UTF-8 source line. The 27 value is a test-case count, not a Markdown-file count.

Elapsed seconds are differences of the raw start/end timestamps below. The targeted-results XML SHA-256 is from `resumed-validation/validation.json`.

```json
{
  "validation_started": "2026-10-18T06:58:02.184328+00:00",
  "validation_finished": "2026-10-18T07:03:52.118743+00:00",
  "validation_elapsed_seconds": 349.934415,
  "full_verification_started": "2026-10-18T06:58:29.698190+00:00",
  "full_verification_finished": "2026-10-18T07:03:40.110336+00:00",
  "full_verification_elapsed_seconds": 310.412146,
  "stryker_started": "2026-10-18T07:03:58.700888+00:00",
  "stryker_finished": "2026-10-18T07:39:59.887753+00:00",
  "stryker_elapsed_seconds": 2161.186865,
  "targeted_xml_sha256": "fb02ef5dad0df3b15c2b73dcd15c355d62bab57a3a701c43d0d7d661b3745403"
}
```

<details><summary>Complete mutant and replay measurement inventory</summary>

#### Recountable inventory

The JSON lists all 3,825 fresh Stryker IDs by source file and report status, the 359 completed replay IDs by CSV outcome, and the 262 fresh survivors outside the completed sample. Replay outcome and report status are separate dimensions; `not_reviewed_fresh_ids` is a set difference, not a replay outcome.

Inventory SHA-256: `ff84b00cb09df3e47bd3be6e2900f23d55a45dc6c2fc49753bda5d29379ca0e8`.

```json
{
  "tested_commit": "610625f4fa19972c48c8d315442982542b257cd8",
  "tree": "ba7e9aeb7a68b7f565652cda831f412e438d54c8",
  "report_path": ".artifacts/verifier-mutation/run.y53la5z6/reports/mutation-report.json",
  "report_sha256": "feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d",
  "report_inventory": {
    "schema_version": "2",
    "mutant_count": 3825,
    "status_counts": {
      "CompileError": 255,
      "Ignored": 411,
      "Killed": 2531,
      "NoCoverage": 0,
      "Pending": 0,
      "RuntimeError": 0,
      "Survived": 621,
      "Timeout": 7
    },
    "fresh_survived_or_nocoverage_count": 621,
    "mutants_by_file_and_status": [
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs",
        "statuses": {
          "CompileError": ["67", "70", "106", "109", "136", "137", "248", "249", "311", "314", "355", "363", "373", "393", "396", "399", "420", "423", "426", "429", "459", "480", "483", "531", "582", "583", "584", "585", "586", "587", "691", "696", "698", "702", "704", "712", "716", "720", "724", "728", "732", "735", "738", "741", "744", "848", "851", "855", "863", "864", "865", "867", "869", "872", "876", "879", "883", "893", "894", "895", "897", "899", "902", "905", "908", "911", "914", "915", "916", "917", "918", "919", "954", "955", "965", "966", "969", "970", "986", "998", "1009", "1016", "1026", "1033", "1043", "1070", "1072", "1074"],
          "Ignored": ["35", "42", "71", "78", "102", "157", "162", "173", "178", "186", "202", "226", "234", "241", "247", "256", "264", "281", "284", "287", "292", "295", "303", "321", "331", "344", "383", "403", "412", "435", "439", "447", "451", "470", "473", "477", "484", "488", "494", "500", "506", "512", "518", "523", "525", "528", "530", "537", "539", "542", "544", "552", "556", "559", "567", "573", "581", "602", "606", "609", "622", "628", "634", "639", "650", "663", "667", "677", "748", "761", "767", "784", "796", "800", "816", "822", "828", "938", "939", "947", "948", "951", "953", "962", "968", "971", "982", "987", "991", "993", "996", "1002", "1004", "1007", "1013", "1015", "1021", "1024", "1030", "1032", "1038", "1041", "1047", "1053", "1055", "1058", "1061", "1063", "1066", "1069", "1080", "1082", "1084", "1093", "1105", "1108", "1111", "1114"],
          "Killed": ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32", "33", "34", "36", "37", "38", "41", "43", "44", "45", "46", "47", "48", "49", "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "60", "61", "62", "63", "64", "65", "66", "68", "69", "72", "73", "74", "75", "76", "77", "79", "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "90", "91", "92", "93", "94", "95", "96", "97", "98", "99", "100", "101", "103", "104", "105", "107", "108", "110", "111", "114", "117", "118", "119", "122", "123", "124", "125", "128", "129", "132", "133", "138", "139", "140", "143", "144", "145", "146", "147", "148", "149", "150", "151", "152", "153", "156", "158", "161", "163", "167", "168", "169", "172", "176", "177", "179", "180", "181", "182", "185", "189", "190", "191", "192", "193", "194", "195", "196", "197", "198", "199", "200", "201", "203", "204", "205", "206", "207", "208", "209", "210", "211", "212", "214", "215", "216", "217", "220", "221", "222", "223", "224", "233", "242", "244", "251", "253", "257", "258", "259", "260", "262", "265", "266", "267", "268", "269", "270", "271", "272", "273", "274", "275", "276", "277", "278", "279", "280", "282", "283", "285", "286", "288", "289", "290", "291", "293", "294", "296", "297", "298", "299", "300", "301", "302", "304", "305", "306", "307", "308", "309", "310", "312", "313", "315", "316", "317", "318", "319", "320", "322", "323", "324", "325", "326", "327", "328", "329", "330", "332", "333", "334", "335", "336", "337", "338", "339", "340", "341", "342", "343", "345", "346", "347", "348", "349", "350", "351", "352", "353", "354", "356", "357", "358", "360", "362", "364", "365", "367", "368", "369", "371", "372", "374", "375", "376", "377", "378", "379", "380", "381", "382", "384", "385", "386", "387", "388", "389", "390", "391", "392", "394", "395", "397", "398", "400", "401", "402", "404", "405", "406", "407", "408", "409", "410", "411", "413", "414", "415", "416", "417", "418", "419", "421", "422", "424", "425", "427", "428", "430", "431", "432", "433", "434", "436", "437", "438", "440", "441", "442", "443", "444", "445", "446", "448", "449", "450", "452", "453", "454", "455", "456", "457", "458", "460", "461", "462", "463", "464", "465", "466", "467", "468", "469", "471", "472", "474", "475", "476", "478", "479", "481", "482", "486", "487", "492", "493", "498", "499", "504", "505", "510", "511", "516", "517", "522", "524", "529", "535", "536", "538", "543", "548", "551", "553", "554", "555", "561", "562", "563", "565", "566", "571", "572", "577", "580", "588", "589", "590", "591", "592", "593", "594", "595", "596", "597", "598", "599", "601", "603", "604", "605", "610", "611", "612", "613", "614", "615", "616", "617", "618", "620", "621", "626", "627", "632", "633", "638", "640", "642", "643", "644", "646", "647", "649", "651", "652", "653", "654", "655", "657", "658", "661", "662", "669", "670", "671", "672", "673", "675", "676", "681", "682", "684", "685", "687", "690", "692", "693", "694", "697", "703", "708", "709", "710", "711", "717", "719", "725", "726", "733", "736", "739", "742", "745", "747", "749", "750", "751", "752", "753", "754", "755", "756", "757", "759", "760", "765", "766", "771", "772", "774", "775", "777", "778", "780", "781", "783", "785", "786", "787", "788", "789", "791", "792", "794", "795", "802", "803", "804", "805", "806", "807", "808", "809", "810", "811", "812", "814", "815", "820", "821", "826", "827", "832", "833", "835", "836", "838", "839", "841", "842", "844", "847", "849", "850", "857", "858", "859", "860", "861", "866", "868", "875", "877", "878", "884", "885", "886", "887", "888", "889", "890", "891", "892", "896", "898", "903", "904", "906", "909", "912", "920", "921", "922", "924", "925", "926", "928", "929", "930", "932", "934", "937", "940", "941", "942", "943", "945", "949", "950", "952", "956", "957", "958", "959", "960", "964", "967", "972", "974", "975", "976", "977", "978", "979", "980", "981", "984", "985", "988", "992", "994", "995", "997", "1003", "1005", "1006", "1008", "1014", "1020", "1022", "1023", "1025", "1027", "1029", "1031", "1037", "1039", "1040", "1042", "1048", "1049", "1050", "1052", "1054", "1057", "1059", "1060", "1062", "1064", "1071", "1073", "1075", "1077", "1078", "1079", "1085", "1086", "1087", "1088", "1089", "1090", "1094", "1095", "1096", "1097", "1098", "1099", "1100", "1101", "1102", "1103", "1104", "1106", "1107", "1109", "1110", "1115", "1116", "1117", "1118", "1119", "1120", "1121", "1122"],
          "Survived": ["39", "40", "112", "113", "115", "116", "120", "121", "126", "127", "130", "131", "134", "135", "141", "142", "154", "155", "159", "160", "164", "165", "166", "170", "171", "174", "175", "183", "184", "187", "188", "213", "218", "219", "225", "227", "228", "229", "230", "231", "232", "235", "236", "237", "238", "239", "240", "243", "245", "246", "250", "252", "254", "255", "261", "263", "359", "361", "366", "370", "485", "489", "490", "491", "495", "496", "497", "501", "502", "503", "507", "508", "509", "513", "514", "515", "519", "520", "521", "526", "527", "532", "533", "534", "540", "541", "545", "546", "547", "549", "550", "557", "558", "560", "564", "568", "569", "570", "574", "575", "576", "578", "579", "600", "607", "608", "619", "623", "624", "625", "629", "630", "631", "635", "636", "637", "641", "645", "648", "656", "659", "660", "664", "665", "666", "668", "674", "678", "679", "680", "683", "686", "688", "689", "695", "699", "700", "701", "705", "706", "707", "713", "714", "715", "718", "721", "722", "723", "727", "729", "730", "731", "734", "737", "740", "743", "746", "758", "762", "763", "764", "768", "769", "770", "773", "776", "779", "782", "790", "793", "797", "798", "799", "801", "813", "817", "818", "819", "823", "824", "825", "829", "830", "831", "834", "837", "840", "843", "845", "846", "852", "853", "854", "856", "862", "870", "871", "873", "874", "880", "881", "882", "900", "901", "907", "910", "913", "923", "927", "931", "933", "935", "936", "944", "946", "961", "963", "973", "983", "989", "990", "999", "1000", "1001", "1010", "1011", "1012", "1017", "1018", "1019", "1028", "1034", "1035", "1036", "1044", "1045", "1046", "1051", "1056", "1065", "1067", "1068", "1076", "1081", "1083", "1091", "1092", "1112", "1113", "1123"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Serialization/StrictJson.cs",
        "statuses": {
          "CompileError": ["1189", "1190", "1240", "1243", "1264", "1279", "1281", "1301", "1305", "1307", "1310", "1323", "1324"],
          "Ignored": ["1124", "1130", "1133", "1142", "1146", "1148", "1150", "1152", "1154", "1156", "1158", "1160", "1163", "1165", "1168", "1172", "1179", "1184", "1186", "1192", "1197", "1212", "1216", "1218", "1222", "1225", "1228", "1233", "1237", "1244", "1246", "1247", "1249", "1253", "1254", "1258", "1262", "1271", "1285", "1291", "1295", "1298", "1302", "1306", "1311", "1313", "1315", "1326", "1327", "1330", "1336", "1339", "1340", "1343", "1349"],
          "Killed": ["1125", "1126", "1127", "1128", "1129", "1134", "1135", "1136", "1137", "1138", "1139", "1140", "1141", "1145", "1151", "1153", "1157", "1159", "1161", "1164", "1166", "1169", "1170", "1171", "1173", "1175", "1176", "1177", "1178", "1180", "1181", "1182", "1183", "1185", "1187", "1188", "1191", "1194", "1195", "1196", "1200", "1201", "1203", "1205", "1206", "1207", "1208", "1209", "1210", "1213", "1215", "1217", "1219", "1221", "1223", "1224", "1226", "1227", "1229", "1230", "1231", "1232", "1234", "1235", "1238", "1239", "1241", "1242", "1245", "1248", "1250", "1251", "1252", "1255", "1256", "1259", "1260", "1261", "1263", "1265", "1266", "1267", "1268", "1269", "1270", "1272", "1274", "1275", "1276", "1277", "1278", "1280", "1282", "1283", "1284", "1286", "1287", "1288", "1289", "1290", "1292", "1294", "1296", "1297", "1299", "1300", "1303", "1304", "1308", "1309", "1312", "1314", "1316", "1318", "1319", "1320", "1321", "1322", "1325", "1329", "1333", "1334", "1335", "1337", "1346", "1348"],
          "Survived": ["1131", "1132", "1143", "1144", "1147", "1149", "1155", "1162", "1167", "1174", "1193", "1198", "1199", "1202", "1204", "1211", "1214", "1220", "1236", "1257", "1273", "1293", "1317", "1328", "1331", "1332", "1338", "1341", "1342", "1344", "1345", "1347", "1350", "1351"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs",
        "statuses": {
          "CompileError": ["1412", "1415", "1461", "1464", "1496", "1499", "1540", "1541"],
          "Ignored": ["1371", "1377", "1380", "1390", "1408", "1419", "1423", "1450", "1456", "1459", "1476", "1493", "1500", "1504", "1510", "1516", "1522", "1528", "1534", "1539", "1542", "1553", "1555", "1557", "1560", "1563", "1566"],
          "Killed": ["1352", "1353", "1354", "1355", "1356", "1357", "1358", "1359", "1360", "1361", "1362", "1363", "1364", "1365", "1366", "1367", "1368", "1369", "1370", "1376", "1381", "1382", "1383", "1384", "1385", "1386", "1387", "1388", "1389", "1392", "1393", "1394", "1395", "1396", "1397", "1398", "1399", "1400", "1401", "1402", "1403", "1404", "1405", "1406", "1407", "1409", "1410", "1411", "1413", "1414", "1416", "1417", "1418", "1420", "1422", "1424", "1425", "1426", "1427", "1428", "1429", "1430", "1432", "1433", "1434", "1436", "1437", "1438", "1440", "1441", "1442", "1444", "1445", "1446", "1448", "1449", "1451", "1453", "1455", "1457", "1460", "1462", "1463", "1465", "1466", "1467", "1469", "1470", "1471", "1473", "1477", "1478", "1479", "1480", "1481", "1482", "1483", "1484", "1485", "1486", "1487", "1488", "1489", "1490", "1491", "1492", "1494", "1495", "1497", "1498", "1501", "1502", "1503", "1505", "1507", "1508", "1509", "1511", "1513", "1514", "1515", "1517", "1519", "1520", "1521", "1523", "1525", "1526", "1527", "1529", "1531", "1532", "1533", "1535", "1537", "1538", "1543", "1544", "1545", "1546", "1547", "1548", "1549", "1550", "1551", "1552", "1554", "1556", "1559", "1562", "1567", "1568", "1569", "1571", "1573", "1574"],
          "Survived": ["1372", "1373", "1374", "1375", "1378", "1379", "1391", "1421", "1431", "1435", "1439", "1443", "1447", "1452", "1454", "1458", "1468", "1472", "1474", "1475", "1506", "1512", "1518", "1524", "1530", "1536", "1558", "1561", "1564", "1565", "1570", "1572", "1575"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/Program.cs",
        "statuses": {
          "CompileError": ["1624", "1643", "1644", "1645", "1646", "1647", "1649", "1650", "1652", "1653", "1657", "1663", "1672"],
          "Ignored": ["1576", "1586", "1587", "1589", "1594", "1599", "1601", "1606", "1612", "1613", "1616", "1618", "1623", "1625", "1628", "1630", "1632", "1639", "1640", "1654", "1660", "1666", "1670", "1679"],
          "Killed": ["1579", "1583", "1584", "1592", "1611", "1614", "1638", "1641", "1655", "1656", "1658", "1661", "1662", "1664", "1667", "1668", "1673", "1674", "1675", "1676", "1677", "1678", "1680", "1681", "1682", "1683", "1684", "1685", "1686", "1687", "1688", "1689"],
          "Survived": ["1577", "1578", "1580", "1581", "1582", "1591", "1593", "1595", "1596", "1597", "1598", "1600", "1602", "1603", "1604", "1605", "1607", "1608", "1609", "1610", "1615", "1617", "1619", "1620", "1621", "1622", "1626", "1627", "1629", "1631", "1633", "1634", "1635", "1636", "1637", "1642", "1659", "1665", "1669", "1671", "1690"],
          "Timeout": ["1585", "1588", "1590", "1648", "1651"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptions.cs",
        "statuses": {
          "CompileError": ["1696"],
          "Ignored": ["1691"],
          "Killed": ["1693", "1694", "1695", "1697", "1698", "1700"],
          "Survived": ["1692", "1699"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierHostOptionsValidator.cs",
        "statuses": {
          "CompileError": ["1786", "1787", "1843", "1845"],
          "Ignored": ["1701", "1704", "1788", "1823", "1825", "1837", "1839", "1847", "1849", "1865", "1870", "1873", "1880", "1883", "1884", "1885", "1889", "1891", "1896", "1906", "1912", "1917", "1922", "1925", "1927", "1930", "1932"],
          "Killed": ["1703", "1707", "1709", "1711", "1713", "1715", "1717", "1719", "1721", "1722", "1724", "1726", "1728", "1729", "1731", "1733", "1735", "1737", "1739", "1741", "1743", "1745", "1747", "1749", "1751", "1753", "1755", "1757", "1759", "1761", "1763", "1767", "1769", "1771", "1773", "1775", "1777", "1779", "1780", "1781", "1783", "1784", "1785", "1789", "1791", "1793", "1795", "1797", "1799", "1801", "1803", "1805", "1807", "1809", "1811", "1813", "1815", "1817", "1819", "1821", "1824", "1826", "1828", "1829", "1831", "1833", "1835", "1838", "1840", "1842", "1844", "1846", "1848", "1853", "1855", "1857", "1859", "1861", "1863", "1866", "1867", "1868", "1869", "1871", "1872", "1877", "1878", "1879", "1881", "1886", "1887", "1888", "1890", "1892", "1893", "1894", "1895", "1897", "1899", "1900", "1901", "1902", "1903", "1904", "1907", "1908", "1909", "1910", "1911", "1913", "1915", "1916", "1918", "1919", "1920", "1921", "1923", "1926", "1928", "1931", "1933"],
          "Survived": ["1702", "1705", "1706", "1708", "1710", "1712", "1714", "1716", "1718", "1720", "1723", "1725", "1727", "1730", "1732", "1734", "1736", "1738", "1740", "1742", "1744", "1746", "1748", "1750", "1752", "1754", "1756", "1758", "1760", "1762", "1764", "1765", "1766", "1768", "1770", "1772", "1774", "1776", "1778", "1782", "1790", "1792", "1794", "1796", "1798", "1800", "1802", "1804", "1806", "1808", "1810", "1812", "1814", "1816", "1818", "1820", "1822", "1827", "1830", "1832", "1834", "1836", "1841", "1850", "1851", "1852", "1854", "1856", "1858", "1860", "1862", "1864", "1874", "1875", "1876", "1882", "1898", "1905", "1914", "1924", "1929", "1934"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifierWorker.cs",
        "statuses": {
          "Ignored": ["1936", "1937", "1939", "1943", "1948", "1950", "1952", "1954", "1956", "1960", "1962", "1965"],
          "Killed": ["1935", "1938", "1941", "1942", "1944", "1947"],
          "Survived": ["1940", "1945", "1946", "1949", "1951", "1953", "1957", "1958", "1959", "1963", "1964", "1966"],
          "Timeout": ["1955", "1961"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs",
        "statuses": {
          "CompileError": ["1992", "1993", "2025", "2026", "2124", "2125", "2126", "2129", "2130", "2132", "2133", "2136", "2137", "2138", "2141", "2142", "2144", "2145"],
          "Ignored": ["1984", "1995", "2035", "2064", "2067", "2072", "2077", "2081", "2099", "2101", "2105", "2108", "2110", "2114", "2121", "2148", "2150", "2155"],
          "Killed": ["1967", "1968", "1969", "1970", "1971", "1972", "1973", "1974", "1975", "1976", "1977", "1978", "1979", "1980", "1981", "1982", "1983", "1985", "1986", "1987", "1988", "1989", "1990", "1991", "1994", "1996", "1997", "1998", "1999", "2000", "2001", "2002", "2003", "2004", "2006", "2007", "2008", "2009", "2010", "2011", "2012", "2013", "2014", "2015", "2016", "2017", "2018", "2019", "2020", "2021", "2022", "2023", "2024", "2027", "2028", "2029", "2030", "2031", "2032", "2033", "2034", "2036", "2037", "2038", "2039", "2040", "2041", "2042", "2043", "2044", "2045", "2046", "2047", "2048", "2049", "2050", "2051", "2052", "2053", "2054", "2055", "2056", "2057", "2058", "2059", "2060", "2061", "2062", "2063", "2065", "2066", "2071", "2073", "2075", "2076", "2078", "2079", "2080", "2085", "2086", "2087", "2088", "2089", "2090", "2091", "2092", "2093", "2094", "2095", "2096", "2097", "2098", "2100", "2104", "2106", "2109", "2113", "2115", "2117", "2118", "2119", "2120", "2122", "2131", "2143", "2146", "2149", "2153", "2154", "2156"],
          "Survived": ["2005", "2068", "2069", "2070", "2074", "2082", "2083", "2084", "2102", "2103", "2107", "2111", "2112", "2116", "2123", "2127", "2128", "2134", "2135", "2139", "2140", "2147", "2151", "2152", "2157"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedArtifacts.cs",
        "statuses": {
          "Killed": ["2158", "2159"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs",
        "statuses": {
          "CompileError": ["2166", "2167", "2168", "2170", "2179", "2180", "2186", "2187", "2194", "2195", "2196", "2198", "2207", "2208", "2214", "2215", "2226", "2227", "2271", "2272", "2360", "2361", "2423", "2424", "2500", "2501", "2889", "2892", "2982", "2983", "2984", "2987", "2988", "2990", "2991", "2994", "2995", "2996", "2999", "3000", "3002", "3003", "3031", "3032", "3043", "3044"],
          "Ignored": ["2160", "2172", "2175", "2188", "2200", "2203", "2220", "2229", "2281", "2283", "2297", "2373", "2435", "2512", "2537", "2576", "2637", "2649", "2664", "2683", "2701", "2738", "2752", "2772", "2787", "2826", "2852", "2854", "2858", "2862", "2866", "2872", "2874", "2878", "2884", "2886", "2896", "2898", "2902", "2906", "2914", "2916", "2922", "2928", "2962", "2964", "2968", "2971", "2973", "2975", "2979", "3006", "3008", "3013", "3016", "3018", "3025", "3027", "3036", "3038", "3048"],
          "Killed": ["2161", "2162", "2163", "2164", "2165", "2169", "2171", "2173", "2174", "2178", "2181", "2182", "2183", "2184", "2185", "2189", "2190", "2191", "2192", "2193", "2197", "2199", "2201", "2202", "2206", "2209", "2210", "2211", "2212", "2213", "2216", "2217", "2218", "2219", "2221", "2222", "2223", "2224", "2225", "2228", "2230", "2231", "2232", "2233", "2234", "2235", "2236", "2237", "2238", "2239", "2240", "2241", "2242", "2243", "2244", "2245", "2246", "2247", "2248", "2249", "2250", "2251", "2252", "2253", "2254", "2255", "2256", "2257", "2258", "2259", "2260", "2261", "2262", "2263", "2264", "2265", "2266", "2267", "2268", "2269", "2270", "2273", "2274", "2275", "2276", "2277", "2278", "2279", "2280", "2282", "2287", "2288", "2289", "2290", "2291", "2292", "2293", "2298", "2299", "2300", "2301", "2302", "2303", "2304", "2305", "2306", "2307", "2308", "2309", "2310", "2311", "2312", "2313", "2314", "2315", "2316", "2317", "2318", "2319", "2320", "2321", "2322", "2323", "2324", "2325", "2326", "2327", "2328", "2329", "2330", "2331", "2332", "2333", "2334", "2335", "2336", "2337", "2338", "2339", "2340", "2341", "2342", "2343", "2344", "2345", "2346", "2347", "2348", "2349", "2350", "2351", "2352", "2353", "2354", "2355", "2356", "2357", "2358", "2359", "2362", "2363", "2364", "2365", "2366", "2367", "2368", "2369", "2370", "2371", "2372", "2374", "2375", "2376", "2377", "2378", "2379", "2380", "2381", "2382", "2383", "2384", "2385", "2386", "2387", "2388", "2389", "2390", "2391", "2392", "2393", "2394", "2395", "2396", "2397", "2398", "2399", "2400", "2401", "2402", "2403", "2404", "2405", "2406", "2407", "2408", "2409", "2410", "2411", "2412", "2413", "2414", "2415", "2416", "2417", "2418", "2419", "2420", "2421", "2422", "2425", "2426", "2427", "2428", "2429", "2430", "2431", "2432", "2433", "2434", "2436", "2437", "2438", "2439", "2440", "2441", "2442", "2443", "2444", "2445", "2446", "2447", "2448", "2449", "2450", "2451", "2452", "2453", "2454", "2455", "2456", "2457", "2458", "2459", "2460", "2461", "2462", "2463", "2464", "2465", "2466", "2467", "2468", "2469", "2470", "2471", "2472", "2473", "2474", "2475", "2476", "2477", "2478", "2479", "2480", "2481", "2482", "2483", "2484", "2485", "2486", "2487", "2488", "2489", "2490", "2491", "2492", "2493", "2494", "2495", "2496", "2497", "2498", "2499", "2502", "2503", "2504", "2505", "2506", "2507", "2508", "2509", "2510", "2511", "2513", "2514", "2515", "2516", "2517", "2518", "2519", "2520", "2521", "2522", "2523", "2524", "2525", "2526", "2527", "2528", "2529", "2530", "2531", "2532", "2533", "2534", "2535", "2536", "2538", "2539", "2540", "2541", "2542", "2543", "2544", "2545", "2546", "2547", "2548", "2549", "2550", "2551", "2552", "2553", "2554", "2555", "2556", "2557", "2558", "2559", "2560", "2561", "2562", "2563", "2564", "2565", "2566", "2567", "2568", "2569", "2570", "2571", "2572", "2573", "2574", "2575", "2577", "2578", "2579", "2580", "2581", "2582", "2583", "2584", "2585", "2586", "2587", "2588", "2589", "2590", "2591", "2592", "2593", "2594", "2595", "2596", "2597", "2598", "2599", "2600", "2601", "2602", "2603", "2604", "2605", "2606", "2607", "2608", "2609", "2610", "2611", "2612", "2613", "2614", "2615", "2616", "2617", "2618", "2619", "2620", "2621", "2622", "2623", "2624", "2625", "2626", "2627", "2628", "2629", "2630", "2631", "2632", "2633", "2634", "2635", "2636", "2638", "2639", "2640", "2641", "2642", "2643", "2644", "2645", "2646", "2647", "2648", "2650", "2651", "2652", "2653", "2654", "2655", "2656", "2657", "2658", "2659", "2660", "2661", "2662", "2663", "2665", "2666", "2667", "2668", "2669", "2670", "2671", "2672", "2673", "2674", "2675", "2676", "2677", "2678", "2679", "2680", "2681", "2682", "2684", "2685", "2686", "2687", "2688", "2689", "2690", "2691", "2692", "2693", "2694", "2695", "2696", "2697", "2698", "2699", "2700", "2702", "2703", "2704", "2705", "2706", "2707", "2708", "2709", "2710", "2711", "2712", "2713", "2714", "2715", "2716", "2717", "2718", "2719", "2720", "2721", "2722", "2723", "2724", "2725", "2726", "2727", "2728", "2729", "2730", "2731", "2732", "2733", "2734", "2735", "2736", "2737", "2739", "2740", "2741", "2742", "2743", "2744", "2745", "2746", "2747", "2748", "2749", "2750", "2751", "2753", "2754", "2755", "2756", "2757", "2758", "2759", "2760", "2761", "2762", "2763", "2764", "2765", "2766", "2767", "2768", "2769", "2770", "2771", "2773", "2774", "2775", "2776", "2777", "2778", "2779", "2780", "2781", "2782", "2783", "2784", "2785", "2786", "2788", "2789", "2790", "2791", "2792", "2793", "2794", "2795", "2796", "2797", "2798", "2799", "2800", "2801", "2802", "2803", "2804", "2805", "2806", "2807", "2808", "2809", "2810", "2811", "2812", "2813", "2814", "2815", "2816", "2817", "2818", "2819", "2820", "2821", "2822", "2823", "2824", "2825", "2827", "2828", "2829", "2830", "2831", "2832", "2833", "2834", "2835", "2836", "2837", "2838", "2839", "2840", "2841", "2842", "2843", "2844", "2845", "2846", "2847", "2848", "2849", "2850", "2851", "2853", "2857", "2859", "2861", "2863", "2864", "2865", "2870", "2871", "2873", "2877", "2879", "2881", "2882", "2883", "2885", "2893", "2894", "2895", "2897", "2901", "2903", "2905", "2907", "2911", "2912", "2913", "2915", "2917", "2920", "2921", "2923", "2925", "2926", "2927", "2929", "2930", "2931", "2932", "2933", "2934", "2935", "2936", "2937", "2938", "2939", "2940", "2941", "2942", "2943", "2944", "2945", "2946", "2947", "2948", "2949", "2950", "2951", "2952", "2953", "2954", "2955", "2956", "2957", "2958", "2959", "2960", "2961", "2963", "2967", "2969", "2972", "2974", "2976", "2978", "2980", "2989", "2992", "3001", "3004", "3007", "3011", "3012", "3014", "3017", "3021", "3022", "3023", "3026", "3030", "3034", "3037", "3039", "3042", "3045", "3046", "3049", "3050", "3051", "3052", "3053", "3055"],
          "Survived": ["2176", "2177", "2204", "2205", "2284", "2285", "2286", "2294", "2295", "2296", "2855", "2856", "2860", "2867", "2868", "2869", "2875", "2876", "2880", "2887", "2888", "2890", "2891", "2899", "2900", "2904", "2908", "2909", "2910", "2918", "2919", "2924", "2965", "2966", "2970", "2977", "2981", "2985", "2986", "2993", "2997", "2998", "3005", "3009", "3010", "3015", "3019", "3020", "3024", "3028", "3029", "3033", "3035", "3040", "3041", "3047", "3054"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs",
        "statuses": {
          "CompileError": ["3090", "3091", "3092", "3094", "3103", "3106", "3114", "3115", "3167", "3168", "3213", "3214", "3254", "3255", "3256", "3259", "3260", "3262", "3263", "3266", "3267", "3268", "3271", "3272", "3274", "3275", "3297", "3298", "3336", "3337", "3338", "3339", "3340", "3341"],
          "Ignored": ["3083", "3096", "3099", "3107", "3110", "3116", "3125", "3178", "3225", "3227", "3232", "3237", "3239", "3243", "3246", "3251", "3278", "3280", "3285", "3288", "3294", "3302", "3308"],
          "Killed": ["3056", "3057", "3058", "3059", "3060", "3061", "3062", "3063", "3064", "3065", "3066", "3067", "3068", "3069", "3070", "3071", "3072", "3073", "3074", "3075", "3076", "3077", "3078", "3079", "3080", "3081", "3082", "3084", "3085", "3086", "3087", "3088", "3089", "3093", "3095", "3097", "3098", "3102", "3104", "3105", "3108", "3109", "3111", "3112", "3113", "3117", "3118", "3119", "3120", "3121", "3122", "3123", "3124", "3126", "3128", "3129", "3130", "3131", "3132", "3133", "3134", "3135", "3136", "3137", "3138", "3139", "3140", "3141", "3142", "3143", "3144", "3145", "3146", "3147", "3148", "3149", "3150", "3151", "3152", "3153", "3154", "3155", "3156", "3157", "3158", "3159", "3160", "3161", "3162", "3163", "3164", "3165", "3166", "3169", "3170", "3171", "3172", "3173", "3174", "3175", "3176", "3177", "3179", "3180", "3181", "3182", "3183", "3184", "3185", "3186", "3187", "3188", "3189", "3190", "3191", "3192", "3193", "3194", "3195", "3196", "3197", "3198", "3199", "3200", "3201", "3202", "3203", "3204", "3205", "3206", "3207", "3208", "3209", "3210", "3211", "3212", "3215", "3216", "3217", "3218", "3219", "3220", "3221", "3222", "3223", "3224", "3226", "3228", "3229", "3230", "3231", "3238", "3242", "3244", "3247", "3248", "3249", "3250", "3252", "3261", "3264", "3273", "3276", "3279", "3283", "3284", "3286", "3289", "3290", "3291", "3292", "3295", "3296", "3299", "3300", "3303", "3304", "3305", "3306", "3309", "3310", "3311", "3312", "3313", "3314", "3315", "3316", "3317", "3318", "3319", "3320", "3321", "3322", "3323", "3324", "3325", "3326", "3327", "3328", "3329", "3330", "3331", "3332", "3333", "3334", "3335", "3346"],
          "Survived": ["3100", "3101", "3127", "3233", "3234", "3235", "3236", "3240", "3241", "3245", "3253", "3257", "3258", "3265", "3269", "3270", "3277", "3281", "3282", "3287", "3293", "3301", "3307", "3342", "3343", "3344", "3345"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs",
        "statuses": {
          "CompileError": ["3389", "3390", "3448", "3449", "3621", "3622", "3623", "3626", "3627", "3629", "3630", "3633", "3634", "3635", "3638", "3639", "3641", "3642", "3658", "3659"],
          "Ignored": ["3381", "3392", "3465", "3493", "3495", "3499", "3508", "3512", "3557", "3559", "3561", "3565", "3569", "3577", "3579", "3583", "3589", "3595", "3597", "3601", "3604", "3606", "3610", "3613", "3618", "3645", "3647", "3652", "3655"],
          "Killed": ["3347", "3348", "3349", "3350", "3351", "3352", "3353", "3354", "3355", "3356", "3357", "3358", "3359", "3360", "3361", "3362", "3363", "3364", "3365", "3366", "3367", "3368", "3369", "3370", "3371", "3372", "3373", "3374", "3375", "3376", "3377", "3378", "3379", "3380", "3382", "3383", "3384", "3385", "3386", "3387", "3388", "3391", "3393", "3394", "3395", "3396", "3397", "3398", "3399", "3400", "3401", "3403", "3404", "3405", "3406", "3407", "3408", "3409", "3410", "3411", "3412", "3413", "3414", "3415", "3416", "3417", "3418", "3419", "3420", "3421", "3422", "3423", "3424", "3425", "3426", "3427", "3428", "3429", "3430", "3431", "3432", "3433", "3434", "3435", "3436", "3437", "3438", "3439", "3440", "3441", "3442", "3443", "3444", "3445", "3446", "3447", "3450", "3451", "3452", "3453", "3454", "3455", "3456", "3457", "3458", "3459", "3460", "3461", "3462", "3463", "3464", "3466", "3467", "3468", "3469", "3470", "3471", "3472", "3473", "3474", "3475", "3476", "3477", "3478", "3479", "3480", "3481", "3482", "3483", "3484", "3485", "3486", "3487", "3488", "3489", "3490", "3491", "3492", "3494", "3498", "3500", "3502", "3503", "3504", "3505", "3507", "3509", "3510", "3511", "3516", "3517", "3518", "3519", "3520", "3521", "3522", "3523", "3524", "3525", "3526", "3527", "3528", "3529", "3530", "3531", "3532", "3533", "3534", "3535", "3536", "3537", "3538", "3539", "3540", "3541", "3542", "3543", "3544", "3545", "3546", "3547", "3548", "3549", "3550", "3551", "3552", "3553", "3554", "3555", "3556", "3558", "3560", "3564", "3566", "3568", "3570", "3574", "3575", "3576", "3578", "3582", "3584", "3586", "3587", "3588", "3590", "3594", "3596", "3600", "3602", "3605", "3609", "3611", "3614", "3615", "3616", "3617", "3619", "3628", "3631", "3640", "3643", "3646", "3650", "3651", "3653", "3656", "3657", "3661"],
          "Survived": ["3402", "3496", "3497", "3501", "3506", "3513", "3514", "3515", "3562", "3563", "3567", "3571", "3572", "3573", "3580", "3581", "3585", "3591", "3592", "3593", "3598", "3599", "3603", "3607", "3608", "3612", "3620", "3624", "3625", "3632", "3636", "3637", "3644", "3648", "3649", "3654", "3660", "3662"]
        }
      },
      {
        "path": "/workspace/eventhubs-kafka-dotnet/src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs",
        "statuses": {
          "CompileError": ["3674", "3675", "3701", "3702", "3772", "3773", "3780", "3814", "3815", "3816"],
          "Ignored": ["3663", "3677", "3679", "3707", "3710", "3715", "3727", "3729", "3736", "3774", "3776", "3783", "3803", "3805", "3811", "3813"],
          "Killed": ["3664", "3665", "3666", "3669", "3670", "3671", "3672", "3673", "3676", "3678", "3683", "3684", "3685", "3686", "3687", "3688", "3689", "3690", "3691", "3692", "3693", "3694", "3695", "3696", "3697", "3698", "3699", "3700", "3703", "3704", "3705", "3708", "3709", "3711", "3713", "3714", "3716", "3718", "3719", "3720", "3721", "3722", "3723", "3724", "3726", "3728", "3734", "3735", "3737", "3738", "3739", "3740", "3741", "3742", "3743", "3744", "3745", "3746", "3747", "3748", "3749", "3750", "3751", "3752", "3753", "3754", "3755", "3756", "3757", "3758", "3759", "3760", "3761", "3762", "3763", "3764", "3765", "3766", "3767", "3768", "3769", "3770", "3771", "3775", "3777", "3778", "3779", "3781", "3782", "3786", "3787", "3788", "3789", "3790", "3791", "3792", "3793", "3794", "3795", "3796", "3797", "3798", "3799", "3800", "3801", "3802", "3804", "3806", "3807", "3808", "3812", "3817", "3819", "3820", "3821", "3822"],
          "Survived": ["3667", "3668", "3680", "3681", "3682", "3706", "3712", "3717", "3725", "3730", "3731", "3732", "3733", "3784", "3785", "3809", "3810", "3818", "3823", "3824"]
        }
      }
    ]
  },
  "sample_replay_inventory": {
    "completed_sample_count": 359,
    "sample_outcome_counts": {
      "compile_error": 17,
      "complete_test_failures_cause_review": 1,
      "validated_pass": 341
    },
    "sampled_fresh_ids_by_outcome": {
      "compile_error": ["39", "218", "245", "254", "1091", "1331", "1332", "1344", "1345", "1596", "1600", "1605", "1607", "1617", "1619", "1631", "1633"],
      "complete_test_failures_cause_review": ["2134"],
      "validated_pass": ["40", "112", "113", "115", "116", "120", "121", "126", "127", "130", "131", "134", "135", "141", "142", "154", "155", "159", "160", "164", "165", "166", "170", "171", "174", "175", "183", "184", "187", "188", "213", "219", "225", "227", "228", "229", "230", "231", "232", "235", "236", "237", "238", "239", "240", "243", "246", "250", "252", "255", "261", "263", "359", "361", "366", "370", "485", "489", "490", "491", "495", "496", "497", "501", "502", "503", "507", "508", "509", "513", "514", "515", "519", "520", "521", "526", "527", "532", "533", "534", "540", "541", "545", "546", "547", "549", "550", "557", "558", "560", "564", "568", "569", "570", "574", "575", "576", "578", "579", "600", "607", "608", "619", "623", "624", "625", "629", "630", "631", "635", "636", "637", "641", "645", "648", "656", "659", "660", "664", "665", "666", "668", "674", "678", "679", "680", "683", "686", "688", "689", "695", "699", "700", "701", "705", "706", "707", "713", "714", "715", "718", "721", "722", "723", "727", "729", "730", "731", "734", "737", "740", "743", "746", "758", "762", "763", "764", "768", "769", "770", "773", "776", "779", "782", "790", "793", "797", "798", "799", "801", "813", "817", "818", "819", "823", "824", "825", "829", "830", "831", "834", "837", "840", "843", "845", "846", "852", "853", "854", "856", "862", "870", "871", "873", "874", "880", "881", "882", "900", "901", "907", "910", "913", "923", "927", "931", "933", "935", "936", "944", "946", "961", "963", "973", "983", "989", "990", "999", "1000", "1001", "1010", "1011", "1012", "1017", "1018", "1019", "1028", "1034", "1035", "1036", "1044", "1045", "1046", "1051", "1056", "1065", "1067", "1068", "1076", "1081", "1083", "1092", "1112", "1113", "1123", "1131", "1132", "1143", "1144", "1147", "1149", "1155", "1162", "1167", "1174", "1193", "1198", "1199", "1202", "1204", "1211", "1214", "1220", "1236", "1257", "1273", "1293", "1317", "1328", "1338", "1341", "1342", "1347", "1350", "1351", "1372", "1373", "1374", "1375", "1378", "1379", "1391", "1421", "1431", "1435", "1439", "1443", "1447", "1452", "1454", "1458", "1468", "1472", "1474", "1475", "1506", "1512", "1518", "1524", "1530", "1536", "1558", "1561", "1564", "1565", "1570", "1572", "1575", "1577", "1578", "1580", "1581", "1582", "1591", "1593", "1595", "1597", "1598", "1602", "1603", "1604", "1608", "1609", "1610", "1615", "1620", "1621", "1622", "1626", "1627", "1629", "1634", "1636", "1642", "1692", "1764", "1940", "2285", "3127", "3402", "3706"]
    },
    "not_reviewed_count": 262,
    "not_reviewed_fresh_ids": ["1635", "1637", "1659", "1665", "1669", "1671", "1690", "1699", "1702", "1705", "1706", "1708", "1710", "1712", "1714", "1716", "1718", "1720", "1723", "1725", "1727", "1730", "1732", "1734", "1736", "1738", "1740", "1742", "1744", "1746", "1748", "1750", "1752", "1754", "1756", "1758", "1760", "1762", "1765", "1766", "1768", "1770", "1772", "1774", "1776", "1778", "1782", "1790", "1792", "1794", "1796", "1798", "1800", "1802", "1804", "1806", "1808", "1810", "1812", "1814", "1816", "1818", "1820", "1822", "1827", "1830", "1832", "1834", "1836", "1841", "1850", "1851", "1852", "1854", "1856", "1858", "1860", "1862", "1864", "1874", "1875", "1876", "1882", "1898", "1905", "1914", "1924", "1929", "1934", "1945", "1946", "1949", "1951", "1953", "1957", "1958", "1959", "1963", "1964", "1966", "2005", "2068", "2069", "2070", "2074", "2082", "2083", "2084", "2102", "2103", "2107", "2111", "2112", "2116", "2123", "2127", "2128", "2135", "2139", "2140", "2147", "2151", "2152", "2157", "2176", "2177", "2204", "2205", "2284", "2286", "2294", "2295", "2296", "2855", "2856", "2860", "2867", "2868", "2869", "2875", "2876", "2880", "2887", "2888", "2890", "2891", "2899", "2900", "2904", "2908", "2909", "2910", "2918", "2919", "2924", "2965", "2966", "2970", "2977", "2981", "2985", "2986", "2993", "2997", "2998", "3005", "3009", "3010", "3015", "3019", "3020", "3024", "3028", "3029", "3033", "3035", "3040", "3041", "3047", "3054", "3100", "3101", "3233", "3234", "3235", "3236", "3240", "3241", "3245", "3253", "3257", "3258", "3265", "3269", "3270", "3277", "3281", "3282", "3287", "3293", "3301", "3307", "3342", "3343", "3344", "3345", "3496", "3497", "3501", "3506", "3513", "3514", "3515", "3562", "3563", "3567", "3571", "3572", "3573", "3580", "3581", "3585", "3591", "3592", "3593", "3598", "3599", "3603", "3607", "3608", "3612", "3620", "3624", "3625", "3632", "3636", "3637", "3644", "3648", "3649", "3654", "3660", "3662", "3667", "3668", "3680", "3681", "3682", "3712", "3717", "3725", "3730", "3731", "3732", "3733", "3784", "3785", "3809", "3810", "3818", "3823", "3824"],
    "completed_replay_batches_by_fresh_id": {
      "39": "batch-1",
      "40": "batch-2",
      "112": "batch-1",
      "113": "batch-2",
      "115": "batch-1",
      "116": "batch-2",
      "120": "batch-1",
      "121": "batch-2",
      "126": "batch-1",
      "127": "batch-2",
      "130": "batch-1",
      "131": "batch-2",
      "134": "batch-1",
      "135": "batch-2",
      "141": "batch-1",
      "142": "batch-2",
      "154": "batch-1",
      "155": "batch-2",
      "159": "batch-1",
      "160": "batch-2",
      "164": "batch-1",
      "165": "batch-2",
      "166": "batch-1",
      "170": "batch-2",
      "171": "batch-1",
      "174": "batch-2",
      "175": "batch-1",
      "183": "batch-2",
      "184": "batch-1",
      "187": "batch-2",
      "188": "batch-1",
      "213": "batch-2",
      "218": "batch-1",
      "219": "batch-2",
      "225": "batch-1",
      "227": "batch-2",
      "228": "batch-1",
      "229": "batch-2",
      "230": "batch-1",
      "231": "batch-2",
      "232": "batch-1",
      "235": "batch-2",
      "236": "batch-1",
      "237": "batch-2",
      "238": "batch-1",
      "239": "batch-2",
      "240": "batch-1",
      "243": "batch-2",
      "245": "batch-1",
      "246": "batch-2",
      "250": "batch-1",
      "252": "batch-2",
      "254": "batch-1",
      "255": "batch-2",
      "261": "batch-1",
      "263": "batch-2",
      "359": "batch-1",
      "361": "batch-2",
      "366": "batch-1",
      "370": "batch-2",
      "485": "batch-1",
      "489": "batch-2",
      "490": "batch-1",
      "491": "batch-2",
      "495": "batch-1",
      "496": "batch-2",
      "497": "batch-1",
      "501": "batch-2",
      "502": "batch-1",
      "503": "batch-2",
      "507": "batch-1",
      "508": "batch-2",
      "509": "batch-1",
      "513": "batch-2",
      "514": "batch-1",
      "515": "batch-2",
      "519": "batch-1",
      "520": "batch-2",
      "521": "batch-1",
      "526": "batch-2",
      "527": "batch-1",
      "532": "batch-2",
      "533": "batch-1",
      "534": "batch-2",
      "540": "batch-1",
      "541": "batch-2",
      "545": "batch-1",
      "546": "batch-2",
      "547": "batch-1",
      "549": "batch-2",
      "550": "batch-1",
      "557": "batch-2",
      "558": "batch-1",
      "560": "batch-2",
      "564": "batch-1",
      "568": "batch-2",
      "569": "batch-1",
      "570": "batch-2",
      "574": "batch-1",
      "575": "batch-2",
      "576": "batch-1",
      "578": "batch-2",
      "579": "batch-1",
      "600": "batch-2",
      "607": "batch-1",
      "608": "batch-2",
      "619": "batch-1",
      "623": "batch-2",
      "624": "batch-1",
      "625": "batch-2",
      "629": "batch-1",
      "630": "batch-2",
      "631": "batch-1",
      "635": "batch-2",
      "636": "batch-1",
      "637": "batch-2",
      "641": "batch-1",
      "645": "batch-2",
      "648": "batch-1",
      "656": "batch-2",
      "659": "batch-1",
      "660": "batch-2",
      "664": "batch-1",
      "665": "batch-2",
      "666": "batch-1",
      "668": "batch-2",
      "674": "batch-1",
      "678": "batch-2",
      "679": "batch-1",
      "680": "batch-2",
      "683": "batch-1",
      "686": "batch-2",
      "688": "batch-1",
      "689": "batch-2",
      "695": "batch-1",
      "699": "batch-2",
      "700": "batch-1",
      "701": "batch-2",
      "705": "batch-1",
      "706": "batch-2",
      "707": "batch-1",
      "713": "batch-2",
      "714": "batch-1",
      "715": "batch-2",
      "718": "batch-1",
      "721": "batch-2",
      "722": "batch-1",
      "723": "batch-2",
      "727": "batch-1",
      "729": "batch-2",
      "730": "batch-1",
      "731": "batch-2",
      "734": "batch-1",
      "737": "batch-2",
      "740": "batch-1",
      "743": "batch-2",
      "746": "batch-1",
      "758": "batch-2",
      "762": "batch-1",
      "763": "batch-2",
      "764": "batch-1",
      "768": "batch-2",
      "769": "batch-1",
      "770": "batch-2",
      "773": "batch-1",
      "776": "batch-2",
      "779": "batch-1",
      "782": "batch-2",
      "790": "batch-1",
      "793": "batch-2",
      "797": "batch-1",
      "798": "batch-2",
      "799": "batch-1",
      "801": "batch-2",
      "813": "batch-1",
      "817": "batch-2",
      "818": "batch-1",
      "819": "batch-2",
      "823": "batch-1",
      "824": "batch-2",
      "825": "batch-1",
      "829": "batch-2",
      "830": "batch-1",
      "831": "batch-2",
      "834": "batch-1",
      "837": "batch-2",
      "840": "batch-1",
      "843": "batch-2",
      "845": "batch-1",
      "846": "batch-2",
      "852": "batch-1",
      "853": "batch-2",
      "854": "batch-1",
      "856": "batch-2",
      "862": "batch-1",
      "870": "batch-2",
      "871": "batch-1",
      "873": "batch-2",
      "874": "batch-1",
      "880": "batch-2",
      "881": "batch-1",
      "882": "batch-2",
      "900": "batch-1",
      "901": "batch-2",
      "907": "batch-1",
      "910": "batch-2",
      "913": "batch-1",
      "923": "batch-2",
      "927": "batch-1",
      "931": "batch-2",
      "933": "batch-1",
      "935": "batch-2",
      "936": "batch-1",
      "944": "batch-2",
      "946": "batch-1",
      "961": "batch-2",
      "963": "batch-1",
      "973": "batch-2",
      "983": "batch-1",
      "989": "batch-2",
      "990": "batch-1",
      "999": "batch-2",
      "1000": "batch-1",
      "1001": "batch-2",
      "1010": "batch-1",
      "1011": "batch-2",
      "1012": "batch-1",
      "1017": "batch-2",
      "1018": "batch-1",
      "1019": "batch-2",
      "1028": "batch-1",
      "1034": "batch-2",
      "1035": "batch-1",
      "1036": "batch-2",
      "1044": "batch-1",
      "1045": "batch-2",
      "1046": "batch-1",
      "1051": "batch-2",
      "1056": "batch-1",
      "1065": "batch-2",
      "1067": "batch-1",
      "1068": "batch-2",
      "1076": "batch-1",
      "1081": "batch-2",
      "1083": "batch-1",
      "1091": "batch-2",
      "1092": "batch-1",
      "1112": "batch-2",
      "1113": "batch-1",
      "1123": "batch-2",
      "1131": "batch-1",
      "1132": "batch-2",
      "1143": "batch-1",
      "1144": "batch-2",
      "1147": "batch-1",
      "1149": "batch-2",
      "1155": "batch-1",
      "1162": "batch-2",
      "1167": "batch-1",
      "1174": "batch-2",
      "1193": "batch-1",
      "1198": "batch-2",
      "1199": "batch-1",
      "1202": "batch-2",
      "1204": "batch-1",
      "1211": "batch-2",
      "1214": "batch-1",
      "1220": "batch-2",
      "1236": "batch-1",
      "1257": "batch-2",
      "1273": "batch-1",
      "1293": "batch-2",
      "1317": "batch-1",
      "1328": "batch-2",
      "1331": "batch-1",
      "1332": "batch-2",
      "1338": "batch-1",
      "1341": "batch-2",
      "1342": "batch-1",
      "1344": "batch-2",
      "1345": "batch-1",
      "1347": "batch-2",
      "1350": "batch-1",
      "1351": "batch-2",
      "1372": "batch-1",
      "1373": "batch-2",
      "1374": "batch-1",
      "1375": "batch-2",
      "1378": "batch-1",
      "1379": "batch-2",
      "1391": "batch-1",
      "1421": "batch-2",
      "1431": "batch-1",
      "1435": "batch-2",
      "1439": "batch-1",
      "1443": "batch-2",
      "1447": "batch-1",
      "1452": "batch-2",
      "1454": "batch-1",
      "1458": "batch-2",
      "1468": "batch-1",
      "1472": "batch-2",
      "1474": "batch-1",
      "1475": "batch-2",
      "1506": "batch-1",
      "1512": "batch-2",
      "1518": "batch-1",
      "1524": "batch-2",
      "1530": "batch-1",
      "1536": "batch-2",
      "1558": "batch-1",
      "1561": "batch-2",
      "1564": "batch-1",
      "1565": "batch-2",
      "1570": "batch-1",
      "1572": "batch-2",
      "1575": "batch-1",
      "1577": "batch-2",
      "1578": "batch-1",
      "1580": "batch-2",
      "1581": "batch-1",
      "1582": "batch-2",
      "1591": "batch-1",
      "1593": "batch-2",
      "1595": "batch-1",
      "1596": "batch-2",
      "1597": "batch-1",
      "1598": "batch-2",
      "1600": "batch-1",
      "1602": "batch-2",
      "1603": "batch-1",
      "1604": "batch-2",
      "1605": "batch-1",
      "1607": "batch-2",
      "1608": "batch-1",
      "1609": "batch-2",
      "1610": "batch-1",
      "1615": "batch-2",
      "1617": "batch-1",
      "1619": "batch-2",
      "1620": "batch-1",
      "1621": "batch-2",
      "1622": "batch-1",
      "1626": "batch-2",
      "1627": "batch-1",
      "1629": "batch-2",
      "1631": "batch-1",
      "1633": "batch-2",
      "1634": "batch-1",
      "1636": "batch-1",
      "1642": "batch-1",
      "1692": "gap-fill",
      "1764": "gap-fill",
      "1940": "gap-fill",
      "2134": "gap-fill",
      "2285": "gap-fill",
      "3127": "gap-fill",
      "3402": "gap-fill",
      "3706": "gap-fill"
    },
    "replay_csv_sha256": {
      ".artifacts/verifier-mutation/fresh-replays/batch-1/mutants.csv": "1f9af26bc7447a1e3b99ea218477fbe0f87aa797773ecd1c3c1786eb1e2437f3",
      ".artifacts/verifier-mutation/fresh-replays/batch-2/mutants.csv": "0ae0bd2e494a8541403436963d269b1233859f01856710b4a1af9c6c06d16cd8",
      ".artifacts/verifier-mutation/fresh-replays/gap-fill/mutants.csv": "66938761c2a347fed61cdcd9b002e8586eade6a402ef3f4999f4b56abc37f4c3"
    },
    "replay_run_json_sha256": {
      ".artifacts/verifier-mutation/fresh-replays/batch-1/run.json": "12ecff00bd7da5225a24540b1be5d0f732f90dee3f9602f2330b7a122fb36c2a",
      ".artifacts/verifier-mutation/fresh-replays/batch-2/run.json": "0f867494f69fa23a989b444ed1892baf007322d4f1b0f43252559f218df2acc1",
      ".artifacts/verifier-mutation/fresh-replays/gap-fill/run.json": "b6942617e1c97d7e6ee86d268dd05de558f8f5a1ecf2ae248337aa7fe303d259"
    },
    "retention_manifest_sha256": "5001d7cbaad5bb0d80c7d8db6db98a8d8eb03b6744d3827b4d540e68c8ef3274"
  },
  "count_integrity": {
    "all_report_ids_unique": true,
    "status_groups_cover_every_report_id_once": true,
    "completed_sample_ids_unique": true,
    "sample_and_not_reviewed_partition_fresh_survivors": true
  }
}
```

</details>

#### Raw input provenance

| Artifact path | SHA-256 |
| --- | --- |
| `.artifacts/verifier-mutation/fresh-replays/batch-1/baseline/results/results.xml` | `57956f34061636d7a067448a52b414909e081b19b1691f112aacc5403fc3d9ab` |
| `.artifacts/verifier-mutation/fresh-replays/batch-1/mutants.csv` | `1f9af26bc7447a1e3b99ea218477fbe0f87aa797773ecd1c3c1786eb1e2437f3` |
| `.artifacts/verifier-mutation/fresh-replays/batch-1/run.json` | `12ecff00bd7da5225a24540b1be5d0f732f90dee3f9602f2330b7a122fb36c2a` |
| `.artifacts/verifier-mutation/fresh-replays/batch-2/mutants.csv` | `0ae0bd2e494a8541403436963d269b1233859f01856710b4a1af9c6c06d16cd8` |
| `.artifacts/verifier-mutation/fresh-replays/batch-2/run.json` | `0f867494f69fa23a989b444ed1892baf007322d4f1b0f43252559f218df2acc1` |
| `.artifacts/verifier-mutation/fresh-replays/gap-fill/mutants.csv` | `66938761c2a347fed61cdcd9b002e8586eade6a402ef3f4999f4b56abc37f4c3` |
| `.artifacts/verifier-mutation/fresh-replays/gap-fill/run.json` | `b6942617e1c97d7e6ee86d268dd05de558f8f5a1ecf2ae248337aa7fe303d259` |
| `.artifacts/verifier-mutation/fresh-replays/retention-manifest.json` | `5001d7cbaad5bb0d80c7d8db6db98a8d8eb03b6744d3827b4d540e68c8ef3274` |
| `.artifacts/verifier-mutation/resumed-validation/build.log` | `8237d6bf24adaab02c4df85204e3cf0a526c28b8e6ceff9bf89c04071a9ded06` |
| `.artifacts/verifier-mutation/resumed-validation/docs-check.log` | `3bb854fb916f7009d8f139c6a427acd677b9641997dd0595a01a3d0b6be9ad1e` |
| `.artifacts/verifier-mutation/resumed-validation/docs-tests.log` | `76ba2de37e61ad23ea3edb58453999c0419d97a4276b285b79238804f157bcb9` |
| `.artifacts/verifier-mutation/resumed-validation/full-verification.log` | `8e1ed1800afc7e1b40dfd2bec53e668ca4c058b50f6833581d8a155a02367422` |
| `.artifacts/verifier-mutation/resumed-validation/validation.json` | `da094d3fd15b308ea731f8547b16adbfc52397a51f254c986c46e40b90e93b81` |
| `.artifacts/verifier-mutation/run.y53la5z6/command.log` | `f2f2a8d2402b3ff641a83d5050fd35b801479197d29238da5a6e95c63dc4fc50` |
| `.artifacts/verifier-mutation/run.y53la5z6/reports/mutation-report.json` | `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d` |
| `.artifacts/verifier-mutation/run.y53la5z6/run.json` | `95e5be349680d1df46fa75d2d8ab51879ea9578d23599816282c83a3d3727933` |
| `/workspace/scratch/build_verifier_measurement_excerpts.py` | `107c951e08cf303edc34cc4d4478d0166e31059ae8cc662dbe3f3e3d7ccd42c9` |
#### Additional measurement fields

These fields complement the measurement excerpts in this document.

##### Source-file inventory and status counts

Report: `/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/run.y53la5z6/reports/mutation-report.json`

SHA-256: `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d`

File entries: 18; entries with at least one mutant: 13; generated mutant records: 3825.

| Source file | Records | Killed | Survived | Timeout | CompileError | Ignored | NoCoverage | RuntimeError | Pending |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `src/Parchwell.Verifier/AssemblyInfo.cs` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Contracts/CheckAssessment.cs` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Contracts/RunIdentity.cs` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Results/VerifierResult.cs` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 1124 | 668 | 250 | 0 | 88 | 118 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 228 | 126 | 34 | 0 | 13 | 55 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Window/StartLineNote.cs` | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 224 | 156 | 33 | 0 | 8 | 27 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/Program.cs` | 115 | 32 | 41 | 5 | 13 | 24 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | 10 | 6 | 2 | 0 | 1 | 1 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 234 | 121 | 82 | 0 | 4 | 27 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifierWorker.cs` | 32 | 6 | 12 | 2 | 0 | 12 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 191 | 130 | 25 | 0 | 18 | 18 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/CapturedArtifacts.cs` | 2 | 2 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 896 | 732 | 57 | 0 | 46 | 61 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 291 | 207 | 27 | 0 | 34 | 23 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 316 | 229 | 38 | 0 | 20 | 29 | 0 | 0 | 0 |
| `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 162 | 116 | 20 | 0 | 10 | 16 | 0 | 0 | 0 |
| **Total** | **3825** | **2531** | **621** | **7** | **255** | **411** | **0** | **0** | **0** |

The 18 source-file entries include five with zero generated mutants; 13 have one or more. Status totals are counted from each file entry's `mutants` array.

##### Compile-error and ignored status metadata

Every CompileError record has `statusReason: "Mutant caused compile errors"` (255 records total). Every Ignored record has mutator `Block removal mutation`, replacement `{}`, and `statusReason: "Removed by block already covered filter"` (411 records total).

| Status | Mutator name | Records |
| --- | --- | ---: |
| CompileError | `Arithmetic mutation` | 11 |
| CompileError | `Block removal mutation` | 45 |
| CompileError | `Conditional (false) mutation` | 2 |
| CompileError | `Conditional (true) mutation` | 2 |
| CompileError | `Equality mutation` | 56 |
| CompileError | `Linq method mutation (AsEnumerable() to Reverse())` | 1 |
| CompileError | `Linq method mutation (Count() to Sum())` | 42 |
| CompileError | `Logical mutation` | 35 |
| CompileError | `LogicalNotExpression to un-LogicalNotExpression mutation` | 35 |
| CompileError | `Negate expression` | 22 |
| CompileError | `Statement mutation` | 1 |
| CompileError | `String mutation` | 3 |
| Ignored | `Block removal mutation` | 411 |

##### Timeout rows from report

`statusReason` is absent for all seven Timeout rows. The report has empty `killedBy` arrays for all seven; the table does not infer which test or host behavior caused a timeout. `coveredBy` is omitted because the report associates tests with a mutant but does not establish a per-test execution outcome.

| ID | Source | Span | Mutator | Replacement | `killedBy` count | `statusReason` |
| ---: | --- | --- | --- | --- | ---: | --- |
| 1585 | `src/Parchwell.Verifier/Program.cs` | `50:9-50:61` | `Statement mutation` | `;` | 0 | not supplied |
| 1588 | `src/Parchwell.Verifier/Program.cs` | `67:25-67:60` | `Null coalescing mutation (remove left)` | `TimeProvider.System` | 0 | not supplied |
| 1590 | `src/Parchwell.Verifier/Program.cs` | `75:17-75:63` | `Statement mutation` | `;` | 0 | not supplied |
| 1648 | `src/Parchwell.Verifier/Program.cs` | `176:64-176:89` | `Equality mutation` | `lifetime > TimeSpan.Zero` | 0 | not supplied |
| 1651 | `src/Parchwell.Verifier/Program.cs` | `182:13-182:30` | `Statement mutation` | `;` | 0 | not supplied |
| 1955 | `src/Parchwell.Verifier/VerifierWorker.cs` | `85:13-85:52` | `Statement mutation` | `;` | 0 | not supplied |
| 1961 | `src/Parchwell.Verifier/VerifierWorker.cs` | `134:13-134:31` | `Equality mutation` | `_timer is null` | 0 | not supplied |

##### Test metadata group reconciliation

The Stryker report contains 1044 test metadata records. Reconciliation with the ordinary XML groups them into 68 exact source/type/method keys; the `Rows` column records per-method test-case multiplicity. Literal test IDs are format-specific and are not treated as equivalent identifiers.

| Source file | Type | Method | Rows | Stryker source span |
| --- | --- | --- | ---: | --- |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_AnyConfiguredModeWithoutHandler_When_WorkerRuns_Then_ExitsNonzeroWithoutOutput` | 3 | `270:5-281:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_BlankRequiredTextOption_When_Validated_Then_ReportsStartupFailure` | 42 | `83:5-91:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_DuplicateRepeatResultPaths_When_OptionsAreValidated_Then_PreservesThemForCheck32` | 1 | `320:5-330:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ExpectedIdentityCardinalityOtherThanFive_When_OptionsAreValidated_Then_PreservesItForCheck32` | 4 | `332:5-346:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MalformedDuration_When_Bound_Then_ReportsStartupFailure` | 1 | `179:5-189:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MalformedInactiveBudget_When_HostBindsOptions_Then_IgnoresTheValue` | 8 | `217:5-249:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MissingOrUnknownMode_When_HostStarts_Then_ExitsNonzeroBeforeIo` | 4 | `23:5-39:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_MissingRequiredOption_When_Validated_Then_ReportsStartupFailure` | 99 | `70:5-81:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_NonpositiveRequiredBudget_When_Validated_Then_ReportsStartupFailure` | 38 | `153:5-164:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_NonpositiveRequiredLimit_When_Validated_Then_ReportsStartupFailure` | 62 | `166:5-177:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProbeGateMode_When_HostBuilds_Then_RequiresNoKafkaOrAzureServices` | 1 | `251:5-268:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProcessLifetimeAtBudgetSum_When_Validated_Then_RejectsEqualityAndAcceptsGreaterValue` | 1 | `191:5-215:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124` | 2 | `348:5-390:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_RelativeBlobServiceUri_When_Validated_Then_ReportsStartupFailure` | 2 | `93:5-102:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_UnexpectedRepeatInputCardinality_When_OptionsAreValidated_Then_PreservesItForFutureGateJudgement` | 4 | `304:5-318:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierHostTests` | `Given_WorkerExceptionCancellationOrHostStop_When_ExecutionEnds_Then_DoesNotReportSuccess` | 3 | `283:5-302:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Host/VerifierHostTests.cs` | `Parchwell.Verifier.Tests.Host.VerifierWatchdogChildProcessTests` | `Given_ChildWatchdogMarker_When_Run_Then_UsesProductionHardExit` | 1 | `698:5-719:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/GoldenArtifactReaderTests.cs` | `Parchwell.Verifier.Tests.Input.GoldenArtifactReaderTests` | `Given_GoldenArtifacts_When_IndependentReadersParse_Then_PreserveSchemaOneEvidence` | 1 | `11:5-163:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_DuplicatePropertyAtNestedDepth_When_Read_Then_ReportsDuplicateToOwner` | 217 | `426:5-445:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InputExceedsConfiguredLimits_When_Read_Then_ThrowsExecutionLimitWithoutCheckRouting` | 1 | `705:5-774:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InvalidEscapedSurrogateInPropertyOrValue_When_Read_Then_RoutesAsMalformedJson` | 30 | `477:5-493:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_InvalidUtf8TruncatedOrBadJsonlFraming_When_Read_Then_RoutesEnvelopeProblem` | 1 | `447:5-475:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MalformedIndex_When_Read_Then_ReturnsExecutionFailure` | 2 | `810:5-824:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MalformedRawJsonInValidStatisticsEnvelope_When_Read_Then_EnvelopeAndNativeProblemsStaySeparate` | 1 | `692:5-703:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingIndex_When_Read_Then_ReturnsExecutionFailure` | 1 | `795:5-808:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingInputSource_When_Read_Then_RoutesAbsenceToItsOwner` | 1 | `776:5-793:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_MissingOrNullCompletionReceipt_When_CapabilitiesAreRead_Then_PreservesPresenceAndFalseValues` | 1 | `34:5-76:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_NativeRawReportHasIndependentShape_When_Read_Then_AllowsExtraFieldsAndRoutesNativeProblems` | 1 | `571:5-690:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_NullCallbackPersistenceAndNullableLifecycleReason_When_Read_Then_PreservesNullWithoutValidityDecision` | 1 | `78:5-96:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_OnlyHarnessOwnedScalarValues_When_Read_Then_UsesExactWriterSetsAndKeepsOrdinaryStringsFree` | 1 | `98:5-146:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_ReorderedPropertiesAndUninterpretedEvidence_When_Read_Then_PreservesDataWithoutSemanticGates` | 1 | `148:5-266:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_RequiredPropertyHasWrongJsonKind_When_Read_Then_RoutesProblemToOwningCheck` | 217 | `24:5-32:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_RequiredPropertyMissing_When_Read_Then_RoutesProblemToOwningCheck` | 217 | `14:5-22:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_StartLineContainerHasUnknownOrDuplicateProperty_When_Read_Then_RoutesToCheckSeven` | 6 | `382:5-412:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_StartLineRequiredPropertyInvalid_When_Read_Then_RoutesProblemToCheckSeven` | 32 | `304:5-316:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_UnknownHarnessField_When_EnvelopeIsRead_Then_RejectsItAtEachClosedShape` | 1 | `330:5-380:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_UnsupportedHarnessScalarsOrSchema_When_Read_Then_RoutesStructuralProblem` | 1 | `268:5-302:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Input/StrictParserTests.cs` | `Parchwell.Verifier.Tests.Input.StrictParserTests` | `Given_ValidEscapedSurrogatePairInValuesAndOpenMapKeys_When_Read_Then_PreservesDecodedText` | 1 | `507:5-522:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_A01RecordedEvidence_When_RoundTripped_Then_PreservesSelectedInt64FieldsAndOmissions` | 1 | `502:5-547:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_AbsentAndObservedEmptyBoundaries_When_RoundTripped_Then_PreservesTheDistinction` | 1 | `228:5-280:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_AssessmentSnippetExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit` | 1 | `628:5-637:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_BrokerCountLowerBoundFalse_When_Read_Then_ReturnsStructuralProblem` | 1 | `452:5-462:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_BrokerCountProjection_When_RoundTripped_Then_PreservesOnlySuppliedMeasurements` | 1 | `411:5-450:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResultWithObservedZeros_When_Serialized_Then_AllSixZeroCountsRemainPresent` | 1 | `62:5-73:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResultWithoutCounts_When_Serialized_Then_ThrowsCodecError` | 1 | `75:5-81:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompleteValidResult_When_Serialized_Then_AllSixOutcomeKeysArePresent` | 1 | `37:5-60:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_CompletedDrain_When_RoundTripped_Then_EmitsOnlyDrainTimeInMilliseconds` | 2 | `110:5-127:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ConfigurationFidelityDifferences_When_RoundTripped_Then_EmitsBoundedSnakeCaseProjection` | 1 | `464:5-500:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_DiagnosticAndAssessmentExceedPerCheckLimit_When_Read_Then_ThrowsExecutionLimit` | 1 | `639:5-651:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_DiagnosticsForDifferentChecks_When_Read_Then_JsonlRowLimitDoesNotApply` | 1 | `598:5-614:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_FailedAndUnevaluatedAssessments_When_RoundTripped_Then_PreservesTheirDistinctStates` | 1 | `83:5-108:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_HashAndBoundedDiagnostics_When_Serialized_Then_EmitsOnlyResultProjection` | 1 | `157:5-187:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_InvalidButUsableProbeProjection_When_RoundTripped_Then_PreservesIndependentEvidence` | 1 | `364:5-409:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_InvalidOrIncompleteResult_When_Serialized_Then_AllSixOutcomeKeysAreAbsent` | 2 | `21:5-35:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_MalformedStartLineNote_When_Read_Then_ReturnsStructuralProblemForFutureCheckSeven` | 1 | `351:5-362:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_MissingNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem` | 1 | `315:5-325:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_NegativeFractionalDuration_When_Serialized_Then_TruncatesTowardZero` | 1 | `571:5-580:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_NullableDiagnosticFields_When_RoundTripped_Then_EmitsAndReadsExplicitNulls` | 1 | `282:5-301:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_OutputExceedsConfiguredBytes_When_Serialized_Then_ThrowsExecutionLimit` | 1 | `549:5-558:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_PartitionCountExceedsConfiguredMaximum_When_ReadStartLine_Then_ThrowsExecutionLimit` | 1 | `653:5-662:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_PreBoundaryOrOutOfRangeOrIoTermination_When_Serialized_Then_BothDrainFieldsAreAbsent` | 2 | `144:5-155:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ProducerAndVerifierVersionsDiffer_When_RoundTripped_Then_CopiesEachSourceSeparately` | 1 | `189:5-226:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_ResultExceedsAggregateEntryLimit_When_Read_Then_ThrowsExecutionLimit` | 1 | `582:5-596:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_SafetyStopDuration_When_RoundTripped_Then_EmitsOnlyLabelledLowerBound` | 1 | `129:5-142:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_StartLineNote_When_RoundTripped_Then_PreservesOffsetsWithoutSelfHash` | 1 | `327:5-349:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_UnicodeDiagnosticExceedsConfiguredUtf8Limit_When_Read_Then_ThrowsExecutionLimit` | 1 | `616:5-626:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_UnicodeDiagnosticExceedsUtf8Limit_When_Serialized_Then_ThrowsExecutionLimit` | 1 | `560:5-569:6` |
| `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests/Results/VerifierResultCodecTests.cs` | `Parchwell.Verifier.Tests.Results.VerifierResultCodecTests` | `Given_WrongKindNullableDiagnosticField_When_Read_Then_ReturnsStructuralProblem` | 1 | `303:5-313:6` |

This table gives the reconciled group names, multiplicities, and Stryker source spans. Span punctuation is normalized to ASCII; method names are retained exactly.
#### Version-option evidence

The plan's version-option command was rechecked while publishing these excerpts; it performs no mutation run. The recorded response and pinned help explain the nonzero result.

```json
{
  "command": [
    "dotnet",
    "stryker",
    "--version"
  ],
  "observed_exit_code": 1,
  "head": "f162e3aebb6d0e369eb144af915f27d200a15513",
  "output": "Specify --help for a list of available options and commands.\nMissing value for option 'version'\n"
}
```

Excerpt from the retained `stryker-help.log`:

```text
  -v|--version                        Project version used in dashboard reporter
                                      and baseline feature. | default: ''
```
### Runner sample coverage

Tested commit `610625f4fa19972c48c8d315442982542b257cd8`; Stryker report SHA-256 `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d`.
The report has 621 survivors. The sample attempted 359 completed replays; 342 reached normal MTP. 262 survivors were not reviewed.

| Replay outcome | Count |
|---|---:|
| Normal MTP validated pass | 341 |
| Build compile error before test | 17 |
| Complete MTP test failure requiring cause review | 1 |
| Selected pending rows ignored | 270 |
| Direct preview-vs-normal-MTP test disagreements | 1 |
| Manual build failures with no normal-MTP comparison | 17 |
| Completed XML reports validated with parse_xunit | 342 |
| Report/replay differences flagged for Hari review | 18 |

#### Mutated source file coverage

| Source file | Mutants | Survivors | Sampled | Coverage |
|---|---:|---:|---:|---|
| `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | 1124 | 250 | 250 | covered |
| `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | 228 | 34 | 34 | covered |
| `src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs` | 224 | 33 | 33 | covered |
| `src/Parchwell.Verifier/Program.cs` | 115 | 41 | 34 | covered |
| `src/Parchwell.Verifier/VerifierHostOptions.cs` | 10 | 2 | 1 | covered |
| `src/Parchwell.Verifier/VerifierHostOptionsValidator.cs` | 234 | 82 | 1 | covered |
| `src/Parchwell.Verifier/VerifierWorker.cs` | 32 | 12 | 1 | covered |
| `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | 191 | 25 | 1 | covered |
| `src/Parchwell.Verifier/VerifyRun/Input/CapturedArtifacts.cs` | 2 | 0 | 0 | no_survivors |
| `src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs` | 896 | 57 | 1 | covered |
| `src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs` | 291 | 27 | 1 | covered |
| `src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs` | 316 | 38 | 1 | covered |
| `src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs` | 162 | 20 | 1 | covered |

src/Parchwell.Verifier/VerifyRun/Input/CapturedArtifacts.cs has 2 Killed mutants and no survivors, so no survivor sample was available.

#### Stryker mutator coverage

All 15 survivor mutator types have at least one normal-MTP execution; none are represented only by compile errors.

| Mutator type | Survivors | Sampled | Coverage |
|---|---:|---:|---|
| Arithmetic mutation | 2 | 2 | covered |
| Block removal mutation | 1 | 1 | covered |
| Boolean mutation | 88 | 87 | covered |
| CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | 3 | 3 | covered |
| Conditional (false) mutation | 3 | 1 | covered |
| Conditional (true) mutation | 4 | 2 | covered |
| Equality mutation | 20 | 17 | covered |
| Linq method mutation (SingleOrDefault() to Single()) | 1 | 1 | covered |
| Logical mutation | 3 | 2 | covered |
| Negate expression | 1 | 1 | covered |
| Null coalescing mutation (remove left) | 2 | 1 | covered |
| PostIncrementExpression to PostDecrementExpression mutation | 8 | 8 | covered |
| Remove checked expression | 3 | 3 | covered |
| Statement mutation | 130 | 59 | covered |
| String mutation | 352 | 171 | covered |

#### Replay/report discrepancies

Stryker report status is preserved separately from the normal replay outcome. Mutant 2134 is the direct preview-vs-normal-MTP disagreement. The 17 compile errors are report/replay differences, but MTP did not execute for them; all 18 are flagged for Hari review:

| Mutant | Source | Stryker status | Replay result | Comparison |
|---:|---|---|---|---|
| 39 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 218 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 245 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 254 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1091 | `src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1331 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1332 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1344 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1345 | `src/Parchwell.Verifier/Common/Serialization/StrictJson.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1596 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1600 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1605 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1607 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1617 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1619 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1631 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 1633 | `src/Parchwell.Verifier/Program.cs` | Survived | Compile error before MTP | MTP did not run; no test comparison |
| 2134 | `src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs` | Survived | Normal assertion failure | Preview survived; normal MTP failed |

Mutant 2134 xUnit report validated as one `Xunit.Sdk.FalseException` failure. The 17 compile errors failed before normal MTP execution.
### Normal compiler diagnostics

These mutants did not reach normal MTP. Diagnostics below are copied from the retained build logs.

| ID | Normal compiler diagnostic |
| ---: | --- |
| 39 | CS0642: Possible mistaken empty statement |
| 218 | CS0642: Possible mistaken empty statement |
| 245 | CS0642: Possible mistaken empty statement |
| 254 | CS0642: Possible mistaken empty statement |
| 1091 | CS0642: Possible mistaken empty statement |
| 1331 | CS0168: The variable 'exception' is declared but never used |
| 1332 | CS0168: The variable 'exception' is declared but never used |
| 1344 | CS0168: The variable 'exception' is declared but never used |
| 1345 | CS0168: The variable 'exception' is declared but never used |
| 1596 | CS0168: The variable 'exception' is declared but never used |
| 1600 | CS8602: Dereference of a possibly null reference. |
| 1605 | CS8604: Possible null reference argument for parameter 'logger' in 'void Program.LogStartupFailed(ILogger logger, Exception exception, string? scenarioId, string? runId)'. |
| 1607 | CS0168: The variable 'exception' is declared but never used |
| 1617 | CS8604: Possible null reference argument for parameter 'logger' in 'void Program.LogCleanupFailed(ILogger logger, Exception exception, string? scenarioId, string? runId)'. |
| 1619 | CS0168: The variable 'exception' is declared but never used |
| 1631 | CS8604: Possible null reference argument for parameter 'logger' in 'void Program.LogDisposalFailed(ILogger logger, Exception exception, string? scenarioId, string? runId)'. |
| 1633 | CS0168: The variable 'exception' is declared but never used |
### Full survivor inventory

Report SHA-256: `feba974fbcf0ab1fda67fd6a42d9fec4a9e3900c792bc81fa4bfb2642b20fe3d`. Tested commit: `610625f4fa19972c48c8d315442982542b257cd8`. Hari's [sampled-survivor decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6247408154) defines a representative preview-runner sample; this table has 621 historical report targets: 359 replayed sample rows and 262 rows marked exactly `not reviewed`.
Completed sample outcomes: 341 validated full-suite passes, 17 compilation failures before normal MTP, and 1 assertion failure. The assertion failure is a preview-runner disagreement requiring Hari review; the mutation score/status counts are unchanged.

#### Command templates

- Interrupted sample replay selector (batch-specific selected ID set substituted for the repeated placeholder): `/opt/codex/runtimes/codex-primary-runtime/dependencies/python/bin/python3 /workspace/scratch/replay_stryker_mutants.py --repo /workspace/eventhubs-kafka-dotnet --report /workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/run.y53la5z6/reports/mutation-report.json --output-dir /workspace/scratch/verifier-fresh-replay-batch-1 --tested-commit 610625f4fa19972c48c8d315442982542b257cd8 --expected-tests 1044 --execute --mutant <each selected historical ID; repeat once per batch ID>`.
- Build: `<pinned-dotnet> build <test-csproj> --configuration Release --no-restore`.
- Test: `<pinned-dotnet> test --project <test-csproj> --configuration Release --no-build --minimum-expected-tests 1 --results-directory <unique-dir> -- --report-xunit-xml --report-xunit-xml-filename results.xml`.
- ValidateReport: `<pinned-dotnet> msbuild scripts/dotnet-verification.proj -t:ValidateReport -p:Report=<unique-dir>/results.xml -p:ExpectedAssembly=Parchwell.Verifier.Tests -nologo`.
- The exact batch argv arrays and selected IDs are retained in `/workspace/eventhubs-kafka-dotnet/.artifacts/verifier-mutation/fresh-replays/fresh-replay-batches.json`; raw per-mutant command argv are retained in the replay CSV.

#### Source aliases

| Alias | Full source path |
| --- | --- |
| Common/Results/VerifierResultCodec.cs | src/Parchwell.Verifier/Common/Results/VerifierResultCodec.cs |
| Common/Serialization/StrictJson.cs | src/Parchwell.Verifier/Common/Serialization/StrictJson.cs |
| Common/Window/StartLineNoteCodec.cs | src/Parchwell.Verifier/Common/Window/StartLineNoteCodec.cs |
| Program.cs | src/Parchwell.Verifier/Program.cs |
| VerifierHostOptions.cs | src/Parchwell.Verifier/VerifierHostOptions.cs |
| VerifierHostOptionsValidator.cs | src/Parchwell.Verifier/VerifierHostOptionsValidator.cs |
| VerifierWorker.cs | src/Parchwell.Verifier/VerifierWorker.cs |
| VerifyRun/Input/ArtifactIndexReader.cs | src/Parchwell.Verifier/VerifyRun/Input/ArtifactIndexReader.cs |
| VerifyRun/Input/CapturedEvidenceReader.cs | src/Parchwell.Verifier/VerifyRun/Input/CapturedEvidenceReader.cs |
| VerifyRun/Input/LedgerReader.cs | src/Parchwell.Verifier/VerifyRun/Input/LedgerReader.cs |
| VerifyRun/Input/ManifestReader.cs | src/Parchwell.Verifier/VerifyRun/Input/ManifestReader.cs |
| VerifyRun/Input/NativeStatisticsReader.cs | src/Parchwell.Verifier/VerifyRun/Input/NativeStatisticsReader.cs |

#### Test aliases

| Alias | Role | Exact current test or candidate |
| --- | --- | --- |
| T001 | candidate | Given_A01BrokerWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T002 | candidate | Given_A01DeliveryCodesWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T003 | candidate | Given_A01DisconnectsWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T004 | candidate | Given_A01NativeTimestampWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T005 | candidate | Given_A01ReportTimestampWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T006 | candidate | Given_A01RequestTimeoutsWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T007 | candidate | Given_A01RoundTripP99WrongKind_When_Read_Then_ReturnsStructuralProblem |
| T008 | candidate | Given_A01ThrottleWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T009 | candidate | Given_A01WindowMalformedAfterValidWindow_When_Read_Then_ReportsIndex |
| T010 | candidate | Given_A01WindowMissingRequiredMember_When_Read_Then_ReturnsStructuralProblem |
| T011 | candidate | Given_A01WindowsWrongContainerKind_When_Read_Then_ReturnsStructuralProblem |
| T012 | candidate | Given_AssessmentArtifactWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T013 | candidate | Given_AssessmentCodeWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T014 | candidate | Given_AssessmentEnumAlternativeAndInvalidTokens_When_Read_Then_PreservesOrRejectsThem |
| T015 | candidate | Given_AssessmentFieldWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T016 | candidate | Given_AssessmentIncompleteClassification_When_RoundTripped_Then_PreservesIncomplete |
| T017 | candidate | Given_AssessmentOptionalEnums_When_Read_Then_PreservesAllTokensAndRejectsUnknown |
| T018 | candidate | Given_AssessmentPassDisposition_When_RoundTripped_Then_PreservesPass |
| T019 | candidate | Given_AssessmentPassOrIncomplete_When_RoundTripped_Then_PreservesAlternateToken |
| T020 | candidate | Given_AssessmentRequiredMemberOrEvaluationMalformed_When_Read_Then_ReturnsStructuralProblem |
| T021 | candidate | Given_AssessmentRowWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T022 | candidate | Given_AssessmentSnippetWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T023 | candidate | Given_BooleanMemberWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T024 | candidate | Given_BrokerClientIdWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T025 | candidate | Given_BrokerCountLowerBoundFalse_When_Read_Then_ReportsExpectedTrue |
| T026 | candidate | Given_BrokerCountLowerBoundFalse_When_Serialized_Then_ThrowsCodecError |
| T027 | candidate | Given_BrokerCountMissingRequiredMember_When_Read_Then_ReturnsStructuralProblem |
| T028 | candidate | Given_BrokerFirstPostDrainWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T029 | candidate | Given_BrokerLowerBoundWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T030 | candidate | Given_BrokerMaximumWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T031 | candidate | Given_BrokerRoleWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T032 | candidate | Given_CompleteValidCountsInvalid_When_Read_Then_ReturnsStructuralProblem |
| T033 | candidate | Given_ConfigurationDifferenceActualWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T034 | candidate | Given_ConfigurationDifferenceExpectedWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T035 | candidate | Given_ConfigurationDifferenceMissingSetting_When_Read_Then_ReturnsStructuralProblem |
| T036 | candidate | Given_ConfigurationDifferenceSettingWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T037 | candidate | Given_ConfigurationFidelityWrongContainerOrRequiredMember_When_Read_Then_ReturnsStructuralProblem |
| T038 | candidate | Given_DiagnosticMalformedMember_When_Read_Then_ReturnsStructuralProblem |
| T039 | candidate | Given_DiagnosticOrAssessmentAtUtf8Limit_When_Read_Then_AcceptsExactBoundary |
| T040 | candidate | Given_DrainElapsedMarkerMalformed_When_Read_Then_ReturnsStructuralProblem |
| T041 | candidate | Given_DrainTimeWrongJsonKind_When_Read_Then_ReturnsStructuralProblem |
| T042 | candidate | Given_DrainTimeWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T043 | candidate | Given_DurationOutsideInt64Milliseconds_When_Serialized_Then_ThrowsCodecError |
| T044 | candidate | Given_FailedCheckElementWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T045 | candidate | Given_JudgedHashMalformed_When_Read_Then_ReturnsStructuralProblem |
| T046 | candidate | Given_MalformedOrPopulatedWatermarkItem_When_Read_Then_PreservesOrRejectsExactMember |
| T047 | candidate | Given_MutuallyExclusiveDurations_When_Serialized_Then_ThrowsCodecError |
| T048 | candidate | Given_NullDiagnosticElement_When_Serialized_Then_ThrowsArgumentNullException |
| T049 | candidate | Given_OptionalInt64PresentWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T050 | candidate | Given_OutputExceedsConfiguredBytes_When_Serialized_Then_ThrowsExecutionLimitWithExpectedText |
| T051 | candidate | Given_PresentOptionalProjectionWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T052 | candidate | Given_ProbeBrokerWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T053 | candidate | Given_ProbeRequiredFieldsMissingOrWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T054 | candidate | Given_ProbeTimestampWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T055 | candidate | Given_ProbeWindowCountWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T056 | candidate | Given_ProbeWindowMaximumWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T057 | candidate | Given_ReadExecutionLimit_When_Exceeded_Then_UsesCurrentExceptionText |
| T058 | candidate | Given_RequiredNullableDiagnosticMember_When_Read_Then_DistinguishesNullMissingAndWrongKind |
| T059 | candidate | Given_ResultFailureProblem_When_Read_Then_UsesExactProblemFields |
| T060 | candidate | Given_ResultIdentityWrongKindOrUnknownProperty_When_Read_Then_ReturnsStructuralProblem |
| T061 | candidate | Given_ResultRequiredVersionFieldWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T062 | candidate | Given_ResultScalarOrOptionalMemberMalformed_When_Read_Then_ReturnsStructuralProblem |
| T063 | candidate | Given_ResultSchemaVersionMalformedOrUnsupported_When_Read_Then_ReturnsStructuralProblem |
| T064 | candidate | Given_ResultSchemaVersionOtherThanOne_When_Serialized_Then_ThrowsCodecError |
| T065 | candidate | Given_ResultStatusTokens_When_Read_Then_AcceptsOnlySpecifiedValues |
| T066 | candidate | Given_ResultStatusUnsupported_When_Read_Then_ReturnsStructuralProblem |
| T067 | candidate | Given_ResultStringWrongKind_When_Read_Then_ReturnsStructuralProblem |
| T068 | candidate | Given_RuntimeVersionMapMalformed_When_Read_Then_ReturnsStructuralProblem |
| T069 | candidate | Given_SecondA01WindowMalformed_When_Read_Then_ReportsIndexedPath |
| T070 | candidate | Given_SecondConfigurationDifferenceMalformed_When_Read_Then_UsesIndexedPath |
| T071 | candidate | Given_SecondConfigurationDifferenceWrongKind_When_Read_Then_UsesIndexedPath |
| T072 | candidate | Given_StartLineWrongContainerKind_When_Read_Then_RejectsMalformedArray |
| T073 | candidate | Given_StartLineWrongContainerKind_When_Read_Then_ReportsProblemCode |
| T074 | candidate | Given_StatusConditionedCountsMalformed_When_Read_Then_ReturnsStructuralProblem |
| T075 | candidate | Given_UnknownNestedResultProperty_When_Read_Then_ReturnsStructuralProblem |
| T076 | candidate | Given_WatermarkItemMalformedAfterValidItem_When_Read_Then_ReportsIndex |
| T077 | candidate | Given_WriterAssessmentLimits_When_Serialized_Then_RejectsExcess |
| T078 | candidate | Given_WriterDiagnosticPerCheckBoundary_When_Serialized_Then_AcceptsExactLimit |
| T079 | candidate | Given_WriterDiagnosticPerCheckBoundary_When_Serialized_Then_AcceptsLimitAndRejectsExcess |
| T080 | candidate | Given_WriterDiagnosticPerCheckCounting_When_Serialized_Then_EnforcesConfiguredCap |
| T081 | candidate | Given_WriterDiagnosticPerCheck_When_Serialized_Then_RejectsExcess |
| T082 | candidate | Given_WriterDiagnosticPerCheck_When_Serialized_Then_ThrowsWithExpectedMessage |
| T083 | candidate | Given_WriterDiagnosticUtf8AndPerCheckLimits_When_Serialized_Then_RejectsExcess |
| T084 | candidate | Given_WriterDiagnosticUtf8Limits_When_Serialized_Then_AcceptsBoundaryAndRejectsExcess |
| T085 | candidate | No current covering test; candidate Given_HostConstructionThrows_When_RunAsync_Then_ContainsTheFailure |
| T086 | candidate | No current covering test; candidate Given_NullRunArguments_When_RunAsync_Then_ReturnsNonzeroOrDocumentedArgumentFailure |
| T087 | current | Given_AnyConfiguredModeWithoutHandler_When_WorkerRuns_Then_ExitsNonzeroWithoutOutput |
| T088 | current | Given_CompleteValidResultWithoutCounts_When_Serialized_Then_ThrowsCodecError |
| T089 | current | Given_MalformedDuration_When_Bound_Then_ReportsStartupFailure |
| T090 | current | Given_MalformedInactiveBudget_When_HostBindsOptions_Then_IgnoresTheValue |
| T091 | current | Given_NativeRawReportHasIndependentShape_When_Read_Then_AllowsExtraFieldsAndRoutesNativeProblems |
| T092 | current | Given_ProcessLifetimeExpiresWhileWorkBlocks_When_WatchdogFires_Then_ChildExits124 |
| T093 | current | Given_RequiredPropertyHasWrongJsonKind_When_Read_Then_RoutesProblemToOwningCheck |
| T094 | current | Given_UnsupportedHarnessScalarsOrSchema_When_Read_Then_RoutesStructuralProblem |
| T095 | current | Given_WorkerExceptionCancellationOrHostStop_When_ExecutionEnds_Then_DoesNotReportSuccess |
| T096 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_InputExceedsConfiguredLimits_When_Read_Then_ThrowsExecutionLimitWithoutCheckRouting |
| T097 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_InvalidEscapedSurrogateInPropertyOrValue_When_Read_Then_RoutesAsMalformedJson |
| T098 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_InvalidUtf8TruncatedOrBadJsonlFraming_When_Read_Then_RoutesEnvelopeProblem |
| T099 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_RequiredPropertyHasWrongJsonKind_When_Read_Then_RoutesProblemToOwningCheck |
| T100 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_RequiredPropertyMissing_When_Read_Then_RoutesProblemToOwningCheck |
| T101 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_StartLineRequiredPropertyInvalid_When_Read_Then_RoutesProblemToCheckSeven |
| T102 | current | Parchwell.Verifier.Tests.Input.StrictParserTests.Given_UnknownHarnessField_When_EnvelopeIsRead_Then_RejectsItAtEachClosedShape |
| T103 | current | Parchwell.Verifier.Tests.Results.VerifierResultCodecTests.Given_OutputExceedsConfiguredBytes_When_Serialized_Then_ThrowsExecutionLimit |
| T104 | current | Parchwell.Verifier.Tests.Results.VerifierResultCodecTests.Given_PartitionCountExceedsConfiguredMaximum_When_ReadStartLine_Then_ThrowsExecutionLimit |
| T105 | none | NO COVERING CASE: a syntactically valid lifecycle JSONL row has a non-object root such as [] |
| T106 | none | No covering case: scheduling on the executable Main path; mutations only affect continuation context capture |
| T107 | none | No covering case; candidate Given_EqualVerifierAndLedgerContainers_When_Validated_Then_ReportsStartupFailure |
| T108 | none | No covering case; candidate Given_HostDisposalThrows_When_RunCompletes_Then_LogsAndReturnsFailure |
| T109 | none | No covering case; candidate Given_HostDisposalThrows_When_RunCompletes_Then_LogsScenarioAndRun |
| T110 | none | No covering case; candidate Given_HostDisposalThrows_When_RunCompletes_Then_ReturnsFailure |
| T111 | none | No covering case; candidate Given_HostDisposalThrows_When_RunCompletes_Then_ReturnsFailureAndLogs |
| T112 | none | No covering case; candidate Given_HostStopThrows_When_CleaningUp_Then_LogsScenarioAndRun |
| T113 | none | No covering case; candidate Given_HostStopThrows_When_CleaningUp_Then_ReturnsFailure |
| T114 | none | No covering case; candidate Given_HostStopThrows_When_CleaningUp_Then_ReturnsFailureAndLogs |
| T115 | none | No covering case; candidate Given_StartupFailureAfterHostBuild_When_RunAsync_Then_LogsAndReturnsFailure |
| T116 | none | No covering case; candidate Given_StartupFailureAfterHostBuild_When_RunAsync_Then_LogsScenarioAndRun |
| T117 | none | No covering case; initial VerifierExecutionState exit code is already 1 before any successful handler can run |
| T118 | none | No covering case; no observable difference for the production built-in singleton registration |
| T119 | none | No covering case; runtime type returned by HostApplicationBuilder.Build is IAsyncDisposable |
| T120 | none | No exact test supplied by map |
| T121 | none | no covering case |

#### Per-mutant evidence and adjudication

Replay directories are relative to `.artifacts/verifier-mutation/fresh-replays/`; each contains the retained build/test logs and, where tests ran, the validated XML. `current` names an existing test, while `candidate` names a proposed absent test, not executed coverage. Every un-replayed row is `not reviewed`.

| Historical ID | Source alias | Exact span | Mutator | Replacement (JSON string) | Report status | Test role / alias | Replay batch / directory | Replay outcome | Final disposition | Source-specific proof | Root review evidence added |
| ---: | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 39 | Common/Results/VerifierResultCodec.cs | 38:13-38:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T064 | batch-1/0001-39 | compile_error | preview compilation discrepancy | The schema version guard rejects every value other than 1, while all current fixtures are schema 1; its throw branch is not covered. Exact mutation at line 38: `throw new ArgumentException("Verifier results must use schema version 1.", nameof(result));` -> `;`. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 40 | Common/Results/VerifierResultCodec.cs | 38:41-38:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T064 | batch-2/0001-40 | validated_pass | genuine missing test | The schema version guard rejects every value other than 1, while all current fixtures are schema 1; its throw branch is not covered. Exact mutation at line 38: `"Verifier results must use schema version 1."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 112 | Common/Results/VerifierResultCodec.cs | 106:96-106:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T063 | batch-1/0002-112 | validated_pass | genuine missing test | The root reader requires an Int32 value exactly equal to 1 before parsing identity; current codec roundtrips use only schema_version 1. The exact mutated literal is either the field label or error code. Exact mutation at line 106: `"schema_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 113 | Common/Results/VerifierResultCodec.cs | 106:114-106:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T063 | batch-2/0002-113 | validated_pass | genuine missing test | The root reader requires an Int32 value exactly equal to 1 before parsing identity; current codec roundtrips use only schema_version 1. The exact mutated literal is either the field label or error code. Exact mutation at line 106: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 115 | Common/Results/VerifierResultCodec.cs | 107:58-107:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T063 | batch-1/0003-115 | validated_pass | genuine missing test | The root reader requires an Int32 value exactly equal to 1 before parsing identity; current codec roundtrips use only schema_version 1. The exact mutated literal is either the field label or error code. Exact mutation at line 107: `"schema_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 116 | Common/Results/VerifierResultCodec.cs | 107:76-107:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T063 | batch-2/0003-116 | validated_pass | genuine missing test | The root reader requires an Int32 value exactly equal to 1 before parsing identity; current codec roundtrips use only schema_version 1. The exact mutated literal is either the field label or error code. Exact mutation at line 107: `"unsupported_schema_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 120 | Common/Results/VerifierResultCodec.cs | 110:107-110:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-1/0004-120 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 110: `"generator_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 121 | Common/Results/VerifierResultCodec.cs | 110:128-110:145 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-2/0004-121 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 110: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 126 | Common/Results/VerifierResultCodec.cs | 113:103-113:120 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-1/0005-126 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 113: `"verifier_commit"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 127 | Common/Results/VerifierResultCodec.cs | 113:122-113:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-2/0005-127 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 113: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 130 | Common/Results/VerifierResultCodec.cs | 114:126-114:160 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-1/0006-130 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 114: `"verifier_confluent_kafka_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 131 | Common/Results/VerifierResultCodec.cs | 114:162-114:179 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-2/0006-131 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 114: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 134 | Common/Results/VerifierResultCodec.cs | 115:126-115:155 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-1/0007-134 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 115: `"verifier_librdkafka_version"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 135 | Common/Results/VerifierResultCodec.cs | 115:157-115:174 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T061 | batch-2/0007-135 | validated_pass | genuine missing test | Each branch reads a distinct required root string and attaches its field/code. Current producer/verifier version test has distinct but correctly typed values only. Exact mutation at line 115: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 141 | Common/Results/VerifierResultCodec.cs | 117:38-117:46 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T066 | batch-1/0008-141 | validated_pass | genuine missing test | The parser accepts only the three enumerated strings; current tests cover accepted values via writer output, not an unknown/wrong-kind status token. Exact mutation at line 117: `"status"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 142 | Common/Results/VerifierResultCodec.cs | 117:48-117:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T066 | batch-2/0008-142 | validated_pass | genuine missing test | The parser accepts only the three enumerated strings; current tests cover accepted values via writer output, not an unknown/wrong-kind status token. Exact mutation at line 117: `"invalid_enum_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 154 | Common/Results/VerifierResultCodec.cs | 131:71-131:83 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T041 | batch-1/0009-154 | validated_pass | genuine missing test | This branch parses captured integer milliseconds as Int64. Current duration tests use writer-produced integer values and therefore cannot exercise malformed JSON. Exact mutation at line 131: `"drain_time"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 155 | Common/Results/VerifierResultCodec.cs | 131:85-131:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T041 | batch-2/0009-155 | validated_pass | genuine missing test | This branch parses captured integer milliseconds as Int64. Current duration tests use writer-produced integer values and therefore cannot exercise malformed JSON. Exact mutation at line 131: `"mutually_exclusive_fields"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 159 | Common/Results/VerifierResultCodec.cs | 134:108-134:120 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T041 | batch-1/0010-159 | validated_pass | genuine missing test | This branch parses captured integer milliseconds as Int64. Current duration tests use writer-produced integer values and therefore cannot exercise malformed JSON. Exact mutation at line 134: `"drain_time"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 160 | Common/Results/VerifierResultCodec.cs | 134:122-134:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T041 | batch-2/0010-160 | validated_pass | genuine missing test | This branch parses captured integer milliseconds as Int64. Current duration tests use writer-produced integer values and therefore cannot exercise malformed JSON. Exact mutation at line 134: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 164 | Common/Results/VerifierResultCodec.cs | 140:111-140:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-1/0011-164 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 140: `"drain_elapsed_at_stop"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 165 | Common/Results/VerifierResultCodec.cs | 140:136-140:153 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-2/0011-165 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 140: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 166 | Common/Results/VerifierResultCodec.cs | 141:17-141:84 | Logical mutation | <code>&quot;!hasLowerBound &amp;&amp; lowerBoundElement.ValueKind != JsonValueKind.True&quot;</code> | Survived | candidate / T040 | batch-1/0012-166 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 141: `!hasLowerBound \|\| lowerBoundElement.ValueKind != JsonValueKind.True` -> `!hasLowerBound && lowerBoundElement.ValueKind != JsonValueKind.True`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 170 | Common/Results/VerifierResultCodec.cs | 142:42-142:77 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-2/0012-170 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 142: `"drain_elapsed_at_stop_lower_bound"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 171 | Common/Results/VerifierResultCodec.cs | 142:79-142:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-1/0013-171 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 142: `"expected_true"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 174 | Common/Results/VerifierResultCodec.cs | 147:38-147:73 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-2/0013-174 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 147: `"drain_elapsed_at_stop_lower_bound"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 175 | Common/Results/VerifierResultCodec.cs | 147:75-147:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T040 | batch-1/0014-175 | validated_pass | genuine missing test | These exact branches enforce the required sibling `true` marker and no orphan representation. The frozen safety-stop test only proves the valid integer+true wire pair. Exact mutation at line 147: `"orphaned_lower_bound_marker"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 183 | Common/Results/VerifierResultCodec.cs | 154:85-154:89 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T074 | batch-2/0014-183 | validated_pass | genuine missing test | Current tests cover valid count readback, writer absence under invalid/incomplete, and writer missing-count error. The JSON-reader missing/wrong-kind/forbidden-count branches are distinct. Exact mutation at line 154: `"ok"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 184 | Common/Results/VerifierResultCodec.cs | 154:91-154:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T074 | batch-1/0015-184 | validated_pass | genuine missing test | Current tests cover valid count readback, writer absence under invalid/incomplete, and writer missing-count error. The JSON-reader missing/wrong-kind/forbidden-count branches are distinct. Exact mutation at line 154: `"missing_or_invalid_counts"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 187 | Common/Results/VerifierResultCodec.cs | 158:38-158:42 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T074 | batch-2/0015-187 | validated_pass | genuine missing test | Current tests cover valid count readback, writer absence under invalid/incomplete, and writer missing-count error. The JSON-reader missing/wrong-kind/forbidden-count branches are distinct. Exact mutation at line 158: `"ok"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 188 | Common/Results/VerifierResultCodec.cs | 158:44-158:75 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T074 | batch-1/0016-188 | validated_pass | genuine missing test | Current tests cover valid count readback, writer absence under invalid/incomplete, and writer missing-count error. The JSON-reader missing/wrong-kind/forbidden-count branches are distinct. Exact mutation at line 158: `"counts_not_allowed_for_status"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 213 | Common/Results/VerifierResultCodec.cs | 189:41-189:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T088 | batch-2/0016-213 | validated_pass | genuine missing test | Frozen test asserts the exception type for this exact path but not message. This is an observable exception-text gap. Exact mutation at line 189: `"A complete_valid result requires all six supplied outcome counts."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 218 | Common/Results/VerifierResultCodec.cs | 191:13-191:121 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T047 | batch-1/0017-218 | compile_error | preview compilation discrepancy | The validator rejects both supplied duration fields together; current tests supply each form separately, so this writer guard has no direct assertion. Exact mutation at line 191: `throw new ArgumentException("drain_time and drain_elapsed_at_stop are mutually exclusive.", nameof(result));` -> `;`. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 219 | Common/Results/VerifierResultCodec.cs | 191:41-191:103 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T047 | batch-2/0017-219 | validated_pass | genuine missing test | Current valid duration tests use one field at a time, so neither the guard nor message is directly asserted. Exact mutation at line 191: `"drain_time and drain_elapsed_at_stop are mutually exclusive."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 225 | Common/Results/VerifierResultCodec.cs | 193:41-193:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T026 | batch-1/0018-225 | validated_pass | genuine missing test | The current test targets Read(false), not this writer validation message. It is a writer-only guard, separate from the nested reader marker checks. Exact mutation at line 193: `"A broker_count projection requires lower_bound=true."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 227 | Common/Results/VerifierResultCodec.cs | 198:13-198:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T048 | batch-2/0018-227 | validated_pass | genuine missing test | Nullable annotations do not enforce a runtime invariant. A caller can pass a null element using null-forgiving syntax or another runtime producer; the guard changes the observable exception type for malformed in-memory input. This may be outside accepted-wire semantics, but it is not unreachable. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 228 | Common/Results/VerifierResultCodec.cs | 199:13-199:65 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T083 | batch-1/0019-228 | validated_pass | genuine missing test | These writer-specific validation calls/counts are not covered by reader limit cases. Existing writer byte test exceeds a string limit only through one snippet input and does not isolate each checked member. Exact mutation at line 199: `CountDiagnostic(diagnostic.Check, perCheck, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 229 | Common/Results/VerifierResultCodec.cs | 200:13-200:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T083 | batch-2/0019-229 | validated_pass | genuine missing test | These writer-specific validation calls/counts are not covered by reader limit cases. Existing writer byte test exceeds a string limit only through one snippet input and does not isolate each checked member. Exact mutation at line 200: `CheckDiagnosticBytes(diagnostic.Code, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 230 | Common/Results/VerifierResultCodec.cs | 201:13-201:63 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T083 | batch-1/0020-230 | validated_pass | genuine missing test | These writer-specific validation calls/counts are not covered by reader limit cases. Existing writer byte test exceeds a string limit only through one snippet input and does not isolate each checked member. Exact mutation at line 201: `CheckDiagnosticBytes(diagnostic.Artifact, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 231 | Common/Results/VerifierResultCodec.cs | 202:13-202:60 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T083 | batch-2/0020-231 | validated_pass | genuine missing test | These writer-specific validation calls/counts are not covered by reader limit cases. Existing writer byte test exceeds a string limit only through one snippet input and does not isolate each checked member. Exact mutation at line 202: `CheckDiagnosticBytes(diagnostic.Field, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 232 | Common/Results/VerifierResultCodec.cs | 203:13-203:62 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T083 | batch-1/0021-232 | validated_pass | genuine missing test | These writer-specific validation calls/counts are not covered by reader limit cases. Existing writer byte test exceeds a string limit only through one snippet input and does not isolate each checked member. Exact mutation at line 203: `CheckDiagnosticBytes(diagnostic.Snippet, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 235 | Common/Results/VerifierResultCodec.cs | 209:13-209:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-2/0021-235 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 209: `ArgumentNullException.ThrowIfNull(assessment);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 236 | Common/Results/VerifierResultCodec.cs | 210:13-210:71 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-1/0022-236 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 210: `CountDiagnostic(assessment.CheckNumber, perCheck, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 237 | Common/Results/VerifierResultCodec.cs | 211:13-211:63 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-2/0022-237 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 211: `CheckDiagnosticBytes(assessment.Artifact, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 238 | Common/Results/VerifierResultCodec.cs | 212:13-212:60 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-1/0023-238 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 212: `CheckDiagnosticBytes(assessment.Field, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 239 | Common/Results/VerifierResultCodec.cs | 213:13-213:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-2/0023-239 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 213: `CheckDiagnosticBytes(assessment.Code, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 240 | Common/Results/VerifierResultCodec.cs | 214:13-214:62 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T077 | batch-1/0024-240 | validated_pass | genuine missing test | Writer shares per-check accounting across diagnostic and fidelity assessment; current per-check tests are reader calls. Existing assessment byte test is reader-only. Exact mutation at line 214: `CheckDiagnosticBytes(assessment.Snippet, limits);` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 243 | Common/Results/VerifierResultCodec.cs | 219:17-219:59 | Equality mutation | <code>&quot;item.Value &gt;= limits.MaxDiagnosticsPerCheck&quot;</code> | Survived | candidate / T079 | batch-2/0024-243 | validated_pass | genuine missing test | This final guard compares accumulated per-check counts. CountDiagnostic also throws immediately when a count exceeds max (line 229), which makes line 220's repeat greater-than throw unreachable for ordinary well-typed values; the line 219 equality mutation is observable at the exact cap. Exact mutation at line 219: `item.Value > limits.MaxDiagnosticsPerCheck` -> `item.Value >= limits.MaxDiagnosticsPerCheck`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 245 | Common/Results/VerifierResultCodec.cs | 220:17-220:130 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T120 | batch-1/0025-245 | compile_error | preview compilation discrepancy | The local dictionary is mutated only through CountDiagnostic. It stores `next` and then throws whenever `next > max`; any return leaves the stored value <= max. Thus the final loop cannot observe an over-limit value, including under unchecked integer wraparound, and line 220's repeated throw is unreachable. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 246 | Common/Results/VerifierResultCodec.cs | 220:55-220:128 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T120 | batch-2/0025-246 | validated_pass | equivalent/unreachable with proof | The local dictionary is mutated only through CountDiagnostic. It stores `next` and then throws whenever `next > max`; any return leaves the stored value <= max. Thus the final loop cannot observe an over-limit value, including under unchecked integer wraparound, and line 220's repeated throw is unreachable. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 250 | Common/Results/VerifierResultCodec.cs | 226:65-226:76 | Arithmetic mutation | <code>&quot;current - 1&quot;</code> | Survived | candidate / T080 | batch-1/0026-250 | validated_pass | genuine missing test | CountDiagnostic's arithmetic and greater-than comparison own writer diagnostics and assessment counts. Existing different-check reader positive case is separate and does not exercise this writer-local counter. Exact mutation at line 226: `current + 1` -> `current - 1`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 252 | Common/Results/VerifierResultCodec.cs | 228:13-228:49 | Equality mutation | <code>&quot;next &gt;= limits.MaxDiagnosticsPerCheck&quot;</code> | Survived | candidate / T078 | batch-2/0026-252 | validated_pass | genuine missing test | Changing `>` to `>=` rejects equality at CountDiagnostic line 228; current writer tests do not establish that boundary. Exact mutation at line 228: `next > limits.MaxDiagnosticsPerCheck` -> `next >= limits.MaxDiagnosticsPerCheck`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 254 | Common/Results/VerifierResultCodec.cs | 229:13-229:126 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T081 | batch-1/0027-254 | compile_error | preview compilation discrepancy | The line 229 early throw is the only immediate counter bound. Although a later duplicate guard exists, disabling only this immediate throw still leaves the final guard to reject excessive counts; compare exception type/message and no bytes returned before calling it behaviorally equivalent. Exact mutation at line 229: `throw new InputLimitExceededException("The verifier result exceeds the configured diagnostics per check limit.");` -> `;`. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 255 | Common/Results/VerifierResultCodec.cs | 229:51-229:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T082 | batch-2/0027-255 | validated_pass | genuine missing test | The existing per-check writer path is untested; if exercised, this exact throw message is observable. Do not use the later duplicate guard as equivalence because this throw is reached first. Exact mutation at line 229: `"The verifier result exceeds the configured diagnostics per check limit."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 261 | Common/Results/VerifierResultCodec.cs | 234:34-234:95 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(value) &gt;= limits.MaxDiagnosticBytes&quot;</code> | Survived | candidate / T084 | batch-1/0028-261 | validated_pass | genuine missing test | The writer checks each non-null text independently using UTF8 byte count; current write test exceeds only one snippet and does not assert equality for each field. Exact mutation at line 234: `Encoding.UTF8.GetByteCount(value) > limits.MaxDiagnosticBytes` -> `Encoding.UTF8.GetByteCount(value) >= limits.MaxDiagnosticBytes`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 263 | Common/Results/VerifierResultCodec.cs | 235:51-235:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T084 | batch-2/0028-263 | validated_pass | genuine missing test | The writer checks each non-null text independently using UTF8 byte count; current write test exceeds only one snippet and does not assert equality for each field. Exact mutation at line 235: `"A verifier result diagnostic exceeds the configured byte limit."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 359 | Common/Results/VerifierResultCodec.cs | 322:47-322:106 | Conditional (false) mutation | <code>&quot;(false?\&quot;pass\&quot; :\&quot;fail\&quot;)&quot;</code> | Survived | candidate / T019 | batch-1/0029-359 | validated_pass | genuine missing test | The current R3 roundtrip covers NotEvaluated and Evaluated/Fail/Invalid, not the alternate enum values. This mutant's source token/condition determines which one of the supplied alternate-state cases distinguishes it. Exact mutation at line 322: `disposition == AssessmentDisposition.Pass ? "pass" : "fail"` -> `(false?"pass" :"fail")`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 361 | Common/Results/VerifierResultCodec.cs | 322:91-322:97 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T018 | batch-2/0029-361 | validated_pass | genuine missing test | The mutated token occurs only on the Pass arm; current roundtrip asserts Fail and NotEvaluated, so this alternate writer token is missing. Exact fresh mutation at line 322: `"pass"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 366 | Common/Results/VerifierResultCodec.cs | 324:50-324:127 | Conditional (true) mutation | <code>&quot;(true?\&quot;invalid\&quot; :\&quot;incomplete\&quot;)&quot;</code> | Survived | candidate / T019 | batch-1/0030-366 | validated_pass | genuine missing test | The current R3 roundtrip covers NotEvaluated and Evaluated/Fail/Invalid, not the alternate enum values. This mutant's source token/condition determines which one of the supplied alternate-state cases distinguishes it. Exact mutation at line 324: `classification == AssessmentClassification.Invalid ? "invalid" : "incomplete"` -> `(true?"invalid" :"incomplete")`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 370 | Common/Results/VerifierResultCodec.cs | 324:115-324:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T016 | batch-2/0030-370 | validated_pass | genuine missing test | The mutated token occurs only on the Incomplete arm; current assessment test uses Invalid classification. Exact fresh mutation at line 324: `"incomplete"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 485 | Common/Results/VerifierResultCodec.cs | 418:69-418:74 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-1/0031-485 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 418: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 489 | Common/Results/VerifierResultCodec.cs | 419:101-419:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0031-489 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 419: `"identity.scenario_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 490 | Common/Results/VerifierResultCodec.cs | 419:125-419:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0032-490 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 419: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 491 | Common/Results/VerifierResultCodec.cs | 419:152-419:157 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-2/0032-491 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 419: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 495 | Common/Results/VerifierResultCodec.cs | 420:111-420:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0033-495 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 420: `"identity.configuration_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 496 | Common/Results/VerifierResultCodec.cs | 420:140-420:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0033-496 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 420: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 497 | Common/Results/VerifierResultCodec.cs | 420:167-420:172 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-1/0034-497 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 420: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 501 | Common/Results/VerifierResultCodec.cs | 421:91-421:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0034-501 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 421: `"identity.run_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 502 | Common/Results/VerifierResultCodec.cs | 421:110-421:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0035-502 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 421: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 503 | Common/Results/VerifierResultCodec.cs | 421:137-421:142 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-2/0035-503 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 421: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 507 | Common/Results/VerifierResultCodec.cs | 422:101-422:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0036-507 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 422: `"identity.instance_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 508 | Common/Results/VerifierResultCodec.cs | 422:125-422:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0036-508 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 422: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 509 | Common/Results/VerifierResultCodec.cs | 422:152-422:157 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-1/0037-509 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 422: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 513 | Common/Results/VerifierResultCodec.cs | 423:118-423:149 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0037-513 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 423: `"identity.expected_instance_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 514 | Common/Results/VerifierResultCodec.cs | 423:151-423:168 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0038-514 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 423: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 515 | Common/Results/VerifierResultCodec.cs | 423:178-423:183 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-2/0038-515 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 423: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 519 | Common/Results/VerifierResultCodec.cs | 424:97-424:117 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-1/0039-519 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 424: `"identity.client_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 520 | Common/Results/VerifierResultCodec.cs | 424:119-424:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T060 | batch-2/0039-520 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 424: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 521 | Common/Results/VerifierResultCodec.cs | 424:146-424:151 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T060 | batch-1/0040-521 | validated_pass | genuine missing test | These lines validate the six shared RunIdentity strings and construct field-specific problems; current result cases only use a valid shared identity, and StartLineNote tests do not invoke this result reader path. Exact mutation at line 424: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 526 | Common/Results/VerifierResultCodec.cs | 435:92-435:109 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T068 | batch-2/0040-526 | validated_pass | genuine missing test | TryStringMap only sees valid string entries in current producer version fixture; separate parsing logic is not tested by root scalar checks. Exact mutation at line 435: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 527 | Common/Results/VerifierResultCodec.cs | 435:119-435:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T068 | batch-1/0041-527 | validated_pass | genuine missing test | TryStringMap only sees valid string entries in current producer version fixture; separate parsing logic is not tested by root scalar checks. Exact mutation at line 435: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 532 | Common/Results/VerifierResultCodec.cs | 441:52-441:55 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T068 | batch-2/0041-532 | validated_pass | genuine missing test | TryStringMap only sees valid string entries in current producer version fixture; separate parsing logic is not tested by root scalar checks. Exact mutation at line 441: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 533 | Common/Results/VerifierResultCodec.cs | 441:73-441:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T068 | batch-1/0042-533 | validated_pass | genuine missing test | TryStringMap only sees valid string entries in current producer version fixture; separate parsing logic is not tested by root scalar checks. Exact mutation at line 441: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 534 | Common/Results/VerifierResultCodec.cs | 442:24-442:29 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T068 | batch-2/0042-534 | validated_pass | genuine missing test | TryStringMap only sees valid string entries in current producer version fixture; separate parsing logic is not tested by root scalar checks. Exact mutation at line 442: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 540 | Common/Results/VerifierResultCodec.cs | 455:91-455:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T044 | batch-1/0043-540 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 455: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 541 | Common/Results/VerifierResultCodec.cs | 455:118-455:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T044 | batch-2/0043-541 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 455: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 545 | Common/Results/VerifierResultCodec.cs | 460:93-460:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T044 | batch-1/0044-545 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 460: `$"{name}[{index}]"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 546 | Common/Results/VerifierResultCodec.cs | 460:113-460:130 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T044 | batch-2/0044-546 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 460: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 547 | Common/Results/VerifierResultCodec.cs | 460:140-460:145 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T044 | batch-1/0045-547 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 460: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 549 | Common/Results/VerifierResultCodec.cs | 462:13-462:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T044 | batch-2/0045-549 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 462: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 550 | Common/Results/VerifierResultCodec.cs | 462:13-462:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T044 | batch-1/0046-550 | validated_pass | genuine missing test | TryIntArray element conversion and index update are distinct; current valid failed-check array does not test a later malformed item. Exact mutation at line 462: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 557 | Common/Results/VerifierResultCodec.cs | 473:89-473:106 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T073 | batch-2/0046-557 | validated_pass | genuine missing test | The mutation changes the code literal used only for a present non-array boundary; current boundary tests use absent, empty array, or valid populated arrays. Exact fresh mutation at line 473: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 558 | Common/Results/VerifierResultCodec.cs | 473:116-473:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T072 | batch-1/0047-558 | validated_pass | genuine missing test | The mutated return is inside the `ValueKind != Array` branch; current populated/empty tests satisfy the array predicate. Exact fresh mutation at line 473: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 560 | Common/Results/VerifierResultCodec.cs | 478:27-478:45 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T076 | batch-2/0047-560 | validated_pass | genuine missing test | This line formats each watermark item path from the loop index. The current positive two-item test does not observe an error path at index 1. Exact mutation at line 478: `$"{name}[{index}]"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 564 | Common/Results/VerifierResultCodec.cs | 479:108-479:113 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T046 | batch-1/0048-564 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 479: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 568 | Common/Results/VerifierResultCodec.cs | 480:103-480:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T046 | batch-2/0048-568 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 480: `".partition"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 569 | Common/Results/VerifierResultCodec.cs | 480:117-480:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T046 | batch-1/0049-569 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 480: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 570 | Common/Results/VerifierResultCodec.cs | 480:144-480:149 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T046 | batch-2/0049-570 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 480: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 574 | Common/Results/VerifierResultCodec.cs | 481:98-481:107 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T046 | batch-1/0050-574 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 481: `".offset"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 575 | Common/Results/VerifierResultCodec.cs | 481:109-481:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T046 | batch-2/0050-575 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 481: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 576 | Common/Results/VerifierResultCodec.cs | 481:136-481:141 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T046 | batch-1/0051-576 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 481: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 578 | Common/Results/VerifierResultCodec.cs | 483:13-483:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T046 | batch-2/0051-578 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 483: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 579 | Common/Results/VerifierResultCodec.cs | 483:13-483:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T046 | batch-1/0052-579 | validated_pass | genuine missing test | Current boundary test proves valid arrays and members, but wrong-kind/object-shape failures need malformed JSON. The exact mutation controls the exact-property, type guard, list append/index, or success return on this line. Exact mutation at line 483: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 600 | Common/Results/VerifierResultCodec.cs | 494:118-494:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T032 | batch-2/0052-600 | validated_pass | genuine missing test | All current count readers consume codec-generated valid counts; writer missing-count exception is a distinct path from this JSON reader. Exact mutation at line 494: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 607 | Common/Results/VerifierResultCodec.cs | 507:92-507:109 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T051 | batch-1/0053-607 | validated_pass | genuine missing test | TryOptionalObject must distinguish absent from present non-object; only valid/absent accepted projections are currently roundtripped. Exact mutation at line 507: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 608 | Common/Results/VerifierResultCodec.cs | 507:119-507:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T051 | batch-2/0053-608 | validated_pass | genuine missing test | TryOptionalObject must distinguish absent from present non-object; only valid/absent accepted projections are currently roundtripped. Exact mutation at line 507: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 619 | Common/Results/VerifierResultCodec.cs | 515:119-515:124 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T027 | batch-1/0054-619 | validated_pass | genuine missing test | This is the failed strict-property-validation return. Current broker projection supplies all required members; a malformed-object fixture is required to observe it. Exact fresh mutation at line 515: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 623 | Common/Results/VerifierResultCodec.cs | 516:97-516:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T031 | batch-2/0054-623 | validated_pass | genuine missing test | The role parse succeeds on the current valid "producer" value; only malformed kind reaches this path. Exact fresh mutation at line 516: `".role"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 624 | Common/Results/VerifierResultCodec.cs | 516:106-516:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T031 | batch-1/0055-624 | validated_pass | genuine missing test | This error-code literal is emitted only for a failed required role string conversion; positive role roundtrip does not execute it. Exact fresh mutation at line 516: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 625 | Common/Results/VerifierResultCodec.cs | 516:133-516:138 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T031 | batch-2/0055-625 | validated_pass | genuine missing test | The false return inside the role parse failure must remain false; the valid projection only exercises successful string conversion. Exact fresh mutation at line 516: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 629 | Common/Results/VerifierResultCodec.cs | 517:106-517:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T024 | batch-1/0056-629 | validated_pass | genuine missing test | Current broker projection uses a valid string client_id; this mutation is confined to malformed input. Exact fresh mutation at line 517: `".client_id"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 630 | Common/Results/VerifierResultCodec.cs | 517:120-517:137 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T024 | batch-2/0056-630 | validated_pass | genuine missing test | This code literal is reached only on failed client_id string conversion; positive broker coverage does not inspect it. Exact fresh mutation at line 517: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 631 | Common/Results/VerifierResultCodec.cs | 517:147-517:152 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T024 | batch-1/0057-631 | validated_pass | genuine missing test | The mutated false return belongs to the client_id wrong-kind branch, not the valid-value branch tested today. Exact fresh mutation at line 517: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 635 | Common/Results/VerifierResultCodec.cs | 518:105-518:119 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T029 | batch-2/0057-635 | validated_pass | genuine missing test | Current broker positive and negative marker inputs are JSON booleans; neither enters the wrong-kind branch. Exact fresh mutation at line 518: `".lower_bound"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 636 | Common/Results/VerifierResultCodec.cs | 518:121-518:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T029 | batch-1/0058-636 | validated_pass | genuine missing test | The exact error literal is only used when TryBool rejects present non-boolean JSON. Exact fresh mutation at line 518: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 637 | Common/Results/VerifierResultCodec.cs | 518:148-518:153 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T029 | batch-2/0058-637 | validated_pass | genuine missing test | The false return is part of the type-error path, while current broker tests give Boolean true/false. Exact fresh mutation at line 518: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 641 | Common/Results/VerifierResultCodec.cs | 519:79-519:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T025 | batch-1/0059-641 | validated_pass | genuine missing test | Current negative test asserts only the field, not the code literal changed by this mutation. Exact fresh mutation at line 519: `"expected_true"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 645 | Common/Results/VerifierResultCodec.cs | 520:107-520:112 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T030 | batch-2/0059-645 | validated_pass | genuine missing test | The optional Int64 type-error branch is distinct from the currently tested numeric and omitted measurement cases. Exact fresh mutation at line 520: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 648 | Common/Results/VerifierResultCodec.cs | 521:114-521:119 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T028 | batch-1/0060-648 | validated_pass | genuine missing test | The optional Int64 parser only reaches this failure for present malformed values; current test uses numeric or omitted values. Exact fresh mutation at line 521: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 656 | Common/Results/VerifierResultCodec.cs | 529:137-529:142 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T037 | batch-2/0060-656 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 529: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 659 | Common/Results/VerifierResultCodec.cs | 530:75-530:88 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T037 | batch-1/0061-659 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 530: `".assessment"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 660 | Common/Results/VerifierResultCodec.cs | 530:154-530:159 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T037 | batch-2/0061-660 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 530: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 664 | Common/Results/VerifierResultCodec.cs | 532:90-532:104 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T037 | batch-1/0062-664 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 532: `".differences"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 665 | Common/Results/VerifierResultCodec.cs | 532:106-532:123 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T037 | batch-2/0062-665 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 532: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 666 | Common/Results/VerifierResultCodec.cs | 532:133-532:138 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T037 | batch-1/0063-666 | validated_pass | genuine missing test | The root parser requires exactly assessment/differences and array kind; current fidelity test uses the valid two-member object only. Exact mutation at line 532: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 668 | Common/Results/VerifierResultCodec.cs | 537:31-537:61 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T070 | batch-2/0063-668 | validated_pass | genuine missing test | This interpolated path includes the row index. Current two-row fixture has both rows valid, so its loop does not expose the error path. Exact fresh mutation at line 537: `$"{path}.differences[{index}]"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 674 | Common/Results/VerifierResultCodec.cs | 538:128-538:133 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T035 | batch-1/0064-674 | validated_pass | genuine missing test | This false return is inside Validate failure for a malformed difference object; current fidelity fixture uses valid setting keys only. Exact fresh mutation at line 538: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 678 | Common/Results/VerifierResultCodec.cs | 539:97-539:135 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T071 | batch-2/0064-678 | validated_pass | genuine missing test | This interpolated path and conversion fail only for malformed setting; both current difference rows contain strings. Exact fresh mutation at line 539: `$"{path}.differences[{index}].setting"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 679 | Common/Results/VerifierResultCodec.cs | 539:137-539:154 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T036 | batch-1/0065-679 | validated_pass | genuine missing test | Current fidelity test supplies valid string settings; this error code is specific to the wrong-kind branch. Exact fresh mutation at line 539: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 680 | Common/Results/VerifierResultCodec.cs | 539:164-539:169 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T036 | batch-2/0065-680 | validated_pass | genuine missing test | The false return is executed only after required setting string conversion fails; positive row assertions do not exercise it. Exact fresh mutation at line 539: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 683 | Common/Results/VerifierResultCodec.cs | 540:117-540:122 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T034 | batch-1/0066-683 | validated_pass | genuine missing test | The optional-string parser accepts omission and strings; current fidelity fixtures cover those valid states, not present wrong-kind. Exact fresh mutation at line 540: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 686 | Common/Results/VerifierResultCodec.cs | 541:113-541:118 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T033 | batch-2/0066-686 | validated_pass | genuine missing test | This independent optional-string failure is not exercised by current supplied string or omitted actual values. Exact fresh mutation at line 541: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 688 | Common/Results/VerifierResultCodec.cs | 543:13-543:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T070 | batch-1/0067-688 | validated_pass | genuine missing test | The loop increment advances diagnostic paths only; valid two-row data cannot distinguish a removed increment, but malformed later-item path does. Exact fresh mutation at line 543: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 689 | Common/Results/VerifierResultCodec.cs | 543:13-543:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T070 | batch-2/0067-689 | validated_pass | genuine missing test | The decrement changes the index used for the second malformed row; current valid rows have no observable path. Exact fresh mutation at line 543: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 695 | Common/Results/VerifierResultCodec.cs | 552:122-552:127 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T020 | batch-1/0068-695 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 552: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 699 | Common/Results/VerifierResultCodec.cs | 553:101-553:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-2/0068-699 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 553: `".check_number"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 700 | Common/Results/VerifierResultCodec.cs | 553:118-553:147 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-1/0069-700 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 553: `"invalid_or_wrong_json_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 701 | Common/Results/VerifierResultCodec.cs | 553:157-553:162 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T020 | batch-2/0069-701 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 553: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 705 | Common/Results/VerifierResultCodec.cs | 554:113-554:126 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-1/0070-705 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 554: `".evaluation"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 706 | Common/Results/VerifierResultCodec.cs | 554:128-554:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-2/0070-706 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 554: `"invalid_or_wrong_json_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 707 | Common/Results/VerifierResultCodec.cs | 554:167-554:172 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T020 | batch-1/0071-707 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 554: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 713 | Common/Results/VerifierResultCodec.cs | 558:51-558:64 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-2/0071-713 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 558: `".evaluation"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 714 | Common/Results/VerifierResultCodec.cs | 558:66-558:95 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T020 | batch-1/0072-714 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 558: `"invalid_or_wrong_json_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 715 | Common/Results/VerifierResultCodec.cs | 558:105-558:110 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T020 | batch-2/0072-715 | validated_pass | genuine missing test | These lines require check_number and one of two evaluation tokens. Current assessments roundtrip NotEvaluated and Evaluated, but do not supply malformed/missing reader JSON; the parsed enum states are distinct. Exact mutation at line 558: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 718 | Common/Results/VerifierResultCodec.cs | 559:54-559:60 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-1/0073-718 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. This exact replacement may affect only the alternate enum arm: compare original fragment and replacement before deciding whether the current Fail/Invalid test distinguishes it; the focused missing case is Pass/Incomplete if not. Exact mutation at line 559: `"pass"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 721 | Common/Results/VerifierResultCodec.cs | 559:148-559:162 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-2/0073-721 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 559: `".disposition"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 722 | Common/Results/VerifierResultCodec.cs | 559:164-559:193 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-1/0074-722 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 559: `"invalid_or_wrong_json_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 723 | Common/Results/VerifierResultCodec.cs | 559:203-559:208 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T017 | batch-2/0074-723 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 559: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 727 | Common/Results/VerifierResultCodec.cs | 560:68-560:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-1/0075-727 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. This exact replacement may affect only the alternate enum arm: compare original fragment and replacement before deciding whether the current Fail/Invalid test distinguishes it; the focused missing case is Pass/Incomplete if not. Exact mutation at line 560: `"incomplete"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 729 | Common/Results/VerifierResultCodec.cs | 560:166-560:183 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-2/0075-729 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 560: `".classification"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 730 | Common/Results/VerifierResultCodec.cs | 560:185-560:214 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T017 | batch-1/0076-730 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 560: `"invalid_or_wrong_json_value"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 731 | Common/Results/VerifierResultCodec.cs | 560:224-560:229 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T017 | batch-2/0076-731 | validated_pass | genuine missing test | Current nested assessment test covers Fail and Invalid; it does not exercise Pass/Incomplete parse branches or unknown enum tokens. Exact mutation at line 560: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 734 | Common/Results/VerifierResultCodec.cs | 561:116-561:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T012 | batch-1/0077-734 | validated_pass | genuine missing test | The assessment optional-string malformed branch is not hit by current valid assessment fields. Exact fresh mutation at line 561: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 737 | Common/Results/VerifierResultCodec.cs | 562:99-562:104 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T021 | batch-2/0077-737 | validated_pass | genuine missing test | The optional Int64 row parser accepts valid integer/absence; current assessments use valid row 2 or null. Exact fresh mutation at line 562: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 740 | Common/Results/VerifierResultCodec.cs | 563:106-563:111 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T015 | batch-1/0078-740 | validated_pass | genuine missing test | Current assessment field is a valid string; malformed member does not reach the positive roundtrip assertions. Exact fresh mutation at line 563: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 743 | Common/Results/VerifierResultCodec.cs | 564:104-564:109 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T013 | batch-2/0078-743 | validated_pass | genuine missing test | This optional string conversion failure is separate from current valid code values. Exact fresh mutation at line 564: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 746 | Common/Results/VerifierResultCodec.cs | 565:110-565:115 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T022 | batch-1/0079-746 | validated_pass | genuine missing test | Current assessment snippet values are strings or null; the wrong-kind branch remains untested. Exact fresh mutation at line 565: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 758 | Common/Results/VerifierResultCodec.cs | 573:191-573:196 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T053 | batch-2/0079-758 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 573: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 762 | Common/Results/VerifierResultCodec.cs | 574:96-574:105 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T053 | batch-1/0080-762 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 574: `".usable"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 763 | Common/Results/VerifierResultCodec.cs | 574:107-574:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T053 | batch-2/0080-763 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 574: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 764 | Common/Results/VerifierResultCodec.cs | 574:134-574:139 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T053 | batch-1/0081-764 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 574: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 768 | Common/Results/VerifierResultCodec.cs | 575:104-575:117 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T053 | batch-2/0081-768 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 575: `".qualifying"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 769 | Common/Results/VerifierResultCodec.cs | 575:119-575:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T053 | batch-1/0082-769 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 575: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 770 | Common/Results/VerifierResultCodec.cs | 575:146-575:151 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T053 | batch-2/0082-770 | validated_pass | genuine missing test | Both flags are required booleans and current probe test only supplies correctly typed booleans. Exact mutation at line 575: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 773 | Common/Results/VerifierResultCodec.cs | 576:108-576:113 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T052 | batch-1/0083-773 | validated_pass | genuine missing test | Current probe uses broker string and omitted optionals; malformed presence is distinct. Exact fresh mutation at line 576: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 776 | Common/Results/VerifierResultCodec.cs | 577:118-577:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T054 | batch-2/0083-776 | validated_pass | genuine missing test | The optional Int64 field is currently supplied as a number or omitted, not wrong-kind. Exact fresh mutation at line 577: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 779 | Common/Results/VerifierResultCodec.cs | 578:110-578:115 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T055 | batch-1/0084-779 | validated_pass | genuine missing test | Current probe count is valid Int64 1 or omitted. Exact fresh mutation at line 578: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 782 | Common/Results/VerifierResultCodec.cs | 579:114-579:119 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T056 | batch-2/0084-782 | validated_pass | genuine missing test | Current probe maximum is numeric 0 or omitted. Exact fresh mutation at line 579: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 790 | Common/Results/VerifierResultCodec.cs | 587:149-587:154 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T002 | batch-1/0085-790 | validated_pass | genuine missing test | Current A01 test uses valid Int32 delivery codes only. Exact fresh mutation at line 587: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 793 | Common/Results/VerifierResultCodec.cs | 588:121-588:126 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T011 | batch-2/0085-793 | validated_pass | genuine missing test | Current A01 projection has a valid windows array; this check is a malformed container branch. Exact fresh mutation at line 588: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 797 | Common/Results/VerifierResultCodec.cs | 590:90-590:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T011 | batch-1/0086-797 | validated_pass | genuine missing test | This field label only appears for a present non-array windows value; the current array fixture cannot assert it. Exact fresh mutation at line 590: `".windows"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 798 | Common/Results/VerifierResultCodec.cs | 590:102-590:119 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T011 | batch-2/0086-798 | validated_pass | genuine missing test | This error code belongs to wrong container kind; current accepted A01 shape provides an array. Exact fresh mutation at line 590: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 799 | Common/Results/VerifierResultCodec.cs | 590:129-590:134 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T011 | batch-1/0087-799 | validated_pass | genuine missing test | The return false is what stops parsing after detecting a non-array windows member; positive array tests do not hit this branch. Exact fresh mutation at line 590: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 801 | Common/Results/VerifierResultCodec.cs | 595:31-595:57 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T009 | batch-2/0087-801 | validated_pass | genuine missing test | This line constructs the indexed nested path for each window. The existing two-window A01 fixture uses valid entries only. Exact mutation at line 595: `$"{path}.windows[{index}]"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 813 | Common/Results/VerifierResultCodec.cs | 598:93-598:98 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T010 | batch-1/0088-813 | validated_pass | genuine missing test | This false return belongs to the strict required-property validation; current windows supply all required keys. Exact fresh mutation at line 598: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 817 | Common/Results/VerifierResultCodec.cs | 599:95-599:115 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T001 | batch-2/0088-817 | validated_pass | genuine missing test | Current broker field is a valid string; the mutation changes only the malformed conversion path. Exact fresh mutation at line 599: `$"{itemPath}.broker"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 818 | Common/Results/VerifierResultCodec.cs | 599:117-599:134 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T001 | batch-1/0089-818 | validated_pass | genuine missing test | Current A01 roundtrip proves valid text but not the error literal from wrong-kind input. Exact fresh mutation at line 599: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 819 | Common/Results/VerifierResultCodec.cs | 599:144-599:149 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T001 | batch-2/0089-819 | validated_pass | genuine missing test | The false return prevents acceptance after required broker string parsing fails; positive rows do not execute it. Exact fresh mutation at line 599: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 823 | Common/Results/VerifierResultCodec.cs | 600:110-600:140 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T005 | batch-1/0090-823 | validated_pass | genuine missing test | Current report_timestamp is valid Int64 data; this is its malformed scalar branch. Exact fresh mutation at line 600: `$"{itemPath}.report_timestamp"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 824 | Common/Results/VerifierResultCodec.cs | 600:142-600:159 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T005 | batch-2/0090-824 | validated_pass | genuine missing test | The current positive A01 data never exercises this error literal. Exact fresh mutation at line 600: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 825 | Common/Results/VerifierResultCodec.cs | 600:169-600:174 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T005 | batch-1/0091-825 | validated_pass | genuine missing test | This return false belongs to TryInt64 failure; current timestamp values are valid integers. Exact fresh mutation at line 600: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 829 | Common/Results/VerifierResultCodec.cs | 601:110-601:140 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T004 | batch-2/0091-829 | validated_pass | genuine missing test | The current required native timestamp is a valid Int64. Exact fresh mutation at line 601: `$"{itemPath}.native_timestamp"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 830 | Common/Results/VerifierResultCodec.cs | 601:142-601:159 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T004 | batch-1/0092-830 | validated_pass | genuine missing test | Current A01 fixture does not enter this type-error branch. Exact fresh mutation at line 601: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 831 | Common/Results/VerifierResultCodec.cs | 601:169-601:174 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T004 | batch-2/0092-831 | validated_pass | genuine missing test | This false return is the scalar failure gate; current data is well-typed. Exact fresh mutation at line 601: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 834 | Common/Results/VerifierResultCodec.cs | 602:132-602:137 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T006 | batch-1/0093-834 | validated_pass | genuine missing test | The optional Int64 parser must reject malformed presence while accepting omission; current rows use numeric or omitted values. Exact fresh mutation at line 602: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 837 | Common/Results/VerifierResultCodec.cs | 603:130-603:135 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T003 | batch-2/0093-837 | validated_pass | genuine missing test | Current A01 rows use numeric or omitted values, never a string here. Exact fresh mutation at line 603: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 840 | Common/Results/VerifierResultCodec.cs | 604:125-604:130 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T008 | batch-1/0094-840 | validated_pass | genuine missing test | The optional throttle measurement is currently numeric or omitted. Exact fresh mutation at line 604: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 843 | Common/Results/VerifierResultCodec.cs | 605:118-605:123 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T007 | batch-2/0094-843 | validated_pass | genuine missing test | Current p99 input is numeric and the second window omits it; malformed presence has no existing assertion. Exact fresh mutation at line 605: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 845 | Common/Results/VerifierResultCodec.cs | 607:13-607:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T069 | batch-1/0095-845 | validated_pass | genuine missing test | The loop index is used only by errors on later items; the current two valid windows never observe the index in a failure path. Exact fresh mutation at line 607: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 846 | Common/Results/VerifierResultCodec.cs | 607:13-607:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T069 | batch-2/0095-846 | validated_pass | genuine missing test | The decrement changes only the subsequent item path; valid windows do not expose the mutated counter. Exact fresh mutation at line 607: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 852 | Common/Results/VerifierResultCodec.cs | 618:83-618:103 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T045 | batch-1/0096-852 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 618: `"judged_file_hashes"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 853 | Common/Results/VerifierResultCodec.cs | 618:105-618:122 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T045 | batch-2/0096-853 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 618: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 854 | Common/Results/VerifierResultCodec.cs | 618:132-618:137 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T045 | batch-1/0097-854 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 618: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 856 | Common/Results/VerifierResultCodec.cs | 623:27-623:57 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T045 | batch-2/0097-856 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 623: `$"judged_file_hashes[{index}]"` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 862 | Common/Results/VerifierResultCodec.cs | 624:116-624:121 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T045 | batch-1/0098-862 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 624: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 870 | Common/Results/VerifierResultCodec.cs | 626:49-626:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T045 | batch-2/0098-870 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 626: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 871 | Common/Results/VerifierResultCodec.cs | 626:76-626:81 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T045 | batch-1/0099-871 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 626: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 873 | Common/Results/VerifierResultCodec.cs | 628:13-628:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T045 | batch-2/0099-873 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 628: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 874 | Common/Results/VerifierResultCodec.cs | 628:13-628:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T045 | batch-1/0100-874 | validated_pass | genuine missing test | Current hash test reads a single valid hash; this parser's container, exact-key validation, member kinds and loop index error paths are not covered by that positive projection. Exact mutation at line 628: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 880 | Common/Results/VerifierResultCodec.cs | 639:83-639:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T038 | batch-2/0100-880 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 639: `"diagnostics"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 881 | Common/Results/VerifierResultCodec.cs | 639:98-639:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T038 | batch-1/0101-881 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 639: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 882 | Common/Results/VerifierResultCodec.cs | 639:125-639:130 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T038 | batch-2/0101-882 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 639: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 900 | Common/Results/VerifierResultCodec.cs | 648:49-648:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T038 | batch-1/0102-900 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 648: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 901 | Common/Results/VerifierResultCodec.cs | 648:76-648:81 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T038 | batch-2/0102-901 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 648: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 907 | Common/Results/VerifierResultCodec.cs | 650:100-650:105 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T038 | batch-1/0103-907 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 650: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 910 | Common/Results/VerifierResultCodec.cs | 651:107-651:112 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T038 | batch-2/0103-910 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 651: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 913 | Common/Results/VerifierResultCodec.cs | 652:111-652:116 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T038 | batch-1/0104-913 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 652: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 923 | Common/Results/VerifierResultCodec.cs | 653:104-653:165 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(artifactName) &gt;= maxDiagnosticBytes&quot;</code> | Survived | candidate / T039 | batch-2/0104-923 | validated_pass | genuine missing test | These `>` checks are inclusive at the configured maximum; current reader tests exercise six bytes over five but do not exercise equality for each independently checked value. Exact mutation at line 653: `Encoding.UTF8.GetByteCount(artifactName) > maxDiagnosticBytes` -> `Encoding.UTF8.GetByteCount(artifactName) >= maxDiagnosticBytes`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 927 | Common/Results/VerifierResultCodec.cs | 654:39-654:93 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(field) &gt;= maxDiagnosticBytes&quot;</code> | Survived | candidate / T039 | batch-1/0105-927 | validated_pass | genuine missing test | These `>` checks are inclusive at the configured maximum; current reader tests exercise six bytes over five but do not exercise equality for each independently checked value. Exact mutation at line 654: `Encoding.UTF8.GetByteCount(field) > maxDiagnosticBytes` -> `Encoding.UTF8.GetByteCount(field) >= maxDiagnosticBytes`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 931 | Common/Results/VerifierResultCodec.cs | 655:41-655:97 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(snippet) &gt;= maxDiagnosticBytes&quot;</code> | Survived | candidate / T039 | batch-2/0105-931 | validated_pass | genuine missing test | These `>` checks are inclusive at the configured maximum; current reader tests exercise six bytes over five but do not exercise equality for each independently checked value. Exact mutation at line 655: `Encoding.UTF8.GetByteCount(snippet) > maxDiagnosticBytes` -> `Encoding.UTF8.GetByteCount(snippet) >= maxDiagnosticBytes`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 933 | Common/Results/VerifierResultCodec.cs | 656:55-656:133 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | candidate / T057 | batch-1/0106-933 | validated_pass | genuine missing test | Frozen tests already enter the affected branch and assert exception type, but not the changed message literal; this candidate adds an exact observable message assertion rather than a new semantic guard. Exact mutation at line 656: `$"{artifact} diagnostic {index} exceeds the configured diagnostic byte limit."` -> `$""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 935 | Common/Results/VerifierResultCodec.cs | 658:13-658:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T038 | batch-2/0106-935 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 658: `index++;` -> `;`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 936 | Common/Results/VerifierResultCodec.cs | 658:13-658:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | candidate / T038 | batch-1/0107-936 | validated_pass | genuine missing test | Current diagnostic tests cover explicit nulls, artifact wrong-kind and snippet missing only. This proposal targets other required keys, nullable-member kinds, closed shape, and later-item path separately. Exact mutation at line 658: `index++` -> `index--`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 944 | Common/Results/VerifierResultCodec.cs | 669:38-669:92 | Equality mutation | <code>&quot;Encoding.UTF8.GetByteCount(value) &gt;= maxDiagnosticBytes&quot;</code> | Survived | candidate / T039 | batch-2/0107-944 | validated_pass | genuine missing test | These `>` checks are inclusive at the configured maximum; current reader tests exercise six bytes over five but do not exercise equality for each independently checked value. Exact mutation at line 669: `Encoding.UTF8.GetByteCount(value) > maxDiagnosticBytes` -> `Encoding.UTF8.GetByteCount(value) >= maxDiagnosticBytes`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 946 | Common/Results/VerifierResultCodec.cs | 670:55-670:138 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T057 | batch-1/0108-946 | validated_pass | genuine missing test | Frozen tests already enter the affected branch and assert exception type, but not the changed message literal; this candidate adds an exact observable message assertion rather than a new semantic guard. Exact mutation at line 670: `"A configuration fidelity assessment exceeds the configured diagnostic byte limit."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 961 | Common/Results/VerifierResultCodec.cs | 696:51-696:124 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T057 | batch-2/0108-961 | validated_pass | genuine missing test | Frozen tests already enter the affected branch and assert exception type, but not the changed message literal; this candidate adds an exact observable message assertion rather than a new semantic guard. Exact mutation at line 696: `"The verifier result exceeds the configured diagnostics per check limit."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 963 | Common/Results/VerifierResultCodec.cs | 702:19-702:59 | Conditional (true) mutation | <code>&quot;(true?null :problems[0])&quot;</code> | Survived | candidate / T075 | batch-1/0109-963 | validated_pass | genuine missing test | This helper returns the first StrictJson.ValidateProperties result. Current tests cover missing snippet and wrong-kind artifact, not an unknown member; the existing null/return paths otherwise remain unasserted. Exact mutation at line 702: `problems.Count == 0 ? null : problems[0]` -> `(true?null :problems[0])`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 973 | Common/Results/VerifierResultCodec.cs | 716:16-716:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T067 | batch-2/0109-973 | validated_pass | genuine missing test | This exact false return prevents substituting a default/null string; current result tests provide all six strings correctly. Exact mutation at line 716: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 983 | Common/Results/VerifierResultCodec.cs | 725:13-725:133 | Logical mutation | <code>&quot;parent.TryGetProperty(name, out JsonElement element) &#124;&#124; (element.ValueKind is JsonValueKind.True or JsonValueKind.False)&quot;</code> | Survived | candidate / T023 | batch-1/0110-983 | validated_pass | genuine missing test | TryBool's property-presence and JSON-kind short circuit is the behavior changed on these lines; current coverage provides booleans only. Exact mutation at line 725: `parent.TryGetProperty(name, out JsonElement element) && (element.ValueKind is JsonValueKind.True or JsonValueKind.False)` -> `parent.TryGetProperty(name, out JsonElement element) \|\| (element.ValueKind is JsonValueKind.True or JsonValueKind.False)`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 989 | Common/Results/VerifierResultCodec.cs | 727:17-727:22 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T062 | batch-2/0110-989 | validated_pass | genuine missing test | These helpers distinguish missing optional members from malformed present values and required scalar failures; current result tests focus on well-typed roundtrips/omissions. Exact mutation at line 727: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 990 | Common/Results/VerifierResultCodec.cs | 728:16-728:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T023 | batch-1/0111-990 | validated_pass | genuine missing test | TryBool's property-presence and JSON-kind short circuit is the behavior changed on these lines; current coverage provides booleans only. Exact mutation at line 728: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 999 | Common/Results/VerifierResultCodec.cs | 736:58-736:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T062 | batch-2/0111-999 | validated_pass | genuine missing test | These helpers distinguish missing optional members from malformed present values and required scalar failures; current result tests focus on well-typed roundtrips/omissions. Exact mutation at line 736: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1000 | Common/Results/VerifierResultCodec.cs | 736:70-736:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T062 | batch-1/0112-1000 | validated_pass | genuine missing test | These helpers distinguish missing optional members from malformed present values and required scalar failures; current result tests focus on well-typed roundtrips/omissions. Exact mutation at line 736: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1001 | Common/Results/VerifierResultCodec.cs | 736:97-736:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T062 | batch-2/0112-1001 | validated_pass | genuine missing test | These helpers distinguish missing optional members from malformed present values and required scalar failures; current result tests focus on well-typed roundtrips/omissions. Exact mutation at line 736: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1010 | Common/Results/VerifierResultCodec.cs | 744:58-744:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T049 | batch-1/0113-1010 | validated_pass | genuine missing test | TryOptionalInt64 distinguishes absent from present malformed values. Existing broker test covers the present numeric and absent cases, but not malformed presence. Exact mutation at line 744: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1011 | Common/Results/VerifierResultCodec.cs | 744:70-744:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T049 | batch-2/0113-1011 | validated_pass | genuine missing test | TryOptionalInt64 distinguishes absent from present malformed values. Existing broker test covers the present numeric and absent cases, but not malformed presence. Exact mutation at line 744: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1012 | Common/Results/VerifierResultCodec.cs | 744:97-744:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T049 | batch-1/0114-1012 | validated_pass | genuine missing test | TryOptionalInt64 distinguishes absent from present malformed values. Existing broker test covers the present numeric and absent cases, but not malformed presence. Exact mutation at line 744: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1017 | Common/Results/VerifierResultCodec.cs | 753:48-753:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-2/0114-1017 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 753: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1018 | Common/Results/VerifierResultCodec.cs | 753:60-753:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-1/0115-1018 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 753: `"missing_property"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1019 | Common/Results/VerifierResultCodec.cs | 754:20-754:25 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T058 | batch-2/0115-1019 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 754: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1028 | Common/Results/VerifierResultCodec.cs | 758:70-758:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-1/0116-1028 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 758: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1034 | Common/Results/VerifierResultCodec.cs | 767:48-767:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-2/0116-1034 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 767: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1035 | Common/Results/VerifierResultCodec.cs | 767:60-767:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-1/0117-1035 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 767: `"missing_property"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1036 | Common/Results/VerifierResultCodec.cs | 768:20-768:25 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T058 | batch-2/0117-1036 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 768: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1044 | Common/Results/VerifierResultCodec.cs | 772:58-772:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-1/0118-1044 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 772: `"."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1045 | Common/Results/VerifierResultCodec.cs | 772:70-772:87 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T058 | batch-2/0118-1045 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 772: `"wrong_json_kind"` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1046 | Common/Results/VerifierResultCodec.cs | 772:97-772:102 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T058 | batch-1/0119-1046 | validated_pass | genuine missing test | Current tests establish explicit-null success, artifact wrong-kind, and missing snippet. Other members follow separate helper calls and need equivalent member-specific literals; the specific mutated line determines which one. Exact mutation at line 772: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1051 | Common/Results/VerifierResultCodec.cs | 779:64-779:69 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T014 | batch-2/0119-1051 | validated_pass | genuine missing test | The frozen roundtrip test contains fail/invalid only. Read helper paths for pass/incomplete/type rejection must be exercised separately from writer conditional branches. Exact mutation at line 779: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1056 | Common/Results/VerifierResultCodec.cs | 783:88-783:92 | Boolean mutation | <code>&quot;false&quot;</code> | Survived | candidate / T014 | batch-1/0120-1056 | validated_pass | genuine missing test | The frozen roundtrip test contains fail/invalid only. Read helper paths for pass/incomplete/type rejection must be exercised separately from writer conditional branches. Exact mutation at line 783: `true` -> `false`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1065 | Common/Results/VerifierResultCodec.cs | 789:17-789:31 | Equality mutation | <code>&quot;text != second&quot;</code> | Survived | candidate / T014 | batch-2/0120-1065 | validated_pass | genuine missing test | The frozen roundtrip test contains fail/invalid only. Read helper paths for pass/incomplete/type rejection must be exercised separately from writer conditional branches. Exact mutation at line 789: `text == second` -> `text != second`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1067 | Common/Results/VerifierResultCodec.cs | 789:98-789:102 | Boolean mutation | <code>&quot;false&quot;</code> | Survived | candidate / T014 | batch-1/0121-1067 | validated_pass | genuine missing test | The frozen roundtrip test contains fail/invalid only. Read helper paths for pass/incomplete/type rejection must be exercised separately from writer conditional branches. Exact mutation at line 789: `true` -> `false`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1068 | Common/Results/VerifierResultCodec.cs | 791:16-791:21 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T014 | batch-2/0121-1068 | validated_pass | genuine missing test | The frozen roundtrip test contains fail/invalid only. Read helper paths for pass/incomplete/type rejection must be exercised separately from writer conditional branches. Exact mutation at line 791: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1076 | Common/Results/VerifierResultCodec.cs | 801:47-801:52 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T065 | batch-1/0122-1076 | validated_pass | genuine missing test | Current writer/read tests use invalid/incomplete/complete_valid in different ways but do not submit a future token through this parser; exact token switch is closed. Exact mutation at line 801: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1081 | Common/Results/VerifierResultCodec.cs | 816:65-816:70 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T059 | batch-2/0122-1081 | validated_pass | genuine missing test | The Fail overloads and Failure/Problem constructors are exercised by normal positive tests only indirectly. Current bad nullable diagnostic assertions do not inspect this root scalar problem shape. Exact mutation at line 816: `false` -> `true`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1083 | Common/Results/VerifierResultCodec.cs | 817:66-817:71 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | candidate / T042 | batch-1/0123-1083 | validated_pass | genuine missing test | The source at line 817 is the long Fail overload used by TryInt64. A quoted number makes TryGetInt64 fail, then this mutation changes only the fallback bool to true while assigning value=0; the caller consequently accepts zero. The test must assert parse failure and exact field/code, not only a thrown exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1091 | Common/Results/VerifierResultCodec.cs | 823:13-823:115 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T043 | batch-2/0123-1091 | compile_error | preview compilation discrepancy | Current duration test protects the toward-zero negative fractional case, but it never enters the Int64 representability branch. Exact mutation at line 823: `throw new ArgumentException("A result duration cannot be represented as an Int64 millisecond value.");` -> `;`. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1092 | Common/Results/VerifierResultCodec.cs | 823:41-823:113 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T043 | batch-1/0124-1092 | validated_pass | genuine missing test | The source branch throws after decimal.Truncate when the value exceeds Int64.MaxValue; current negative-fractional test exercises truncation but not this branch, and the mutation alters only this observable message. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1112 | Common/Results/VerifierResultCodec.cs | 854:13-854:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T120 | batch-2/0124-1112 | validated_pass | equivalent/unreachable with proof | This guard is in BoundedMemoryStream.WriteByte. Pinned Utf8JsonWriter stream flush calls Stream.Write(ReadOnlySpan<byte>), not WriteByte; the full pinned Utf8JsonWriter source contains no WriteByte call. Separate reachable span/byte[] override dispatch is documented in top-level bounded_stream_dispatch; do not infer these WriteByte mutations prove the other overrides unreachable. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1113 | Common/Results/VerifierResultCodec.cs | 855:13-855:35 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T120 | batch-1/0125-1113 | validated_pass | equivalent/unreachable with proof | Utf8JsonWriter(Stream) flushes WrittenSpan with Stream.Write(ReadOnlySpan<byte>). BoundedMemoryStream overrides Write(ReadOnlySpan<byte>), and its `base.Write(buffer)` enters MemoryStream.Write(ReadOnlySpan<byte>). Since runtime type is derived, MemoryStream delegates to base Stream.Write(ReadOnlySpan<byte>) (MemoryStream.cs 621-629). Stream rents a byte array, copies the span, then calls virtual Write(sharedBuffer,0,buffer.Length) (Stream.cs 912-923), dispatching to BoundedMemoryStream.Write(byte[],int,int), which has its own EnsureCapacityFor(count) before writing. Thus both bounded Write overloads are reachable. The same count is checked twice with no write between; removing the span-overload EnsureCapacityFor(buffer.Length) is redundant, not unreachable. The WriteByte override is separate and unreachable from this writer path: Utf8JsonWriter source contains no WriteByte call and flush dispatches via Write(ReadOnlySpan<byte>). This exact mutant removes only the body call from WriteByte. It does not affect either reachable Write overload. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1123 | Common/Results/VerifierResultCodec.cs | 861:55-861:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | candidate / T050 | batch-2/0125-1123 | validated_pass | genuine missing test | Frozen output limit test enters the bound but asserts exception type only; these mutations change its observable message or condition, so the exact limit boundary needs a distinct test/assertion. Exact mutation at line 861: `"The verifier result exceeds the configured output byte limit."` -> `""`. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1131 | Common/Serialization/StrictJson.cs | 25:13-25:101 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0126-1131 | validated_pass | genuine missing test | This removes the MaxArtifactBytes throw in ParseDocument itself. The existing P13 oversized-ledger case does not exercise this site: LedgerReader first calls ReadJsonLines, whose separate artifact-byte guard throws before it parses rows. Other direct-document readers call ParseDocument, but no current test sets MaxArtifactBytes below a valid manifest/index/native/start-line document while keeping MaxTotalInputBytes permissive. Add that direct-document boundary case to observe this guard. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1132 | Common/Serialization/StrictJson.cs | 25:51-25:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-2/0126-1132 | validated_pass | genuine missing test | This changes only the artifact-byte-limit exception message in ParseDocument. The existing P13 oversized-ledger case does not execute this guard: LedgerReader first calls ReadJsonLines, which rejects the oversized source at its own earlier byte check. No current test reaches ParseDocument with a standalone document larger than MaxArtifactBytes while MaxTotalInputBytes permits the input, so neither guard behavior nor its useful artifact/limit diagnostic is covered. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1143 | Common/Serialization/StrictJson.cs | 41:17-41:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0127-1143 | validated_pass | genuine missing test | Removing this Dispose leaves a JsonDocument undisposed on the non-object root rejection path. JsonDocument owns parsed metadata/input storage and Dispose releases that storage; the returned structural problem is unchanged, so current result assertions do not observe this resource-lifecycle obligation. No current test asserts cleanup on this path; keep any deterministic lifecycle assertion separate from validity semantics. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1144 | Common/Serialization/StrictJson.cs | 42:66-42:83 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-2/0127-1144 | validated_pass | genuine missing test | The mutation changes only the root_not_object problem code. ParseDocument returns this code when requireObject is true and the parsed root is not an object; no named test supplies a scalar/array root and asserts this code. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1147 | Common/Serialization/StrictJson.cs | 48:17-48:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0128-1147 | validated_pass | genuine missing test | Removing this Dispose leaves a JsonDocument undisposed on the duplicate-property rejection path. JsonDocument owns parsed metadata/input storage and Dispose releases that storage; the returned structural problem is unchanged, so current result assertions do not observe this resource-lifecycle obligation. No current test asserts cleanup on this path; keep any deterministic lifecycle assertion separate from validity semantics. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1149 | Common/Serialization/StrictJson.cs | 56:62-56:76 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T098 | batch-2/0128-1149 | validated_pass | genuine missing test | The mutation clears the invalid_utf8 problem code after strict decoding fails. The framing test supplies invalid UTF-8, but its assertion checks only that a problem routes to check 1; it does not assert Code == invalid_utf8. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1155 | Common/Serialization/StrictJson.cs | 76:29-76:46 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T099 | batch-1/0129-1155 | validated_pass | genuine missing test | The mutation clears expected_object from ValidateProperties' non-object result. Wrong-kind reader tests assert owner and field, but do not assert the expected_object code for a nested shape replaced with a scalar. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1162 | Common/Serialization/StrictJson.cs | 90:38-90:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T100 | batch-2/0129-1162 | validated_pass | genuine missing test | The mutation clears missing_property, the code emitted when a required JSON key is absent. The required-property theory checks owner and field only, not the code. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1167 | Common/Serialization/StrictJson.cs | 98:38-98:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T102 | batch-1/0130-1167 | validated_pass | genuine missing test | The mutation clears unknown_property for a key outside the exact allowed set. Unknown-field tests assert check routing and field path; they do not assert this code. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1174 | Common/Serialization/StrictJson.cs | 114:51-114:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-2/0130-1174 | validated_pass | genuine missing test | The mutation changes only the interpolated artifact-byte-limit exception message in ReadJsonLines; it leaves the `utf8.Length > limits.MaxArtifactBytes` guard and exception type intact. The P13 oversized-ledger case reaches this message but asserts only InputLimitExceededException, so useful artifact/limit diagnostic context is not covered. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1193 | Common/Serialization/StrictJson.cs | 136:89-136:106 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T098 | batch-1/0131-1193 | validated_pass | genuine missing test | The mutation clears jsonl_blank_row for an empty JSONL record. The framing test includes LF-only input but asserts only a nonempty problem list and owner, not the blank-row code. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1198 | Common/Serialization/StrictJson.cs | 141:17-141:125 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0131-1198 | validated_pass | genuine missing test | Removing this row-specific throw does not remove the resource failure: the next ParseDocument call passes `MaxArtifactBytes = MaxRowBytes` and independently throws when that same row is oversized. It changes the exception from row-specific context to the generic artifact byte-limit message. The P13 one-byte-over-row case checks only exception type, so it does not cover preservation of row number/row-limit diagnostics. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1199 | Common/Serialization/StrictJson.cs | 141:55-141:123 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-1/0132-1199 | validated_pass | genuine missing test | Only the row-limit exception message interpolation changes; exception type and row-byte guard remain. The current exception message identifies artifact, row number, and byte limit; P13 checks only exception type, so it does not verify that useful failure context survives. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1202 | Common/Serialization/StrictJson.cs | 151:13-151:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0132-1202 | validated_pass | genuine missing test | Removing this Dispose leaves the successfully parsed JSONL row JsonDocument undisposed before continuing to the next row. JsonDocument owns parsed metadata/input storage and Dispose releases that storage; the extracted raw row and parsed result are unchanged, so current value assertions do not observe this resource-lifecycle obligation. No current test asserts cleanup on this path. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1204 | Common/Serialization/StrictJson.cs | 153:23-153:43 | Remove checked expression | <code>&quot;entries + 1&quot;</code> | Survived | none / T121 | batch-1/0133-1204 | validated_pass | equivalent/unreachable with proof | This removes checked arithmetic from the local JSONL row counter. The counter increments once per LF in ReadOnlyMemory<byte>; its number of increments cannot exceed the input memory length (Int32-sized), so entries + 1 cannot overflow Int64 for any representable input. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1211 | Common/Serialization/StrictJson.cs | 154:56-154:83 | Equality mutation | <code>&quot;entries &gt;= limits.MaxEntries&quot;</code> | Survived | current / T096 | batch-2/0133-1211 | validated_pass | genuine missing test | The changed post-row comparison rejects exactly MaxEntries rows. Existing P13 uses MaxEntries=28 while the ledger has only 2 rows, so it does not reach this comparison at equality; add one minimal JSONL row with MaxEntries=1 while keeping its row-token count within that ceiling. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1214 | Common/Serialization/StrictJson.cs | 156:55-156:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-1/0134-1214 | validated_pass | genuine missing test | Only the row-or-entry limit exception message changes; the guard/type remain. The message is the only context naming this failure as a row-or-entry ceiling, and the current assertion checks only exception type. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1220 | Common/Serialization/StrictJson.cs | 174:18-174:30 | String mutation | <code>&quot;\&quot;Stryker was here!\&quot;&quot;</code> | Survived | none / T121 | batch-2/0134-1220 | validated_pass | equivalent/unreachable with proof | TryGetString writes this out value only on a false return. All reader call sites branch on the boolean and return/record a structural problem before consuming the out string; the changed fallback value is not observed by those callers. This is an internal helper, not a public parser surface. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1236 | Common/Serialization/StrictJson.cs | 206:17-206:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T099 | batch-1/0135-1236 | validated_pass | genuine missing test | The mutation clears wrong_json_kind from StrictJson.WrongKind. Wrong-kind parser cases verify route and field but do not assert that their StructuralProblem.Code remains wrong_json_kind. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1257 | Common/Serialization/StrictJson.cs | 238:51-238:58 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | none / T121 | batch-2/0135-1257 | validated_pass | genuine missing test | The mutation decrements the array-item path index instead of incrementing it while FindDuplicate traverses children. Existing duplicate tests add a duplicate under a first array item; none verifies the diagnostic path for a duplicate nested under a later array item. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1273 | Common/Serialization/StrictJson.cs | 263:55-263:106 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-1/0136-1273 | validated_pass | genuine missing test | The mutations clear the exception context that distinguishes nesting-depth excess from entry-count excess; the same exception type still throws. P13 currently checks exception type only, so it does not exercise the diagnostic distinction described by the plan. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1293 | Common/Serialization/StrictJson.cs | 273:59-273:108 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-2/0136-1293 | validated_pass | genuine missing test | The mutations clear the exception context that distinguishes nesting-depth excess from entry-count excess; the same exception type still throws. P13 currently checks exception type only, so it does not exercise the diagnostic distinction described by the plan. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1317 | Common/Serialization/StrictJson.cs | 311:37-311:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T097 | batch-1/0137-1317 | validated_pass | equivalent/unreachable with proof | This changes only the JsonException message created after the narrowly caught System.Text.Json Rethrowable InvalidOperationException. ParseDocument catches JsonException and maps by exception type to malformed_json, so the surrogate-routing result remains unchanged. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1328 | Common/Serialization/StrictJson.cs | 420:22-420:45 | Remove checked expression | <code>&quot;_bytes + count&quot;</code> | Survived | none / T121 | batch-2/0137-1328 | validated_pass | genuine missing test | This removes checked addition from InputBudget.ConsumeBytes. The plan explicitly requires checked aggregate-byte arithmetic and overflow to be an execution failure; no current named test drives long overflow. A direct test can consume long.MaxValue then one more byte using a limits value that permits the first consume. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1331 | Common/Serialization/StrictJson.cs | 424:13-424:138 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0138-1331 | compile_error | preview compilation discrepancy | The mutation removes the overflow-to-InputLimitExceededException throw in ConsumeBytes. The checked addition catch is the path that distinguishes sum overflow from normal aggregate-byte excess; there is no current direct overflow test. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1332 | Common/Serialization/StrictJson.cs | 424:51-424:136 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-2/0138-1332 | compile_error | preview compilation discrepancy | The changed text clears the aggregate-byte failure explanation while preserving the exception type. The current aggregate-byte assertion confirms a throw, not that the execution diagnostic identifies the byte budget as the cause. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1338 | Common/Serialization/StrictJson.cs | 429:51-429:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T096 | batch-1/0139-1338 | validated_pass | genuine missing test | The changed text clears the aggregate-byte failure explanation while preserving the exception type. The current aggregate-byte assertion confirms a throw, not that the execution diagnostic identifies the byte budget as the cause. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1341 | Common/Serialization/StrictJson.cs | 437:24-437:49 | Remove checked expression | <code>&quot;_entries + count&quot;</code> | Survived | none / T121 | batch-2/0139-1341 | validated_pass | genuine missing test | This removes checked addition from InputBudget.ConsumeEntries. The plan requires checked aggregate-entry arithmetic; no named test drives a long overflow (for example MaxEntries=long.MaxValue, then consume long.MaxValue and 1). | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1342 | Common/Serialization/StrictJson.cs | 437:32-437:48 | Arithmetic mutation | <code>&quot;_entries - count&quot;</code> | Survived | none / T121 | batch-1/0140-1342 | validated_pass | genuine missing test | The mutation subtracts consumed counts, so consecutive artifacts reduce rather than accumulate the private aggregate. Current P13 limits each reader's local input but does not share one InputBudget across two inputs with a combined entry count over the ceiling; add that targeted aggregate-budget case. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1344 | Common/Serialization/StrictJson.cs | 441:13-441:139 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0140-1344 | compile_error | preview compilation discrepancy | The mutation removes the overflow-to-InputLimitExceededException throw in ConsumeEntries. The checked sum catch is required by the plan's checked aggregate-entry rule; no named test directly exercises overflow. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1345 | Common/Serialization/StrictJson.cs | 441:51-441:137 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-1/0141-1345 | compile_error | preview compilation discrepancy | These mutations clear the overflow/aggregate-entry cause text but retain exception type and control flow. Existing tests assert the execution-failure type only; no assertion verifies the diagnostic distinguishes entry-budget overflow from entry-limit excess. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1347 | Common/Serialization/StrictJson.cs | 444:13-444:41 | Equality mutation | <code>&quot;_entries &gt;= limits.MaxEntries&quot;</code> | Survived | none / T121 | batch-2/0141-1347 | validated_pass | genuine missing test | The mutation rejects aggregate count exactly equal to MaxEntries. Current parser P13 boundaries cover local row/token counts, not a shared InputBudget reaching its aggregate cap exactly; add a direct shared-budget equality case. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1350 | Common/Serialization/StrictJson.cs | 446:13-446:118 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0142-1350 | validated_pass | genuine missing test | The mutation removes the aggregate-entry execution-failure throw. Current tests do not demonstrate two input readers sharing a budget whose combined ConsumeEntries exceeds MaxEntries; a direct aggregate-budget test is needed to show this guard. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1351 | Common/Serialization/StrictJson.cs | 446:51-446:116 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-2/0142-1351 | validated_pass | genuine missing test | These mutations clear the overflow/aggregate-entry cause text but retain exception type and control flow. Existing tests assert the execution-failure type only; no assertion verifies the diagnostic distinguishes entry-budget overflow from entry-limit excess. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1372 | Common/Window/StartLineNoteCodec.cs | 29:9-29:49 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0143-1372 | validated_pass | genuine missing test | This removes an explicit null-argument guard from StartLineNoteCodec.Write (note, limits, identity, partition list, or one partition item). The codec currently promises an ArgumentNullException at the guard site; no named test supplies these invalid arguments and asserts the exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1373 | Common/Window/StartLineNoteCodec.cs | 30:9-30:51 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0143-1373 | validated_pass | genuine missing test | This removes an explicit null-argument guard from StartLineNoteCodec.Write (note, limits, identity, partition list, or one partition item). The codec currently promises an ArgumentNullException at the guard site; no named test supplies these invalid arguments and asserts the exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1374 | Common/Window/StartLineNoteCodec.cs | 31:9-31:58 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0144-1374 | validated_pass | genuine missing test | This removes an explicit null-argument guard from StartLineNoteCodec.Write (note, limits, identity, partition list, or one partition item). The codec currently promises an ArgumentNullException at the guard site; no named test supplies these invalid arguments and asserts the exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1375 | Common/Window/StartLineNoteCodec.cs | 32:9-32:64 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0144-1375 | validated_pass | genuine missing test | This removes an explicit null-argument guard from StartLineNoteCodec.Write (note, limits, identity, partition list, or one partition item). The codec currently promises an ArgumentNullException at the guard site; no named test supplies these invalid arguments and asserts the exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1378 | Common/Window/StartLineNoteCodec.cs | 35:13-35:102 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0145-1378 | validated_pass | genuine missing test | This custom ArgumentException message is observable to callers; PR3 has no production caller that normalizes it. Current writer coverage has no invalid StartLineNote.SchemaVersion assertion, so the useful failure context is untested. Candidate coverage should preserve the message/parameter without making all prose an accidental protocol. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1379 | Common/Window/StartLineNoteCodec.cs | 35:41-35:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T121 | batch-2/0145-1379 | validated_pass | genuine missing test | This custom ArgumentException message is observable to callers; PR3 has no production caller that normalizes it. Current writer coverage has no invalid StartLineNote.SchemaVersion assertion, so the useful failure context is untested. Candidate coverage should preserve the message/parameter without making all prose an accidental protocol. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1391 | Common/Window/StartLineNoteCodec.cs | 49:17-49:57 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0146-1391 | validated_pass | genuine missing test | This removes an explicit null-argument guard from StartLineNoteCodec.Write (note, limits, identity, partition list, or one partition item). The codec currently promises an ArgumentNullException at the guard site; no named test supplies these invalid arguments and asserts the exception. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1421 | Common/Window/StartLineNoteCodec.cs | 91:56-91:73 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0146-1421 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1431 | Common/Window/StartLineNoteCodec.cs | 104:93-104:110 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0147-1431 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1435 | Common/Window/StartLineNoteCodec.cs | 105:110-105:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0147-1435 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1439 | Common/Window/StartLineNoteCodec.cs | 106:122-106:139 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0148-1439 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1443 | Common/Window/StartLineNoteCodec.cs | 107:136-107:153 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0148-1443 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1447 | Common/Window/StartLineNoteCodec.cs | 108:131-108:148 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0149-1447 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1452 | Common/Window/StartLineNoteCodec.cs | 113:57-113:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0149-1452 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1454 | Common/Window/StartLineNoteCodec.cs | 116:13-116:58 | Equality mutation | <code>&quot;highsElement.GetArrayLength() &gt;= maxPartitions&quot;</code> | Survived | current / T104 | batch-1/0150-1454 | validated_pass | genuine missing test | The mutation changes the partition-count guard from > maxPartitions to >= maxPartitions, rejecting exactly-max-sized notes. The named limit test uses two partitions with maxPartitions=1 (strictly over); there is no exact-equality accepted-boundary assertion. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1458 | Common/Window/StartLineNoteCodec.cs | 118:51-118:112 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | current / T104 | batch-2/0150-1458 | validated_pass | genuine missing test | Only the partition-limit diagnostic text changes; the throw and bound remain. The message is the sole explanation that the configured partition count was exceeded; the existing test checks only exception type. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1468 | Common/Window/StartLineNoteCodec.cs | 129:112-129:129 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0151-1468 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1472 | Common/Window/StartLineNoteCodec.cs | 130:98-130:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0151-1472 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1474 | Common/Window/StartLineNoteCodec.cs | 132:13-132:21 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0152-1474 | validated_pass | genuine missing test | The mutation removes/reverses the partition array index increment used in diagnostic paths. Existing malformed partition test cases use only partition_highs[0]; the two-element round trip tests valid values and never assert a second-item error path. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1475 | Common/Window/StartLineNoteCodec.cs | 132:13-132:20 | PostIncrementExpression to PostDecrementExpression mutation | <code>&quot;index--&quot;</code> | Survived | none / T121 | batch-2/0152-1475 | validated_pass | genuine missing test | The mutation removes/reverses the partition array index increment used in diagnostic paths. Existing malformed partition test cases use only partition_highs[0]; the two-element round trip tests valid values and never assert a second-item error path. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1506 | Common/Window/StartLineNoteCodec.cs | 169:125-169:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0153-1506 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1512 | Common/Window/StartLineNoteCodec.cs | 170:140-170:157 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0153-1512 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1518 | Common/Window/StartLineNoteCodec.cs | 171:110-171:127 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0154-1518 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1524 | Common/Window/StartLineNoteCodec.cs | 172:125-172:142 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0154-1524 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1530 | Common/Window/StartLineNoteCodec.cs | 173:151-173:168 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-1/0155-1530 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1536 | Common/Window/StartLineNoteCodec.cs | 174:119-174:136 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T101 | batch-2/0155-1536 | validated_pass | genuine missing test | Each mutation clears wrong_json_kind from a StartLineNote structural problem (schema, root string/array field, partition value, or identity string). StartLineRequiredPropertyInvalid asserts owner and path only; it does not assert the code, although P2 requires the literal structural problem. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1558 | Common/Window/StartLineNoteCodec.cs | 215:13-215:38 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0156-1558 | validated_pass | equivalent/unreachable with proof | This removes EnsureCapacityFor(count) from the private Write(byte[], int, int) override. Utf8JsonWriter 10.0.13 (pinned commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec, /workspace/scratch/pinned-sources/Utf8JsonWriter.cs:410-419) writes pending output through Write(WrittenSpan) on NET. The private span override checks EnsureCapacityFor(buffer.Length) before calling base.Write(buffer). For derived MemoryStream, pinned MemoryStream.cs:621-629 delegates base.Write(span) to Stream.Write(span); pinned Stream.cs:912-923 then virtual-dispatches Write(sharedBuffer, 0, buffer.Length) to this byte[] override. If this inner guard is removed, the outer span guard has already checked the same count against the same Length, with no write between them; bound enforcement is unchanged. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1561 | Common/Window/StartLineNoteCodec.cs | 221:13-221:46 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0156-1561 | validated_pass | equivalent/unreachable with proof | This removes EnsureCapacityFor(buffer.Length) from the private span override. The call then enters pinned .NET 10.0.13 MemoryStream.Write(ReadOnlySpan<byte>) for a derived instance (MemoryStream.cs:621-629), which delegates to Stream.Write(ReadOnlySpan<byte>); Stream.cs:912-923 copies the same span and virtual-dispatches to Write(byte[], 0, buffer.Length), where this class still calls EnsureCapacityFor(count) before writing. No write changes Length between the checks, and both use the same positive byte count. Thus the byte[] override still enforces the configured output bound. The relevant pinned runtime files are from commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1564 | Common/Window/StartLineNoteCodec.cs | 227:13-227:34 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-1/0157-1564 | validated_pass | equivalent/unreachable with proof | This removes the guard in BoundedMemoryStream.WriteByte. The only writer is Utf8JsonWriter 10.0.13 (pinned commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec), whose stream path commits pending data with _stream.Write(WrittenSpan) at Utf8JsonWriter.cs:410-419; it has no _stream.WriteByte call. The private override has no other caller, so its guard is unreachable from the codec. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1565 | Common/Window/StartLineNoteCodec.cs | 228:13-228:35 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T121 | batch-2/0157-1565 | validated_pass | equivalent/unreachable with proof | This removes base.WriteByte(value) from the private BoundedMemoryStream.WriteByte override. The only writer is Utf8JsonWriter 10.0.13 (pinned commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec), whose stream path commits pending data with _stream.Write(WrittenSpan) at Utf8JsonWriter.cs:410-419; it has no _stream.WriteByte call. The private override has no other caller, so this body is unreachable from the codec. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1570 | Common/Window/StartLineNoteCodec.cs | 233:17-233:26 | Equality mutation | <code>&quot;count &lt;= 0&quot;</code> | Survived | none / T121 | batch-1/0158-1570 | validated_pass | equivalent/unreachable with proof | This changes EnsureCapacityFor so it rejects zero counts. The private helper is called only by the byte[], span, and WriteByte overrides. Utf8JsonWriter 10.0.13 (pinned commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec) flushes the stream only when BytesPending != 0 and passes WrittenSpan (Utf8JsonWriter.cs:410-419); its code has no WriteByte call. Its span write count is therefore positive. For the span path, pinned MemoryStream.cs:621-629 and Stream.cs:912-923 delegate the same positive length to the byte[] override. No codec-reachable call passes zero to this helper. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1572 | Common/Window/StartLineNoteCodec.cs | 233:30-233:59 | Equality mutation | <code>&quot;Length &gt;= maximumBytes - count&quot;</code> | Survived | current / T103 | batch-2/0158-1572 | validated_pass | genuine missing test | This changes the byte-bound comparison so a write that reaches exactly maximumBytes throws. Existing output-limit coverage only tests a serialization larger than 32 bytes; it does not prove an exactly-max-size serialized note succeeds. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1575 | Common/Window/StartLineNoteCodec.cs | 234:55-234:118 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T103 | batch-1/0159-1575 | validated_pass | genuine missing test | Only the output-byte-limit explanation is removed; the byte guard and exception type remain. The existing output-limit test asserts the type but not that logs retain the output-byte-limit reason. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1577 | Program.cs | 36:9-36:49 | Statement mutation | <code>&quot;;&quot;</code> | Survived | candidate / T086 | batch-2/0159-1577 | validated_pass | genuine missing test | Removing the guard changes the runtime exception path before host creation. Host.CreateApplicationBuilder receives the same null array and may throw a different exception; Program.RunAsync catches it and returns 1 if host stays null. Nullable annotations do not suppress runtime null. No H1-H9 test calls RunAsync(null!), so record this as missing coverage, not equivalent behavior. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1578 | Program.cs | 39:9-41:32 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T089 | batch-1/0160-1578 | validated_pass | genuine missing test | Lines 39-44 wire AddOptions().ValidateOnStart(), the custom options binder and IValidateOptions registration. The existing host test checks only a nonzero result, which the missing-handler path can also produce; it does not prove malformed options prevent a handler from running. Candidate: host-run malformed required option with counting handler and assert zero calls. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1580 | Program.cs | 44:9-44:110 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T089 | batch-2/0160-1580 | validated_pass | genuine missing test | Lines 39-44 wire AddOptions().ValidateOnStart(), the custom options binder and IValidateOptions registration. The existing host test checks only a nonzero result, which the missing-handler path can also produce; it does not prove malformed options prevent a handler from running. Candidate: host-run malformed required option with counting handler and assert zero calls. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1581 | Program.cs | 45:9-45:76 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T092 | batch-1/0161-1581 | validated_pass | genuine missing test | RunAsync passes the injected TimeProvider to BuildHostCore and TryStartWatchdog; the child watchdog test supplies ManualTimeProvider. Removing the registration or always selecting TimeProvider.System can bypass its synthetic deadline, so this mutation should be distinguished by the blocked-work/cleanup child case. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1582 | Program.cs | 45:39-45:74 | Null coalescing mutation (remove left) | <code>&quot;TimeProvider.System&quot;</code> | Survived | current / T092 | batch-2/0161-1582 | validated_pass | genuine missing test | RunAsync passes the injected TimeProvider to BuildHostCore and TryStartWatchdog; the child watchdog test supplies ManualTimeProvider. Removing the registration or always selecting TimeProvider.System can bypass its synthetic deadline, so this mutation should be distinguished by the blocked-work/cleanup child case. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1591 | Program.cs | 75:56-75:61 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | none / T106 | batch-1/0162-1591 | validated_pass | equivalent/unreachable with proof | The code path starts at Program.Main(string[] args) => RunAsync(args); Main installs no SynchronizationContext or custom TaskScheduler, and this Generic Host executable runs as a console process. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [Task.cs lines 2451-2453](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) maps ConfigureAwait(bool) to ContinueOnCapturedContext; [lines 2494-2516](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516) only capture a non-base SynchronizationContext or a non-default TaskScheduler when true. Thus false-to-true is equivalent for the normal process entrypoint. This inference does not extend to internal RunAsync calls under a custom context/scheduler. The pin used here is source tag v10.0.13; the runtime source is cached under /workspace/scratch/pinned-sources/task-v10.0.13.cs and its SHA-256 is recorded in runtime_source_evidence. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1593 | Program.cs | 77:37-77:42 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | none / T106 | batch-2/0162-1593 | validated_pass | equivalent/unreachable with proof | The code path starts at Program.Main(string[] args) => RunAsync(args); Main installs no SynchronizationContext or custom TaskScheduler, and this Generic Host executable runs as a console process. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [Task.cs lines 2451-2453](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) maps ConfigureAwait(bool) to ContinueOnCapturedContext; [lines 2494-2516](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516) only capture a non-base SynchronizationContext or a non-default TaskScheduler when true. Thus false-to-true is equivalent for the normal process entrypoint. This inference does not extend to internal RunAsync calls under a custom context/scheduler. The pin used here is source tag v10.0.13; the runtime source is cached under /workspace/scratch/pinned-sources/task-v10.0.13.cs and its SHA-256 is recorded in runtime_source_evidence. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1595 | Program.cs | 81:17-81:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T095 | batch-1/0163-1595 | validated_pass | genuine missing test | Inner StartAsync/wait exceptions call MarkFailed before logging; H6 expects nonzero for worker exception/cancellation/host stop. Without this write, a prior successful state could be retained, but all exceptions occur before successful completion in the tested path; cleanup faults are separate candidates below. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1596 | Program.cs | 82:17-86:54 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T095 | batch-2/0163-1596 | compile_error | preview compilation discrepancy | These source spans emit the structured host-failure log and supply ScenarioId/RunId from IConfiguration. H6 checks only exit code and absence of output; the log is externally observable and neither event nor identity fields are asserted. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1597 | Program.cs | 85:36-85:57 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T095 | batch-1/0164-1597 | validated_pass | genuine missing test | These source spans emit the structured host-failure log and supply ScenarioId/RunId from IConfiguration. H6 checks only exit code and absence of output; the log is externally observable and neither event nor identity fields are asserted. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1598 | Program.cs | 86:35-86:51 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T095 | batch-2/0164-1598 | validated_pass | genuine missing test | These source spans emit the structured host-failure log and supply ScenarioId/RunId from IConfiguration. H6 checks only exit code and absence of output; the log is externally observable and neither event nor identity fields are asserted. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1600 | Program.cs | 91:17-91:33 | Equality mutation | <code>&quot;host is null&quot;</code> | Survived | candidate / T085 | batch-1/0165-1600 | compile_error | preview compilation discrepancy | When BuildHostCore throws before assigning host, host remains null. The mutation inverts the guard and dereferences host.Services inside the outer catch, so RunAsync can escape with a secondary NullReferenceException instead of returning its failure code. No host-construction fault test covers this branch. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1602 | Program.cs | 93:17-93:101 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;logger = host.Services.GetService&lt;ILoggerFactory&gt;()?.CreateLogger(nameof(Program))&quot;</code> | Survived | none / T118 | batch-2/0165-1602 | validated_pass | equivalent/unreachable with proof | This changes `??=` to `=` while resolving `ILoggerFactory` in the outer catch. On Program.Main's production path, configureForTests is null and the Host builder supplies the same built-in singleton service already resolved earlier in RunAsync; the built ServiceProvider does not replace that registration during execution. This is limited to the normal production entrypoint. The test-only configureForTests hook can register altered services and is not covered by this equivalence claim. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [HostBuilder.cs lines 305-306](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs#L305-L306) registers IConfiguration as singleton, and VerifierExecutionState is registered singleton in Program.cs line 46. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1603 | Program.cs | 94:17-94:77 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;configuration = host.Services.GetService&lt;IConfiguration&gt;()&quot;</code> | Survived | none / T118 | batch-1/0166-1603 | validated_pass | equivalent/unreachable with proof | This changes `??=` to `=` while resolving `IConfiguration` in the outer catch. On Program.Main's production path, configureForTests is null and the Host builder supplies the same built-in singleton service already resolved earlier in RunAsync; the built ServiceProvider does not replace that registration during execution. This is limited to the normal production entrypoint. The test-only configureForTests hook can register altered services and is not covered by this equivalence claim. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [HostBuilder.cs lines 305-306](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/Microsoft.Extensions.Hosting/src/HostBuilder.cs#L305-L306) registers IConfiguration as singleton, and VerifierExecutionState is registered singleton in Program.cs line 46. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1604 | Program.cs | 95:17-95:77 | CoalesceAssignmentExpression to SimpleAssignmentExpression mutation | <code>&quot;state = host.Services.GetService&lt;VerifierExecutionState&gt;()&quot;</code> | Survived | none / T118 | batch-2/0166-1604 | validated_pass | equivalent/unreachable with proof | This changes `??=` to `=` while resolving `VerifierExecutionState` in the outer catch. On Program.Main's production path, configureForTests is null and the Host builder supplies the same built-in singleton service already resolved earlier in RunAsync; the built ServiceProvider does not replace that registration during execution. This is limited to the normal production entrypoint. The test-only configureForTests hook can register altered services and is not covered by this equivalence claim. Pinned source confirms IConfiguration is singleton in HostBuilder.cs:305-306, ILoggerFactory is singleton in LoggingServiceCollectionExtensions.cs:38, and VerifierExecutionState is registered singleton in Program.cs:46; LoggerFactory.CreateLogger caches by category at LoggerFactory.cs:140-157. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1605 | Program.cs | 96:21-96:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | none / T115 | batch-1/0167-1605 | compile_error | preview compilation discrepancy | This guard controls whether LogStartupFailed is called when the outer catch has a built host. H1 invalid modes fail in StartAsync and use LogHostFailed, so they do not execute this outer-catch branch; startup logging after host build has no exercising test. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1607 | Program.cs | 98:21-102:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T116 | batch-2/0167-1607 | compile_error | preview compilation discrepancy | These spans log an exception caught outside the StartAsync block and attach configured ScenarioId/RunId. Current host tests do not capture this event or force an outer-catch failure after host construction. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1608 | Program.cs | 101:40-101:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T116 | batch-1/0168-1608 | validated_pass | genuine missing test | These spans log an exception caught outside the StartAsync block and attach configured ScenarioId/RunId. Current host tests do not capture this event or force an outer-catch failure after host construction. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1609 | Program.cs | 102:40-102:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T116 | batch-2/0168-1609 | validated_pass | genuine missing test | These spans log an exception caught outside the StartAsync block and attach configured ScenarioId/RunId. Current host tests do not capture this event or force an outer-catch failure after host construction. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1610 | Program.cs | 105:17-105:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T117 | batch-1/0169-1610 | validated_pass | equivalent/unreachable with proof | The outer catch is entered either before the inner StartAsync/wait block, when VerifierExecutionState still has its constructor value 1, or after an exception escaped that inner catch. In the latter case line 81 has already called MarkFailed before LogHostFailed, so the state is already 1. This makes the outer MarkFailed idempotent for each catch path; the proof depends on the ordering in Program.RunAsync, not nullable annotations. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1615 | Program.cs | 113:55-113:60 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | none / T106 | batch-2/0169-1615 | validated_pass | equivalent/unreachable with proof | The code path starts at Program.Main(string[] args) => RunAsync(args); Main installs no SynchronizationContext or custom TaskScheduler, and this Generic Host executable runs as a console process. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [Task.cs lines 2451-2453](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) maps ConfigureAwait(bool) to ContinueOnCapturedContext; [lines 2494-2516](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516) only capture a non-base SynchronizationContext or a non-default TaskScheduler when true. Thus false-to-true is equivalent for the normal process entrypoint. This inference does not extend to internal RunAsync calls under a custom context/scheduler. The pin used here is source tag v10.0.13; the runtime source is cached under /workspace/scratch/pinned-sources/task-v10.0.13.cs and its SHA-256 is recorded in runtime_source_evidence. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1617 | Program.cs | 117:21-117:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | none / T114 | batch-1/0170-1617 | compile_error | preview compilation discrepancy | This logger guard is only reached when host.StopAsync throws. H6 covers worker exception/cancellation and application stop, not an IHostedService cleanup exception; no test asserts containment or cleanup log for a failing StopAsync. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1619 | Program.cs | 119:21-123:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T112 | batch-2/0170-1619 | compile_error | preview compilation discrepancy | These spans emit the cleanup-failure log and include scenario/run identity after StopAsync throws. Existing tests include blocked cleanup killed by the watchdog, which cannot observe this catch or its structured fields. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1620 | Program.cs | 122:40-122:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T112 | batch-1/0171-1620 | validated_pass | genuine missing test | These spans emit the cleanup-failure log and include scenario/run identity after StopAsync throws. Existing tests include blocked cleanup killed by the watchdog, which cannot observe this catch or its structured fields. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1621 | Program.cs | 123:40-123:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T112 | batch-2/0171-1621 | validated_pass | genuine missing test | These spans emit the cleanup-failure log and include scenario/run identity after StopAsync throws. Existing tests include blocked cleanup killed by the watchdog, which cannot observe this catch or its structured fields. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1622 | Program.cs | 126:17-126:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T113 | batch-1/0172-1622 | validated_pass | genuine missing test | A handler can mark execution successful before host shutdown; if StopAsync then throws, this MarkFailed is what prevents exit 0. Current H6 does not inject a cleanup failure after successful handler completion. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1626 | Program.cs | 133:21-133:80 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T111 | batch-2/0172-1626 | validated_pass | genuine missing test | This statement awaits IAsyncDisposable.DisposeAsync. H7's blocked-cleanup variant exits from the watchdog before disposal, and normal RunAsync tests do not inject a disposal failure. A host service whose DisposeAsync throws after handler completion would exercise both failure containment and state override. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1627 | Program.cs | 133:73-133:78 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | none / T106 | batch-1/0173-1627 | validated_pass | equivalent/unreachable with proof | The code path starts at Program.Main(string[] args) => RunAsync(args); Main installs no SynchronizationContext or custom TaskScheduler, and this Generic Host executable runs as a console process. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [Task.cs lines 2451-2453](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) maps ConfigureAwait(bool) to ContinueOnCapturedContext; [lines 2494-2516](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516) only capture a non-base SynchronizationContext or a non-default TaskScheduler when true. Thus false-to-true is equivalent for the normal process entrypoint. This inference does not extend to internal RunAsync calls under a custom context/scheduler. The pin used here is source tag v10.0.13; the runtime source is cached under /workspace/scratch/pinned-sources/task-v10.0.13.cs and its SHA-256 is recorded in runtime_source_evidence. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1629 | Program.cs | 137:21-137:36 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T119 | batch-2/0173-1629 | validated_pass | equivalent/unreachable with proof | BuildHostCore returns Host.CreateApplicationBuilder(args).Build(); the pinned dotnet/runtime v10.0.13 Internal.Host declaration implements IAsyncDisposable (cached /workspace/scratch/pinned-sources/host.cs line 19), so the sync-dispose else arm does not execute for this constructed production host. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1631 | Program.cs | 142:21-142:39 | Equality mutation | <code>&quot;logger is null&quot;</code> | Survived | none / T108 | batch-1/0174-1631 | compile_error | preview compilation discrepancy | The logger guard is on the disposal-exception path only. No current host test has a disposable service that throws during host disposal or captures the resulting log. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1633 | Program.cs | 144:21-148:59 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T109 | batch-2/0174-1633 | compile_error | preview compilation discrepancy | These spans emit the disposal-failure event and attach ScenarioId/RunId. No test reaches the disposal catch; H7 intentionally terminates during blocked cleanup. | The normal warnings-as-errors build rejected this mutant before MTP tests could run; Stryker recorded Survived. Preserve both observations; this is not an assertion kill. Exact compiler diagnostics are in sample-compile-discrepancies.json. |
| 1634 | Program.cs | 147:40-147:61 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T109 | batch-1/0175-1634 | validated_pass | genuine missing test | These spans emit the disposal-failure event and attach ScenarioId/RunId. No test reaches the disposal catch; H7 intentionally terminates during blocked cleanup. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1635 | Program.cs | 148:40-148:56 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1636 | Program.cs | 151:17-151:37 | Statement mutation | <code>&quot;;&quot;</code> | Survived | none / T110 | batch-1/0176-1636 | validated_pass | genuine missing test | This MarkFailed runs after a disposal exception and must override a handler's earlier success. Current tests do not inject a disposal failure after success. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1637 | Program.cs | 155:9-155:29 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1642 | Program.cs | 163:101-163:106 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | none / T106 | batch-1/0177-1642 | validated_pass | equivalent/unreachable with proof | The code path starts at Program.Main(string[] args) => RunAsync(args); Main installs no SynchronizationContext or custom TaskScheduler, and this Generic Host executable runs as a console process. Pinned dotnet/runtime commit 6aa72daa24f66c01c3d7b6a32519e9d21396deec: [Task.cs lines 2451-2453](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2451-L2453) maps ConfigureAwait(bool) to ContinueOnCapturedContext; [lines 2494-2516](https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs#L2494-L2516) only capture a non-base SynchronizationContext or a non-default TaskScheduler when true. Thus false-to-true is equivalent for the normal process entrypoint. This inference does not extend to internal RunAsync calls under a custom context/scheduler. The pin used here is source tag v10.0.13; the runtime source is cached under /workspace/scratch/pinned-sources/task-v10.0.13.cs and its SHA-256 is recorded in runtime_source_evidence. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1659 | Program.cs | 222:37-222:45 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1665 | Program.cs | 227:37-227:45 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1669 | Program.cs | 232:21-232:29 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1671 | Program.cs | 234:13-234:69 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1690 | Program.cs | 257:9-258:107 | Logical mutation | <code>&quot;property.Equals(nameof(VerifierHostOptions.ProcessLifetime), StringComparison.OrdinalIgnoreCase)\n        &amp;&amp; property.Equals(nameof(VerifierHostOptions.CleanupTimeout), StringComparison.OrdinalIgnoreCase)&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1692 | VerifierHostOptions.cs | 96:13-96:25 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T090 | gap-fill/0004-1692 | validated_pass | equivalent/unreachable with proof | The mutated switch pattern is the start-line arm. A nonmatch yields default(VerifierMode), and VerifierMode.StartLine is the enum's zero/default member; the separate return pattern still rejects unknown strings. Thus the out value for the accepted literal remains StartLine. The current malformed-inactive-budget theory also provides positive valid-mode execution for start-line and probe-gate, preserving the distinction between exact accepted modes and rejected strings. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 1699 | VerifierHostOptions.cs | 102:41-102:49 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1702 | VerifierHostOptionsValidator.cs | 13:9-13:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1705 | VerifierHostOptionsValidator.cs | 18:13-18:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1706 | VerifierHostOptionsValidator.cs | 18:24-18:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1708 | VerifierHostOptionsValidator.cs | 22:41-22:62 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1710 | VerifierHostOptionsValidator.cs | 23:45-23:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1712 | VerifierHostOptionsValidator.cs | 24:49-24:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1714 | VerifierHostOptionsValidator.cs | 25:57-25:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1716 | VerifierHostOptionsValidator.cs | 26:53-26:82 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1718 | VerifierHostOptionsValidator.cs | 29:45-29:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1720 | VerifierHostOptionsValidator.cs | 30:46-30:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1723 | VerifierHostOptionsValidator.cs | 36:58-36:88 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1725 | VerifierHostOptionsValidator.cs | 37:56-37:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1727 | VerifierHostOptionsValidator.cs | 38:56-38:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1730 | VerifierHostOptionsValidator.cs | 43:48-43:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1732 | VerifierHostOptionsValidator.cs | 44:54-44:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1734 | VerifierHostOptionsValidator.cs | 45:53-45:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1736 | VerifierHostOptionsValidator.cs | 46:57-46:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1738 | VerifierHostOptionsValidator.cs | 47:55-47:82 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1740 | VerifierHostOptionsValidator.cs | 48:51-48:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1742 | VerifierHostOptionsValidator.cs | 49:64-49:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1744 | VerifierHostOptionsValidator.cs | 50:49-50:70 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1746 | VerifierHostOptionsValidator.cs | 51:59-51:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1748 | VerifierHostOptionsValidator.cs | 52:61-52:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1750 | VerifierHostOptionsValidator.cs | 53:54-53:76 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1752 | VerifierHostOptionsValidator.cs | 54:61-54:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1754 | VerifierHostOptionsValidator.cs | 55:62-55:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1756 | VerifierHostOptionsValidator.cs | 56:56-56:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1758 | VerifierHostOptionsValidator.cs | 57:56-57:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1760 | VerifierHostOptionsValidator.cs | 58:63-58:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1762 | VerifierHostOptionsValidator.cs | 59:59-59:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1764 | VerifierHostOptionsValidator.cs | 61:21-61:102 | Negate expression | <code>&quot;!(StringComparer.Ordinal.Equals(options.VerifierContainer, options.LedgerContainer))&quot;</code> | Survived | none / T107 | gap-fill/0001-1764 | validated_pass | genuine missing test | The negated equality condition reverses the required separation check at VerifierContainer == LedgerContainer. ValidOptions always supplies different literals; no test covers equality rejected and distinct values accepted. The source contract states output belongs in a separate verifier-only container, so this behavior needs an explicit validator test. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1765 | VerifierHostOptionsValidator.cs | 63:21-63:110 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1766 | VerifierHostOptionsValidator.cs | 63:32-63:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1768 | VerifierHostOptionsValidator.cs | 69:48-69:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1770 | VerifierHostOptionsValidator.cs | 70:53-70:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1772 | VerifierHostOptionsValidator.cs | 71:51-71:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1774 | VerifierHostOptionsValidator.cs | 72:59-72:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1776 | VerifierHostOptionsValidator.cs | 73:61-73:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1778 | VerifierHostOptionsValidator.cs | 74:56-74:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1782 | VerifierHostOptionsValidator.cs | 77:62-77:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1790 | VerifierHostOptionsValidator.cs | 90:46-90:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1792 | VerifierHostOptionsValidator.cs | 91:36-91:52 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1794 | VerifierHostOptionsValidator.cs | 92:41-92:62 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1796 | VerifierHostOptionsValidator.cs | 93:49-93:78 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1798 | VerifierHostOptionsValidator.cs | 94:39-94:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1800 | VerifierHostOptionsValidator.cs | 95:47-95:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1802 | VerifierHostOptionsValidator.cs | 96:44-96:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1804 | VerifierHostOptionsValidator.cs | 97:52-97:77 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1806 | VerifierHostOptionsValidator.cs | 98:48-98:76 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1808 | VerifierHostOptionsValidator.cs | 99:39-99:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1810 | VerifierHostOptionsValidator.cs | 100:55-100:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1812 | VerifierHostOptionsValidator.cs | 101:53-101:86 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1814 | VerifierHostOptionsValidator.cs | 102:43-102:66 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1816 | VerifierHostOptionsValidator.cs | 103:46-103:72 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1818 | VerifierHostOptionsValidator.cs | 104:47-104:74 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1820 | VerifierHostOptionsValidator.cs | 105:40-105:60 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1822 | VerifierHostOptionsValidator.cs | 106:44-106:68 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1827 | VerifierHostOptionsValidator.cs | 113:24-113:62 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1830 | VerifierHostOptionsValidator.cs | 117:55-117:102 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1832 | VerifierHostOptionsValidator.cs | 118:49-118:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1834 | VerifierHostOptionsValidator.cs | 119:53-119:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1836 | VerifierHostOptionsValidator.cs | 120:49-120:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1841 | VerifierHostOptionsValidator.cs | 127:24-127:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1850 | VerifierHostOptionsValidator.cs | 136:17-136:78 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1851 | VerifierHostOptionsValidator.cs | 136:28-136:76 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1852 | VerifierHostOptionsValidator.cs | 137:17-137:26 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1854 | VerifierHostOptionsValidator.cs | 140:44-140:90 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1856 | VerifierHostOptionsValidator.cs | 141:49-141:100 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1858 | VerifierHostOptionsValidator.cs | 142:39-142:80 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1860 | VerifierHostOptionsValidator.cs | 143:44-143:90 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1862 | VerifierHostOptionsValidator.cs | 144:52-144:106 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1864 | VerifierHostOptionsValidator.cs | 145:42-145:86 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1874 | VerifierHostOptionsValidator.cs | 161:13-161:96 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1875 | VerifierHostOptionsValidator.cs | 161:24-161:94 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1876 | VerifierHostOptionsValidator.cs | 162:13-162:20 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1882 | VerifierHostOptionsValidator.cs | 167:24-167:121 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1898 | VerifierHostOptionsValidator.cs | 198:24-198:99 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1905 | VerifierHostOptionsValidator.cs | 206:34-206:68 | Equality mutation | <code>&quot;value.Ticks &lt; MaximumTimeoutTicks&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1914 | VerifierHostOptionsValidator.cs | 212:24-212:61 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1924 | VerifierHostOptionsValidator.cs | 223:24-223:66 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1929 | VerifierHostOptionsValidator.cs | 231:24-231:46 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1934 | VerifierHostOptionsValidator.cs | 239:24-239:58 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1940 | VerifierWorker.cs | 47:31-47:56 | Linq method mutation (SingleOrDefault() to Single()) | <code>&quot;_handlers.Single&quot;</code> | Survived | current / T087 | gap-fill/0002-1940 | validated_pass | genuine missing test | SingleOrDefault preserves the missing-handler case as a null result and reports mode unavailable; Single turns that same case into a caught exception and reports execution failure. H5 asserts only nonzero/no output, so the externally visible distinction is unasserted; capture the unavailable diagnostic for each mode. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 1945 | VerifierWorker.cs | 50:78-50:83 | Boolean mutation | <code>&quot;true&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1946 | VerifierWorker.cs | 51:21-51:66 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1949 | VerifierWorker.cs | 56:21-60:47 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1951 | VerifierWorker.cs | 67:13-71:39 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1953 | VerifierWorker.cs | 76:13-81:39 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1957 | VerifierWorker.cs | 124:9-124:57 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1958 | VerifierWorker.cs | 125:9-125:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1959 | VerifierWorker.cs | 129:21-129:57 | Null coalescing mutation (remove left) | <code>&quot;Environment.Exit&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1963 | VerifierWorker.cs | 136:13-136:94 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1964 | VerifierWorker.cs | 136:49-136:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 1966 | VerifierWorker.cs | 148:9-148:27 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2005 | VerifyRun/Input/ArtifactIndexReader.cs | 46:56-46:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2068 | VerifyRun/Input/ArtifactIndexReader.cs | 80:77-80:114 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2069 | VerifyRun/Input/ArtifactIndexReader.cs | 80:81-80:99 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2070 | VerifyRun/Input/ArtifactIndexReader.cs | 80:101-80:112 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2074 | VerifyRun/Input/ArtifactIndexReader.cs | 81:63-81:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2082 | VerifyRun/Input/ArtifactIndexReader.cs | 88:63-88:92 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2083 | VerifyRun/Input/ArtifactIndexReader.cs | 88:67-88:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2084 | VerifyRun/Input/ArtifactIndexReader.cs | 88:93-88:102 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2102 | VerifyRun/Input/ArtifactIndexReader.cs | 101:76-101:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2103 | VerifyRun/Input/ArtifactIndexReader.cs | 101:80-101:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2107 | VerifyRun/Input/ArtifactIndexReader.cs | 102:64-102:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2111 | VerifyRun/Input/ArtifactIndexReader.cs | 111:76-111:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2112 | VerifyRun/Input/ArtifactIndexReader.cs | 111:80-111:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2116 | VerifyRun/Input/ArtifactIndexReader.cs | 112:75-112:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2123 | VerifyRun/Input/ArtifactIndexReader.cs | 113:73-113:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2127 | VerifyRun/Input/ArtifactIndexReader.cs | 119:76-119:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2128 | VerifyRun/Input/ArtifactIndexReader.cs | 119:80-119:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2134 | VerifyRun/Input/ArtifactIndexReader.cs | 120:110-120:139 | Statement mutation | <code>&quot;;&quot;</code> | Survived | current / T093 | gap-fill/0005-2134 | complete_test_failures_cause_review | preview-runner discrepancy | The `RequiredPropertyCases` row for `index` / `schema_version` replaces the present scalar with `{}`. That reaches the Int32 wrong-kind branch while leaving every required property present, so this `Add("wrong_json_kind", path)` is the only problem for `schema_version`. If the statement is removed, the reader returns an execution failure with an empty Problems list because its parsed value is null; `ParseResult.IsSuccess` is defined by `Problems.Count == 0`, so `ReadIndexProblems`'s `Assert.False(result.IsSuccess)` should fail first. If execution reached the enclosing `Assert.Contains`, it would also lack the owner-null plus field `schema_version` finding. The normal-MTP replay confirmed the first assertion failure; the result is recorded separately from the unchanged Stryker Survived status. | Normal MTP completed 1044 cases with 1043 passed and one Xunit.Sdk.FalseException at ReadIndexProblems line 911 for index/schema_version wrong kind. Removing Add leaves an empty problem list and IsSuccess true. Stryker recorded Survived. Hari accepted this difference for PR 3 on 2026-10-18 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/84#issuecomment-6248723728). |
| 2135 | VerifyRun/Input/ArtifactIndexReader.cs | 120:114-120:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2139 | VerifyRun/Input/ArtifactIndexReader.cs | 126:76-126:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2140 | VerifyRun/Input/ArtifactIndexReader.cs | 126:80-126:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2147 | VerifyRun/Input/ArtifactIndexReader.cs | 127:115-127:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2151 | VerifyRun/Input/ArtifactIndexReader.cs | 133:76-133:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2152 | VerifyRun/Input/ArtifactIndexReader.cs | 133:80-133:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2157 | VerifyRun/Input/ArtifactIndexReader.cs | 134:91-134:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2176 | VerifyRun/Input/CapturedEvidenceReader.cs | 28:40-28:77 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2177 | VerifyRun/Input/CapturedEvidenceReader.cs | 28:78-28:87 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2204 | VerifyRun/Input/CapturedEvidenceReader.cs | 56:40-56:77 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2205 | VerifyRun/Input/CapturedEvidenceReader.cs | 56:78-56:87 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2284 | VerifyRun/Input/CapturedEvidenceReader.cs | 117:59-117:88 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2285 | VerifyRun/Input/CapturedEvidenceReader.cs | 117:63-117:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | none / T105 | gap-fill/0006-2285 | validated_pass | genuine missing test | This string mutation leaves the branch and its control flow intact but changes the diagnostic-code literal `"expected_object"` to an empty string. ReadJsonLines checks syntax but permits this root through; current lifecycle rows are object-shaped and no test asserts expected_object at `$`. The cited `NO COVERING CASE: a syntactically valid lifecycle JSONL row has a non-object root such as []` does not supply that branch input, so it cannot establish the changed diagnostic literal or path. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 2286 | VerifyRun/Input/CapturedEvidenceReader.cs | 117:82-117:85 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2294 | VerifyRun/Input/CapturedEvidenceReader.cs | 122:13-122:56 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2295 | VerifyRun/Input/CapturedEvidenceReader.cs | 122:17-122:40 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2296 | VerifyRun/Input/CapturedEvidenceReader.cs | 122:42-122:54 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2855 | VerifyRun/Input/CapturedEvidenceReader.cs | 361:76-361:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2856 | VerifyRun/Input/CapturedEvidenceReader.cs | 361:80-361:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2860 | VerifyRun/Input/CapturedEvidenceReader.cs | 362:63-362:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2867 | VerifyRun/Input/CapturedEvidenceReader.cs | 369:63-369:96 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2868 | VerifyRun/Input/CapturedEvidenceReader.cs | 369:67-369:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2869 | VerifyRun/Input/CapturedEvidenceReader.cs | 369:97-369:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2875 | VerifyRun/Input/CapturedEvidenceReader.cs | 378:74-378:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2876 | VerifyRun/Input/CapturedEvidenceReader.cs | 378:78-378:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2880 | VerifyRun/Input/CapturedEvidenceReader.cs | 379:62-379:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2887 | VerifyRun/Input/CapturedEvidenceReader.cs | 386:69-386:116 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2888 | VerifyRun/Input/CapturedEvidenceReader.cs | 386:73-386:90 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2890 | VerifyRun/Input/CapturedEvidenceReader.cs | 386:99-386:102 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2891 | VerifyRun/Input/CapturedEvidenceReader.cs | 386:117-386:126 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2899 | VerifyRun/Input/CapturedEvidenceReader.cs | 395:76-395:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2900 | VerifyRun/Input/CapturedEvidenceReader.cs | 395:80-395:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2904 | VerifyRun/Input/CapturedEvidenceReader.cs | 396:63-396:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2908 | VerifyRun/Input/CapturedEvidenceReader.cs | 402:70-402:113 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2909 | VerifyRun/Input/CapturedEvidenceReader.cs | 402:74-402:91 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2910 | VerifyRun/Input/CapturedEvidenceReader.cs | 402:93-402:111 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2918 | VerifyRun/Input/CapturedEvidenceReader.cs | 415:17-415:47 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2919 | VerifyRun/Input/CapturedEvidenceReader.cs | 415:21-415:39 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2924 | VerifyRun/Input/CapturedEvidenceReader.cs | 419:64-419:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2965 | VerifyRun/Input/CapturedEvidenceReader.cs | 440:76-440:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2966 | VerifyRun/Input/CapturedEvidenceReader.cs | 440:80-440:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2970 | VerifyRun/Input/CapturedEvidenceReader.cs | 441:64-441:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2977 | VerifyRun/Input/CapturedEvidenceReader.cs | 456:80-456:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2981 | VerifyRun/Input/CapturedEvidenceReader.cs | 457:75-457:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2985 | VerifyRun/Input/CapturedEvidenceReader.cs | 463:76-463:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2986 | VerifyRun/Input/CapturedEvidenceReader.cs | 463:80-463:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2993 | VerifyRun/Input/CapturedEvidenceReader.cs | 464:114-464:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2997 | VerifyRun/Input/CapturedEvidenceReader.cs | 470:76-470:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 2998 | VerifyRun/Input/CapturedEvidenceReader.cs | 470:80-470:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3005 | VerifyRun/Input/CapturedEvidenceReader.cs | 471:115-471:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3009 | VerifyRun/Input/CapturedEvidenceReader.cs | 477:76-477:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3010 | VerifyRun/Input/CapturedEvidenceReader.cs | 477:80-477:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3015 | VerifyRun/Input/CapturedEvidenceReader.cs | 478:91-478:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3019 | VerifyRun/Input/CapturedEvidenceReader.cs | 484:76-484:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3020 | VerifyRun/Input/CapturedEvidenceReader.cs | 484:80-484:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3024 | VerifyRun/Input/CapturedEvidenceReader.cs | 487:17-487:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3028 | VerifyRun/Input/CapturedEvidenceReader.cs | 492:76-492:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3029 | VerifyRun/Input/CapturedEvidenceReader.cs | 492:80-492:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3033 | VerifyRun/Input/CapturedEvidenceReader.cs | 494:17-494:56 | Equality mutation | <code>&quot;value.ValueKind != JsonValueKind.Number&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3035 | VerifyRun/Input/CapturedEvidenceReader.cs | 495:17-495:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3040 | VerifyRun/Input/CapturedEvidenceReader.cs | 502:36-502:66 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3041 | VerifyRun/Input/CapturedEvidenceReader.cs | 502:40-502:58 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3047 | VerifyRun/Input/CapturedEvidenceReader.cs | 507:17-507:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3054 | VerifyRun/Input/CapturedEvidenceReader.cs | 510:87-510:115 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3100 | VerifyRun/Input/LedgerReader.cs | 45:17-45:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3101 | VerifyRun/Input/LedgerReader.cs | 46:17-46:26 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3127 | VerifyRun/Input/LedgerReader.cs | 79:21-79:49 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T094 | gap-fill/0007-3127 | validated_pass | genuine missing test | The unsupported-schema/value case checks this owner and field but not Code equals '"unsupported_schema_version"'; replacing only the diagnostic code leaves its current assertions intact. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 3233 | VerifyRun/Input/LedgerReader.cs | 138:21-138:100 | Conditional (true) mutation | <code>&quot;(true?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3234 | VerifyRun/Input/LedgerReader.cs | 138:21-138:100 | Conditional (false) mutation | <code>&quot;(false?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3235 | VerifyRun/Input/LedgerReader.cs | 138:62-138:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3236 | VerifyRun/Input/LedgerReader.cs | 138:82-138:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3240 | VerifyRun/Input/LedgerReader.cs | 147:76-147:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3241 | VerifyRun/Input/LedgerReader.cs | 147:80-147:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3245 | VerifyRun/Input/LedgerReader.cs | 148:75-148:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3253 | VerifyRun/Input/LedgerReader.cs | 155:73-155:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3257 | VerifyRun/Input/LedgerReader.cs | 161:76-161:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3258 | VerifyRun/Input/LedgerReader.cs | 161:80-161:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3265 | VerifyRun/Input/LedgerReader.cs | 162:114-162:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3269 | VerifyRun/Input/LedgerReader.cs | 168:76-168:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3270 | VerifyRun/Input/LedgerReader.cs | 168:80-168:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3277 | VerifyRun/Input/LedgerReader.cs | 169:115-169:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3281 | VerifyRun/Input/LedgerReader.cs | 175:76-175:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3282 | VerifyRun/Input/LedgerReader.cs | 175:80-175:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3287 | VerifyRun/Input/LedgerReader.cs | 176:91-176:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3293 | VerifyRun/Input/LedgerReader.cs | 185:17-185:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3301 | VerifyRun/Input/LedgerReader.cs | 194:17-194:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3307 | VerifyRun/Input/LedgerReader.cs | 203:17-203:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3342 | VerifyRun/Input/LedgerReader.cs | 232:55-232:131 | Conditional (true) mutation | <code>&quot;(true?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3343 | VerifyRun/Input/LedgerReader.cs | 232:55-232:131 | Conditional (false) mutation | <code>&quot;(false?\&quot;wrong_json_kind\&quot; :\&quot;missing_property\&quot;)&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3344 | VerifyRun/Input/LedgerReader.cs | 232:93-232:110 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3345 | VerifyRun/Input/LedgerReader.cs | 232:113-232:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3402 | VerifyRun/Input/ManifestReader.cs | 50:56-50:84 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | current / T094 | gap-fill/0008-3402 | validated_pass | genuine missing test | The unsupported-schema/value case checks this owner and field but not Code equals '"unsupported_schema_version"'; replacing only the diagnostic code leaves its current assertions intact. | Normal MTP completed all 1044 tests successfully; the source review identifies the observable behavior or diagnostic detail not asserted by the current suite. This records a coverage gap, not a new required check. |
| 3496 | VerifyRun/Input/ManifestReader.cs | 94:76-94:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3497 | VerifyRun/Input/ManifestReader.cs | 94:80-94:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3501 | VerifyRun/Input/ManifestReader.cs | 95:63-95:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3506 | VerifyRun/Input/ManifestReader.cs | 97:55-97:114 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3513 | VerifyRun/Input/ManifestReader.cs | 104:64-104:97 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3514 | VerifyRun/Input/ManifestReader.cs | 104:68-104:85 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3515 | VerifyRun/Input/ManifestReader.cs | 104:98-104:107 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3562 | VerifyRun/Input/ManifestReader.cs | 128:76-128:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3563 | VerifyRun/Input/ManifestReader.cs | 128:80-128:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3567 | VerifyRun/Input/ManifestReader.cs | 129:63-129:80 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3571 | VerifyRun/Input/ManifestReader.cs | 135:71-135:114 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3572 | VerifyRun/Input/ManifestReader.cs | 135:75-135:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3573 | VerifyRun/Input/ManifestReader.cs | 135:94-135:112 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3580 | VerifyRun/Input/ManifestReader.cs | 144:74-144:104 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3581 | VerifyRun/Input/ManifestReader.cs | 144:78-144:96 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3585 | VerifyRun/Input/ManifestReader.cs | 145:62-145:79 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3591 | VerifyRun/Input/ManifestReader.cs | 152:76-152:122 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3592 | VerifyRun/Input/ManifestReader.cs | 152:80-152:97 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3593 | VerifyRun/Input/ManifestReader.cs | 152:99-152:120 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3598 | VerifyRun/Input/ManifestReader.cs | 160:76-160:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3599 | VerifyRun/Input/ManifestReader.cs | 160:80-160:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3603 | VerifyRun/Input/ManifestReader.cs | 161:64-161:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3607 | VerifyRun/Input/ManifestReader.cs | 170:76-170:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3608 | VerifyRun/Input/ManifestReader.cs | 170:80-170:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3612 | VerifyRun/Input/ManifestReader.cs | 171:75-171:92 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3620 | VerifyRun/Input/ManifestReader.cs | 178:73-178:100 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3624 | VerifyRun/Input/ManifestReader.cs | 184:76-184:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3625 | VerifyRun/Input/ManifestReader.cs | 184:80-184:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3632 | VerifyRun/Input/ManifestReader.cs | 185:114-185:131 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3636 | VerifyRun/Input/ManifestReader.cs | 191:76-191:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3637 | VerifyRun/Input/ManifestReader.cs | 191:80-191:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3644 | VerifyRun/Input/ManifestReader.cs | 192:115-192:132 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3648 | VerifyRun/Input/ManifestReader.cs | 198:76-198:106 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3649 | VerifyRun/Input/ManifestReader.cs | 198:80-198:98 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3654 | VerifyRun/Input/ManifestReader.cs | 199:91-199:108 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3660 | VerifyRun/Input/ManifestReader.cs | 207:17-207:56 | Equality mutation | <code>&quot;value.ValueKind != JsonValueKind.Number&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3662 | VerifyRun/Input/ManifestReader.cs | 208:17-208:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3667 | VerifyRun/Input/NativeStatisticsReader.cs | 19:13-19:115 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3668 | VerifyRun/Input/NativeStatisticsReader.cs | 19:51-19:113 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3680 | VerifyRun/Input/NativeStatisticsReader.cs | 42:17-42:51 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3681 | VerifyRun/Input/NativeStatisticsReader.cs | 42:21-42:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3682 | VerifyRun/Input/NativeStatisticsReader.cs | 42:40-42:43 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3706 | VerifyRun/Input/NativeStatisticsReader.cs | 55:13-57:14 | Block removal mutation | <code>&quot;{}&quot;</code> | Survived | current / T091 | gap-fill/0003-3706 | validated_pass | equivalent/unreachable with proof | NativeStatisticsReader.Read independently rejects any parser.Problems; removing this inner guard cannot yield a success while check-20 problems exist. Recoverable check-21 absences are stored in MissingFields and are not blocked by this guard. | Normal MTP completed all 1044 tests successfully; root reviewed the specific source proof and its stated call-path limits. A passing suite alone is not the equivalence proof. |
| 3712 | VerifyRun/Input/NativeStatisticsReader.cs | 66:21-66:39 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3717 | VerifyRun/Input/NativeStatisticsReader.cs | 71:21-71:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3725 | VerifyRun/Input/NativeStatisticsReader.cs | 78:55-78:118 | String mutation | <code>&quot;$\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3730 | VerifyRun/Input/NativeStatisticsReader.cs | 86:21-86:76 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3731 | VerifyRun/Input/NativeStatisticsReader.cs | 86:25-86:42 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3732 | VerifyRun/Input/NativeStatisticsReader.cs | 86:44-86:54 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3733 | VerifyRun/Input/NativeStatisticsReader.cs | 87:21-87:30 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3784 | VerifyRun/Input/NativeStatisticsReader.cs | 123:17-123:52 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3785 | VerifyRun/Input/NativeStatisticsReader.cs | 123:21-123:38 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3809 | VerifyRun/Input/NativeStatisticsReader.cs | 144:13-144:48 | Statement mutation | <code>&quot;;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3810 | VerifyRun/Input/NativeStatisticsReader.cs | 144:17-144:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3818 | VerifyRun/Input/NativeStatisticsReader.cs | 153:63-153:81 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3823 | VerifyRun/Input/NativeStatisticsReader.cs | 159:17-159:34 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
| 3824 | VerifyRun/Input/NativeStatisticsReader.cs | 164:90-164:114 | String mutation | <code>&quot;\&quot;\&quot;&quot;</code> | Survived | not reviewed / not reviewed | not reviewed | not reviewed | not reviewed | not reviewed | not reviewed |
