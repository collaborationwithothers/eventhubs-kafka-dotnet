using System.Text.Json;
using Parchwell.LoadGen.IdleObservation;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleObservation;

#pragma warning disable CA1707
public sealed class IdleObservationOptionsTests
{
    private static readonly string[] Bounds =
    [
        "ProcessLifetimeSeconds", "ActionWindowMs", "PostTerminalStatisticsSeconds", "CleanupSeconds", "RegistrationUploadSeconds",
        "CaptureUploadSeconds", "HostCleanupSeconds", "ReceiptUploadSeconds", "PollSliceMs", "MaxCaptureBytes", "MaxRegistrationBytes",
        "MaxCompletionBytes", "MaxStatisticsReportBytes", "MaxClientErrors", "MaxErrorBytes", "MaxDiagnosticsPerClient",
        "MaxDiagnosticBytes", "CallbackQueueCapacity", "MaxSettingsKeys", "MaxSettingsBytes"
    ];

    public static TheoryData<string> BoundNames()
    {
        var data = new TheoryData<string>();
        foreach (string bound in Bounds) data.Add(bound);
        return data;
    }

    [Theory]
    [InlineData("A02", "C0")]
    [InlineData("A02", "C1")]
    [InlineData("A03", "C0")]
    [InlineData("A03", "C1")]
    [InlineData("A03", "C2")]
    [InlineData("A04", "C0")]
    [InlineData("A04", "C1")]
    public void Given_CommittedLimitsAndAdmittedPair_When_Validating_Then_Accepts(string item, string configuration)
    {
        IdleObservationOptions options = Options();
        options.ScenarioId = item;
        options.ConfigurationId = configuration;
        options.JobName = $"run-1-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-idle";
        options.Validate();
        options.RunKind = "smoke";
        options.Validate();
    }

    [Fact]
    public void Given_LimitsFile_When_Reading_Then_CoversExactlyTheTwentyBounds()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs", "operations", "idle-observation-runtime-limits.json")));
        Assert.Equal(Bounds.Order(StringComparer.Ordinal), document.RootElement.GetProperty("capture").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(BoundNames))]
    public void Given_BoundChangedByOne_When_Validating_Then_RejectsFixedBounds(string property)
    {
        IdleObservationOptions options = Options();
        options.Validate();
        var info = typeof(IdleObservationOptions).GetProperty(property)!;
        info.SetValue(options, (int)info.GetValue(options)! + 1);
        var error = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Equal("idle_observation_fixed_bounds_invalid", error.Message);
    }

    [Theory]
    [InlineData("ScenarioId", "B01")]
    [InlineData("ConfigurationId", "C2")]
    [InlineData("RunKind", "probe")]
    [InlineData("JobName", "run-1-a02-c0-single")]
    [InlineData("BlobContainer", "single-action")]
    [InlineData("NamespaceName", "azure-ns")]
    [InlineData("BlobEndpoint", "http://example.blob.core.windows.net/")]
    public void Given_InvalidIdentity_When_Validating_Then_RejectsIdentity(string property, string wrong)
    {
        IdleObservationOptions options = Options();
        typeof(IdleObservationOptions).GetProperty(property)!.SetValue(options, wrong);
        var error = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Equal("idle_observation_identity_invalid", error.Message);
    }

    internal static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Parchwell.slnx"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("repository_root");
        return root;
    }

    internal static IdleObservationOptions Options()
    {
        var options = new IdleObservationOptions
        {
            ScenarioId = "A02", ConfigurationId = "C0", RunId = "run-1", RunKind = "batch", JobName = "run-1-a02-c0-idle",
            BlobContainer = "ledger", ScenarioRoot = "/app", SourceCommit = new string('a', 40), PodName = "pod-1", PodUid = "uid-1",
            NamespaceName = "namespace-test", BlobEndpoint = "https://example.blob.core.windows.net/",
            ScenarioLoadOptions = new() { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 }
        };
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs", "operations", "idle-observation-runtime-limits.json")));
        foreach (JsonProperty property in document.RootElement.GetProperty("capture").EnumerateObject())
            typeof(IdleObservationOptions).GetProperty(property.Name)!.SetValue(options, property.Value.GetInt32());
        return options;
    }
}
