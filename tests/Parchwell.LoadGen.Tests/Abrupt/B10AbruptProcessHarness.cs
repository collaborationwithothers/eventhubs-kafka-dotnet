using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Parchwell.LoadGen.Run.Abrupt;

namespace Parchwell.LoadGen.Tests.Abrupt;

internal static class B10AbruptProcessHarness
{
    internal sealed record Control(string Mode, string StorageRoot, string BootstrapServers = "", bool RealTime = false);
    internal static void Install(string directory)
    {
        string uid = File.ReadLines("/proc/self/status").Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[1];
        File.AppendAllText(Path.Combine(directory, "hook-loaded.jsonl"), JsonSerializer.Serialize(new
        {
            role = Environment.GetEnvironmentVariable("B10Abrupt__Role"), pid = Environment.ProcessId,
            uid = int.Parse(uid, CultureInfo.InvariantCulture), process_path = Environment.ProcessPath,
            loadgen_assembly = typeof(B10AbruptHost).Assembly.Location
        }) + "\n");
        if (Environment.GetEnvironmentVariable("B10Abrupt__Role") == "child")
            File.AppendAllText(Path.Combine(directory, "child-pids.txt"), Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "\n");
        Control control = JsonSerializer.Deserialize<Control>(File.ReadAllBytes(Path.Combine(directory, "test-control.json")))!;
        if (control.Mode == "sleep")
        {
            File.WriteAllText(Path.Combine(directory, "child.pid"), Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            Thread.Sleep(TimeSpan.FromSeconds(30));
            Environment.Exit(0);
        }
        if (control.Mode == "signal-failed") B10AbruptHost.SelfKillOverride = () => (-1, 1);
        if (control.Mode == "real-kafka" && control.RealTime && control.BootstrapServers.Length != 0 &&
            Environment.GetEnvironmentVariable("B10Abrupt__Role") == "parent")
        {
            var capture = new LocalCgroupCapture();
            B10AbruptHost.CaptureBeforeOverride = capture.Before;
            B10AbruptHost.CaptureAfterOverride = capture.After;
        }
        var clock = new ProcessClock();
        B10AbruptHost.ConfigureTestServices = services =>
        {
            if (control.Mode != "host-cross-failure")
            {
                services.RemoveAll<IB10AbruptStorage>();
                services.AddSingleton<IB10AbruptStorage>(new FileStorage(control.StorageRoot));
                if (!control.RealTime && control.Mode != "image-kafka")
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton<TimeProvider>(clock);
                }
            }
        };
        B10AbruptHost.ProducerFactoryOverride = (configuration, statistics, error) =>
        {
            if (control.BootstrapServers.Length != 0)
            {
                var local = new ProducerConfig(configuration);
                local.BootstrapServers = control.BootstrapServers;
                local.SecurityProtocol = SecurityProtocol.Plaintext;
                local.SaslMechanism = null;
                local.SaslOauthbearerConfig = null;
                return new ProducerBuilder<byte[], byte[]>(local).SetStatisticsHandler(statistics).SetErrorHandler(error).Build();
            }
            IProducer<byte[], byte[]> producer = DispatchProxy.Create<IProducer<byte[], byte[]>, RecordingProducer>();
            var recording = (RecordingProducer)(object)producer;
            recording.Initialize(directory, control, producer, statistics, control.Mode == "host-cross-failure" ? TimeProvider.System : clock);
            return producer;
        };
    }

    // This startup-hook fixture can prove the real local membership when this workspace's
    // cgroup namespace exposes / while its cgroup2 mount root is "/..". Production Resolve stays closed.
    internal sealed class LocalCgroupCapture(B10AbruptProcess.ProcessIdentity? processIdentity = null)
    {
        private B10AbruptProcess.ProcessIdentity? observer = processIdentity;

