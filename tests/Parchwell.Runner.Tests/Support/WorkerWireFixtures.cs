using System.Globalization;
using System.Text;
using Parchwell.Verifier;
using VerifierIdentity = Parchwell.Verifier.Common.Contracts.RunIdentity;

namespace Parchwell.Runner.Tests;

/// <summary>Produces synthetic receipt controls using the actual production worker writers.</summary>
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
            var state = new VerifierCompletionState();
            state.MarkReturned(new VerifierHostOptions
            {
                Mode = mode, ScenarioId = "A01", ConfigurationId = identity?.ConfigurationId,
                RunId = identity?.RunId, InstanceId = identity?.InstanceId, ExpectedInstanceId = identity?.ExpectedInstanceId,
                ClientId = identity?.ClientId, VerifierCommit = sourceCommit
            }, handlerExitCode ?? (mode == "verify" ? 1 : 0), CancellationToken.None, CancellationToken.None);
            await state.WriteReceiptAsync(output);
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }
}
