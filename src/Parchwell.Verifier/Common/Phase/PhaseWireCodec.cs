using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Common.Phase;

internal sealed class PhaseWireIdentityException(string message) : FormatException(message);

/// <summary>Closed, bounded codecs for C5 registration and worker receipts.</summary>
internal static partial class PhaseWireCodec
{
    internal const int MaximumBytes = 65_536;
    private static readonly HashSet<string> RegistrationFields = ["schema_version", "identity", "phase", "repeat_number", "job_name", "pod_name", "pod_uid", "source_commit"];
    private static readonly HashSet<string> ProducerCompletionFields = ["schema_version", "identity", "phase", "repeat_number", "job_name", "pod_name", "pod_uid", "source_commit", "mode", "handler_exit_code", "cleanup_completed"];
    private static readonly HashSet<string> ReceiptFields = ["schema_version", "mode", "scenario_id", "configuration_id", "run_id", "phase", "repeat", "job_name", "pod_name", "identity", "source_commit", "handler_exit_code", "cleanup_completed", "started_utc", "completed_utc", "artifacts", "publication_acceptance", "repeat_checks"];

    internal static byte[] WriteRegistration(ProducerRegistration value)
    {
        ArgumentNullException.ThrowIfNull(value); ValidateRegistration(value);
        return Write(writer =>
        {
        writer.WriteStartObject(); writer.WriteNumber("schema_version", value.SchemaVersion); WriteIdentity(writer, value.Identity);
        writer.WriteString("phase", value.Phase); writer.WriteNumber("repeat_number", value.RepeatNumber); writer.WriteString("job_name", value.JobName);
        writer.WriteString("pod_name", value.PodName); writer.WriteString("pod_uid", value.PodUid); writer.WriteString("source_commit", value.SourceCommit); writer.WriteEndObject();
        });
    }

    internal static ProducerRegistration ReadRegistration(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = Parse(bytes);
        JsonElement root = document.RootElement; Exact(root, RegistrationFields);
        var value = new ProducerRegistration(Int(root, "schema_version"), ReadIdentity(Prop(root, "identity")), Str(root, "phase"), Int(root, "repeat_number"), Str(root, "job_name"), Str(root, "pod_name"), Str(root, "pod_uid"), Str(root, "source_commit"));
        ValidateUntrustedCoordinates(value.Phase, value.RepeatNumber, "Registration");
        ValidateRegistration(value);
        return value;
    }

    internal static byte[] WriteProducerCompletion(ProducerCompletionReceipt value) => Write(writer =>
    {
        writer.WriteStartObject(); writer.WriteNumber("schema_version", value.SchemaVersion); WriteIdentity(writer, value.Identity);
        writer.WriteString("phase", value.Phase); writer.WriteNumber("repeat_number", value.RepeatNumber); writer.WriteString("job_name", value.JobName);
        writer.WriteString("pod_name", value.PodName); writer.WriteString("pod_uid", value.PodUid); writer.WriteString("source_commit", value.SourceCommit);
        writer.WriteString("mode", value.Mode); writer.WriteNumber("handler_exit_code", value.HandlerExitCode); writer.WriteBoolean("cleanup_completed", value.CleanupCompleted); writer.WriteEndObject();
    });

    internal static ProducerCompletionReceipt ReadProducerCompletion(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = Parse(bytes); JsonElement root = document.RootElement; Exact(root, ProducerCompletionFields);
        var value = new ProducerCompletionReceipt(Int(root, "schema_version"), ReadIdentity(Prop(root, "identity")), Str(root, "phase"), Int(root, "repeat_number"), Str(root, "job_name"), Str(root, "pod_name"), Str(root, "pod_uid"), Str(root, "source_commit"), Str(root, "mode"), Int(root, "handler_exit_code"), Bool(root, "cleanup_completed"));
        ValidateUntrustedCoordinates(value.Phase, value.RepeatNumber, "Producer completion receipt");
        if (!PhaseIdentity.IsRunId(value.Identity.RunId) || value.RepeatNumber is < 1 or > 5 ||
            value.JobName != PhaseIdentity.JobName(value.Identity.RunId, value.Phase, value.RepeatNumber))
            throw new PhaseWireIdentityException("Producer completion receipt does not bind to its repeat.");
        if (value.Identity.ScenarioId != "A01" ||
            value.Identity.ConfigurationId != PhaseIdentity.ConfigurationFor(value.Phase) ||
            value.Identity.InstanceId != value.Identity.ExpectedInstanceId)
            throw new PhaseWireIdentityException("Producer completion receipt identity does not match its phase.");
        ValidateRegistration(new ProducerRegistration(value.SchemaVersion, value.Identity, value.Phase, value.RepeatNumber, value.JobName, value.PodName, value.PodUid, value.SourceCommit));
        if (value.Mode != "loadgen" || value.HandlerExitCode != 0 || !value.CleanupCompleted) throw new FormatException("Producer completion receipt does not establish successful cleanup.");
        return value;
    }

