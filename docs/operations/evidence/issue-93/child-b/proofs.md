# Verifier coverage B callable-domain proofs

These are proof arguments conditionally approved under [the child decision, Q3][decision]. They are distinct from observed mutant kills and do not change the raw score denominator.

[Revision 2 of the approved implementation plan][plan] specifies these five arguments. The [independent current-revision plan check][check] accepted P1-P5 over their actual callable domains, with P4 limited to the pinned runtime and subject to rechecking if execution selects another implementation. It reported no counterexample. These arguments cover direct calls, including constructible inconsistent DTO inputs, rather than assuming only current production callers. Earlier exceptional paths common to the original and replacement remain common exceptional paths.

The frozen report SHA-256 is `f7b3aa454bf530820e683cf1e478f866ef9e9e4f6d538fdcb66f02391929d492`. Each line key below is that report identity followed by the stated file and line, as recorded in the linked frozen variant. Source links use the merged predecessor baseline `09a70930e471353b97ad07f9560e9734e562294e`. Span coordinates reproduce the frozen record without renumbering.

## P1: variant 4875

Frozen identity: `VerifyRun/Judgement/ConfigurationFidelityCheck.cs:45`; [span, mutator and replacement][v4875]: `(45,26)-(45,62)`, `Logical mutation`, `selectedOverride || observedOverride`. The original expression is `selectedOverride && observedOverride` in [ConfigurationFidelityCheck.Evaluate][fidelity].

Premises: the preceding `if (selectedOverride != observedOverride)` handles unequal booleans. Its `else if` reaches this expression only for equal booleans, and both operands are local values without side effects. The remaining pairs are `(false,false)` and `(true,true)`.

Argument: AND and OR agree for each remaining pair. They execute the same body and compare the same override values. Any earlier null dereference occurs before either expression in both versions. The frozen English explanation that any override immediately causes a mismatch does not describe this exact guarded replacement.

Conclusion: body execution, retained differences, assessment and exceptions coincide for every reached callable state. The independent check accepted P1 on this equality argument, including the earlier exceptional paths.

## P2: variant 5147

Frozen identity: `VerifyRun/Judgement/StatisticsChecks.cs:43`; [span, mutator and replacement][v5147]: `(43,31)-(43,43)`, `Equality mutation`, `number < 26`. The original condition is `number <= 26` in [StatisticsChecks.Evaluate][statistics-init].

Premises: all observations are newly constructed. `Observation.Missing` is an ordinary auto-property with the false default, and the loop counter is local and unobserved. Only check 26's initial assignment differs. `associationsComplete` requires `statisticsAvailable` to be true, regardless of direct DTO contents or report-count consistency.

Argument: if statistics are available, the original assigns false to check 26 and the replacement leaves its false default. Otherwise `associationsComplete` is necessarily false, and the immediately following incomplete-association branch assigns `Missing=true` to check 26 in both versions before any report processing. Other observations and local state are identical; the changed loop evaluation has no external side effect.

Conclusion: state after the initialization and incomplete-association branch, subsequent results and exceptions coincide across the callable domain. The independent check accepted P2, including unavailable statistics and inconsistent direct associations. Variant 5146 on this same line remains assigned to child A.

## P3: variant 5315

Frozen identity: `VerifyRun/Judgement/StatisticsChecks.cs:174`; [span, mutator and replacement][v5315]: `(174,17)-(174,53)`, `Logical mutation`, `selected is null && selectionUnknown`. The original expression is `selected is null || selectionUnknown` in [StatisticsChecks.Evaluate][statistics-boundary].

Premises: the expression is reached only inside the `boundaryAvailable` block. `selectionUnknown` starts as `!associationsComplete` and is subsequently only assigned true. Earlier boundary handling at lines 153-157 can set check 26 missing. The changed statement only assigns true to its ordinary `Missing` property and never clears it.

Argument: equal operands give the same result. If `selected` is non-null and `selectionUnknown=true`, line 153 has already set check 26 missing. If `selected` is null and `selectionUnknown=false`, then `associationsComplete=true`, hence `statisticsAvailable=true`; with `boundaryAvailable=true`, lines 154-157 have already set check 26 missing. Thus the original's extra assignment in either unequal case only repeats an existing true value.

Conclusion: both versions enter all subsequent code with identical state, including the state used for diagnostic precedence. This covers inconsistent direct DTO inputs as well as parsed evidence. The independent check accepted P3 on these prior assignments; it did not narrow the domain to parser outputs.

## P4: variant 5531

Frozen identity: `VerifyRun/Window/MessageIdentityReader.cs:11`; [span, mutator and replacement][v5531]: `(11,59)-(11,64)`, `Boolean mutation`, `true`. This changes the first argument of `new UTF8Encoding(false, true)` in [MessageIdentityReader][identity] to true.

