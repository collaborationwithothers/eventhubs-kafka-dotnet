using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.ProbeGate;

/// <summary>Loads local repeat evidence and publishes only after bounded input cleanup.</summary>
internal sealed class ProbeGateHandler(IOptions<VerifierHostOptions> configured, TimeProvider clock,
    ScenarioLoader scenarioLoader, IProbeGateFileStore files) : IVerifierModeHandler
{
    public VerifierMode Mode => VerifierMode.ProbeGate;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        VerifierHostOptions options = configured.Value;
        var expected = options.ExpectedRepeats!.Select(repeat => new RunIdentity(repeat.ScenarioId!,
            repeat.ConfigurationId!, repeat.RunId!, repeat.InstanceId!, repeat.ExpectedInstanceId!, repeat.ClientId!)).ToArray();
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, int.MaxValue, options.MaxTotalInputBytes.Value,
            options.MaxTotalInputBytes.Value, options.MaxDiagnosticBytes!.Value);
        var budget = new InputBudget(limits);
        var inputs = new List<ProbeGateInput>();
        Parchwell.Scenarios.ProbeGate gate;
        long origin = clock.GetTimestamp();
        using (var deadline = new CancellationTokenSource(options.IoTimeout!.Value, clock))
        using (var work = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token))
        {
            LoadedScenario scenario = await scenarioLoader.LoadAsync(options.ScenarioId!, work.Token).AsTask().WaitAsync(work.Token).ConfigureAwait(false);
            CheckDeadline(origin, options.IoTimeout.Value, work.Token);
            if (!ProbeGateEvaluator.IsSupportedGate(options.ScenarioId!, scenario.Definition.Gate))
                throw new InvalidOperationException("The selected scenario has no supported probe gate.");
            gate = (Parchwell.Scenarios.ProbeGate)scenario.Definition.Gate;
            if (ProbeGateEvaluator.GetRootReasons(expected, options.RepeatResultPaths!.Count).Count == 0)
            {
                foreach (string path in options.RepeatResultPaths)
                {
                    inputs.Add(await ReadInputAsync(path, limits, budget, options, origin, work.Token).ConfigureAwait(false));
                    CheckDeadline(origin, options.IoTimeout.Value, work.Token);
                }
            }
        }
        IReadOnlyList<string> roots = ProbeGateEvaluator.GetRootReasons(expected, options.RepeatResultPaths!.Count);
        ProbeGateResult result = roots.Count > 0 ? new("inconclusive", roots, [], options.VerifierCommit!) :
            ProbeGateEvaluator.Evaluate(new(gate, expected, inputs), options.VerifierCommit!);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = ProbeGateResultCodec.Write(result, options.MaxOutputBytes!.Value);
        long publicationOrigin = clock.GetTimestamp();
        using var publicationDeadline = new CancellationTokenSource(options.IoTimeout.Value, clock);
        using var publication = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, publicationDeadline.Token);
        Stream? output = null;
        try
        {
            output = await files.CreateNewAsync(options.GateOutputPath!, publication.Token).AsTask().WaitAsync(publication.Token).ConfigureAwait(false);
            CheckDeadline(publicationOrigin, options.IoTimeout.Value, publication.Token);
            await output.WriteAsync(bytes, publication.Token).AsTask().WaitAsync(publication.Token).ConfigureAwait(false);
            CheckDeadline(publicationOrigin, options.IoTimeout.Value, publication.Token);
        }
        finally
        {
            if (output is not null)
            {
                await output.DisposeAsync().AsTask().WaitAsync(publication.Token).ConfigureAwait(false);
                CheckDeadline(publicationOrigin, options.IoTimeout.Value, publication.Token);
            }
        }
        return 0;
    }

    private async Task<ProbeGateInput> ReadInputAsync(string path, InputLimits limits, InputBudget budget,
        VerifierHostOptions options, long origin, CancellationToken cancellationToken)
    {
        Stream? input = null;
        try
        {
            try
            {
                input = await files.OpenReadAsync(path, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                CheckDeadline(origin, options.IoTimeout!.Value, cancellationToken);
                using var bytes = new MemoryStream();
                byte[] buffer = new byte[4096];
                while (true)
                {
                    int read = await input.ReadAsync(buffer, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
                    budget.ConsumeBytes(read);
                    if (read > limits.MaxArtifactBytes - bytes.Length)
                        throw new InputLimitExceededException("A gate input exceeds its configured byte limit.");
                    CheckDeadline(origin, options.IoTimeout.Value, cancellationToken);
                    if (read == 0) break;
                    bytes.Write(buffer, 0, read);
                }
                ParseResult<VerifierResult> parsed = VerifierResultCodec.Read(bytes.ToArray(), path, limits,
                    options.MaxDiagnosticsPerCheck!.Value);
                return parsed.IsSuccess ? new(path, ProbeGateInputStatus.Readable, parsed.Value) :
                    new(path, ProbeGateInputStatus.Unreadable, null);
            }
            catch (FileNotFoundException)
            {
                CheckDeadline(origin, options.IoTimeout!.Value, cancellationToken);
                return new(path, ProbeGateInputStatus.Missing, null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                CheckDeadline(origin, options.IoTimeout!.Value, cancellationToken);
                return new(path, ProbeGateInputStatus.Unreadable, null);
            }
        }
        finally
        {
            if (input is not null)
            {
                long cleanupOrigin = clock.GetTimestamp();
                using var cleanupDeadline = new CancellationTokenSource(options.CleanupTimeout!.Value, clock);
                await input.DisposeAsync().AsTask().WaitAsync(cleanupDeadline.Token).ConfigureAwait(false);
                CheckDeadline(cleanupOrigin, options.CleanupTimeout.Value, cleanupDeadline.Token);
            }
        }
    }

    private void CheckDeadline(long origin, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (clock.GetElapsedTime(origin) >= budget) throw new TimeoutException("The gate operation budget expired.");
    }
}
