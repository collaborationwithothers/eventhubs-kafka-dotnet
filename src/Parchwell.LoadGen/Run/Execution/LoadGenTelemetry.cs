using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Parchwell.LoadGen.Run.Execution;

internal static class LoadGenTelemetry
{
    private static readonly ActivitySource Source = new("Parchwell.Workers");
    private static readonly Meter Meter = new("Parchwell.Workers");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("parchwell.worker.operations");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("parchwell.worker.operation.duration_ms", "ms");

    internal static OperationScope Begin(TimeProvider clock, string scenario, string run, string clientId, string operation) =>
        new OperationScope(clock, scenario, run, clientId, operation);

    internal sealed class OperationScope : IDisposable
    {
        private readonly TimeProvider _clock;
        private readonly long _started;
        private readonly string _scenario;
        private readonly string _run;
        private readonly string _clientId;
        private readonly string _operation;
        private readonly Activity? _activity;
        private int _completed;

        internal OperationScope(TimeProvider clock, string scenario, string run, string clientId, string operation)
        {
            _clock = clock;
            _started = clock.GetTimestamp();
            _scenario = scenario;
            _run = run;
            _clientId = clientId;
            _operation = operation;
            string activityName = operation switch
            {
                "registration" => "loadgen.registration",
                "publication" => "loadgen.publication",
                "diagnostics" => "loadgen.diagnostics",
                "run" => "loadgen.run",
                _ => "loadgen.finalisation"
            };
            _activity = Source.StartActivity(activityName, ActivityKind.Internal);
            SetTags(_activity, "started");
        }

        internal void Succeed() => Interlocked.Exchange(ref _completed, 1);

        public void Dispose()
        {
            bool success = Volatile.Read(ref _completed) == 1;
            string outcome = success ? "success" : "failure";
            SetTags(_activity, outcome);
            if (!success) _activity?.SetStatus(ActivityStatusCode.Error);
            Operations.Add(1, Tags(outcome));
            Duration.Record(_clock.GetElapsedTime(_started).TotalMilliseconds, Tags(outcome));
            _activity?.Dispose();
        }

        private void SetTags(Activity? activity, string outcome)
        {
            activity?.SetTag("scenario", _scenario);
            activity?.SetTag("run", _run);
            activity?.SetTag("role", "LoadGenProducer");
            activity?.SetTag("client_id", _clientId);
            activity?.SetTag("operation", _operation);
            activity?.SetTag("outcome", outcome);
        }

        private TagList Tags(string outcome) => new()
        {
            { "scenario", _scenario }, { "run", _run }, { "role", "LoadGenProducer" },
            { "client_id", _clientId }, { "operation", _operation }, { "outcome", outcome }
        };
    }
}
