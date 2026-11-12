using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Parchwell.Verifier.Common.Kafka;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.Tests.Support;
using Xunit;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyBoundedReadTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private const string FirstProblem = "b10_abrupt_verify_first_evidence_invalid";
    private const string SuccessorProblem = "b10_abrupt_verify_successor_evidence_invalid";
    private const string FirstReason = "b10_restart_first_evidence_unavailable";
    private const string IdentityReason = "b10_restart_identity_unauthenticated";
    private const string SuccessorReason = "b10_restart_successor_evidence_unavailable";

    [Fact]
    public async Task Given_ValidBoundedStoreReads_When_HostRuns_Then_EvidenceRemainsAvailable()
    {
        await kafka.ResetAsync();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files);
        var store = new BoundedStore(run);

        Assert.Equal(0, await RunAsync(run, store));

        Assert.Empty(run.Data["problems"]!.AsArray());
        Assert.Empty(run.Data["eligibility_reasons"]!.AsArray());
        Assert.Equal("pending_sweep", run.Data["eligibility"]!.GetValue<string>());
        Assert.Equal("neither", run.Data["comparison"]!.GetValue<string>());
        Assert.Equal(2, run.Data["arms"]!.AsArray().Count);
        Assert.Equal(B10AbruptVerifyEvidence.Second, run.Data["successor_instance_id"]!.GetValue<string>());
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.Equal(0, store.Rejections);
    }

    [Theory]
    [InlineData("registration.json", 65536L, 1)]
    [InlineData("registration.json", 65536L, 2)]
    [InlineData("journal.jsonl", 16777216L, 1)]
    [InlineData("abrupt-manifest.json", 65536L, 1)]
    [InlineData("termination.json", 65536L, 1)]
    [InlineData("same_instance-pre-acceptance-report.json", 1048576L, 1)]
    [InlineData("control-post-drain-report.json", 1048576L, 1)]
    [InlineData("cross_restart-pre-acceptance-report.json", 1048576L, 1)]
    public async Task Given_FirstBoundedStoreLimitException_When_HostRuns_Then_OnlyFirstUnavailableReasonIsPublished(string name, long limit, int occurrence)
    {
        await kafka.ResetAsync();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files);
        var store = new BoundedStore(run, Path(B10AbruptVerifyEvidence.First, name), limit, occurrence);

        Assert.Equal(0, await RunAsync(run, store));

        AssertUnavailable(run, true, FirstReason);
        Assert.Equal(1, store.Rejections);
        Assert.Equal(occurrence, store.TargetReads);
    }

    [Theory]
    [InlineData("registration.json", 65536L, 2)]
    [InlineData("registration.json", 65536L, 3)]
    [InlineData("ledger.jsonl", 268435456L, 1)]
    [InlineData("manifest.json", 67108864L, 1)]
    [InlineData("statistics.jsonl", 134217728L, 1)]
    [InlineData("request-lifecycle.jsonl", 134217728L, 1)]
    [InlineData("capabilities.json", 65536L, 1)]
    [InlineData("completion.json", 65536L, 1)]
    public async Task Given_SuccessorBoundedStoreLimitException_When_HostRuns_Then_OnlySuccessorUnavailableReasonIsPublished(string name, long limit, int occurrence)
    {
        await kafka.ResetAsync();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, new B10AbruptVerifyEvidence().Files);
        var store = new BoundedStore(run, Path(B10AbruptVerifyEvidence.Second, name), limit, occurrence);

        Assert.Equal(0, await RunAsync(run, store));

        AssertUnavailable(run, false, SuccessorReason);
        Assert.Equal(1, store.Rejections);
        Assert.Equal(occurrence, store.TargetReads);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("schema")]
    [InlineData("hash")]
    public async Task Given_FirstContentFaultAfterSuccessfulBoundedRead_When_HostRuns_Then_IdentityReasonIsRetained(string defect)
    {
        await kafka.ResetAsync();
        var evidence = new B10AbruptVerifyEvidence();
        if (defect == "json") evidence.Files["first/abrupt-manifest.json"] = Encoding.UTF8.GetBytes("{");
        else if (defect == "schema") evidence.Change("first/abrupt-manifest.json", root => root["schema_version"] = 2);
        else evidence.Change("first/termination.json", root => root["data"]!["manifest"]!["sha256"] = new string('0', 64));
        if (defect != "hash") evidence.Change("first/termination.json", root => root["data"]!["manifest"] =
            B10AbruptVerifyEvidence.Descriptor("abrupt-manifest.json", evidence.Files["first/abrupt-manifest.json"]));
        evidence.RehashSuccessor();
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, evidence.Files);
        var store = new BoundedStore(run);

        Assert.Equal(0, await RunAsync(run, store));

        AssertUnavailable(run, true, IdentityReason);
        Assert.Equal(0, store.Rejections);
    }

    private static string Path(string pod, string name) => B10AbruptVerifyEvidence.Prefix + pod + "/" + name;

    private static void AssertUnavailable(B10AbruptVerifyHarness run, bool first, string reason)
    {
        Assert.Equal(first ? FirstProblem : SuccessorProblem,
            Assert.Single(run.Data["problems"]!.AsArray())!.GetValue<string>());
        Assert.Equal(reason, Assert.Single(run.Data["eligibility_reasons"]!.AsArray())!.GetValue<string>());
        Assert.Equal("ineligible", run.Data["eligibility"]!.GetValue<string>());
        Assert.Equal("unavailable", run.Data["comparison"]!.GetValue<string>());
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.NotNull(run.Data["end_line"]);
        Assert.NotNull(run.Data["timeout"]);
        Assert.Null(run.Data["successor_instance_id"]);
        if (first)
        {
            Assert.Null(run.Data["arms"]);
            Assert.Null(run.Data["native_errors"]);
        }
        else Assert.Equal(2, run.Data["arms"]!.AsArray().Count);
    }

    private async Task<int> RunAsync(B10AbruptVerifyHarness run, IProducerArtifactStore store)
    {
        using var authentication = new KafkaSessionTestSupport();
        authentication.Refresh();
        return await Program.RunAsync(run.Configuration.Select(pair => "--" + pair.Key + "=" + pair.Value).ToArray(), services =>
        {
            services.RemoveAll<IHostedService>();
            foreach (ServiceDescriptor item in services.Where(item => item.ServiceType.Name is "IConfigureTracerProviderBuilder" or "IConfigureMeterProviderBuilder" or "IConfigureLoggerProviderBuilder").ToArray()) services.Remove(item);
            services.RemoveAll<IVerifierArtifactStore>();
            services.AddSingleton<IVerifierArtifactStore>(run);
            services.RemoveAll<IProducerArtifactStore>();
            services.AddSingleton(store);
            services.RemoveAll<VerifierKafkaSessionFactory>();
            services.AddSingleton(provider => new VerifierKafkaSessionFactory(_ =>
            {
                IConsumer<byte[], byte[]> consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
                return new VerifierKafkaSession(consumer, new DependentAdminClientBuilder(consumer.Handle).Build(), authentication.Authentication,
                    run.Clock, provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value, new CancellationTokenSource());
            }));
        }, run.Clock).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
    }

    // Models both BoundedBlobReader overflow branches without retaining oversized blob contents.
    private sealed class BoundedStore(B10AbruptVerifyHarness run, string? rejectedPath = null, long expectedLimit = 0, int occurrence = 1) : IProducerArtifactStore
    {
        internal int Rejections { get; private set; }
        internal int TargetReads { get; private set; }
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => run.ReadAsync(name, cancellationToken);
        public Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken) => run.ListAsync(prefix, cancellationToken);

        public async Task<byte[]?> ReadBoundedAsync(string name, long maximumBytes, CancellationToken cancellationToken)
        {
            if (name == rejectedPath)
            {
                Assert.Equal(expectedLimit, maximumBytes);
                if (++TargetReads == occurrence)
                {
                    Rejections++;
                    throw new InvalidDataException("verifier_input_byte_limit_exceeded");
                }
            }
            byte[]? bytes = await run.ReadAsync(name, cancellationToken);
            Assert.True(bytes is null || bytes.LongLength <= maximumBytes);
            return bytes;
        }
    }
}
#pragma warning restore CA1707
