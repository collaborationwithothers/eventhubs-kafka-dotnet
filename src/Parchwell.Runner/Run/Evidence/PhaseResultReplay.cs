using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Execution;

namespace Parchwell.Runner.Run.Evidence;

/// <summary>Rehydrates accepted originals without depending on an earlier dispatch's workspace.</summary>
internal static class PhaseResultReplay
{
    internal static bool HasExpectedOrdinals(PhaseRecord phase) => phase.Repeats.Length == 5 &&
        phase.Repeats.Select(repeat => repeat.Ordinal).Order().SequenceEqual(Enumerable.Range(1, 5));

    internal static async Task<byte[]> ReadAsync(EvidenceExporter exporter, LedgerDocument document, string phaseName,
        RepeatRecord repeat, string? outputDirectory, CancellationToken token)
    {
        PhaseRecord phase = document.Phases[phaseName];
        WorkerIdentity identity = ExpectedIdentity(phase, phaseName, repeat);
        // Once an export exists its receipt is authoritative; a bad or partial export never falls back to local bytes.
        byte[] bytes = phase.PrivateExport is { } receipt
            ? await exporter.ReadVerifiedFileAsync(phase.EnvironmentRunId!, repeat.RunId + "/result.json", receipt,
                PrivateProvenanceCollector.MaxReceiptBytes, token).ConfigureAwait(false)
            : outputDirectory is not null
                ? await PrivateProvenanceCollector.ReadBoundedAsync(Path.Combine(outputDirectory, repeat.RunId!, "result.json"),
                    PrivateProvenanceCollector.MaxReceiptBytes, token).ConfigureAwait(false)
                : throw new InvalidDataException("replay_result_unavailable");
        if (bytes.Length > PrivateProvenanceCollector.MaxReceiptBytes || Convert.ToHexStringLower(SHA256.HashData(bytes)) != repeat.ResultHash)
            throw new InvalidDataException("replay_result_hash");
        using JsonDocument parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        JsonElement result = parsed.RootElement;
        if (result.GetProperty("schema_version").GetInt32() != 1 || WorkerIdentity.Read(result.GetProperty("identity")) != identity ||
            WorkerIdentity.Text(result, "verifier_commit") != document.SourceCommit || WorkerIdentity.Text(result, "status") != repeat.Outcome)
            throw new InvalidDataException("replay_result_identity");
        return bytes;
    }

    internal static async Task<IReadOnlyList<RepeatExecution>> RestoreGateInputsAsync(EvidenceExporter exporter, TimeProvider clock,
        LedgerDocument document, string sourceCommit, string outputDirectory, CancellationToken token)
    {
        PhaseRecord phase = document.Phases["probe"];
        if (document.SourceCommit != sourceCommit || phase.State != "destroyed" || phase.PrivateExport?.Complete != true ||
            phase.DestroyReceipt is not { Complete: true } destroy || destroy.EnvironmentRunId != phase.EnvironmentRunId || !HasExpectedOrdinals(phase))
            throw new InvalidDataException("gate_probe_not_completed");
        using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(300), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, timer.Token);
        var captured = new List<RepeatExecution>();
        foreach (RepeatRecord repeat in phase.Repeats.OrderBy(repeat => repeat.Ordinal))
        {
            byte[] bytes = await ReadAsync(exporter, document, "probe", repeat, null, deadline.Token).ConfigureAwait(false);
            WorkerIdentity identity = ExpectedIdentity(phase, "probe", repeat);
            captured.Add(new(identity, new(true, false, "diagnostic", null, "diagnostic"), new(new Dictionary<string, byte[]>()), bytes, []));
        }
        // The existing verifier process consumes files. Write only after every immutable original has been verified.
        foreach (RepeatExecution repeat in captured)
        {
            string directory = Path.Combine(outputDirectory, repeat.Identity.RunId);
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, "result.json"), repeat.Result, deadline.Token).ConfigureAwait(false);
        }
        return captured;
    }

    private static WorkerIdentity ExpectedIdentity(PhaseRecord phase, string phaseName, RepeatRecord repeat)
    {
        if (phase.EnvironmentRunId is null || repeat.State != "completed" || repeat.RunId is null || repeat.AdmittedIdentity is null ||
            repeat.Ordinal is < 1 or > 5 || repeat.RunId != phase.EnvironmentRunId + "-r" + repeat.Ordinal.ToString(CultureInfo.InvariantCulture) ||
            repeat.Outcome is not ("complete_valid" or "invalid" or "incomplete") || repeat.Reason != repeat.Outcome)
            throw new InvalidDataException("replay_repeat_unavailable");
        string admitted = repeat.AdmittedIdentity;
        return new("A01", phaseName == "probe" ? "C0" : phaseName, repeat.RunId, admitted, admitted, admitted);
    }
}
