# Packet-summary core receipt

This PR implements a synthetic-tested decoder and bounded summary encoder from the approved capture design. It supplies no packet acquisition, live observation or public result.

## Execution card

- Deliverable: the exact new production/test paths, contract and state update in [core plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6616295730), updated 2026-11-03T21:50:57Z.
- Authorization: [standing investigation approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), [approved design and later-work direction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298), and the [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6616348469). The check identifies this revision and states `Blocking findings: none`; the plan has no open question for this component.
- Source main: `4738586e121568be759e82766776b39520246418`, the merged design PR. No existing source, test or project file is to change.
- Mandatory gates: the plan's Acceptance and publication checkpoint; AGENTS.md GOVERNANCE and Before opening a pull request; docs/agents/pull-requests.md; factory.md Item lifecycle, Authority reads and Escalation.
- Exact verification commands: `./scripts/verify-dotnet.sh`; `npm run check:docs` (locked dependencies are already installed); `git diff --check`; the development-time Python scope/ASCII/source-integrity assertion recorded below. No live dispatch belongs to this PR.
- Earliest normal push/PR: successful acceptance, applicable cold-review coverage and refreshed authority. Final-head CI and hosted reviews remain separate required gates.
- Budgets: the plan caps changed-file documentation-warning dispositions at 12, with exhaustion action `push -wip and hand off to the next session`. Its fixed fixture matrix includes one pass through all byte flag combinations and one maximum-count streaming case. Other runtime-sized work follows AGENTS.md rule 11; no discretionary retry loop is authorized.
- Cold-review budget: docs/agents/pull-requests.md rule 8 owns the cumulative budget and exhaustion action. "At most two comprehensive passes run per PR across all sessions." It then allows "at most two focused correction-review rounds", including its P2-correction exception. "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari."
- Predicate map: not applicable to verifier work; no verifier predicates change. The component's own boundary-test map will identify its planned test members and unreachable production byte ceilings.
- Models: Codex implementing session and separate high-effort implementation agent, with exact model names not exposed. Planning and the independent plan check were separate high-effort agents. No core cold reviewer has started.

## Sources and specification comparison

