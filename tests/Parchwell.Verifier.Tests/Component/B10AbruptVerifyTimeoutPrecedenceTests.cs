using System.Text;
using System.Text.Json.Nodes;
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
// Failed timeout evidence reserves only listed directory identities; it cannot authenticate counts or hide foreign records.
[Collection("B10 abrupt verifier")]
[Trait("Tier", "Docker")]
public sealed class B10AbruptVerifyTimeoutPrecedenceTests(B10AbruptVerifyKafkaFixture kafka) : IClassFixture<B10AbruptVerifyKafkaFixture>
{
    private const string ProducerRoot = "synthetic/C1/r1/producer/";
    private const string TimeoutRoot = ProducerRoot + B10AbruptVerifyEvidence.Timeout + "/";
    private const string TimeoutProblem = "b10_abrupt_verify_timeout_evidence_invalid";
    private const string JoinProblem = "b10_abrupt_verify_record_unjoined";
    private const string TimeoutReason = "b10_restart_timeout_bookkeeping_unavailable";
    private const string JoinReason = "b10_restart_identity_unauthenticated";
    private static readonly string[] ArmCounts = ["unconfirmed_count", "resend_invoked", "originals_seen", "resends_seen", "pair_count", "unreported_attempts", "unreported_seen", "unreported_unseen", "lost_resends"];
    private static readonly string[] MemberCounts = ["copy_count", "original_copies", "resend_copies", "other_copies", "pair_count"];
    private static readonly string[] TimeoutCounts = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];

    [Theory]
    [InlineData("valid", false)]
    [InlineData("second-first", false)]
    [InlineData("second-last", false)]
    [InlineData("incomplete", false)]
    [InlineData("negative-pages", false)]
    [InlineData("excess-pages", false)]
    [InlineData("outside-first", false)]
    [InlineData("outside-last", false)]
    [InlineData("root-object-first", false)]
    [InlineData("root-object-last", false)]
    [InlineData("empty-instance-first", false)]
    [InlineData("invalid-instance-first", false)]
    [InlineData("empty-object-first", false)]
    [InlineData("missing-registration", false)]
    [InlineData("duplicate-registration", false)]
    [InlineData("invalid-registration", false)]
    [InlineData("wrong-job", false)]
    [InlineData("registration-alias", false)]
    [InlineData("missing-completion", false)]
    [InlineData("missing-publication", false)]
    [InlineData("valid", true)]
    [InlineData("second-first", true)]
    [InlineData("incomplete", true)]
    [InlineData("outside-first", true)]
    [InlineData("invalid-instance-first", true)]
    [InlineData("empty-object-first", true)]
    [InlineData("registration-alias", true)]
    [InlineData("missing-publication", true)]
    public async Task Given_TimeoutRecordAndEvidenceDefect_When_HostRuns_Then_HoldoutPreservesProbeCountsAndForeignDetection(string defect, bool foreign)
    {
        await kafka.ResetAsync();
        var evidence = new B10AbruptVerifyEvidence();
        evidence.SetTimeoutRows(B10AbruptVerifyEvidence.TimeoutRow("timeout-attempt", "ordinary", 0));
        using var run = new B10AbruptVerifyHarness(kafka.Bootstrap, evidence.Files);
        MutateArtifacts(run, defect);
        await run.ProduceAsync(0, 0, "same_instance", attempt: "s4");
        await run.ProduceAsync(0, 1, "same_instance", attempt: "t4");
        await run.ProduceAsync(1, 0);
        await run.ProduceAsync(1, 1, instance: B10AbruptVerifyEvidence.Second, attempt: "r4");
        await run.ProduceAsync(2, 0, instance: B10AbruptVerifyEvidence.Timeout, attempt: "timeout-attempt", key: "ordinary", sequence: 0);
        long next = 1;
        if (defect is "second-first" or "second-last")
            await run.ProduceAsync(2, next++, instance: "other", attempt: "untrusted", key: "ordinary-other", sequence: 0);
        if (foreign)
        {
            string instance = defect switch
            {
                "outside-first" => "foreign",
                "invalid-instance-first" => "invalid.instance",
                "empty-object-first" => "other",
                _ => "unlisted-foreign"
            };
            await run.ProduceAsync(2, next, instance: instance, attempt: "foreign-attempt", key: "foreign", sequence: 0);
        }

        Assert.Equal(0, await RunWithListingAsync(run, defect));
        bool invalid = defect != "valid";
        string[] problems = invalid ? foreign ? [TimeoutProblem, JoinProblem] : [TimeoutProblem] : foreign ? [JoinProblem] : [];
        string[] reasons = foreign ? invalid ? [JoinReason, TimeoutReason] : [JoinReason] : invalid ? [TimeoutReason] : [];
        Assert.Equal(problems, run.Data["problems"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(reasons, run.Data["eligibility_reasons"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(invalid || foreign ? "ineligible" : "pending_sweep", run.Data["eligibility"]!.GetValue<string>());
        Assert.Equal(foreign ? "unavailable" : "both", run.Data["comparison"]!.GetValue<string>());
        Assert.True(run.Data["window_complete"]!.GetValue<bool>());
        Assert.NotNull(run.Data["end_line"]);
        if (invalid) Assert.Null(run.Data["timeout"]);
        else
        {
            Assert.Equal(B10AbruptVerifyEvidence.Timeout, run.Data["timeout_instance_id"]!.GetValue<string>());
            Assert.Equal(new long[] { 1, 0, 0, 0, 0, 0 }, TimeoutCounts
                .Select(field => run.Data["timeout"]![field]!.GetValue<long>()));
        }
        if (invalid && defect is not ("missing-completion" or "missing-publication")) Assert.Null(run.Data["timeout_instance_id"]);
        if (defect is "missing-completion" or "missing-publication")
            Assert.Equal(B10AbruptVerifyEvidence.Timeout, run.Data["timeout_instance_id"]!.GetValue<string>());
        JsonArray arms = run.Data["arms"]!.AsArray();
        Assert.Equal(2, arms.Count);
        foreach (JsonNode? arm in arms)
        {
            bool cross = arm!["arm"]!.GetValue<string>() == "cross_restart";
            Assert.Equal(new long[] { 1, 1, 1, 1, 1, cross ? 1 : 0, cross ? 1 : 0, 0, 0 }, ArmCounts.Select(field => arm[field]!.GetValue<long>()));
            JsonNode member = Assert.Single(arm["members"]!.AsArray())!;
            Assert.Equal(new long[] { 2, 1, 1, 0, 1 }, MemberCounts.Select(field => member[field]!.GetValue<long>()));
            Assert.False(member["resend_lost"]!.GetValue<bool>());
            Assert.False(member["copies_truncated"]!.GetValue<bool>());
            Assert.Equal(2, member["copies"]!.AsArray().Count);
            Assert.NotNull(arm["witness"]);
        }
    }

    private static void MutateArtifacts(B10AbruptVerifyHarness run, string defect)
    {
        if (defect == "missing-registration") run.Ledger.Remove(TimeoutRoot + "registration.json");
        if (defect == "invalid-registration") run.Ledger[TimeoutRoot + "registration.json"] = Encoding.UTF8.GetBytes("{}");
        if (defect == "wrong-job")
        {
            JsonObject registration = JsonNode.Parse(run.Ledger[TimeoutRoot + "registration.json"])!.AsObject();
            registration["job_name"] = "synthetic-c1-r1-loadgen-restart";
            run.Ledger[TimeoutRoot + "registration.json"] = Encoding.UTF8.GetBytes(registration.ToJsonString());
        }
        if (defect == "registration-alias")
        {
            JsonObject registration = JsonNode.Parse(run.Ledger[TimeoutRoot + "registration.json"])!.AsObject();
            registration["identity"]!["instance_id"] = "unlisted-foreign";
            run.Ledger[TimeoutRoot + "registration.json"] = Encoding.UTF8.GetBytes(registration.ToJsonString());
        }
        if (defect == "missing-completion") run.Ledger.Remove(TimeoutRoot + "completion.json");
        if (defect == "missing-publication") run.Ledger.Remove(TimeoutRoot + "publication/index.json");
    }

    private async Task<int> RunWithListingAsync(B10AbruptVerifyHarness run, string defect)
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
            services.AddSingleton<IProducerArtifactStore>(new ListingStore(run, defect));
            services.RemoveAll<VerifierKafkaSessionFactory>();
            services.AddSingleton(provider => new VerifierKafkaSessionFactory(_ =>
            {
                IConsumer<byte[], byte[]> consumer = ComponentRunHarness.Consumer(kafka.Bootstrap);
                return new VerifierKafkaSession(consumer, new DependentAdminClientBuilder(consumer.Handle).Build(), authentication.Authentication,
                    run.Clock, provider.GetRequiredService<IOptions<VerifierHostOptions>>().Value, new CancellationTokenSource());
            }));
        }, run.Clock).WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
    }

    private sealed class ListingStore(B10AbruptVerifyHarness run, string defect) : IProducerArtifactStore
    {
        public Task<byte[]?> ReadAsync(string name, CancellationToken cancellationToken) => run.ReadAsync(name, cancellationToken);

        public async Task<BlobListing> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            BlobListing listing = await run.ListAsync(prefix, cancellationToken);
            if (prefix != ProducerRoot) return listing;
            string[] names = listing.Names.ToArray();
            string? extra = defect switch
            {
                "second-first" or "second-last" => ProducerRoot + "other/registration.json",
                "outside-first" or "outside-last" => "outside/foreign/registration.json",
                "root-object-first" or "root-object-last" => ProducerRoot + "orphan.json",
                "empty-instance-first" => ProducerRoot + "/registration.json",
                "invalid-instance-first" => ProducerRoot + "invalid.instance/registration.json",
                "empty-object-first" => ProducerRoot + "other/",
                "duplicate-registration" => TimeoutRoot + "registration.json",
                _ => null
            };
            if (extra is not null) names = defect.EndsWith("first", StringComparison.Ordinal) ? [extra, .. names] : [.. names, extra];
            return defect switch
            {
                "incomplete" => new(names[..1], 1, false, "listing_incomplete"),
                "negative-pages" => new(names, -1, true),
                "excess-pages" => new(names, 17, true),
                _ => new(names, 1, true)
            };
        }
    }
}
#pragma warning restore CA1707
