using System.Reflection;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Authentication;
using Parchwell.Kafka.Configuration;
using Parchwell.LoadGen.IdleObservation;
using Parchwell.LoadGen.Run.Monitoring;
using Parchwell.LoadGen.SingleAction;
using Parchwell.LoadGen.SingleActionV2;
using Parchwell.LoadGen.Tests.IdleObservation;
using Parchwell.LoadGen.Tests.SingleAction;
using Parchwell.LoadGen.Tests.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707
public sealed class ConcreteNativeFactoryCallbackTests
{
    private static string Raw(int repeat, bool consumer, bool cleanup = false) => $"{{\"name\":\"native\",\"client_id\":\"same-client\",\"type\":\"{(consumer ? "consumer" : "producer")}\",\"ts\":{(cleanup ? 6 : 5)},\"time\":1,\"age\":{(cleanup ? 6 : 5)},\"tx\":{(repeat == 0 ? 5 : 2)}}}";

    [Theory]
    [InlineData("single-producer")]
    [InlineData("single-consumer")]
    [InlineData("idle-producer")]
    [InlineData("v2-native")]
    [InlineData("v2-producer")]
    [InlineData("v2-consumer")]
    public async Task ConcreteBuilder_RegistersCallbackForEveryBuildAndCleanup(string path)
    {
        bool consumer = path.EndsWith("consumer", StringComparison.Ordinal);
        KafkaClientRole role = consumer ? KafkaClientRole.VerifierConsumer : KafkaClientRole.LoadGenProducer;
        string run = "factory-" + path;
        string scenario = path == "idle-producer" ? "A02" : path == "v2-native" ? "F06" : path.StartsWith("v2", StringComparison.Ordinal) ? "B08" : "F04";
        string phase = path == "idle-producer" || path.StartsWith("v2", StringComparison.Ordinal) && path != "v2-native" ? "C0" : path == "v2-native" ? "C6" : "C2";
        var measurements = new List<(long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) => { if (instrument.Name == "kafka.librdkafka.client.tx") meterListener.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => { var attributes = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal); if (Equals(attributes.GetValueOrDefault("run"), run)) lock (measurements) measurements.Add((value, attributes)); });
        listener.Start();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddSingleton<IKafkaTokenProvider, NoTokenProvider>();
        services.AddParchwellKafka(role, value => { value.NamespaceName = "namespace-test"; value.ClientId = "pod-1"; });
        services.AddParchwellKafkaAuthentication(value =>
        {
            value.ClientId = "00000000-0000-0000-0000-000000000001";
            value.TenantId = "00000000-0000-0000-0000-000000000004";
            value.TokenFilePath = "/unopened/synthetic-token";
        });
        NativeMonitoringFixture store = NativeMonitoringFixture.Register(services, run, scenario, phase, "job-1", "pod-uid", new string('a', 40), "capture/source.json");
        using ServiceProvider provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<KafkaAuthenticationFactory>();
        var monitoring = provider.GetRequiredService<NativeMonitoringHost>();
        var configuration = provider.GetRequiredService<KafkaConfigurationFactory>();
        var settings = consumer
            ? configuration.CreateVerifierConsumer().Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : configuration.CreateLoadGenProducer().Configuration.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var callbacks = new List<Action<string>>();
        IProducer<byte[], byte[]> BuildProducer(ProducerBuilder<byte[], byte[]> builder)
        {
            var handler = Handler<Action<IProducer<byte[], byte[]>, string>>(builder);
            Action<string> callback = raw => handler(null!, raw);
            callbacks.Add(callback);
            callback(Raw(callbacks.Count - 1, consumer)); // callback while Build is still in progress
            return DispatchProxy.Create<IProducer<byte[], byte[]>, EmptyClient>();
        }
        IConsumer<byte[], byte[]> BuildConsumer(ConsumerBuilder<byte[], byte[]> builder)
        {
            var handler = Handler<Action<IConsumer<byte[], byte[]>, string>>(builder);
            Action<string> callback = raw => handler(null!, raw);
            callbacks.Add(callback);
            callback(Raw(callbacks.Count - 1, consumer));
            return DispatchProxy.Create<IConsumer<byte[], byte[]>, EmptyClient>();
        }
        var captured = new List<Func<IReadOnlyList<string>>>();
        if (path.StartsWith("single", StringComparison.Ordinal))
        {
            var options = SingleActionCaptureTests.Options();
            options.RunId = run;
            var factory = new SingleActionSessionFactory(auth, options, role, monitoring, BuildProducer, BuildConsumer);
            for (int i = 0; i < 2; i++)
            {
                settings["client.id"] = "same-client";
                var capture = new SingleActionCapture(options, TimeProvider.System, TimeProvider.System.GetTimestamp(), new(100));
                if (consumer) factory.Consumer(settings, capture, TestContext.Current.CancellationToken); else factory.Producer(settings, capture, TestContext.Current.CancellationToken);
                captured.Add(() => capture.Snapshot().Statistics.Select(x => x.RawBase64).ToArray());
            }
        }
        else if (path == "idle-producer")
        {
            var options = IdleObservationOptionsTests.Options();
            options.RunId = run;
            var factory = new IdleObservationSessionFactory(auth, options, monitoring, BuildProducer);
            for (int i = 0; i < 2; i++)
            {
                settings["client.id"] = "same-client";
                var capture = new IdleObservationCapture(options, TimeProvider.System, TimeProvider.System.GetTimestamp(), 10, new IdleObservationClientLogBudget(1));
                factory.Producer(settings, capture, TestContext.Current.CancellationToken);
                captured.Add(() => capture.Snapshot().Statistics.Select(x => x.RawBase64).ToArray());
            }
        }
        else
        {
            var options = path == "v2-native" ? ProbeV2OptionsTests.NativeOptions("nativejwt", "jwt-bearer") : ProbeV2OptionsTests.Options("B08", "C0", consumer ? "read" : "action");
            options.RunId = run;
            role = ProbeV2Stage.For(options.ScenarioId, options.ConfigurationId, options.Stage).Role;
            var factory = new ProbeV2SessionFactory(auth, options, monitoring, BuildProducer, BuildConsumer);
            for (int i = 0; i < 2; i++)
            {
                settings["client.id"] = "same-client";
                var capture = new ProbeV2Capture(ProbeV2Limits.Fixed(), TimeProvider.System, TimeProvider.System.GetTimestamp(), 0);
                if (i == 0) capture.NoOutcome();
                if (consumer) factory.Consumer(settings, capture, TestContext.Current.CancellationToken); else factory.Producer(settings, capture, TestContext.Current.CancellationToken);
                if (i == 1) capture.NoOutcome();
                var snapshotSettings = new Dictionary<string, string>(settings, StringComparer.Ordinal);
                captured.Add(() => capture.Snapshot(1, snapshotSettings["client.id"], ProbeV2Stage.For(options.ScenarioId, options.ConfigurationId, options.Stage), snapshotSettings, [], null, new("topic", "explicit", 0, 0, "", null)).Statistics.Select(x => x.RawBase64).ToArray());
            }
        }
        Assert.Equal(2, callbacks.Count);
        Assert.Single(captured[0]());
        Assert.Equal(path.StartsWith("v2", StringComparison.Ordinal) ? 0 : 1, captured[1]().Count);
        for (int i = 0; i < callbacks.Count; i++) callbacks[i](Raw(i, consumer, cleanup: true)); // final native cleanup callback
        for (int i = 0; i < captured.Count; i++)
        {
            var values = captured[i]();
            int expected = path.StartsWith("v2", StringComparison.Ordinal) ? 1 : 2;
            Assert.Equal(expected, values.Count);
            Assert.Equal(Raw(i, consumer, cleanup: path.StartsWith("v2", StringComparison.Ordinal) && i == 1),
                Encoding.UTF8.GetString(Convert.FromBase64String(values[0])));
            if (expected == 2)
                Assert.Equal(Raw(i, consumer, cleanup: true), Encoding.UTF8.GetString(Convert.FromBase64String(values[1])));
        }
        await monitoring.CompleteAsync(true, TestContext.Current.CancellationToken);
        using JsonDocument index = JsonDocument.Parse(Assert.Single(store.Objects, p => p.Key.EndsWith("/index.json", StringComparison.Ordinal)).Value);
        Assert.Equal(run, index.RootElement.GetProperty("identity").GetProperty("run_id").GetString());
        Assert.Equal(phase, index.RootElement.GetProperty("identity").GetProperty("phase").GetString());
        Assert.True(index.RootElement.GetProperty("capture_complete").GetBoolean());
        JsonElement clients = index.RootElement.GetProperty("clients");
        Assert.Equal(2, clients.GetArrayLength());
        for (int i = 0; i < 2; i++)
        {
            JsonElement client = clients[i];
            Assert.Equal("same-client", client.GetProperty("client_id").GetString());
            Assert.Equal(role.ToString(), client.GetProperty("role").GetString());
            Assert.Equal("built", client.GetProperty("build_status").GetString());
            Assert.Equal(2, client.GetProperty("report_count").GetInt32());
            Assert.True(client.GetProperty("capture_complete").GetBoolean());
            Assert.Empty(client.GetProperty("diagnostics").EnumerateArray());
            JsonElement[] sources = client.GetProperty("raw_sources").EnumerateArray().ToArray();
            Assert.Equal(2, sources.Length);
            Assert.Equal(new long[] { 1, 2 }, sources.Select(source => source.GetProperty("capture_ordinal").GetInt64()));
            bool selective = path.StartsWith("v2", StringComparison.Ordinal);
            for (int ordinal = 1; ordinal <= 2; ordinal++)
            {
                JsonElement source = sources[ordinal - 1];
                bool selected = !selective || (i == 0 ? ordinal == 1 : ordinal == 2);
                string raw = Raw(i, consumer, cleanup: ordinal == 2);
                Assert.Equal(1, source.GetProperty("row_count").GetInt64());
                if (selected)
                {
                    Assert.Equal("existing_capture", source.GetProperty("kind").GetString());
                    Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw))), source.GetProperty("sha256").GetString());
                }
                else
                {
                    Assert.Equal("raw_chunk", source.GetProperty("kind").GetString());
                    Assert.Equal(ordinal, source.GetProperty("report_ordinal").GetInt64());
                    byte[] chunk = store.Objects[source.GetProperty("source").GetString()!];
                    Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(chunk)), source.GetProperty("sha256").GetString());
                    using JsonDocument row = JsonDocument.Parse(Encoding.UTF8.GetString(chunk).TrimEnd('\n'));
                    Assert.Equal(i + 1, row.RootElement.GetProperty("client_ordinal").GetInt32());
                    Assert.Equal(ordinal, row.RootElement.GetProperty("report_ordinal").GetInt64());
                    Assert.Equal(raw, row.RootElement.GetProperty("raw").GetString());
                }
            }
        }
        Assert.Equal(path.StartsWith("v2", StringComparison.Ordinal) ? 3 : 1, store.Objects.Count);
        Assert.Equal(new long[] { 5, 2, 0, 0 }, measurements.Select(m => m.Value));
        Assert.All(measurements, measurement =>
        {
            Assert.Equal(scenario, measurement.Tags["scenario"]);
            Assert.Equal(run, measurement.Tags["run"]);
            Assert.Equal(role.ToString(), measurement.Tags["role"]);
            Assert.Equal("same-client", measurement.Tags["client_id"]);
        });
    }

    private sealed class FixedClock : TimeProvider
    {
        public override long GetTimestamp() => 10;
        public override long TimestampFrequency => 1_000_000;
    }
    private static T Handler<T>(object builder) where T : Delegate => Assert.IsType<T>(builder.GetType().GetProperty("StatisticsHandler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(builder));
    public class EmptyClient : DispatchProxy { protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null; }
    private sealed class NoTokenProvider : IKafkaTokenProvider
    {
        public AccessToken GetToken(string scope, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected token acquisition");
    }
}
