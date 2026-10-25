using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Publication;

/// <summary>Immutable deployment identity used by LoadGen registration and publication objects.</summary>
/// <param name="Identity">The six-field artifact identity.</param>
/// <param name="Phase">The selected execution phase.</param>
/// <param name="RepeatNumber">The one-based repeat number.</param>
/// <param name="JobName">The Kubernetes Job identity.</param>
/// <param name="PodName">The Kubernetes pod name.</param>
/// <param name="PodUid">The Kubernetes pod UID.</param>
/// <param name="SourceCommit">The lowercase source commit SHA.</param>
public sealed record ProducerRunContext(RunIdentity Identity, string Phase, int RepeatNumber, string JobName,
    string PodName, string PodUid, string SourceCommit)
{
    /// <summary>Gets the run and repeat prefix.</summary>
    public string RepeatPrefix => $"{Identity.RunId}/{Phase}/r{RepeatNumber}/";
    /// <summary>Gets the producer instance prefix.</summary>
    public string InstancePrefix => $"{RepeatPrefix}producer/{Uri.EscapeDataString(PodName)}/";
    /// <summary>Gets the create-only registration object path.</summary>
    public string RegistrationPath => InstancePrefix + "registration.json";
    /// <summary>Gets the accepted publication object prefix.</summary>
    public string PublicationPrefix => InstancePrefix + "publication/";
    /// <summary>Gets the diagnostic object prefix.</summary>
    public string DiagnosticsPrefix => InstancePrefix + "diagnostics/";
    /// <summary>Gets the create-only completion object path.</summary>
    public string CompletionPath => InstancePrefix + "completion.json";

    /// <summary>Validates the stable identity, phase, repeat and Job relationships.</summary>
    public void Validate()
    {
        Identity.Validate();
        bool validPair = Phase switch
        {
            "probe" => Identity.ConfigurationId == "C0",
            "C0" => Identity.ConfigurationId == "C0",
            "C3" => Identity.ConfigurationId == "C3",
            _ => false
        };
        if (!validPair || RepeatNumber is < 1 or > 5 ||
            string.IsNullOrWhiteSpace(JobName) || string.IsNullOrWhiteSpace(PodName) ||
            string.IsNullOrWhiteSpace(PodUid) || PodUid.Any(char.IsWhiteSpace) || PodUid.Any(char.IsControl) ||
            SourceCommit is null || SourceCommit.Length != 40 ||
            SourceCommit.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new InvalidDataException("producer_run_context_invalid");
        if (!System.Text.RegularExpressions.Regex.IsMatch(Identity.RunId, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant) || Identity.RunId.Length > 29)
            throw new InvalidDataException("producer_run_context_run_id_invalid");
        if (!StringComparer.Ordinal.Equals(JobName, $"{Identity.RunId}-{Phase.ToLowerInvariant()}-r{RepeatNumber}-loadgen") ||
            !StringComparer.Ordinal.Equals(PodName, Identity.InstanceId))
            throw new InvalidDataException("producer_run_context_identity_mismatch");
    }
}