        internal B10AbruptCgroup.Observation Before()
        {
            observer = B10AbruptProcess.ReadIdentity(Environment.ProcessId);
            B10AbruptCgroup.Observation normal;
            try { normal = B10AbruptCgroup.CaptureBefore(observer); }
            catch (InvalidOperationException exception) when (exception.Message == "b10_abrupt_cgroup_unavailable")
            {
                string proc = "/proc/" + observer.Pid.ToString(CultureInfo.InvariantCulture);
                if (!IsNamespaceMismatch(Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(proc + "/cgroup")),
                    Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(proc + "/mountinfo")))) throw;
                return CaptureCandidates(observer, "/sys/fs/cgroup", "/sys/fs/cgroup/init");
            }
            B10AbruptCgroup.Observation proven = CaptureMember(normal.CgroupPath, observer);
            _ = B10AbruptCgroup.Compare(normal, proven);
            return proven;
        }

        internal B10AbruptCgroup.Observation After(B10AbruptCgroup.Observation before)
        {
            B10AbruptProcess.ProcessIdentity process = observer ?? throw Unavailable();
            B10AbruptCgroup.Observation proven = CaptureMember(before.CgroupPath, process);
            _ = B10AbruptCgroup.Compare(before, proven);
            B10AbruptCgroup.Observation after = B10AbruptCgroup.CaptureAfter(before);
            _ = B10AbruptCgroup.Compare(proven, after);
            _ = B10AbruptCgroup.Compare(after, CaptureMember(before.CgroupPath, process));
            return after;
        }

        internal static B10AbruptCgroup.Observation CaptureMember(string directory, B10AbruptProcess.ProcessIdentity process)
        {
            if (B10AbruptProcess.ReadIdentity(process.Pid) != process) throw Unavailable();
            B10AbruptCgroup.Observation captured = B10AbruptCgroup.Capture(directory, process.BootId, descriptor =>
                ValidateMembership(Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(descriptor + "/cgroup.procs")),
                    Encoding.ASCII.GetString(B10AbruptProc.ReadBounded(descriptor + "/cgroup.type")), process.Pid));
            if (B10AbruptProcess.ReadIdentity(process.Pid) != process) throw Unavailable();
            return captured;
        }

        internal static B10AbruptCgroup.Observation CaptureCandidates(B10AbruptProcess.ProcessIdentity process, string root, string init)
        {
            B10AbruptCgroup.Observation? found = null;
            foreach (string candidate in new[] { root, init })
            {
                B10AbruptCgroup.Observation captured;
                try { captured = CaptureMember(candidate, process); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException ||
                    exception is InvalidOperationException { Message: "b10_abrupt_cgroup_unavailable" }) { continue; }
                if (found is not null) throw Unavailable();
                found = captured;
            }
            return found ?? throw Unavailable();
        }

        internal static void ValidateMembership(string members, string type, int pid)
        {
            if (type.Trim() != "domain") throw Unavailable();
            bool found = false;
            foreach (string member in members.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(member, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value <= 0) throw Unavailable();
                if (value == pid) found = true;
            }
            if (!found) throw Unavailable();
        }

        internal static bool IsNamespaceMismatch(string membership, string mountInfo)
        {
            if (membership.TrimEnd('\n') != "0::/") return false;
            string[][] mounts = mountInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => Array.IndexOf(parts, "-") is var separator && separator >= 6 &&
                    separator + 3 < parts.Length && parts[separator + 1] == "cgroup2").ToArray();
            return mounts.Length == 1 && mounts[0][3] == "/.." && mounts[0][4] == "/sys/fs/cgroup";
        }

        private static InvalidOperationException Unavailable() => new("b10_abrupt_cgroup_unavailable");
    }

    internal sealed class FileStorage(string root) : IB10AbruptStorage
    {
        public async Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken)
        {
            string path = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await stream.WriteAsync(bytes, cancellationToken);
            stream.Flush(true);
        }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) =>
            B10AbruptPublisher.ReadFileAsync(Path.Combine(root, name), maximumBytes, cancellationToken);
    }

    internal sealed class ProcessClock : TimeProvider
    {
        private long offset;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => checked((long)((Int128)Stopwatch.GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency) + Interlocked.Read(ref offset));
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow().AddTicks(Interlocked.Read(ref offset));
        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref offset, elapsed.Ticks);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            // Only the synthetic process tests advance release time. The Kafka run keeps the real 120 s release.
            if (dueTime >= TimeSpan.FromSeconds(100))
            {
                Advance(dueTime);
                dueTime = TimeSpan.FromMilliseconds(1);
            }
            return TimeProvider.System.CreateTimer(callback, state, dueTime, period);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "The native producer interface Dispose invocation releases the recording timer through DispatchProxy.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852", Justification = "DispatchProxy generates a subclass of this type.")]
    public class RecordingProducer : DispatchProxy
    {
        private string directory = "";
        private Control control = null!;
        private readonly List<Action<DeliveryReport<byte[], byte[]>>> pending = [];
        private readonly object statisticsGate = new();
        private Timer? reports;
        private TimeProvider clock = TimeProvider.System;
        private long producerCreatedMicroseconds;
        internal void Initialize(string output, Control setup, IProducer<byte[], byte[]> producer,
            Action<IProducer<byte[], byte[]>, string> statistics, TimeProvider timeProvider)
        {
            directory = output; control = setup; clock = timeProvider;
            producerCreatedMicroseconds = checked((long)((Int128)clock.GetTimestamp() * 1000000 / clock.TimestampFrequency));
            reports = new Timer(_ =>
            {
                lock (statisticsGate) statistics(producer, RawStatistics());
            }, null, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20));
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "Produce":
                    var message = (Message<byte[], byte[]>)args![1]!;
                    var callback = (Action<DeliveryReport<byte[], byte[]>>)args[2]!;
                    string key = Encoding.UTF8.GetString(message.Key);
                    string attempt = Encoding.UTF8.GetString(message.Headers.GetLastBytes("attempt-id"));
                    int ordinal = int.Parse(Encoding.UTF8.GetString(message.Headers.GetLastBytes("sequence")), CultureInfo.InvariantCulture);
                    bool cross = key.EndsWith("/cross_restart", StringComparison.Ordinal);
                    if (ordinal == -1) { callback(Delivered()); return null; }
                    Record(cross ? "cross_produce" : "control_produce");
                    if (cross && control.Mode == "host-cross-failure" && ordinal == 4)
                        throw new ProduceException<byte[], byte[]>(new Error(ErrorCode.Local_QueueFull), new DeliveryResult<byte[], byte[]>());
                    if (!cross)
                    {
                        if (control.Mode == "empty" || ordinal < 4)
                        {
                            callback(Delivered());
                            WaitForTerminal(attempt);
                        }
                        else if (pending.Count == 0) pending.Add(callback);
                        else
                        {
                            foreach (var original in pending) original(Delivered());
                            pending.Clear();
                            callback(Delivered());
                        }
                    }
                    return null;
                case "Flush":
                    Record("Flush");
                    if (control.Mode == "incomplete")
                    {
                        if (clock is ProcessClock advancing) advancing.Advance(TimeSpan.FromSeconds(301));
                        return 1;
                    }
                    foreach (var original in pending) original(Delivered());
                    pending.Clear();
                    return 0;
                case "Dispose": Record("Dispose"); reports?.Dispose(); return null;
                case "get_Name": return "synthetic-native";
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
        private void Record(string value) => File.AppendAllText(Path.Combine(directory, "producer-calls.txt"), value + "\n");
        private void WaitForTerminal(string attempt)
        {
            string path = Path.Combine(directory, "abrupt", "journal.jsonl");
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (File.Exists(path) && File.ReadLines(path).Any(line => line.Contains("\"kind\":\"terminal\"", StringComparison.Ordinal) && line.Contains(attempt, StringComparison.Ordinal))) return;
                Thread.Sleep(1);
            }
            throw new TimeoutException("synthetic_terminal_not_written");
        }
        private string RawStatistics()
        {
            long micros = checked((long)((Int128)clock.GetTimestamp() * 1000000 / clock.TimestampFrequency));
            return JsonSerializer.Serialize(new
            {
                name = "synthetic-native", client_id = Environment.GetEnvironmentVariable("B10Abrupt__PodName"), type = "producer",
                ts = micros, time = clock.GetUtcNow().ToUnixTimeSeconds(), age = micros - producerCreatedMicroseconds,
                topics = new Dictionary<string, object> { ["document-events"] = new { partitions = new Dictionary<string, object> { ["0"] = new { leader = 0 } } } },
                brokers = new Dictionary<string, object>
                {
                    ["broker"] = new { name = "broker", nodeid = 0, source = "learned", state = "UP", stateage = 0,
                        req_timeouts = 0, disconnects = 0, txretries = 0, txerrs = 0, rxerrs = 0,
                        throttle = new { cnt = 1, max = 0 }, rtt = new { cnt = 1, p99 = 0 } }
                }
            });
        }
        private static DeliveryReport<byte[], byte[]> Delivered() => new() { Error = new Error(ErrorCode.NoError), Status = PersistenceStatus.Persisted, Partition = 0, Offset = 1 };
    }
}
