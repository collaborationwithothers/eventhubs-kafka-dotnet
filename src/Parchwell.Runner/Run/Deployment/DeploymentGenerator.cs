using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;

namespace Parchwell.Runner.Run.Deployment;

public sealed record DeploymentFiles(string GeneratedValuesPath, string RuntimeValuesPath,
    string LoadGenValuesPath, string VerifierValuesPath, JsonElement Runtime, string HelmBinaryPath = "helm");

public sealed partial class DeploymentGenerator(ScenarioLoader loader, HelmValuesGenerator generator, IScenarioSource sourceFiles)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public async Task<DeploymentFiles> GenerateAsync(JsonElement input, string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        if (input.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("runtime_schema_invalid");
        foreach (string forbidden in new[] { "contract_version", "scenario_id", "configuration_id", "profile", "kafka", "runtime" })
            if (input.TryGetProperty(forbidden, out _)) throw new InvalidDataException("runtime_cannot_override_generated_values");
        string source = Text(input, "source_commit");
        if (!SourceSha().IsMatch(source)) throw new InvalidDataException("source_commit_invalid");
        string phase = Text(input, "phase");
        string configuration = phase switch { "probe" or "C0" => "C0", "C3" => "C3", _ => throw new InvalidDataException("phase_invalid") };
        string run = Text(input, "run_id"), producer = Text(input, "producer_pod_name");
        foreach (string name in new[] { run, producer })
            if (!DnsName().IsMatch(name) || name.Length > (name == run ? 40 : 63)) throw new InvalidDataException("deployment_identity_invalid");
        string mode = input.TryGetProperty("mode", out JsonElement modeValue) ? modeValue.GetString()! : "loadgen";
        if (mode is not ("loadgen" or "start-line" or "verify")) throw new InvalidDataException("deployment_mode_invalid");
        JsonElement ephemeral = input.GetProperty("ephemeral_outputs"), persistent = input.GetProperty("persistent_outputs");
        JsonElement eh = Output(ephemeral, "eventhubs"), blob = Output(ephemeral, "blob");
        JsonElement ips = Output(ephemeral, "private_endpoint_ips"), identity = Output(ephemeral, "workload_identity");
        if (Text(identity, "namespace") != "parchwell") throw new InvalidDataException("workload_namespace_invalid");
        var serviceAccounts = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (string role in new[] { "loadgen", "verifier" })
        {
            JsonElement serviceAccount = identity.GetProperty("service_accounts").GetProperty(role);
            if (Text(serviceAccount, "name") != role || string.IsNullOrWhiteSpace(Text(serviceAccount, "client_id")))
                throw new InvalidDataException("scoped_service_account_invalid");
            serviceAccounts.Add(role, serviceAccount.Clone());
        }
        string ns = Text(eh, "namespace_name"), account = Text(blob, "account_url");
        if (!Uri.TryCreate(account, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidDataException("blob_account_invalid");
        if (Text(blob.GetProperty("containers"), "ledger") != "ledger" || Text(blob.GetProperty("containers"), "verifier") != "verifier")
            throw new InvalidDataException("worker_containers_invalid");
        string registry = Text(Output(persistent, "acr"), "login_server");
        if (registry.Contains('/') || registry.Any(char.IsWhiteSpace)) throw new InvalidDataException("registry_invalid");
        foreach (string endpoint in new[] { "eventhubs", "blob" })
            if (!IPAddress.TryParse(Text(ips, endpoint), out IPAddress? ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new InvalidDataException("private_endpoint_ipv4_required");
        using JsonDocument limits = await ReadDocumentAsync(Text(input, "limits_file"), 1048576, cancellationToken).ConfigureAwait(false);
        if (limits.RootElement.GetProperty("schema_version").GetInt32() != 1) throw new InvalidDataException("limits_schema_invalid");
        using JsonDocument tags = await ReadDocumentAsync(Text(input, "service_tags_file"), 16777216, cancellationToken).ConfigureAwait(false);
        ServiceTagData.Validate(tags.RootElement);
        if (tags.RootElement.TryGetProperty("synthetic", out JsonElement synthetic) && synthetic.GetBoolean() &&
            (!input.TryGetProperty("synthetic", out JsonElement fixture) || !fixture.GetBoolean()))
            throw new InvalidDataException("synthetic_service_tags_cannot_deploy");
        LoadedScenario loaded = await loader.LoadAsync("A01", cancellationToken).ConfigureAwait(false);
        string definitionText = await ReadScenarioAsync(loaded.Definition.Source.Path, limits.RootElement.GetProperty("scenario_parsing").GetProperty("MaxDocumentCharacters").GetInt32(), cancellationToken).ConfigureAwait(false);
        string profileText = await ReadScenarioAsync(loaded.Profile.Source.Path, limits.RootElement.GetProperty("scenario_parsing").GetProperty("MaxDocumentCharacters").GetInt32(), cancellationToken).ConfigureAwait(false);
        bool requiresStatistics = loaded.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer && measure.Id.StartsWith("stats.", StringComparison.Ordinal));
        bool requiresRequests = loaded.Definition.Measures.Any(measure => measure.Role == KafkaClientRole.LoadGenProducer && measure.Id.StartsWith("request.", StringComparison.Ordinal));
        if (limits.RootElement.GetProperty("loadgen").GetProperty("RequireStatisticsEvidence").GetBoolean() != requiresStatistics ||
            limits.RootElement.GetProperty("loadgen").GetProperty("RequireRequestEvidence").GetBoolean() != requiresRequests)
            throw new InvalidDataException("evidence_requirements_do_not_match_measures");
        string loadgenValues = Resolve(loaded, ns, producer, producer, configuration);
        string verifierValues = Resolve(loaded, ns, producer, run + "-verifier", configuration);
        var loadgenEnv = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var verifierEnv = new SortedDictionary<string, string>(StringComparer.Ordinal);
        AddSection(loadgenEnv, limits.RootElement.GetProperty("loadgen"), "LoadGen__");
        AddSection(loadgenEnv, limits.RootElement.GetProperty("scenario_parsing"), "LoadGen__");
        AddSection(verifierEnv, limits.RootElement.GetProperty("verifier_shared"), "Verifier__");
        AddSection(verifierEnv, limits.RootElement.GetProperty("verifier_kafka"), "Verifier__");
        AddSection(verifierEnv, limits.RootElement.GetProperty(mode == "start-line" ? "verifier_start_line" : "verifier_verify"), "Verifier__");
        AddSection(verifierEnv, limits.RootElement.GetProperty("scenario_parsing"), "Verifier__ScenarioLoadOptions__");
        foreach (var pair in new Dictionary<string, string> { ["ScenarioId"] = "A01", ["ConfigurationId"] = configuration,
            ["RunId"] = run, ["NamespaceName"] = ns, ["ExpectedInstanceId"] = producer, ["ClientId"] = producer })
        { loadgenEnv["LoadGen__" + pair.Key] = pair.Value; verifierEnv["Verifier__" + pair.Key] = pair.Value; }
        loadgenEnv["LoadGen__ActualInstanceId"] = producer;
        loadgenEnv["LoadGen__PodName"] = producer;
        loadgenEnv["LoadGen__ScenarioSourceRoot"] = "/app";
        loadgenEnv["LoadGen__OutputRoot"] = "/work/loadgen";
        loadgenEnv["LoadGen__EnrichmentGroupId"] = "a01-unused-enrichment";
        loadgenEnv["LoadGen__BlobEndpoint"] = account;
        loadgenEnv["LoadGen__BlobContainer"] = "ledger";
        string[] brokers = input.TryGetProperty("expected_broker_names", out JsonElement brokerNames)
            ? brokerNames.EnumerateArray().Select(item => item.GetString() ?? throw new InvalidDataException("broker_name_invalid")).ToArray() : [];
        if (brokers.Length > 64 || brokers.Distinct(StringComparer.Ordinal).Count() != brokers.Length || brokers.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("broker_inventory_invalid");
        if (mode == "verify" && brokers.Length == 0) throw new InvalidDataException("independent_broker_inventory_required");
        // LoadGen admission replaces the initially empty inventory before host startup.
        loadgenEnv["LoadGen__ExpectedBrokerNamesCsv"] = string.Join(',', brokers);
        verifierEnv["Verifier__Mode"] = mode == "start-line" ? mode : "verify";
        verifierEnv["Verifier__InstanceId"] = producer;
        verifierEnv["Verifier__VerifierClientId"] = run + "-verifier";
        verifierEnv["Verifier__VerifierCommit"] = source;
        verifierEnv["Verifier__BlobServiceUri"] = account;
        verifierEnv["Verifier__VerifierContainer"] = "verifier";
        verifierEnv["Verifier__LedgerContainer"] = "ledger";
        verifierEnv["Verifier__ScenarioRoot"] = "/app";
        string prefix = phase + "/" + run;
        verifierEnv["Verifier__StartLineOutputName"] = prefix + "/start-line.json";
        verifierEnv["Verifier__BrokerInventoryOutputName"] = prefix + "/broker-inventory.json";
        verifierEnv["Verifier__StartLineInputName"] = prefix + "/start-line.json";
        verifierEnv["Verifier__ResultOutputName"] = prefix + "/result.json";
        verifierEnv["Verifier__ArtifactPrefix"] = "A01/" + configuration + "/" + run + "/" + producer;
        for (int i = 0; i < brokers.Length; i++) verifierEnv["Verifier__ExpectedBrokerNames__" + i.ToString(CultureInfo.InvariantCulture)] = brokers[i];
        foreach (var env in new[] { loadgenEnv, verifierEnv })
        {
            env["PARCHWELL_SOURCE_COMMIT"] = source;
            env["PARCHWELL_GENERATED_VALUES_FILE"] = "/deployment/generated-values.yaml";
        }
        loadgenEnv["PARCHWELL_ADMISSION_FILE"] = "/admission/admission.json";
        loadgenEnv["PARCHWELL_EXPECTED_RUN_ID"] = run;
        loadgenEnv["LoadGen__GeneratedValuesFile"] = "/deployment/generated-values.yaml";
        var runtime = new Dictionary<string, object?>
        {
            ["job_deadline_seconds"] = limits.RootElement.GetProperty("execution").GetProperty(mode switch { "loadgen" => "loadgen_job_seconds", "start-line" => "start_line_job_seconds", _ => "verify_job_seconds" }).GetInt32(),
            ["mode"] = mode, ["phase"] = phase, ["run_id"] = run, ["source_commit"] = source,
            ["definition_sha256"] = Hash(definitionText), ["profile_sha256"] = Hash(profileText),
            ["loadgen_generated_values"] = loadgenValues, ["verifier_generated_values"] = verifierValues,
            ["loadgen_generated_sha256"] = Hash(loadgenValues), ["verifier_generated_sha256"] = Hash(verifierValues),
            ["loadgen_environment"] = loadgenEnv, ["verifier_environment"] = verifierEnv,
            ["eventhubs_ip"] = Text(ips, "eventhubs"), ["blob_ip"] = Text(ips, "blob"),
            ["service_tags"] = tags.RootElement.Clone(), ["admission_name"] = run + "-admission",
            ["loadgen_image"] = Image(input, "loadgen", registry, source), ["verifier_image"] = Image(input, "verifier", registry, source),
            ["service_accounts"] = serviceAccounts
        };
        Directory.CreateDirectory(outputDirectory);
        string generated = Path.Combine(outputDirectory, "generated-values.yaml"), envelope = Path.Combine(outputDirectory, "runtime-values.yaml");
        string lg = Path.Combine(outputDirectory, "loadgen-values.yaml"), ver = Path.Combine(outputDirectory, "verifier-values.yaml");
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "scenario.yaml"), definitionText, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "saturation-ramp.yaml"), profileText, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(generated, verifierValues, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(lg, loadgenValues, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(ver, verifierValues, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(envelope, JsonSerializer.Serialize(new { runtime }, JsonOptions) + "\n", cancellationToken).ConfigureAwait(false);
        return new DeploymentFiles(generated, envelope, lg, ver, JsonSerializer.SerializeToElement(runtime), input.TryGetProperty("helm_binary_file", out JsonElement helmBinary) ? helmBinary.GetString()! : "helm");
    }

    private async Task<string> ReadScenarioAsync(string path, int maxCharacters, CancellationToken cancellationToken)
    {
        using TextReader reader = await sourceFiles.OpenReadAsync(path, cancellationToken).ConfigureAwait(false);
        char[] buffer = new char[4096];
        var text = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (text.Length + count > maxCharacters) throw new InvalidDataException("scenario_hash_input_exceeds_limit");
            text.Append(buffer, 0, count);
        }
        return text.ToString();
    }

    private string Resolve(LoadedScenario loaded, string ns, string producer, string verifier, string configuration)
    {
        var services = new ServiceCollection();
        foreach (KafkaClientRole role in loaded.Definition.Roles)
            services.AddParchwellKafka(role, options => { options.NamespaceName = ns; options.ClientId = role == KafkaClientRole.VerifierConsumer ? verifier : producer; options.EnrichmentGroupId = "a01-unused-enrichment"; });
        services.AddSingleton<ScenarioResolver>();
        using ServiceProvider provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<ScenarioResolver>();
        return generator.Generate(resolver.Resolve(loaded), configuration);
    }

    private static string Image(JsonElement input, string role, string registry, string source)
    {
        JsonElement image = input.GetProperty("images").GetProperty(role);
        string digest = Text(image, "digest");
        if (Text(image, "source_commit") != source || !ImageDigest().IsMatch(digest)) throw new InvalidDataException("immutable_image_receipt_mismatch");
        return registry + "/parchwell/" + role + "@" + digest;
    }
    private static void AddSection(IDictionary<string, string> target, JsonElement section, string prefix)
    {
        foreach (JsonProperty property in section.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object) AddSection(target, property.Value, prefix + property.Name + "__");
            else target[prefix + property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()! : property.Value.GetRawText();
        }
    }
    private static JsonElement Output(JsonElement outputs, string key)
    { JsonElement value = outputs.GetProperty(key); return value.TryGetProperty("value", out JsonElement nested) ? nested : value; }
    private static string Text(JsonElement value, string key)
    { string text = value.GetProperty(key).GetString() ?? ""; return !string.IsNullOrWhiteSpace(text) && !text.Any(char.IsControl) ? text : throw new InvalidDataException("runtime_text_invalid"); }
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static async Task<JsonDocument> ReadDocumentAsync(string path, int cap, CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > cap) throw new InvalidDataException("deployment_document_too_large");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > cap) throw new InvalidDataException("deployment_document_too_large");
        return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
    }
    [GeneratedRegex("^[a-f0-9]{40}$", RegexOptions.CultureInvariant)] private static partial Regex SourceSha();
    [GeneratedRegex("^sha256:[a-f0-9]{64}$", RegexOptions.CultureInvariant)] private static partial Regex ImageDigest();
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.CultureInvariant)] private static partial Regex DnsName();
}