    internal static byte[] WriteReceipt(WorkerReceipt value)
    {
        ArgumentNullException.ThrowIfNull(value); ValidateReceipt(value);
        return Write(writer => WriteReceiptJson(writer, value));
    }

    internal static WorkerReceipt ReadReceipt(ReadOnlyMemory<byte> bytes)
    {
        using JsonDocument document = Parse(bytes); JsonElement root = document.RootElement; Exact(root, ReceiptFields);
        string mode = Str(root, "mode");
        JsonElement repeatElement = Prop(root, "repeat");
        int? repeat = repeatElement.ValueKind == JsonValueKind.Null ? null : Integer(repeatElement);
        JsonElement identityElement = Prop(root, "identity");
        RunIdentity? identity = identityElement.ValueKind == JsonValueKind.Null ? null : ReadIdentity(identityElement);
        JsonElement acceptanceElement = Prop(root, "publication_acceptance");
        PublicationAcceptanceSnapshot? acceptance = acceptanceElement.ValueKind == JsonValueKind.Null ? null : ReadAcceptance(acceptanceElement);
        JsonElement checksElement = Prop(root, "repeat_checks");
        RepeatChecks? checks = checksElement.ValueKind == JsonValueKind.Null ? null : ReadChecks(checksElement);
        JsonElement artifacts = Prop(root, "artifacts");
        if (artifacts.ValueKind != JsonValueKind.Array) throw new FormatException("Receipt artifacts must be an array.");
        var refs = artifacts.EnumerateArray().Select(ReadReference).ToArray();
        var receipt = new WorkerReceipt(Int(root, "schema_version"), mode, Str(root, "scenario_id"), Str(root, "configuration_id"), Str(root, "run_id"), Str(root, "phase"), repeat, Str(root, "job_name"), Str(root, "pod_name"), identity, Str(root, "source_commit"), Int(root, "handler_exit_code"), Bool(root, "cleanup_completed"), Str(root, "started_utc"), Str(root, "completed_utc"), refs, acceptance, checks);
        ValidateUntrustedCoordinates(receipt.Phase, receipt.Repeat, "Receipt");
        try { ValidateReceipt(receipt); }
        catch (ArgumentException ex) { throw new PhaseWireIdentityException($"Receipt identity is invalid: {ex.Message}"); }
        return receipt;
    }

    private static void ValidateUntrustedCoordinates(string phase, int? repeat, string subject)
    {
        try
        {
            PhaseIdentity.ValidatePhase(phase);
            if (repeat is int value) PhaseIdentity.ValidateRepeat(value);
        }
        catch (ArgumentException ex)
        {
            // Shared identity helpers use argument exceptions for trusted
            // callers. At this wire boundary the fields are untrusted.
            throw new PhaseWireIdentityException($"{subject} phase or repeat coordinate is invalid: {ex.Message}");
        }
    }

