#pragma warning disable CA1707 // Repository-required Given/When/Then test names.
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.LoadGen.Run.Input;
using Parchwell.Runner.Run.Deployment;
using Parchwell.Scenarios;
using Xunit;

namespace Parchwell.Runner.Tests.Deployment;

public sealed class DeploymentGeneratorTests
{
    [Fact]
    public async Task Given_IndependentVerifierInputCapacity_When_Generated_Then_EmitsBothExplicitBoundsUnchanged()
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> input = fixture.Input("C0");
        DeploymentFiles files = await fixture.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), fixture.Directory, TestContext.Current.CancellationToken);
        JsonElement verifier = files.Runtime.GetProperty("verifier_environment");
        Assert.Equal("4194304", verifier.GetProperty("Verifier__MaxInputEntries").GetString());
        Assert.Equal("186000", verifier.GetProperty("Verifier__MaxInventoryEntries").GetString());
    }

    [Theory]
    [InlineData("MaxAttempts", null)]
    [InlineData("MaxAttempts", "\"186000\"")]
    [InlineData("MaxAttempts", "-1")]
    [InlineData("MaxAttempts", "0")]
    [InlineData("UploadTimeout", null)]
    [InlineData("UploadTimeout", "30")]
    [InlineData("UploadTimeout", "\"-00:00:01\"")]
    [InlineData("UploadTimeout", "\"00:00:00\"")]
    public async Task Given_MissingOrInvalidDeploymentBound_When_Generated_Then_NoDeploymentFilesAreEmitted(string field, string? json)
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> input = fixture.Input("C0");
        string limitsPath = Path.Combine(fixture.Directory, "limits.json");
        JsonNode limits = JsonNode.Parse(await File.ReadAllTextAsync((string)input["limits_file"]!, TestContext.Current.CancellationToken))!;
        if (json is null) limits["loadgen"]!.AsObject().Remove(field);
        else limits["loadgen"]![field] = JsonNode.Parse(json);
        await File.WriteAllTextAsync(limitsPath, limits.ToJsonString(), TestContext.Current.CancellationToken);
        input["limits_file"] = limitsPath;
        string output = Path.Combine(fixture.Directory, "generated");

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), output, TestContext.Current.CancellationToken));

        Assert.False(System.IO.Directory.Exists(output));
    }

    [Theory]
    [InlineData("C0")]
    [InlineData("C3")]
    public async Task Given_ThreeTerraformServiceAccounts_When_Generated_Then_OnlySchemaPermittedWorkerIdentitiesRemain(string phase)
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> input = fixture.Input(phase);
        JsonElement sourceAccounts = JsonSerializer.SerializeToElement(input).GetProperty("ephemeral_outputs")
            .GetProperty("workload_identity").GetProperty("service_accounts");
        Assert.Equal(["enrichment-consumer", "loadgen", "verifier"], sourceAccounts.EnumerateObject()
            .Select(property => property.Name).Order(StringComparer.Ordinal));
        DeploymentFiles files = await fixture.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), fixture.Directory, TestContext.Current.CancellationToken);
        using JsonDocument envelope = JsonDocument.Parse(await File.ReadAllBytesAsync(files.RuntimeValuesPath, TestContext.Current.CancellationToken));
        using JsonDocument schema = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(DeploymentFixture.RepositoryRoot, "charts/parchwell/values.schema.json"), TestContext.Current.CancellationToken));
        JsonElement accountSchema = schema.RootElement.GetProperty("properties").GetProperty("runtime")
            .GetProperty("properties").GetProperty("service_accounts");
        Assert.False(accountSchema.GetProperty("additionalProperties").GetBoolean());
        foreach (JsonElement runtime in new[] { files.Runtime, envelope.RootElement.GetProperty("runtime") })
        {
            JsonElement accounts = runtime.GetProperty("service_accounts");
            string[] names = accounts.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(accountSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal), names);
            Assert.Equal(accountSchema.GetProperty("required").EnumerateArray().Select(property => property.GetString()).Order(StringComparer.Ordinal), names);
            Assert.False(accounts.TryGetProperty("enrichment-consumer", out _));
            foreach (string role in names)
            {
                JsonElement original = sourceAccounts.GetProperty(role), actual = accounts.GetProperty(role);
                Assert.Equal(original.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal),
                    actual.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
                foreach (string field in new[] { "name", "client_id", "subject" })
                    Assert.Equal(original.GetProperty(field).GetString(), actual.GetProperty(field).GetString());
            }
        }
        JsonElement loadgen = files.Runtime.GetProperty("loadgen_environment");
        Assert.Equal("/deployment/generated-values.yaml", loadgen.GetProperty("LoadGen__GeneratedValuesFile").GetString());
        Assert.Equal(loadgen.GetProperty("PARCHWELL_GENERATED_VALUES_FILE").GetString(), loadgen.GetProperty("LoadGen__GeneratedValuesFile").GetString());
    }

    [Theory]
    [InlineData("C0", "60000")]
    [InlineData("C3", "20000")]
    public async Task Given_RealScenario_When_Generated_Then_BothActualWorkerResolutionsMatch(string phase, string socket)
    {
        using var fixture = new DeploymentFixture();
        DeploymentFiles result = await fixture.GenerateAsync(phase);
        string loadgen = await File.ReadAllTextAsync(result.LoadGenValuesPath, TestContext.Current.CancellationToken);
        string verifier = await File.ReadAllTextAsync(result.VerifierValuesPath, TestContext.Current.CancellationToken);
        Assert.NotEqual(loadgen, verifier);
        Assert.Contains("\"socket.timeout.ms\": \"" + socket + "\"", loadgen, StringComparison.Ordinal);
        Assert.Contains("\"request.timeout.ms\": \"60000\"", loadgen, StringComparison.Ordinal);
        Assert.Contains("\"message.timeout.ms\": \"300000\"", loadgen, StringComparison.Ordinal);
        Assert.Contains("\"seed\": 1401", verifier, StringComparison.Ordinal);
        Assert.Equal(verifier, await File.ReadAllTextAsync(result.GeneratedValuesPath, TestContext.Current.CancellationToken));
        using JsonDocument envelope = JsonDocument.Parse(await File.ReadAllTextAsync(result.RuntimeValuesPath, TestContext.Current.CancellationToken));
        Assert.Equal(["runtime"], envelope.RootElement.EnumerateObject().Select(property => property.Name));
        foreach (JsonElement runtime in new[] { result.Runtime, envelope.RootElement.GetProperty("runtime") })
        {
            Assert.False(runtime.TryGetProperty("service_tags", out _));
            Assert.Equal("203.0.113.1", runtime.GetProperty("eventhubs_ip").GetString());
            Assert.Equal("203.0.113.2", runtime.GetProperty("blob_ip").GetString());
        }
        foreach (string role in new[] { "verifier" })
        {
            Dictionary<string, string> env = result.Runtime.GetProperty(role + "_environment").Deserialize<Dictionary<string, string>>()!;
            env[(role == "loadgen" ? "LoadGen__ScenarioSourceRoot" : "Verifier__ScenarioRoot")] = DeploymentFixture.RepositoryRoot;
            env["Verifier__TenantId"] = "synthetic-tenant";
            env["Verifier__WorkloadIdentityClientId"] = "synthetic-client";
            env["Verifier__FederatedTokenFilePath"] = "/tmp/synthetic-token";
            string[] args = env.SelectMany(pair => new[] { "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value }).ToArray();
            Type program = role == "loadgen" ? typeof(LoadGenHostOptions).Assembly.GetType("Parchwell.LoadGen.Program")! : typeof(Parchwell.Verifier.VerifierHostOptions).Assembly.GetType("Parchwell.Verifier.Program")!;
            MethodInfo method = program.GetMethod("BuildHost", BindingFlags.NonPublic | BindingFlags.Static)!;
            object?[] parameters = role == "loadgen" ? [args, null, null] : [args, null];
            using IHost host = (IHost)method.Invoke(null, parameters)!;
            if (role == "loadgen") host.Services.GetRequiredService<IOptions<LoadGenHostOptions>>().Value.Validate();
            else Assert.True(new Parchwell.Verifier.VerifierHostOptionsValidator().Validate(null,
                host.Services.GetRequiredService<IOptions<Parchwell.Verifier.VerifierHostOptions>>().Value).Succeeded);
            ScenarioLoader workerLoader = host.Services.GetRequiredService<ScenarioLoader>();
            ScenarioResolver workerResolver = host.Services.GetRequiredService<ScenarioResolver>();
            LoadedScenario actual = await workerLoader.LoadAsync("A01", TestContext.Current.CancellationToken);
            ResolvedScenario resolved = workerResolver.Resolve(actual);
            HelmValuesGenerator workerGenerator = host.Services.GetRequiredService<HelmValuesGenerator>();
            string expected = workerGenerator.Generate(resolved, phase);
            GeneratedValuesCompatibility.Validate(workerGenerator, resolved, phase, role == "loadgen" ? result.LoadGenValuesPath : result.VerifierValuesPath);
            Assert.Throws<InvalidOperationException>(() => GeneratedValuesCompatibility.Validate(workerGenerator, resolved, phase,
                role == "loadgen" ? result.VerifierValuesPath : result.LoadGenValuesPath));
            Assert.Equal(role == "loadgen" ? loadgen : verifier, expected);
        }
    }

    [Theory]
    [InlineData("start-line")]
    [InlineData("verify")]
    public async Task Given_VerifierMode_When_Generated_Then_ActualModeValidatorAcceptsEveryRequiredOption(string mode)
    {
        using var fixture = new DeploymentFixture();
        DeploymentFiles files = await fixture.GenerateAsync("C0", mode);
        Dictionary<string, string> env = files.Runtime.GetProperty("verifier_environment").Deserialize<Dictionary<string, string>>()!;
        env["Verifier__TenantId"] = "synthetic-tenant";
        env["Verifier__WorkloadIdentityClientId"] = "synthetic-client";
        env["Verifier__FederatedTokenFilePath"] = "/tmp/synthetic-token";
        foreach (string key in env.Keys.Where(key => key.StartsWith("Verifier__", StringComparison.Ordinal)))
        {
            string property = key["Verifier__".Length..].Split("__", StringSplitOptions.None)[0];
            Assert.NotNull(typeof(Parchwell.Verifier.VerifierHostOptions).GetProperty(property));
        }
        string[] args = env.SelectMany(pair => new[] { "--" + pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value }).ToArray();
        using IHost host = Parchwell.Verifier.Program.BuildHost(args);
        var options = host.Services.GetRequiredService<IOptions<Parchwell.Verifier.VerifierHostOptions>>().Value;
        var validation = new Parchwell.Verifier.VerifierHostOptionsValidator().Validate(null, options);
        Assert.True(validation.Succeeded, string.Join(";", validation.Failures ?? []));
    }

    [Fact]
    public async Task Given_PoisonedExpectedOutcome_When_Generated_Then_ItIsNeverRead()
    {
        using var fixture = new DeploymentFixture();
        string path = Path.Combine(fixture.Directory, "checkout");
        System.IO.Directory.CreateDirectory(Path.Combine(path, "scenarios", "A01"));
        System.IO.Directory.CreateDirectory(Path.Combine(path, "scenarios", "profiles"));
        foreach (string relative in new[] { "scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml" })
            File.Copy(Path.Combine(DeploymentFixture.RepositoryRoot, relative), Path.Combine(path, relative));
        await File.WriteAllTextAsync(Path.Combine(path, "scenarios", "A01", "expected.yaml"), "!!poison: never parse this", TestContext.Current.CancellationToken);
        DeploymentFiles files = await fixture.GenerateAsync("C0", sourceRoot: path);
        Assert.True(File.Exists(files.GeneratedValuesPath));
    }

    [Theory]
    [InlineData("contract_version", "1")]
    [InlineData("scenario_id", "\"other\"")]
    [InlineData("kafka", "{}")]
    [InlineData("profile", "{}")]
    public async Task Given_RuntimeOverridesContract_When_Generated_Then_ItFails(string property, string json)
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> input = fixture.Input("C0");
        input[property] = JsonDocument.Parse(json).RootElement.Clone();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), fixture.Directory, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_WrongImageSource_When_Generated_Then_ItFails()
    {
        using var fixture = new DeploymentFixture();
        Dictionary<string, object?> input = fixture.Input("C0");
        input["images"] = new { loadgen = new { source_commit = new string('b', 40), digest = "sha256:" + new string('a', 64) }, verifier = new { source_commit = new string('a', 40), digest = "sha256:" + new string('a', 64) } };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Generator().GenerateAsync(JsonSerializer.SerializeToElement(input), fixture.Directory, TestContext.Current.CancellationToken));
    }
}

