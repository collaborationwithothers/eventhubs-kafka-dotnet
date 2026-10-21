using System.Text;
using Confluent.Kafka;
using Parchwell.Verifier.VerifyRun.Window;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.Tests.Support;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

public sealed class MessageIdentityReaderTests
{
    [Fact]
    public void Given_MatchingWriterIdentity_When_ReadingAndJoining_Then_CheckSixPasses()
    {
        var record = Record();
        record.Message.Key = Encoding.UTF8.GetBytes("k");
        record.Message.Headers.Remove("instance-id");
        record.Message.Headers.Remove("attempt-id");
        record.Message.Headers.Add("instance-id", Encoding.UTF8.GetBytes("instance-1"));
        record.Message.Headers.Add("attempt-id", Encoding.UTF8.GetBytes("a1"));
        var request = RecordedEvidence.SyntheticRequest() with { Records = [MessageIdentityReader.Read(record)] };
        Assert.Equal(AssessmentDisposition.Pass,
            BookkeepingChecks.Evaluate(request).Single(r => r.Assessment.CheckNumber == 6).Assessment.Disposition);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1", 1L)]
    [InlineData("-1", -1L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void Given_CanonicalInt64_When_Reading_Then_ValueIsPreserved(string text, long expected)
    {
        var result = MessageIdentityReader.Read(Record(text));
        Assert.Equal(expected, result.Sequence);
        Assert.Empty(result.Problems);
        Assert.Equal("key", result.Key);
        Assert.Equal("instance", result.InstanceId);
        Assert.Equal("attempt", result.AttemptId);
        Assert.Equal("document-events", result.Topic);
        Assert.Equal(2, result.Partition);
        Assert.Equal(5, result.Offset);
    }

    [Theory]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("01")]
    [InlineData("-01")]
    [InlineData("-0")]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public void Given_NoncanonicalSequence_When_Reading_Then_CheckSixOwnsProblem(string text)
    {
        var result = MessageIdentityReader.Read(Record(text));
        Assert.Null(result.Sequence);
        Assert.Contains(result.Problems, p => p.CheckNumber == 6 && p.Field == "sequence");
        Assert.Equal([new StructuralProblem(6, "noncanonical_sequence", "consumed", null, "sequence", null)], result.Problems);
    }

    [Theory]
    [InlineData("instance-id", "missing")]
    [InlineData("instance-id", "null")]
    [InlineData("instance-id", "duplicate")]
    [InlineData("instance-id", "utf8")]
    [InlineData("attempt-id", "missing")]
    [InlineData("attempt-id", "null")]
    [InlineData("attempt-id", "duplicate")]
    [InlineData("attempt-id", "utf8")]
    [InlineData("sequence", "missing")]
    [InlineData("sequence", "null")]
    [InlineData("sequence", "duplicate")]
    [InlineData("sequence", "utf8")]
    public void Given_BrokenIdentityHeader_When_Reading_Then_CheckSixOwnsProblem(string field, string mode)
    {
        var record = Record();
        if (mode != "duplicate") record.Message.Headers.Remove(field);
        if (mode == "duplicate") record.Message.Headers.Add(field, Encoding.UTF8.GetBytes("1"));
        if (mode == "null") record.Message.Headers.Add(field, null);
        if (mode == "utf8") record.Message.Headers.Add(field, [0xff]);
        Assert.Contains(MessageIdentityReader.Read(record).Problems, p => p.CheckNumber == 6 && p.Field == field);
        string code = mode switch
        {
            "missing" => "missing_header",
            "duplicate" => "duplicate_header",
            "null" => "null_identity_bytes",
            _ => "invalid_utf8"
        };
        Assert.Equal([new StructuralProblem(6, code, "consumed", null, field, null)], MessageIdentityReader.Read(record).Problems);
        if (mode == "utf8")
        {
            var result = MessageIdentityReader.Read(record);
            switch (field)
            {
                case "instance-id": Assert.Null(result.InstanceId); break;
                case "attempt-id": Assert.Null(result.AttemptId); break;
                case "sequence": Assert.Null(result.Sequence); break;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Given_NullOrInvalidKey_When_Reading_Then_CheckSixOwnsProblem(bool invalid)
    {
        var record = Record();
        record.Message.Key = invalid ? [0xff] : null!;
        Assert.Contains(MessageIdentityReader.Read(record).Problems, p => p.CheckNumber == 6 && p.Field == "key");
        Assert.Equal([new StructuralProblem(6, invalid ? "invalid_utf8" : "null_identity_bytes", "consumed", null, "key", null)],
            MessageIdentityReader.Read(record).Problems);
        Assert.Null(MessageIdentityReader.Read(record).Key);
    }

    [Fact]
    public void Given_EmptyKeyAndHeaderValues_When_Reading_Then_OnlyEmptySequenceIsMalformed()
    {
        var record = Record("");
        record.Message.Key = [];
        record.Message.Headers.Remove("instance-id");
        record.Message.Headers.Remove("attempt-id");
        record.Message.Headers.Add("instance-id", []);
        record.Message.Headers.Add("attempt-id", []);
        var result = MessageIdentityReader.Read(record);
        Assert.Equal("", result.Key);
        Assert.Equal("", result.InstanceId);
        Assert.Equal("", result.AttemptId);
        Assert.Single(result.Problems);
    }

    [Fact]
    public void Given_NullHeaders_When_Reading_Then_AllIdentityHeadersAreMissing()
    {
        var record = Record();
        record.Message.Headers = null;
        Assert.Equal(3, MessageIdentityReader.Read(record).Problems.Count);
    }

    [Fact]
    public void Given_EmptyHeaders_When_Reading_Then_AllIdentityHeadersAreMissing()
    {
        var record = Record();
        record.Message.Headers = new Headers();
        Assert.Equal(3, MessageIdentityReader.Read(record).Problems.Count);
    }

    [Fact]
    public void Given_NullMessage_When_Reading_Then_KeyAndIdentityHeadersAreMissing()
    {
        var record = Record();
        record.Message = null!;
        Assert.Equal(4, MessageIdentityReader.Read(record).Problems.Count);
    }

    internal static ConsumeResult<byte[], byte[]> Record(string sequence = "1", long offset = 5)
    {
        var headers = new Headers();
        headers.Add("instance-id", Encoding.UTF8.GetBytes("instance"));
        headers.Add("attempt-id", Encoding.UTF8.GetBytes("attempt"));
        headers.Add("sequence", Encoding.UTF8.GetBytes(sequence));
        return new() { Topic = "document-events", Partition = new Partition(2), Offset = new Offset(offset),
            Message = new() { Key = Encoding.UTF8.GetBytes("key"), Value = [], Headers = headers } };
    }
}
