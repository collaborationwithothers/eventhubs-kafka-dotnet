using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Input;
using Xunit;
using Parchwell.Verifier.Tests.Support;

namespace Parchwell.Verifier.Tests.Component;

#pragma warning disable CA1707
public sealed class B10ScientificThreeJobEarlierReadTests
{
    private const string Run = "b10-1-1";
    private static readonly InputLimits Limits = new(32, 268435456, 2147483648, 65536, 4096, 10000, 4096);
    private static VerifierHostOptions Options => new()
    {
        RunId = Run, Repeat = 3, VerifierCommit = B10AbruptVerifyEvidence.Commit, LoadGenImage = B10AbruptVerifyEvidence.Image
    };

    [Fact]
    public async Task Given_EmptyEarlierPrefixes_When_Read_Then_EveryEarlierPrefixIsReadAndNoIdentityIsInvented()
    {
        var listed = new List<string>();
        IReadOnlySet<string> result = await Read(async (prefix, ct) =>
        {
            listed.Add(prefix); await Task.CompletedTask; return new BlobListing([], 0, true);
        });
        Assert.Empty(result);
        Assert.Equal([$"{Run}/C1/r1/producer/", $"{Run}/C1/r1/b10-abrupt/", $"{Run}/C1/r2/producer/", $"{Run}/C1/r2/b10-abrupt/"], listed);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public async Task Given_PageBoundary_When_Read_Then_OnlyBoundedCompleteListingIsAccepted(int pages, bool accepted)
    {
        Task<IReadOnlySet<string>> task = Read((_, _) => Task.FromResult(new BlobListing([], pages, true)));
        if (accepted) Assert.Empty(await task); else await Assert.ThrowsAsync<FormatException>(() => task);
    }

    [Theory]
    [InlineData(1023, true)]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public async Task Given_EntryBoundary_When_Read_Then_OnlyBoundedListingIsAccepted(int count, bool accepted)
    {
        Task<IReadOnlySet<string>> task = Read((prefix, _) => Task.FromResult(new BlobListing(
            Enumerable.Range(0, count).Select(index => prefix + "earlier/file" + index).ToArray(), 1, true)));
        if (accepted) Assert.Empty(await task); else await Assert.ThrowsAsync<FormatException>(() => task);
    }

    [Theory]
    [InlineData(1048575, true)]
    [InlineData(1048576, true)]
    [InlineData(1048577, false)]
    public async Task Given_ListingNameByteBoundary_When_Read_Then_OnlyBoundedListingIsAccepted(int bytes, bool accepted)
    {
        Task<IReadOnlySet<string>> task = Read((prefix, _) =>
        {
            string stem = prefix + "earlier/";
            return Task.FromResult(new BlobListing([stem + new string('x', bytes - Encoding.UTF8.GetByteCount(stem))], 1, true));
        });
        if (accepted) Assert.Empty(await task); else await Assert.ThrowsAsync<FormatException>(() => task);
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("escaped")]
    [InlineData("nested-registration")]
    [InlineData("invalid-instance")]
    [InlineData("missing-registration")]
    [InlineData("invalid-registration")]
    public async Task Given_UnavailableEarlierEvidence_When_Read_Then_ItIsRejected(string defect)
    {
        Task<IReadOnlySet<string>> task = Read((prefix, _) => Task.FromResult(new BlobListing(
            [defect switch
            {
                "escaped" => "other/earlier/registration.json",
                "nested-registration" => prefix + "earlier/nested/registration.json",
                "invalid-instance" => prefix + "../registration.json",
                _ => prefix + "earlier/registration.json"
            }], 1, defect != "incomplete")), (_, _, _) => Task.FromResult<byte[]?>(
                defect == "missing-registration" ? null : Encoding.UTF8.GetBytes("{}")));
        await Assert.ThrowsAnyAsync<Exception>(() => task);
    }

    [Fact]
    public async Task Given_ValidOrdinaryAndAbruptEarlierRegistrations_When_Read_Then_BothInstanceTypesAreAuthenticated()
    {
        string ordinary = PhasePaths.Registration(Run, "C1", 1, "earlier-timeout");
        byte[] ordinaryBytes = Registration(1, "earlier-timeout");
        var abrupt = new B10AbruptVerifyEvidence();
        JsonObject root = JsonNode.Parse(abrupt.Files["first/registration.json"])!.AsObject();
        root["purpose"] = "scientific";
        JsonNode identity = root["identity"]!; identity["run_id"] = Run; identity["job_name"] = Run + "-c1-r2-loadgen-restart";
        identity["repeat"] = 2; identity["instance_id"] = "earlier-first"; identity["pod_name"] = "earlier-first";
        string abruptName = $"{Run}/C1/r2/b10-abrupt/earlier-first/registration.json";
        var files = new Dictionary<string, byte[]> { [ordinary] = ordinaryBytes, [abruptName] = Encoding.UTF8.GetBytes(root.ToJsonString()) };
        IReadOnlySet<string> result = await Read((prefix, _) => Task.FromResult(new BlobListing(files.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray(), 1, true)),
            (name, _, _) => Task.FromResult<byte[]?>(files[name]));
        Assert.Equal(["earlier-first", "earlier-timeout"], result.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Given_CurrentInstanceEqualsEarlierInstance_When_Read_Then_ItIsRejected()
    {
        string name = PhasePaths.Registration(Run, "C1", 1, "earlier");
        await Assert.ThrowsAsync<FormatException>(() => EarlierRepeatEvidenceReader.ReadAsync(Options,
            new HashSet<string>(StringComparer.Ordinal) { "earlier" }, new InputBudget(Limits),
            (prefix, _) => Task.FromResult(new BlobListing(name.StartsWith(prefix, StringComparison.Ordinal) ? [name] : [], 1, true)),
            (_, _, _) => Task.FromResult<byte[]?>(Registration(1, "earlier")), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public async Task Given_RegistrationByteBoundary_When_Read_Then_ReaderUsesExistingRegistrationCeiling(int length, bool accepted)
    {
        string name = PhasePaths.Registration(Run, "C1", 1, "earlier");
        byte[] seed = Registration(1, "earlier");
        byte[] bytes = Enumerable.Repeat((byte)' ', length).ToArray(); seed.CopyTo(bytes, 0);
        Task<IReadOnlySet<string>> task = Read((prefix, _) => Task.FromResult(new BlobListing(
            name.StartsWith(prefix, StringComparison.Ordinal) ? [name] : [], 1, true)), (_, maximum, _) =>
        {
            Assert.Equal(65536, maximum); return Task.FromResult<byte[]?>(bytes);
        });
        if (accepted) Assert.Equal(["earlier"], await task); else await Assert.ThrowsAsync<FormatException>(() => task);
    }

    [Theory]
    [InlineData(false, "scientific", 1)]
    [InlineData(true, "diagnostic_trial", 3)]
    [InlineData(true, "scientific", 2)]
    public void Given_EnvelopePurposeOrRepeatMismatch_When_Read_Then_AbruptReaderRejectsIt(bool scientific, string purpose, int repeat)
    {
        var evidence = new B10AbruptVerifyEvidence();
        JsonObject root = JsonNode.Parse(evidence.Files["first/registration.json"])!.AsObject();
        root["purpose"] = purpose;
        string runId = scientific ? Run : B10AbruptVerifyEvidence.Run;
        int expectedRepeat = scientific ? 3 : 1;
        root["identity"]!["run_id"] = runId; root["identity"]!["repeat"] = repeat;
        root["identity"]!["job_name"] = $"{runId}-c1-r{expectedRepeat}-loadgen-restart";
        var options = new VerifierHostOptions { RunId = runId, Repeat = expectedRepeat,
            VerifierCommit = B10AbruptVerifyEvidence.Commit, LoadGenImage = B10AbruptVerifyEvidence.Image };
        Assert.Throws<FormatException>(() => AbruptEvidenceCodec.Envelope(Encoding.UTF8.GetBytes(root.ToJsonString()),
            "registration", options, B10AbruptVerifyEvidence.First, null));
    }

    private static Task<IReadOnlySet<string>> Read(Func<string, CancellationToken, Task<BlobListing>> list,
        Func<string, long, CancellationToken, Task<byte[]?>>? read = null) =>
        EarlierRepeatEvidenceReader.ReadAsync(Options, new HashSet<string>(StringComparer.Ordinal), new InputBudget(Limits),
            list, read ?? ((_, _, _) => throw new InvalidOperationException("Unexpected read.")), TestContext.Current.CancellationToken);
    private static byte[] Registration(int repeat, string instance) => PhaseWireCodec.WriteRegistration(new ProducerRegistration(1,
        new RunIdentity("B10", "C1", Run, instance, instance, instance), "C1", repeat,
        PhaseIdentity.JobName(Run, "C1", repeat), instance, "earlier-uid", B10AbruptVerifyEvidence.Commit));
}
#pragma warning restore CA1707
