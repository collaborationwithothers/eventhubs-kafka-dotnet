using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Parchwell.Verifier;

/// <summary>Executes the one mode selected for this verifier process.</summary>
/// <remarks>A successful host exit requires a selected handler to finish successfully (ADR-017).</remarks>
internal sealed partial class VerifierWorker : BackgroundService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Verifier mode {Mode} is unavailable for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogUnavailable(ILogger logger, string? mode, string? scenarioId, string? runId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Verifier mode {Mode} stopped before completion for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogStopped(ILogger logger, string? mode, string? scenarioId, string? runId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Verifier mode {Mode} failed for scenario {ScenarioId} run {RunId}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, string? mode, string? scenarioId, string? runId);

    private readonly IOptions<VerifierHostOptions> _options;
    private readonly IEnumerable<IVerifierModeHandler> _handlers;
    private readonly VerifierExecutionState _executionState;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<VerifierWorker> _logger;

    public VerifierWorker(
        IOptions<VerifierHostOptions> options,
        IEnumerable<IVerifierModeHandler> handlers,
        VerifierExecutionState executionState,
        IHostApplicationLifetime applicationLifetime,
        ILogger<VerifierWorker> logger)
    {
        _options = options;
        _handlers = handlers;
        _executionState = executionState;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CancellationToken applicationStopping = _applicationLifetime.ApplicationStopping;
        using CancellationTokenRegistration hostStoppingRegistration = applicationStopping.Register(
            static state => ((VerifierExecutionState)state!).MarkStopped(), _executionState);
        using CancellationTokenRegistration workerStoppingRegistration = stoppingToken.Register(
            static state => ((VerifierExecutionState)state!).MarkStopped(), _executionState);
        try
        {
            var configuredMode = _options.Value.Mode;
            if (VerifierModeParser.TryParse(configuredMode, out var mode))
            {
                var handler = _handlers.SingleOrDefault(candidate => candidate.Mode == mode);
                if (handler is not null)
                {
                    int exitCode = await handler.ExecuteAsync(stoppingToken).ConfigureAwait(false);
                    if (exitCode == 0)
                    {
                        _executionState.MarkSucceeded(stoppingToken, applicationStopping);
                    }
                    else
                    {
                        _executionState.MarkFailed();
                    }
                }
                else
                {
                    LogUnavailable(
                        _logger,
                        configuredMode,
                        _options.Value.ScenarioId,
                        _options.Value.RunId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A host stop before successful work leaves the explicit failure outcome in place.
            LogStopped(
                _logger,
                _options.Value.Mode,
                _options.Value.ScenarioId,
                _options.Value.RunId);
        }
        catch (Exception exception)
        {
            // Worker exceptions do not become successful merely because the Generic Host stops.
            LogFailed(
                _logger,
                exception,
                _options.Value.Mode,
                _options.Value.ScenarioId,
                _options.Value.RunId);
        }
        finally
        {
            _applicationLifetime.StopApplication();
        }
    }
}

/// <summary>Runs a selected verifier mode after host startup validation.</summary>
internal interface IVerifierModeHandler
{
    VerifierMode Mode { get; }

    Task<int> ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>Tracks the explicit process outcome independently of Generic Host shutdown.</summary>
internal sealed class VerifierExecutionState
{
    private const int Running = 0;
    private const int Succeeded = 1;
    private const int Failed = 2;
    private int _state = Running;

    public int ExitCode => Volatile.Read(ref _state) == Succeeded ? 0 : 1;

    public void MarkSucceeded(CancellationToken stoppingToken, CancellationToken applicationStopping)
    {
        // Completion and a premature stop compete for the running state; failure is sticky.
        Interlocked.CompareExchange(ref _state, Succeeded, Running);
        // Cancellation can be requested while its callback is still waiting to run.
        // Observe it after the transition so an earlier request cannot be overwritten.
        if (stoppingToken.IsCancellationRequested || applicationStopping.IsCancellationRequested)
        {
            MarkFailed();
        }
    }

    public void MarkStopped() => Interlocked.CompareExchange(ref _state, Failed, Running);

    public void MarkFailed() => Interlocked.Exchange(ref _state, Failed);
}

/// <summary>Terminates a verifier process whose lifetime budget expires.</summary>
/// <remarks>The timer remains armed through host cleanup; production expiry terminates with code 124.</remarks>
internal sealed class ProcessWatchdog : IDisposable
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _processLifetime;
    private readonly Action<int> _hardExit;
    private ITimer? _timer;

    public ProcessWatchdog(
        TimeProvider timeProvider,
        TimeSpan processLifetime,
        Action<int>? hardExitForTests = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(processLifetime, TimeSpan.Zero);

        _timeProvider = timeProvider;
        _processLifetime = processLifetime;
        _hardExit = hardExitForTests ?? Environment.Exit;
    }

    public void Start()
    {
        if (_timer is not null)
        {
            throw new InvalidOperationException("The process watchdog has already started.");
        }

        _timer = _timeProvider.CreateTimer(
            static state => ((ProcessWatchdog)state!).OnExpired(),
            this,
            _processLifetime,
            Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void OnExpired() => _hardExit(124);
}
