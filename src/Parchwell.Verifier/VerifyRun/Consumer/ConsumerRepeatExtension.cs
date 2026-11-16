using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using Azure;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Consumer.Judgement;

namespace Parchwell.Verifier.VerifyRun.Consumer;

internal sealed class ConsumerRepeatExtension(VerifierHostOptions options, ConsumerVerificationPlan plan,
    ConsumerArtifactReader reader, IVerifierArtifactStore verifierStore, ConsumerOffsetReader offsets,
    TimeProvider clock, string bootstrapServers) : IRepeatResultExtension
{
    public int CompletionDeadlineMs => plan.CompletionDeadlineMs;

    public async Task<RepeatResultExtension> EvaluateAsync(RepeatResultContext context, CancellationToken cancellationToken)
    {
        string prefix=PhaseIdentity.RepeatPrefix(options.RunId!,options.Phase!,options.Repeat!.Value);
        ConsumerArtifacts artifacts=await reader.ReadAsync(prefix,context.InputBudget,cancellationToken).ConfigureAwait(false);
        string snapshotName=prefix+"consumer-start.json";
        ConsumerStartSnapshot? snapshot=null;
        if(!artifacts.Unavailable)
        {
            try
            {
                using var timeout=new CancellationTokenSource(options.BlobIoTimeout!.Value,clock);
                using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeout.Token);
                long started=clock.GetTimestamp();
                byte[]? bytes=await verifierStore.ReadBoundedAsync(snapshotName,options.MaxArtifactBytes!.Value,linked.Token)
                    .WaitAsync(options.BlobIoTimeout.Value,clock,cancellationToken).ConfigureAwait(false);
                if(clock.GetElapsedTime(started)>options.BlobIoTimeout.Value)throw new TimeoutException("consumer_blob_timeout");
                linked.Token.ThrowIfCancellationRequested();
                if(bytes is not null)
                {
                    if(bytes.LongLength>options.MaxArtifactBytes.Value)throw new InputLimitExceededException("consumer_artifact_bytes");
                    context.InputBudget.ConsumeBytes(bytes.LongLength);
                    try {snapshot=ConsumerArtifactCodec.ReadSnapshot(bytes);}
                    catch(Exception exception) when(exception is FormatException or KeyNotFoundException or InvalidOperationException)
                    { artifacts=artifacts with {Diagnostics=artifacts.Diagnostics.Append(new ConsumerDiagnostic("consumer_start_assignment_invalid",Artifact:snapshotName,Detail:"snapshot malformed")).ToArray()}; }
                }
            }
            catch(Exception exception) when(!cancellationToken.IsCancellationRequested&&exception is RequestFailedException or OperationCanceledException or TimeoutException or InputLimitExceededException or IOException)
            {
                artifacts=artifacts with {Unavailable=true,Diagnostics=artifacts.Diagnostics.Append(new ConsumerDiagnostic("consumer_record_unavailable",Artifact:snapshotName,Detail:exception.GetType().Name)).ToArray()};
            }
        }
        var request=new ConsumerJudgementRequest(context.RootStatus,options.RunId!,options.ScenarioId!,options.ConfigurationId!,options.Phase!,options.Repeat.Value,
            ConsumerPlanValidation.GroupId(options),plan,bootstrapServers,artifacts,snapshot,context.Window,context.LedgerRows,context.ConsumedRecords,context.DeadlineInstant);
        int[] partitions=context.Window.Partitions.Select(p=>p.Partition).Order().ToArray();
        var read=new ConsumerOffsetRead("not_attempted",partitions,null,null,null,[]);
        ConsumerJudgementResult preflight=ConsumerJudgement.Evaluate(request,read,false);
        if(preflight.ShouldRead&&clock.GetUtcNow()<context.DeadlineInstant)
            read=await offsets(partitions,context.DeadlineInstant,cancellationToken).ConfigureAwait(false);
        ConsumerJudgementResult result=ConsumerJudgement.Evaluate(request,read);
        return new(result.Status,result.Consumer);
    }
}