    private static void WriteReceiptJson(Utf8JsonWriter w, WorkerReceipt r)
    {
        w.WriteStartObject(); w.WriteNumber("schema_version", r.SchemaVersion); w.WriteString("mode", r.Mode); w.WriteString("scenario_id", r.ScenarioId); w.WriteString("configuration_id", r.ConfigurationId); w.WriteString("run_id", r.RunId); w.WriteString("phase", r.Phase);
        if (r.Repeat is int repeat) w.WriteNumber("repeat", repeat); else w.WriteNull("repeat"); w.WriteString("job_name", r.JobName); w.WriteString("pod_name", r.PodName);
        if (r.Identity is null) w.WriteNull("identity"); else WriteIdentity(w, r.Identity); w.WriteString("source_commit", r.SourceCommit); w.WriteNumber("handler_exit_code", r.HandlerExitCode); w.WriteBoolean("cleanup_completed", r.CleanupCompleted); w.WriteString("started_utc", r.StartedUtc); w.WriteString("completed_utc", r.CompletedUtc);
        w.WritePropertyName("artifacts"); w.WriteStartArray(); foreach (var a in r.Artifacts) WriteReference(w, a); w.WriteEndArray();
        if (r.PublicationAcceptance is null) w.WriteNull("publication_acceptance"); else WriteAcceptance(w, r.PublicationAcceptance);
        if (r.RepeatChecks is null) w.WriteNull("repeat_checks"); else { w.WritePropertyName("repeat_checks"); w.WriteStartObject(); w.WriteString("producer_job_binding", r.RepeatChecks.ProducerJobBinding); w.WriteString("registered_producer_count", r.RepeatChecks.RegisteredProducerCount); w.WriteString("broker_inventory_coverage", r.RepeatChecks.BrokerInventoryCoverage); w.WriteEndObject(); }
        w.WriteEndObject();
    }

    private static void ValidateReceipt(WorkerReceipt r)
    {
        if (r.Mode == "start-line" && (r.Phase is "probe" or "C0" or "C3") && (r.ScenarioId != "A01" || r.ConfigurationId != PhaseIdentity.ConfigurationFor(r.Phase) ||
            r.Identity is { } startIdentity && (startIdentity.ScenarioId != r.ScenarioId || startIdentity.ConfigurationId != r.ConfigurationId ||
                startIdentity.RunId != r.RunId || startIdentity.InstanceId != startIdentity.ExpectedInstanceId)))
            throw new PhaseWireIdentityException("Start-line receipt identity does not bind to its phase.");
        if (r.SchemaVersion != 1 || r.Mode is not ("start-line" or "verify" or "judge") || !PhaseIdentity.IsRunId(r.RunId) || r.SourceCommit.Length != 40 || !CommitRegex().IsMatch(r.SourceCommit)) throw new FormatException("Receipt has invalid fixed identity fields.");
        PhaseIdentity.ValidatePhase(r.Phase);
        if (r.ConfigurationId != PhaseIdentity.ConfigurationFor(r.Phase) || r.ScenarioId != "A01") throw new FormatException("Receipt scenario or configuration does not match its phase.");
        if (r.Mode == "judge" ? r.Repeat is not null || r.Identity is not null : r.Repeat is null || r.Identity is null) throw new FormatException("Receipt nullable identity fields do not match its mode.");
        if (r.Repeat is int repeat) PhaseIdentity.ValidateRepeat(repeat);
        if (!Text(r.JobName,253,true) || !Text(r.PodName,253,true)) throw new FormatException("Receipt Job and pod names must be bounded text.");
        DateTimeOffset started = ParseUtc(r.StartedUtc); DateTimeOffset completed = ParseUtc(r.CompletedUtc);
        if (completed < started || !r.CleanupCompleted || r.HandlerExitCode is not (0 or 1)) throw new FormatException("Receipt timing, exit, or cleanup fields are invalid.");
        if (r.Mode == "start-line" && r.HandlerExitCode != 0) throw new FormatException("Start-line receipt must record successful handler completion.");
        if (r.Mode == "verify" && (r.RepeatChecks is null || (r.PublicationAcceptance is not null && r.PublicationAcceptance.Objects.Count != 6))) throw new FormatException("Verify receipt acceptance fields are invalid.");
        if (r.Mode != "verify" && (r.PublicationAcceptance is not null || r.RepeatChecks is not null)) throw new FormatException("Only verify receipts can carry acceptance fields.");
        if (r.Mode == "judge" && r.Artifacts.Count != 1 || r.Mode == "start-line" && r.Artifacts.Count != 2 || r.Mode == "verify" && r.Artifacts.Count != 1) throw new FormatException("Receipt artifact list has the wrong role-specific size.");
        if (r.Mode == "judge")
        {
            ArtifactReference candidate=r.Artifacts[0];
            if (r.JobName != PhaseIdentity.JudgeJobName(r.RunId,r.Phase) || candidate.Container != "verifier" || candidate.Name != PhasePaths.Candidate(r.RunId,r.Phase))
                throw new FormatException("Judge receipt does not bind its fixed candidate path.");
        }
        else
        {
            RunIdentity identity=r.Identity!;
            if (identity.ScenarioId != "A01" || identity.ConfigurationId != r.ConfigurationId || identity.RunId != r.RunId ||
                identity.InstanceId != identity.ExpectedInstanceId || !Segment(identity.InstanceId) || !Text(identity.ClientId,253,false))
                throw new FormatException("Receipt producer identity does not bind to its run.");
            int repeatNumber=r.Repeat!.Value;
            if (r.Mode=="start-line")
            {
                string job=PhaseIdentity.JobName(r.RunId,r.Phase,repeatNumber);
                if (r.JobName!=PhaseIdentity.StartLineJobName(r.RunId,r.Phase,repeatNumber) || identity.InstanceId!=job || identity.ExpectedInstanceId!=job || identity.ClientId!=job)
                    throw new PhaseWireIdentityException("Start-line receipt does not bind to its expected Job identity.");
                if (
                    r.Artifacts[0].Container!="verifier" || r.Artifacts[0].Name!=PhasePaths.StartLine(r.RunId,r.Phase,repeatNumber) ||
                    r.Artifacts[1].Container!="verifier" || r.Artifacts[1].Name!=PhasePaths.BrokerInventory(r.RunId,r.Phase,repeatNumber))
                    throw new FormatException("Start-line receipt does not bind its expected Job and fixed artifacts.");
            }
            else
            {
                string result=PhasePaths.Result(r.RunId,r.Phase,repeatNumber);
                if (r.JobName!=PhaseIdentity.VerifyJobName(r.RunId,r.Phase,repeatNumber) || r.Artifacts[0].Container!="verifier" || r.Artifacts[0].Name!=result)
                    throw new FormatException("Verify receipt does not bind its fixed result path.");
                if (r.PublicationAcceptance is not null) ValidateAcceptance(r,r.PublicationAcceptance);
            }
        }
    }