internal sealed class DeploymentFixture : IDisposable
{
    public static string RepositoryRoot { get; } = FindRepository();
    private readonly string scenarioRoot = RepositoryRoot;
    private static readonly string[] TerraformWorkloads = ["loadgen", "enrichment-consumer", "verifier"];
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "parchwell-deployment-" + Guid.NewGuid().ToString("N"));
    public DeploymentFixture()
    {
        System.IO.Directory.CreateDirectory(Directory);
    }
    public DeploymentGenerator Generator(string? sourceRoot = null)
    {
        var source = new DeploymentScenarioSource(sourceRoot ?? scenarioRoot);
        return new(new ScenarioLoader(source, Options.Create(new ScenarioLoadOptions { MaxDocumentCharacters = 1048576, MaxNestingDepth = 32, MaxScalarCharacters = 65536, MaxParserEvents = 100000 })), new HelmValuesGenerator(), source);
    }
    public Dictionary<string, object?> Input(string phase, string mode = "verify") => new()
    {
        ["schema_version"] = 1, ["synthetic"] = true, ["source_commit"] = new string('a',40), ["phase"] = phase,
        ["subscription_guid"] = "00000000-0000-0000-0000-000000000001",
        ["run_id"] = "synthetic-a01-r1", ["producer_pod_name"] = "synthetic-producer-pod", ["mode"] = mode,
        ["limits_file"] = Path.Combine(scenarioRoot, "docs/operations/a01-runtime-limits.json"),
        ["expected_broker_names"] = new[] { "sasl_ssl://broker.example.invalid:9093/0" },
        ["images"] = new { loadgen = new { source_commit = new string('a',40), digest = "sha256:" + new string('a',64) }, verifier = new { source_commit = new string('a',40), digest = "sha256:" + new string('b',64) } },
        ["persistent_outputs"] = new { acr = new { login_server = "registry.example.invalid" } },
        ["ephemeral_outputs"] = new { eventhubs = new { namespace_name = "synthetic-ns" }, blob = new { account_url = "https://storage.example.invalid", containers = new { ledger = "ledger", verifier = "verifier" } }, private_endpoint_ips = new { eventhubs = "203.0.113.1", blob = "203.0.113.2" }, workload_identity = new { @namespace = "parchwell", service_accounts = TerraformWorkloads.ToDictionary(role => role, role => new { name = role, client_id = "synthetic-" + role, subject = "system:serviceaccount:parchwell:" + role }, StringComparer.Ordinal) } }
    };
    public Task<DeploymentFiles> GenerateAsync(string phase, string mode = "verify", string? sourceRoot = null) => Generator(sourceRoot).GenerateAsync(JsonSerializer.SerializeToElement(Input(phase, mode)), Path.Combine(Directory, phase + "-" + mode), TestContext.Current.CancellationToken);
    public void Dispose() => System.IO.Directory.Delete(Directory, true);
    private static string FindRepository()
    {
        string current = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(current, "Parchwell.slnx"))) current = Path.GetDirectoryName(current) ?? throw new DirectoryNotFoundException();
        return current;
    }
}
