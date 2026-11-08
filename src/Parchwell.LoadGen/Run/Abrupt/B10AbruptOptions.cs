using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed class B10AbruptOptions
{
    internal const string Prefix = "B10Abrupt__";
    private static readonly string[] OtherPrefixes = ["LoadGen__", "Verifier__", "SingleAction__", "ProbeV2__", "IdleObservation__", "SeedF04__"];
    internal static IReadOnlyDictionary<string, int> Policy { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["MaxFrameBytes"] = 65536, ["QueueCapacity"] = 256, ["MaxJournalBytes"] = 16777216,
        ["MaxJournalEntries"] = 4096, ["MaxAuxiliaryBytes"] = 65536, ["MaxJsonDepth"] = 32,
        ["ArmTimeoutSeconds"] = 30, ["KillTimeoutSeconds"] = 10, ["ReapTimeoutSeconds"] = 10,
        ["EofTimeoutSeconds"] = 10, ["ReleaseDelaySeconds"] = 120
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
    public string EventsPipeHandle => Get("EventsPipeHandle");
    public string AckPipeHandle => Get("AckPipeHandle");
    public string IdentityChallenge => Get("IdentityChallenge");
    private string Get(string key) => _values.GetValueOrDefault(key, string.Empty);

    internal static bool HasReservedEnvironment() => Environment.GetEnvironmentVariables().Keys.Cast<string>()
        .Any(name => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase));

    internal static B10AbruptOptions Read() => Read(Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
        .Select(entry => new KeyValuePair<string, string>((string)entry.Key, entry.Value?.ToString() ?? string.Empty)));

    internal static B10AbruptOptions Read(IEnumerable<KeyValuePair<string, string>> environment)
    {
        HashSet<string> names = ["Enabled", "Purpose", "Role", "RunId", "ScenarioId", "ConfigurationId", "Phase", "Repeat",
            "SourceCommit", "ImageReference", "JobName", "PodName", "PodUid", "NamespaceName", "BlobEndpoint",
            "ScenarioSourceRoot", "OutputRoot", "StartLineBindingPath", "PredecessorBindingPath", "EventsPipeHandle", "AckPipeHandle", "IdentityChallenge"];
        names.UnionWith(Policy.Keys);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in environment)
        {
            if (OtherPrefixes.Any(prefix => entry.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) throw Invalid();
            if (!entry.Key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string name = entry.Key[Prefix.Length..];
            if (!entry.Key.StartsWith(Prefix, StringComparison.Ordinal) || !names.Contains(name) || !values.TryAdd(name, entry.Value))
                throw Invalid();
        }
        return new B10AbruptOptions(values);
    }

    internal void Validate(string? expectedRole = null)
    {
        if (!Enabled || Purpose != "diagnostic_trial" || ScenarioId != "B10" || ConfigurationId != "C1" || Phase != "C1" ||
            Get("Repeat") != "1" || (expectedRole is not null && Role != expectedRole)) throw Invalid();
        ValidateRole();
        foreach (string name in new[] { "RunId", "SourceCommit", "ImageReference", "JobName", "PodName", "PodUid", "NamespaceName", "BlobEndpoint", "ScenarioSourceRoot", "OutputRoot" })
            if (string.IsNullOrWhiteSpace(Get(name)) || Get(name).Any(char.IsControl)) throw Invalid();
        if (!Regex.IsMatch(RunId, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant) || RunId.Length > 29 ||
            !Regex.IsMatch(SourceCommit, "^[a-f0-9]{40}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(ImageReference, "^[a-z0-9][a-z0-9./:_-]*@sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)) throw Invalid();
        foreach (string value in new[] { PodName, PodUid, NamespaceName })
            if (value.Any(char.IsWhiteSpace) || value.Contains('/', StringComparison.Ordinal) || value is "." or ".." or "seed") throw Invalid();
        if (JobName != $"{RunId}-c1-r1-loadgen{(Role == "successor" ? "2" : string.Empty)}") throw Invalid();
        if (!Uri.TryCreate(BlobEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
            endpoint.AbsolutePath != "/" || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0) throw Invalid();
        foreach (string path in new[] { ScenarioSourceRoot, OutputRoot, Role == "successor" ? PredecessorBindingPath : StartLineBindingPath })
            if (!Path.IsPathFullyQualified(path) || path.Split(Path.DirectorySeparatorChar).Contains("..", StringComparer.Ordinal)) throw Invalid();
        foreach (var policy in Policy)
            if (_values.TryGetValue(policy.Key, out string? value) && value != policy.Value.ToString(CultureInfo.InvariantCulture)) throw Invalid();
    }

    internal void ValidateRole()
    {
        if (Role is not ("supervisor" or "successor" or "child")) throw Invalid();
        string required = Role == "successor" ? "PredecessorBindingPath" : "StartLineBindingPath";
        string rejected = Role == "successor" ? "StartLineBindingPath" : "PredecessorBindingPath";
        if (string.IsNullOrWhiteSpace(Get(required)) || _values.ContainsKey(rejected)) throw Invalid();
        string[] childFields = ["EventsPipeHandle", "AckPipeHandle", "IdentityChallenge"];
        if (Role != "child")
        {
            if (childFields.Any(_values.ContainsKey)) throw Invalid();
        }
        else if (!Regex.IsMatch(IdentityChallenge, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) ||
            !long.TryParse(EventsPipeHandle, NumberStyles.None, CultureInfo.InvariantCulture, out long events) || events < 0 ||
            !long.TryParse(AckPipeHandle, NumberStyles.None, CultureInfo.InvariantCulture, out long ack) || ack < 0 || events == ack) throw Invalid();
    }

    internal IReadOnlyDictionary<string, string> ToChildEnvironment() => _values.Where(pair => pair.Key is not
        ("Role" or "EventsPipeHandle" or "AckPipeHandle" or "IdentityChallenge" or "PredecessorBindingPath"))
        .ToDictionary(pair => Prefix + pair.Key, pair => pair.Value, StringComparer.Ordinal);

    internal B10AbruptWire.Identity Identity(string? predecessor = null) => new(RunId, ScenarioId, ConfigurationId, Phase,
        Repeat, SourceCommit, ImageReference, JobName, PodName, PodUid, PodName, predecessor);
    private static InvalidDataException Invalid() => new("b10_abrupt_configuration_invalid");
}
