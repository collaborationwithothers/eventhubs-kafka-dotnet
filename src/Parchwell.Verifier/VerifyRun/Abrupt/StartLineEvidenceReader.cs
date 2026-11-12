using System.Security.Cryptography;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Abrupt;

internal sealed record StartLineEvidence(ParseResult<StartLineNote> Start,
    ParseResult<BrokerInventory> Inventory, byte[]? NoteBytes, byte[]? InventoryBytes,
    WorkerReceipt? Receipt, string StartName, string InventoryName, RunIdentity ExpectedIdentity);

internal static class StartLineEvidenceReader
{
    internal static async Task<StartLineEvidence> ReadAsync(VerifierHostOptions options,
        InputLimits inputLimits, InputBudget budget,
        Func<string, CancellationToken, Task<byte[]?>> readVerifier, CancellationToken cancellationToken)
    {
        byte[]? startReceiptBytes=await readVerifier(PhasePaths.StartLineCompletion(options.RunId!,options.Phase!,options.Repeat!.Value),cancellationToken).ConfigureAwait(false);
        WorkerReceipt? startReceipt=null;
        StructuralProblem? startReceiptProblem=null;
        if(startReceiptBytes is not null)
        {
            budget.ConsumeBytes(startReceiptBytes.Length);
            try { startReceipt=PhaseWireCodec.ReadReceipt(startReceiptBytes); }
            catch(PhaseWireIdentityException) { startReceiptProblem=new StructuralProblem(7,"start_line_receipt_binding_invalid","start-line",null,"identity",null); }
            catch(FormatException) { /* retain the missing-evidence path below */ }
            catch(System.Text.Json.JsonException) { /* malformed completion remains missing evidence */ }
        }
        string startJob=PhaseIdentity.StartLineJobName(options.RunId!,options.Phase!,options.Repeat!.Value);
        if(startReceipt is not null&&(startReceipt.Mode!="start-line"||startReceipt.JobName!=startJob||startReceipt.RunId!=options.RunId||startReceipt.Phase!=options.Phase||startReceipt.Repeat!=options.Repeat||startReceipt.SourceCommit!=options.SourceCommit))
        {
            string field=startReceipt.SourceCommit!=options.SourceCommit?"source_commit":"identity";
            startReceiptProblem=new StructuralProblem(7,"start_line_receipt_binding_invalid","start-line",null,field,null);
            startReceipt=null;
        }
        string startName=PhasePaths.StartLine(options.RunId!,options.Phase!,options.Repeat!.Value);
        string inventoryName=PhasePaths.BrokerInventory(options.RunId!,options.Phase!,options.Repeat!.Value);
        if(startReceipt is { Artifacts.Count: 2 })
        {
            try
            {
                ValidateReference(startReceipt.Artifacts[0],"verifier",startName);
                ValidateReference(startReceipt.Artifacts[1],"verifier",inventoryName);
            }
            catch(InvalidOperationException)
            {
                startReceiptProblem=new StructuralProblem(7,"artifact_reference_invalid","start-line",null,"reference",null);
                startReceipt=null;
            }
        }
        else if(startReceipt is not null)
        {
            startReceiptProblem=new StructuralProblem(7,"artifact_reference_invalid","start-line",null,"reference",null);
            startReceipt=null;
        }
        byte[]? noteBytes=await readVerifier(startName,cancellationToken).ConfigureAwait(false);
        byte[]? inventoryBytes=await readVerifier(inventoryName,cancellationToken).ConfigureAwait(false);
        if(noteBytes is not null)budget.ConsumeBytes(noteBytes.Length);if(inventoryBytes is not null)budget.ConsumeBytes(inventoryBytes.Length);
        StructuralProblem? noteReferenceProblem=null;
        StructuralProblem? inventoryReferenceProblem=null;
        if(noteBytes is not null&&startReceipt is not null)
        {
            try { VerifyHashAndLength(startReceipt.Artifacts[0],noteBytes); }
            catch(InvalidOperationException) { noteReferenceProblem=new StructuralProblem(7,"artifact_reference_hash_mismatch","start-line",null,"reference",null); }
        }
        if(inventoryBytes is not null&&startReceipt is not null)
        {
            try { VerifyHashAndLength(startReceipt.Artifacts[1],inventoryBytes); }
            catch(InvalidOperationException) { inventoryReferenceProblem=new StructuralProblem(6,"artifact_reference_hash_mismatch","broker-inventory",null,"reference",null); }
        }
        RunIdentity expectedStartIdentity=ExpectedIdentity(options);
        ParseResult<StartLineNote> start=noteBytes is null
            ?ParseResult<StartLineNote>.Failure(new StructuralProblem(7,"missing_input",startName,null,null,null))
            :StartLineNoteCodec.Read(noteBytes,startName,inputLimits,options.MaxPartitions!.Value);
        if(startReceiptProblem is not null||noteReferenceProblem is not null)
            start=ParseResult<StartLineNote>.Failure(startReceiptProblem??noteReferenceProblem!);
        ParseResult<BrokerInventory> inventory=inventoryBytes is null
            ?ParseResult<BrokerInventory>.Failure(new StructuralProblem(6,"missing_input",inventoryName,null,null,null))
            :BrokerInventoryReader.Read(inventoryBytes,inventoryName,expectedStartIdentity,options.SourceCommit!,inputLimits);
        if(inventoryReferenceProblem is not null)
            inventory=ParseResult<BrokerInventory>.Failure(inventoryReferenceProblem);
        if(inventory.IsExecutionFailure)
            throw new InvalidOperationException("Broker inventory parsing ended with an execution failure.");

        return new(start, inventory, noteBytes, inventoryBytes, startReceipt, startName, inventoryName, expectedStartIdentity);
    }

    internal static RunIdentity ExpectedIdentity(VerifierHostOptions o)
    {
        string loadgen = PhaseIdentity.JobName(o.RunId!, o.Phase!, o.Repeat!.Value);
        return new(o.ScenarioId!, o.ConfigurationId!, o.RunId!, loadgen, loadgen, loadgen);
    }
    private static void ValidateReference(ArtifactReference r, string container, string name)
    {
        if (r.Container != container || r.Name != name || r.Length < 0)
            throw new InvalidOperationException("artifact_reference_invalid");
    }
    private static void VerifyHashAndLength(ArtifactReference reference, byte[] bytes)
    {
        if (reference.Length != bytes.LongLength || reference.Sha256 != Convert.ToHexStringLower(SHA256.HashData(bytes)))
            throw new InvalidOperationException("artifact_reference_hash_mismatch");
    }
}
