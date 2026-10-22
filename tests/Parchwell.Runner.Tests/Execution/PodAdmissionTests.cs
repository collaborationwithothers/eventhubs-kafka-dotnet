using System.Text.Json;
using Parchwell.Runner.Run.Execution;
using Xunit;

namespace Parchwell.Runner.Tests.Execution;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
public sealed class PodAdmissionTests
{
    private static readonly string[] ExpectedResourceVersions = ["rv-2", "rv-3", "rv-4"];
    private static readonly string[] Brokers = ["sasl_ssl://broker.example.invalid:9093/0"];

    [Fact]
    public void Given_NoObservedProducer_When_Releasing_Then_AbsenceDoesNotAuthorizeAdmission()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        Assert.Empty(admission.Events);
        Assert.Throws<InvalidDataException>(() => admission.Producer);
        Assert.Throws<InvalidDataException>(() => admission.Release(Brokers, new string('a', 64)));
    }

    [Fact]
    public void Given_OneProducerHistory_When_Released_Then_ReceiptCarriesIndependentPodIdentityAndExactInputs()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        var producer = new ProducerPod("producer-pod", "uid-1");
        admission.Observe(new("rv-2", "ADDED", producer));
        admission.Observe(new("rv-3", "MODIFIED", producer));
        admission.Observe(new("rv-4", "BOOKMARK", null));
        Assert.Equal(producer, admission.Producer);
        Assert.Equal("initial-rv", admission.InitialResourceVersion);
        using JsonDocument receipt = JsonDocument.Parse(admission.Release(Brokers, new string('a', 64)));
        JsonElement root = receipt.RootElement;
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.Equal("run-1", root.GetProperty("run_id").GetString());
        Assert.Equal(producer.Name, root.GetProperty("pod_name").GetString());
        Assert.Equal(producer.Uid, root.GetProperty("pod_uid").GetString());
        Assert.Equal(Brokers, root.GetProperty("expected_broker_names").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(new string('a', 64), root.GetProperty("generated_values_sha256").GetString());
        Assert.True(root.GetProperty("release").GetBoolean());
        Assert.Equal(ExpectedResourceVersions, admission.Events.Select(item => item.ResourceVersion));
    }

    [Theory]
    [InlineData("ADDED", "second-pod")]
    [InlineData("ADDED", "producer-pod")]
    [InlineData("DELETED", "second-pod")]
    public void Given_SecondUidWithNoProductionEvidence_When_Observed_Then_RepeatCannotBeReleased(string eventType, string secondName)
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        admission.Observe(new("rv-2", "ADDED", new("producer-pod", "uid-1")));
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-3", eventType, new(secondName, "uid-2"))));
        Assert.Equal(2, admission.Events.Count);
        Assert.Throws<InvalidDataException>(() => admission.Producer);
        Assert.Throws<InvalidDataException>(() => admission.Release(Brokers, new string('a', 64)));
    }

    [Fact]
    public void Given_ReleasedProducer_When_SecondUidArrivesDuringEvidenceCollection_Then_EarlierReleaseDoesNotAuthorizeReplacement()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        admission.Observe(new("rv-2", "ADDED", new("producer-pod", "uid-1")));
        using JsonDocument release = JsonDocument.Parse(admission.Release(Brokers, new string('a', 64)));
        Assert.True(release.RootElement.GetProperty("release").GetBoolean());
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-3", "ADDED", new("second-pod", "uid-2"))));
        Assert.Throws<InvalidDataException>(() => admission.Producer);
        Assert.Throws<InvalidDataException>(() => admission.Release(Brokers, new string('a', 64)));
    }

    [Fact]
    public void Given_DeletedOriginalProducer_When_ReplacementArrives_Then_DeletionDoesNotEraseOriginalUid()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        var first = new ProducerPod("producer-pod", "uid-1");
        admission.Observe(new("rv-2", "ADDED", first));
        admission.Observe(new("rv-3", "DELETED", first));
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-4", "ADDED", new("producer-pod", "uid-2"))));
        Assert.Equal(3, admission.Events.Count);
        Assert.Throws<InvalidDataException>(() => admission.Producer);
    }

    [Theory]
    [InlineData("ERROR", "rv-3")]
    [InlineData("LOST", "rv-3")]
    [InlineData("BOOKMARK", "")]
    public void Given_LostOrExpiredWatch_When_LaterSnapshotShowsOneProducer_Then_UniquenessCannotBeRecovered(string eventType, string resourceVersion)
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        var producer = new ProducerPod("producer-pod", "uid-1");
        admission.Observe(new("rv-2", "ADDED", producer));
        Assert.Throws<InvalidDataException>(() => admission.Observe(new(resourceVersion, eventType, null)));
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-4", "ADDED", producer)));
        Assert.Throws<InvalidDataException>(() => admission.Release(Brokers, new string('a', 64)));
    }

    [Fact]
    public void Given_ExistingUid_When_ObservedWithDifferentName_Then_IdentityMutationIsRejected()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        admission.Observe(new("rv-2", "ADDED", new("producer-pod", "uid-1")));
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-3", "MODIFIED", new("other-pod", "uid-1"))));
    }

    [Fact]
    public void Given_MaximumWatchHistory_When_OneMoreEventArrives_Then_StorageRemainsBounded()
    {
        var admission = new PodAdmission("run-1", "initial-rv");
        var producer = new ProducerPod("producer-pod", "uid-1");
        for (int index = 0; index < 10000; index++)
            admission.Observe(new("rv-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), "MODIFIED", producer));
        Assert.Equal(10000, admission.Events.Count);
        Assert.Equal(producer, admission.Producer);
        Assert.Throws<InvalidDataException>(() => admission.Observe(new("rv-overflow", "MODIFIED", producer)));
        Assert.Equal(10000, admission.Events.Count);
    }
}
#pragma warning restore CA1707
