using Microsoft.Extensions.Options;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.SingleAction;
using Parchwell.Verifier.Common.Storage;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal interface ISingleActionSeedSource
{
    Task<byte[]?> ReadAsync(string path,CancellationToken cancellationToken);
}

internal sealed class SingleActionSeedSource : ISingleActionSeedSource
{
    public async Task<byte[]?> ReadAsync(string path,CancellationToken cancellationToken)
    {
        if(!File.Exists(path))return null;
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.Asynchronous);
        if(stream.Length>65536)throw new FormatException("seed_input_limit");
        var bytes=new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes,cancellationToken).ConfigureAwait(false);
        if(stream.ReadByte()!=-1)throw new FormatException("seed_input_changed");
        return bytes;
    }
}

/// <summary>Reads bounded captured artifacts and delegates every evidence decision to the judgement files.</summary>
internal sealed class SingleActionHandler(IOptions<VerifierHostOptions> configured,IProducerArtifactStore captureStore,IVerifierArtifactStore verifier,ScenarioLoader loader,ISingleActionSeedSource seedSource,SingleActionScenarioResolver resolver,TimeProvider clock,VerifierCompletionState completion) : IVerifierModeHandler
{
    public VerifierMode Mode=>VerifierMode.SingleActionJudge;

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var options=configured.Value;
        string run=options.RunId!,item=options.ScenarioId!,configuration=options.ConfigurationId!,source=options.SourceCommit!;
        string prefix=SingleActionPaths.Prefix(run,item,configuration);
        var budget=new InputBudget(new(32,134217728,268435456,262144,10000,1000000,2048));
        async Task<byte[]?> Read(Func<CancellationToken,Task<byte[]?>> operation,long maximum)
        {
            byte[]? bytes=await Bounded(operation,cancellationToken).ConfigureAwait(false);
            if(bytes is not null){budget.ConsumeBytes(bytes.Length);if(bytes.Length>maximum)throw new FormatException("single_action_artifact_limit");}
            return bytes;
        }
        SingleActionRegistration? registration=null;SingleActionCompletion? completed=null;SingleActionCapture? capture=null;
        bool registrationValid=false,captureValid=false;
        try
        {
            BlobListing listing=await Bounded(ct=>captureStore.ListAsync(prefix+"registrations/",ct),cancellationToken).ConfigureAwait(false);
            if(listing.IsComplete&&listing.Names.Count==1&&IsRegistrationPath(listing.Names[0],prefix))
            {
                byte[]? bytes=await Read(ct=>captureStore.ReadBoundedAsync(listing.Names[0],65536,ct),65536).ConfigureAwait(false);
                if(bytes is not null)registration=SingleActionWire.Read<SingleActionRegistration>(bytes,65536);
                registrationValid=SingleActionConfigurationJudgement.Registration(registration,run,item,configuration,source)&&listing.Names[0]==prefix+$"registrations/{registration!.PodName}.json";
            }
        }
        catch(Exception ex) when(Recoverable(ex,cancellationToken)){}
        if(registrationValid&&registration is not null)
        {
            try
            {
                byte[]? bytes=await Read(ct=>captureStore.ReadBoundedAsync(prefix+$"instances/{registration.PodName}/capture.json",134217728,ct),134217728).ConfigureAwait(false);
                if(bytes is not null)capture=SingleActionWire.Read<SingleActionCapture>(bytes,134217728);
                captureValid=SingleActionConfigurationJudgement.Capture(capture,registration);
            }
            catch(Exception ex) when(Recoverable(ex,cancellationToken)){}
            try
            {
                byte[]? bytes=await Read(ct=>captureStore.ReadBoundedAsync(prefix+$"instances/{registration.PodName}/completion.json",65536,ct),65536).ConfigureAwait(false);
                if(bytes is not null)completed=SingleActionWire.Read<SingleActionCompletion>(bytes,65536);
            }
            catch(Exception ex) when(Recoverable(ex,cancellationToken)){}
        }
        var earlier=new Dictionary<string,SingleActionCandidate>(StringComparer.Ordinal);
        SingleActionCandidate? control=null;SingleActionJudgeReceipt? controlReceipt=null;
        string[] configurations=SingleActionConfigurationJudgement.Configurations(item);
        bool final=configurations.LastOrDefault()==configuration;
        foreach(string previous in configurations.TakeWhile(c=>c!=configuration).Where(c=>final||item=="F04"&&configuration=="C4"&&c=="C0"))
        {
            try
            {
                string name=SingleActionPaths.Candidate(run,item,previous);
                byte[]? bytes=await Read(ct=>verifier.ReadBoundedAsync(name,16777216,ct),16777216).ConfigureAwait(false);
                byte[]? receiptBytes=await Read(ct=>verifier.ReadBoundedAsync(SingleActionPaths.JudgeCompletion(run,item,previous),65536,ct),65536).ConfigureAwait(false);
                var candidate=bytes is null?null:SingleActionCandidateCodec.Read(bytes);
                var receipt=receiptBytes is null?null:SingleActionWire.Read<SingleActionJudgeReceipt>(receiptBytes,65536);
                if(previous=="C0"){control=candidate;controlReceipt=receipt;}
                if(SingleActionConfigurationJudgement.Published(candidate,receipt,run,item,previous,source))earlier[previous]=candidate!;
            }
            catch(Exception ex) when(Recoverable(ex,cancellationToken)){}
        }
        bool seedValid=item!="F04";
        if(item=="F04")
        {
            try
            {
                byte[]? receiptBytes=await Read(ct=>seedSource.ReadAsync(options.SeedReceiptPath!,ct),65536).ConfigureAwait(false);
                byte[]? podBytes=await Read(ct=>seedSource.ReadAsync(options.SeedPodIdentityPath!,ct),65536).ConfigureAwait(false);
                seedValid=SingleActionConfigurationJudgement.Seed(receiptBytes is null?null:SingleActionWire.Read<SingleActionSeedReceipt>(receiptBytes,65536),podBytes is null?null:SingleActionWire.Read<SingleActionSeedPod>(podBytes,65536),run,source);
            }
            catch(Exception ex) when(Recoverable(ex,cancellationToken)){}
            if(configuration=="C4")seedValid&=SingleActionConfigurationJudgement.Control(control,controlReceipt,run,source);
        }
        var projections=new List<SingleActionRepeatProjection>();
        if(captureValid)
        {
            LoadedScenario scenario=await loader.LoadAsync(item,cancellationToken).ConfigureAwait(false);
            var declaration=scenario.Definition.SingleAction!.Configurations[configuration];
            foreach(var repeat in capture!.Repeats)
            {
                var role=resolver.Resolve(scenario,repeat.ClientId,configuration,declaration.Role);
                var expected=role.Settings.ToDictionary(p=>p.Key,p=>p.Value.Value,StringComparer.Ordinal);
                var omissions=role.Omissions.Select(o=>o.Key).Concat(role.ScenarioOmissions.IsDefault?Array.Empty<string>():role.ScenarioOmissions.Select(o=>o.Key));
                projections.Add(SingleActionRepeatJudgement.Judge(repeat,declaration,expected,omissions,run));
            }
        }
        bool security=SingleActionConfigurationJudgement.SecurityFinding(item,configuration,registrationValid,captureValid,projections.ToArray());
        var repeats=SingleActionConfigurationJudgement.ApplyGlobal(registrationValid,captureValid,SingleActionConfigurationJudgement.Completion(completed,registration),seedValid,projections);
        var aggregate=SingleActionConfigurationJudgement.Aggregate(repeats);
        var result=new SingleActionCandidate(1,"single-action",true,run,item,configuration,source,capture?.ManagedVersion,capture?.NativeVersion,
            new(StringComparer.Ordinal){["loadgen"]=Digest(options.LoadGenImage!),["verifier"]=Digest(options.VerifierImage!),["enrichment"]=Digest(options.EnrichmentImage!)},aggregate.Status,repeats,aggregate.Deterministic,aggregate.Outcome,"not_applicable",[],security,null);
        if(final){earlier[configuration]=result;result=result with{ItemAssessment=SingleActionConfigurationJudgement.JudgeItem(item,earlier)};}
        string candidateName=SingleActionPaths.Candidate(run,item,configuration);
        await Bounded(async ct=>{await verifier.CreateAsync(candidateName,SingleActionCandidateCodec.Write(result),ct).ConfigureAwait(false);return true;},cancellationToken).ConfigureAwait(false);
        if(aggregate.Status!="complete_valid")return 1;
        var judgeReceipt=new SingleActionJudgeReceipt(1,"single-action-judge",run,item,configuration,source,options.JobName!,options.PodName!,options.PodUid!,candidateName,0,true);
        completion.SetPendingBlobReceipt("verifier",SingleActionPaths.JudgeCompletion(run,item,configuration),SingleActionWire.Write(judgeReceipt,65536));
        return 0;
    }

    private static bool IsRegistrationPath(string name,string prefix)
    {
        string registrations=prefix+"registrations/";
        if(!name.StartsWith(registrations,StringComparison.Ordinal)||!name.EndsWith(".json",StringComparison.Ordinal))return false;
        string pod=name[registrations.Length..^5];
        return pod.Length is >=1 and <=253&&!name.Contains("..",StringComparison.Ordinal)&&pod.All(character=>char.IsAsciiLetterOrDigit(character)||character is '-' or '.');
    }

    private static string Digest(string image)=>image[(image.LastIndexOf('@')+1)..];
    private static bool Recoverable(Exception ex,CancellationToken token)=>ex is FormatException or InvalidDataException or IOException or TimeoutException||ex is OperationCanceledException&&!token.IsCancellationRequested;
    private async Task<T> Bounded<T>(Func<CancellationToken,Task<T>> operation,CancellationToken token)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10),clock);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,deadline.Token);
        long start=clock.GetTimestamp();
        T value=await operation(linked.Token).ConfigureAwait(false);linked.Token.ThrowIfCancellationRequested();
        if(clock.GetElapsedTime(start)>=TimeSpan.FromSeconds(10))throw new TimeoutException("single_action_io_timeout");
        return value;
    }
}
