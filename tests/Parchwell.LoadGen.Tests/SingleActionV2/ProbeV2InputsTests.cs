using Confluent.Kafka;
using Parchwell.LoadGen.SingleActionV2;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleActionV2;

#pragma warning disable CA1707
public sealed partial class ProbeV2InputsTests
{
    [Fact]
    public async Task Given_FiveTypedErrorsWithoutDelivery_When_ReadHelperAdmits_Then_AllSlotsNeedNoClient()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([false, false, false, false, false]);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, repeat, TestContext.Current.CancellationToken);
            Assert.Equal("not_applicable_no_delivery", admission.Admission);
            Assert.Null(admission.Requested);
        }
    }
    [Fact]
    public async Task Given_MixedSuccessAndError_When_ReadHelperAdmits_Then_OnlyDeliveredSlotsConstructClients()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, false, true, false, true]);
        for (int repeat = 1; repeat <= 5; repeat++)
        {
            var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, repeat, TestContext.Current.CancellationToken);
            Assert.Equal(repeat % 2 == 1 ? "required" : "not_applicable_no_delivery", admission.Admission);
            if (repeat % 2 == 1) Assert.Equal(repeat + 20, admission.Requested!.Offset);
        }
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("failed_receipt")]
    [InlineData("wrong_slot")]
    [InlineData("success_without_location")]
    [InlineData("wrong_key")]
    [InlineData("wrong_account")]
    public async Task Given_InvalidDependency_When_ReadHelperAdmits_Then_ConstructsNoClient(string fault)
    {
        using var fixture = new Fixture();
        if (fault != "missing") await fixture.WriteAsync([true, true, true, true, true], fault);
        var admission = await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken);
        Assert.Equal("not_attempted_dependency", admission.Admission);
        Assert.Null(admission.Requested);
    }
    [Fact]
    public async Task Given_ErrorWithLaterAcknowledgedLocation_When_ReadHelperAdmits_Then_ReadbackIsRequired()
    {
        using var fixture = new Fixture();
        await fixture.WriteAsync([true, true, true, true, true], "late_delivery");
        Assert.Equal("required", (await fixture.Inputs.AdmitAsync(fixture.Stage, 1, TestContext.Current.CancellationToken)).Admission);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "probe-v2-inputs-" + Guid.NewGuid().ToString("N"));
        internal string CapturePath => Path.Combine(directory, ProbeV2Options.Job(options.RunId, options.ScenarioId, options.ConfigurationId, "action"), "capture.json");
        internal ProbeV2Inputs Inputs { get; }
        internal ProbeV2Stage Stage { get; } = ProbeV2Stage.For("F06", "C2", "read");
        private readonly ProbeV2Options options = ProbeV2OptionsTests.Options("F06", "C2", "read");
        internal Fixture(int? maximum = null)
        {
            options.InputRoot = directory;
            if (maximum is { } bound) options.Limits.MaxCaptureBytes = bound;
            Inputs = new(options, new TestClock());
        }
        internal async Task WriteAsync(bool[] delivered, string? fault = null)
        {
            ProbeV2Options source = ProbeV2OptionsTests.Options("F06", "C2", "action");
            ProbeV2Stage stage = ProbeV2Stage.For("F06", "C2", "action");
            ProbeV2Registration registration = ProbeV2Wire.Registration(source);
            var repeats = Enumerable.Range(1, 5).Select(repeat =>
            {
                bool success = delivered[repeat - 1];
                string? result = success && fault != "late_delivery" ? "success" : "error";
                var error = result == "success" ? null : new ProbeV2Error((int)ErrorCode.TopicAuthorizationFailed, false, "broker", "synthetic", 1, 1);
                var request = new ProbeV2Requested(stage.Topic, "explicit", 0, null,
                    Convert.ToBase64String(stage.Key(source, repeat)), Convert.ToBase64String(stage.Value(source, repeat)!));
                if (fault == "wrong_key" && repeat == 1) request = request with { KeyBase64 = "eA==" };
                return new ProbeV2Repeat(fault == "wrong_slot" && repeat == 1 ? 2 : repeat, "required", source.PodName + "-r" + repeat, "produce_one", true, 1, 1,
                    new Dictionary<string, string>(), [], null, request,
                    success && fault != "success_without_location" ? new ProbeV2Delivery(stage.Topic, 0, repeat + 20, "Persisted", null, 1, 2) : null,
                    new(0, 0, null), result, error, [], [], []);
            }).ToArray();
            var capture = new ProbeV2CaptureFile(2, "single-action-v2", true, source.RunId, source.ScenarioId, source.ConfigurationId,
                source.Stage, source.SourceCommit, source.JobName, source.PodName, source.PodUid, source.ServiceAccount, "2.16.0", "2.16.0", repeats,
                fault == "failed_receipt" ? 1 : 0, true, true);
            var pod = new ProbeV2Pod(2, source.JobName, source.PodName, source.PodUid, fault == "wrong_account" ? "probe" : source.ServiceAccount,
                "Job", source.JobName, "sha256:" + new string('a', 64));
            string target = Path.Combine(directory, source.JobName);
            Directory.CreateDirectory(target);
            await File.WriteAllBytesAsync(Path.Combine(target, "registration.json"), ProbeV2Wire.Encode(registration, 65536), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(target, "capture.json"), ProbeV2Wire.Encode(capture, 524288), TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(target, "pod.json"), ProbeV2Wire.Encode(pod, 65536), TestContext.Current.CancellationToken);
        }
        public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
