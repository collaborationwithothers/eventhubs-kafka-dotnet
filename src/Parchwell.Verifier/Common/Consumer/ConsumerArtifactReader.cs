using Azure;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.Common.Consumer;

internal sealed class ConsumerArtifactReader(IProducerArtifactStore store, InputLimits limits, TimeSpan timeout, TimeProvider clock)
{
    internal async Task<ConsumerArtifacts> ReadAsync(string repeatPrefix, InputBudget budget, CancellationToken token, bool readCompletions = true)
    {
        string root=repeatPrefix+"consumer/";var members=new List<ConsumerMember>();var diagnostics=new List<ConsumerDiagnostic>();int count=0;string artifact=root;
        try
        {
            BlobListing listing=await Bounded(t=>store.ListAsync(root,t),token).ConfigureAwait(false);
            if(!listing.IsComplete)throw new InputLimitExceededException("consumer_listing_incomplete");
            string[] markers=listing.Names.Where(n=>n.StartsWith(root,StringComparison.Ordinal)&&n.EndsWith("/registration.json",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();count=markers.Length;
            foreach(string marker in markers)
            {
                artifact=marker;byte[]? registrationBytes=await Read(marker).ConfigureAwait(false);ConsumerRegistration? registration=null;bool registrationInvalid=false,recordInvalid=false;
                string prefix=marker[..^"registration.json".Length];
                try { registration=ConsumerArtifactCodec.ReadRegistration(registrationBytes??[]);string escaped=prefix[root.Length..^1];if(escaped.Contains('/')||registration.PodName!=Uri.UnescapeDataString(escaped)||registration.InstanceId!=registration.PodName)registrationInvalid=true; }
                catch(Exception e) when(e is FormatException or KeyNotFoundException or InvalidOperationException){registrationInvalid=true;}
                var rows=new List<ConsumerRow>();string[] blobs=listing.Names.Where(n=>n.StartsWith(prefix+"record/",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
                for(int index=0;index<blobs.Length;index++)
                {
                    artifact=blobs[index];if(artifact!=$"{prefix}record/{index:D6}.jsonl")recordInvalid=true;
                    byte[]? bytes=await Read(artifact).ConfigureAwait(false);if(bytes is null){recordInvalid=true;continue;}
                    if(bytes.Length==0||bytes[^1]!=(byte)'\n'){recordInvalid=true;continue;}
                    int start=0,lineCount=0;
                    for(int end=0;end<bytes.Length;end++)if(bytes[end]==(byte)'\n')
                    {
                        lineCount++;
                        try {ConsumerRow row=ConsumerArtifactCodec.ReadRow(bytes.AsMemory(start,end-start));if(row.RowSequence!=rows.Count||row.InstanceId!=registration?.InstanceId)recordInvalid=true;rows.Add(row);}
                        catch(Exception e) when(e is FormatException or KeyNotFoundException or InvalidOperationException){recordInvalid=true;}
                        start=end+1;
                    }
                    if(lineCount>40000||(index<blobs.Length-1&&lineCount!=40000))recordInvalid=true;
                }
                if(blobs.Length==0)recordInvalid=true;
                ConsumerCompletion? completion=null;
                if(readCompletions)
                {
                    artifact=prefix+"completion.json";byte[]? bytes=await Read(artifact).ConfigureAwait(false);
                    if(bytes is not null)try {completion=ConsumerArtifactCodec.ReadCompletion(bytes);if(registration is null||!Matches(completion,registration)||completion.Rows!=rows.Count||completion.RecordBlobs!=blobs.Length||completion.MessagesProcessed!=rows.Count(r=>r.Kind=="processed"))recordInvalid=true;}
                    catch(Exception e) when(e is FormatException or KeyNotFoundException or InvalidOperationException){recordInvalid=true;}
                }
                if(registrationInvalid)diagnostics.Add(new("consumer_registration_invalid",registration?.InstanceId,marker));
                if(recordInvalid)diagnostics.Add(new("consumer_record_invalid",registration?.InstanceId,prefix));
                members.Add(new(prefix,registration,rows,completion,blobs.Length,registrationInvalid,recordInvalid));
            }
            return new(members,count,false,diagnostics);
        }
        catch(Exception e) when(!token.IsCancellationRequested && e is RequestFailedException or OperationCanceledException or InputLimitExceededException or IOException)
        { diagnostics.Add(new("consumer_record_unavailable",Artifact:artifact,Detail:e.GetType().Name));return new(members,count,true,diagnostics); }
        async Task<byte[]?> Read(string name){byte[]? bytes=await Bounded(t=>store.ReadBoundedAsync(name,limits.MaxArtifactBytes,t),token).ConfigureAwait(false);if(bytes is not null){if(bytes.LongLength>limits.MaxArtifactBytes)throw new InputLimitExceededException("consumer_artifact_bytes");budget.ConsumeBytes(bytes.LongLength);}return bytes;}
    }
    private async Task<T> Bounded<T>(Func<CancellationToken,Task<T>> action,CancellationToken token)
    {
        using var deadline=new CancellationTokenSource(timeout,clock);using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,deadline.Token);long origin=clock.GetTimestamp();T value=await action(linked.Token).WaitAsync(timeout,clock,token).ConfigureAwait(false);if(clock.GetElapsedTime(origin)>timeout)throw new OperationCanceledException("consumer_blob_timeout");linked.Token.ThrowIfCancellationRequested();return value;
    }
    private static bool Matches(ConsumerCompletion c,ConsumerRegistration r)=>c.RunId==r.RunId&&c.ScenarioId==r.ScenarioId&&c.ConfigurationId==r.ConfigurationId&&c.Phase==r.Phase&&c.RepeatNumber==r.RepeatNumber&&c.Role==r.Role&&c.InstanceId==r.InstanceId&&c.JobName==r.JobName&&c.PodUid==r.PodUid&&c.SourceCommit==r.SourceCommit;
}