    private static void ValidateAcceptance(WorkerReceipt receipt,PublicationAcceptanceSnapshot acceptance)
    {
        int repeat=receipt.Repeat!.Value;
        RunIdentity identity=receipt.Identity!;
        string prefix=PhasePaths.ProducerPrefix(receipt.RunId,receipt.Phase,repeat,identity.InstanceId);
        if(acceptance.ProducerPrefix!=prefix || acceptance.ProducerIdentity!=identity || acceptance.SourceCommit!=receipt.SourceCommit ||
            acceptance.StartLine.Container!="verifier" || acceptance.StartLine.Name!=PhasePaths.StartLine(receipt.RunId,receipt.Phase,repeat) ||
            acceptance.BrokerInventory.Container!="verifier" || acceptance.BrokerInventory.Name!=PhasePaths.BrokerInventory(receipt.RunId,receipt.Phase,repeat) ||
            acceptance.ProducerReceipt.Container!="ledger" || acceptance.ProducerReceipt.Name!=PhasePaths.ProducerCompletion(receipt.RunId,receipt.Phase,repeat,identity.InstanceId) ||
            acceptance.Objects.Count!=6 || !acceptance.Objects.Select(x=>(x.Container,x.Name)).SequenceEqual(
                PublicationPaths(receipt.RunId,receipt.Phase,repeat,identity.InstanceId),EqualityComparer<(string,string)>.Default))
            throw new FormatException("Verify publication acceptance does not bind its fixed evidence paths.");
    }

