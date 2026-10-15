using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Scenarios.Tests;

/// <summary>
/// Shared synthetic inputs: a minimal valid A01 declaration and profile, parsing budgets and a Kafka
/// provider. Tests change one part of the valid documents, so a failure points at that part.
/// </summary>
internal static class ScenarioTestData
{
    internal const string ScenarioPath = "scenarios/A01/scenario.yaml";
    internal const string ProfilePath = "scenarios/profiles/synthetic.yaml";

    // These are authored synthetic declarations, not copied from a scenario catalog.
    internal const string ValidScenario = """
        schema_version: 1
        id: A01
        baseline: Parchwell.Kafka
        roles: [LoadGenProducer, VerifierConsumer]
        profile: profiles/synthetic.yaml
        configurations:
          baseline: {}
        fault:
          live: {kind: none}
          component: {kind: none}
        repeats: 5
        gate: {kind: none}
        measures:
          - {id: verifier.ok, role: VerifierConsumer}
        """;

    internal const string ValidProfile = """
        schema_version: 1
        id: synthetic
        seed: -17
        tenants:
          - {id: tenant-a, weight: 3}
          - {id: tenant-b, weight: 1}
        documents_per_burst: 2
        payloads:
          uploaded: [{size_bytes: 128, weight: 1}, {size_bytes: 512, weight: 2}]
          ocr-started: [{size_bytes: 64, weight: 1}]
          ocr-finished: [{size_bytes: 96, weight: 1}]
          classified: [{size_bytes: 80, weight: 1}]
          indexed: [{size_bytes: 40, weight: 1}]
        stages:
          - {id: warmup, offered_attempts_per_second: 2, duration_seconds: 3}
          - {id: measure, offered_attempts_per_second: 5, duration_seconds: 4}
        """;

    internal static ScenarioLoadOptions Limits(int document = 16_384, int depth = 20, int scalar = 1_024, int events = 2_000) => new()
    {
        MaxDocumentCharacters = document,
        MaxNestingDepth = depth,
        MaxScalarCharacters = scalar,
        MaxParserEvents = events
    };

    internal static ScenarioLoader Loader(IScenarioSource source, ScenarioLoadOptions? options = null) =>
        new(source, Options.Create(options ?? Limits()));

    internal static ServiceProvider Provider(params KafkaClientRole[] roles)
    {
        var services = new ServiceCollection();
        foreach (var role in roles)
            services.AddParchwellKafka(role, options =>
            {
                options.NamespaceName = "synthetic-namespace";
                options.ClientId = "synthetic-client";
                options.EnrichmentGroupId = "synthetic-group";
            });
        return services.BuildServiceProvider();
    }

    internal static InMemoryScenarioSource Source(string scenario = ValidScenario, string profile = ValidProfile) =>
        new((ScenarioPath, scenario), (ProfilePath, profile));
}

/// <summary>
/// An in-memory scenario source that records each requested path and each reader it hands out,
/// so tests can prove which files the loader opened and that it disposed every reader.
/// </summary>
internal sealed class InMemoryScenarioSource(params (string Path, string Text)[] documents) : IScenarioSource
{
    private readonly Dictionary<string, string> _documents = documents.ToDictionary(item => item.Path, item => item.Text, StringComparer.Ordinal);
    private readonly List<string> _requested = [];
    private readonly List<TrackingReader> _readers = [];

    internal IReadOnlyList<string> RequestedPaths => _requested;
    internal IReadOnlyList<TrackingReader> Readers => _readers;

    public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _requested.Add(logicalPath);
        if (!_documents.TryGetValue(logicalPath, out var contents))
            throw new FileNotFoundException("Synthetic fixture is absent: " + logicalPath);
        var reader = new TrackingReader(contents);
        _readers.Add(reader);
        return ValueTask.FromResult<TextReader>(reader);
    }
}

/// <summary>
/// A string reader that records disposal. The loader owns every reader it opens and must dispose
/// it (docs/operations/scenario-contract.md "Input and ownership").
/// </summary>
internal sealed class TrackingReader(string contents) : StringReader(contents)
{
    internal bool WasDisposed { get; private set; }

    protected override void Dispose(bool disposing)
    {
        WasDisposed = true;
        base.Dispose(disposing);
    }
}
