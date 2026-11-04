# Packet-summary event queue contract

The packet-summary queue holds at most 256 already-derived events and rejects further input when that bound is exhausted. Successful queue completion means only that sealed input has been removed from the queue. It does not establish successful file finalization or complete packet evidence.

## Scope and authority

This component implements [Bound the packet-summary event queue, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6623743554), approved under standing approval by its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6623779978). The [approved design](idle-connection-capture-design.md), Selected resource and capture bounds, owns the queue policy and evidence interpretation. The [core contract](idle-connection-capture-core-contract.md) owns derived event syntax, serialization and the writer's total bounds. The [file contract](idle-connection-capture-file-contract.md) owns local staging and finalization.

The earlier core and file contracts describe their own staged PR boundaries. This queue adds another component; no executable observer, packet source or production pump exists in this change. It accepts only the immutable `PacketSummaryEvent` value, never raw buffers or `PrivatePacketHeader`. It adds no privilege, runtime route, I/O, logging, scientific decision or publication path.

## API and resident bound

The internal `PacketSummaryEventQueue()` uses `MaximumEntries = 256`. An internal test constructor takes a capacity from 1 through 256 and throws `ArgumentOutOfRangeException` outside that range, before allocating the ring. There is no production override or external option.

The API exposes immutable `Capacity` and individually synchronized `Count`, `State` and `FailureCode` properties. Operations are `TryEnqueue(PacketSummaryEvent)`, `TryDequeue(out PacketSummaryEvent)`, `TryCompleteAdding()` and `Abort()`. It owns no native/file resource and implements no disposal protocol.

A fixed `PacketSummaryEvent[capacity]` ring holds resident values. It never resizes or retains encoded output. Removal clears the slot and advances the head with wraparound, preserving first-in-first-out order. Count means resident entries, not total traffic. An accepted event can leave the ring and free capacity for another event; the existing writer separately enforces 65536 total events.

Admission uses the real `PacketSummaryEncoding.TryEncodeEvent` under the queue lock. Its temporary encoding validates existing syntax and the 256-byte serialized-event cap, then is discarded. The later writer re-encodes the unchanged value. This serialized bound is not a measurement of managed object size or total process memory; a dequeued consumer-held value and temporary encoder memory are not resident ring entries. No memory or throughput measurement is claimed.

## States, results and precedence

The initial state is Active, Count is zero and FailureCode is null. The queue states are `Active`, `Draining`, `Completed` and `Faulted`; dequeue results are `Item`, `Empty`, `Completed` and `Faulted`.

| Operation and state | Result and transition |
|---|---|
| Enqueue in Active | Validate encoding, then available capacity; append and return true only if both pass. |
| Enqueue in Draining or Completed | Return false and fault with `packet_queue_state_invalid`; clear any remainder. |
| Enqueue in Faulted | Return false unchanged. |
| Dequeue in Active with zero Count | Return Empty and default out value; remain Active. Empty is not end-of-input. |
| Dequeue in Active with items | Return the oldest Item and remove it; remain Active. |
| Dequeue in Draining | Return the oldest Item and remove it; removing the final Item also sets Completed. That Item still needs downstream processing. |
| Dequeue in Completed or Faulted | Return the matching terminal result and default out value, unchanged. |
| Seal in Active | Return true; become Draining with resident items, otherwise Completed. |
| Seal in Draining or Completed | Return true unchanged; no item is consumed. |
| Seal in Faulted | Return false unchanged. |
| Abort in Active or Draining | Fault with `packet_queue_abandoned` and clear resident values. |
| Abort in Completed or Faulted | Do nothing; preserve completion or the first failure. |

Admission precedence is state, then real encoding, then resident capacity. Invalid syntax on a full Active queue therefore preserves the encoder's `packet_event_invalid`; after sealing, even an invalid event produces `packet_queue_state_invalid`. Other encoder failures retain their original fixed code, including `packet_event_bytes_exceeded`. Valid input when Count equals Capacity produces `packet_queue_capacity_exceeded`. There is no wait-for-space, sampling or eviction to admit another value.

Every fault preserves the first fixed failure, sets Faulted, clears the bounded ring and resets its head/count. Nothing emits another Item after that transition. Discarding a failed remainder is not successful draining or lossless capture. No diagnostic contains a raw event value, path or exception message. Unrelated process-fatal allocation exceptions are not caught or represented as recoverable queue failures.

## Concurrency and ownership

One private lock protects every stateful operation and property read; Capacity is immutable. Encoding executes inside that lock, so validation and admission form one queue operation. The intended future runtime has one event producer and one consumer, with safe overlapping calls. There is no background task, async method, callback, channel, semaphore or I/O in the production queue.

Lock acquisition can wait for another short operation. This is not a lock-free or scheduling-latency claim. Separate property reads do not form an atomic multi-property snapshot. Consumers must use the dequeue result rather than reading Count to predict whether dequeue will succeed.

## Future runtime and evidence boundary

The future runtime must stop its producer on queue failure, preserve the fixed diagnostic in its private receipt, abandon an unfinished summary and never convert failed queue contents into a successful capture receipt. A separately checked runtime plan must specify producer shutdown/join, draining and writer completion, including processing the final dequeued Item before completing the file. Queue Completed alone cannot authorize file completion.

This PR writes no control/receipt file at runtime and changes no existing local storage path. The verifier alone will judge experimental completeness and authorize public fields under its separately checked plan. Filtering/acquisition, flow generation, clocks/audits, actual-image compatibility and live proof remain unfinished work.

## Verification boundary

Tests use synthetic derived events and in-memory streams. They cover constructor and resident limits, FIFO/wraparound, precedence, first-failure clearing, sealing, abandonment and exact value preservation. Real encoder outputs are compared before/after queueing; a composition test sends dequeued Items to the real stream writer and parses its output, asserting queue completion separately from writer completion.

A bounded cross-task test coordinates one producer and one consumer for a fixed sequence, with cancellation and no polling or sleeps. It tests the actual queue across tasks without measuring throughput or proving every scheduler interleaving. These tests do not prove packet filtering, socket acquisition, runtime shutdown, downstream failure propagation or live completeness. The [authored receipt](evidence/issue-334/capture-queue/receipt.md) records actual verification evidence.
