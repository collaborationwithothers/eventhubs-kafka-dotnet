using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal interface IProbeV2Inputs
{
    Task<byte[]?> ReadAsync(string path, int maximum, CancellationToken cancellationToken);
}

internal sealed class ProbeV2Inputs : IProbeV2Inputs
{
    public async Task<byte[]?> ReadAsync(string path, int maximum, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > maximum) throw new InputLimitExceededException("v2_capture_limit");
        byte[] bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes,cancellationToken).ConfigureAwait(false);
        if (stream.ReadByte() != -1) throw new InputLimitExceededException("v2_input_changed");
        return bytes;
    }
}

internal sealed class ProbeV2Resolver(ProbeV2Options options)
{
    internal ResolvedRoleConfiguration Resolve(LoadedScenario scenario,string client,string configuration,KafkaClientRole role)
    {
        var services = new ServiceCollection();
        foreach (var declared in scenario.Definition.Roles)
            services.AddParchwellKafka(declared,value =>
            {
                value.NamespaceName = options.NamespaceName; value.ClientId = client;
                value.EnrichmentGroupId = $"probe-v2-{options.RunId}-{options.ScenarioId.ToLowerInvariant()}-{configuration.ToLowerInvariant()}";
            });
        using var provider = services.BuildServiceProvider();
        return new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(scenario).Configurations[configuration].Roles[role];
    }
}

internal sealed record ProbeV2Publication(string CandidateName, string CandidateSha256);

