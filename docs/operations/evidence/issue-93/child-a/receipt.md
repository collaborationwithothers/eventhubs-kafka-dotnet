This receipt records #97 child A's additive verifier regressions against the frozen PR 6b missing-test inventory. It records ordinary execution evidence, the two variants transferred to child B, and mutation evidence deferred to parent item closure.

Implementation acceptance is incomplete. The final authorized ordinary suite restored successfully, then failed compilation before any tests ran. The later decision requires stopping at this failure; no discovery, focused execution, XML validation, documentation pass or cold review followed it.

## Authority and tested inputs

- [Implementation plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6294278647), updated `2026-10-20T15:41:28Z`.
- [Independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6295196111) and [implementation approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6296501231).
- [Adopted child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6294423763), including the [parent split](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/93#issuecomment-6292867583), [item-plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/93#issuecomment-6292118819), and [parent approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/93#issuecomment-6293531003).
- [Cloud execution decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/97#issuecomment-6296951009) permits one SDK setup and the final suite, and records the first Mac attempt without importing its raw files. It says: "If prerequisites are missing or suite-2 fails or does not complete, stop and report."

The tested commit was `cd13c844b1ed302731a21af5062e811a63a721bf`, tree `110921c7a91a982e329408f8579fb5af108c93ff`, in `/workspace/eventhubs-kafka-dotnet`. [The original controller source manifest](raw/suite-2/tested-source.json) records the actual input hashes; [the controller receipt](raw/suite-2/controller.json) reports `source_inputs_unchanged=true` and `execution_evidence_complete=false`.

The one SDK setup selected official Linux x64 SDK 10.0.402, checked the archive against the official release metadata's SHA-512, and extracted it outside the repository. [SDK selection and hashes](raw/setup-1/sdk-selection.json), [official metadata snapshot](raw/setup-1/release-metadata.json), [setup outcome](raw/setup-1/setup.json) and [setup log](raw/setup-1/command.log) retain the observations. The effective NuGet source listing contains only `https://api.nuget.org/v3/index.json`. No build, test or restore was run during setup.

## Assigned inventory

The plan assigns 108 active line keys / 185 active variants after transferring StatisticsChecks.cs:43 /5147 and :174 /5315 to child B. These are approved inventory figures, not newly reconciled completion counts. The finite per-line case/discovery/XML receipt has not been produced because execution stopped before tests. No line or variant is claimed complete.

Historical report SHA-256: `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492`. Frozen input hashes and the reused controller hash, measured in this checkout:

| Input | SHA-256 |
| --- | --- |
| `docs/operations/evidence/issue-92-pr6b/review/lines.json` | `ebddcc1555896e5ce8388acf5d5317bdb03e0a4d189fc5a669b0e2cb8533e1d5` |
| `docs/operations/evidence/issue-92-pr6b/review/dispositions.json` | `5d05c7071f29415c040f62c48547d63777b58f84697ded09e42738ced29c6452` |
| `docs/operations/evidence/issue-92-pr6b/controller.py` | `dd596cbc108bea213c33ae170b5eb75fc30a6d0aa838684cef2f5703052b2fe5` |

The two transfers remain assignment-only under the adopted child decision. No coverage, equivalence or accepted-gap disposition is made for them.

## Discovery and execution

The [ledger](raw/controller-invocations.json) retains both consumed suite slots. [Slot 1](raw/suite-1-user-reported.json) is explicitly a decision-authorized record of user-reported evidence, not an imported original controller receipt: Darwin, SDK 10.0.402, locked restore failed before compilation with NU1507/NU1900. Its original Mac files are not imported, as the later decision directs.

Slot 2 used the unchanged child command and approved controller:

```bash
python3 /workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-92-pr6b/controller.py --mode suite \
  --repo-root /workspace/eventhubs-kafka-dotnet \
  --cwd /workspace/eventhubs-kafka-dotnet \
  --run-dir /workspace/eventhubs-kafka-dotnet/docs/operations/evidence/issue-93/child-a/raw/suite-2 \
  --ledger-path /workspace/eventhubs-kafka-dotnet/.artifacts/issue-97/controller-invocations.json \
  --tested-commit cd13c844b1ed302731a21af5062e811a63a721bf --seconds 1800 \
  -- bash ./scripts/verify-dotnet.sh
```

The invocation inherited `PATH` with `/workspace/.tools/dotnet-10.0.402` prepended, plus `DOTNET_NOLOGO=1` and `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`. No test population override was present. The receipt records invocation ordinal 2, exit code 1, `command-exit`, no termination, and elapsed time `32.66370604200006` seconds.

[Locked restore](raw/suite-2/verification/restore.log) passed. [Release build](raw/suite-2/verification/build.log) failed with CS8506 and CS8131 at `tests/Parchwell.Verifier.Tests/Judgement/BookkeepingChecksTests.cs:332`: the added deconstruction's switch expression has no best inferred type for its string/nullable-row tuples. No tests executed; no discovery or focused XML exists. A proposed repair is to give every tuple's second component an explicit `long?` type while retaining the specified diagnostic values; that repair was not applied after the required stop.

[Copy provenance](raw/suite-2-copy.json) identifies the actual suite output directory printed by the script and its retained complete copy. The original [compressed command log](raw/suite-2/command.log.gz), [controller receipt](raw/suite-2/controller.json) and [tested-source receipt](raw/suite-2/tested-source.json) remain unchanged. The diagnostic, test-case and exception assertions have not been validated by execution.

## Mutation deferred to item closure

Status: `mutation evidence deferred to #93 closure under #89`. No mutation command, baseline, replay, mutant-kill claim or score claim was made. The historical compilation limitation for 5014 remains a historical limitation.

## Validation and review

Validation did not pass. Both suite slots are consumed; the SDK setup slot is consumed. The discovery, focused-test, XML-validator and documentation allowances have not been used. No cold review has run, and no implementation PR or cold-reviewed head exists. [The raw manifest](raw-manifest.json) records retained files, producers, byte sizes and SHA-256 values.

This evidence is saved through a rule-12 backup push; it is not a rule-8 reviewed branch push. Source and test files remain at the failed tested commit's bytes. Further repair or execution needs an issue decision under the later stop instruction.

## Deferred parent work

Child B owns its unresolved inventory and the two transferred variant identities. Parent #93 still requires the groups 01-08 mutation evidence under #89 before closure. No live A01 run is performed or authorized by this receipt.
