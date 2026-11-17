using Parchwell.Verifier.VerifyRun.Consumer.Offsets;
using System.Text;
using Azure;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Consumer;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.VerifyRun;
using Parchwell.Verifier.VerifyRun.Configuration;
using Parchwell.Verifier.VerifyRun.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class IntegrationConsumerHostTests
{
    [Fact]
    public async Task Given_C03Fixture_U1_When_ConsumerExtensionRuns_Then_EveryRootFieldIsByteIdentical()
    {
        using var plain = new HostEvidenceBuilder(scenarioId: "C03", phase: "C0");
        using var extended = new HostEvidenceBuilder(scenarioId: "C03", phase: "C0");
        Assert.Equal(0, await plain.Verify().ExecuteAsync(TestContext.Current.CancellationToken));
        var options = extended.Options;
        var limits = new InputLimits(options.MaxInputDepth!.Value, options.MaxArtifactBytes!.Value,
            options.MaxTotalInputBytes!.Value, options.MaxRowBytes!.Value, options.MaxRowsPerArtifact!.Value,
            options.MaxInputEntries!.Value, options.MaxDiagnosticBytes!.Value);
        var plan = new ConsumerVerificationPlan(null, 600000,
            new Dictionary<string, string> { ["isolation.level"] = "read_committed" },
            new Dictionary<string, string> { ["isolation.level"] = "read_committed" });
        int reads = 0;
        ConsumerOffsetReader offsets = (_, _, _) => { reads++; throw new InvalidOperationException("No registered members authorize an offset read."); };
        var extension = new ConsumerRepeatExtension(options, plan,
            new ConsumerArtifactReader(new EmptyConsumerStore(), limits, options.BlobIoTimeout!.Value, extended.Kafka.Clock),
            extended, offsets, extended.Kafka.Clock, "synthetic-namespace.servicebus.windows.net:9093");
        var completion = new VerifierCompletionState();
        Assert.Equal(1, await Handler(extended, completion, extension).ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, reads);
        using JsonDocument original = JsonDocument.Parse(plain.Output!);
        using JsonDocument result = JsonDocument.Parse(extended.Output!);
        Assert.Equal(original.RootElement.EnumerateObject().Select(p => p.Name).Append("consumer"), result.RootElement.EnumerateObject().Select(p => p.Name));
        foreach (JsonProperty property in original.RootElement.EnumerateObject())
            Assert.Equal(Encoding.UTF8.GetBytes(property.Value.GetRawText()), Encoding.UTF8.GetBytes(result.RootElement.GetProperty(property.Name).GetRawText()));
        Assert.Equal("invalid", result.RootElement.GetProperty("consumer").GetProperty("status").GetString());
        Assert.Equal(["consumer_registration_invalid"], result.RootElement.GetProperty("consumer").GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task Given_B04Fixture_U1_When_VerifiedWithoutExtension_Then_ResultAndReceiptEqualRecordedBaseBytes()
    {
        using var fixture = new HostEvidenceBuilder(scenarioId: "B04", phase: "C0");
        CanonicalizeB04Settings(fixture);
        var completion = new VerifierCompletionState();
        int exit = await fixture.Verify(completion).ExecuteAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, exit);
        completion.MarkReturned(fixture.Options, exit, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out string container, out string name, out byte[] receipt));
        Assert.Equal("verifier", container);
        Assert.Equal(PhasePaths.VerifyCompletion(fixture.Options.RunId!, fixture.Options.Phase!, fixture.Options.Repeat!.Value), name);
        Assert.Equal(Encoding.UTF8.GetBytes(IntegrationB04BaseFixtures.Result), fixture.Output);
        Assert.Equal(Encoding.UTF8.GetBytes(IntegrationB04BaseFixtures.Receipt), receipt);
    }

    [Theory]
    [InlineData("listing")]
    [InlineData("forbidden")]
    [InlineData("timeout")]
    [InlineData("artifact-limit")]
    [InlineData("aggregate-limit")]
    public async Task Given_UnavailableConsumerEvidence_U20_When_Verified_Then_WritesResultAndFailureReceipt(string failure)
    {
        using var fixture=new HostEvidenceBuilder(scenarioId:"C03",phase:"C0");
        var store=new ConsumerWireFixture();
        string prefix=PhaseIdentity.RepeatPrefix(fixture.Options.RunId!,fixture.Options.Phase!,1);
        var renamed=store.Blobs.ToDictionary(pair=>pair.Key.Replace(ConsumerWireFixture.Prefix,prefix,StringComparison.Ordinal),pair=>pair.Value,StringComparer.Ordinal);
        store.Blobs.Clear();foreach(var entry in renamed)store.Blobs.Add(entry.Key,entry.Value);
        if(failure=="listing")store.ListingComplete=false;
        else store.ReadOverride=(name,_)=>
        {
            if(name.EndsWith("record/000000.jsonl",StringComparison.Ordinal))
            {
                if(failure=="forbidden")throw new RequestFailedException(403,"synthetic denied");
                if(failure=="timeout")fixture.Kafka.Clock.Advance(101);
                if(failure=="artifact-limit")return Task.FromResult<byte[]?>(new byte[131073]);
                if(failure=="aggregate-limit")return Task.FromResult<byte[]?>(new byte[131072]);
            }
            return Task.FromResult(store.Blobs.GetValueOrDefault(name));
        };
        if(failure=="aggregate-limit")fixture.Options.MaxTotalInputBytes=131072;
        VerifierHostOptions options=fixture.Options;
        var limits=new InputLimits(options.MaxInputDepth!.Value,options.MaxArtifactBytes!.Value,options.MaxTotalInputBytes!.Value,
            options.MaxRowBytes!.Value,options.MaxRowsPerArtifact!.Value,options.MaxInputEntries!.Value,options.MaxDiagnosticBytes!.Value);
        var settings=new Dictionary<string,string>{{"isolation.level","read_committed"}};
        var plan=new ConsumerVerificationPlan(null,600000,settings,settings);int offsetReads=0;
        ConsumerOffsetReader offsets=(_,_,_)=>{offsetReads++;throw new InvalidOperationException("Unavailable records cannot authorize an offset read.");};
        var extension=new ConsumerRepeatExtension(options,plan,new ConsumerArtifactReader(store,limits,options.BlobIoTimeout!.Value,fixture.Kafka.Clock),
            fixture,offsets,fixture.Kafka.Clock,"synthetic:9092");
        var completion=new VerifierCompletionState();
        Assert.Equal(1,await Handler(fixture,completion,extension).ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0,offsetReads);
        Assert.Equal(1,fixture.Creates);
        Assert.Equal(options.ResultOutputName,fixture.OutputName);
        using JsonDocument result=JsonDocument.Parse(fixture.Output!);
        Assert.Equal("complete_valid",result.RootElement.GetProperty("status").GetString());
        JsonElement consumer=result.RootElement.GetProperty("consumer");
        Assert.Equal("incomplete",consumer.GetProperty("status").GetString());
        Assert.Equal(["consumer_record_unavailable"],consumer.GetProperty("reasons").EnumerateArray().Select(value=>value.GetString()));
        Assert.Equal("not_attempted",consumer.GetProperty("offset_read").GetProperty("outcome").GetString());
        Assert.False(consumer.TryGetProperty("counts",out _));
        Assert.NotEmpty(consumer.GetProperty("diagnostics").EnumerateArray());
        completion.MarkReturned(options,1,CancellationToken.None,CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _,out string receiptName,out byte[] receipt));
        Assert.Equal(PhasePaths.VerifyCompletion(options.RunId!,options.Phase!,1),receiptName);
        Assert.Equal(1,PhaseWireCodec.ReadReceipt(receipt).HandlerExitCode);
    }

    public static TheoryData<string, string> StatusPairs
    {
        get
        {
            var pairs = new TheoryData<string, string>();
            foreach (VerifierResultStatus root in Enum.GetValues<VerifierResultStatus>())
                foreach (VerifierResultStatus consumer in Enum.GetValues<VerifierResultStatus>()) pairs.Add(StatusName(root), StatusName(consumer));
            return pairs;
        }
    }

    [Theory]
    [MemberData(nameof(StatusPairs))]
    public async Task Given_RootAndConsumerStatusPair_U19_When_Verified_Then_ProcessAndReceiptSucceedOnlyForTwoCompleteStatuses(
        string rootName, string consumerName)
    {
        VerifierResultStatus rootStatus = ParseStatus(rootName), consumerStatus = ParseStatus(consumerName);
        using var fixture = new HostEvidenceBuilder(scenarioId: "C03", phase: "C0");
        if (rootStatus == VerifierResultStatus.Invalid) fixture.Inputs["manifest.json"] = null;
        if (rootStatus == VerifierResultStatus.Incomplete) fixture.Kafka.ReadMetadata = () => KafkaSessionTestSupport.Metadata();
        var completion = new VerifierCompletionState();
        var extension = new StatusExtension(consumerStatus);
        int exit = await Handler(fixture, completion, extension).ExecuteAsync(TestContext.Current.CancellationToken);
        int expected = rootStatus == VerifierResultStatus.CompleteValid && consumerStatus == VerifierResultStatus.CompleteValid ? 0 : 1;
        Assert.Equal(expected, exit);
        Assert.Equal(1, extension.Calls);
        Assert.Equal(rootStatus, extension.RootStatus);
        using JsonDocument result = JsonDocument.Parse(fixture.Output!);
        Assert.Equal(StatusName(rootStatus), result.RootElement.GetProperty("status").GetString());
        Assert.Equal(StatusName(consumerStatus), result.RootElement.GetProperty("consumer").GetProperty("status").GetString());
        completion.MarkReturned(fixture.Options, exit, CancellationToken.None, CancellationToken.None);
        Assert.True(completion.TryGetBlobReceipt(out _, out string path, out byte[] receipt));
        Assert.Equal(PhasePaths.VerifyCompletion(fixture.Options.RunId!, fixture.Options.Phase!, fixture.Options.Repeat!.Value), path);
        WorkerReceipt parsed = PhaseWireCodec.ReadReceipt(receipt);
        Assert.Equal("verify", parsed.Mode);
        Assert.Equal(expected, parsed.HandlerExitCode);
    }

    private static void CanonicalizeB04Settings(HostEvidenceBuilder fixture)
    {
        JsonObject capabilities = fixture.Json("capabilities.json");
        JsonObject settings = capabilities["resolved_configuration"]!["settings"]!.AsObject();
        var ordered = new JsonObject();
        foreach (KeyValuePair<string, JsonNode?> entry in settings.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            ordered.Add(entry.Key, entry.Value?.DeepClone());
        capabilities["resolved_configuration"]!["settings"] = ordered;
        fixture.Set("capabilities.json", capabilities);
    }

    private static VerifyRunHandler Handler(HostEvidenceBuilder fixture, VerifierCompletionState completion, IRepeatResultExtension extension)
    {
        _ = fixture.Verify(); // The support fixture constructs its coherent publications before the selected handler is created.
        return new VerifyRunHandler(Options.Create(fixture.Options), fixture.HandlerClock ?? fixture.Kafka.Clock,
            new VerifierKafkaSessionFactory(_ => fixture.Kafka.Session), fixture, fixture, fixture.Loader,
            new RegisteredProducerConfigurationResolver(Options.Create(fixture.Options)), completion, extension);
    }

    private static VerifierResultStatus ParseStatus(string name) => name switch
    {
        "complete_valid" => VerifierResultStatus.CompleteValid,
        "invalid" => VerifierResultStatus.Invalid,
        "incomplete" => VerifierResultStatus.Incomplete,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static string StatusName(VerifierResultStatus status) => status switch
    {
        VerifierResultStatus.CompleteValid => "complete_valid",
        VerifierResultStatus.Invalid => "invalid",
        VerifierResultStatus.Incomplete => "incomplete",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private sealed class StatusExtension(VerifierResultStatus status) : IRepeatResultExtension
    {
        public int CompletionDeadlineMs => 600000;
        internal int Calls { get; private set; }
        internal VerifierResultStatus? RootStatus { get; private set; }
        public Task<RepeatResultExtension> EvaluateAsync(RepeatResultContext context, CancellationToken cancellationToken)
        {
            Calls++; RootStatus = context.RootStatus;
            string name = StatusName(status);
            var block = new JsonObject
            {
                ["schema_version"] = 1, ["status"] = name,
                ["reasons"] = status == VerifierResultStatus.CompleteValid ? new JsonArray() : new JsonArray(status == VerifierResultStatus.Invalid ? "consumer_registration_invalid" : "consumer_completion_deadline"),
                ["group_id"] = "synthetic-c5-c0-r1-consumers", ["kill_count"] = null, ["members"] = new JsonArray(),
                ["offset_read"] = new JsonObject { ["outcome"] = "not_attempted", ["requested_partitions"] = new JsonArray(), ["started_utc"] = null, ["ended_utc"] = null, ["error_code"] = null, ["offsets"] = new JsonArray() },
                ["partitions"] = new JsonArray(), ["diagnostics"] = new JsonArray()
            };
            if (status == VerifierResultStatus.CompleteValid) block["counts"] = new JsonObject { ["processed_once"] = 0, ["processed_again"] = 0, ["processed_again_extra_rows"] = 0, ["committed_never_processed"] = 0 };
            return Task.FromResult(new RepeatResultExtension(status, JsonSerializer.SerializeToElement(block)));
        }
    }

    private sealed class EmptyConsumerStore : IProducerArtifactStore
    {
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => Task.FromResult(new BlobListing([], 1, true));
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => Task.FromResult<byte[]?>(null);
    }
}
#pragma warning restore CA1707
