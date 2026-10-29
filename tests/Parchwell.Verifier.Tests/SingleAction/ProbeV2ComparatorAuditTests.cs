using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2ComparatorAuditTests
{
    [Theory]
    [InlineData("C0",-1)][InlineData("C0",0)][InlineData("C0",1)]
    [InlineData("C1",-1)][InlineData("C1",0)][InlineData("C1",1)]
    public void Given_CorrelatedE01TargetOffsetAroundSeed_When_Judged_Then_OnlyStrictIncreaseIsValid(string configuration,int delta)
    {
        var options = ProbeV2Fixture.Options("E01",configuration); var evidence = ProbeV2Fixture.Evidence(options);
        var action = evidence[(configuration,"action")]; var read = evidence[(configuration,"read")];
        long Offset(int ordinal) => evidence[(configuration,"seed")].Capture!.Repeats[ordinal - 1].Delivery!.Offset + delta;
        evidence[(configuration,"action")] = action with { Capture = action.Capture! with
        { Repeats = action.Capture.Repeats.Select(row => row with { Delivery = row.Delivery! with { Offset = Offset(row.Repeat) } }).ToArray() } };
        evidence[(configuration,"read")] = read with { Capture = read.Capture! with
        { Repeats = read.Capture.Repeats.Select(row => row with { Requested = row.Requested! with { Offset = Offset(row.Repeat) },
            ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { Offset = Offset(row.Repeat) } } }).ToArray() } };
        var slots = ProbeV2Fixture.Check(options,evidence);
        Assert.All(slots.Values.SelectMany(rows => rows),row => Assert.Equal("complete_valid",row.Status));
        var prior = new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal);
        if (configuration == "C1") prior.Add("C0",ProbeV2Fixture.Candidate("E01"));
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,slots,ProbeV2Fixture.Scope(options),prior);
        Assert.Equal(delta > 0 ? "complete_valid" : "invalid",result.Status);
        Assert.All(result.Repeats,row => Assert.Equal(delta > 0 ? Array.Empty<string>() : ["v2_seed"],row.Reasons));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_NativeSecondsAroundMultiplicationLimit_When_Checked_Then_OverflowIsUnusableEvidence(int delta)
    {
        var (options,evidence,row) = Fixture(); long seconds = long.MaxValue / 1000 + delta;
        row = row with { Statistics = [ProbeV2Fixture.Statistic(row.ClientId!,row.Action,seconds)] };
        var result = Judge(options,evidence,row);
        Assert.Equal(delta > 0 ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(delta > 0 ? ["v2_statistics"] : Array.Empty<string>(),result.Reasons);
        if (delta > 0) Assert.Null(result.StatisticsReference);
        else Assert.Equal(seconds,result.StatisticsReference!.NativeTime);
    }

    [Theory]
    [InlineData("action",329999)][InlineData("action",330000)][InlineData("action",330001)]
    [InlineData("read",119999)][InlineData("read",120000)][InlineData("read",120001)]
    public void Given_CoherentSuccessfulTerminalAtActionWindow_When_Checked_Then_InclusiveDeadlineIsEnforced(string stage,long elapsed)
    {
        var (options,evidence,row) = Fixture(stage);
        row = row with { ElapsedMs = elapsed,Statistics = [row.Statistics[0] with { ObservedElapsedMs = elapsed + 1 }] };
        row = stage == "action" ? row with { Delivery = row.Delivery! with { ObservedElapsedMs = elapsed } } :
            row with { ReadObservation = row.ReadObservation with { Record = row.ReadObservation.Record! with { ObservedElapsedMs = elapsed } } };
        bool exceeded = elapsed > (stage == "action" ? 330000 : 120000);
        var result = Judge(options,evidence,row);
        Assert.Equal(exceeded ? "invalid" : "complete_valid",result.Status);
        Assert.Equal(exceeded ? ["v2_terminal","v2_statistics"] : Array.Empty<string>(),result.Reasons);
    }

    [Theory]
    [InlineData("action",329999)][InlineData("action",330000)][InlineData("action",330001)]
    [InlineData("read",119999)][InlineData("read",120000)][InlineData("read",120001)]
    public void Given_CoherentFatalErrorAtActionWindow_When_Checked_Then_LateErrorCannotBecomeEarlierTerminal(string stage,long elapsed)
    {
        var (options,evidence,row) = Fixture(stage); var error = new ProbeV2Error(-150,true,"local","synthetic",elapsed,1);
        row = row with { ElapsedMs = elapsed,Result = "error",Error = error,ClientErrors = [error],Delivery = null,
            ReadObservation = new(0,0,null),Statistics = [row.Statistics[0] with { ObservedElapsedMs = elapsed + 1 }] };
        bool exceeded = elapsed > (stage == "action" ? 330000 : 120000);
        var result = Judge(options,evidence,row);
        Assert.Equal(exceeded ? "invalid" : "complete_valid",result.Status);
        Assert.Equal(exceeded ? ["v2_terminal","v2_statistics"] : Array.Empty<string>(),result.Reasons);
    }

    public static TheoryData<string,string,int> ObservationBoundaries
    {
        get
        {
            var data = new TheoryData<string,string,int>();
            foreach (string stage in new[] { "action","read" })
                foreach (string member in new[] { "partition","offset","observed","sequence" })
                    foreach (int value in new[] { -1,0,1 }) data.Add(stage,member,value);
            return data;
        }
    }
    [Theory,MemberData(nameof(ObservationBoundaries))]
    public void Given_DeliveryOrRecordNumericFieldAtZero_When_Checked_Then_OnlyNegativeIsMalformed(string stage,string field,int value)
    {
        var (options,evidence,row) = Fixture(stage);
        if (stage == "action")
        {
            var delivery = row.Delivery!;
            delivery = field switch { "partition" => delivery with { Partition = value },"offset" => delivery with { Offset = value },
                "observed" => delivery with { ObservedElapsedMs = value },_ => delivery with { Sequence = value } };
            row = row with { Delivery = delivery };
        }
        else
        {
            var record = row.ReadObservation.Record!;
            record = field switch { "partition" => record with { Partition = value },"offset" => record with { Offset = value },
                "observed" => record with { ObservedElapsedMs = value },_ => record with { Sequence = value } };
            row = row with { ReadObservation = row.ReadObservation with { Record = record } };
        }
        if (field == "observed" && value >= 0) row = row with { ElapsedMs = value };
        var result = Judge(options,evidence,row);
        Assert.Equal(value < 0 ? "invalid" : "complete_valid",result.Status);
        Assert.Equal(value < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
        if (stage == "action" && field is "partition" or "offset") Assert.Equal(value >= 0,SingleActionRepeatJudgement.V2Delivered(row));
    }

    [Theory]
    [InlineData("elapsed",-1)][InlineData("elapsed",0)][InlineData("elapsed",1)]
    [InlineData("unix",-1)][InlineData("unix",0)][InlineData("unix",1)]
    public void Given_RepeatTimesAtZero_When_Checked_Then_NegativeTimesRemainMalformed(string field,long value)
    {
        var (options,evidence,row) = Fixture();
        row = field == "elapsed" ? row with { Terminal = false,Result = null,ElapsedMs = value } : row with { TerminalUnixMs = value };
        var result = Judge(options,evidence,row);
        Assert.Equal(value < 0 ? "invalid" : field == "elapsed" ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(value < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("code",-1)][InlineData("code",0)][InlineData("code",1)]
    [InlineData("observed",-1)][InlineData("observed",0)][InlineData("observed",1)]
    [InlineData("sequence",-1)][InlineData("sequence",0)][InlineData("sequence",1)]
    public void Given_CallbackErrorNumbersAtZero_When_Checked_Then_CodeMustBeNonzeroAndTimingNonnegative(string field,int value)
    {
        var (options,evidence,row) = Fixture(); var error = new ProbeV2Error(-1,false,"local","",0,0);
        error = field switch { "code" => error with { Code = value },"observed" => error with { ObservedElapsedMs = value },_ => error with { Sequence = value } };
        var result = Judge(options,evidence,row with { ClientErrors = [error] }); bool valid = field == "code" ? value != 0 : value >= 0;
        Assert.Equal(valid ? "complete_valid" : "invalid",result.Status); Assert.Equal(valid ? Array.Empty<string>() : ["v2_wire"],result.Reasons);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void Given_RepeatOrdinalAtBothEnds_When_Checked_Then_ExactlyOneThroughFiveAreAdmitted(int ordinal)
    {
        var (options,evidence,_) = Fixture(); var row = ProbeV2Fixture.Row(options,"C0","action",ordinal);
        var result = Judge(options,evidence,row); bool valid = ordinal is >= 1 and <= 5;
        Assert.Equal(valid ? "complete_valid" : "invalid",result.Status); Assert.Equal(valid ? Array.Empty<string>() : ["v2_wire"],result.Reasons);
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_E01RequestedPartitionAroundZero_When_Checked_Then_OnlyDeclaredPartitionIsFaithful(int partition)
    {
        var options = ProbeV2Fixture.Options("E01"); var evidence = ProbeV2Fixture.Evidence(options,"C0","action"); var row = evidence.Capture!.Repeats[0];
        var result = Judge(options,evidence,row with { Requested = row.Requested! with { Partition = partition } });
        Assert.Equal(partition == 0 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(partition != 0,result.Reasons.Contains("v2_configuration",StringComparer.Ordinal));
        Assert.Equal(partition < 0,result.Reasons.Contains("v2_wire",StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_ReadLocationOffsetAroundDelivery_When_Judged_Then_ExactBindingIsRequired(int delta)
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options); var read = evidence[("C0","read")];
        evidence[("C0","read")] = read with { Capture = read.Capture! with { Repeats = read.Capture.Repeats.Select(row => row with
        { Requested = row.Requested! with { Offset = row.Requested.Offset + delta },ReadObservation = row.ReadObservation with
        { Record = row.ReadObservation.Record! with { Offset = row.ReadObservation.Record.Offset + delta } } }).ToArray() } };
        var result = SingleActionConfigurationJudgement.V2Candidate(options,evidence,ProbeV2Fixture.Check(options,evidence),ProbeV2Fixture.Scope(options),new Dictionary<string,ProbeV2Candidate>(StringComparer.Ordinal));
        Assert.Equal(delta == 0 ? "complete_valid" : "invalid",result.Status);
        Assert.All(result.Repeats,row => Assert.Equal(delta == 0 ? Array.Empty<string>() : ["v2_location"],row.Reasons));
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void Given_ImageMapCount_When_Checked_Then_ExactlyTwoImageRolesAreRequired(int count)
    {
        var expected = ProbeV2Fixture.Options().ImageDigests(); var actual = expected.Take(count).ToDictionary(x => x.Key,x => x.Value,StringComparer.Ordinal);
        if (count == 3) actual.Add("extra",expected["loadgen"]);
        Assert.Equal(count == 2,SingleActionConfigurationJudgement.V2Images(actual,expected));
    }

    [Theory]
    [InlineData(0)][InlineData(70)][InlineData(71)][InlineData(72)]
    public void Given_ImageDigestLength_When_Checked_Then_ExactWireLengthIsRequired(int length)
    {
        string digest = length == 0 ? "" : "sha256:" + new string('a',length - 7);
        var map = new Dictionary<string,string>(StringComparer.Ordinal) { ["loadgen"] = digest,["verifier"] = digest };
        Assert.Equal(length == 71,SingleActionConfigurationJudgement.V2Images(map,map));
    }

    [Theory]
    [InlineData("registration",1)][InlineData("registration",2)][InlineData("registration",3)]
    [InlineData("capture",1)][InlineData("capture",2)][InlineData("capture",3)]
    [InlineData("pod",1)][InlineData("pod",2)][InlineData("pod",3)]
    public void Given_ActorSchemaVersion_When_Checked_Then_OnlyVersionTwoBinds(string file,int version)
    {
        var (options,evidence,_) = Fixture();
        evidence = file switch { "registration" => evidence with { Registration = evidence.Registration! with { SchemaVersion = version } },
            "capture" => evidence with { Capture = evidence.Capture! with { SchemaVersion = version } },_ => evidence with { Pod = evidence.Pod! with { SchemaVersion = version } } };
        Assert.Equal(version == 2 ? Array.Empty<string>() : ["v2_identity"],SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_ActorExitCode_When_Checked_Then_OnlyZeroCompletes(int code)
    {
        var (options,evidence,_) = Fixture();
        Assert.Equal(code == 0 ? Array.Empty<string>() : ["v2_completion"],SingleActionConfigurationJudgement.V2EvidenceProblems(options,evidence with { Capture = evidence.Capture! with { HandlerExitCode = code } }));
    }

    [Theory]
    [InlineData("candidate_schema",1)][InlineData("candidate_schema",2)][InlineData("candidate_schema",3)]
    [InlineData("receipt_schema",1)][InlineData("receipt_schema",2)][InlineData("receipt_schema",3)]
    [InlineData("receipt_exit",-1)][InlineData("receipt_exit",0)][InlineData("receipt_exit",1)]
    [InlineData("repeats",0)][InlineData("repeats",4)][InlineData("repeats",5)][InlineData("repeats",6)]
    public void Given_PublishedNumericBoundary_When_Checked_Then_OnlyExactSchemaCompletionAndFiveSlotsBind(string field,int value)
    {
        var options = ProbeV2Fixture.Options(); var candidate = ProbeV2Fixture.Candidate();
        if (field == "candidate_schema") candidate = candidate with { SchemaVersion = value };
        if (field == "repeats") candidate = candidate with { Repeats = Enumerable.Range(0,value).Select(i => candidate.Repeats[i % 5] with { Repeat = i + 1 }).ToArray() };
        byte[] raw = ProbeV2Wire.Write(candidate,16777216); string prefix = ProbeV2Matrix.Prefix("run1","B08","C0");
        var receipt = new ProbeV2JudgeReceipt(field == "receipt_schema" ? value : 2,"single-action-v2-judge","run1","B08","C0",ProbeV2Fixture.Source,
            "run1-b08-c0-judge","run1-b08-c0-judge-abc","judge-uid",prefix + "candidate.json",Convert.ToHexStringLower(SHA256.HashData(raw)),field == "receipt_exit" ? value : 0,true);
        int expected = field == "repeats" ? 5 : field == "receipt_exit" ? 0 : 2;
        Assert.Equal(value == expected,SingleActionConfigurationJudgement.V2Published(options,"C0",candidate,receipt,raw));
    }

    [Theory]
    [InlineData("accounts",0)][InlineData("accounts",3)][InlineData("accounts",4)][InlineData("accounts",5)]
    [InlineData("topics",0)][InlineData("topics",2)][InlineData("topics",3)][InlineData("topics",4)]
    [InlineData("assignments",0)][InlineData("assignments",13)][InlineData("assignments",14)][InlineData("assignments",15)]
    [InlineData("confirmation",0)][InlineData("confirmation",1)][InlineData("confirmation",2)][InlineData("confirmation",3)]
    public void Given_PermissionScopeCollectionCount_When_Checked_Then_OnlyExactRoleInventoryIsAdmitted(string field,int count)
    {
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options);
        int expected = field switch { "accounts" => 4,"topics" => 3,"assignments" => 14,_ => 2 };
        if (field == "accounts") Resize(scope.Deployed.Accounts,count);
        if (field == "topics") Resize(scope.Deployed.Topics,count);
        if (field == "assignments") Resize(scope.Deployed.Assignments,count);
        if (field == "confirmation") Resize(scope.EffectiveScope!.Accounts,count);
        Assert.Equal(count == expected,SingleActionConfigurationJudgement.V2Scope(options,scope));
    }

    [Theory]
    [InlineData(0)][InlineData(11)][InlineData(12)][InlineData(13)]
    public void Given_NarrowRoleMapCount_When_Checked_Then_ExactlyTwelveExistingAssignmentsAreRequired(int count)
    {
        var options = ProbeV2Fixture.Options(); var scope = ProbeV2Fixture.Scope(options); Resize(scope.Deployed.Assignments,count);
        Assert.Equal(count == 12,SingleActionConfigurationJudgement.V2Scope(options,scope));
    }

    [Theory]
    [InlineData("scope",1)][InlineData("scope",2)][InlineData("scope",3)]
    [InlineData("confirmation",0)][InlineData("confirmation",1)][InlineData("confirmation",2)]
    public void Given_PermissionScopeSchemaBoundary_When_Checked_Then_OnlyDeclaredVersionsAreAdmitted(string field,int version)
    {
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options);
        scope = field == "scope" ? scope with { SchemaVersion = version } : scope with { EffectiveScope = scope.EffectiveScope! with { SchemaVersion = version } };
        Assert.Equal(version == (field == "scope" ? 2 : 1),SingleActionConfigurationJudgement.V2Scope(options,scope));
    }

    [Theory]
    [InlineData(0)][InlineData(63)][InlineData(64)][InlineData(65)]
    public void Given_EffectiveScopeHashLength_When_Checked_Then_ExactlySixtyFourHexCharactersAreRequired(int length)
    {
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options) with { EffectiveScopeSha256 = new string('a',length) };
        Assert.Equal(length == 64,SingleActionConfigurationJudgement.V2Scope(options,scope));
    }

    [Theory]
    [InlineData('/',false)][InlineData('0',true)][InlineData('1',true)][InlineData('8',true)][InlineData('9',true)][InlineData(':',false)]
    [InlineData('`',false)][InlineData('a',true)][InlineData('b',true)][InlineData('e',true)][InlineData('f',true)][InlineData('g',false)]
    public void Given_HashCharacterAtHexRangeEdges_When_Checked_Then_ImageAndScopeHashesUseLowercaseHex(char value,bool valid)
    {
        string hex = new(value,64);
        var map = new Dictionary<string,string>(StringComparer.Ordinal) { ["loadgen"] = "sha256:" + hex,["verifier"] = "sha256:" + hex };
        Assert.Equal(valid,SingleActionConfigurationJudgement.V2Images(map,map));
        var options = ProbeV2Fixture.Options("F06"); var scope = ProbeV2Fixture.Scope(options) with { EffectiveScopeSha256 = hex };
        Assert.Equal(valid,SingleActionConfigurationJudgement.V2Scope(options,scope));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_ReportedElapsedAroundObservedTerminal_When_Checked_Then_ExactObservationTimeIsRequired(int delta)
    {
        var (options,evidence,row) = Fixture(); row = row with { ElapsedMs = row.ElapsedMs + delta };
        var result = Judge(options,evidence,row);
        Assert.Equal(delta == 0 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(delta == 0 ? Array.Empty<string>() : ["v2_terminal"],result.Reasons);
    }

    [Theory]
    [InlineData("B08","delivered",-1)][InlineData("B08","delivered",0)][InlineData("B08","delivered",1)]
    [InlineData("B08","consumed",-1)][InlineData("B08","consumed",0)][InlineData("B08","consumed",1)]
    [InlineData("F06","delivered",-1)][InlineData("F06","delivered",0)][InlineData("F06","delivered",1)]
    [InlineData("F06","consumed",-1)][InlineData("F06","consumed",0)][InlineData("F06","consumed",1)]
    public void Given_ComparedPartitionAroundFirstRepeat_When_Aggregated_Then_OnlyExactSemanticClassIsEqual(string item,string field,int delta)
    {
        var first = ProbeV2Fixture.Candidate(item).Repeats[0];
        var semantic = first.SemanticOutcome;
        semantic = field == "delivered" ? semantic with { DeliveredPartition = semantic.DeliveredPartition + delta } : semantic with { ConsumedPartition = semantic.ConsumedPartition + delta };
        Assert.Equal(delta == 0,SingleActionConfigurationJudgement.V2Equal(item,"C0",first,first with { SemanticOutcome = semantic }));
    }

    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_SkippedSlotElapsedAroundZero_When_Checked_Then_OnlyZeroIsWellFormed(int elapsed)
    {
        var options = ProbeV2Fixture.Options("F06","C2"); var evidence = ProbeV2Fixture.Evidence(options,"C2","read");
        var row = evidence.Capture!.Repeats[0] with { ElapsedMs = elapsed };
        var result = Judge(options,evidence,row);
        Assert.Equal(elapsed == 0 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(elapsed == 0 ? Array.Empty<string>() : ["v2_wire"],result.Reasons);
    }

    [Theory]
    [InlineData("settings")][InlineData("omissions")][InlineData("errors")][InlineData("statistics")][InlineData("records")][InlineData("eof")]
    public void Given_SkippedSlotWithNonemptyClientEvidence_When_Checked_Then_UnconstructedClientCannotClaimEvidence(string field)
    {
        var options = ProbeV2Fixture.Options("F06","C2"); var evidence = ProbeV2Fixture.Evidence(options,"C2","read"); var row = evidence.Capture!.Repeats[0];
        row = field switch
        {
            "settings" => row with { Settings = new Dictionary<string,string>(StringComparer.Ordinal) { ["key"] = "value" } },
            "omissions" => row with { OmittedKeys = ["key"] },"errors" => row with { ClientErrors = [new(-1,false,"local","",0,0)] },
            "statistics" => row with { Statistics = [ProbeV2Fixture.Statistic("synthetic",row.Action,0)] },
            "records" => row with { ReadObservation = new(1,0,null) },_ => row with { ReadObservation = new(0,1,null) }
        };
        var result = Judge(options,evidence,row); Assert.Equal("invalid",result.Status); Assert.Equal(["v2_wire"],result.Reasons);
    }

    [Theory]
    [InlineData(31)][InlineData(32)][InlineData(33)]
    public void Given_NativeStatisticsNestingDepth_When_Checked_Then_ParserLimitIsInclusive(int depth)
    {
        var (options,evidence,row) = Fixture();
        string raw = Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64));
        raw = raw[..^1] + ",\"extra\":" + new string('[',depth - 1) + "0" + new string(']',depth - 1) + "}";
        row = row with { Statistics = [row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) }] };
        var result = Judge(options,evidence,row);
        Assert.Equal(depth > 32 ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(depth > 32 ? ["v2_capture_limit","v2_statistics"] : Array.Empty<string>(),result.Reasons);
    }

    [Theory]
    [InlineData(9999)][InlineData(10000)][InlineData(10001)]
    public void Given_NativeStatisticsEntryCount_When_Checked_Then_ParserLimitIsInclusive(int count)
    {
        var (options,evidence,row) = Fixture();
        string raw = Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64));
        raw = raw[..^1] + ",\"extra\":[" + string.Join(',',Enumerable.Repeat("0",count - 7)) + "]}";
        Assert.True(Encoding.UTF8.GetByteCount(raw) < 32768);
        row = row with { Statistics = [row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) }] };
        var result = Judge(options,evidence,row);
        Assert.Equal(count > 10000 ? "incomplete" : "complete_valid",result.Status);
        Assert.Equal(count > 10000 ? ["v2_capture_limit","v2_statistics"] : Array.Empty<string>(),result.Reasons);
    }

    [Theory]
    [InlineData("9223372036854775806",true)][InlineData("9223372036854775807",true)][InlineData("9223372036854775808",false)]
    public void Given_NativeMicrosecondsAtInt64Ceiling_When_Checked_Then_OutOfRangeIsUnusable(string nativeTs,bool valid)
    {
        var (options,evidence,row) = Fixture();
        string raw = Encoding.UTF8.GetString(Convert.FromBase64String(row.Statistics[0].RawBase64)).Replace("\"ts\":1", "\"ts\":" + nativeTs,StringComparison.Ordinal);
        row = row with { Statistics = [row.Statistics[0] with { RawBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)) }] };
        var result = Judge(options,evidence,row);
        Assert.Equal(valid ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(valid ? Array.Empty<string>() : ["v2_statistics"],result.Reasons);
    }

    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    public void Given_ActualSettingsCountAroundExpected_When_Checked_Then_ExactNativeDictionaryIsRequired(int count)
    {
        var (options,evidence,row) = Fixture(); var expected = row.Settings;
        var actual = row.Settings.ToDictionary(x => x.Key,x => x.Value,StringComparer.Ordinal); Resize(actual,count);
        var result = SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row with { Settings = actual },expected,[]);
        Assert.Equal(count == 2 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(count == 2 ? Array.Empty<string>() : ["v2_configuration"],result.Reasons);
    }

    [Theory]
    [InlineData(0)][InlineData(99)][InlineData(100)][InlineData(101)]
    public void Given_CleanupDiagnosticCount_When_Checked_Then_CountExhaustionHasItsOwnReason(int count)
    {
        var (options,evidence,row) = Fixture();
        var result = Judge(options,evidence,row with { Diagnostics = Enumerable.Repeat("client_cleanup_failed:TimeoutException",count).ToArray() });
        Assert.Equal(count == 0 ? "complete_valid" : "incomplete",result.Status);
        Assert.Equal(count > 100,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal));
        Assert.Equal(count > 0,result.Reasons.Contains("v2_completion",StringComparer.Ordinal));
        Assert.DoesNotContain("v2_wire",result.Reasons);
    }

    [Theory]
    [InlineData(2047)][InlineData(2048)][InlineData(2049)]
    public void Given_CleanupDiagnosticBytes_When_Checked_Then_SizeExhaustionHasItsOwnReason(int bytes)
    {
        const string prefix = "client_cleanup_failed:"; var (options,evidence,row) = Fixture();
        int padding = bytes - Encoding.UTF8.GetByteCount(prefix);
        string diagnostic = prefix + new string('é',padding / 2) + (padding % 2 == 0 ? "" : "x");
        Assert.Equal(bytes,Encoding.UTF8.GetByteCount(diagnostic));
        var result = Judge(options,evidence,row with { Diagnostics = [diagnostic] });
        Assert.Equal("incomplete",result.Status); Assert.Contains("v2_completion",result.Reasons);
        Assert.Equal(bytes > 2048,result.Reasons.Contains("v2_capture_limit",StringComparer.Ordinal)); Assert.DoesNotContain("v2_wire",result.Reasons);
    }

    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)]
    public void Given_NonfatalCallbackSequenceAroundTerminalError_When_Checked_Then_OnlySameSequenceCannotBePromoted(int sequence)
    {
        var (options,evidence,row) = Fixture();
        row = row with { Result = "error",Delivery = null,Error = new(-1,false,"local","terminal",10,2),
            ClientErrors = [new(-1,false,"local","callback",5,sequence)],Statistics = [row.Statistics[0] with { Sequence = 4 }] };
        var result = Judge(options,evidence,row);
        Assert.Equal(sequence == 2 ? "invalid" : "complete_valid",result.Status);
        Assert.Equal(sequence == 2 ? ["v2_terminal"] : Array.Empty<string>(),result.Reasons);
    }

    [Theory]
    [InlineData(119999)][InlineData(120000)][InlineData(120001)]
    public void Given_MatchingRecordAtSilenceWindowEdge_When_Checked_Then_OnlyPostterminalRecordPreservesAbsenceClass(long observed)
    {
        var options = ProbeV2Fixture.Options("F06","C10"); var evidence = ProbeV2Fixture.Evidence(options,"C10","action"); var row = evidence.Capture!.Repeats[0];
        row = row with { ReadObservation = new(1,0,new(row.Requested!.Topic,0,row.Requested.Offset!.Value,row.Requested.KeyBase64,row.Requested.ValueBase64,observed,1)) };
        var result = Judge(options,evidence,row);
        Assert.Equal(observed > 120000 ? "complete_valid" : "invalid",result.Status);
        Assert.Equal(observed > 120000 ? Array.Empty<string>() : ["v2_terminal"],result.Reasons);
    }

    private static void Resize<T>(Dictionary<string,T> values,int count)
    {
        T first = values.First().Value;
        foreach (string key in values.Keys.Skip(count).ToArray()) values.Remove(key);
        while (values.Count < count) values.Add("extra" + values.Count.ToString(CultureInfo.InvariantCulture),first);
    }

    private static (ProbeV2Options Options,ProbeV2Evidence Evidence,ProbeV2Repeat Row) Fixture(string stage = "action")
    {
        var options = ProbeV2Fixture.Options(); var evidence = ProbeV2Fixture.Evidence(options,"C0",stage);
        return (options,evidence,evidence.Capture!.Repeats[0]);
    }
    private static ProbeV2RepeatProjection Judge(ProbeV2Options options,ProbeV2Evidence evidence,ProbeV2Repeat row) =>
        SingleActionRepeatJudgement.JudgeV2Slot(options,evidence,row,row.Settings,[]);
}
