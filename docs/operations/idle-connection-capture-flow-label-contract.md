# Private flow-label storage contract

The private flow-label table stores an exact supplied connection identity and returns a stable ordinal within one instance. It does not interpret TCP packets or establish a valid connection generation, complete correlation or publishable evidence. Runtime lifecycle validation and verifier-controlled publication remain later work.

The caller supplies local IPv4, local port, remote IPv4, remote port and the opening sequence number as private scalar values. The table compares those values exactly and retains at most 64 distinct identities without eviction or reset. The approved capture design owns that bound and all scientific interpretation.

## Scope and source

[Bound private flow-label storage, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626802577) and its [identifying independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6626855188) specify this component. The [approved capture design](idle-connection-capture-design.md), sections Selected socket-to-repeat and clock correlation and Selected resource and capture bounds, owns the identity fields and generation budget. The [core contract](idle-connection-capture-core-contract.md) owns header normalization and explicit event encoding. Blueprint [ADR-019](../blueprint.md) retains evidence judgment in the verifier.

The implementation is [PacketFlowLabelTable.cs](../../src/Parchwell.LoadGen/IdleConnectionCapture/PacketFlowLabelTable.cs); its synthetic acceptance cases are [PacketFlowLabelTableTests.cs](../../tests/Parchwell.LoadGen.Tests/IdleConnectionCapture/PacketFlowLabelTableTests.cs). There is no runtime caller, native acquisition, configuration change or public schema change.

## Storage and failure

`PacketFlowLabelTable.TryGetOrAdd` accepts five private identity scalars and returns a success flag plus an ordinal through `out int flow`. `Count` reports distinct stored identities; `FailureCode` is initially null. `MaximumFlows` is 64. One sequential owner operates each instance; it provides no concurrency guarantee.

A fixed array holds only the five scalars per entry. The one-based array index is the label. While healthy, exact equality returns the existing label before the capacity check, including when the table is full. A new identity below capacity receives the next label and increments Count once. Zero and maximum scalar values are accepted without endpoint or network validation. Independent instances allocate their own labels.

A new identity at capacity sets `packet_flow_limit_exceeded`, returns false and leaves all entries and Count unchanged. Every call after that failure returns false, including lookup of an existing identity. Failure always returns flow zero and retains the fixed code. There is no eviction, removal, reset, completion or configurable capacity. Allocation failure is not caught or reported as successful capture.

A different supplied opening sequence is a different storage key; that fact grants no permission for connection reuse. The table exposes no entries or formatted keys and has no logging or serialization. Its ordinary `ToString()` returns the type name. It retains no header buffers, acknowledgment, flags, timestamps or event history. The fixed scan examines at most 64 entries; this is an algorithmic bound, not a measured memory claim.

## Enforced checks

1. All five private identity scalars participate in exact equality; identical identities reuse one label while distinct identities consume one new slot.
2. Labels are one-based, stable and instance-local; entries are never evicted, reset or reused within the instance.
3. Capacity is exactly 64 stored identities. While healthy, an existing identity succeeds at capacity; a new identity at capacity returns false with flow 0 and terminal `packet_flow_limit_exceeded` without changing stored entries or Count.
4. After failure, every call returns false and flow 0, preserving Count and the fixed failure code.
5. The table exports only ordinal/count/fixed failure information and ordinary type-name formatting. It retains only bounded private scalars and adds no logging, key serialization or packet/event history.
6. The table makes no endpoint, packet-state, generation-validity, clock, coverage, completeness or publication judgment. No runtime caller, configuration, schema, package, project, privilege, workflow or expected-outcome change is introduced.

## Verification

The [single authored receipt](evidence/issue-334/capture-flow-labels/receipt.md) records commands and measured outcomes. Fixtures are synthetic. Tests cover each equality field, stable labels, scalar extremes, independent instances, capacity and terminal failure. Compatibility cases feed the real decoder's normalized scalars to the table and the returned ordinal to the real encoder, asserting exact allowed JSON fields. They explicitly supply the opening sequence and do not prove a handshake or packet-to-generation mapping.

## Remaining integration

Opening-SYN admission, reset recognition, completed bidirectional close, overlapping/reused generations, delayed packets and ambiguous sequence reuse are not implemented or selected here. A later implementation must validate lifecycle before storing a new identity and separately resolve each packet to its generation; this table cannot do either. It must never infer complete mapping merely because table lookup or JSON encoding succeeds.

Filtering/acquisition, worker/control integration, audits/clocks, actual-image/live proof and verifier/publication remain incomplete. This component adds no retained-summary path, public field or actual observation. Existing scientific rules, privacy boundaries, scenario results and dispatch requirements remain in force.
