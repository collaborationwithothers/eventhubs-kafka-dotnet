using System.Globalization;
using System.Text;
using Parchwell.Verifier.Common.Serialization;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests;

/// <summary>Produces actual LoadGen receipts and synthetic legacy verifier stdout receipts for Runner tests.</summary>
internal static class WorkerReceipts
{
    internal static async Task<byte[]> WriteAsync(string mode, VerifierIdentity? identity, string sourceCommit, int? handlerExitCode = null)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        if (mode == "loadgen")
        {
            ArgumentNullException.ThrowIfNull(identity);
            var loadIdentity = new Parchwell.LoadGen.Run.Contracts.RunIdentity(identity.ScenarioId,
                identity.ConfigurationId, identity.RunId, identity.InstanceId, identity.ExpectedInstanceId, identity.ClientId);
            await Parchwell.LoadGen.Program.WriteCompletionAsync(output,
                Parchwell.LoadGen.Program.CreateCompletionReceipt(loadIdentity, sourceCommit));
        }
        else
        {
            if (mode != "probe-gate") ArgumentNullException.ThrowIfNull(identity);
            byte[] bytes = DeploymentJson.Write(65_536, writer =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("schema_version", 1);
                writer.WriteString("mode", mode);
                if (mode != "probe-gate") DeploymentJson.WriteIdentity(writer, identity!);
                else
                {
                    writer.WritePropertyName("identity");
                    writer.WriteStartObject();
                    writer.WriteString("scenario_id", "A01");
                    writer.WriteEndObject();
                }
                writer.WriteString("source_commit", sourceCommit);
                writer.WriteNumber("handler_exit_code", handlerExitCode ?? (mode == "verify" ? 1 : 0));
                writer.WriteBoolean("cleanup_completed", true);
                writer.WriteEndObject();
            });
            await output.WriteLineAsync("PARCHWELL_COMPLETION " + Encoding.UTF8.GetString(bytes));
            await output.FlushAsync();
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }
}
