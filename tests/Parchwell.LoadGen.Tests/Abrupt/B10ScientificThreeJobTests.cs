using System.Globalization;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707
[Collection("B10AbruptEnvironment")]
public sealed class B10ScientificThreeJobTests
{
    [Theory]
    [InlineData("parent", "1")]
    [InlineData("child", "3")]
    [InlineData("successor", "5")]
    public void Given_ScientificIdentity_When_Validated_Then_RepeatAndJobAreBound(string role, string repeat)
    {
        var options = Options("scientific", "b10-1-1", repeat, role);
        options.Validate(role);
        Assert.Equal(int.Parse(repeat, CultureInfo.InvariantCulture), options.Repeat);
        Assert.Equal(options.Repeat, options.Identity().Repeat);
        Assert.Equal($"b10-1-1-c1-r{repeat}-loadgen-{(role == "successor" ? "successor" : "restart")}", options.JobName);
        Assert.Equal($"B10/b10-1-1/C1/{repeat}/cross_restart", B10AbruptWire.ProbeKey(options.Identity(), "cross_restart"));
        Assert.Equal($"b10-1-1/C1/r{repeat}/b10-abrupt/synthetic-first/", B10AbruptWire.ArtifactPrefix(options.Identity(), options.Purpose));
    }

    [Theory]
    [InlineData("scientific", "b10d-1-1", "1")]
    [InlineData("scientific", "synthetic", "1")]
    [InlineData("scientific", "b10-1-1\n", "1")]
    [InlineData("scientific", "b10-1-1", "0")]
    [InlineData("scientific", "b10-1-1", "6")]
    [InlineData("scientific", "b10-1-1", "03")]
    [InlineData("diagnostic_trial", "b10-1-1", "2")]
    public void Given_InvalidScientificOrTrialIdentity_When_Validated_Then_DiagnosticIdentityFails(string purpose, string runId, string repeat)
    {
        var failure = Assert.Throws<InvalidDataException>(() => Options(purpose, runId, repeat).Validate());
        Assert.Equal("DiagnosticIdentity", B10AbruptOptions.ReadValidationDetail(failure));
    }

