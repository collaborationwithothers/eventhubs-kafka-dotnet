using Azure.Core;
using Confluent.Kafka;

namespace Parchwell.Kafka.Authentication;

/// <summary>Owns OAuth bearer token state for one native Kafka client lifetime.</summary>
/// <remarks>
/// This session exposes authentication evidence for its client. It does not decide whether a
/// scenario run is eligible to complete successfully.
/// </remarks>
public sealed class KafkaOAuthBearerSession
{
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
    private DateTimeOffset? _installedTokenExpiresOn;
    private KafkaAuthenticationFailure? _initialFailure;
    private KafkaAuthenticationFailure? _latestFailure;
    private bool _attached;
    private Confluent.Kafka.IClient? _callbackClient;

    /// <summary>Creates a session for one native Kafka client.</summary>
    /// <param name="identity">The scenario run and Kafka client identity.</param>
    /// <param name="scope">The validated Event Hubs token scope.</param>
    /// <param name="principalName">The workload application client ID reported to Kafka.</param>
    /// <param name="tokenProvider">The synchronous token acquisition boundary.</param>
    /// <param name="timeProvider">The clock used to validate token expiry.</param>
    /// <param name="metrics">The authentication failure metrics recorder.</param>
    /// <param name="cancellationToken">The run cancellation token forwarded to acquisition.</param>
    internal KafkaOAuthBearerSession(
        KafkaAuthenticationIdentity identity,
        string scope,
        string principalName,
        IKafkaTokenProvider tokenProvider,
        TimeProvider timeProvider,
        KafkaAuthenticationMetrics metrics,
        CancellationToken cancellationToken)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _principalName = principalName ?? throw new ArgumentNullException(nameof(principalName));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _cancellationToken = cancellationToken;
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
            AccessToken token;
            try
            {
                token = _tokenProvider.GetToken(_scope, _cancellationToken);
            }
            catch (Exception)
            {
                Fail(phase, "acquisition", reportFailure);
                return;
            }

            DateTimeOffset now;
            try
            {
                now = _timeProvider.GetUtcNow();
            }
            catch (Exception)
            {
                Fail(phase, "token_validation", reportFailure);
                return;
            }

            if (string.IsNullOrEmpty(token.Token) || token.ExpiresOn <= now)
            {
                Fail(phase, "token_validation", reportFailure);
                return;
            }

            try
            {
                install(token.Token, token.ExpiresOn.ToUnixTimeMilliseconds(), _principalName);
            }
            catch (Exception)
            {
                Fail(phase, "installation", reportFailure);
                return;
            }

            _installedTokenExpiresOn = token.ExpiresOn;
        }
    }

    private void Fail(string phase, string reason, Action<string> reportFailure)
    {
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
