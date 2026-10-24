using System.Text;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Xunit;

namespace Parchwell.Verifier.Tests.Phase;

#pragma warning disable CA1707
public sealed class PhaseWireCodecTests
{
    [Theory]
    [InlineData("probe","probe")]
    [InlineData("C0","C0")]
    [InlineData("C3","C3")]
    public void Given_ValidPhase_When_DerivingRepeatNames_Then_PreservesBlobCaseAndLowercasesJob(string phase,string path)
    {
        Assert.Equal($"synthetic-c5-{phase.ToLowerInvariant()}-r1-loadgen",PhaseIdentity.JobName("synthetic-c5",phase,1));
        Assert.Equal($"synthetic-c5/{path}/r1/",PhaseIdentity.RepeatPrefix("synthetic-c5",phase,1));
        Assert.Equal($"synthetic-c5/{path}/r5/result.json",PhasePaths.Result("synthetic-c5",phase,5));
    }

    [Theory]
    [InlineData("",false)] [InlineData("synthetic-c5",true)] [InlineData("x",true)]
    [InlineData("-bad",false)] [InlineData("bad-",false)] [InlineData("UPPER",false)] [InlineData("123456789012345678901234567890",false)]
    public void Given_RunId_When_Validating_Then_EnforcesDnsSafeBound(string runId,bool valid)=>Assert.Equal(valid,PhaseIdentity.IsRunId(runId));

    [Fact]
    public void Given_ProducerRegistration_When_RoundTripped_Then_ClosedIdentityIsRetained()
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        var value=new ProducerRegistration(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40));
        byte[] bytes=PhaseWireCodec.WriteRegistration(value);
        Assert.Equal(value,PhaseWireCodec.ReadRegistration(bytes));
        byte[] malformed=[..bytes.AsSpan(0,bytes.Length-1).ToArray(),..Encoding.UTF8.GetBytes(",\"extra\":true}")];
        Assert.ThrowsAny<Exception>(()=>PhaseWireCodec.ReadRegistration(malformed));
    }

    [Fact]
    public void Given_ProducerCompletionWithoutSuccessfulCleanup_When_Read_Then_RejectsSuccessClaim()
    {
        var identity=new RunIdentity("A01","C0","synthetic-c5","pod-c5","pod-c5","producer-c5");
        var value=new ProducerCompletionReceipt(1,identity,"C0",1,"synthetic-c5-c0-r1-loadgen","pod-c5","uid-c5",new string('a',40),"loadgen",1,false);
        Assert.Throws<FormatException>(()=>PhaseWireCodec.ReadProducerCompletion(PhaseWireCodec.WriteProducerCompletion(value)));
    }
}
#pragma warning restore CA1707
