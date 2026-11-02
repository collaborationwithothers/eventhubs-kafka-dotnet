using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.Common.SingleAction;

namespace Parchwell.LoadGen.IdleObservation;

// Owns bounded raw callback transport for one client. It never judges evidence or a hypothesis.
internal sealed class IdleObservationCapture(IdleObservationOptions options, TimeProvider clock, long started,
    int maxStatisticsReports, IdleObservationClientLogBudget clientLogs)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly Queue<Action> _callbacks = new();
    private readonly List<SingleActionClientError> _errors = [];
    private readonly List<SingleActionStatistics> _statistics = [];
    private readonly List<string> _diagnostics = [];
    private readonly TaskCompletionSource<(Error Error, long Elapsed, long Unix)> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<(Error Error, long Elapsed, long Unix)> _second = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _statisticsSequence;
    private int _statisticsCallbacks;
    private int _errorCallbacks;
    private long _errorsDropped;
    private bool _limit;

    internal void Log(LogMessage message) => clientLogs.Append(message);
    internal long ElapsedMs => (long)clock.GetElapsedTime(started).TotalMilliseconds;

    internal void Error(Error error)
    {
        long elapsed = ElapsedMs;
        lock (_gate)
        {
            if (_limit) return;
            if (Encoding.UTF8.GetByteCount(error.Reason) > options.MaxErrorBytes) { MarkLimit(); return; }
            if (_errorCallbacks >= options.MaxClientErrors)
            {
                if (_errorsDropped < long.MaxValue) _errorsDropped++;
                return;
            }
            if (!EnqueueLocked(() => _errors.Add(new((int)error.Code, error.IsFatal, Origin(error), error.Reason, elapsed)))) return;
            _errorCallbacks++;
        }
    }

    internal void Statistics(string raw)
    {
        long elapsed = ElapsedMs;
        lock (_gate)
        {
            if (_limit) return;
            if (_statisticsCallbacks >= maxStatisticsReports) { MarkLimit(); return; }
            byte[] bytes;
            try
            {
                if (StrictUtf8.GetByteCount(raw) > options.MaxStatisticsReportBytes) { MarkLimit(); return; }
                bytes = StrictUtf8.GetBytes(raw);
            }
            catch (EncoderFallbackException) { MarkLimit(); return; }
            string encoded = Convert.ToBase64String(bytes);
            int sequence = ++_statisticsSequence;
            if (!EnqueueLocked(() => _statistics.Add(new(sequence, elapsed, encoded)))) return;
            _statisticsCallbacks++;
        }
    }

    internal Task<(Error Error, long Elapsed, long Unix)> Delivery(int attempt) => attempt == 1 ? _first.Task : _second.Task;

    internal void Delivered(int attempt, Error error)
    {
        long elapsed = ElapsedMs;
        long unix = clock.GetUtcNow().ToUnixTimeMilliseconds();
        lock (_gate)
        {
            if (_limit) return;
            if (error.IsError && Encoding.UTF8.GetByteCount(error.Reason) > options.MaxErrorBytes) { MarkLimit(); return; }
            (attempt == 1 ? _first : _second).TrySetResult((error, elapsed, unix));
        }
    }

    internal void Diagnostic(string value)
    {
        lock (_gate)
        {
            if (Encoding.UTF8.GetByteCount(value) > options.MaxDiagnosticBytes || _diagnostics.Count >= options.MaxDiagnosticsPerClient - 1)
            {
                MarkLimit();
                return;
            }
            _diagnostics.Add(value);
        }
    }

    internal void CaptureLimit() { lock (_gate) MarkLimit(); }

    internal void Drain()
    {
        lock (_gate) while (_callbacks.TryDequeue(out Action? callback)) callback();
    }

    internal (SingleActionClientError[] Errors, long Dropped, SingleActionStatistics[] Statistics, string[] Diagnostics) Snapshot()
    {
        lock (_gate)
        {
            Drain();
            return (_errors.ToArray(), _errorsDropped, _statistics.ToArray(), _diagnostics.ToArray());
        }
    }

    internal static string Origin(Error error) => error.IsLocalError ? "local" : "broker";

    private bool EnqueueLocked(Action callback)
    {
        if (_limit) return false;
        if (_callbacks.Count >= options.CallbackQueueCapacity) { MarkLimit(); return false; }
        _callbacks.Enqueue(callback);
        return true;
    }

    private void MarkLimit()
    {
        if (_limit) return;
        _limit = true;
        _diagnostics.Add("capture_limit");
    }
}
