# Packet-summary core contract

The packet-summary core decodes already-truncated synthetic IPv4/TCP headers and encodes only the packet fields allowed by the approved capture design. It does not acquire packets, establish complete observations or publish results. Runtime filtering, correlation, atomic file finalization and verifier-controlled publication remain later work.

## Scope and sources

The internal `Parchwell.LoadGen.IdleConnectionCapture` types implement [core plan Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6616295730), independently checked under standing approval by [the identifying plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6616348469). The [approved capture design](idle-connection-capture-design.md) owns the scientific rules and runtime bounds. This component is not an executable observer route or a verifier.

Header field layout comes from [RFC 791 section 3.1](https://www.rfc-editor.org/rfc/rfc791.html#section-3.1) and [RFC 9293 section 3.1](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.1). IPv4 IHL and TCP Data Offset count 32-bit words. IPv4 Total Length includes the original data, so it may exceed the supplied header-only span. Diagnostic precedence and the supported input shape below are implementation choices in the approved core plan.

## Header decoding

`PacketHeaderDecoder.TryDecode(ReadOnlySpan<byte> header, uint endpointIpv4, PacketDirection direction, out PrivatePacketHeader? packet, out PacketHeaderDiagnostic diagnostic)` accepts only an already-truncated header span. A future kernel filter must exclude payload before delivery to this component; a decoder test cannot prove that boundary.

The decoder checks these conditions in order:

1. Direction must be `Outbound` or `Inbound`; the span must contain 20 through 120 bytes. The version must be IPv4. IHL must describe 20 through 60 supplied bytes, and Total Length must be at least IHL.
2. The remote IPv4 address, selected by direction, must equal the supplied endpoint.
3. A fragment with MF set or a nonzero fragment offset must contain only its IPv4 header. Such a header returns `FragmentUnsupported`; extra fragment bytes return `HeaderLengthInvalid`. It is never parsed as TCP. DF alone is allowed.
4. An unfragmented header must name TCP and contain at least its minimum header. Data Offset must describe 20 through 60 TCP-header bytes. The supplied span must equal the two declared header lengths, and Total Length must be at least that sum.
5. The remote port must be 9093. The decoder then extracts normalized local/remote endpoints, sequence and acknowledgment values, the flags and header lengths. Options are recognized only through their lengths; their bytes are not retained.

The fixed diagnostics are `None`, `HeaderLengthInvalid`, `UnsupportedNetworkProtocol`, `UnexpectedPeer` and `FragmentUnsupported`. Failure returns `false` with a null packet; success returns `true` with `None`. Invalid direction takes the header-length diagnostic. No diagnostic includes raw input or endpoint values. Checksums, TCP state and experimental completeness are outside this decoder's contract.

`PrivatePacketHeader` is a sealed immutable class containing only in-memory correlation inputs. It must never be passed to a serializer or logger. It has no raw-buffer reference, options copy or value-revealing `ToString`. An explicit `PacketSummaryEvent` contains only flow, direction, elapsed nanoseconds and flags; private header fields are not inputs to the encoder.

## Explicit JSON encoding

`PacketSummaryEncoding.TryEncodeEvent(PacketSummaryEvent, out ReadOnlyMemory<byte>, out string?)` writes compact UTF-8 into a fixed 256-byte scratch buffer. It uses explicit `Utf8JsonWriter` property calls, with no reflection-based serialization or arbitrary extension fields.

| Object | Properties, in output order |
|---|---|
| Envelope | `schema_version`, `run_id`, `source_commit`, `configuration_id`, `repeat`, `capture_started_utc`, `events`, `capture_ended_utc` |
| Event | `flow`, `direction`, `elapsed_ns`, `flags` |

Schema version is integer 1. Direction is exactly `outbound` or `inbound`. Flags are an array containing only present flags, ordered `SYN`, `ACK`, `FIN`, `RST`, `PSH`, `URG`, `ECE`, `CWR`; zero flags produces an empty array. There are no addresses, ports, sequence values, raw headers, option bytes or payload fields.

Identity syntax follows the plan's existing idle-run grammar: a lowercase alphanumeric run ID of length 1 through 29, allowing internal hyphens but no leading/trailing hyphen; a source commit of exactly 40 lowercase hexadecimal characters; configuration `C0` or `C1`; repeat 1 through 5; and a zero-offset start timestamp. Event syntax allows flow 1 through 64, a defined direction and nonnegative elapsed nanoseconds. The component does not check flow history or event ordering. UTC labels use invariant `O` formatting, and the zero-offset end must be at or after start. They are never used to align packet events.

The maximal legal compact event is 125 UTF-8 bytes, computed from the plan's flow 64, outbound direction, maximum signed 64-bit elapsed value and all eight flags. The 256-byte safety cap remains the approved budget, not a claim that the schema can reach it.

## Bounded streaming

`PacketSummaryStreamWriter(Stream, PacketSummaryIdentity)` validates identity and begins one envelope. The caller owns the writable stream; the writer never disposes it. `TryAppend(PacketSummaryEvent)` appends one encoded event, and `TryComplete(DateTimeOffset)` writes the final end label and closes the envelope. Calls are sequential and have one owner.

| Bound | Approved literal |
|---|---|
| Events | 65536 |
| Encoded event | 256 bytes |
| Inter-event separator | One byte |
| All non-event envelope bytes | 65536 bytes |
| Entire stream, including envelope | 20971520 bytes |

The writer reserves the full envelope allowance before the first write, then checks every event, separator and aggregate byte count before appending. It does not retain an event list or a whole summary. Overflow-safe subtraction or checked arithmetic prevents a wrapped value from passing a guard. The internal budget seam supports smaller positive limits for tests only; no options, environment variable or runtime route changes production defaults.

State is `Active`, `Completed` or `Faulted`. Any encoding, budget, cancellation or stream-write failure ends usable writing. A failed or completed writer rejects subsequent append/completion calls and writes no more bytes. A partial stream is not a completed summary, and valid JSON is not a completeness receipt.

The exposed bookkeeping is state, fixed failure code, event count and bytes written. Bytes written counts successful stream writes; a caller stream can write part of a buffer before throwing, so that counter cannot establish the actual length of a failed stream. Failure never turns that uncertainty into successful completion or includes exception text.

Fixed failure codes are:

- `packet_identity_invalid`
- `packet_event_invalid`
- `packet_event_count_exceeded`
- `packet_event_bytes_exceeded`
- `packet_summary_bytes_exceeded`
- `packet_summary_envelope_exceeded`
- `packet_summary_write_failed`
- `packet_summary_state_invalid`

This writer creates, renames and uploads no file. A later observer owns `/work` placement, file-boundary limits and atomic finalization. This PR selects no retained-summary destination and publishes no unchecked summary.

## Synthetic verification boundary

The tests exercise minimum/maximum and mixed header lengths, both directions, all flag combinations, fragments, malformed headers and diagnostic precedence. Synthetic payload and option sentinels must not appear in encoded output. They also exercise exact JSON key sets, identity/event boundaries, count/byte guards, cancellation, stream failures and terminal states.

The legal schema and event-count cap cannot reach every production byte ceiling. Budget-helper boundary cases and smaller-limit tests through the real writer exercise those otherwise unreachable guards. The maximum-count case streams to a counting sink, without retaining an event array. None of these tests proves kernel filtering, packet acquisition, socket ownership, clocks, atomic finalization or live evidence completeness.
