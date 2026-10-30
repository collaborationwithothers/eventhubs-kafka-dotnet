using Azure.Core;
using Confluent.Kafka;
using System.Collections.ObjectModel;

namespace Parchwell.Kafka.Authentication;

/// <summary>Owns OAuth bearer token state for one native Kafka client lifetime.</summary>
/// <remarks>
/// This session exposes authentication evidence for its client. It does not decide whether a
/// scenario run is eligible to complete successfully.
/// </remarks>
public sealed class KafkaOAuthBearerSession
{
    private const int MaximumObservedAttempts = 128;
    private readonly object _gate = new();
    private readonly object _attachmentGate = new();
    private readonly TaskCompletionSource<KafkaAuthenticationFailure> _initialFailureSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly KafkaAuthenticationIdentity _identity;
    private readonly string _scope;
    private readonly string _principalName;
    private readonly IKafkaTokenProvider _tokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly KafkaAuthenticationMetrics _metrics;
    private readonly CancellationToken _cancellationToken;
    private readonly bool _captureLifecycle;
    private readonly long _timestampFrequency;
    private readonly List<KafkaAuthenticationLifecycleEvent> _lifecycleEvents = [];
    private DateTimeOffset? _installedTokenExpiresOn;
    private KafkaAuthenticationFailure? _initialFailure;
    private KafkaAuthenticationFailure? _latestFailure;
    private bool _attached;
    private bool _attemptLimitReached;
    private bool _observationClockUnavailable;
    private int _observedAttempts;
    private Confluent.Kafka.IClient? _callbackClient;

