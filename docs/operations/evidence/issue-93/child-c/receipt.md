# Verifier mutation closure receipt

This record retains the completed fresh full Stryker.NET invocation for #103 and its exact mapping of the original assignment. Execution and identity mapping are complete; residual survivors, proof qualifications and runner limitations still prevent a claim that #93 is closed.

## Authority and tested inputs

The [child adoption decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/103#issuecomment-6307733141) adopts the [parent closure decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/93#issuecomment-6306618757). [Plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/103#issuecomment-6307793865), updated `2026-10-21T04:20:31Z`, passed its [fresh independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/103#issuecomment-6307992377) under factory.md standing approval, after [Hari's correction decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/103#issuecomment-6307924821). The preceding [child A receipt](../child-a/receipt.md), [child B receipt](../child-b/receipt.md) and [child B proofs](../child-b/proofs.md) retain their own evidence unchanged.

| Input | Measured value | Evidence |
| --- | --- | --- |
| Tested main commit | `e59271330e6ed3f727c0a0a3268ec9da77718bb5` | [source manifest](raw/mutation-full/tested-source.json) |
| Tested tree | `deebcddffb10e109360bbc2012abcb0117fd903a` | [source manifest](raw/mutation-full/tested-source.json) |
| Input-manifest SHA-256 | `2b3929392321343983341803ed6e1f98eab2588bb154959dca89bb9e689cd666` | [source manifest](raw/mutation-full/tested-source.json) |
| Stryker config SHA-256 | `d6b0b494cecd06fccc04af990e43f02c9aafe36e23759804d6628a9156c6361b` | [source manifest](raw/mutation-full/tested-source.json), [configuration](../../../../../tests/Parchwell.Verifier.Tests/stryker-config.json) |
| SDK / Stryker | `10.0.402` / `5.0.0` | [SDK output](raw/setup/sdk-version.txt), [tools](raw/setup/tools.txt), [run log](raw/mutation-full/command.log.gz) |
| Installed host runtimes | Microsoft.NETCore.App and Microsoft.AspNetCore.App `10.0.13` | [runtime listing](raw/setup/runtimes.txt), [host information](raw/setup/dotnet-info.txt) |
| Source inputs unchanged across the run | `true` | [controller](raw/mutation-full/controller.json) |

The measurement ran with HEAD still at the tested main commit. The new documentation and mapper were uncommitted measurement support; no production, .NET test, dependency, project-selection, scenario, expected-outcome or workflow change was made. The manifest records actual production/test/config hashes rather than claiming that a later documentation commit was the measured input. Configuration remained Release/net10.0, the existing mutation-only selection and break 60; no baseline result or source-only incremental selection was supplied.

The [ordinary input comparison](raw/validation/ordinary-input-comparison.json) matches 293 tracked inputs against child B's successful suite, with no addition, removal or mismatch. That includes the plan's core input set plus the unchanged `.editorconfig` and solution file. This is retained ordinary evidence for identical input bytes, not a new ordinary-suite execution.

| Retained child-B ordinary assembly | Passed | Failed | Skipped | Errors |
| --- | --- | --- | --- | --- |
| `Parchwell.ArchitectureTests` | 8 | 0 | 0 | 0 |
| `Parchwell.Kafka.Tests` | 306 | 0 | 0 | 0 |
| `Parchwell.LoadGen.Tests` | 262 | 0 | 0 | 0 |
| `Parchwell.Scenarios.Tests` | 94 | 0 | 0 | 0 |
| `Parchwell.Verifier.Tests` | 2167 | 0 | 0 | 0 |

## Execution

The only mutation invocation used the unchanged [existing controller](../../issue-92-pr6b/controller.py) with a dedicated ledger. It exited 0 by `command-exit` after 6398.475696 s, within its 7200 s cap; termination was absent. The controller observed 5685 generated mutants against its 10000 cap. The logged created-count boundary, not preallocation prevention, is the controller's limit mechanism. No second mutation invocation, targeted run, replay, discovery invocation or independent baseline execution occurred.

The [launch receipt](raw/mutation-launch.json) records the exact controller command, working directory and SDK environment selection. The [controller receipt](raw/mutation-full/controller.json) records the exact child command and environment:

```text
dotnet stryker --config-file stryker-config.json --output /workspace/eventhubs-kafka-dotnet/.artifacts/issue-103/mutation-full
```

Child cwd was `/workspace/eventhubs-kafka-dotnet/tests/Parchwell.Verifier.Tests`. Child overrides were `ParchwellVerifierMutationOnly=true` and `RestoreSources=https://api.nuget.org/v3/index.json`; the [launch receipt](raw/mutation-launch.json) records the inherited SDK-path selection. Both output paths were fresh, and the [invocation ledger](raw/controller-invocations.json) retains the single reservation.

| Invocation | Exit | Measured elapsed s | Evidence |
| --- | --- | --- | --- |
| SDK preparation | 0 | 9.903487 | [setup receipt](raw/setup/setup.json) |
| Existing tool restore | 0 | 5.036496 | [restore receipt](raw/setup/tool-restore.json) |
| Full mutation | 0 | 6398.475696 | [controller](raw/mutation-full/controller.json) |
| Seven synthetic mapper tests, invocation 1 | 0 | 0.431945 | [receipt](raw/validation/helper-tests-1.json), [log](raw/validation/helper-tests-1.log) |
| Sole report extraction | 0 | 1.076410 | [receipt](raw/validation/extraction.json), [log](raw/validation/extraction.log) |

The fresh run log reports 1708 tests found, an initial test run started, and per-test coverage capture completed for 1708 tests. It does not separately emit the initial phase's passed/failed/skipped counts or an explicit completion outcome. Those values are unavailable, not inferred from the report's test population; no extra control execution was authorized. Exit 0 and the controller's complete-execution flag are retained alongside that control-evidence limitation.

| Population or result | Measured value |
| --- | --- |
| Generated mutants, logged by controller | 5685 |
| Serialized report records | 1700 |
| Raw `CompileError` | 153 |
| Raw `Ignored` | 613 |
| Raw `Killed` | 919 |
| Raw `Survived` | 12 |
| Raw `Timeout` | 3 |
| Score numerator / denominator | 922 / 934 |
| Display-only score | 98.72% |
| Uncompressed report SHA-256 | `e6d0e74c18d6004f5ad8a4bd5e801b668be4385629de3db3416f9f6c860d7ab5` |
| Stored report gzip SHA-256 | `4c50fecfb1ed447a4a3b9fe871fdc57d2e15c6bd06539146fa7ec2e2aa8c4e84` |
| Uncompressed / stored report bytes | 61624434 / 33387324 |

The [retained report](raw/mutation-full/mutation-report.json.gz) supplies raw statuses. [Closure JSON](raw/closure.json) preserves full spans/replacements and report/source hashes. The score numerator is Killed plus Timeout; the denominator adds Survived and NoCoverage. CompileError, Ignored and proof annotations are never rewritten or used to adjust that denominator. Generated and serialized counts are different populations.

## Assigned variants

The sole extraction matched 228 original variants across 136 frozen line keys uniquely against verified full source texts. The assignment retains 109 missing-test lines / 187 variants and 27 unresolved lines / 41 variants. Raw assigned outcomes are 219 Killed and 9 Survived. No assigned variant is missing or ambiguous.

The original IDs happen to be unchanged in this report, but matching used file, complete span, mutator, replacement and identical source text. It did not use ID equality. Proof evidence is separate from observed kills. The excluded OutcomeClassifier:38 / 5025 proof remains outside this assignment.

| Original frozen line key | Original ID -> new report ID: raw status |
| --- | --- |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:ProbeGate/ProbeGateEvaluator.cs:39` | 1883 -> 1883: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/A01/A01EvidenceRecorder.cs:22` | 2657 -> 2657: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:28` | 4591 -> 4591: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:33` | 4595 -> 4595: Killed; 4596 -> 4596: Killed; 4597 -> 4597: Killed; 4598 -> 4598: Killed; 4599 -> 4599: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:36` | 4608 -> 4608: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:39` | 4612 -> 4612: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:41` | 4618 -> 4618: Killed; 4619 -> 4619: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:47` | 4628 -> 4628: Killed; 4629 -> 4629: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:48` | 4631 -> 4631: Killed; 4632 -> 4632: Killed; 4633 -> 4633: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:52` | 4640 -> 4640: Killed; 4641 -> 4641: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:54` | 4648 -> 4648: Killed; 4649 -> 4649: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:55` | 4652 -> 4652: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:59` | 4660 -> 4660: Killed; 4661 -> 4661: Killed; 4662 -> 4662: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:60` | 4663 -> 4663: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:66` | 4666 -> 4666: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:67` | 4668 -> 4668: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:68` | 4670 -> 4670: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:69` | 4672 -> 4672: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:70` | 4674 -> 4674: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:71` | 4676 -> 4676: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:94` | 4689 -> 4689: Killed; 4690 -> 4690: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:100` | 4697 -> 4697: Killed; 4698 -> 4698: Killed; 4699 -> 4699: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:110` | 4708 -> 4708: Killed; 4709 -> 4709: Killed; 4710 -> 4710: Killed; 4711 -> 4711: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:116` | 4719 -> 4719: Killed; 4720 -> 4720: Killed; 4721 -> 4721: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:124` | 4728 -> 4728: Killed; 4729 -> 4729: Killed; 4730 -> 4730: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:131` | 4737 -> 4737: Killed; 4738 -> 4738: Killed; 4739 -> 4739: Killed; 4740 -> 4740: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:135` | 4744 -> 4744: Killed; 4745 -> 4745: Killed; 4746 -> 4746: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:137` | 4749 -> 4749: Killed; 4750 -> 4750: Killed; 4751 -> 4751: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:151` | 4763 -> 4763: Killed; 4764 -> 4764: Killed; 4765 -> 4765: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:163` | 4776 -> 4776: Killed; 4777 -> 4777: Killed; 4778 -> 4778: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:164` | 4781 -> 4781: Killed; 4782 -> 4782: Killed; 4783 -> 4783: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:165` | 4786 -> 4786: Killed; 4787 -> 4787: Killed; 4788 -> 4788: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:166` | 4791 -> 4791: Killed; 4792 -> 4792: Killed; 4793 -> 4793: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:169` | 4803 -> 4803: Killed; 4804 -> 4804: Killed; 4805 -> 4805: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:173` | 4812 -> 4812: Killed; 4813 -> 4813: Killed; 4814 -> 4814: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:175` | 4820 -> 4820: Killed; 4821 -> 4821: Killed; 4822 -> 4822: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/BookkeepingChecks.cs:182` | 4824 -> 4824: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:18` | 4851 -> 4851: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:29` | 4856 -> 4856: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:34` | 4866 -> 4866: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:43` | 4874 -> 4874: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:45` | 4875 -> 4875: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:50` | 4881 -> 4881: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:55` | 4885 -> 4885: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:61` | 4889 -> 4889: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:69` | 4897 -> 4897: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/ConfigurationFidelityCheck.cs:70` | 4898 -> 4898: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:12` | 4899 -> 4899: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:13` | 4900 -> 4900: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:26` | 4914 -> 4914: Killed; 4915 -> 4915: Killed; 4916 -> 4916: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:29` | 4922 -> 4922: Killed; 4923 -> 4923: Killed; 4924 -> 4924: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:35` | 4928 -> 4928: Killed; 4929 -> 4929: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:39` | 4937 -> 4937: Killed; 4938 -> 4938: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:46` | 4949 -> 4949: Killed; 4951 -> 4951: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:48` | 4956 -> 4956: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:49` | 4957 -> 4957: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:53` | 4960 -> 4960: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:54` | 4962 -> 4962: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:55` | 4964 -> 4964: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:56` | 4966 -> 4966: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:62` | 4972 -> 4972: Killed; 4973 -> 4973: Killed; 4974 -> 4974: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:71` | 4983 -> 4983: Killed; 4984 -> 4984: Killed; 4985 -> 4985: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:77` | 4988 -> 4988: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/LateWriteChecks.cs:84` | 4992 -> 4992: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/OutcomeClassifier.cs:35` | 5014 -> 5014: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/RunJudgement.cs:67` | 5042 -> 5042: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/RunJudgement.cs:83` | 5064 -> 5064: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/RunJudgement.cs:88` | 5076 -> 5076: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/RunJudgement.cs:107` | 5095 -> 5095: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/RunJudgement.cs:117` | 5102 -> 5102: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:22` | 5121 -> 5121: Killed; 5122 -> 5122: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:27` | 5129 -> 5129: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:35` | 5135 -> 5135: Killed; 5136 -> 5136: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:38` | 5140 -> 5140: Killed; 5141 -> 5141: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:40` | 5144 -> 5144: Killed; 5145 -> 5145: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:43` | 5146 -> 5146: Killed; 5147 -> 5147: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:55` | 5157 -> 5157: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:61` | 5167 -> 5167: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:63` | 5169 -> 5169: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:73` | 5180 -> 5180: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:74` | 5181 -> 5181: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:83` | 5191 -> 5191: Killed; 5192 -> 5192: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:89` | 5201 -> 5201: Killed; 5202 -> 5202: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:90` | 5203 -> 5203: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:92` | 5204 -> 5204: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:98` | 5212 -> 5212: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:102` | 5216 -> 5216: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:108` | 5219 -> 5219: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:109` | 5220 -> 5220: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:110` | 5221 -> 5221: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:118` | 5224 -> 5224: Killed; 5225 -> 5225: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:119` | 5226 -> 5226: Killed; 5227 -> 5227: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:120` | 5229 -> 5229: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:121` | 5230 -> 5230: Killed; 5231 -> 5231: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:124` | 5233 -> 5233: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:125` | 5236 -> 5236: Killed; 5237 -> 5237: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:127` | 5243 -> 5243: Killed; 5244 -> 5244: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:130` | 5245 -> 5245: Killed; 5246 -> 5246: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:131` | 5248 -> 5248: Killed; 5249 -> 5249: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:136` | 5260 -> 5260: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:139` | 5262 -> 5262: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:143` | 5267 -> 5267: Killed; 5268 -> 5268: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:147` | 5276 -> 5276: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:149` | 5282 -> 5282: Killed; 5283 -> 5283: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:156` | 5295 -> 5295: Killed; 5296 -> 5296: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:157` | 5297 -> 5297: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:165` | 5304 -> 5304: Killed; 5306 -> 5306: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:167` | 5307 -> 5307: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:174` | 5315 -> 5315: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:188` | 5330 -> 5330: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:191` | 5338 -> 5338: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:194` | 5343 -> 5343: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:204` | 5350 -> 5350: Killed; 5351 -> 5351: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:205` | 5352 -> 5352: Killed; 5353 -> 5353: Killed; 5354 -> 5354: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:210` | 5361 -> 5361: Killed; 5362 -> 5362: Killed; 5363 -> 5363: Killed; 5364 -> 5364: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:211` | 5366 -> 5366: Killed; 5367 -> 5367: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:216` | 5369 -> 5369: Killed; 5372 -> 5372: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:217` | 5373 -> 5373: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:218` | 5379 -> 5379: Killed; 5380 -> 5380: Killed; 5381 -> 5381: Killed; 5382 -> 5382: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:224` | 5385 -> 5385: Killed; 5386 -> 5386: Killed; 5387 -> 5387: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Judgement/StatisticsChecks.cs:233` | 5388 -> 5388: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/ConsumedRecordBudget.cs:35` | 5523 -> 5523: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/ConsumedRecordBudget.cs:45` | 5530 -> 5530: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:11` | 5531 -> 5531: Survived; 5532 -> 5532: Survived |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:26` | 5544 -> 5544: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:36` | 5552 -> 5552: Killed; 5553 -> 5553: Killed; 5554 -> 5554: Killed; 5555 -> 5555: Killed; 5556 -> 5556: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:46` | 5561 -> 5561: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:52` | 5565 -> 5565: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/MessageIdentityReader.cs:57` | 5566 -> 5566: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/RunWindow.cs:22` | 5568 -> 5568: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/RunWindow.cs:25` | 5573 -> 5573: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/RunWindow.cs:28` | 5574 -> 5574: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/RunWindow.cs:33` | 5580 -> 5580: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/WindowConsumer.cs:23` | 5638 -> 5638: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/WindowConsumer.cs:28` | 5643 -> 5643: Killed |
| `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492:VerifyRun/Window/WindowConsumer.cs:42` | 5661 -> 5661: Survived |

## Residual limitations

The run log contains 506 WRN-tagged lines, including 499 coverage-relay acknowledgement timeouts marked Dubious. Other warnings include target-framework assumptions, an initial simulated-build restore fallback, the preview-MTP notice and a safe-mode compile-error notice. The [log summary](raw/validation/log-summary.json) records the count definition, command, selected control/tool lines and original log hash/size; the complete [compressed log](raw/mutation-full/command.log.gz) remains the source. These observations do not establish a runner discrepancy or explain any particular surviving variant.

| Still-Survived assigned original ID | Separate proof or residual state |
| --- | --- |
| 4875 | [P1](../child-b/proofs.md#p1-variant-4875); raw status remains Survived |
| 4899 | No accepted gap or new proof; residual-disposition decision remains with Hari |
| 4900 | No accepted gap or new proof; residual-disposition decision remains with Hari |
| 5147 | [P2](../child-b/proofs.md#p2-variant-5147); raw status remains Survived |
| 5297 | No accepted gap or new proof; residual-disposition decision remains with Hari |
| 5315 | [P3](../child-b/proofs.md#p3-variant-5315); raw status remains Survived |
| 5531 | [P4](../child-b/proofs.md#p4-variant-5531); raw status remains Survived; runtime premise not verified for the mutation test process |
| 5532 | No accepted gap or new proof; residual-disposition decision remains with Hari |
| 5661 | [P5](../child-b/proofs.md#p5-variant-5661); raw status remains Survived |

Original IDs 4899, 4900, 5297, 5532 have no proof annotation in this closure map. The five existing P1-P5 arguments remain in [child B's proofs](../child-b/proofs.md); P4's installed-runtime listing is not treated as verification of the runtime selected by the mutation test process. No additional proof or runtime investigation was performed.

Historical ordinary replays 2657, 4585 and 5014 remain compilation-limited comparisons in the [historical record](../../issue-92-pr6b/review/replay/replay.json). Their exact tuples are now each reported Killed by this fresh Stryker run; 4585 is control-only, outside the assigned variants. These observations neither repair the historical ordinary replays nor establish agreement between runners. Their original limitations remain linked in [closure JSON](raw/closure.json).

No new survivor repair or accepted gap is made by this pull request.

Issue #93 remains open pending a residual-disposition decision, including the surviving assigned variants, P4's qualification, the initial-control outcome/count limitations and the observed MTP warnings. This receipt does not authorize live A01 or change an expected outcome, threshold or verdict.

## New survivors

Across the complete report, 2 tuples are newly surviving relative to the historical Survived/NoCoverage set and 10 previously surviving tuples remain. The following newly surviving entries are recorded mechanically, without investigation or repair:

| New report ID | File and exact span | Raw status | Mutator and replacement |
| --- | --- | --- | --- |
| 1891 | `ProbeGate/ProbeGateEvaluator.cs:47:17-47:86` | Survived | Logical mutation: `input.Status != ProbeGateInputStatus.Readable && input.Result is null` |
| 1907 | `ProbeGate/ProbeGateEvaluator.cs:60:22-60:87` | Survived | Logical mutation: `identity!.ScenarioId != "A01" && identity.ConfigurationId != "C0"` |

The full raw survivor list, including 5025 outside the fixed assignment, remains in [closure JSON](raw/closure.json). Newly surviving describes a report-to-report status classification, not a finding about its cause.

## Validation and review

The [output-validation receipt](raw/validation/closure-validation.json) records `passed: true` and the complete executed Python argv. It verified unique original-ID coverage, integer score arithmetic and report/source hash linkage. Seven fixed synthetic tests passed; their [source hashes and outcomes](raw/validation/helper-tests-1.json) identify the mapper code used. The original report was extracted once, and no mutation or extraction retry occurred.

The [raw manifest](raw-manifest.json) lists each retained raw file's repository-relative path, exact producer/source commit, byte size and SHA-256. The mapper's uncommitted source hash is retained in the extraction receipt; the measured source commit identifies production/test/config inputs. Program output remains under raw/. Authored mapper, tests, receipt and [cold-review record](cold-review.md) remain outside raw/ and in the review diff.

Documentation pass 1 exited 0 after 7.122616 s with no advisory warnings; its [receipt](raw/validation/docs-1.json) and [log](raw/validation/docs-1.log) retain the exact command and output. The full diff whitespace check flags the retained tool-list header in `raw/setup/tools.txt`; those program-written bytes are preserved verbatim. The authored diff whitespace check excludes only the rule-8 raw path. The first cold review reported `Findings: none at P0 or P1`; its [record](cold-review.md) retains the scope and outcome. The final publication diff receives a fresh cold review before push.

Documentation pass 2 exited 0 after 6.588314 s with no advisory warnings; its [receipt](raw/validation/docs-2.json) and [log](raw/validation/docs-2.log) retain the command and output. Both approved documentation passes are complete.

The final read-only manifest verifier uses the exact approved command below with `ROOT=/workspace/eventhubs-kafka-dotnet`, `E="$ROOT/docs/operations/evidence/issue-93/child-c"`, and `TESTED=e59271330e6ed3f727c0a0a3268ec9da77718bb5`. Its observed exit was 0; output reported `passed: true` for 29 files. It creates no raw output.

```bash
timeout --kill-after=5s 120s python3 -c '
import hashlib, json, pathlib, sys
root, evidence = (pathlib.Path(value).resolve() for value in sys.argv[1:3])
manifest = json.loads((evidence / "raw-manifest.json").read_text())
entries = manifest["files"]
expected = {path.relative_to(root).as_posix(): path for path in (evidence / "raw").rglob("*") if path.is_file()}
paths = [entry["path"] for entry in entries]
assert paths == sorted(paths) and len(paths) == len(set(paths))
assert set(paths) == set(expected)
for entry in entries:
    path = expected[entry["path"]]
    payload = path.read_bytes()
    assert entry["size_bytes"] == len(payload), str(path)
    assert entry["sha256"] == hashlib.sha256(payload).hexdigest(), str(path)
    assert isinstance(entry["producer"], str) and entry["producer"].strip(), str(path)
    assert sys.argv[3] in entry["producer"], str(path)
print(json.dumps({"passed": True, "files": len(entries), "source_commit": sys.argv[3]}))
' "$ROOT" "$E" "$TESTED"
```
