using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Window;

/// <summary>Strictly reads the independent start-line broker inventory without contacting Kafka.</summary>
internal static class BrokerInventoryReader
{
    internal const int MaximumBrokers = 64;
    private static readonly IReadOnlySet<string> RootFields = new HashSet<string>(StringComparer.Ordinal) { "schema_version", "identity", "brokers", "source", "source_commit" };
    private static readonly IReadOnlySet<string> BrokerFields = new HashSet<string>(StringComparer.Ordinal) { "id", "host", "port", "name" };
    private static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.Ordinal) { "scenario_id", "configuration_id", "run_id", "instance_id", "expected_instance_id", "client_id" };

    internal static ParseResult<BrokerInventory> Read(ReadOnlyMemory<byte> bytes, string artifact, RunIdentity expectedStartIdentity, string expectedCommit, InputLimits limits)
    {
        ParseResult<JsonDocument> parsed = StrictJson.ParseDocument(bytes, artifact, limits, checkNumber: null);
        if (!parsed.IsSuccess) return ParseResult<BrokerInventory>.Failure(parsed.Problems);
        using JsonDocument document = parsed.Value!;
        JsonElement root = document.RootElement;
        var problems = StrictJson.ValidateProperties(root, "$", RootFields, RootFields, artifact, null);
        if (problems.Count > 0) return ParseResult<BrokerInventory>.Failure(problems);
        if (!Int(root, "schema_version", out int version) || version != 1) return Fail("schema_version");
        JsonElement id = root.GetProperty("identity");
        problems = StrictJson.ValidateProperties(id, "identity", IdentityFields, IdentityFields, artifact, null);
        if (problems.Count > 0) return ParseResult<BrokerInventory>.Failure(problems);
        if (!TryIdentity(id, out RunIdentity? identity)) return Fail("identity");
        if (identity != expectedStartIdentity) return Fail("identity");
        if (!String(root,"source",out string? source) || source!="kafka_metadata") return Fail("source");
        if (!String(root,"source_commit",out string? commit) || commit!=expectedCommit) return Fail("source_commit");
        JsonElement array=root.GetProperty("brokers");
        if(array.ValueKind!=JsonValueKind.Array || array.GetArrayLength() is 0 or > MaximumBrokers) return Fail("brokers");
        var entries=new List<BrokerInventoryEntry>(array.GetArrayLength()); var ids=new HashSet<int>(); var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(JsonElement item in array.EnumerateArray())
        {
            problems=StrictJson.ValidateProperties(item,"brokers",BrokerFields,BrokerFields,artifact,null);
            if(problems.Count>0)return ParseResult<BrokerInventory>.Failure(problems);
            if(!Int(item,"id",out int brokerId)||!String(item,"host",out string? host)||!Int(item,"port",out int port)||!String(item,"name",out string? name))return Fail("broker");
            if(brokerId<0 || port is <1 or >65535 || string.IsNullOrWhiteSpace(host)||host.Length>253||host.Any(char.IsWhiteSpace)||Uri.CheckHostName(host)==UriHostNameType.Unknown)return Fail("broker");
            string node=IPAddress.TryParse(host,out var address)&&address.AddressFamily==AddressFamily.InterNetworkV6?$"[{host.Trim('[',']')}]":host;
            string expected=string.Create(CultureInfo.InvariantCulture,$"sasl_ssl://{node}:{port}/{brokerId}");
            if(name!=expected||!ids.Add(brokerId)||!names.Add(name))return Fail("broker");
            entries.Add(new(brokerId,host,port,name));
        }
        return ParseResult<BrokerInventory>.Success(new(version,identity!,entries,source!,commit!));

        ParseResult<BrokerInventory> Fail(string field)=>ParseResult<BrokerInventory>.Failure(new StructuralProblem(null,"invalid_broker_inventory",artifact,null,field,null));
    }

    private static bool TryIdentity(JsonElement e,out RunIdentity? identity)
    {
        identity=null;
        if(!String(e,"scenario_id",out var s)||!String(e,"configuration_id",out var c)||!String(e,"run_id",out var r)||!String(e,"instance_id",out var i)||!String(e,"expected_instance_id",out var x)||!String(e,"client_id",out var cl))return false;
        identity=new(s!,c!,r!,i!,x!,cl!);return true;
    }
    private static bool String(JsonElement e,string n,out string? v){if(e.TryGetProperty(n,out var x)&&StrictJson.TryGetString(x,out var s)){v=s;return true;}v=null;return false;}
    private static bool Int(JsonElement e,string n,out int v){if(e.TryGetProperty(n,out var x)&&StrictJson.TryGetInt32(x,out v))return true;v=0;return false;}
}