    private static IEnumerable<(string Container,string Name)> PublicationPaths(string run,string phase,int repeat,string instance)
    {
        foreach(string name in new[]{"index.json","ledger.jsonl","manifest.json","statistics.jsonl","request-lifecycle.jsonl","capabilities.json"})
            yield return ("ledger",PhasePaths.Publication(run,phase,repeat,instance,name));
    }

    private static void ValidateRegistration(ProducerRegistration v)
    {
        if (v.SchemaVersion != 1 || !PhaseIdentity.IsRunId(v.Identity.RunId) || v.Identity.ScenarioId != "A01" || v.Identity.ConfigurationId != PhaseIdentity.ConfigurationFor(v.Phase) || v.Identity.InstanceId != v.Identity.ExpectedInstanceId || !Text(v.Identity.ClientId,253,true) || v.RepeatNumber is < 1 or > 5 || v.JobName != PhaseIdentity.JobName(v.Identity.RunId, v.Phase, v.RepeatNumber) || !Segment(v.Identity.InstanceId) || !Text(v.PodName,253,true) || !Text(v.PodUid,253,false) || v.PodUid.Any(char.IsWhiteSpace) || !CommitRegex().IsMatch(v.SourceCommit)) throw new FormatException("Producer registration is malformed or does not bind to its repeat.");
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        if (!DateTimeOffset.TryParseExact(value, "O", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset result) || result.Offset != TimeSpan.Zero) throw new FormatException("Receipt timestamp must be a UTC round-trip value.");
        return result;
    }

    private static bool Segment(string x) => x.Length is > 0 and <= 128 && x.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool Text(string x, int max, bool controls) => x.Length is > 0 && x.Length <= max && !x.Any(c => char.IsControl(c) || (controls && c is '/' or '\\')) && !string.IsNullOrWhiteSpace(x);

