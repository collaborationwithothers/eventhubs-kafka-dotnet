using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.Common.SingleAction;

namespace Parchwell.LoadGen.SingleAction;

// Owns bounded raw callback transport only. It never decides verifier validity or a hypothesis.
internal sealed class SingleActionCapture(SingleActionHostOptions options, TimeProvider clock, long started,
    SingleActionDiagnosticBudget diagnosticBudget)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly Queue<Action> _callbacks = new();
    private readonly List<SingleActionClientError> _errors = [];
    private readonly List<SingleActionStatistics> _statistics = [];
    private readonly List<string> _diagnostics = [];
    private int _statisticsSequence;
    private bool _limit;
    private bool _assigned;
    private Error? _fatalError;
    private (long Elapsed, long Unix)? _assignmentObservation;
    private (long Elapsed, long Unix)? _fatalObservation;
    internal bool Limit { get { lock (_gate) return _limit; } }
    internal bool Assigned { get { lock (_gate) return _assigned; } }
    internal Error? FatalError { get { lock (_gate) return _fatalError; } }
    internal (long Elapsed, long Unix)? AssignmentObservation { get { lock (_gate) return _assignmentObservation; } }
    internal (long Elapsed, long Unix)? FatalObservation { get { lock (_gate) return _fatalObservation; } }
    internal long ElapsedMs => (long)clock.GetElapsedTime(started).TotalMilliseconds;
    internal void AssignedPartitions(int count)
    {
        if (count <= 0) return;
        long elapsed = ElapsedMs;
        long unix = clock.GetUtcNow().ToUnixTimeMilliseconds();
        Enqueue(() => { _assigned = true; _assignmentObservation ??= (elapsed, unix); });
    }
    internal void Error(Error error)
    {
        long elapsed = ElapsedMs;
        long unix = clock.GetUtcNow().ToUnixTimeMilliseconds();
        lock (_gate)
        {
            if (Encoding.UTF8.GetByteCount(error.Reason) > options.MaxErrorBytes) { MarkLimit(); return; }
            EnqueueLocked(() =>
            {
                if (_errors.Count >= options.MaxClientErrors) { MarkLimit(); return; }
                _errors.Add(new((int)error.Code, error.IsFatal, Origin(error), error.Reason, elapsed));
                if (error.IsFatal) { _fatalError ??= error; _fatalObservation ??= (elapsed, unix); }
            });
        }
    }
    internal void Statistics(string raw)
    {
        long elapsed = ElapsedMs;
        lock (_gate)
        {
            byte[] bytes;
            try
            {
                if (StrictUtf8.GetByteCount(raw) > options.MaxStatisticsReportBytes) { MarkLimit(); return; }
                bytes = StrictUtf8.GetBytes(raw);
            }
            catch (EncoderFallbackException) { MarkLimit(); return; }
            string encoded = Convert.ToBase64String(bytes);
            int sequence = ++_statisticsSequence;
            EnqueueLocked(() =>
            {
                if (_statistics.Count >= options.MaxStatisticsReports) { MarkLimit(); return; }
                _statistics.Add(new(sequence, elapsed, encoded));
            });
        }
    }
    internal void Diagnostic(string value)
    {
        lock (_gate)
        {
            if (Encoding.UTF8.GetByteCount(value) > options.MaxDiagnosticBytes || !diagnosticBudget.TryAdd(_diagnostics, value)) { MarkLimit(); return; }
        }
    }
    internal void CaptureLimit() { lock (_gate) MarkLimit(); }
    internal void Drain()
    {
        lock (_gate) while (_callbacks.TryDequeue(out Action? callback)) callback();
    }
    internal (SingleActionClientError[] Errors, SingleActionStatistics[] Statistics, IReadOnlyList<string> Diagnostics) Snapshot()
    {
        Drain();
        lock (_gate) return (_errors.ToArray(), _statistics.ToArray(), _diagnostics.AsReadOnly());
    }
    internal static string Origin(Error error) => error.IsBrokerError ? "broker" : "local";
    private void Enqueue(Action callback) { lock (_gate) EnqueueLocked(callback); }
    private void EnqueueLocked(Action callback)
    {
        if (_limit) return;
        if (_callbacks.Count >= options.CallbackQueueCapacity) { MarkLimit(); return; }
        _callbacks.Enqueue(callback);
    }
    private void MarkLimit()
    {
        if (_limit) return;
        _limit = true;
        diagnosticBudget.MarkLimit(_diagnostics);
    }
}

internal sealed class SingleActionDiagnosticBudget(int maximum)
{
    private readonly object _gate = new();
    private readonly List<List<string>> _lists = [];
    private int _count;
    internal bool TryAdd(List<string> target, string value)
    {
        lock (_gate)
        {
            if (_count >= maximum) return false;
            Register(target);
            target.Add(value);
            _count++;
            return true;
        }
    }
    internal void MarkLimit(List<string> target)
    {
        lock (_gate)
        {
            Register(target);
            if (_count >= maximum)
            {
                // Retain an explicit incomplete marker inside the same finite configuration
                // budget. Frozen repeat views share these lists until publication.
                List<string> previous = _lists.Last(list => list.Any(value => value != "capture_limit"));
                previous.RemoveAt(previous.FindLastIndex(value => value != "capture_limit"));
                _count--;
            }
            target.Add("capture_limit");
            _count++;
        }
    }
    private void Register(List<string> target)
    {
        if (!_lists.Contains(target)) _lists.Add(target);
    }
}
