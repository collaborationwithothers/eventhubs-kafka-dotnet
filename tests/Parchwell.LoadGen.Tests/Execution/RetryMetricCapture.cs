using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Parchwell.LoadGen.Tests.Execution;

internal sealed class RetryMetricCapture : IDisposable
{
    private static readonly string[] ExpectedTagNames = ["client_id", "event", "retry_kind", "role", "run", "scenario"];
    private readonly MeterListener listener = new();
    private readonly ConcurrentQueue<(string Kind, string Event)> events = new();

    internal RetryMetricCapture(string run, string scenario, string clientId)
    {
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == "Parchwell.Workers" && instrument.Name == "parchwell.loadgen.application_retry.events")
            {
                Xunit.Assert.Equal("count", instrument.Unit);
                Xunit.Assert.IsType<Counter<long>>(instrument);
                observer.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in tags) values.Add(tag.Key, tag.Value?.ToString() ?? "");
            if (values.GetValueOrDefault("run") != run || values.GetValueOrDefault("scenario") != scenario ||
                values.GetValueOrDefault("client_id") != clientId) return;
            Xunit.Assert.Equal(1, value);
            Xunit.Assert.Equal(ExpectedTagNames, values.Keys.Order(StringComparer.Ordinal));
            Xunit.Assert.Equal("LoadGenProducer", values["role"]);
            events.Enqueue((values["retry_kind"], values["event"]));
        });
        listener.Start();
    }

    internal string[] Events(string kind) => events.Where(item => item.Kind == kind).Select(item => item.Event).ToArray();
    public void Dispose() => listener.Dispose();
}