Read on 2026-11-03: [RFC 791 section 3.1](https://www.rfc-editor.org/rfc/rfc791.html#section-3.1), Figure 4 and the IHL, Total Length, flags, protocol and address descriptions; [RFC 9293 section 3.1](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.1), Figure 1, Data Offset and control-bit definitions. These support the field layout and header-length interpretation. They do not establish any packet acquisition or kernel privacy property.

At the source commit, `IdleObservationOptions.cs` supplies the existing run/commit grammar; project wildcard inclusion and `Run/Ledger/AssemblyInfo.cs` permit these internal types and tests without a project change. The new implementation has no cross-slice call or executable route. Manual full-string character validation implements the specified grammar, including rejection of trailing newline characters.

The approved design owns experimental interpretation and limits. The new contract describes only the selected component's decoder precedence, explicit public/private projection and bounded stream writes. The source files implement plan Exact changes sections 1-4; the synthetic tests implement section 5; the contract and state implement section 6. File-boundary atomic finalization, acquisition, socket/flow ownership, clocks, verifier judgment and retention remain unimplemented. A stream write can partially succeed before throwing; the exposed byte counter records only successful write calls and is not a failed-stream length receipt.

Synthetic fixture values are labeled synthetic in `SyntheticPacketHeaders.cs`. They are not observations of Event Hubs or a node. The component test map is:

| Requirement | Test members (in the planned test files) |
|---|---|
| Header size, options and both directions | `Given_SyntheticHeaderLengths_When_DecodingBothDirections_Then_NormalizesOnlyScalarFields`; `Given_SyntheticSpanBoundary_When_Decoding_Then_RequiresExactDeclaredHeaders`; declared-offset and Total Length theories |
| Flags and privacy projection | synthetic flags theory; `Given_SyntheticPayloadAndOptions_When_ProjectingHeaders_Then_ManagedSummaryContainsOnlyAllowedFields`; `Given_AllByteFlagCombinations_When_EncodingOnceEach_Then_UsesExactOrderedFlagStrings` |
| Fragment/peer checks and precedence | fragment and remote-port theories; `Given_MultipleSyntheticMalformedFields_When_Decoding_Then_UsesSpecifiedDiagnosticPrecedence` |
| Identity/event syntax and exact JSON | encoding boundary theories; `Given_EmptyOrPopulatedEvents_When_Completing_Then_EmitsExactCompactEnvelope`; start/end UTC cases |
| Event count | `Given_ProductionCountBoundary_When_StreamingOnce_Then_CompletesAtBothAllowedCountsAndRejectsNextEvent` streams to counting sinks without an event array |
| Byte/envelope/total limits | production-boundary budget theories, arithmetic-extreme case, and smaller-ceiling tests through the real writer |
| Write faults and terminal states | `Given_StreamFaultOrCancellation_When_WritingEachStage_Then_FaultIsTerminal`; completed-writer and invalid-event cases |

The legal compact event tops out at 125 bytes, while the policy cap is 256. The schema and event-count ceiling leave production total/envelope ceilings unreachable as exact real-output states. Budget-helper boundary tests plus smaller positive limits through the production writer test these guards without broadening the schema or exposing configuration overrides. These tests are planned evidence until the verification results below record execution.

## Verification

The execution card was authored before implementation. The pinned SDK 10.0.402 was installed in the workspace's temporary tools directory, and the managed Docker daemon responded successfully. The required `./scripts/verify-dotnet.sh` exited 1: locked restore passed, then the solution build failed with three distinct CA1861 analyzer errors. The count was computed from unique file/line/column/code diagnostics, excluding the repeated build-summary lines. Architecture checks and test execution were not reached, so no test pass is claimed.

`git diff --check` and `git diff --cached --check` passed. The following development-time scope assertion passed after staging all planned paths; it verified eleven paths, unchanged existing source/test/routes, ASCII docs and the computed 125-byte maximal compact event / 16908288-byte conservative ceiling:

```bash
python - <<'PY'
from pathlib import Path
import subprocess,json,re
base='4738586e121568be759e82766776b39520246418'
production={f'src/Parchwell.LoadGen/IdleConnectionCapture/{n}.cs' for n in ['PacketCaptureTypes','PacketHeaderDecoder','PacketSummaryEncoding','PacketSummaryStreamWriter']}
tests={f'tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/{n}.cs' for n in ['PacketHeaderDecoderTests','PacketSummaryEncodingTests','PacketSummaryStreamWriterTests','SyntheticPacketHeaders']}
docs={'docs/operations/idle-connection-capture-core-contract.md','docs/operations/evidence/issue-334/capture-core/receipt.md','items/334/state.md'}
expected=production|tests|docs
changed=set(subprocess.check_output(['git','diff','--name-only',base],text=True).splitlines())
assert changed==expected,sorted(changed^expected)
existing=set(subprocess.check_output(['git','ls-tree','-r','--name-only',base],text=True).splitlines())
assert changed&existing=={'items/334/state.md'}
for path in docs:assert Path(path).read_text().isascii(),path
for path in production:
 text=Path(path).read_text();assert 'namespace Parchwell.LoadGen.IdleConnectionCapture;' in text
 assert not re.search(r'using Parchwell\.LoadGen\.(?!IdleConnectionCapture)',text),path
# Git's exact file set plus existing-path intersection leaves existing routes, sources and tests byte-identical.
event={'flow':64,'direction':'outbound','elapsed_ns':2**63-1,'flags':['SYN','ACK','FIN','RST','PSH','URG','ECE','CWR']}
assert len(json.dumps(event,separators=(',',':')).encode())==125
assert 65536*257+65536==16908288<20971520
print(f'core scope verified: {len(expected)} files; existing routes unchanged; maximal compact event 125 bytes; conservative ceiling 16908288 bytes')
PY
```

`npm run check:docs` exited 0 with eight warnings in unchanged files and none in the authored paths at the scan checkpoint. Subsequent edits only record this handoff; they have not received a new documentation scan or cold review. No generated output or downloaded source is committed.

### First measured execution workload and continuation

The first build measured three analyzer findings, all CA1861 (prefer static readonly fields for repeated constant-array arguments):

- `PacketHeaderDecoderTests.cs:76`, the expected event-key array.
- `PacketSummaryEncodingTests.cs:26`, the expected event-key array.
- `PacketSummaryStreamWriterTests.cs:36`, the expected envelope-key array.

The approved plan caps documentation-warning dispositions but does not cap compiler/analyzer findings. AGENTS.md Before opening a pull request rule 11 says: "At an uncapped first measurement, record the count and an estimate of the remaining time under hard safety rule 11, then push -wip and hand off to the next session." No correction or verification retry was attempted after this measurement.

Estimate on 2026-11-03: 20-40 minutes for the three identical array-extraction fixes, full verification and review preparation, based on three localized changes and the still-unrun test suite. This is not a measured duration; hosted CI/review waiting is additional and unknown. The next session has these three concrete fixes, must preserve every assertion, rerun the required verification, update this receipt/state and complete cold/hosted review before a normal push or PR. The backup branch is `codex/issue-334-packet-summary-core-wip`; it is not a PR branch.

## Review record

No cold review has started for this PR. Cumulative consumption: zero comprehensive and zero focused rounds. The merged design PR consumed one comprehensive and zero focused rounds under its separate budget. Complete reports and dispositions belong in the PR self-review record, with final-head CI and hosted review evidence; no report will be added to its own reviewed receipt.
