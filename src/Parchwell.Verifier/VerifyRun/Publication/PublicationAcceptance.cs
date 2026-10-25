using System.Security.Cryptography;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Common.Window;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Publication;

internal sealed record PublicationReadOutcome(PublicationAcceptanceSnapshot? AcceptedSnapshot,
    IReadOnlyDictionary<string,byte[]?> Payloads, IReadOnlyList<string> RejectionCodes, bool Incomplete)
{
    internal bool Accepted => AcceptedSnapshot is not null;
}

/// <summary>Independently verifies the complete fixed publication before it can authorize J2 evidence.</summary>
internal static class PublicationAcceptance
{
    internal static readonly string[] PublicationNames=["index.json","ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"];
    internal static readonly string[] DescriptorNames=["ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"];
    internal const long MaximumObjectBytes=268_435_456;
    internal const long MaximumAggregateBytes=1_073_741_824;

    internal static PublicationReadOutcome Evaluate(BlobListing listing,string producerPrefix,
        IReadOnlyDictionary<string,byte[]?> payloads,ProducerRegistration registration,
        ProducerCompletionReceipt? producerReceipt,ArtifactIndex? index,SealedRunManifest? manifest,
        StartLineNote? startLine,BrokerInventory? inventory,string sourceCommit,
        ArtifactReference? startReference,ArtifactReference? inventoryReference,ArtifactReference? producerReference,
        long configuredObjectLimit,long configuredAggregateLimit)
    {
        var rejected=new List<string>();
        bool incomplete=!listing.IsComplete;
        if(!listing.IsComplete)rejected.Add(listing.FailureCode??"listing_incomplete");
        string expectedPublication=producerPrefix+"publication/";
        string[] members=listing.Names.Where(n=>n.StartsWith(expectedPublication,StringComparison.Ordinal)).Select(n=>n[expectedPublication.Length..]).Order(StringComparer.Ordinal).ToArray();
        string[] expected=PublicationNames.Order(StringComparer.Ordinal).ToArray();
        if(!members.SequenceEqual(expected,StringComparer.Ordinal))
        {
            if(members.Any(n=>n.Contains('/')||!expected.Contains(n,StringComparer.Ordinal)))rejected.Add("publication_membership_invalid");
            else {rejected.Add("publication_incomplete");incomplete=true;}
        }
        long total=0;
        var objectRefs=new List<ArtifactReference>(6);
        foreach(string name in PublicationNames)
        {
            if(!payloads.TryGetValue(name,out byte[]? bytes)||bytes is null){rejected.Add("publication_object_missing");incomplete=true;continue;}
            total=checked(total+bytes.LongLength);
            if(bytes.LongLength>Math.Min(MaximumObjectBytes,configuredObjectLimit))rejected.Add("publication_object_oversized");
            string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
            objectRefs.Add(new("ledger",expectedPublication+name,bytes.LongLength,hash));
        }
        if(total>Math.Min(MaximumAggregateBytes,configuredAggregateLimit))rejected.Add("publication_aggregate_oversized");
        if(index is null) {rejected.Add("publication_index_unavailable");incomplete=true;}
        else
        {
            if(index.Identity!=registration.Identity)rejected.Add("publication_identity_invalid");
            if(index.WorkerStatus!="complete"||!index.BookkeepingComplete||!index.WorkerComplete||!index.PublicationComplete)rejected.Add("publication_completion_invalid");
            if(index.Artifacts.Count!=5||!index.Artifacts.Select(a=>a.Name).SequenceEqual(DescriptorNames,StringComparer.Ordinal))rejected.Add("publication_descriptors_invalid");
            else foreach(var descriptor in index.Artifacts)
            {
                if(!payloads.TryGetValue(descriptor.Name,out byte[]? bytes)||bytes is null){rejected.Add("publication_object_missing");incomplete=true;continue;}
                string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
                if(descriptor.Length!=bytes.LongLength||!StringComparer.Ordinal.Equals(descriptor.Sha256,hash))rejected.Add("publication_reference_invalid");
            }
        }
        if(manifest is null){rejected.Add("publication_manifest_unavailable");incomplete=true;}
        else if(manifest.Identity!=registration.Identity)rejected.Add("publication_manifest_identity_invalid");
        if(producerReceipt is null){rejected.Add("producer_completion_unavailable");incomplete=true;}
        else if(!Matches(producerReceipt,registration))rejected.Add("producer_completion_invalid");
        RunIdentity expectedStart=new(registration.Identity.ScenarioId,registration.Identity.ConfigurationId,registration.Identity.RunId,
            registration.JobName,registration.JobName,registration.JobName);
        if(startLine is null){rejected.Add("start_line_unavailable");incomplete=true;}
        else if(startLine.Identity!=expectedStart||startLine.VerifierCommit!=sourceCommit)rejected.Add("start_line_invalid");
        if(inventory is null){rejected.Add("broker_inventory_unavailable");incomplete=true;}
        else if(inventory.Identity!=expectedStart||inventory.SourceCommit!=sourceCommit)rejected.Add("broker_inventory_invalid");
        if(registration.SourceCommit!=sourceCommit||startReference is not null&&registration.Identity.RunId!=startReference.Name.Split('/')[0])
            rejected.Add("publication_source_invalid");

        if(rejected.Count!=0)
        {
            string[] codes=rejected.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            // Missing evidence can make the publication incomplete, but it must not
            // downgrade an independently observed integrity violation.
            bool incompleteOnly=incomplete&&codes.All(IsIncompleteCode);
            return new(null,payloads,codes,incompleteOnly);
        }
        // These references are required by the snapshot, but their absence must not
        // prevent the independent integrity checks above from running.
        if(startReference is null||inventoryReference is null||producerReference is null)
            return new(null,payloads,["publication_reference_unavailable"],true);
        var snapshot=new PublicationAcceptanceSnapshot(producerPrefix,registration.Identity,sourceCommit,startReference,inventoryReference,producerReference,objectRefs);
        return new(snapshot,payloads,[],false);
    }

    private static bool Matches(ProducerCompletionReceipt receipt,ProducerRegistration registration)=>
        receipt.SchemaVersion==registration.SchemaVersion&&receipt.Identity==registration.Identity&&receipt.Phase==registration.Phase&&
        receipt.RepeatNumber==registration.RepeatNumber&&receipt.JobName==registration.JobName&&receipt.PodName==registration.PodName&&
        receipt.PodUid==registration.PodUid&&receipt.SourceCommit==registration.SourceCommit&&receipt.Mode=="loadgen"&&
        receipt.HandlerExitCode==0&&receipt.CleanupCompleted;

    private static bool IsIncompleteCode(string code)=>code is "listing_incomplete" or "publication_incomplete" or
        "publication_object_missing" or "publication_index_unavailable" or "publication_manifest_unavailable" or
        "producer_completion_unavailable" or "start_line_unavailable" or "broker_inventory_unavailable" or
        "publication_reference_unavailable";
}
