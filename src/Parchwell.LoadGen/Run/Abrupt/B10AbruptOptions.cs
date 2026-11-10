using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptOptions
{
    internal const int OriginalCount = 5;
    internal const string Prefix = "B10Abrupt__";
    internal enum ValidationDetail
    {
        Unclassified, MixedPrefix, SettingNameOrDuplicate, DiagnosticIdentity, RequiredIdentity,
        RunSourceOrImage, PodOrNamespace, JobName, BlobEndpoint, AbsolutePath, FixedPolicy,
        Role, Binding, ApphostProcessPathMismatch, ApphostMissing
    }
    private static readonly string[] OtherPrefixes = ["LoadGen__", "Verifier__", "SingleAction__", "ProbeV2__", "IdleObservation__", "SeedF04__"];
    internal static IReadOnlyDictionary<string, int> Policy { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["MaxRecordBytes"] = 65536, ["QueueCapacity"] = 256, ["MaxJournalBytes"] = 16777216,
        ["MaxJournalEntries"] = 4096, ["MaxAuxiliaryBytes"] = 65536, ["MaxJsonDepth"] = 32,
        ["ReleaseDelaySeconds"] = 120, ["ProbeRate"] = 100, ["CutOrdinal"] = 4
    };
    private readonly Dictionary<string, string> _values;
    public B10AbruptOptions() : this(new Dictionary<string, string>(StringComparer.Ordinal)) { }
    internal void CopyFrom(B10AbruptOptions other) { foreach (var pair in other._values) _values.Add(pair.Key, pair.Value); }
    private B10AbruptOptions(Dictionary<string, string> values) => _values = values;
    public bool Enabled => Get("Enabled") == "true";
    public string Purpose => Get("Purpose");
    public string Role => Get("Role");
    public string RunId => Get("RunId");
    public string ScenarioId => Get("ScenarioId");
    public string ConfigurationId => Get("ConfigurationId");
    public string Phase => Get("Phase");
    public int Repeat => int.TryParse(Get("Repeat"), CultureInfo.InvariantCulture, out int value) ? value : 0;
    public string SourceCommit => Get("SourceCommit");
    public string ImageReference => Get("ImageReference");
    public string JobName => Get("JobName");
    public string PodName => Get("PodName");
    public string PodUid => Get("PodUid");
    public string NamespaceName => Get("NamespaceName");
    public string BlobEndpoint => Get("BlobEndpoint");
    public string ScenarioSourceRoot => Get("ScenarioSourceRoot");
    public string OutputRoot => Get("OutputRoot");
    public string StartLineBindingPath => Get("StartLineBindingPath");
    public string PredecessorBindingPath => Get("PredecessorBindingPath");
    private string Get(string key) => _values.GetValueOrDefault(key, string.Empty);

    internal static bool HasReservedEnvironment() => Environment.GetEnvironmentVariables().Keys.Cast<string>()
        .Any(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));

    internal static B10AbruptOptions Read() => Read(Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
        .Select(entry => new KeyValuePair<string, string>((string)entry.Key, entry.Value?.ToString() ?? string.Empty)));

    internal static B10AbruptOptions Read(IEnumerable<KeyValuePair<string, string>> environment)
    {
        HashSet<string> names = ["Enabled", "Purpose", "Role", "RunId", "ScenarioId", "ConfigurationId", "Phase", "Repeat",
            "SourceCommit", "ImageReference", "JobName", "PodName", "PodUid", "NamespaceName", "BlobEndpoint",
            "ScenarioSourceRoot", "OutputRoot", "StartLineBindingPath", "PredecessorBindingPath"];
        names.UnionWith(Policy.Keys);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in environment)
        {
            if (OtherPrefixes.Any(prefix => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) throw Invalid(ValidationDetail.MixedPrefix);
            if (!entry.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string name = entry.Key[Prefix.Length..];
            if (!entry.Key.StartsWith(Prefix, StringComparison.Ordinal) || !names.Contains(name) || !values.TryAdd(name, entry.Value))
                throw Invalid(ValidationDetail.SettingNameOrDuplicate);
        }
        return new B10AbruptOptions(values);
    }

    internal void Validate(string? expectedRole = null)
    {
        if (!Enabled || Purpose != "diagnostic_trial" || ScenarioId != "B10" || ConfigurationId != "C1" || Phase != "C1" ||
            Get("Repeat") != "1" || (expectedRole is not null && Role != expectedRole)) throw Invalid(ValidationDetail.DiagnosticIdentity);
        ValidateRole();
        foreach (string name in new[] { "RunId", "SourceCommit", "ImageReference", "JobName", "PodName", "PodUid", "NamespaceName", "BlobEndpoint", "ScenarioSourceRoot", "OutputRoot" })
            if (string.IsNullOrWhiteSpace(Get(name)) || Get(name).Any(char.IsControl)) throw Invalid(ValidationDetail.RequiredIdentity);
        if (!Regex.IsMatch(RunId, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant) || RunId.Length > 29 ||
            !Regex.IsMatch(SourceCommit, "^[a-f0-9]{40}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(ImageReference, "^[a-z0-9][a-z0-9./:_-]*@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)) throw Invalid(ValidationDetail.RunSourceOrImage);
        foreach (string value in new[] { PodName, PodUid, NamespaceName })
            if (value.Any(char.IsWhiteSpace) || value.Contains('/', StringComparison.Ordinal) || value is "." or ".." or "seed") throw Invalid(ValidationDetail.PodOrNamespace);
        if (JobName != $"{RunId}-c1-r1-loadgen-{(Role == "successor" ? "successor" : "restart")}") throw Invalid(ValidationDetail.JobName);
        if (!Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
            endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0) throw Invalid(ValidationDetail.BlobEndpoint);
        foreach (string path in new[] { ScenarioSourceRoot, OutputRoot, Role == "successor" ? PredecessorBindingPath : StartLineBindingPath })
            if (!Path.IsPathFullyQualified(path) || path.Split(Path.DirectorySeparatorChar).Contains("..", StringComparer.Ordinal)) throw Invalid(ValidationDetail.AbsolutePath);
        foreach (var policy in Policy)
            if (_values.TryGetValue(policy.Key, out string? value) && value != policy.Value.ToString(CultureInfo.InvariantCulture)) throw Invalid(ValidationDetail.FixedPolicy);
    }

    internal void ValidateRole()
    {
        if (Role is not ("parent" or "successor" or "child")) throw Invalid(ValidationDetail.Role);
        string required = Role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath";
        string rejected = Role == "successor" ? "StartLineBindingPath" : "PredecessorBindingPath";
        if (string.IsNullOrWhiteSpace(Get(required)) || _values.ContainsKey(rejected)) throw Invalid(ValidationDetail.Binding);
    }

    internal IReadOnlyDictionary<string, string> ToChildEnvironment() => _values.Where(pair => pair.Key is not
        ("Role" or "PredecessorBindingPath"))
        .ToDictionary(pair => Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal);

    internal B10AbruptWire.Identity Identity(string? predecessor = null) => new(RunId, ScenarioId, ConfigurationId, Phase,
        Repeat, SourceCommit, ImageReference, JobName, PodName, PodUid, PodName, predecessor);
    private static InvalidDataException Invalid(ValidationDetail detail, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) => B10AbruptDiagnostics.Attach(WithDetail(new InvalidDataException("b10_abrupt_configuration_invalid"), detail), null, file, member, line);

    internal static T WithDetail<T>(T exception, ValidationDetail detail) where T : Exception
    {
        exception.Data["validation_detail"] = detail;
        return exception;
    }

    internal static string ReadValidationDetail(Exception exception) =>
        exception.Data["validation_detail"] is ValidationDetail detail && Enum.IsDefined(detail)
            ? detail.ToString() : nameof(ValidationDetail.Unclassified);
}
