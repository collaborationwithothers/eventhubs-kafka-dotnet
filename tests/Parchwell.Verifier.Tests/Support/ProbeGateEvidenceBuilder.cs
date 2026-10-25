using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Scenario;
using Parchwell.Verifier.ProbeGate;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Provides independently authored synthetic repeats and observable local stream boundaries.</summary>
internal sealed class ProbeGateEvidenceBuilder : IDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "probe-gate-" + Guid.NewGuid().ToString("N"));
    internal List<string> Events { get; } = [];
    internal List<string> ScenarioOpens { get; } = [];
    internal List<string> InputOpens { get; } = [];
    internal VerifierHostOptions Options { get; }
    internal GateClock Clock { get; } = new();
    internal Dictionary<string, byte[]> Inputs { get; } = new(StringComparer.Ordinal);
    internal Func<string, Stream>? InputStream { get; set; }
    internal Func<Stream>? OutputStream { get; set; }
    internal byte[]? Output { get; private set; }
    internal int Creates { get; private set; }

    internal ProbeGateEvidenceBuilder()
    {
        string repository = FindRepository();
        Directory.CreateDirectory(Path.Combine(Root, "scenarios", "A01"));
        Directory.CreateDirectory(Path.Combine(Root, "scenarios", "profiles"));
        File.Copy(Path.Combine(repository, "scenarios", "A01", "scenario.yaml"), Path.Combine(Root, "scenarios", "A01", "scenario.yaml"));
        File.Copy(Path.Combine(repository, "scenarios", "profiles", "saturation-ramp.yaml"), Path.Combine(Root, "scenarios", "profiles", "saturation-ramp.yaml"));
        Options = new()
        {
            Mode = "probe-gate", ScenarioId = "A01", VerifierCommit = "synthetic-gate-commit", SourceCommit = "synthetic-gate-commit",
            ProcessLifetime = TimeSpan.FromSeconds(30), CleanupTimeout = TimeSpan.FromSeconds(1), IoTimeout = TimeSpan.FromSeconds(5),
            MaxOutputBytes = 100_000, MaxDiagnosticsPerCheck = 4, MaxDiagnosticBytes = 512,
            MaxArtifactBytes = 100_000, MaxTotalInputBytes = 1_000_000, MaxInputDepth = 16,
            ScenarioRoot = Root, GateOutputPath = Path.Combine(Root, "gate.json"),
            ScenarioLoadOptions = new() { MaxDocumentCharacters = 16_384, MaxNestingDepth = 32, MaxScalarCharacters = 2048, MaxParserEvents = 2000 },
            ExpectedRepeats = Enumerable.Range(1, 5).Select(index => Expected(Identity(index))).ToList(),
            RepeatResultPaths = Enumerable.Range(1, 5).Select(index => Path.Combine(Root, $"probe-{index}.json")).ToList()
        };
        for (int index = 0; index < 5; index++) Inputs[Options.RepeatResultPaths[index]] = RawResult(Identity(index + 1), qualifying: index == 0);
    }

    internal static RunIdentity Identity(int index) => new("A01", "C0", $"probe-{index}", $"instance-{index}", $"instance-{index}", "synthetic-producer");
    internal static ExpectedProbeRepeat Expected(RunIdentity identity) => new()
    {
        ScenarioId = identity.ScenarioId, ConfigurationId = identity.ConfigurationId, RunId = identity.RunId,
        InstanceId = identity.InstanceId, ExpectedInstanceId = identity.ExpectedInstanceId, ClientId = identity.ClientId
    };

    // Raw result bytes do not use the production recorder or result writer.
    internal static byte[] RawResult(RunIdentity identity, bool usable = true, bool qualifying = false,
        bool includeObservation = true, long maximum = 0, string diagnostics = "[]", string? extraObservation = null)
    {
        string observation = includeObservation ? ",\"probe_observation\":{\"usable\":" + Bool(usable) +
            ",\"qualifying\":" + Bool(qualifying) + ",\"broker\":\"synthetic-long-broker-reference\",\"report_index\":3,\"window_count\":1,\"window_maximum\":" + maximum.ToString(System.Globalization.CultureInfo.InvariantCulture) + extraObservation + "}" : "";
        return Encoding.UTF8.GetBytes("{\"schema_version\":1,\"identity\":" + IdentityJson(identity) +
            ",\"verifier_commit\":\"synthetic-repeat-commit\",\"verifier_confluent_kafka_version\":\"synthetic\",\"verifier_librdkafka_version\":\"synthetic\",\"status\":\"invalid\",\"failed_checks\":[12],\"judged_file_hashes\":[],\"diagnostics\":" + diagnostics + observation + "}");
    }

    internal static VerifierResult Result(RunIdentity identity, bool usable = true, bool qualifying = false,
        bool includeObservation = true, long maximum = 0) => new(1, identity, null, null,
        "synthetic", "synthetic", "synthetic", VerifierResultStatus.Invalid, [12], null, null, null, null,
        null, null, null, includeObservation ? new(usable, qualifying, "broker", 3, 1, maximum) : null, null, [], []);

    internal static string IdentityJson(RunIdentity identity) => JsonSerializer.Serialize(new
    {
        scenario_id = identity.ScenarioId, configuration_id = identity.ConfigurationId, run_id = identity.RunId,
        instance_id = identity.InstanceId, expected_instance_id = identity.ExpectedInstanceId, client_id = identity.ClientId
    });
    private static string Bool(bool value) => value ? "true" : "false";

    internal ProbeGateHandler Handler() => new(Microsoft.Extensions.Options.Options.Create(Options), Clock,
        new ScenarioLoader(new TrackingScenarioSource(this), Microsoft.Extensions.Options.Options.Create(new ScenarioLoadOptions
        { MaxDocumentCharacters = 16_384, MaxNestingDepth = 32, MaxScalarCharacters = 2048, MaxParserEvents = 2000 })), new TrackingFiles(this));

    internal Task<int> RunHostAsync() => Program.RunAsync(Arguments(), services =>
    {
        services.RemoveAll<IProbeGateFileStore>();
        services.AddSingleton<IProbeGateFileStore>(new TrackingFiles(this));
        services.RemoveAll<IScenarioSource>();
        services.AddSingleton<IScenarioSource>(new TrackingScenarioSource(this));
    }, Clock);

    internal string[] Arguments() => JsonOptionsArguments(Options);

    internal static string[] JsonOptionsArguments(VerifierHostOptions options)
    {
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(options));
        var arguments = new List<string>();
        Append(document.RootElement, "Verifier");
        return arguments.ToArray();
        void Append(JsonElement value, string path)
        {
            if (value.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty property in value.EnumerateObject()) Append(property.Value, path + ":" + property.Name);
            else if (value.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in value.EnumerateArray()) Append(item, path + ":" + index++);
            }
            else if (value.ValueKind != JsonValueKind.Null) arguments.Add("--" + path + "=" + value.ToString());
        }
    }

    internal void WriteLocalInputs()
    {
        foreach ((string path, byte[] bytes) in Inputs) File.WriteAllBytes(path, bytes);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private sealed class TrackingScenarioSource(ProbeGateEvidenceBuilder owner) : IScenarioSource
    {
        private readonly FileSystemScenarioSource _source = new(owner.Root);
        public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
        {
            owner.ScenarioOpens.Add(logicalPath);
            return _source.OpenReadAsync(logicalPath, cancellationToken);
        }
    }

    private sealed class TrackingFiles(ProbeGateEvidenceBuilder owner) : IProbeGateFileStore
    {
        public ValueTask<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.InputOpens.Add(path);
            owner.Events.Add("open:" + path);
            Stream stream = owner.InputStream?.Invoke(path) ?? (owner.Inputs.TryGetValue(path, out byte[]? bytes)
                ? new MemoryStream(bytes, writable: false) : throw new FileNotFoundException("synthetic missing input"));
            return ValueTask.FromResult<Stream>(new ObservedStream(stream, owner, path, output: false));
        }
        public ValueTask<Stream> CreateNewAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.Creates++;
            owner.Events.Add("create:" + path);
            return ValueTask.FromResult<Stream>(new ObservedStream(owner.OutputStream?.Invoke() ?? new MemoryStream(), owner, path, output: true));
        }
    }

    private sealed class ObservedStream(Stream inner, ProbeGateEvidenceBuilder owner, string path, bool output) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            owner.Events.Add("read:" + path);
            return inner.ReadAsync(buffer, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            owner.Events.Add("write:" + path);
            return inner.WriteAsync(buffer, cancellationToken);
        }
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override async ValueTask DisposeAsync()
        {
            owner.Events.Add("dispose:" + path);
            if (output && inner is MemoryStream memory) owner.Output = memory.ToArray();
            try { await inner.DisposeAsync(); }
            finally { await base.DisposeAsync(); }
            GC.SuppressFinalize(this);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class GateClock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
        internal void Advance(TimeSpan duration) => _ticks += duration.Ticks;
    }

    internal sealed class FaultStream : MemoryStream
    {
        private readonly Action? read;
        private readonly Action? cleanup;
        private readonly Action? write;
        internal FaultStream(byte[] bytes, Action? read = null, Action? cleanup = null, Action? write = null)
        {
            base.Write(bytes);
            Position = 0;
            this.read = read;
            this.cleanup = cleanup;
            this.write = write;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            read?.Invoke();
            return base.ReadAsync(buffer, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            write?.Invoke();
            return base.WriteAsync(buffer, cancellationToken);
        }
        public override ValueTask DisposeAsync()
        {
            cleanup?.Invoke();
            return base.DisposeAsync();
        }
    }
}
