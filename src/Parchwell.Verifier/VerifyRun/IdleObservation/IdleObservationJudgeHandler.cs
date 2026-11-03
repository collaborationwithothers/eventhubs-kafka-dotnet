using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.IdleObservation;

/// <summary>Reads capture blobs from the run's ledger container.</summary>
internal interface IIdleLedger
{
    Task<byte[]?> ReadAsync(string path, long maximum, CancellationToken token);
}

internal sealed class BlobIdleLedger(BlobContainerClient container) : IIdleLedger
{
    public Task<byte[]?> ReadAsync(string path, long maximum, CancellationToken token) =>
        BoundedBlobReader.ReadAsync(container.GetBlobClient(path), maximum, token);
}

/// <summary>Resolves the native settings a captured producer client should have been given.</summary>
internal sealed class IdleObservationResolver(IdleObservationJudgeOptions options)
{
    internal ResolvedRoleConfiguration Resolve(LoadedScenario scenario, string client, string configuration, KafkaClientRole role)
    {
        var services = new ServiceCollection();
        foreach (var declared in scenario.Definition.Roles)
            services.AddParchwellKafka(declared, value =>
            {
                value.NamespaceName = options.NamespaceName;
                value.ClientId = client;
            });
        using var provider = services.BuildServiceProvider();
        return new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(scenario).Configurations[configuration].Roles[role];
    }
}

