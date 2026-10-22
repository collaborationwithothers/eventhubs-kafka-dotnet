#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Evidence;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

// Valid synthetic JSON with trailing whitespace isolates the candidate input-byte ceiling.
public sealed class PublicCandidateBoundaryTests
{
    [Theory]
    [InlineData(1_048_575, true)]
    [InlineData(1_048_576, true)]
    [InlineData(1_048_577, false)]
    public void Given_InputByteBoundary_When_CandidateCreated_Then_AcceptsOnlyWithinLimit(int size, bool allowed)
    {
        byte[] input = new byte[size];
        input.AsSpan().Fill((byte)' ');
        Encoding.UTF8.GetBytes("{\"run_id\":\"synthetic-boundary\"}").CopyTo(input, 0);
        const string subscription = "00000000-0000-0000-0000-000000000001";
        if (!allowed)
        {
            var exception = Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, subscription));
            Assert.Equal("candidate_size_exceeded", exception.Message);
            return;
        }
        using var candidate = JsonDocument.Parse(PublicCandidate.Create(input, subscription));
        Assert.True(candidate.RootElement.GetProperty("synthetic").GetBoolean());
        Assert.Equal("synthetic-boundary", candidate.RootElement.GetProperty("run_id").GetString());
    }
}
