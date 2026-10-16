using Parchwell.LoadGen.Run.Contracts;
using Xunit;

namespace Parchwell.LoadGen.Tests.Contracts;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class RunIdentityTests
{
    // A runner-recorded worker cannot be replaced by changing a ledger identity.
    [Fact]
    public void Given_UnexpectedInstance_When_Validating_Then_Rejects()
    {
        var identity = new RunIdentity("A01", "C0", "run", "other", "expected", "pod");
        Assert.Throws<InvalidOperationException>(identity.Validate);
    }

    // IDs stay atomic when later rendered as artifact metadata or correlation fields.
    [Theory]
    [InlineData("two words")]
    [InlineData("run\nforged")]
    [InlineData("")]
    public void Given_InvalidIdentity_When_Validating_Then_Rejects(string runId)
    {
        var identity = new RunIdentity("A01", "C0", runId, "instance", "instance", "pod");
        Assert.Throws<ArgumentException>(identity.Validate);
    }
}