internal sealed class ProbeV2Handler(ProbeV2Options options, IProbeV2Inputs inputs, IVerifierArtifactStore store,
    ScenarioLoader loader, ProbeV2Resolver resolver, TimeProvider clock)
{
    internal async Task<ProbeV2Publication> ExecuteAsync(CancellationToken cancellationToken)
    {
        int files = 0; long total = 0;
        async Task<byte[]?> Read(Func<CancellationToken,Task<byte[]?>> read, int maximum)
        {
            if (++files > options.JudgeLimits.MaxInputFiles) throw new InputLimitExceededException("v2_input_files");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.JudgeLimits.IoSeconds),clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token,cancellationToken);
            byte[]? bytes = await read(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            if (bytes is not null)
            {
                total = checked(total + bytes.Length);
                if (bytes.Length > maximum || total > options.JudgeLimits.MaxTotalInputBytes) throw new InputLimitExceededException("v2_input_bytes");
            }
            return bytes;
        }
        async Task<T?> Local<T>(string path,int maximum,List<string> problems,Action<byte[]>? raw = null) where T : class
        {
            try
            {
                byte[]? bytes = await Read(ct => inputs.ReadAsync(path,maximum,ct),maximum).ConfigureAwait(false);
                if (bytes is null) { problems.Add("v2_completion"); return null; }
                T value = ProbeV2Wire.Read<T>(bytes,maximum); raw?.Invoke(bytes); return value;
            }
            catch (Exception exception) when (Recoverable(exception,cancellationToken))
            {
                problems.Add(exception is InputLimitExceededException ? "v2_capture_limit" : exception is FormatException or System.Text.Json.JsonException ? "v2_wire" : "v2_completion");
                return null;
            }
        }
        var evidence = new Dictionary<(string Configuration,string Stage),ProbeV2Evidence>();
        foreach (var key in ProbeV2Matrix.Dependencies(options.ScenarioId,options.ConfigurationId))
        {
            string root = Path.Combine(options.InputRoot,ProbeV2Matrix.Job(options.RunId,options.ScenarioId,key.Configuration,key.Stage));
            var problems = new List<string>(); string? hash = null;
            var registration = await Local<ProbeV2Registration>(Path.Combine(root,"registration.json"),options.Limits.MaxRegistrationBytes,problems).ConfigureAwait(false);
            var capture = await Local<ProbeV2CaptureFile>(Path.Combine(root,"capture.json"),options.Limits.MaxCaptureBytes,problems,bytes => hash = Convert.ToHexStringLower(SHA256.HashData(bytes))).ConfigureAwait(false);
            var pod = await Local<ProbeV2Pod>(Path.Combine(root,"pod.json"),options.Limits.MaxPodBytes,problems).ConfigureAwait(false);
            evidence.Add(key,new(key.Configuration,key.Stage,registration,capture,pod,hash,problems.Distinct(StringComparer.Ordinal).ToArray()));
        }
        var scopeProblems = new List<string>();
        var scope = await Local<ProbeV2Scope>(Path.Combine(options.InputRoot,"scope","role-scope.json"),options.JudgeLimits.MaxRoleScopeBytes,scopeProblems).ConfigureAwait(false);
        LoadedScenario scenario = await loader.LoadAsync(options.ScenarioId,cancellationToken).ConfigureAwait(false);
        if (scenario.Definition.ProbeV2 is null) throw new InvalidOperationException("v2_declaration_required");
        var slots = new Dictionary<(string Configuration,string Stage),ProbeV2RepeatProjection[]>();
        foreach (var (key,value) in evidence)
        {
            string[] global = SingleActionConfigurationJudgement.V2EvidenceProblems(options,value);
            bool shape = value.Capture is not null && value.Capture.Repeats.Count == 5 && value.Capture.Repeats.Select(x => x.Repeat).SequenceEqual(Enumerable.Range(1,5));
            if (!shape)
            {
                string[] failed = global.Concat([value.Capture is null ? "v2_completion" : "v2_wire"]).Distinct(StringComparer.Ordinal).ToArray();
                slots.Add(key,Enumerable.Range(1,5).Select(ordinal => new ProbeV2RepeatProjection(ordinal,
                    failed.Any(x => x is "v2_wire" or "v2_identity") ? "invalid" : "incomplete",failed,null,null,null,new(null,null,null,null,null),null,null,null,[],[])).ToArray());
                continue;
            }
            var projections = new List<ProbeV2RepeatProjection>();
            foreach (var repeat in value.Capture!.Repeats)
            {
                var matrix = ProbeV2Matrix.For(options.ScenarioId,key.Configuration,key.Stage);
                IReadOnlyDictionary<string,string> settings = new Dictionary<string,string>(StringComparer.Ordinal); IEnumerable<string> omissions = [];
                if (repeat.Admission == "required" && repeat.ClientId is not null)
                {
                    var resolved = resolver.Resolve(scenario,repeat.ClientId,key.Configuration,matrix.Role);
                    settings = resolved.Settings.ToDictionary(x => x.Key,x => x.Value.Value,StringComparer.Ordinal);
                    omissions = resolved.Omissions.Select(x => x.Key).Concat(resolved.ScenarioOmissions.IsDefault ? [] : resolved.ScenarioOmissions.Select(x => x.Key));
                }
                var projection = SingleActionRepeatJudgement.JudgeV2Slot(options,value,repeat,settings,omissions);
                string[] reasons = projection.Reasons.Concat(global).Distinct(StringComparer.Ordinal).ToArray();
                projections.Add(projection with { Reasons = reasons,Status = projection.Status == "invalid" || global.Any(x => x is "v2_identity" or "v2_wire") ? "invalid" : reasons.Length == 0 ? "complete_valid" : "incomplete" });
            }
            slots.Add(key,projections.ToArray());
        }
        var previous = new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        bool previousLimited = false;
        string[] configurations = ProbeV2Matrix.Configurations(options.ScenarioId);
        if (configurations.Last() == options.ConfigurationId)
        {
            foreach (string configuration in configurations.TakeWhile(x => x != options.ConfigurationId))
            {
                try
                {
                    string previousPrefix = ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,configuration);
                    byte[]? candidateBytes = await Read(ct => store.ReadBoundedAsync(previousPrefix + "candidate.json",options.JudgeLimits.MaxCandidateBytes,ct),options.JudgeLimits.MaxCandidateBytes).ConfigureAwait(false);
                    byte[]? receiptBytes = await Read(ct => store.ReadBoundedAsync(previousPrefix + "judge-completion.json",options.JudgeLimits.MaxJudgeReceiptBytes,ct),options.JudgeLimits.MaxJudgeReceiptBytes).ConfigureAwait(false);
                    var candidate = candidateBytes is null ? null : ProbeV2Wire.Read<ProbeV2Candidate>(candidateBytes,options.JudgeLimits.MaxCandidateBytes);
                    var receipt = receiptBytes is null ? null : ProbeV2Wire.Read<ProbeV2JudgeReceipt>(receiptBytes,options.JudgeLimits.MaxJudgeReceiptBytes);
                    if (SingleActionConfigurationJudgement.V2Published(options,configuration,candidate,receipt,candidateBytes)) previous.Add(configuration,candidate!);
                }
                catch (Exception exception) when (Recoverable(exception,cancellationToken)) { previousLimited |= exception is InputLimitExceededException; }
            }
        }
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,scope,previous);
        if (scopeProblems.Contains("v2_wire",StringComparer.Ordinal)) result = result with { Status = "invalid",Deterministic = null,OutcomeClass = null,Diagnostics = [..result.Diagnostics,"v2_wire"] };
        if (previousLimited || scopeProblems.Contains("v2_capture_limit",StringComparer.Ordinal))
            result = result with { Status = result.Status == "invalid" ? "invalid" : "incomplete",Deterministic = null,OutcomeClass = null,Diagnostics = [..result.Diagnostics,"v2_capture_limit"] };
        byte[] output = ProbeV2Wire.Write(result,options.JudgeLimits.MaxCandidateBytes);
        string name = ProbeV2Matrix.Prefix(options.RunId,options.ScenarioId,options.ConfigurationId) + "candidate.json";
        using var publicationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.JudgeLimits.IoSeconds),clock);
        using var publicationToken = CancellationTokenSource.CreateLinkedTokenSource(publicationDeadline.Token,cancellationToken);
        await store.CreateAsync(name,output,publicationToken.Token).WaitAsync(publicationToken.Token).ConfigureAwait(false);
        return new(name,Convert.ToHexStringLower(SHA256.HashData(output)));
    }
    private static bool Recoverable(Exception exception,CancellationToken cancellationToken) => exception is FormatException or System.Text.Json.JsonException or IOException or InputLimitExceededException or TimeoutException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
}
