# Packet-capture counter contract

The packet-counter accumulator sums each submitted packet and drop sample into unsigned 64-bit totals. It requires an explicit final sample before software completion and retains only partial diagnostic totals after failure. It does not read a socket or establish complete packet evidence.

## Authority and source boundary

This component implements [Accumulate packet-capture statistics, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626175155), approved under standing approval by its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626231782). The [approved design](idle-connection-capture-design.md), Selected resource and capture bounds, owns sampling policy, capture-failure handling and experimental interpretation. Blueprint [ADR-019](../blueprint.md) owns the division between capture-time controls and verifier judgment.

In the [pinned Linux v6.12 packet-socket implementation](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/net/packet/af_packet.c), `packet_getsockopt` handles `PACKET_STATISTICS` by copying and clearing statistics and exchanging the drop counter with zero. It adds drops into returned `tp_packets` before copy-out. The later `put_user` or `copy_to_user` can fail after that reset. The [pinned packet-socket API](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/include/uapi/linux/if_packet.h), `struct tpacket_stats`, declares `tp_packets` and `tp_drops` as unsigned integer fields.

These are claims about that source revision, not the deployed AKS kernel. The managed API accepts already-decoded `uint` samples; native layout, returned-length validation and decoding are not implemented here. The component cannot detect native counter wrap, omitted samples or a caller submitting a sample twice. A read failure must not be retried and represented as recovery of the lost sample.

The [core](idle-connection-capture-core-contract.md), [file](idle-connection-capture-file-contract.md) and [queue](idle-connection-capture-queue-contract.md) contracts describe their own staged component boundaries. This arithmetic component does not turn them into an executable observer or add a runtime receipt, public field, destination or publication path.

## API and scalar state

The internal `PacketCaptureCounters()` uses `ulong.MaxValue` for packet-total, drop-total and sample-count arithmetic ceilings. Its internal test constructor accepts three positive `ulong` ceilings and rejects zero with `ArgumentOutOfRangeException` before storing the ceilings. Reduced ceilings are test seams, not runtime options or experimental thresholds.

The read-only properties are `PacketTotal`, `DropTotal`, `SampleCount`, `State` and `FailureCode`. Methods are `TryAddSample(uint packets, uint drops)`, `TryComplete(uint packets, uint drops)`, `FailRead()` and `Abort()`. One sequential owner performs operations; no lock or concurrency guarantee is added.

The accumulator retains only running scalar totals, immutable ceilings, state and the first fixed failure code. It has no sample history, raw buffer, header, endpoint, timestamp, serialization, I/O, logger, native interop or background task. It owns no resource needing `IDisposable`.

PacketTotal and DropTotal are independent sums of the fields submitted by the caller. Drops are neither added to packets again nor subtracted from them. SampleCount counts successfully accepted submissions, including zero samples and the final sample. There is no `drops <= packets`, zero-drop, cadence, inferred-loss or packet/event-count-equality check.

## Addition and completion

Initial state is Active with all totals zero and null FailureCode. Both sample methods use the same admission sequence:

1. Faulted rejects unchanged. Completed faults with `packet_counter_state_invalid`, retaining totals.
2. Check packet addition against remaining packet capacity, then drop addition against remaining drop capacity, then whether SampleCount already equals its ceiling. Subtraction-based checks occur before any total changes. A failed check faults with `packet_counter_overflow` and preserves all earlier totals.
3. Add each submitted value once and increment SampleCount once. Samples are independent read results; they are not cumulative counters to subtract, reset or deduplicate.
4. A successful ordinary sample leaves Active. A successful final sample sets Completed only after its additions.

A zero/zero sample is an observed caller submission and increases SampleCount. Initial zeros with SampleCount zero indicate no submitted evidence. `TryComplete(0, 0)` on a fresh instance accepts one explicit final sample, producing Completed with SampleCount one. No parameterless completion or automatic final zero exists. Repeating completion submits another sample and therefore fails state admission.

Nonzero drops are accepted and accumulated, including in a final sample. Completed means only that the supplied final addition succeeded. It establishes no successful native read, correct cadence, drop-free run, successful file finalization or complete observation. The runtime and verifier still apply the approved design's respective responsibilities.

## Failure and state transitions

| Operation | Active | Completed | Faulted |
|---|---|---|---|
| Successful ordinary sample | Add atomically; remain Active | Fault with state-invalid | Return false unchanged |
| Successful final sample | Add atomically; become Completed | Fault with state-invalid | Return false unchanged |
| Addition exceeds any ceiling | Fault with overflow; retain prior totals | State-invalid wins before arithmetic | Return false with first failure |
| FailRead | Fault with read-failed; add nothing | Fault with state-invalid | No change |
| Abort | Fault with abandoned | No change | No change |

The exact codes are `packet_counter_state_invalid`, `packet_counter_overflow`, `packet_counter_read_failed` and `packet_counter_abandoned`. All faults preserve the first code, set Faulted and permanently reject later additions/completion. Retained totals after failure are partial diagnostics, never complete totals. Supplied values and exception text do not enter diagnostics.

FailRead records a failed, unavailable or unsupported read without creating a sample or incrementing SampleCount. Abort abandons unfinished accounting while preserving earlier accepted samples. There is no reset or recovery API. Abort after Completed preserves Completed, null failure and the accepted totals.

## Future caller obligations

One sequential owner must supply every successful native sample once. A failed, unavailable or unsupported sample calls FailRead instead of adding zero. The final sample must be acquired after the separately specified bounded drain. The runtime applies the existing stop/cleanup policy and preserves the fixed diagnostic; the verifier alone applies approved completeness rules.

Native decoding, acquisition, sampling cadence, time bounds, counter interpretation in the private receipt and publication remain separately planned integration. No retry or automatic recovery is authorized by this component. A successful software completion cannot substitute for those missing proofs.

## Verification limits

Unit tests use literal synthetic samples with the actual accumulator. They cover independent arithmetic boundaries, atomic final-add failure, explicit zero versus missing reads, repeated samples, first-failure preservation, completed/faulted precedence and accumulation beyond a single native field's range. Reduced test ceilings exercise representational guards without a large sampling campaign.

The tests do not prove native layout, successful socket reads, absence of source-counter wrap or omissions, sample cadence, actual-image compatibility or live completeness. The [single authored receipt](evidence/issue-334/capture-counters/receipt.md) records actual commands, results and the behavior-to-test map.
