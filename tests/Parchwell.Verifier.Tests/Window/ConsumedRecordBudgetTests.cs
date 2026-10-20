using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

public sealed class ConsumedRecordBudgetTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void Given_NonpositiveLimit_When_Constructing_Then_Rejects(long records, long bytes) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsumedRecordBudget(records, bytes));

    [Fact]
    public void Given_RecordLimitTwo_When_Accounting_Then_ThirdIsRejectedBeforeCounterAdvances()
    {
        var budget = new ConsumedRecordBudget(2, 100);
        budget.Account(Record());
        Assert.Equal(1, budget.Records);
        budget.Account(Record());
        Assert.Equal(2, budget.Records);
        InputLimitExceededException exception = Assert.Throws<InputLimitExceededException>(() => budget.Account(Record()));
        Assert.Equal("The consumed window exceeds its configured record or byte limit.", exception.Message);
        Assert.Equal(2, budget.Records);
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void Given_ByteLimitEight_When_Accounting_Then_EqualityIsAllowed(int bytes, bool rejects)
    {
        var budget = new ConsumedRecordBudget(2, 8);
        var record = Record();
        record.Message.Value = new byte[bytes];
        if (rejects)
        {
            InputLimitExceededException exception = Assert.Throws<InputLimitExceededException>(() => budget.Account(record));
            Assert.Equal("The consumed window exceeds its configured record or byte limit.", exception.Message);
        }
        else { budget.Account(record); Assert.Equal(bytes, budget.Bytes); }
    }

    [Fact]
    public void Given_MultibyteAndUnknownHeaders_When_Accounting_Then_AllRawBytesCount()
    {
        var record = Record();
        record.Message.Key = [1, 2];
        record.Message.Value = [3, 4, 5];
        record.Message.Headers.Add("é", [6]);
        var budget = new ConsumedRecordBudget(2, 8);
        budget.Account(record);
        Assert.Equal(8, budget.Bytes);
        record.Message.Headers.Add("x", null);
        Assert.Throws<InputLimitExceededException>(() => new ConsumedRecordBudget(1, 8).Account(record));
    }

    [Fact]
    public void Given_DuplicateUnknownHeaders_When_Accounting_Then_EachOccurrenceCounts()
    {
        var record = Record();
        record.Message.Headers.Add("x", [1]);
        record.Message.Headers.Add("x", [2]);
        var budget = new ConsumedRecordBudget(1, 4);
        budget.Account(record);
        Assert.Equal(4, budget.Bytes);
    }

    [Fact]
    public void Given_EmptyAndNullBytes_When_Accounting_Then_TheyAddZero()
    {
        var budget = new ConsumedRecordBudget(2, 1);
        var record = Record();
        budget.Account(record);
        record.Message.Key = null!;
        record.Message.Value = null!;
        record.Message.Headers = null;
        budget.Account(record);
        Assert.Equal(0, budget.Bytes);
        Assert.Equal(2, budget.Records);
    }

    [Fact]
    public void Given_CumulativeByteLimit_When_Accounting_Then_PollsDoNotResetCounter()
    {
        var budget = new ConsumedRecordBudget(3, 8);
        var record = Record();
        record.Message.Value = new byte[4];
        budget.Account(record);
        budget.Account(record);
        Assert.Throws<InputLimitExceededException>(() => budget.Account(record));
        Assert.Equal(8, budget.Bytes);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Given_NearInt64Maximum_When_Adding_Then_OverflowIsExecutionFailure(long amount, bool overflow)
    {
        if (overflow)
        {
            InputLimitExceededException exception = Assert.Throws<InputLimitExceededException>(() => ConsumedRecordBudget.Add(long.MaxValue - 1, amount));
            Assert.Equal("Consumed-window accounting overflowed Int64.", exception.Message);
        }
        else Assert.Equal(long.MaxValue - 1 + amount, ConsumedRecordBudget.Add(long.MaxValue - 1, amount));
    }

    private static ConsumeResult<byte[], byte[]> Record() => new()
    {
        Topic = "document-events", Partition = new Partition(0), Offset = new Offset(0),
        Message = new() { Key = [], Value = [], Headers = new Headers() }
    };
}
