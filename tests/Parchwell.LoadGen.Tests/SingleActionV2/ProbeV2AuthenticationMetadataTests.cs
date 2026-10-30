using System.Text;
using System.Text.Json;
using Azure.Core;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed class ProbeV2AuthenticationMetadataTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset E0 = new(2030, 1, 1, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset E1 = new(2030, 1, 1, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Given_OptionalFlag_When_Reading_Then_OnlyExactBooleansAreAccepted()
    {
        Assert.False(Read(null).CaptureAuthenticationMetadata);
        Assert.False(Read("false").CaptureAuthenticationMetadata);
        Assert.True(Read("true").CaptureAuthenticationMetadata);
        foreach (string value in new[] { "TRUE", "False", "1", "", " true", "true ", "true\n" })
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Read(value));
            Assert.Equal("v2_authentication_capture_option", error.Message);
        }
    }

    [Fact]
    public void Given_FirstAndLaterInstalls_When_Collecting_Then_E0IsFirstActualInstalledExpiry()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var clock = new FixedClock(Now);
        var session = new KafkaOAuthBearerSession(new("synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "client"),
            "https://synthetic/.default", "synthetic-principal", new QueueProvider(E0, E1), clock, metrics,
            TestContext.Current.CancellationToken, captureLifecycle: true);
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));
        session.Refresh((_, _, _) => { }, _ => Assert.Fail("Unexpected failure."));
        var collector = Collector(() => static () => Observation(1, 10, 1));
        collector.BeforeClient();
        ProbeV2AuthenticationMetadata value = collector.Finish(true, true, session.GetLifecycleSnapshot);
        Assert.Equal(E0, value.InitialInstalledExpiresOn);
        Assert.Equal(E1, value.Lifecycle!.Events[^1].InstalledExpiresOn);
        Assert.DoesNotContain("exchange", JsonSerializer.Serialize(value), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cache", JsonSerializer.Serialize(value), StringComparison.OrdinalIgnoreCase);

        Assert.Null(FinishWithSnapshot(null).InitialInstalledExpiresOn);
        Assert.Null(FinishWithSnapshot(new(true, true, null, session.GetLifecycleSnapshot().Identity, 1000, [] )).InitialInstalledExpiresOn);
        KafkaAuthenticationLifecycleSnapshot noInstall = session.GetLifecycleSnapshot() with
        { Events = [new(1, 1, "initial", "acquisition_returned", Now, 1, E0, null, null)] };
        Assert.Null(FinishWithSnapshot(noInstall).InitialInstalledExpiresOn);
    }

    [Fact]
    public void Given_TwoSamples_When_Collecting_Then_BoundariesAndGenerationsAreRetained()
    {
        int factories = 0, calls = 0;
        var observations = new Queue<ProjectedTokenFileObservation>([Observation(1, 10, 1), Observation(2, 20, 2)]);
        var collector = Collector(() => { factories++; return () => { calls++; return observations.Dequeue(); }; });
        collector.BeforeClient();
        ProbeV2AuthenticationMetadata value = collector.Finish(false, true, null);
        Assert.Equal(1, factories);
        Assert.Equal(2, calls);
        Assert.Equal(["before_client", "after_client_cleanup"], value.ProjectedFileSamples.Select(sample => sample.Boundary));
        Assert.Equal([1, 2], value.ProjectedFileSamples.Select(sample => sample.Observation!.Generation));
        Assert.Equal("session_unavailable", value.CaptureError);
        foreach (string status in new[] { "unavailable", "sample_limit" })
        {
            var single = Collector(() => () => Observation(1, 10, null, status));
            single.BeforeClient();
            var result = single.Finish(false, true, null);
            Assert.Equal(status, result.ProjectedFileSamples[0].Observation!.Status);
        }
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("first")]
    [InlineData("second")]
    [InlineData("snapshot")]
    public void Given_ObserverOrSnapshotError_When_Collecting_Then_OnlySanitizedReasonSurvives(string failure)
    {
        int calls = 0;
        var collector = Collector(() => failure == "factory"
            ? throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR /synthetic/private-token-path")
            : () => ++calls == (failure == "first" ? 1 : 2) && failure is "first" or "second"
                ? throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR /synthetic/private-token-path")
                : Observation(calls, 10 * calls, calls));
        collector.BeforeClient();
        ProbeV2AuthenticationMetadata value = collector.Finish(true, true, failure == "snapshot"
            ? () => throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR")
            : () => EmptySnapshot());
        Assert.Equal(failure == "snapshot" ? "lifecycle_snapshot" : "projected_observer", value.CaptureError);
        string json = JsonSerializer.Serialize(value, ProbeV2Wire.JsonOptions);
        Assert.DoesNotContain("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR", json, StringComparison.Ordinal);
        Assert.DoesNotContain("/synthetic/private-token-path", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_TOKEN_MUST_NOT_APPEAR", json, StringComparison.Ordinal);

        if (failure == "first" || failure == "second") Assert.Equal(2, calls);
    }

    [Fact]
    public void Given_ObserverAndSnapshotError_When_Collecting_Then_FirstSanitizedErrorWins()
    {
        var collector = Collector(() => () => throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR"));
        collector.BeforeClient();
        var value = collector.Finish(true, true, () => throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR"));
        Assert.Equal("projected_observer", value.CaptureError);
        Assert.Null(value.Lifecycle);
        Assert.Null(value.InitialInstalledExpiresOn);
    }

    [Fact]
    public void Given_IncompleteCleanup_When_Finishing_Then_FinalSampleAndSnapshotAreNeverCalled()
    {
        int calls = 0, snapshots = 0;
        var collector = Collector(() => () => { calls++; return Observation(calls, 10, calls); });
        collector.BeforeClient();
        ProbeV2AuthenticationMetadata value = collector.Finish(true, false, () => { snapshots++; throw new InvalidOperationException(); });
        Assert.Equal(1, calls);
        Assert.Equal(0, snapshots);
        Assert.Equal("client_cleanup_incomplete", value.CaptureError);
        Assert.False(value.ClientCleanupCompleted);
        Assert.Null(value.ProjectedFileSamples[1].Observation);
        Assert.Null(value.Lifecycle);
        Assert.Null(value.InitialInstalledExpiresOn);
    }

    [Fact]
    public void Given_NoConstructedClient_When_Finishing_Then_SessionIsNotRead()
    {
        int snapshots = 0;
        var collector = Collector(() => static () => Observation(1, 10, 1));
        collector.BeforeClient();
        ProbeV2AuthenticationMetadata value = collector.Finish(false, true, () => { snapshots++; return EmptySnapshot(); });
        Assert.Equal(0, snapshots);
        Assert.Equal("session_unavailable", value.CaptureError);
        Assert.Null(value.InitialInstalledExpiresOn);
    }

    [Theory]
    [InlineData(262143)]
    [InlineData(262144)]
    [InlineData(262145)]
    public void Given_EncodedSizeBoundary_When_Encoding_Then_InclusiveByteLimitIsEnforced(int size)
    {
        ProbeV2AuthenticationMetadata value = SizedRecord(size, nonAscii: false);
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(value, ProbeV2Wire.JsonOptions);
        Assert.Equal(size, original.Length);
        byte[] encoded = ProbeV2AuthenticationMetadataWriter.Encode(value);
        if (size <= 262144) Assert.Equal(original, encoded);
        else
        {
            var fallback = ProbeV2Wire.Read<ProbeV2AuthenticationMetadata>(encoded, 262144);
            Assert.Equal("serialization_limit", fallback.CaptureError);
            Assert.Null(fallback.InitialInstalledExpiresOn);
            Assert.Null(fallback.Lifecycle);
            Assert.Empty(fallback.ProjectedFileSamples);
            Assert.Equal(value.ClientId, fallback.ClientId);
            Assert.Equal(value.ClientCleanupCompleted, fallback.ClientCleanupCompleted);
        }
    }

    [Fact]
    public void Given_NonAsciiPadding_When_Encoding_Then_Utf8ByteLimitIsEnforced()
    {
        ProbeV2AuthenticationMetadata value = SizedRecord(262144, nonAscii: true);
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(value, ProbeV2Wire.JsonOptions);
        Assert.Equal(262144, original.Length);
        Assert.Equal(original, ProbeV2AuthenticationMetadataWriter.Encode(value));
    }

    [Fact]
    public void Given_OversizedFallback_When_Encoding_Then_NoLineCanBeWritten()
    {
        ProbeV2AuthenticationMetadata value = BaseRecord() with { ClientId = new string('x', 300000) };
        Assert.Empty(ProbeV2AuthenticationMetadataWriter.Encode(value));
    }

    [Fact]
    public void Given_SerializationError_When_Encoding_Then_FallbackOmitsExceptionText()
    {
        ProbeV2AuthenticationMetadata value = BaseRecord() with { ProjectedFileSamples = new ThrowingList() };
        byte[] encoded = ProbeV2AuthenticationMetadataWriter.Encode(value);
        Assert.NotEmpty(encoded);
        string json = Encoding.UTF8.GetString(encoded);
        Assert.Contains("\"capture_error\":\"serialization\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Given_FailingDiagnosticWriter_When_Writing_Then_NoExceptionEscapesOrRetryOccurs()
    {
        var clock = new ManualClock();
        var failing = new TrackingWriter(writeFails: true);
        await ProbeV2AuthenticationMetadataWriter.TryWriteAsync(failing, BaseRecord(), 10, clock);
        Assert.Equal(1, failing.WriteCalls);
        Assert.Equal(0, failing.FlushCalls);

        var timeoutWriter = new TrackingWriter(neverCompletes: true);
        Task writing = ProbeV2AuthenticationMetadataWriter.TryWriteAsync(timeoutWriter, BaseRecord(), 10, clock);
        Assert.False(writing.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(10));
        await writing;
        Assert.Equal(1, timeoutWriter.WriteCalls);
        Assert.Equal(0, timeoutWriter.FlushCalls);

        var flushFails = new TrackingWriter(flushFails: true);
        await ProbeV2AuthenticationMetadataWriter.TryWriteAsync(flushFails, BaseRecord(), 10, clock);
        Assert.Equal(1, flushFails.WriteCalls);
        Assert.Equal(1, flushFails.FlushCalls);

        var success = new TrackingWriter();
        await ProbeV2AuthenticationMetadataWriter.TryWriteAsync(success, BaseRecord(), 10, clock);
        Assert.Equal(1, success.WriteCalls);
        Assert.Equal(1, success.FlushCalls);
        Assert.StartsWith(ProbeV2AuthenticationMetadataWriter.Marker, success.Content, StringComparison.Ordinal);
        Assert.Contains("\"schema_version\"", success.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR", success.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_CaptureDisabled_When_AttachingSession_Then_NoLifecycleReferenceIsRetained()
    {
        using var metrics = new KafkaAuthenticationMetrics();
        var session = new KafkaOAuthBearerSession(new("synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "client"),
            "scope", "principal", new QueueProvider(E0), new FixedClock(Now), metrics,
            TestContext.Current.CancellationToken, captureLifecycle: true);
        var disabled = new ProbeV2Capture(ProbeV2Limits.Fixed(), new FixedClock(Now), 0, 1000);
        disabled.AttachAuthentication(session);
        Assert.Null(disabled.AuthenticationSession);
        var enabled = new ProbeV2Capture(ProbeV2Limits.Fixed(), new FixedClock(Now), 0, 1000, captureAuthenticationMetadata: true);
        enabled.AttachAuthentication(session);
        Assert.Same(session, enabled.AuthenticationSession);
        ProbeV2Repeat scientific = enabled.Snapshot(1, "client", ProbeV2Stage.For("B08", "C0", "action"), new Dictionary<string, string>(), [], null,
            new("document-events", "automatic", null, null, "aw==", "dg=="));
        string json = JsonSerializer.Serialize(scientific, ProbeV2Wire.JsonOptions);
        Assert.Contains("\"repeat\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("authentication", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ProbeV2Options Read(string? capture)
    {
        Dictionary<string, string> environment = ProbeV2OptionsTests.Environment(ProbeV2OptionsTests.Options());
        const string key = "ProbeV2__CaptureAuthenticationMetadata";
        if (capture is null) environment.Remove(key); else environment[key] = capture;
        return ProbeV2Options.Read(environment);
    }

    private static ProbeV2AuthenticationMetadataCollector Collector(Func<Func<ProjectedTokenFileObservation>> sampler) =>
        new(ProbeV2OptionsTests.Options(), ProbeV2Stage.For("B08", "C0", "action"), 1, "client", sampler);

    private static ProbeV2AuthenticationMetadata FinishWithSnapshot(KafkaAuthenticationLifecycleSnapshot? snapshot)
    {
        var collector = Collector(() => static () => Observation(1, 10, 1));
        collector.BeforeClient();
        return collector.Finish(true, true, () => snapshot!);
    }

    private static ProjectedTokenFileObservation Observation(int sample, long timestamp, int? generation, string status = "observed") =>
        new(sample, Now, timestamp, 1000, generation, status);

    private static KafkaAuthenticationLifecycleSnapshot EmptySnapshot() => new(true, true, null,
        new("synthetic-scenario", "synthetic-run", KafkaClientRole.LoadGenProducer, "client"), 1000, []);

    private static ProbeV2AuthenticationMetadata BaseRecord()
    {
        KafkaAuthenticationLifecycleSnapshot lifecycle = EmptySnapshot() with
        { Events = [new(1, 1, "initial", "installation_succeeded", Now, 1, E0, E0, null)] };
        return new(1, "single-action-v2-authentication-metadata", true,
            "run-1", "B08", "C0", "action", new string('a', 40), "job", "pod", "pod-uid", "loadgen",
            1, "client", "produce_one", E0, lifecycle,
            [new("before_client", Observation(1, 10, 1)), new("after_client_cleanup", Observation(2, 20, 1))], true, null);
    }

    private static ProbeV2AuthenticationMetadata SizedRecord(int target, bool nonAscii)
    {
        ProbeV2AuthenticationMetadata template = BaseRecord();
        string prefix = nonAscii ? "é" : "";
        for (int padding = 0; padding <= target; padding++)
        {
            var candidate = template with { ClientId = prefix + new string('x', padding) };
            if (JsonSerializer.SerializeToUtf8Bytes(candidate, ProbeV2Wire.JsonOptions).Length == target) return candidate;
        }
        throw new InvalidOperationException("synthetic_size_not_reached");
    }

    private sealed class QueueProvider(params DateTimeOffset[] expiries) : IKafkaTokenProvider
    {
        private int index;
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) =>
            new("SYNTHETIC_TOKEN_MUST_NOT_APPEAR", expiries[index++]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private long timestamp;
        public override DateTimeOffset GetUtcNow() => now;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => ++timestamp;
    }

    private sealed class ThrowingList : IReadOnlyList<ProbeV2ProjectedFileSample>
    {
        public int Count => 2;
        public ProbeV2ProjectedFileSample this[int index] => throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR");
        public IEnumerator<ProbeV2ProjectedFileSample> GetEnumerator() => throw new InvalidOperationException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TrackingWriter(bool writeFails = false, bool flushFails = false, bool neverCompletes = false) : StringWriter
    {
        internal int WriteCalls { get; private set; }
        internal int FlushCalls { get; private set; }
        internal string Content => ToString();
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            if (writeFails) throw new IOException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR");
            if (neverCompletes) return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return base.WriteLineAsync(buffer, cancellationToken);
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCalls++;
            if (flushFails) throw new IOException("SYNTHETIC_EXCEPTION_MUST_NOT_APPEAR");
            return base.FlushAsync(cancellationToken);
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utc = Now;
        private long timestamp;
        public override DateTimeOffset GetUtcNow() => utc;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, dueTime);
            lock (timers) timers.Add(timer);
            return timer;
        }
        internal void Advance(TimeSpan amount)
        {
            List<ManualTimer> due;
            lock (timers)
            {
                utc += amount;
                timestamp += (long)amount.TotalMilliseconds;
                due = timers.Where(timer => timer.IsDue).ToList();
                foreach (ManualTimer timer in due) timer.Disable();
            }
            foreach (ManualTimer timer in due) timer.Fire();
        }
        private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state, TimeSpan due) : ITimer
        {
            private TimeSpan? dueTime = due == Timeout.InfiniteTimeSpan ? null : TimeSpan.FromMilliseconds(owner.timestamp) + due;
            internal bool IsDue => dueTime is TimeSpan value && value <= TimeSpan.FromMilliseconds(owner.timestamp);
            internal void Disable() => dueTime = null;
            internal void Fire() => callback(state);
            public bool Change(TimeSpan newDueTime, TimeSpan period) { dueTime = newDueTime == Timeout.InfiniteTimeSpan ? null : TimeSpan.FromMilliseconds(owner.timestamp) + newDueTime; return true; }
            public void Dispose() => dueTime = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
