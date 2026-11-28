// This file sits under Component/ only so the default mutation-only build drops it (the csproj removes Component/**/*.cs).
// The tests still run in the normal build and in the full-suite mutation build. Why: docs/operations/verifier-mutation.md, section "Issue 602 fast-suite equivalence check".
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Window;
using Xunit;

namespace Parchwell.Verifier.Tests.Window;

#pragma warning disable CA1707

public sealed partial class ConsumedRecordBudgetTests
{
    [Fact]
    public void Given_DeployedPhaseRecordLimit_When_AccountingEmptyRecords_Then_EqualityPassesAndNextRecordCannotAdvanceCounters()
    {
        (long recordLimit,long byteLimit)=PhaseCapacityLimits();
        Assert.Equal(9300000,recordLimit);
        Assert.Equal(429496729600,byteLimit);
        var budget=new ConsumedRecordBudget(recordLimit,byteLimit);
        ConsumeResult<byte[],byte[]> record=Record();

        for(long index=0;index<recordLimit;index++)budget.Account(record);

        Assert.Equal(recordLimit,budget.Records);
        Assert.Equal(0,budget.Bytes);
        InputLimitExceededException exception=Assert.Throws<InputLimitExceededException>(()=>budget.Account(record));
        Assert.Equal("The consumed window exceeds its configured record or byte limit.",exception.Message);
        Assert.Equal(recordLimit,budget.Records);
        Assert.Equal(0,budget.Bytes);
    }
}