    private static byte[] Write(Action<Utf8JsonWriter> action) => DeploymentJson.Write(MaximumBytes, action);
    private static JsonDocument Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBytes) throw new InputLimitExceededException("Phase wire object exceeds 65536 bytes.");
        var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        try { CheckDuplicates(document.RootElement); return document; } catch { document.Dispose(); throw; }
    }
    private static void CheckDuplicates(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object) { var names = new HashSet<string>(StringComparer.Ordinal); foreach (var p in e.EnumerateObject()) { if (!names.Add(p.Name)) throw new FormatException("Duplicate phase wire property."); CheckDuplicates(p.Value); } }
        else if (e.ValueKind == JsonValueKind.Array) foreach (var x in e.EnumerateArray()) CheckDuplicates(x);
    }
    private static void Exact(JsonElement e, HashSet<string> fields) { if (e.ValueKind != JsonValueKind.Object || e.EnumerateObject().Any(p => !fields.Contains(p.Name)) || fields.Any(f => !e.TryGetProperty(f, out _))) throw new FormatException("Phase wire object has missing or unknown fields."); }
    private static JsonElement Prop(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v : throw new FormatException($"Missing {name}.");
    private static string Str(JsonElement e, string n) => Prop(e,n).ValueKind == JsonValueKind.String ? Prop(e,n).GetString()! : throw new FormatException($"{n} must be a string.");
    private static int Int(JsonElement e, string n) => Integer(Prop(e,n));
    private static int Integer(JsonElement e) => e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n) ? n : throw new FormatException("Expected a 32-bit integer.");
    private static bool Bool(JsonElement e, string n) => Prop(e,n).ValueKind is JsonValueKind.True or JsonValueKind.False ? Prop(e,n).GetBoolean() : throw new FormatException($"{n} must be boolean.");

    private static void WriteIdentity(Utf8JsonWriter w, RunIdentity i) { w.WritePropertyName("identity"); w.WriteStartObject(); w.WriteString("scenario_id", i.ScenarioId); w.WriteString("configuration_id", i.ConfigurationId); w.WriteString("run_id", i.RunId); w.WriteString("instance_id", i.InstanceId); w.WriteString("expected_instance_id", i.ExpectedInstanceId); w.WriteString("client_id", i.ClientId); w.WriteEndObject(); }
    private static RunIdentity ReadIdentity(JsonElement e) { Exact(e,["scenario_id","configuration_id","run_id","instance_id","expected_instance_id","client_id"]); return new(Str(e,"scenario_id"),Str(e,"configuration_id"),Str(e,"run_id"),Str(e,"instance_id"),Str(e,"expected_instance_id"),Str(e,"client_id")); }
    private static void WriteReference(Utf8JsonWriter w, ArtifactReference a) { w.WriteStartObject(); w.WriteString("container",a.Container); w.WriteString("name",a.Name); w.WriteNumber("length",a.Length); w.WriteString("sha256",a.Sha256); w.WriteEndObject(); }
    private static ArtifactReference ReadReference(JsonElement e) { Exact(e,["container","name","length","sha256"]); var length=Prop(e,"length"); var sha=Str(e,"sha256"); var value=new ArtifactReference(Str(e,"container"),Str(e,"name"),length.ValueKind==JsonValueKind.Number&&length.TryGetInt64(out var n)?n:throw new FormatException("length must be Int64."),sha); if(value.Container is not ("ledger" or "verifier")||value.Length<0||!ShaRegex().IsMatch(sha)) throw new FormatException("Artifact reference is malformed."); return value; }
    private static void WriteAcceptance(Utf8JsonWriter w, PublicationAcceptanceSnapshot a) { w.WritePropertyName("publication_acceptance"); w.WriteStartObject(); w.WriteString("status","accepted"); w.WriteString("producer_prefix",a.ProducerPrefix); w.WritePropertyName("producer_identity"); WriteIdentityValue(w,a.ProducerIdentity); w.WriteString("source_commit",a.SourceCommit); w.WritePropertyName("start_line"); WriteReference(w,a.StartLine); w.WritePropertyName("broker_inventory"); WriteReference(w,a.BrokerInventory); w.WritePropertyName("producer_receipt"); WriteReference(w,a.ProducerReceipt); w.WritePropertyName("objects"); w.WriteStartArray(); foreach(var o in a.Objects) WriteReference(w,o); w.WriteEndArray(); w.WriteEndObject(); }
    private static PublicationAcceptanceSnapshot ReadAcceptance(JsonElement e) { Exact(e,["status","producer_prefix","producer_identity","source_commit","start_line","broker_inventory","producer_receipt","objects"]); if(Str(e,"status")!="accepted") throw new FormatException("Publication acceptance status must be accepted."); JsonElement list=Prop(e,"objects"); if(list.ValueKind!=JsonValueKind.Array) throw new FormatException("Accepted objects must be an array."); return new(Str(e,"producer_prefix"),ReadIdentity(Prop(e,"producer_identity")),Str(e,"source_commit"),ReadReference(Prop(e,"start_line")),ReadReference(Prop(e,"broker_inventory")),ReadReference(Prop(e,"producer_receipt")),list.EnumerateArray().Select(ReadReference).ToArray()); }
    private static void WriteIdentityValue(Utf8JsonWriter w, RunIdentity i) { w.WriteStartObject(); w.WriteString("scenario_id", i.ScenarioId); w.WriteString("configuration_id", i.ConfigurationId); w.WriteString("run_id", i.RunId); w.WriteString("instance_id", i.InstanceId); w.WriteString("expected_instance_id", i.ExpectedInstanceId); w.WriteString("client_id", i.ClientId); w.WriteEndObject(); }
    private static RepeatChecks ReadChecks(JsonElement e) { Exact(e,["producer_job_binding","registered_producer_count","broker_inventory_coverage"]); string[] vals=[Str(e,"producer_job_binding"),Str(e,"registered_producer_count"),Str(e,"broker_inventory_coverage")]; if(vals.Any(v=>v is not ("pass" or "fail" or "unavailable"))) throw new FormatException("Repeat check status is invalid."); return new(vals[0],vals[1],vals[2]); }
    [GeneratedRegex("\\A[a-f0-9]{40}\\z",RegexOptions.CultureInvariant)] private static partial Regex CommitRegex();
    [GeneratedRegex("\\A[a-f0-9]{64}\\z",RegexOptions.CultureInvariant)] private static partial Regex ShaRegex();
}