    [Theory]
    [InlineData("synthetic")]
    [InlineData("b10-1-1")]
    public void Given_TrialWithAnyValidRunId_When_Validated_Then_TrialEnvelopeAndPrefixRemain(string runId)
    {
        var options = Options("diagnostic_trial", runId, "1");
        options.Validate();
        byte[] bytes = B10AbruptWire.SerializeEnvelope("registration", options.Identity(), new { registered_utc = B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()) }, options.Purpose);
        var root = B10AbruptWire.ReadEnvelope(bytes, "registration");
        Assert.Equal("diagnostic_trial", root.GetProperty("purpose").GetString());
        Assert.Equal($"{runId}/C1/r1/b10-abrupt-diagnostic/synthetic-first/", B10AbruptWire.ArtifactPrefix(options.Identity(), options.Purpose));
    }

    [Theory]
    [InlineData("scientific", 3, "diagnostic_trial", 1)]
    [InlineData("diagnostic_trial", 1, "scientific", 3)]
    [InlineData("scientific", 2, "scientific", 3)]
    public void Given_MismatchedPurposeOrRepeat_When_SuccessorReadsJournal_Then_MalformedFrame(string writtenPurpose, int writtenRepeat, string purpose, int repeat)
    {
        var identity = Options(writtenPurpose, "b10-1-1", writtenRepeat.ToString(CultureInfo.InvariantCulture)).Identity();
        var frame = new B10AbruptWire.Frame(identity, 1, B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()), 0, 1,
            "child_started", B10AbruptWire.Element(new { pid = 1, start_time_ticks = 1, boot_id = "synthetic" }));
        byte[] bytes = B10AbruptWire.WriteFrame(frame, writtenPurpose);
        Assert.Equal("b10_abrupt_capture_gap:malformed_frame", Assert.Throws<InvalidDataException>(() => B10AbruptWire.ReadFrame(bytes, purpose, repeat)).Message);
        var snapshot = B10AbruptJournal.Read([.. bytes, 10], identity with { Repeat = repeat }, purpose);
        Assert.Equal(["malformed_frame"], snapshot.Gaps);
        Assert.Empty(snapshot.Frames);
    }

    [Fact]
    public async Task Given_ScientificRepeatThree_When_ProducerAndSuccessorPublish_Then_AllActualBytesCarryConfiguredIdentity()
    {
        string root = Path.Combine(Path.GetTempPath(), "b10-scientific-" + Guid.NewGuid().ToString("N"));
        try
        {
            await B10AbruptWireProducerTests.ProduceAsync(root, TestContext.Current.CancellationToken, purpose: "scientific", repeat: 3);
            foreach (string directory in new[] { "first", "successor" })
            {
                foreach (string file in new[] { "registration.json", directory == "first" ? "termination.json" : "completion.json" })
                {
                    var envelope = B10AbruptWire.Parse(await File.ReadAllBytesAsync(Path.Combine(root, directory, file), TestContext.Current.CancellationToken));
                    Assert.Equal("scientific", envelope.GetProperty("purpose").GetString());
                    Assert.Equal(3, envelope.GetProperty("identity").GetProperty("repeat").GetInt32());
                    Assert.False(envelope.GetProperty("scientific_admissible").GetBoolean());
                }
            }
            byte[] journal = await File.ReadAllBytesAsync(Path.Combine(root, "first", "journal.jsonl"), TestContext.Current.CancellationToken);
            var snapshot = B10AbruptJournal.Read(journal, purpose: "scientific");
            Assert.Empty(snapshot.Gaps);
            Assert.Empty(snapshot.Failures);
            Assert.All(snapshot.Frames.Where(frame => frame.Kind is "prepared" or "warmup_prepared"), frame =>
                Assert.Equal("B10/b10-1-1/C1/3/" + frame.Data.GetProperty("arm").GetString(), frame.Data.GetProperty("key").GetString()));
            Assert.DoesNotContain("/C1/1/", Encoding.UTF8.GetString(journal), StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Given_ScientificRegistration_When_Published_Then_StorageNameUsesScientificRepeatPrefix()
    {
        var options = Options("scientific", "b10-1-1", "3");
        var store = new Storage();
        var publisher = new B10AbruptPublisher(store, TimeProvider.System, options.Purpose);
        await publisher.RegisterDiagnosticAsync(options.Identity(), TestContext.Current.CancellationToken);
        Assert.Equal("b10-1-1/C1/r3/b10-abrupt/synthetic-first/registration.json", store.Name);
        Assert.Equal("scientific", B10AbruptWire.ReadEnvelope(store.Bytes!, "registration", options.Purpose, options.Repeat).GetProperty("purpose").GetString());
    }

    private static B10AbruptOptions Options(string purpose, string runId, string repeat, string role = "parent")
    {
        var valid = B10AbruptWireProducerTests.TestOptions("/work/loadgen", role);
        var values = valid.ToChildEnvironment().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values[B10AbruptOptions.Prefix + "Role"] = role;
        values[B10AbruptOptions.Prefix + "Purpose"] = purpose;
        values[B10AbruptOptions.Prefix + "RunId"] = runId;
        values[B10AbruptOptions.Prefix + "Repeat"] = repeat;
        values[B10AbruptOptions.Prefix + "PodName"] = "synthetic-first";
        values[B10AbruptOptions.Prefix + "JobName"] = $"{runId}-c1-r{repeat}-loadgen-{(role == "successor" ? "successor" : "restart")}";
        if (role == "successor") values[B10AbruptOptions.Prefix + "PredecessorBindingPath"] = "/bindings/predecessor.json";
        return B10AbruptOptions.Read(values);
    }

    private sealed class Storage : IB10AbruptStorage
    {
        internal string? Name { get; private set; }
        internal byte[]? Bytes { get; private set; }
        public Task CreateAsync(string name, byte[] bytes, CancellationToken cancellationToken) { Name = name; Bytes = bytes; return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(string name, int maximumBytes, CancellationToken cancellationToken) { Assert.Equal(Name, name); return Task.FromResult(Bytes!); }
    }
}