    /// <summary>Creates a session for one native Kafka client.</summary>
    /// <param name="identity">The scenario run and Kafka client identity.</param>
    /// <param name="scope">The validated Event Hubs token scope.</param>
    /// <param name="principalName">The workload application client ID reported to Kafka.</param>
    /// <param name="tokenProvider">The synchronous token acquisition boundary.</param>
    /// <param name="timeProvider">The clock used to validate token expiry.</param>
    /// <param name="metrics">The authentication failure metrics recorder.</param>
    /// <param name="cancellationToken">The run cancellation token forwarded to acquisition.</param>
    /// <param name="captureLifecycle">Whether to retain bounded diagnostic lifecycle observations.</param>
    internal KafkaOAuthBearerSession(
        KafkaAuthenticationIdentity identity,
        string scope,
        string principalName,
        IKafkaTokenProvider tokenProvider,
        TimeProvider timeProvider,
        KafkaAuthenticationMetrics metrics,
        CancellationToken cancellationToken,
        bool captureLifecycle = false)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _principalName = principalName ?? throw new ArgumentNullException(nameof(principalName));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _cancellationToken = cancellationToken;
        _captureLifecycle = captureLifecycle;
        if (captureLifecycle)
        {
            try
            {
                _timestampFrequency = timeProvider.TimestampFrequency;
            }
            catch (Exception)
            {
                _observationClockUnavailable = true;
            }
        }
    }

    /// <summary>Gets the expiry of the last token successfully installed in the native client.</summary>
    public DateTimeOffset? InstalledTokenExpiresOn
    {
        get
        {
            lock (_gate)
            {
                return _installedTokenExpiresOn;
            }
        }
    }

    /// <summary>Gets the sticky first failure before any token was successfully installed.</summary>
    /// <remarks>A later successful installation does not clear this failure.</remarks>
    public KafkaAuthenticationFailure? InitialFailure
    {
        get
        {
            lock (_gate)
            {
                return _initialFailure;
            }
        }
    }

    /// <summary>Gets a notification that completes when the first failure occurs before token installation.</summary>
    /// <remarks>
    /// The task completes with the same sticky failure exposed by <see cref="InitialFailure"/>. It never
    /// resets after later success and remains pending for refresh-only failures. A session is intended for
    /// one native client lifetime.
    /// </remarks>
    public Task<KafkaAuthenticationFailure> InitialFailureTask => _initialFailureSignal.Task;

    /// <summary>Gets the most recent handler failure, including failures after a successful install.</summary>
    /// <remarks>A refresh error alone does not set <see cref="InitialFailure"/> or decide run eligibility.</remarks>
    public KafkaAuthenticationFailure? LatestFailure
    {
        get
        {
            lock (_gate)
            {
                return _latestFailure;
            }
        }
    }

    /// <summary>Gets a defensive snapshot of the opt-in callback observations for this session.</summary>
    /// <remarks>Successful token installation is not evidence of broker acceptance.</remarks>
    /// <returns>The current immutable observation snapshot.</returns>
    public KafkaAuthenticationLifecycleSnapshot GetLifecycleSnapshot()
    {
        lock (_gate)
        {
            string? incompleteReason = !_captureLifecycle
                ? "capture_disabled"
                : _attemptLimitReached
                    ? "attempt_limit"
                    : _observationClockUnavailable ? "clock_unavailable" : null;
            KafkaAuthenticationLifecycleEvent[] copy = _lifecycleEvents.ToArray();
            ReadOnlyCollection<KafkaAuthenticationLifecycleEvent> events = Array.AsReadOnly(copy);
            return new KafkaAuthenticationLifecycleSnapshot(
                _captureLifecycle,
                _captureLifecycle && incompleteReason is null,
                incompleteReason,
                _identity,
                _timestampFrequency,
                events);
        }
    }

    /// <summary>Refreshes and installs a token through the native Kafka client.</summary>
    /// <param name="client">The native client receiving the token.</param>
    internal void Refresh(Confluent.Kafka.IClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        BindCallbackClient(client);
        // The pinned API takes absolute epoch milliseconds and optional SASL extensions:
        // https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ClientExtensions.cs#L39-L91
        Refresh(
            (token, expiry, principal) => client.OAuthBearerSetToken(
                token, expiry, principal, new Dictionary<string, string>(StringComparer.Ordinal)),
            error => client.OAuthBearerSetTokenFailure(error));
    }

    /// <summary>Claims this session for one validated native builder attachment.</summary>
    /// <remarks>A claim is permanent, including when building or disposing the native client later fails.</remarks>
    /// <exception cref="InvalidOperationException">The session has already been claimed by a builder.</exception>
    internal void ClaimAttachment()
    {
        lock (_attachmentGate)
        {
            if (_attached)
                throw new InvalidOperationException("OAuth bearer session is already attached to a native client.");
            _attached = true;
        }
    }

    private void BindCallbackClient(Confluent.Kafka.IClient client)
    {
        lock (_attachmentGate)
        {
            if (_callbackClient is null)
            {
                _callbackClient = client;
                return;
            }

            if (!ReferenceEquals(_callbackClient, client))
                throw new InvalidOperationException("OAuth bearer session callback belongs to a different native client.");
        }
    }

    /// <summary>Refreshes a token using the supplied native operations.</summary>
    /// <param name="install">Installs the token and its absolute expiry.</param>
    /// <param name="reportFailure">Reports a sanitized failure to the native client.</param>
    internal void Refresh(Action<string, long, string> install, Action<string> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(reportFailure);

        lock (_gate)
        {
            string phase = _installedTokenExpiresOn is null ? "initial" : "refresh";
            int attempt = BeginObservedAttempt();
            DateTimeOffset? acquiredExpiresOn = null;
            RecordLifecycleEvent(attempt, phase, "callback_started", null, null);
            AccessToken token;
            try
            {
                token = _tokenProvider.GetToken(_scope, _cancellationToken);
            }
            catch (Exception)
            {
                Fail(phase, "acquisition", reportFailure, attempt, acquiredExpiresOn);
                return;
            }

            acquiredExpiresOn = token.ExpiresOn;
            RecordLifecycleEvent(attempt, phase, "acquisition_returned", acquiredExpiresOn, null);

            DateTimeOffset now;
            try
            {
                now = _timeProvider.GetUtcNow();
            }
            catch (Exception)
            {
                Fail(phase, "token_validation", reportFailure, attempt, acquiredExpiresOn);
                return;
            }

            if (string.IsNullOrEmpty(token.Token) || token.ExpiresOn <= now)
            {
                Fail(phase, "token_validation", reportFailure, attempt, acquiredExpiresOn);
                return;
            }

            try
            {
                install(token.Token, token.ExpiresOn.ToUnixTimeMilliseconds(), _principalName);
            }
            catch (Exception)
            {
                Fail(phase, "installation", reportFailure, attempt, acquiredExpiresOn);
                return;
            }

            _installedTokenExpiresOn = token.ExpiresOn;
            RecordLifecycleEvent(attempt, phase, "installation_succeeded", acquiredExpiresOn, null);
        }
    }

    private int BeginObservedAttempt()
    {
        if (!_captureLifecycle)
            return 0;

        if (_observedAttempts >= MaximumObservedAttempts)
        {
            _attemptLimitReached = true;
            return 0;
        }

        _observedAttempts++;
        return _observedAttempts;
    }

    private void RecordLifecycleEvent(
        int attempt,
        string phase,
        string stage,
        DateTimeOffset? acquiredExpiresOn,
        string? failureReason)
    {
        if (attempt == 0)
            return;

        DateTimeOffset? observedAt = null;
        long? timestamp = null;
        try
        {
            observedAt = _timeProvider.GetUtcNow().ToUniversalTime();
            timestamp = _timeProvider.GetTimestamp();
        }
        catch (Exception)
        {
            observedAt = null;
            timestamp = null;
            _observationClockUnavailable = true;
        }

        _lifecycleEvents.Add(new KafkaAuthenticationLifecycleEvent(
            _lifecycleEvents.Count + 1,
            attempt,
            phase,
            stage,
            observedAt,
            timestamp,
            acquiredExpiresOn,
            _installedTokenExpiresOn,
            failureReason));
    }

    private void Fail(
        string phase,
        string reason,
        Action<string> reportFailure,
        int attempt,
        DateTimeOffset? acquiredExpiresOn)
    {
        RecordLifecycleEvent(attempt, phase, "failed", acquiredExpiresOn, reason);
        KafkaAuthenticationFailure failure = new(_identity, phase, reason);
        _latestFailure = failure;
        if (StringComparer.Ordinal.Equals(phase, "initial"))
        {
            _initialFailure ??= failure;
            _initialFailureSignal.TrySetResult(failure);
        }

        // Count here: native failure reporting can coalesce repeated identical errors and retains
        // the installed token while scheduling another refresh (FM-06):
        // https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_sasl_oauthbearer.c#L483-L497
        _metrics.Record(failure);
        try
        {
            reportFailure(reason);
        }
        catch (Exception)
        {
            // Native error reporting must not escape the refresh callback.
        }
    }
}
