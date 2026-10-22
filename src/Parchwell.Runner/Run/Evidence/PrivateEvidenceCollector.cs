using System.Text.Json;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Run.Evidence;

/// <summary>Retains corroborating source and exact CLI text only in the private phase export.</summary>
public sealed class PrivateEvidenceCollector(KqlCollector collector, string parametersFile, string sourceFile)
{
    public async Task CollectAsync(string outputDirectory, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(outputDirectory, "kql");
        Directory.CreateDirectory(directory);
        if (new FileInfo(sourceFile).Length > 65_536) throw new InvalidDataException("private_kql_input_limit");
        byte[] source = await File.ReadAllBytesAsync(sourceFile, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "source.kql"), source, cancellationToken).ConfigureAwait(false);
        if (new FileInfo(parametersFile).Length > 1_048_576) throw new InvalidDataException("private_kql_input_limit");
        byte[] parameters = await File.ReadAllBytesAsync(parametersFile, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "parameters.private.json"), parameters, cancellationToken).ConfigureAwait(false);
        KqlReceipt receipt = await collector.CollectAsync(KqlCollector.ReadPrivateParameters(parameters), System.Text.Encoding.UTF8.GetString(source), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(directory, "query.kql"), receipt.Query, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "cli-output.json"), receipt.OriginalOutput, cancellationToken).ConfigureAwait(false);
        if (receipt.OriginalError is not null)
            await File.WriteAllBytesAsync(Path.Combine(directory, "cli-error.txt"), receipt.OriginalError, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(directory, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new
        { status = receipt.Status, reason = receipt.Reason, rows = receipt.Rows, corroborating_only = true }, LedgerJson.Options), cancellationToken).ConfigureAwait(false);
    }
}
