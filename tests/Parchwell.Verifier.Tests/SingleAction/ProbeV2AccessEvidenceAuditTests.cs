using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Parchwell.Verifier.VerifyRun.SingleAction;
using Xunit;

#pragma warning disable CA1707
namespace Parchwell.Verifier.Tests.SingleAction;

public sealed class ProbeV2AccessEvidenceAuditTests
{
    [Theory]
    [InlineData("scope_null","v2_identity")]
    [InlineData("schema_version","v2_wire")]
    [InlineData("principal_rebound","v2_identity")]
    [InlineData("target_rebound","v2_identity")]
    [InlineData("base64_over_cap","v2_capture_limit")]
    [InlineData("base64_invalid","v2_wire")]
    [InlineData("depth_33","v2_wire")]
    [InlineData("projection_started","v2_wire")]
    public void Given_EnvelopeDefect_When_Checked_Then_ExactProblemIsReturned(string defect,string problems)
    {
        var options = ProbeV2Fixture.Options("F06"); var access = ProbeV2AccessEvidenceTests.Access(options); ProbeV2Scope? scope = ProbeV2Fixture.Scope(options);
        switch (defect)
        {
            case "scope_null": scope = null; break;
            case "schema_version": access = access with { SchemaVersion = 2 }; break;
            case "principal_rebound": access = Rebind(options,access with { Principals = new(access.Principals,StringComparer.Ordinal) { ["loadgen"] = "22222222-2222-2222-2222-222222222222" } }); break;
            case "target_rebound": access = Rebind(options,access with { Targets = new(access.Targets,StringComparer.Ordinal) { ["document-events"] = access.Targets["document-state"] } }); break;
            case "base64_over_cap": access = access with { ProjectionBase64 = new string('!',43693) }; break;
            case "base64_invalid": access = access with { ProjectionBase64 = "!!!!" }; break;
            case "depth_33":
                JsonNode deep = JsonValue.Create(new string('x',129));
                for (int i = 0; i < 40; i++) deep = new JsonArray(deep);
                access = Edit(access,p => p["pad"] = deep); break;
            case "projection_started": access = Edit(access,p => p["started_unix_ms"] = 1001); break;
            default: throw new ArgumentOutOfRangeException(nameof(defect));
        }
        var result = SingleActionConfigurationJudgement.V2AccessEvidence(options,scope,access);
        Assert.Equal(problems,string.Join(',',result.Problems)); Assert.Null(result.AuditHash);
    }

