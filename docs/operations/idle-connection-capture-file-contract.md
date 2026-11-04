# Packet-summary file contract

The packet-summary file writer streams the existing derived JSON into an owned staging file and finalizes it only after successful completion, flush and close. A finalized file is local evidence, not a completeness receipt or a published result. Packet acquisition, runtime integration and verifier-controlled publication remain later work.

## Scope and authority

This component implements [Finalize bounded packet-summary files, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6617689106), with the exact flush-evidence qualification from its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6617754758). The [approved capture design](idle-connection-capture-design.md) owns scientific interpretation and runtime bounds. The [core contract](idle-connection-capture-core-contract.md) owns identity syntax, the JSON schema, event/count/envelope checks and stream accounting.

The file wrapper adds ownership and file-operation controls, not captured-evidence judgment. No raw packet buffer or `PrivatePacketHeader` is accepted by its API. It adds no capture route, producer setting, privilege, workflow, cloud transfer or public result.

## API and local paths

The internal `PacketSummaryFileWriter(string directory, PacketSummaryIdentity identity)` owns its staging handle and the existing non-owning core stream writer. Its methods are `TryAppend(PacketSummaryEvent)`, `TryComplete(DateTimeOffset)` and `Dispose()`. Calls are sequential under one owner. It exposes only `State`, `FailureCode`, `CleanupFailed`, `EventCount` and `BytesWritten`.

The caller supplies an existing absolute directory dedicated to this writer. Null, empty, relative and embedded-NUL directory strings fail with `packet_summary_path_invalid` before file operations. Null identity is a programming argument error. Invalid identity uses `packet_identity_invalid` before file creation. Path validation precedes platform validation; unsupported platforms use `packet_summary_platform_unsupported` before creation. Production supports Linux only.

Names are fixed: `packet-summary.json.partial` for staging and `packet-summary.json` for the final file. No identity value is interpolated into a filename. The exact later runtime location is:

| Purpose | Path |
|---|---|
| Dedicated directory | `/work/packet-summary` |
| Staging file | `/work/packet-summary/packet-summary.json.partial` |
| Final local file | `/work/packet-summary/packet-summary.json` |

This component creates no directory or pod volume. The later runtime route owns directory creation, the approved disk-backed work-volume mount and proof of its ownership. Tests substitute isolated temporary directories. No Blob destination, public artifact or permanent retained-summary path is selected here.

The directory and its ancestors are trusted and are not concurrently modified by an adversary. Absolute-path validation does not establish symlink containment or hostile-directory safety. The final destination may become occupied before completion; the native rename operation must reject that collision.

## Ownership and byte guard

The production factory creates the partial with `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.None`, a configured 4096-byte stream buffer and `FileOptions.SequentialScan`. An existing partial file, directory or symlink is an open failure. It is never adopted, overwritten or deleted by this attempt. The component performs no directory enumeration, stale-file recovery or collision retry. A factory that fails after opening a handle must dispose that handle before throwing.

`IPacketSummaryFileFactory` and `IPacketSummaryStagingFile` are the narrow filesystem test seam. The staging object exposes its bounded `Content` stream and separate `FlushToDisk`, `Close`, `FinalizeFile` and `DeletePartial` operations. The wrapper owns a staging object only after successful factory return.

An independent stream guard checks the approved 20971520-byte file cap before forwarding each synchronous array or span write. It uses subtraction-based arithmetic, counts successful writes and permanently refuses writing after a rejection or inner write fault. It supports no read, seek or length-changing operation, whole-file buffering or async production route. An internal test seam permits smaller positive limits up to the production cap; no external configuration can change that cap. A boundary write failure reaches the wrapper through the core's `packet_summary_write_failed` code.

`EventCount` and `BytesWritten` mirror the core when it exists and are zero otherwise. As in the core, bytes written counts successful write calls. A throwing stream can have consumed part of a request, so this value does not certify the actual length of a failed file. Such a stream cannot continue or complete successfully.

## Finalization and cleanup

Completion requires `FileStream.Flush(flushToDisk: true)` to return without throwing, then close and the no-replace rename to succeed. This requests file flushing; it does not prove power-loss durability. The pinned Unix helper ignores EROFS, EINVAL and ENOTSUP from its synchronization call, so successful API return is not an independent storage-durability measurement.

