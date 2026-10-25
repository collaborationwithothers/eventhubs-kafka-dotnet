using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Parchwell.LoadGen.Run.Execution;
using Xunit;

namespace Parchwell.LoadGen.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class LoadGenTelemetryTests
{
    [Theory]
    [InlineData(true, "success")]
    [InlineData(false, "failure")]
    public void Given_FinalisationOperation_When_Recorded_Then_ActivityAndMetricsCarryOnlyBoundIdentityAndOutcome(bool succeed, string expectedOutcome)
    {
        // The listeners are process-wide and other worker tests emit on the same source in
        // parallel, so only this invocation's run is recorded.
        string run = "telemetry-run-" + expectedOutcome;
        var activities = new ConcurrentQueue<Activity>();
        var operationMeasurements = new ConcurrentQueue<(long Value, Dictionary<string, string> Tags)>();
        var durations = new ConcurrentQueue<(double Value, Dictionary<string, string> Tags)>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Parchwell.Workers",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (Equals(activity.GetTagItem("run"), run)) activities.Enqueue(activity);
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Parchwell.Workers" && instrument.Name is
                "parchwell.worker.operations" or "parchwell.worker.operation.duration_ms")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            Dictionary<string, string> values = ToDictionary(tags);
            if (instrument.Name == "parchwell.worker.operations" && values.GetValueOrDefault("run") == run)
                operationMeasurements.Enqueue((value, values));
        });
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            Dictionary<string, string> values = ToDictionary(tags);
            if (instrument.Name == "parchwell.worker.operation.duration_ms" && values.GetValueOrDefault("run") == run)
                durations.Enqueue((value, values));
        });
        meterListener.Start();

        using (LoadGenTelemetry.OperationScope operation = LoadGenTelemetry.Begin(TimeProvider.System,
                   "A01", run, "pod-7", "ledger_drain"))
        {
            if (succeed) operation.Succeed();
        }

        Activity activity = Assert.Single(activities);
        Assert.Equal("loadgen.finalisation", activity.OperationName);
        Assert.Equal("A01", activity.GetTagItem("scenario"));
        Assert.Equal(run, activity.GetTagItem("run"));
        Assert.Equal("LoadGenProducer", activity.GetTagItem("role"));
        Assert.Equal("pod-7", activity.GetTagItem("client_id"));
        Assert.Equal("ledger_drain", activity.GetTagItem("operation"));
        Assert.Equal(expectedOutcome, activity.GetTagItem("outcome"));

        var operationMeasurement = Assert.Single(operationMeasurements);
        Assert.Equal(1, operationMeasurement.Value);
        Assert.Equal(expectedOutcome, operationMeasurement.Tags["outcome"]);
        Assert.Equal("ledger_drain", operationMeasurement.Tags["operation"]);
        Assert.Equal("A01", operationMeasurement.Tags["scenario"]);
        Assert.Equal(run, operationMeasurement.Tags["run"]);
        Assert.Equal("LoadGenProducer", operationMeasurement.Tags["role"]);
        Assert.Equal("pod-7", operationMeasurement.Tags["client_id"]);
        var duration = Assert.Single(durations);
        Assert.True(duration.Value >= 0);
        Assert.Equal(operationMeasurement.Tags, duration.Tags);
        Assert.DoesNotContain(operationMeasurement.Tags.Keys,
            key => key.Contains("payload", StringComparison.OrdinalIgnoreCase) || key.Contains("connection", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> tag in tags)
            values.Add(tag.Key, tag.Value?.ToString() ?? string.Empty);
        return values;
    }
}
#pragma warning restore CA1707