    [Theory]
    [InlineData("complete_with_reason","v2_wire")]
    [InlineData("incomplete_without_reason","v2_wire")]
    [InlineData("mismatch_without_reason","v2_wire")]
    [InlineData("source_limit_value","v2_wire")]
    [InlineData("incomplete_zero_requests","v2_access_evidence")]
    [InlineData("incomplete_two_reasons","v2_access_evidence,v2_capture_limit")]
    public void Given_StatusOrLimitDefect_When_Checked_Then_ExactProblemsAreReturned(string defect,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            switch (defect)
            {
                case "complete_with_reason": p["reasons"] = new JsonArray("membership_unavailable"); break;
                case "incomplete_without_reason": p["status"] = "incomplete"; break;
                case "mismatch_without_reason":
                    p["status"] = "mismatch";
                    var entry = p["entries"]!.AsArray()[0]!.DeepClone(); entry["target"] = "document-state"; entry["assignment_alias"] = "assignment:" + Hash("extra"); p["entries"]!.AsArray().Add(entry);
                    break;
                case "source_limit_value": p["source_limits"]!.AsObject()["requests"] = 255; break;
                case "incomplete_zero_requests":
                    Incomplete(p); p["observed"]!.AsObject()["requests"] = 0; p["coverage"]!.AsObject()["definitions"] = new JsonArray(); p["entries"] = new JsonArray();
                    foreach (var pair in p["coverage"]!.AsObject()["accounts"]!.AsObject())
                    {
                        var owner = pair.Value!;
                        owner["membership_pages"] = new JsonArray(); owner["assignment_collections"]!.AsArray()[0]!.AsObject()["pages"] = new JsonArray();
                    }
                    break;
                case "incomplete_two_reasons": p["status"] = "incomplete"; p["reasons"] = new JsonArray("membership_unavailable","request_limit"); break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
        });
        var result = Judge(options,access);
        Assert.Equal(problems,string.Join(',',result.Problems)); Assert.Null(result.AuditHash);
    }

    [Theory]
    [InlineData("definition_raw_hash","v2_wire")]
    [InlineData("duplicate_definition","v2_wire")]
    [InlineData("coverage_target_order","v2_wire")]
    [InlineData("extra_coverage_account","v2_wire")]
    [InlineData("membership_page_hash","v2_wire")]
    [InlineData("membership_page_hex","v2_wire")]
    [InlineData("directory_role_alias","v2_wire")]
    [InlineData("duplicate_directory_role","v2_wire")]
    [InlineData("duplicate_group","v2_wire")]
    [InlineData("collection_page_hash","v2_wire")]
    [InlineData("duplicate_collection","v2_wire")]
    public void Given_CoverageDefect_When_Checked_Then_WireIsRejected(string defect,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            var coverage = p["coverage"]!; var owner = coverage["accounts"]!.AsObject()["loadgen"]!; string group = "group:" + Hash("group");
            switch (defect)
            {
                case "definition_raw_hash": coverage["definitions"]!.AsArray()[1]!.AsObject()["raw_sha256"] = "x"; break;
                case "duplicate_definition": coverage["definitions"]!.AsArray().Add(coverage["definitions"]!.AsArray()[0]!.DeepClone()); p["observed"]!.AsObject()["requests"] = 7; break;
                case "coverage_target_order": coverage["targets"] = new JsonArray("document-state","document-events-dlq","document-events"); break;
                case "extra_coverage_account": coverage["accounts"]!.AsObject()["probe"] = owner.DeepClone(); break;
                case "membership_page_hash": owner["membership_pages"]!.AsArray().Add("x"); p["observed"]!.AsObject()["requests"] = 7; break;
                case "membership_page_hex": owner["membership_pages"] = new JsonArray(new string('0',63) + "g"); break;
                case "directory_role_alias": owner["directory_roles"] = new JsonArray("directory_role:x"); break;
                case "duplicate_directory_role": owner["directory_roles"] = new JsonArray("directory_role:" + Hash("role"),"directory_role:" + Hash("role")); break;
                case "duplicate_group": Incomplete(p); owner["groups"] = new JsonArray(group,group); break;
                case "collection_page_hash":
                    owner["groups"] = new JsonArray(group); owner["assignment_collections"]!.AsArray()[0]!.AsObject()["pages"]!.AsArray().Add("x");
                    owner["assignment_collections"]!.AsArray().Add(new JsonObject { ["subject"] = group,["complete"] = true,["pages"] = new JsonArray(Hash("group-page")) });
                    p["observed"]!.AsObject()["requests"] = 8; break;
                case "duplicate_collection":
                    Incomplete(p); owner["assignment_collections"]!.AsArray().Add(new JsonObject { ["subject"] = "loadgen",["complete"] = true,["pages"] = new JsonArray(Hash("second-page")) });
                    p["observed"]!.AsObject()["requests"] = 7; break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
        });
        var result = Judge(options,access);
        Assert.Equal(problems,string.Join(',',result.Problems)); Assert.Null(result.AuditHash);
    }

    [Theory]
    [InlineData("collection_incomplete","v2_wire")]
    [InlineData("group_collection_incomplete","v2_wire")]
    [InlineData("collection_without_pages","v2_wire")]
    [InlineData("both_memberships_incomplete","v2_wire")]
    [InlineData("group_collection_requests_6","v2_wire")]
    [InlineData("group_collection_requests_7","")]
    public void Given_CompleteStatus_When_CoverageIsChecked_Then_EveryCollectionMustBeComplete(string defect,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            var accounts = p["coverage"]!.AsObject()["accounts"]!; var owner = accounts["loadgen"]!; string group = "group:" + Hash("group");
            void AddGroup(bool complete,int requests)
            {
                owner["groups"] = new JsonArray(group);
                owner["assignment_collections"]!.AsArray().Add(new JsonObject { ["subject"] = group,["complete"] = complete,["pages"] = new JsonArray(Hash("group-page")) });
                p["observed"]!.AsObject()["requests"] = requests;
            }
            switch (defect)
            {
                case "collection_incomplete": owner["assignment_collections"]!.AsArray()[0]!.AsObject()["complete"] = false; break;
                case "group_collection_incomplete": AddGroup(false,7); break;
                case "collection_without_pages": owner["assignment_collections"]!.AsArray()[0]!.AsObject()["pages"] = new JsonArray(); break;
                case "both_memberships_incomplete": owner["membership_complete"] = false; accounts["enrichment-consumer"]!.AsObject()["membership_complete"] = false; break;
                case "group_collection_requests_6": AddGroup(true,6); break;
                case "group_collection_requests_7": AddGroup(true,7); break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
        });
        var result = Judge(options,access);
        Assert.Equal(problems,string.Join(',',result.Problems)); Assert.Equal(problems.Length == 0 ? access.ProjectionSha256 : null,result.AuditHash);
    }

    [Theory]
    [InlineData("unknown_target")]
    [InlineData("rebound_assignment")]
    public void Given_IncompleteAuditWithEntryDefect_When_Checked_Then_WireIsRejected(string defect)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            Incomplete(p);
            if (defect == "unknown_target") p["entries"]!.AsArray()[0]!.AsObject()["target"] = "document-other";
            else { var entry = p["entries"]!.AsArray()[0]!.DeepClone(); entry["target"] = "document-state"; p["entries"]!.AsArray().Add(entry); }
        });
        Assert.Equal(["v2_wire"],Judge(options,access).Problems);
    }

    [Theory]
    [InlineData("custom_role","event_hub","account","v2_access_evidence")]
    [InlineData("condition","consumer_group","account","v2_access_evidence")]
    [InlineData("delegation","namespace","account","v2_access_evidence")]
    [InlineData("unfamiliar_operation","resource_group","account","v2_access_evidence")]
    [InlineData("unmatched_scope","subscription","account","v2_access_evidence")]
    [InlineData("definition_shape","management_group","account","v2_access_evidence")]
    [InlineData("unexpected_grant","other","group","v2_access_evidence")]
    [InlineData("other_reason","event_hub","account","v2_wire")]
    public void Given_IncompleteAuditWithUnsupportedGrant_When_Checked_Then_ClosedVocabularyApplies(string reason,string scopeClass,string subject,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            Incomplete(p); string group = "group:" + Hash("group");
            p["coverage"]!.AsObject()["accounts"]!.AsObject()["loadgen"]!.AsObject()["groups"] = new JsonArray(group);
            p["unsupported"] = new JsonArray(new JsonObject { ["account"] = "loadgen",["subject"] = subject == "group" ? group : "loadgen",["scope_class"] = scopeClass,["reason"] = reason,["raw_sha256"] = Hash("raw") });
        });
        Assert.Equal(problems,string.Join(',',Judge(options,access).Problems));
    }

    [Theory]
    [InlineData(false,"v2_wire")]
    [InlineData(true,"v2_access_configuration")]
    public void Given_MismatchWithMatchingTable_When_Checked_Then_OnlyUnexpectedGrantExplainsIt(bool unexpectedGrant,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            p["status"] = "mismatch"; p["reasons"] = new JsonArray("configuration_mismatch");
            if (unexpectedGrant) p["unsupported"] = new JsonArray(new JsonObject { ["account"] = "loadgen",["subject"] = "loadgen",["scope_class"] = "namespace",["reason"] = "unexpected_grant",["raw_sha256"] = Hash("raw") });
        });
        Assert.Equal(problems,string.Join(',',Judge(options,access).Problems));
    }

    [Theory]
    [InlineData("entries_8193","v2_capture_limit")]
    [InlineData("unsupported_4097","v2_capture_limit")]
    [InlineData("groups_65","v2_capture_limit")]
    [InlineData("assignment_collections_66","v2_capture_limit")]
    [InlineData("entry_string_96","v2_wire")]
    [InlineData("entry_string_97","v2_capture_limit")]
    [InlineData("string_128","v2_wire")]
    [InlineData("string_129","v2_capture_limit")]
    [InlineData("entry_string_empty","v2_capture_limit")]
    [InlineData("entry_string_non_ascii","v2_capture_limit")]
    [InlineData("entry_string_127","v2_wire")]
    [InlineData("entry_1024_bytes","v2_wire")]
    [InlineData("entry_1025_bytes","v2_capture_limit")]
    public void Given_ProjectionBoundsCase_When_Checked_Then_CapturePrecedesWireParsing(string defect,string problems)
    {
        var options = ProbeV2Fixture.Options("F06");
        var access = Edit(ProbeV2AccessEvidenceTests.Access(options),p =>
        {
            var owner = p["coverage"]!.AsObject()["accounts"]!.AsObject()["loadgen"]!; var entry = p["entries"]!.AsArray()[0]!.AsObject();
            switch (defect)
            {
                case "entries_8193": p["entries"] = Zeros(8193); break;
                case "unsupported_4097": p["unsupported"] = Zeros(4097); break;
                case "groups_65": owner["groups"] = Zeros(65); break;
                case "assignment_collections_66": owner["assignment_collections"] = Zeros(66); break;
                case "entry_string_96": entry["scope_class"] = new string('x',96); break;
                case "entry_string_97": entry["scope_class"] = new string('x',97); break;
                case "string_128": p["mode"] = new string('x',128); break;
                case "string_129": p["mode"] = new string('x',129); break;
                case "entry_string_empty": entry["scope_class"] = ""; break;
                case "entry_string_non_ascii": entry["scope_class"] = "event_hu\u00e9"; break;
                case "entry_string_127": entry["scope_class"] = "event_hub\u007f"; break;
                case "entry_1024_bytes": Pad(entry,1024); break;
                case "entry_1025_bytes": Pad(entry,1025); break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
        });
        if (defect.StartsWith("entry_102",StringComparison.Ordinal))
        {
            using var document = JsonDocument.Parse(Convert.FromBase64String(access.ProjectionBase64));
            Assert.Equal(defect == "entry_1024_bytes" ? 1024 : 1025,JsonSerializer.SerializeToUtf8Bytes(document.RootElement.GetProperty("entries")[0]).Length);
        }
        Assert.Equal(problems,string.Join(',',Judge(options,access).Problems));
    }

    private static ProbeV2AccessResult Judge(ProbeV2Options options,ProbeV2AccessEnvelope access) => SingleActionConfigurationJudgement.V2AccessEvidence(options,ProbeV2Fixture.Scope(options),access);
    private static void Incomplete(JsonObject projection) { projection["status"] = "incomplete"; projection["reasons"] = new JsonArray("membership_unavailable"); }
    private static JsonArray Zeros(int count) => new(Enumerable.Range(0,count).Select(_ => (JsonNode?)JsonValue.Create(0)).ToArray());
    private static void Pad(JsonObject entry,int size)
    {
        int index = 0;
        while (size - entry.ToJsonString().Length > 200) entry["p" + index++] = new string('x',96);
        int room = size - entry.ToJsonString().Length - 16;
        entry["q0"] = new string('x',room / 2); entry["q1"] = new string('x',room - room / 2);
    }
    private static ProbeV2AccessEnvelope Rebind(ProbeV2Options options,ProbeV2AccessEnvelope access)
    {
        string[] topics = ["document-events","document-events-dlq","document-state"];
        string binding = "probe-v2-access-v1\n" + options.RunId + "\n" + options.SourceCommit + "\n" + access.NamespaceId.ToLowerInvariant() + "\n" +
            "loadgen=" + access.Principals["loadgen"].ToLowerInvariant() + "\n" + "enrichment-consumer=" + access.Principals["enrichment-consumer"].ToLowerInvariant() + "\n" +
            string.Concat(topics.Select(x => x + "=" + access.Targets[x].ToLowerInvariant() + "\n"));
        return Edit(access,p => p["binding_sha256"] = Hash(Encoding.UTF8.GetBytes(binding)));
    }
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ProbeV2AccessEnvelope Edit(ProbeV2AccessEnvelope access,Action<JsonObject> edit)
    {
        var projection = JsonNode.Parse(Convert.FromBase64String(access.ProjectionBase64))!.AsObject(); edit(projection);
        byte[] bytes = Encoding.UTF8.GetBytes(projection.ToJsonString());
        return access with { ProjectionBase64 = Convert.ToBase64String(bytes),ProjectionSha256 = Hash(bytes) };
    }
}
