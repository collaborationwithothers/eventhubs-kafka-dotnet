using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.Common.Consumer;
using Xunit;

namespace Parchwell.Verifier.Tests.Consumer;

#pragma warning disable CA1707
public sealed class ConsumerArtifactTests
{
    [Theory]
    [InlineData("subject%2Fmember", false)]
    [InlineData("subject%2fmember", true)]
    public async Task Given_EncodedPodName_U3_When_Loaded_Then_RequiresTheContractMemberPrefix(string encoded, bool invalid)
    {
        const string pod = "subject/member";
        var fixture = new ConsumerWireFixture();
        JsonObject marker = ConsumerWireFixture.Registration("subject");
        marker["pod_name"] = pod;
        marker["instance_id"] = pod;
        marker["effective_settings"]!["client.id"] = pod;
        fixture.Set("subject", "registration.json", marker);
        fixture.SetRows("subject", [ConsumerWireFixture.Assignment(pod, [0])]);
        JsonObject receipt = ConsumerWireFixture.Completion("subject", 1, 1, 0);
        receipt["instance_id"] = pod;
        fixture.Set("subject", "completion.json", receipt);
        string prefix = ConsumerWireFixture.Prefix + "consumer/subject/";
        foreach (string name in fixture.Blobs.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            byte[] bytes = fixture.Blobs[name];
            fixture.Blobs.Remove(name);
            fixture.Blobs.Add(ConsumerWireFixture.Prefix + "consumer/" + encoded + "/" + name[prefix.Length..], bytes);
        }
        ConsumerMember member = Assert.Single((await fixture.Load()).Members, member => member.Registration?.Role == "subject");
        Assert.Equal(invalid, member.RegistrationInvalid);
        Assert.False(member.RecordInvalid);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("unordered-settings")]
    [InlineData("observer")]
    [InlineData("pod-prefix")]
    [InlineData("instance-pod")]
    public async Task Given_MalformedRegistration_U3_When_Loaded_Then_RegistrationInvalid(string mutation)
    {
        var fixture = new ConsumerWireFixture();
        JsonObject marker = ConsumerWireFixture.Registration("subject");
        switch (mutation)
        {
            case "unknown": marker["unknown"] = 1; break;
            case "unordered-settings": marker["effective_settings"] = new JsonObject { ["z"] = "value", ["a"] = "value" }; break;
            case "observer": marker["role"] = "observer"; break;
            case "pod-prefix": marker["pod_name"] = "other"; marker["instance_id"] = "other"; break;
            case "instance-pod": marker["instance_id"] = "other"; break;
        }
        fixture.Set("subject", "registration.json", marker);
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.True(loaded.Members.Single(m => m.Prefix.Contains("/subject/", StringComparison.Ordinal)).RegistrationInvalid);
    }