Sources: [.NET 10 FileStream.cs, Flush(bool)](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/System.Private.CoreLib/src/System/IO/FileStream.cs) and [FileStreamHelpers.Unix.cs, FlushToDisk](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/System.Private.CoreLib/src/System/IO/Strategies/FileStreamHelpers.Unix.cs).

The full sequence is core `TryComplete` success, flush, close, native finalization, then wrapper `Completed`. A failure prevents every later finalization stage. Successful core completion or valid JSON alone cannot complete the file wrapper. Neither file flushing nor atomic name visibility proves power-loss durability of the parent directory.

The native boundary calls `renameat2(AT_FDCWD, partialPath, AT_FDCWD, finalPath, RENAME_NOREPLACE)` once. `AT_FDCWD` is -100 and `RENAME_NOREPLACE` is 1. It uses the Linux libc entry point with explicit UTF-8 pathname marshaling. A nonzero return, missing library or missing entry point becomes `packet_summary_finalize_failed`. There is no File.Move, copy, overwrite, retry, shell or broader-privilege fallback. No fallible operation follows successful rename before recording completion.

The [pinned .NET Unix MoveFile implementation](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/System.Private.CoreLib/src/System/IO/FileSystem.Unix.cs) contains rename and link/copy paths, so it is not the selected strict no-copy/no-replace mechanism. The [pinned Linux do_renameat2 implementation](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/fs/namei.c) checks mount mismatch and rejects an occupied destination under its rename lock before calling `vfs_rename`. Inference from that path: successful same-directory finalization makes the completed file visible under its final name without copying partial content into it. Destination collision handling includes regular files, directories and dangling symlinks. The [rename flag](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/include/uapi/linux/fs.h), [directory descriptor constant](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/include/uapi/linux/fcntl.h) and [glibc nonzero-flag path](https://github.com/bminor/glibc/blob/glibc-2.42/sysdeps/unix/sysv/linux/renameat2.c) supply the selected ABI and lack of plain-rename fallback.

These sources do not establish the deployed kernel, libc, seccomp or filesystem support. Unsupported operation fails local finalization. Actual-image and live smoke compatibility remain later proof.

After a fault with staging ownership, cleanup closes the handle if close was not already attempted and attempts deletion of the owned partial once. A failed close is not retried; cleanup still attempts partial deletion and sets `CleanupFailed`. A deletion failure also sets that flag. The first primary failure remains unchanged. Repeated failure/disposal calls perform no more I/O, and the final path is never deleted. Successful rename ends ownership of the partial name.

## States and diagnostics

The wrapper has `Active`, `Completed` and `Faulted` states independently of the core's earlier completion. Dispose while Active faults with `packet_summary_abandoned` and performs bounded cleanup. Dispose after success preserves Completed, its null failure code and final file. Append or completion after Completed returns false and faults with `packet_summary_state_invalid`, leaving the final file intact. Calls after Faulted return false with the original failure and no I/O. Dispose is idempotent.

Additional fixed codes are `packet_summary_path_invalid`, `packet_summary_platform_unsupported`, `packet_summary_open_failed`, `packet_summary_flush_failed`, `packet_summary_close_failed`, `packet_summary_finalize_failed` and `packet_summary_abandoned`. Core failures retain the exact code defined in the core contract. Boundary exceptions, including cancellation, become their stage's fixed code; cleanup faults never replace the first failure. No diagnostic contains a path, exception text, native error detail, address or packet contents. No production logging is added.

## Verification boundary

Unit tests use recording boundary fakes and in-memory streams to check ordering, failure precedence, bounded cleanup, terminal states and the independent byte guard. Integration tests exercise real Linux staging and rename behavior, existing-file/directory/symlink collisions, a destination created after staging begins, abandoned files and small byte limits. Inputs and identity values are synthetic.

Those tests establish local filesystem behavior only. They do not establish process-crash or power-loss behavior, capture privacy, actual-image compatibility, socket ownership, clocks, experimental completeness or Azure behavior. A later consumer must require the appropriate checked completion receipt; file existence alone authorizes no absence statement or public result.
