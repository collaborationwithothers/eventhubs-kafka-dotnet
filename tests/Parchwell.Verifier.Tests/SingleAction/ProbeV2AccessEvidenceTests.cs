using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2AccessEvidenceTests
{
    internal static ProbeV2AccessEnvelope Access(ProbeV2Options options)
    {
        var scope = ProbeV2Fixture.Scope(options);
        string[] topics = ["document-events","document-events-dlq","document-state"];
        string[] accounts = ["loadgen","enrichment-consumer"];
        var principals = accounts.ToDictionary(x => x,x => scope.Deployed.Accounts[x].PrincipalId,StringComparer.Ordinal);
        var targets = topics.ToDictionary(x => x,x => scope.Deployed.Topics[x],StringComparer.Ordinal);
        string binding = "probe-v2-access-v1\n" + options.RunId + "\n" + options.SourceCommit + "\n" + scope.Deployed.NamespaceId.ToLowerInvariant() + "\n" +
            string.Concat(accounts.Select(x => x + "=" + principals[x].ToLowerInvariant() + "\n")) + string.Concat(topics.Select(x => x + "=" + targets[x].ToLowerInvariant() + "\n"));
        string sender = "role:" + Hash("sender"),receiver = "role:" + Hash("receiver");
        ProbeV2AccessEntry Entry(string account,string target,string operation,int index) => new(account,account,target,operation,operation == "send" ? "sender" : "receiver","assignment:" + Hash("assignment" + index),operation == "send" ? sender : receiver,"event_hub");
        var projection = new ProbeV2AccessProjection(1,"azure_rbac_configuration_check",options.RunId,options.SourceCommit,1000,2000,Hash(binding),"complete",[],
            new(6,new(StringComparer.Ordinal) { ["arm"] = "2022-04-01",["graph"] = "v1.0" }),
            new(accounts.ToDictionary(x => x,x => new ProbeV2AccessAccount(true,[Hash(x + "members")],[],[],[new(x,true,[Hash(x + "assignments")])]),StringComparer.Ordinal),topics,
                [new(sender,Hash("sender-definition")),new(receiver,Hash("receiver-definition"))]),
            [Entry("loadgen","document-events","send",0),Entry("enrichment-consumer","document-events","receive",1),Entry("enrichment-consumer","document-state","send",2),Entry("enrichment-consumer","document-events-dlq","send",3)],[],
            new(StringComparer.Ordinal) { ["audit_seconds"] = 600,["requests"] = 256,["pages_per_collection"] = 8,["groups"] = 64,["definitions"] = 64,["assignments"] = 4096,["private_files"] = 1024,["response_bytes"] = 1048576,["total_raw_bytes"] = 16777216,["projection_bytes"] = 32768,["automatic_retries"] = 0,["first_admission_age_ms"] = 60000 });
        byte[] bytes = ProbeV2Wire.Write(projection,32768);
        return new(1,"azure_rbac_configuration_check",options.RunId,options.SourceCommit,scope.Deployed.NamespaceId,principals,targets,1000,2000,Convert.ToBase64String(bytes),Hash(bytes));
    }
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ProbeV2AccessEnvelope Edit(ProbeV2AccessEnvelope access,Action<JsonObject> edit)
    {
        var projection = JsonNode.Parse(Convert.FromBase64String(access.ProjectionBase64))!.AsObject(); edit(projection);
        byte[] bytes = Encoding.UTF8.GetBytes(projection.ToJsonString());
        return access with { ProjectionBase64 = Convert.ToBase64String(bytes),ProjectionSha256 = Hash(bytes) };
    }
    private static ProbeV2AccessResult Judge(ProbeV2Options options,ProbeV2AccessEnvelope? access) => SingleActionConfigurationJudgement.V2AccessEvidence(options,ProbeV2Fixture.Scope(options),access);
    [Fact]
    public void Given_CompleteMeasuredSnapshot_When_Checked_Then_OnlyVerifiedHashIsReturned()
    {
        var options = ProbeV2Fixture.Options("F06"); var access = Access(options);
        var result = Judge(options,access); Assert.Empty(result.Problems); Assert.Equal(access.ProjectionSha256,result.AuditHash);
    }
    [Fact]
    public void Given_MissingAudit_When_Checked_Then_AttestationCannotSubstitute()
    {
        var options = ProbeV2Fixture.Options("F06");
        Assert.Equal(["v2_access_evidence"],Judge(options,null).Problems);
    }
    [Theory]
    [InlineData("run")][InlineData("source")][InlineData("namespace")][InlineData("principal")][InlineData("target")][InlineData("hash")][InlineData("projection_run")][InlineData("projection_source")][InlineData("binding")]
    public void Given_WrongBinding_When_Checked_Then_IdentityFailureHasNoAuditHash(string field)
    {
        var options = ProbeV2Fixture.Options("F06"); var access = Access(options);
        access = field switch
        {
            "run" => access with { RunId = "other" },"source" => access with { SourceCommit = new string('2',40) },"namespace" => access with { NamespaceId = access.NamespaceId + "other" },
            "principal" => access with { Principals = new(access.Principals,StringComparer.Ordinal) { ["loadgen"] = "22222222-2222-2222-2222-222222222222" } },
            "target" => access with { Targets = new(access.Targets,StringComparer.Ordinal) { ["document-events"] = access.Targets["document-state"] } },
            "hash" => access with { ProjectionSha256 = new string('0',64) },
            _ => Edit(access,p => p[field == "projection_run" ? "run_id" : field == "projection_source" ? "source_commit" : "binding_sha256"] = field == "binding" ? new string('0',64) : "other")
        };
        var result = Judge(options,access); Assert.Equal(["v2_identity"],result.Problems); Assert.Null(result.AuditHash);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,true)][InlineData(1,true)][InlineData(599999,true)][InlineData(600000,true)][InlineData(600001,false)]
    public void Given_DurationBoundary_When_Checked_Then_ClosedAuditWindowApplies(long duration,bool accepted)
    {
        var options = ProbeV2Fixture.Options("F06"); var access = Access(options);
        access = Edit(access,p => p["completed_unix_ms"] = 1000 + duration) with { CompletedUnixMs = 1000 + duration };
        var result = Judge(options,access);
        if (accepted) Assert.Empty(result.Problems); else Assert.Equal(["v2_wire"],result.Problems);
    }
    [Theory]
    [InlineData(-1)][InlineData(0)][InlineData(1)]
    public void Given_StartTimestampBoundary_When_Checked_Then_NegativeTimeIsRejected(long start)
    {
        var options = ProbeV2Fixture.Options("F06"); var access = Access(options);
        access = Edit(access,p => { p["started_unix_ms"] = start;p["completed_unix_ms"] = start + 1; }) with { StartedUnixMs = start,CompletedUnixMs = start + 1 };
        if (start < 0) Assert.Equal(["v2_wire"],Judge(options,access).Problems); else Assert.Empty(Judge(options,access).Problems);
    }
    [Theory]
    [InlineData("membership_unavailable","v2_access_evidence")][InlineData("request_limit","v2_capture_limit")][InlineData("page_limit","v2_capture_limit")]
    [InlineData("group_limit","v2_capture_limit")][InlineData("definition_limit","v2_capture_limit")][InlineData("assignment_limit","v2_capture_limit")]
    [InlineData("private_file_limit","v2_capture_limit")][InlineData("response_limit","v2_capture_limit")][InlineData("raw_bytes_limit","v2_capture_limit")][InlineData("projection_limit","v2_capture_limit")]
    public void Given_TypedIncompleteAudit_When_Checked_Then_GapReasonIsPreserved(string reason,string problem)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(Access(options),p => { p["status"] = "incomplete";p["reasons"] = new JsonArray(reason);p["coverage"]!["accounts"]!["loadgen"]!["membership_complete"] = false; });
        Assert.Equal([problem],Judge(options,access).Problems);
    }
    [Theory]
    [InlineData("success")][InlineData("COMPLETE")][InlineData("")]
    public void Given_UnknownStatus_When_Checked_Then_WireIsRejected(string status)
    {
        var options = ProbeV2Fixture.Options("F06");Assert.Equal(["v2_wire"],Judge(options,Edit(Access(options),p => p["status"] = status)).Problems);
    }
    [Theory]
    [InlineData(-1,false)][InlineData(0,false)][InlineData(1,false)][InlineData(5,false)][InlineData(6,true)][InlineData(7,true)][InlineData(255,true)][InlineData(256,true)][InlineData(257,false)]
    public void Given_MeasuredRequestBoundary_When_Checked_Then_RetainedQueriesAndCapAreEnforced(int requests,bool accepted)
    {
        var options = ProbeV2Fixture.Options("F06");var result = Judge(options,Edit(Access(options),p => p["observed"]!["requests"] = requests));
        if (accepted) Assert.Empty(result.Problems); else Assert.Equal(["v2_wire"],result.Problems);
    }
    [Theory]
    [InlineData("observed")][InlineData("coverage")][InlineData("source_limits")][InlineData("entries")][InlineData("status")]
    public void Given_MissingRequiredProjectionMember_When_Checked_Then_ItCannotDefault(string field)
    {
        var options = ProbeV2Fixture.Options("F06");Assert.Equal(["v2_wire"],Judge(options,Edit(Access(options),p => p.Remove(field))).Problems);
    }
    [Theory]
    [InlineData("reason")][InlineData("duplicate_reason")][InlineData("unknown_key")][InlineData("bool_requests")][InlineData("api")][InlineData("alias")][InlineData("duplicate_entry")][InlineData("unknown_entry")][InlineData("empty_entries")][InlineData("false_completion")][InlineData("empty_membership")][InlineData("unknown_subject")]
    public void Given_MalformedOrContradictoryProjection_When_Checked_Then_WireIsRejected(string field)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(Access(options),p =>
        {
            switch (field)
            {
                case "reason": p["status"] = "incomplete";p["reasons"] = new JsonArray("other");break;
                case "duplicate_reason": p["status"] = "incomplete";p["reasons"] = new JsonArray("membership_unavailable","membership_unavailable");break;
                case "unknown_key": p["success"] = true;break;
                case "bool_requests": p["observed"]!["requests"] = true;break;
                case "api": p["observed"]!["api_versions"]!["arm"] = "2020-01-01";break;
                case "alias": p["entries"]![0]!["assignment_alias"] = "assignment:wrong";break;
                case "duplicate_entry": p["entries"]!.AsArray().Add(p["entries"]![0]!.DeepClone());break;
                case "unknown_entry": p["entries"]![0]!["proof"] = true;break;
                case "empty_entries": p["entries"] = new JsonArray();break;
                case "false_completion": p["coverage"]!["accounts"]!["loadgen"]!["membership_complete"] = false;break;
                case "empty_membership": p["coverage"]!["accounts"]!["loadgen"]!["membership_pages"] = new JsonArray();break;
                default: p["entries"]![0]!["subject"] = "probe";break;
            }
        });
        Assert.Equal(["v2_wire"],Judge(options,access).Problems);
    }
    [Fact]
    public void Given_AdditionalSupportedGrant_When_Checked_Then_MismatchIsEvidenceGap()
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(Access(options),p =>
        {
            p["status"] = "mismatch";p["reasons"] = new JsonArray("configuration_mismatch");
            var entry = p["entries"]![0]!.DeepClone(); entry["target"] = "document-state";entry["assignment_alias"] = "assignment:" + Hash("extra");p["entries"]!.AsArray().Add(entry);
        });
        Assert.Equal(["v2_access_configuration"],Judge(options,access).Problems);
    }
    [Theory]
    [InlineData(32767,false)][InlineData(32768,false)][InlineData(32769,true)]
    public void Given_ProjectionByteBoundary_When_Checked_Then_OverCapPrecedesWireParsing(int length,bool overCap)
    {
        var options = ProbeV2Fixture.Options("F06");var access = Access(options);
        byte[] original = Convert.FromBase64String(access.ProjectionBase64);
        byte[] bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original) + new string(' ',length - original.Length));
        access = access with { ProjectionBase64 = Convert.ToBase64String(bytes),ProjectionSha256 = Hash(bytes) };
        if (overCap) Assert.Equal(["v2_capture_limit"],Judge(options,access).Problems); else Assert.Empty(Judge(options,access).Problems);
    }
    [Theory]
    [InlineData("membership_pages",7,true)][InlineData("membership_pages",8,true)][InlineData("membership_pages",9,false)]
    [InlineData("pages",7,true)][InlineData("pages",8,true)][InlineData("pages",9,false)]
    [InlineData("groups",63,true)][InlineData("groups",64,true)][InlineData("groups",65,false)]
    [InlineData("directory_roles",63,true)][InlineData("directory_roles",64,true)][InlineData("directory_roles",65,false)]
    [InlineData("definitions",63,true)][InlineData("definitions",64,true)][InlineData("definitions",65,false)]
    public void Given_InnerCollectionBoundary_When_Checked_Then_EachCapIsCheckedBeforeMaterialization(string field,int count,bool accepted)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(Access(options),p =>
        {
            var owner = p["coverage"]!["accounts"]!["loadgen"]!;
            if (field is "membership_pages" or "pages")
            {
                var pages = new JsonArray(Enumerable.Range(0,count).Select(i => (JsonNode?)JsonValue.Create(Hash("page" + i))).ToArray());
                if (field == "membership_pages") owner[field] = pages; else owner["assignment_collections"]![0]!["pages"] = pages;
                p["observed"]!["requests"] = 5 + count;
            }
            else if (field == "groups")
            {
                owner["groups"] = new JsonArray(Enumerable.Range(0,count).Select(i => (JsonNode?)JsonValue.Create("group:" + Hash("group" + i))).ToArray());
                var collections = owner["assignment_collections"]!.AsArray();
                for (int i = 0; i < count; i++) collections.Add(new JsonObject { ["subject"] = "group:" + Hash("group" + i),["complete"] = true,["pages"] = new JsonArray(Hash("collection" + i)) });
                p["observed"]!["requests"] = 6 + count;
            }
            else if (field == "directory_roles") owner[field] = new JsonArray(Enumerable.Range(0,count).Select(i => (JsonNode?)JsonValue.Create("directory_role:" + Hash("role" + i))).ToArray());
            else
            {
                var definitions = p["coverage"]!["definitions"]!.AsArray();
                for (int i = 2; i < count; i++) definitions.Add(new JsonObject { ["alias"] = "role:" + Hash("definition" + i),["raw_sha256"] = Hash("raw" + i) });
                p["observed"]!["requests"] = 4 + count;
            }
        });
        var result = Judge(options,access);
        if (accepted) Assert.Empty(result.Problems); else Assert.Equal(["v2_capture_limit"],result.Problems);
    }
    [Theory]
    [InlineData(22,true)][InlineData(23,true)][InlineData(24,false)]
    public void Given_ReasonCollectionBoundary_When_Checked_Then_ClosedVocabularyAndCardinalityApply(int count,bool withinBound)
    {
        string[] reasons = ["input_invalid","platform_unsupported","membership_unavailable","assignment_unavailable","definition_unavailable","membership_shape","assignment_shape","definition_shape","unsupported_grant","configuration_mismatch","pagination_invalid","request_limit","page_limit","group_limit","definition_limit","assignment_limit","private_file_limit","response_limit","raw_bytes_limit","projection_limit","time_limit","transport_failure","safety_stop"];
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(Access(options),p => { p["status"] = "incomplete";p["reasons"] = new JsonArray(reasons.Take(count).Concat(Enumerable.Repeat("safety_stop",count > 23 ? 1 : 0)).Order(StringComparer.Ordinal).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()); });
        var result = Judge(options,access);
        Assert.Contains("v2_capture_limit",result.Problems);
        if (withinBound) Assert.Contains("v2_access_evidence",result.Problems); else Assert.Single(result.Problems);
    }
    [Fact]
    public async Task Given_PythonProducerExport_When_ImportIsRequested_Then_RealEnvelopeAndProjectionAreChecked()
    {
        string? directory = Environment.GetEnvironmentVariable("PARCHWELL_V2_ACCESS_CONTRACT_IMPORT");
        if (directory is null) return;
        var inputs = new ProbeV2Inputs();
        byte[] envelopeBytes = await inputs.ReadAsync(Path.Combine(directory,"access-check.json"),131072,CancellationToken.None) ?? throw new InvalidOperationException("access_export_missing");
        var access = ProbeV2Wire.Read<ProbeV2AccessEnvelope>(envelopeBytes,131072);
        byte[] projection = await inputs.ReadAsync(Path.Combine(directory,"probe-v2-access-check.json"),32768,CancellationToken.None) ?? throw new InvalidOperationException("access_projection_missing");
        Assert.Equal(projection,Convert.FromBase64String(access.ProjectionBase64));
        var options = ProbeV2Fixture.Options("F06");options.RunId = access.RunId;options.SourceCommit = access.SourceCommit;
        var scope = ProbeV2Fixture.Scope(options);var deployed = scope.Deployed;
        var accounts = new Dictionary<string,ProbeV2Account>(deployed.Accounts,StringComparer.Ordinal);
        foreach (var pair in access.Principals) accounts[pair.Key] = accounts[pair.Key] with { PrincipalId = pair.Value };
        scope = scope with { Deployed = deployed with { NamespaceId = access.NamespaceId,Topics = access.Targets,Accounts = accounts } };
        var result = SingleActionConfigurationJudgement.V2AccessEvidence(options,scope,access);
        Assert.Empty(result.Problems);Assert.Equal(Hash(projection),result.AuditHash);
    }
}
