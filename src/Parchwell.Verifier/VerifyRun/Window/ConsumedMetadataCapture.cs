using System.Buffers;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.Window;

internal sealed class ConsumedMetadataOptions
{
    public bool? Enabled { get; set; }
    public long? MaxRowBytes { get; set; }
    public long? MaxChunkBytes { get; set; }
    public long? MaxBytes { get; set; }
}

// A capture failure never changes a scientific assessment. Its missing manifest or sticky gap
// is independently enforced by the archive inventory.
internal sealed class ConsumedMetadataCapture
{
    private readonly VerifierHostOptions _options;
    private readonly IVerifierArtifactStore _store;
    private readonly RunWindow _window;
    private readonly long _index;
    private readonly string _kind;
    private readonly int? _repeat;
    private readonly string _id;
    private readonly string _prefix;
    private readonly Func<Stream> _openChunk;
    private readonly List<object> _chunks = [];
    private readonly List<object> _gaps = [];
    private Stream? _file;
    private long _chunkRows, _observed, _captured, _bytes;
    private long? _firstMissing;

    private ConsumedMetadataCapture(VerifierHostOptions options, IVerifierArtifactStore store, RunWindow window,
        long index, string kind, int? repeat, Func<Stream>? openChunk)
    {
        _options = options; _store = store; _window = window; _index = index; _kind = kind; _repeat = repeat;
        _openChunk = openChunk ?? (() => new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose));
        _id = PhasePaths.ConsumedCaptureId(options.Mode!, options.JobName!, options.PodUid!, index);
        _prefix = PhasePaths.ConsumedCapturePrefix(options.RunId!, options.Phase!,
            options.Mode is "b10-sweep" or "b10-final-sweep" ? null : repeat, _id);
    }

    internal static ConsumedMetadataCapture? TryStart(VerifierHostOptions options, IVerifierArtifactStore store,
        RunWindow window, long index, string kind, int? repeat, Func<Stream>? openChunk = null)
    {
        try { return options.ConsumedMetadata?.Enabled == true ? new(options, store, window, index, kind, repeat, openChunk) : null; }
        catch (Exception) { return null; }
    }

    internal void Observe(ConsumeResult<byte[], byte[]> record)
    {
        long ordinal = _observed;
        try { _observed = checked(_observed + 1); }
        catch (OverflowException) { Gap("byte_limit", ordinal); return; }
        if (_firstMissing is not null) return;
        long checkpoint = _file?.Length ?? 0;
        try
        {
            // Reject large base64 sources before allocating decoded identity strings or JSON buffers.
            long minimum = 0;
            static long Base64Length(long n) => checked(((n + 2) / 3) * 4);
            if (record.Message?.Key is { } key) minimum = checked(minimum + Base64Length(key.LongLength));
            if (minimum > _options.ConsumedMetadata!.MaxRowBytes!.Value ||
                record.Topic?.Length > _options.ConsumedMetadata.MaxRowBytes.Value)
            { Gap("row_limit", ordinal); return; }
            if (record.Message?.Headers is { } headers)
                foreach (IHeader header in headers)
                {
                    if (header.Key.Length > _options.ConsumedMetadata.MaxRowBytes.Value) { Gap("row_limit", ordinal); return; }
                    minimum = checked(minimum + 4L + Encoding.UTF8.GetByteCount(header.Key));
                    if (header.GetValueBytes() is { } value) minimum = checked(minimum + Base64Length(value.LongLength));
                    if (minimum > _options.ConsumedMetadata.MaxRowBytes.Value) { Gap("row_limit", ordinal); return; }
                }
            if (minimum > _options.ConsumedMetadata!.MaxRowBytes!.Value) { Gap("row_limit", ordinal); return; }
            var decoded = MessageIdentityReader.Read(record);
            // Count actual JSON-escaped text, including the duplicate decoded projection,
            // before asking the JSON writer to allocate its per-string scratch buffer.
            static long EscapedBytes(string? text)
            {
                if (text is null) return 0;
                ReadOnlySpan<char> input = text.AsSpan();
                Span<char> output = stackalloc char[256];
                long total = 0;
                while (!input.IsEmpty)
                {
                    OperationStatus status = JavaScriptEncoder.Default.Encode(input, output, out int consumed, out int written, isFinalBlock: true);
                    total = checked(total + Encoding.UTF8.GetByteCount(output[..written]));
                    input = input[consumed..];
                    if (status == OperationStatus.InvalidData || consumed == 0 && written == 0)
                        throw new InvalidDataException("Cannot encode metadata text.");
                }
                return total;
            }
            long encoded = minimum;
            encoded = checked(encoded + EscapedBytes(record.Topic) + EscapedBytes(_id) + EscapedBytes(decoded.Key) +
                EscapedBytes(decoded.InstanceId) + EscapedBytes(decoded.AttemptId));
            if (record.Message?.Headers is { } checkedHeaders)
                foreach (IHeader header in checkedHeaders)
                    encoded = checked(encoded + EscapedBytes(header.Key) - Encoding.UTF8.GetByteCount(header.Key));
            foreach (var problem in decoded.Problems)
                encoded = checked(encoded + EscapedBytes(problem.Field) + EscapedBytes(problem.Code));
            if (encoded > _options.ConsumedMetadata.MaxRowBytes.Value) { Gap("row_limit", ordinal); return; }
            using var row = new MemoryStream();
            using (var bounded = new BoundedRowStream(row, _options.ConsumedMetadata.MaxRowBytes.Value - 1))
            using (var writer = new Utf8JsonWriter(bounded))
            {
                writer.WriteStartObject(); writer.WriteNumber("schema_version", 1); writer.WriteString("capture_id", _id);
                writer.WriteNumber("ordinal", ordinal); writer.WriteString("topic", record.Topic);
                writer.WriteNumber("partition", record.Partition.Value); writer.WriteNumber("offset", record.Offset.Value);
                writer.WriteStartObject("timestamp");
                Timestamp timestamp = record.Message?.Timestamp ?? Timestamp.Default;
                writer.WriteString("type", timestamp.Type switch { TimestampType.CreateTime => "create_time", TimestampType.LogAppendTime => "log_append_time", _ => "not_available" });
                if (timestamp.Type is not (TimestampType.CreateTime or TimestampType.LogAppendTime)) writer.WriteNull("unix_milliseconds"); else writer.WriteNumber("unix_milliseconds", timestamp.UnixTimestampMs);
                writer.WriteEndObject();
                if (record.Message?.Key is { } rawKey) writer.WriteBase64String("key_base64", rawKey); else writer.WriteNull("key_base64");
                writer.WriteString("key", decoded.Key); writer.WriteString("instance_id", decoded.InstanceId); writer.WriteString("attempt_id", decoded.AttemptId);
                if (decoded.Sequence is { } sequence) writer.WriteNumber("sequence", sequence); else writer.WriteNull("sequence");
                writer.WriteStartArray("headers");
                if (record.Message?.Headers is { } rawHeaders) foreach (IHeader header in rawHeaders)
                {
                    writer.WriteStartObject(); writer.WriteString("name", header.Key);
                    if (header.GetValueBytes() is { } value) writer.WriteBase64String("value_base64", value); else writer.WriteNull("value_base64");
                    writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteStartObject("payload");
                byte[]? payload = record.Message?.Value; writer.WriteBoolean("is_null", payload is null);
                if (payload is null) { writer.WriteNull("length"); writer.WriteNull("sha256"); }
                else { writer.WriteNumber("length", payload.LongLength); writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(payload))); }
                writer.WriteEndObject(); writer.WriteStartArray("identity_problems");
                foreach (var problem in decoded.Problems) { writer.WriteStartObject(); writer.WriteString("field", problem.Field); writer.WriteString("code", problem.Code); writer.WriteEndObject(); }
                writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
            }
            long length = checked(row.Length + 1);
            if (length > _options.ConsumedMetadata.MaxChunkBytes!.Value || length > _options.ConsumedMetadata.MaxBytes!.Value - _bytes)
            { Gap("byte_limit", ordinal); return; }
            if (_file is not null && length > _options.ConsumedMetadata.MaxChunkBytes.Value - _file.Length) { Seal(); checkpoint = 0; }
            if (_firstMissing is not null) return;
            // The phase inventory reserves two entries for archive controls; a single capture
            // cannot publish more data objects than the entire inventory can admit.
            if (_file is null && _chunks.Count >= 1021) { Gap("byte_limit", ordinal); return; }
            _file ??= _openChunk();
            row.Position = 0; row.CopyTo(_file); _file.WriteByte((byte)'\n'); _chunkRows++; _captured++; _bytes = checked(_bytes + length);
        }
        catch (RowLimitException) { Gap("row_limit", ordinal); }
        catch (OverflowException) { Gap("byte_limit", ordinal); }
        catch (Exception)
        {
            try { if (_file is not null) { _file.SetLength(checkpoint); _file.Position = checkpoint; } }
            catch (Exception) { long first = _captured - _chunkRows; _captured = first; _chunkRows = 0; _file?.Dispose(); _file = null; Gap("write_failed", first); }
            Gap("write_failed", ordinal);
        }
    }

    private void Seal()
    {
        if (_file is null) return;
        if (_chunkRows == 0) { _file.Dispose(); _file = null; return; }
        long first = _captured - _chunkRows;
        try
        {
            _file.Flush(); long length = _file.Length; _file.Position = 0;
            string sha = Convert.ToHexStringLower(SHA256.HashData(_file)); _file.Position = 0;
            string name = _prefix + $"chunk-{_chunks.Count + 1:D6}.jsonl";
            using var timeout = new CancellationTokenSource(_options.BlobIoTimeout!.Value);
            _store.CreateStreamAsync(name, _file, timeout.Token).GetAwaiter().GetResult();
            _chunks.Add(new { name, bytes = length, sha256 = sha, rows = _chunkRows });
        }
        catch (Exception) { _captured = first; Gap("upload_failed", first); }
        finally { _file.Dispose(); _file = null; _chunkRows = 0; }
    }

    private void Gap(string code, long? ordinal)
    {
        if (ordinal is not null && (_firstMissing is null || ordinal.Value < _firstMissing.Value)) _firstMissing = ordinal;
        if (_gaps.Count < 100) _gaps.Add(new { code, ordinal });
    }

    internal void Finish(bool sourceComplete, bool interrupted = false)
    {
        if (!sourceComplete) Gap(interrupted ? "interrupted" : "source_window_incomplete", _observed);
        Seal();
        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version = 1, capture_id = _id,
                identity = new { run_id = _options.RunId, phase = _options.Phase, scenario_id = _options.ScenarioId, configuration_id = _options.ConfigurationId, job_name = _options.JobName, pod_uid = _options.PodUid, source_commit = _options.SourceCommit },
                window = new { index = _index, kind = _kind, repeat = _repeat, topic = RunWindow.Topic,
                    start = _window.Partitions.OrderBy(p => p.Partition).Select(p => new { partition = p.Partition, offset = p.Start }).ToArray(),
                    end = _window.Partitions.OrderBy(p => p.Partition).Select(p => new { partition = p.Partition, offset = p.End }).ToArray() },
                chunks = _chunks, observed_rows = _observed, captured_rows = _captured,
                complete = sourceComplete && _gaps.Count == 0 && _observed == _captured,
                first_missing_ordinal = _firstMissing, gaps = _gaps
            });
            using var timeout = new CancellationTokenSource(_options.BlobIoTimeout!.Value);
            _store.CreateAsync(_prefix + "capture.json", json, timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception) { /* Missing capture.json is an archive gap. */ }
    }

    private sealed class RowLimitException : Exception { }
    private sealed class BoundedRowStream(Stream inner, long limit) : Stream
    {
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) { if (buffer.Length > limit - inner.Length) throw new RowLimitException(); inner.Write(buffer); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