Premises: `StrictUtf8` is private and is used only for `GetString`; no caller observes its preamble, equality or hash APIs. In the pinned [UTF8Encoding constructors][utf8-constructor], the first argument sets `_emitUTF8Identifier` separately from `_isThrowException`. The unchanged second argument retains exception fallback. [Encoding.GetString(byte[])][encoding] dispatches to the range overload. The pinned [UTF8Encoding decoding path][utf8-decode] does not read `_emitUTF8Identifier`; the field's reads belong to [preamble, equality and hash APIs][utf8-preamble].

Argument: changing the preamble flag leaves decoding and invalid-byte fallback unchanged. Null identity bytes take the same pre-decoding branch in `MessageIdentityReader`; empty, malformed and preamble-prefixed byte sequences take the same decoding path and produce the same strings or exceptions. The reader therefore retains the same null decoded values and structural problems. Variant 5532 changes the second constructor argument and is a separate regression assignment, not part of this proof.

Runtime premise: the cited implementation is runtime commit `6aa72daa24f66c01c3d7b6a32519e9d21396deec`. The [retained B setup output](raw/setup-1/command.log) reports SDK `10.0.402`, Host `10.0.13`, commit `4099f83977`. The [official host source manifest][host-manifest], retained as [raw source](raw/proof-sources/source-manifest.json), maps `runtime` to that full runtime commit. The retained [UTF8Encoding.cs](raw/proof-sources/UTF8Encoding.cs) SHA-256 is `47f5e4c98751367a86329ab1e0a76d86b39fd076a5b019284694f39d40fd9011`, matching the independent check. The retained [Encoding.cs](raw/proof-sources/Encoding.cs) SHA-256 is `44b11ddb443bd71b1c7a4406a8749d234c0e0b135d9a36dc53c4f0887b197db4`. Fetch status, effective URLs and TLS verification results are recorded separately under [raw/proof-sources](raw/proof-sources/UTF8Encoding.fetch.json), with response headers and per-source metadata. These are source and setup observations, not mutation execution evidence.

Conclusion: decoding, problems and exceptions coincide for every input reachable through `Read` under this pinned runtime. The independent check accepted P4 for this implementation and independently matched Host `4099f83977` through its source manifest. The B setup/source match satisfies that premise for the retained selection; if subsequent execution selects a different runtime implementation, recheck the proof before accepting it for those bytes.

## P5: variant 5661

Frozen identity: `VerifyRun/Window/WindowConsumer.cs:42`; [span, mutator and replacement][v5661]: `(42,47)-(42,68)`, `Equality mutation`, `pollSlice <= remaining`. The original condition is `pollSlice < remaining` in [WindowConsumer.Drain][consumer].

Premises: both operands are local `TimeSpan` values. The conditional passes the selected value directly to `Consume`; selecting an operand has no branch side effect and exposes no reference identity.

Argument: unequal operands select the same operand in both versions. Equality changes operand selection, but equal `TimeSpan` values have identical ticks, so the resulting value passed to `Consume` is identical. Zero and negative values on reached paths obey the same argument; positivity of current configuration is not a premise.

Conclusion: native timeout arguments, subsequent behavior and exceptions coincide for all reached `TimeSpan` input pairs. The independent check accepted P5 on value equality across the complete reached domain. Existing timeout regression rows remain regression evidence, not observed mutant kills.

All five arguments retain the child decision's Q3 scope. They request no exclusion or accepted gap, and no new mutation execution is claimed here. Parent closure remains `pending #93 closure run under #89 and remaining Q5 dispositions` under the child decision, Q4.

[decision]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301526641
[plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301492532
[check]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/100#issuecomment-6301817595
[v4875]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L1551-L1573
[v5147]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L2469-L2491
[v5315]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3297-L3319
[v5531]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L3801-L3823
[v5661]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/docs/operations/evidence/issue-92-pr6b/review/dispositions.json#L4107-L4129
[fidelity]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/src/Parchwell.Verifier/VerifyRun/Judgement/ConfigurationFidelityCheck.cs#L38-L59
[statistics-init]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs#L23-L55
[statistics-boundary]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/src/Parchwell.Verifier/VerifyRun/Judgement/StatisticsChecks.cs#L153-L199
[identity]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/src/Parchwell.Verifier/VerifyRun/Window/MessageIdentityReader.cs#L11-L57
[consumer]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/09a70930e471353b97ad07f9560e9734e562294e/src/Parchwell.Verifier/VerifyRun/Window/WindowConsumer.cs#L23-L65
[utf8-constructor]: https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Text/UTF8Encoding.cs#L79-L109
[utf8-decode]: https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Text/UTF8Encoding.cs#L677-L703
[utf8-preamble]: https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Text/UTF8Encoding.cs#L861-L893
[encoding]: https://github.com/dotnet/runtime/blob/6aa72daa24f66c01c3d7b6a32519e9d21396deec/src/libraries/System.Private.CoreLib/src/System/Text/Encoding.cs#L998-L1003
[host-manifest]: https://github.com/dotnet/dotnet/blob/4099f83977/src/source-manifest.json#L81-L86