/// <summary>Judges the idle-observation captures of the selected items of a run and publishes one candidate per selected item.</summary>
internal sealed class IdleObservationJudgeHandler(IdleObservationJudgeOptions options, IIdleLedger ledger, IVerifierArtifactStore store,
    ScenarioLoader loader, IdleObservationResolver resolver, TimeProvider clock)
{
    internal async Task<IdleCandidateReference[]> ExecuteAsync(CancellationToken cancellationToken)
    {
        var references = new List<IdleCandidateReference>();
        var pods = options.Pods();
        foreach (string item in options.SelectedItems())
        {
            LoadedScenario scenario = await loader.LoadAsync(item, cancellationToken).ConfigureAwait(false);
            if (scenario.Definition.IdleObservation is null) throw new InvalidOperationException("idle_declaration_required");
            var results = new Dictionary<string, IdleConfigurationResult>(StringComparer.Ordinal);
            string? managed = null, native = null;
            foreach (string configuration in IdleObservationJudgement.Configurations(item))
            {
                var judged = await JudgeConfigurationAsync(scenario, item, configuration, pods.TryGetValue(item + configuration, out var pod) ? pod : null, cancellationToken).ConfigureAwait(false);
                results.Add(configuration, judged.Result);
                if (judged.Capture is not null) { managed ??= judged.Capture.ManagedVersion; native ??= judged.Capture.NativeVersion; }
            }
            IdleItemAssessment assessment = IdleObservationJudgement.Assess(item, options.RunKind, results);
            var candidate = new IdleObservationCandidate(1, "idle-observation-judge", true, options.RunKind, options.RunId, item, options.SourceCommit,
                managed, native, options.ImageDigests(), [.. IdleObservationJudgement.Configurations(item).Select(c => results[c])], assessment,
                "not_applicable", item == "A02" ? ["k37_not_tested"] : []);
            byte[] output = SingleActionWire.Write(candidate, options.Limits.MaxCandidateBytes);
            string name = IdleObservationPaths.Candidate(options.RunId, item);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.Limits.IoSeconds), clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
            await store.CreateAsync(name, output, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            references.Add(new(item, name, Convert.ToHexStringLower(SHA256.HashData(output)), IdleObservationJudgement.ItemStatus(results.Values), assessment.Reason));
        }
        return [.. references];
    }

    private enum ReadProblem { None, Missing, Limit, Wire }

    private async Task<(IdleConfigurationResult Result, IdleCapture? Capture)> JudgeConfigurationAsync(LoadedScenario scenario, string item,
        string configuration, (string Pod, string Uid)? pod, CancellationToken cancellationToken)
    {
        if (pod is null)
        {
            var (reasons, invalid) = IdleObservationJudgement.CaptureProblems(null, null, null, options.RunId, item, configuration, "", "", options.SourceCommit, options.RunKind);
            return (IdleObservationJudgement.Configuration(configuration, reasons, invalid, [], null), null);
        }
        string root = IdleObservationPaths.Capture(options.RunId, item, configuration);
        var problems = new List<ReadProblem>();
        string? hash = null;
        async Task<T?> Read<T>(string path, long maximum, int seconds, bool keepHash) where T : class
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), clock);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
                byte[]? bytes = await ledger.ReadAsync(root + path, maximum, linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
                if (bytes is null) { problems.Add(ReadProblem.Missing); return null; }
                T value = SingleActionWire.Read<T>(bytes, maximum);
                if (keepHash) hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                return value;
            }
            catch (RequestFailedException) { problems.Add(ReadProblem.Missing); return null; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { problems.Add(ReadProblem.Missing); return null; }
            catch (Exception exception) when (exception is InvalidDataException or InputLimitExceededException) { problems.Add(ReadProblem.Limit); return null; }
            catch (Exception exception) when (exception is FormatException or System.Text.Json.JsonException) { problems.Add(ReadProblem.Wire); return null; }
        }
        var registration = await Read<SingleActionRegistration>($"registrations/{pod.Value.Pod}.json", options.Limits.MaxRegistrationBytes, options.Limits.IoSeconds, false).ConfigureAwait(false);
        var completion = await Read<SingleActionCompletion>($"instances/{pod.Value.Pod}/completion.json", options.Limits.MaxCompletionBytes, options.Limits.IoSeconds, false).ConfigureAwait(false);
        var capture = await Read<IdleCapture>($"instances/{pod.Value.Pod}/capture.json", options.Limits.MaxCaptureBytes, options.Limits.CaptureIoSeconds, true).ConfigureAwait(false);
        string[] captureReasons;
        bool captureInvalid;
        if (problems.Contains(ReadProblem.Wire)) (captureReasons, captureInvalid) = (["idle_wire"], true);
        else if (problems.Contains(ReadProblem.Limit)) (captureReasons, captureInvalid) = (["idle_capture_limit"], false);
        else (captureReasons, captureInvalid) = IdleObservationJudgement.CaptureProblems(registration, capture, completion, options.RunId, item, configuration,
            pod.Value.Pod, pod.Value.Uid, options.SourceCommit, options.RunKind);
        if (captureReasons.Length > 0)
            return (IdleObservationJudgement.Configuration(configuration, captureReasons, captureInvalid, [], hash), capture);
        var repeats = new List<IdleRepeatResult>();
        for (int index = 0; index < capture!.Repeats.Length; index++)
        {
            IdleRepeat repeat = capture.Repeats[index];
            int ordinal = index + 1;
            // An identity mismatch invalidates the repeat in JudgeRepeat, so resolving for the expected client id gives the same outcome without trusting captured text.
            var resolved = resolver.Resolve(scenario, $"{pod.Value.Pod}-r{ordinal}", configuration, scenario.Definition.IdleObservation!.Role);
            var settings = resolved.Settings.ToDictionary(x => x.Key, x => x.Value.Value, StringComparer.Ordinal);
            string[] omissions = [.. resolved.Omissions.Select(x => x.Key).Concat(resolved.ScenarioOmissions.IsDefault ? [] : resolved.ScenarioOmissions.Select(x => x.Key))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
            repeats.Add(IdleObservationJudgement.JudgeRepeat(repeat, ordinal, pod.Value.Pod, settings, omissions, 420,
                options.Limits.MaxStatisticsReportBytes, options.Limits.MaxBrokerEntries));
        }
        return (IdleObservationJudgement.Configuration(configuration, [], false, [.. repeats], hash), capture);
    }
}
