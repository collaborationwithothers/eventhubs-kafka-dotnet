using System.Text.Json;
using Parchwell.Verifier.Common.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed class ConsumerRegistrationPathTests
{
    [Theory]
    [InlineData("", false, false)]
    [InlineData("/", false, false)]
    [InlineData("s/", true, false)]
    [InlineData("ss/", true, false)]
    [InlineData("", false, true)]
    [InlineData("/", false, true)]
    public async Task Given_MemberPathBoundary_U3_When_RegistrationIsRead_Then_EmptySegmentIsInvalidWithoutAborting(string memberPath, bool valid, bool emptyIdentity)
    {
        var wire = new ConsumerWireFixture();
        wire.Blobs.Remove(ConsumerWireFixture.Name("subject", "registration.json"));
        var registration = ConsumerWireFixture.Registration("subject");
        string member = memberPath.TrimEnd('/');
        if (emptyIdentity || member.Length > 0)
        {
            registration["pod_name"] = member;
            registration["instance_id"] = member;
            registration["effective_settings"]!["client.id"] = member;
        }
        wire.Blobs[ConsumerWireFixture.Prefix + "consumer/" + memberPath + "registration.json"] = JsonSerializer.SerializeToUtf8Bytes(registration);

        ConsumerArtifacts artifacts = await wire.Load(completions: false);

        Assert.False(artifacts.Unavailable);
        Assert.Equal(2, artifacts.RegistrationCount);
        ConsumerMember subject = Assert.Single(artifacts.Members, entry => entry.Registration?.Role == "subject");
        Assert.Equal(!valid, subject.RegistrationInvalid);
        Assert.Equal(valid ? 0 : 1, artifacts.Diagnostics.Count(diagnostic => diagnostic.Reason == "consumer_registration_invalid"));
    }
}
#pragma warning restore CA1707