    [Theory]
    [InlineData(65535, false)]
    [InlineData(65536, false)]
    [InlineData(65537, true)]
    public async Task Given_RegistrationByteBoundary_U3_When_Loaded_Then_EnforcesWireLimit(int size, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        string name = ConsumerWireFixture.Name("subject", "registration.json");
        fixture.Blobs[name] = Pad(fixture.Blobs[name], size);
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, loaded.Members.Single(m => m.Prefix.Contains("/subject/", StringComparison.Ordinal)).RegistrationInvalid);
    }

    [Theory]
    [InlineData(39999, true, true)]
    [InlineData(40000, true, false)]
    [InlineData(40001, true, true)]
    [InlineData(40000, false, false)]
    [InlineData(40001, false, true)]
    public async Task Given_RecordBlobRowBoundaries_U4_When_Loaded_Then_EnforcesRollover(int count, bool following, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        fixture.SetRows("subject", Enumerable.Range(0, count).Select(n => ConsumerWireFixture.Assignment("subject", [0], n)));
        if (following) fixture.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0], count)], 1);
        fixture.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", count + (following ? 1 : 0), following ? 2 : 1, 0));
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, Subject(loaded).RecordInvalid);
    }

    [Theory]
    [InlineData("blob-gap")]
    [InlineData("sequence-gap")]
    [InlineData("sequence-repeat")]
    [InlineData("receipt-rows")]
    [InlineData("receipt-blobs")]
    [InlineData("receipt-processed")]
    [InlineData("receipt-job")]
    [InlineData("empty-record")]
    [InlineData("no-lf")]
    public async Task Given_RecordIntegrityFault_U4_When_Loaded_Then_RecordInvalid(string mutation)
    {
        var fixture = new ConsumerWireFixture();
        switch (mutation)
        {
            case "blob-gap": fixture.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0], 1)], 2); break;
            case "sequence-gap": fixture.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0]), ConsumerWireFixture.Assignment("subject", [0], 1), ConsumerWireFixture.Assignment("subject", [0], 3)]); break;
            case "sequence-repeat": fixture.SetRows("subject", [ConsumerWireFixture.Assignment("subject", [0]), ConsumerWireFixture.Assignment("subject", [0], 1), ConsumerWireFixture.Assignment("subject", [0], 1)]); break;
            case "receipt-rows": fixture.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", 2, 1, 0)); break;
            case "receipt-blobs": fixture.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", 1, 2, 0)); break;
            case "receipt-processed": fixture.Set("subject", "completion.json", ConsumerWireFixture.Completion("subject", 1, 1, 1)); break;
            case "receipt-job": JsonObject receipt = ConsumerWireFixture.Completion("subject", 1, 1, 0); receipt["job_name"] = "other"; fixture.Set("subject", "completion.json", receipt); break;
            case "empty-record": fixture.Blobs.Remove(ConsumerWireFixture.Name("subject", "record/000000.jsonl")); break;
            case "no-lf": string name = ConsumerWireFixture.Name("subject", "record/000000.jsonl"); fixture.Blobs[name] = fixture.Blobs[name][..^1]; break;
        }
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.True(Subject(loaded).RecordInvalid);
    }

    [Theory]
    [InlineData("unknown", true)]
    [InlineData("duplicate", true)]
    [InlineData("missing", true)]
    [InlineData("schema", true)]
    [InlineData("instance", true)]
    [InlineData("topic", true)]
    [InlineData("six-digits", true)]
    [InlineData("seven-digits", false)]
    [InlineData("unsorted-partitions", true)]
    [InlineData("duplicate-partitions", true)]
    [InlineData("empty-partitions", false)]
    public async Task Given_RowShape_U4_When_Loaded_Then_EnforcesExactWireContract(string mutation, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        JsonObject row = ConsumerWireFixture.Assignment("subject", [0]);
        switch (mutation)
        {
            case "unknown": row["unknown"] = 1; break;
            case "missing": row.Remove("event_utc"); break;
            case "schema": row["schema_version"] = 2; break;
            case "instance": row["instance_id"] = "other"; break;
            case "topic": row = ConsumerWireFixture.Processed("subject"); row["topic"] = "other"; break;
            case "six-digits": row["event_utc"] = "2026-11-15T00:00:00.000000Z"; break;
            case "unsorted-partitions": row["partitions"] = new JsonArray(1, 0); break;
            case "duplicate-partitions": row["partitions"] = new JsonArray(0, 0); break;
            case "empty-partitions": row["partitions"] = new JsonArray(); break;
        }
        fixture.SetRows("subject", [row]);
        if (mutation == "duplicate") fixture.Blobs[ConsumerWireFixture.Name("subject", "record/000000.jsonl")] = Encoding.UTF8.GetBytes(row.ToJsonString().Replace("\"schema_version\":1", "\"schema_version\":1,\"schema_version\":1", StringComparison.Ordinal) + "\n");
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, Subject(loaded).RecordInvalid);
    }

    [Theory]
    [InlineData(65535, false)]
    [InlineData(65536, false)]
    [InlineData(65537, true)]
    public async Task Given_RowByteBoundary_U4_When_Loaded_Then_LimitIncludesLineFeed(int size, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        string name = ConsumerWireFixture.Name("subject", "record/000000.jsonl");
        fixture.Blobs[name] = Pad(fixture.Blobs[name][..^1], size - 1).Append((byte)'\n').ToArray();
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, Subject(loaded).RecordInvalid);
    }

    [Theory]
    [InlineData("callback-returned")]
    [InlineData("callback-submitted")]
    [InlineData("explicit-callback")]
    [InlineData("explicit-null")]
    [InlineData("returned-row-error")]
    [InlineData("returned-entry-error")]
    [InlineData("returned-different-offset")]
    [InlineData("returned-different-order")]
    [InlineData("partition-row-error")]
    [InlineData("request-no-error")]
    [InlineData("request-entries")]
    public async Task Given_MalformedCommitClause_U4_When_Loaded_Then_RecordInvalid(string mutation)
    {
        var fixture = new ConsumerWireFixture();
        JsonObject row = ConsumerWireFixture.Commit("subject", 0, "explicit", "returned");
        switch (mutation)
        {
            case "callback-returned": row["source"] = "callback"; row["submitted"] = null; break;
            case "callback-submitted": row["source"] = "callback"; row["outcome"] = "callback"; row["submitted"] = new JsonArray(); break;
            case "explicit-callback": row["outcome"] = "callback"; break;
            case "explicit-null": row["submitted"] = null; break;
            case "returned-row-error": row["error_code"] = 1; row["error_reason"] = "synthetic"; break;
            case "returned-entry-error": row["offsets"]![0]!["error_code"] = 1; break;
            case "returned-different-offset": row["offsets"]![0]!["offset"] = 111; break;
            case "returned-different-order": row["submitted"] = new JsonArray(ConsumerWireFixture.Offset(110), ConsumerWireFixture.Offset(50, partition: 1)); row["offsets"] = new JsonArray(ConsumerWireFixture.Offset(50, true, 1), ConsumerWireFixture.Offset(110, true)); break;
            case "partition-row-error": row["outcome"] = "partition_exception"; row["error_code"] = 1; row["error_reason"] = "synthetic"; break;
            case "request-no-error": row["outcome"] = "request_exception"; row["offsets"] = new JsonArray(); break;
            case "request-entries": row["outcome"] = "request_exception"; row["error_code"] = 1; row["error_reason"] = "synthetic"; break;
        }
        fixture.SetRows("subject", [row]);
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.True(Subject(loaded).RecordInvalid);
    }

    [Theory]
    [InlineData("unknown", true)]
    [InlineData("65535", false)]
    [InlineData("65536", false)]
    [InlineData("65537", true)]
    public async Task Given_CompletionShape_U4_When_Loaded_Then_EnforcesReceiptWireContract(string mutation, bool invalid)
    {
        var fixture = new ConsumerWireFixture();
        string name = ConsumerWireFixture.Name("subject", "completion.json");
        if (mutation == "unknown") { JsonObject receipt = ConsumerWireFixture.Completion("subject", 1, 1, 0); receipt["unknown"] = 1; fixture.Set("subject", "completion.json", receipt); }
        else fixture.Blobs[name] = Pad(fixture.Blobs[name], int.Parse(mutation, System.Globalization.CultureInfo.InvariantCulture));
        ConsumerArtifacts loaded = await fixture.Load();
        Assert.False(loaded.Unavailable);
        Assert.Equal(invalid, Subject(loaded).RecordInvalid);
    }

    [Fact]
    public async Task Given_NoRowsDuringStartLine_U4_When_LoadedForSnapshot_Then_PreservesEmptyRecord()
    {
        var fixture = new ConsumerWireFixture();
        fixture.Blobs.Remove(ConsumerWireFixture.Name("subject", "record/000000.jsonl"));
        ConsumerArtifacts loaded = await fixture.Load(false);
        Assert.False(loaded.Unavailable);
        Assert.False(Subject(loaded).RecordInvalid);
        Assert.Empty(Subject(loaded).Rows);
    }

    private static ConsumerMember Subject(ConsumerArtifacts artifacts) => artifacts.Members.Single(m => m.Registration?.Role == "subject");
    private static byte[] Pad(byte[] bytes, int size) => bytes.Concat(Enumerable.Repeat((byte)' ', size - bytes.Length)).ToArray();
}
#pragma warning restore CA1707
