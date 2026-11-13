using System.Text;
using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Phase;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.Common.Storage;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Parchwell.Verifier.VerifyRun.Input;
using Parchwell.Verifier.VerifyRun.Judgement;
using Parchwell.Verifier.VerifyRun.Window;
using static Parchwell.Verifier.VerifyRun.Abrupt.AbruptEvidenceCodec;

namespace Parchwell.Verifier.VerifyRun.B10Final;

/// <summary>Retains authenticated input independently for the timeout and restart cohorts.</summary>
internal sealed class B10FinalInput(int number)
{
    internal int Number { get; } = number;
    internal bool Started { get; set; }
    internal ArtifactReference? Source { get; set; }
    internal List<DateTimeOffset> Completions { get; } = [];
    internal HashSet<string> Instances { get; } = new(StringComparer.Ordinal);
    internal StartLineEvidence? Start { get; set; }
    internal Dictionary<int, long>? Starts { get; set; }
    internal Dictionary<int, long>? Ends { get; set; }
    internal bool InputValid { get; set; }
    internal bool TimeoutInvalid { get; set; }
    internal string? TimeoutInstance { get; set; }
    internal IReadOnlyList<LedgerRow> Rows { get; set; } = [];
    internal TimeoutEvidence? Timeout { get; set; }
    internal List<B10FinalProducerVersion> Versions { get; } = [];
    internal JsonElement? Nonfinal { get; set; }
    internal HashSet<string> RestartReasons { get; } = new(StringComparer.Ordinal);
    internal B10FinalTermination Termination { get; set; } = new(null, null);
    internal AbruptFirstEvidence? First { get; set; }
    internal AbruptSuccessorEvidence? Successor { get; set; }
    internal string? FirstInstance { get; set; }
    internal string? SuccessorInstance { get; set; }
    internal bool RestartInputInvalid { get; set; }
    internal bool KillTooLate { get; set; }
    internal VerifierHostOptions? Options { get; set; }
}

/// <summary>Reopens immutable evidence instead of treating nonfinal classifications as producer authority.</summary>
internal sealed class B10FinalRepeatEvidence(VerifierHostOptions options, InputLimits limits, InputBudget budget,
    Func<string, CancellationToken, Task<byte[]?>> readVerifier,
    Func<string, long, CancellationToken, Task<byte[]?>> readProducer,
    Func<string, CancellationToken, Task<BlobListing>> listProducer)
{
    internal async Task ReadAsync(B10FinalInput input, CancellationToken ct)
    {
        VerifierHostOptions local = RepeatOptions(input.Number);
        input.Options = local;
        byte[]? startReceipt = await readVerifier(PhasePaths.StartLineCompletion(options.RunId!, options.Phase!, input.Number), ct).ConfigureAwait(false);
        input.Started = startReceipt is not null;
        if (!input.Started) return;
        try
        {
            StartLineEvidence start = await StartLineEvidenceReader.ReadAsync(local, limits, budget, readVerifier, ct).ConfigureAwait(false);
            RunWindow window = RunWindow.Create(start.Start, [], start.ExpectedIdentity, local.ExpectedProducerJobNames);
            Require(start.Receipt is { HandlerExitCode: 0, CleanupCompleted: true } && start.Receipt.Identity == start.ExpectedIdentity &&
                start.Start.IsSuccess && start.Start.Value?.VerifierCommit == options.SourceCommit && start.Inventory.IsSuccess &&
                start.Start.Value!.Identity == start.ExpectedIdentity && start.Start.Value.Topic == RunWindow.Topic &&
                start.Start.Value.ExpectedJobNames is not null && start.Start.Value.ExpectedJobNames.SequenceEqual(local.ExpectedProducerJobNames, StringComparer.Ordinal) &&
                window.Assessments.All(assessment => assessment.Disposition != AssessmentDisposition.Fail), "start_line");
            input.Start = start;
            input.Starts = start.Start.Value!.PartitionHighs.ToDictionary(high => high.Partition, high => high.High);
        }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct)) { }
        if (options.Phase == "C0") await ReadC0(input, local, ct).ConfigureAwait(false);
        else await ReadC1(input, local, ct).ConfigureAwait(false);
    }

    private async Task ReadC0(B10FinalInput input, VerifierHostOptions local, CancellationToken ct)
    {
        try
        {
            string resultName = PhasePaths.Result(options.RunId!, "C0", input.Number);
            byte[]? resultBytes = await readVerifier(resultName, ct).ConfigureAwait(false);
            if (resultBytes is not null) input.Source = B10FinalSweepHandler.Reference("verifier", resultName, resultBytes);
            byte[] receiptBytes = await readVerifier(PhasePaths.VerifyCompletion(options.RunId!, "C0", input.Number), ct).ConfigureAwait(false) ?? throw new FormatException("receipt_missing");
            WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(receiptBytes);
            input.Completions.Add(AbruptReconciliation.Utc(receipt.CompletedUtc));
            Require(resultBytes is not null, "result_missing");
            BlobListing listing = await listProducer(PhasePaths.ProducersPrefix(options.RunId!, "C0", input.Number), ct).ConfigureAwait(false);
            var discovered = new List<(ProducerRegistration Registration, string PathInstance)>();
            foreach (string name in listing.Names.Where(name => name.EndsWith("/registration.json", StringComparison.Ordinal)))
            {
                byte[] bytes = await RequiredProducer(name, ct).ConfigureAwait(false);
                ProducerRegistration registration = PhaseWireCodec.ReadRegistration(bytes);
                input.Instances.Add(registration.Identity.InstanceId);
                string prefix = PhasePaths.ProducersPrefix(options.RunId!, "C0", input.Number);
                Require(name.StartsWith(prefix, StringComparison.Ordinal), "registration_path");
                string[] tail = name[prefix.Length..].Split('/');
                Require(tail.Length == 2 && tail[1] == "registration.json", "registration_path");
                discovered.Add((registration, tail[0]));
            }
            IReadOnlyList<ProducerRegistration> producers = B10IntakeChecks.BindRegistrations(discovered, "B10", "C0", options.RunId!, "C0", input.Number,
                options.SourceCommit!, local.ExpectedProducerJobNames);
            ProducerRegistration producer = producers[0];
            input.TimeoutInstance = producer.Identity.InstanceId;
            VerifierResult result = VerifierResultCodec.Read(resultBytes!, resultName, limits, options.MaxDiagnosticsPerCheck!.Value).Value ?? throw new FormatException("result_invalid");
            Require(BindsReceipt(receipt, "verify", input.Number, PhaseIdentity.VerifyJobName(options.RunId!, "C0", input.Number), 1) &&
                receipt.Artifacts.Count == 1 && receipt.Artifacts[0] == input.Source && result.Identity == producer.Identity && receipt.Identity == producer.Identity &&
                result.VerifierCommit == options.SourceCommit && result.Status == VerifierResultStatus.Incomplete && result.B10WindowChecksComplete == true &&
                result.B10Intake is { Producers.Count: 1 } && receipt.B10Intake is { Producers.Count: 1 } &&
                result.B10Intake.Phase == "C0" && result.B10Intake.RepeatNumber == input.Number &&
                receipt.B10Intake.Phase == "C0" && receipt.B10Intake.RepeatNumber == input.Number, "c0_input");
            B10ProducerEvidence left = result.B10Intake!.Producers[0], right = receipt.B10Intake!.Producers[0];
            Require(left.Identity == producer.Identity && right.Identity == producer.Identity && left.JobName == producer.JobName && right.JobName == producer.JobName &&
                left.PredecessorInstanceId is null && right.PredecessorInstanceId is null && SameSnapshot(left.PublicationAcceptance, right.PublicationAcceptance), "intake");
            PublicationAcceptanceSnapshot snapshot = right.PublicationAcceptance ?? throw new FormatException("publication_missing");
            Require(input.Start is not null, "start_invalid");
            string prefixName = PhasePaths.ProducerPrefix(options.RunId!, "C0", input.Number, producer.Identity.InstanceId);
            string completionName = PhasePaths.ProducerCompletion(options.RunId!, "C0", input.Number, producer.Identity.InstanceId);
            byte[] completionBytes = await RequiredProducer(completionName, ct).ConfigureAwait(false);
            _ = PhaseWireCodec.ReadProducerCompletion(completionBytes, "C0", input.Number, producer);
            Require(snapshot.ProducerIdentity == producer.Identity && snapshot.ProducerPrefix == prefixName && snapshot.SourceCommit == options.SourceCommit &&
                snapshot.StartLine == B10FinalSweepHandler.Reference("verifier", input.Start!.StartName, input.Start.NoteBytes!) &&
                snapshot.BrokerInventory == B10FinalSweepHandler.Reference("verifier", input.Start.InventoryName, input.Start.InventoryBytes!) &&
                snapshot.ProducerReceipt == B10FinalSweepHandler.Reference("ledger", completionName, completionBytes) && snapshot.Objects.Count == 6 &&
                snapshot.Objects.Select(reference => reference.Name).SequenceEqual(TimeoutPublicationAcceptance.PublicationNames.Select(name => prefixName + "publication/" + name), StringComparer.Ordinal), "publication_binding");
            var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (ArtifactReference reference in snapshot.Objects)
            {
                byte[] bytes = await RequiredProducer(reference.Name, ct).ConfigureAwait(false);
                Require(reference == B10FinalSweepHandler.Reference("ledger", reference.Name, bytes), "publication_hash");
                raw[reference.Name[(prefixName.Length + "publication/".Length)..]] = bytes;
            }
            ParseResult<IReadOnlyList<LedgerRow>> ledger = LedgerReader.ReadRows(raw["ledger.jsonl"], limits, budget);
            ParseResult<SealedRunManifest> manifest = ManifestReader.Read(raw["manifest.json"], limits, budget, local.MaxInventoryEntries!.Value);
            Require(ledger.IsSuccess && ledger.Value is not null && manifest.IsSuccess && manifest.Value?.Identity == producer.Identity &&
                ledger.Value!.All(row => row.Identity == producer.Identity && row.Attempt.InstanceId == producer.Identity.InstanceId), "ledger_invalid");
            input.Rows = ledger.Value!;
            input.Versions.Add(Version(manifest.Value!));
            Dictionary<int, long>? starts = Vector(result.StartLine), ends = Vector(result.EndLine);
            Require(starts is not null && ends is not null && SameVector(starts, input.Start!.Start.Value!.PartitionHighs.ToDictionary(high => high.Partition, high => high.High)), "result_vector");
            input.Starts = starts;
            input.Ends = ends;
            input.InputValid = true;
        }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct)) { input.InputValid = false; }
    }

    private async Task ReadC1(B10FinalInput input, VerifierHostOptions local, CancellationToken ct)
    {
        string root = $"{options.RunId}/C1/r{input.Number}/b10-abrupt/";
        string name = root + "nonfinal-result.json";
        byte[]? dataBytes = null, receiptBytes = null;
        bool readFailed = false;
        try
        {
            dataBytes = await readVerifier(name, ct).ConfigureAwait(false);
            receiptBytes = await readVerifier(root + "verify-completion.json", ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct))
        {
            // An unreadable restart input is a restart-only failure; the ordinary timeout publication is read below.
            readFailed = true; dataBytes = null; receiptBytes = null; input.RestartInputInvalid = true;
        }
        if (dataBytes is not null) input.Source = B10FinalSweepHandler.Reference("verifier", name, dataBytes);
        try
        {
            if (receiptBytes is not null)
            {
                WorkerReceipt receipt = PhaseWireCodec.ReadReceipt(receiptBytes);
                input.Completions.Add(AbruptReconciliation.Utc(receipt.CompletedUtc));
                Require(BindsReceipt(receipt, "b10-abrupt-verify", input.Number, $"{options.RunId}-c1-r{input.Number}-b10-abrupt-verify", 0) &&
                    receipt.Identity is null && receipt.Artifacts.Count == 1 && receipt.Artifacts[0] == input.Source, "nonfinal_receipt");
            }
            Require(dataBytes is not null && receiptBytes is not null, "nonfinal_absent");
            JsonElement envelope = Parse(dataBytes!);
            Exact(envelope, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
            Require(envelope.GetProperty("schema_version").GetInt32() == 3 && Text(envelope, "dialect") == "b10_abrupt" &&
                Text(envelope, "purpose") == "scientific" && envelope.GetProperty("synthetic").GetBoolean() &&
                !envelope.GetProperty("scientific_admissible").GetBoolean() && Text(envelope, "kind") == "nonfinal_result", "nonfinal_envelope");
            JsonElement identity = envelope.GetProperty("identity");
            Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
            Require(Text(identity, "run_id") == options.RunId && Text(identity, "scenario_id") == "B10" && Text(identity, "configuration_id") == "C1" &&
                Text(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == input.Number &&
                Text(identity, "source_commit") == options.SourceCommit && Text(identity, "image_reference") == options.VerifierImage &&
                Text(identity, "job_name") == $"{options.RunId}-c1-r{input.Number}-b10-abrupt-verify" &&
                Text(identity, "pod_name") == Text(identity, "instance_id") && NullableText(identity, "predecessor_instance_id") is null, "nonfinal_identity");
            _ = Text(identity, "pod_uid");
            JsonElement data = envelope.GetProperty("data");
            Exact(data, "first_instance_id", "successor_instance_id", "timeout_instance_id", "timeout", "comparison", "native_errors", "eligibility", "eligibility_reasons", "start_line", "end_line", "window_complete", "window_limit", "arms", "problems", "problem_details", "finality", "earlier_repeat_records");
            Require(Text(data, "finality") == "nonfinal" && Text(data, "eligibility") is "pending_sweep" or "ineligible", "nonfinal_finality");
            ValidateArms(data.GetProperty("arms"));
            ValidateTimeout(data.GetProperty("timeout"));
            _ = AbruptEvidenceCodec.Strings(data.GetProperty("problems"));
            Dictionary<int, long>? starts = JsonVector(data.GetProperty("start_line")), ends = JsonVector(data.GetProperty("end_line"));
            // Validate before keeping anything: a rejected object must not install vectors, timeout counts or eligibility reasons.
            if (starts is not null && ends is not null && input.Start is not null)
                Require(SameVector(starts, input.Start.Start.Value!.PartitionHighs.ToDictionary(high => high.Partition, high => high.High)), "nonfinal_start_mismatch");
            input.Nonfinal = data;
            string[] reasons = Strings(data.GetProperty("eligibility_reasons")).ToArray();
            Require(reasons.All(B10FinalProjectionCodec.IsRestartReason), "nonfinal_eligibility_reason");
            input.RestartReasons.UnionWith(reasons);
            if (starts is not null && ends is not null) { input.Starts = starts; input.Ends = ends; }
            input.InputValid = true;
        }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct))
        { if (dataBytes is not null || receiptBytes is not null) input.RestartInputInvalid = true; }
        bool timeoutListingRead = false;
        var timeoutReader = new TimeoutEvidenceReader(local, readProducer, async (prefix, token) =>
        {
            BlobListing listing = await listProducer(prefix, token).ConfigureAwait(false);
            timeoutListingRead = true;
            return listing;
        }, limits, budget);
        try
        {
            TimeoutRegistration registration = await timeoutReader.ReadListingAsync(ct).ConfigureAwait(false);
            input.TimeoutInstance = registration.InstanceId;
            input.Instances.Add(registration.InstanceId);
            if (input.Start is not null)
            {
                input.Timeout = await timeoutReader.ReadPublicationAsync(registration, input.Start, ct).ConfigureAwait(false);
                input.Rows = input.Timeout.Ledger.Value ?? [];
                if (input.Timeout.Manifest.Value is { } manifest) input.Versions.Add(Version(manifest));
            }
        }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct))
        {
            input.TimeoutInvalid = !timeoutListingRead || timeoutReader.DirectoryInstanceIds.Count != 0;
            input.Instances.UnionWith(timeoutReader.DirectoryInstanceIds);
            input.TimeoutInstance ??= timeoutReader.DirectoryInstanceIds.Order(StringComparer.Ordinal).FirstOrDefault();
        }
        try { await ReadRestart(input, local, root, ct).ConfigureAwait(false); }
        catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct))
        {
            bool hasPerRepeatInput = dataBytes is not null || receiptBytes is not null;
            input.RestartInputInvalid |= !input.KillTooLate && (hasPerRepeatInput || input.Termination.Class == "intended_self_kill");
            if (input.KillTooLate)
            {
                if (exception is AbruptEvidenceException abrupt)
                    input.RestartReasons.UnionWith(abrupt.Reasons.Select(reason => AbruptDiagnostics.ReasonNames[reason - 1]));
                else input.RestartReasons.Add(AbruptDiagnostics.ReasonNames[exception is IOException or InvalidDataException or InputLimitExceededException ? 1 : 2]);
            }
            else if (!hasPerRepeatInput && input.Termination.Class is null)
                input.RestartReasons.Add(AbruptDiagnostics.ReasonNames[1]);
        }
        if (input.Nonfinal is { } nonfinal && input.InputValid)
        {
            try
            {
                Require(NullableText(nonfinal, "timeout_instance_id") == input.TimeoutInstance && NullableText(nonfinal, "first_instance_id") == input.First?.Pod &&
                    NullableText(nonfinal, "successor_instance_id") == input.Successor?.Pod && SameMembers(input, nonfinal), "nonfinal_restart_agreement");
            }
            catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct)) { input.RestartInputInvalid = true; }
        }
        else if (!readFailed && dataBytes is null && receiptBytes is null && input.Termination.Class is not null)
        {
            if (input.Termination.Class == "no_kill") input.RestartReasons.Add(input.Termination.Outcome!);
            else if (input.Termination.Class is "unexpected_exit" or "unknown") input.RestartReasons.Add(AbruptDiagnostics.ReasonNames[3]);
            else if (input.KillTooLate) input.RestartReasons.Add("kill_too_late");
            else input.RestartInputInvalid = true;
        }
        if (input.Nonfinal is null && input.Termination.Class is null) input.RestartReasons.Add(AbruptDiagnostics.ReasonNames[1]);
    }

    private async Task ReadRestart(B10FinalInput input, VerifierHostOptions local, string root, CancellationToken ct)
    {
        BlobListing listing = await listProducer(root, ct).ConfigureAwait(false);
        Require(listing.Names.All(name => name.StartsWith(root, StringComparison.Ordinal) && name[root.Length..].Split('/').Length == 2), "abrupt_listing");
        var groups = listing.Names.GroupBy(name => name[root.Length..].Split('/')[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var reader = new AbruptEvidenceReader(local, readProducer);
        var firsts = new List<(string Pod, string Uid)>();
        foreach (string pod in groups.Keys.Order(StringComparer.Ordinal))
        {
            try
            {
                byte[]? registration = await readProducer(root + pod + "/registration.json", 65536, ct).ConfigureAwait(false);
                if (registration is not null) ReserveScopedInstance(input, registration, pod, local);
            }
            catch (Exception exception) when (B10FinalSweepHandler.EvidenceFailure(exception, ct))
            { /* The authenticated reader below assigns the restart failure. */ }
        }
        foreach (string pod in groups.Keys.Order(StringComparer.Ordinal))
        {
            var registration = await reader.ReadScientificRegistrationAsync(root, pod, ct).ConfigureAwait(false);
            input.Instances.Add(pod);
            if (registration.Predecessor is null) firsts.Add((pod, registration.Uid));
        }
        if (firsts.Count != 1) throw new AbruptEvidenceException("first_registration_count", new HashSet<int> { 2 });
        (string firstPod, string uid) = firsts[0];
        input.FirstInstance = firstPod;
        byte[] terminationBytes = await ParentFile("termination.json", 65536).ConfigureAwait(false);
        JsonElement termination = Envelope(terminationBytes, "termination", local, firstPod, null, uid).GetProperty("data");
        byte[] manifestBytes = await ParentFile("abrupt-manifest.json", 65536).ConfigureAwait(false);
        JsonElement manifest = Envelope(manifestBytes, "abrupt_manifest", local, firstPod, null, uid).GetProperty("data");
        Bind(termination.GetProperty("manifest"), "abrupt-manifest.json", manifestBytes);
        byte[] journal = await ParentFile("journal.jsonl", 16777216).ConfigureAwait(false);
        Bind(manifest.GetProperty("journal"), "journal.jsonl", journal);
        var expectedFiles = new HashSet<string>(FirstFiles, StringComparer.Ordinal);
        foreach (JsonElement descriptor in manifest.GetProperty("report_files").EnumerateArray())
        {
            string file = Text(descriptor, "name");
            Require(ReportFiles.Contains(file, StringComparer.Ordinal) && expectedFiles.Add(file), "report_files");
            Bind(descriptor, file, await ParentFile(file, 1048576).ConfigureAwait(false));
        }
        Require(expectedFiles.SetEquals(groups[firstPod].Select(path => path[(root.Length + firstPod.Length + 1)..])), "first_object_set");
        Require(journal.Length > 0 && journal[^1] == 10, "journal_newline");
        long sequence = 0;
        int lineStart = 0;
        for (int offset = 0; offset < journal.Length; offset++)
        {
            if (journal[offset] != 10) continue;
            Require(offset - lineStart <= options.MaxRowBytes!.Value, "journal_row_bytes");
            byte[] bytes = journal[lineStart..offset];
            string kind = Text(Parse(bytes), "kind");
            JsonElement frame = Envelope(bytes, kind, local, firstPod, null, uid, true);
            Require(JournalKinds.Contains(kind, StringComparer.Ordinal) && sequence < Math.Min(4096L, options.MaxRowsPerArtifact!.Value) &&
                frame.GetProperty("sequence").GetInt64() == ++sequence, "journal_sequence");
            lineStart = offset + 1;
        }
        Require(manifest.GetProperty("journal_records").GetInt64() == sequence, "journal_records");
        string terminationClass = Text(termination, "termination");
        Require(terminationClass is "intended_self_kill" or "no_kill" or "unexpected_exit" or "unknown", "termination_class");
        input.Termination = new(terminationClass, NullableText(termination, "outcome"));
        if (terminationClass != "intended_self_kill") return;
        if (NullableText(termination, "outcome") == "kill_too_late")
        {
            input.KillTooLate = true;
            input.RestartReasons.Add("kill_too_late");
            input.First = await ReadEmptyKill(root, firstPod, uid, terminationBytes, manifest, journal, groups[firstPod], local, ct).ConfigureAwait(false);
        }
        else
        {
            if (!termination.GetProperty("successor_permitted").GetBoolean())
            {
                input.RestartInputInvalid = true;
                if (!termination.GetProperty("exit_observed").GetBoolean() || termination.GetProperty("exit_code_local_observation").GetInt32() != 137 ||
                    termination.GetProperty("capture_gaps").GetArrayLength() != 0) input.RestartReasons.Add(AbruptDiagnostics.ReasonNames[3]);
                return;
            }
            input.First = await reader.ReadFirstAsync(root, firstPod, uid, groups[firstPod], ct).ConfigureAwait(false);
            string[] successors = groups.Keys.Where(pod => pod != firstPod).ToArray();
            Require(successors.Length == 1, "successor_count");
            input.SuccessorInstance = successors[0];
            await reader.ReadRegistrationAsync(root, successors[0], firstPod, ct).ConfigureAwait(false);
            input.Successor = await reader.ReadSuccessorAsync(root, successors[0], input.First, groups[successors[0]], ct).ConfigureAwait(false);
        }
        input.RestartReasons.UnionWith(input.First.Reasons.Select(reason => AbruptDiagnostics.ReasonNames[reason - 1]));
        if (input.Successor is not null) input.RestartReasons.UnionWith(input.Successor.Reasons.Select(reason => AbruptDiagnostics.ReasonNames[reason - 1]));
        if (input.First.Invalid || input.Successor?.Invalid == true) input.RestartInputInvalid = !input.KillTooLate;

        async Task<byte[]> ParentFile(string file, long maximum)
        {
            byte[] bytes = await readProducer(root + firstPod + "/" + file, maximum, ct).ConfigureAwait(false) ?? throw new InvalidDataException("first_object_missing");
            if (bytes.Length == 0) throw new AbruptEvidenceException("first_object_empty", new HashSet<int> { 2 });
            return bytes;
        }
    }

    // A job/image binding failure affects restart eligibility, while a scoped registered instance still owns its records.
    private static void ReserveScopedInstance(B10FinalInput input, byte[] bytes, string pod, VerifierHostOptions local)
    {
        _ = PhasePaths.ProducerPrefix(local.RunId!, "C1", input.Number, pod);
        JsonElement root = Parse(bytes);
        Exact(root, "schema_version", "dialect", "purpose", "synthetic", "scientific_admissible", "kind", "identity", "data");
        Require(root.GetProperty("schema_version").GetInt32() == 3 && Text(root, "dialect") == "b10_abrupt" && Text(root, "purpose") == "scientific" &&
            root.GetProperty("synthetic").GetBoolean() && !root.GetProperty("scientific_admissible").GetBoolean() && Text(root, "kind") == "registration", "registration_scope");
        JsonElement identity = root.GetProperty("identity");
        Exact(identity, "run_id", "scenario_id", "configuration_id", "phase", "repeat", "source_commit", "image_reference", "job_name", "pod_name", "pod_uid", "instance_id", "predecessor_instance_id");
        Require(Text(identity, "run_id") == local.RunId && Text(identity, "scenario_id") == "B10" && Text(identity, "configuration_id") == "C1" &&
            Text(identity, "phase") == "C1" && identity.GetProperty("repeat").GetInt32() == input.Number && Text(identity, "pod_name") == pod && Text(identity, "instance_id") == pod,
            "registration_scope");
        foreach (string field in new[] { "source_commit", "image_reference", "job_name", "pod_uid" }) _ = Text(identity, field);
        _ = NullableText(identity, "predecessor_instance_id");
        Exact(root.GetProperty("data"), "registered_utc");
        _ = AbruptReconciliation.Utc(Text(root.GetProperty("data"), "registered_utc"));
        input.Instances.Add(pod);
    }

    // The ordinary abrupt reader requires a nonempty kill set. This path replaces only checks 7 and 11 with K.
    private async Task<AbruptFirstEvidence> ReadEmptyKill(string root, string pod, string uid, byte[] terminationBytes,
        JsonElement manifest, byte[] journal, IReadOnlyList<string> names, VerifierHostOptions local, CancellationToken ct)
    {
        JsonElement termination = Parse(terminationBytes).GetProperty("data");
        if (!termination.GetProperty("exit_observed").GetBoolean() || termination.GetProperty("exit_code_local_observation").GetInt32() != 137 ||
            termination.GetProperty("capture_gaps").GetArrayLength() != 0 || manifest.GetProperty("gaps").GetArrayLength() != 0 ||
            termination.GetProperty("kill_unconfirmed_attempt_ids").GetArrayLength() != 0 || termination.GetProperty("successor_permitted").GetBoolean())
            throw new AbruptEvidenceException("empty_kill_termination", new HashSet<int> { 4 });
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            string file = name[(root.Length + pod.Length + 1)..];
            files.Add(file, await RequiredProducer(name, ct).ConfigureAwait(false));
        }
        Require(FirstFiles.All(files.ContainsKey), "first_files");
        var expected = new HashSet<string>(FirstFiles, StringComparer.Ordinal);
        foreach (JsonElement descriptor in manifest.GetProperty("report_files").EnumerateArray())
        {
            string file = Text(descriptor, "name");
            Require(ReportFiles.Contains(file, StringComparer.Ordinal) && expected.Add(file) && files.ContainsKey(file), "report_files");
            Bind(descriptor, file, files[file]);
        }
        Require(expected.SetEquals(files.Keys), "first_files");
        Require(journal.Length > 0 && journal[^1] == 10, "journal_newline");
        var frames = new List<JsonElement>();
        foreach (string line in Encoding.UTF8.GetString(journal).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Require(frames.Count < Math.Min(4096L, options.MaxRowsPerArtifact!.Value), "journal_records");
            byte[] bytes = Encoding.UTF8.GetBytes(line);
            string kind = Text(Parse(bytes), "kind");
            JsonElement frame = Envelope(bytes, kind, local, pod, null, uid, true);
            Require(kind != "gap" && JournalKinds.Contains(kind, StringComparer.Ordinal) && frame.GetProperty("sequence").GetInt64() == frames.Count + 1L, "journal_sequence");
            frames.Add(frame);
        }
        Require(manifest.GetProperty("journal_records").GetInt64() == frames.Count, "journal_records");
        if (frames.Last(frame => ProducingKinds.Contains(Text(frame, "kind"), StringComparer.Ordinal)).GetProperty("kind").GetString() != "kill_intent")
            throw new AbruptEvidenceException("empty_kill_intent", new HashSet<int> { 4 });
        var attempts = new Dictionary<string, AbruptAttempt>(StringComparer.Ordinal);
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);
        var terminals = new Dictionary<string, AbruptTerminal>(StringComparer.Ordinal);
        string[]? control = null;
        long cut = 0;
        foreach (JsonElement frame in frames)
        {
            string kind = Text(frame, "kind");
            JsonElement data = frame.GetProperty("data");
            if (kind == "prepared")
            {
                string id = Text(data, "attempt_id"), arm = Text(data, "arm"), role = Text(data, "role"), key = Text(data, "key"), hash = Text(data, "payload_sha256");
                long ordinal = data.GetProperty("ordinal").GetInt64();
                Require(arm is "same_instance" or "cross_restart" && role is "original" or "resend" && ordinal is >= 0 and <= 4 &&
                    data.GetProperty("sequence").GetInt64() == ordinal && key == $"B10/{options.RunId}/C1/{local.Repeat}/{arm}" &&
                    hash.Length == 64 && hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
                    attempts.TryAdd(id, new(id, arm, role, ordinal, key, ordinal, hash, NullableText(data, "predecessor_attempt_id"), frame.GetProperty("sequence").GetInt64())), "prepared");
            }
            else if (kind is "accepted" or "synchronous_rejection" or "terminal")
            {
                string id = Text(data, "attempt_id");
                Require(attempts.TryGetValue(id, out AbruptAttempt? attempt), "prepared_missing");
                if (kind == "terminal") Require(terminals.TryAdd(id, new(data.GetProperty("delivered").GetBoolean(), data.GetProperty("error_code").GetInt32(),
                    data.GetProperty("persistence_status").ValueKind == JsonValueKind.Null ? null : data.GetProperty("persistence_status").GetInt32())), "terminal_duplicate");
                else Require(data.GetProperty("prepared_sequence").GetInt64() == attempt!.JournalSequence && (kind == "accepted" ? accepted : rejected).Add(id), "acceptance");
            }
            else if (kind == "cut")
            {
                Require(control is null, "cut_duplicate");
                control = Select("same_instance");
                Require(control.SequenceEqual(Strings(data.GetProperty("unconfirmed_attempt_ids")), StringComparer.Ordinal), "control_set");
                cut = frame.GetProperty("sequence").GetInt64();
            }
        }
        foreach (string arm in new[] { "same_instance", "cross_restart" })
        {
            AbruptAttempt[] originals = attempts.Values.Where(attempt => attempt.Arm == arm && attempt.Role == "original").OrderBy(attempt => attempt.JournalSequence).ToArray();
            Require(originals.Select(attempt => attempt.Ordinal).SequenceEqual(Enumerable.Range(0, 5).Select(value => (long)value)) &&
                originals.Select(attempt => attempt.PayloadSha256).Distinct(StringComparer.Ordinal).Count() == 1, "originals");
        }
        Require(control is { Length: > 0 } && control.SequenceEqual(Strings(termination.GetProperty("control_unconfirmed_attempt_ids")), StringComparer.Ordinal), "control_set");
        if (Select("cross_restart").Length != 0) throw new AbruptEvidenceException("empty_kill_set", new HashSet<int> { 4 });
        var resends = new Dictionary<string, string>(StringComparer.Ordinal);
        AbruptAttempt[] controlResends = attempts.Values.Where(attempt => attempt.Role == "resend").OrderBy(attempt => attempt.JournalSequence).ToArray();
        if (controlResends.Length != control!.Length) throw new AbruptEvidenceException("control_resend_count", new HashSet<int> { 10 });
        foreach (AbruptAttempt resend in controlResends)
        {
            Require(resend.Predecessor is not null && attempts.ContainsKey(resend.Predecessor), "control_resend_binding");
            AbruptAttempt original = attempts[resend.Predecessor!];
            Require(resend.Arm == "same_instance" && resend.JournalSequence > cut && resend.Key == original.Key && resend.Sequence == original.Sequence &&
                resend.PayloadSha256 == original.PayloadSha256 && resends.TryAdd(original.Id, resend.Id), "control_resend_binding");
        }
        Require(resends.Keys.SequenceEqual(control!, StringComparer.Ordinal), "control_resends");
        return AbruptReconciliation.First(new(pod, uid, attempts, control!, [], resends, terminals, journal, terminationBytes)
        { Frames = frames, Files = files, Accepted = accepted, Rejected = rejected }, termination);

        string[] Select(string arm) => attempts.Values.Where(attempt => attempt.Arm == arm && attempt.Role == "original" && accepted.Contains(attempt.Id) &&
            !terminals.ContainsKey(attempt.Id) && !rejected.Contains(attempt.Id)).OrderBy(attempt => attempt.JournalSequence).Select(attempt => attempt.Id).ToArray();
    }

    // The sweep later reads these counts unchecked, so a malformed object must be rejected here, inside the guarded block.
    // The contract gives the nonfinal timeout as exactly these six nonnegative counts, or null.
    internal static void ValidateTimeout(JsonElement timeout)
    {
        if (timeout.ValueKind == JsonValueKind.Null) return;
        Require(timeout.ValueKind == JsonValueKind.Object, "nonfinal_timeout");
        string[] fields = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
        Exact(timeout, fields);
        foreach (string field in fields)
        {
            JsonElement value = timeout.GetProperty(field);
            Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long count) && count >= 0, "nonfinal_timeout_count");
        }
    }

    internal static void ValidateArms(JsonElement arms)
    {
        if (arms.ValueKind == JsonValueKind.Null) return;
        Require(arms.GetArrayLength() == 2, "nonfinal_arms");
        int index = 0;
        foreach (JsonElement arm in arms.EnumerateArray())
        {
            Exact(arm, "arm", "key", "unconfirmed_count", "resend_invoked", "originals_seen", "resends_seen", "pair_count", "witness", "members",
                "unreported_attempts", "unreported_seen", "unreported_unseen", "lost_resends");
            Require(Text(arm, "arm") == (index++ == 0 ? "same_instance" : "cross_restart"), "nonfinal_arm_order");
            _ = Text(arm, "key");
            Count(arm.GetProperty("unconfirmed_count"), false);
            Count(arm.GetProperty("unreported_attempts"), false);
            foreach (string field in new[] { "resend_invoked", "originals_seen", "resends_seen", "pair_count", "unreported_seen", "unreported_unseen", "lost_resends" })
                Count(arm.GetProperty(field));
            JsonElement witness = arm.GetProperty("witness");
            if (witness.ValueKind != JsonValueKind.Null)
            {
                Exact(witness, "original_attempt_id", "resend_attempt_id", "original", "resend");
                _ = Text(witness, "original_attempt_id");
                _ = Text(witness, "resend_attempt_id");
                Coordinate(witness.GetProperty("original"));
                Coordinate(witness.GetProperty("resend"));
            }
            foreach (JsonElement member in arm.GetProperty("members").EnumerateArray())
            {
                Exact(member, "original_attempt_id", "sequence", "payload_sha256", "resend_attempt_id", "resend_terminal", "copy_count", "original_copies",
                    "resend_copies", "other_copies", "pair_count", "copies", "copies_truncated", "resend_lost");
                _ = Text(member, "original_attempt_id");
                Count(member.GetProperty("sequence"), false);
                _ = Text(member, "payload_sha256");
                _ = NullableText(member, "resend_attempt_id");
                JsonElement terminal = member.GetProperty("resend_terminal");
                if (terminal.ValueKind != JsonValueKind.Null)
                {
                    Exact(terminal, "delivered", "error_code", "persistence_status");
                    _ = terminal.GetProperty("delivered").GetBoolean();
                    _ = terminal.GetProperty("error_code").GetInt32();
                    if (terminal.GetProperty("persistence_status").ValueKind != JsonValueKind.Null) _ = terminal.GetProperty("persistence_status").GetInt32();
                }
                foreach (string field in new[] { "copy_count", "original_copies", "resend_copies", "other_copies", "pair_count" }) Count(member.GetProperty(field));
                foreach (string field in new[] { "copies_truncated", "resend_lost" })
                    if (member.GetProperty(field).ValueKind != JsonValueKind.Null) _ = member.GetProperty(field).GetBoolean();
                JsonElement copies = member.GetProperty("copies");
                if (copies.ValueKind == JsonValueKind.Null) continue;
                foreach (JsonElement copy in copies.EnumerateArray())
                {
                    Exact(copy, "partition", "offset", "instance_id", "attempt_id", "role");
                    Require(copy.GetProperty("partition").GetInt32() >= 0 && copy.GetProperty("offset").GetInt64() >= 0, "nonfinal_copy_coordinate");
                    _ = copy.GetProperty("instance_id").GetString();
                    _ = copy.GetProperty("attempt_id").GetString();
                    Require(Text(copy, "role") is "original" or "resend" or "other", "nonfinal_copy_role");
                }
            }
        }

        static void Count(JsonElement value, bool nullable = true) => Require(nullable && value.ValueKind == JsonValueKind.Null || value.GetInt64() >= 0, "nonfinal_count");
        static void Coordinate(JsonElement value)
        {
            Exact(value, "partition", "offset");
            Require(value.GetProperty("partition").GetInt32() >= 0 && value.GetProperty("offset").GetInt64() >= 0, "nonfinal_witness_coordinate");
        }
    }

    internal static bool SameMembers(B10FinalInput input, JsonElement nonfinal)
    {
        if (input.First is null) return nonfinal.GetProperty("arms").ValueKind == JsonValueKind.Null;
        JsonElement arms = nonfinal.GetProperty("arms");
        if (arms.ValueKind == JsonValueKind.Null) return false;
        foreach (JsonElement arm in arms.EnumerateArray())
        {
            string name = Text(arm, "arm");
            IReadOnlyList<string> ids = name == "same_instance" ? input.First.Control : input.First.Kill;
            IReadOnlyDictionary<string, string>? resends = name == "same_instance" ? input.First.ControlResends : input.Successor?.Resends;
            IReadOnlyDictionary<string, AbruptTerminal>? terminals = name == "same_instance" ? input.First.Terminals : input.Successor?.Terminals;
            long unreported = input.First.Attempts.Values.LongCount(attempt => attempt.Arm == name && input.First.Accepted.Contains(attempt.Id) &&
                !input.First.Terminals.ContainsKey(attempt.Id) && !input.First.Rejected.Contains(attempt.Id));
            string key = input.First.Attempts.Values.First(attempt => attempt.Arm == name && attempt.Role == "original").Key;
            if (Text(arm, "key") != key || arm.GetProperty("unconfirmed_count").GetInt64() != ids.Count ||
                arm.GetProperty("unreported_attempts").GetInt64() != unreported ||
                (arm.GetProperty("resend_invoked").ValueKind == JsonValueKind.Null ? resends is not null :
                    resends is null || arm.GetProperty("resend_invoked").GetInt64() != resends.Count)) return false;
            JsonElement[] members = arm.GetProperty("members").EnumerateArray().ToArray();
            if (!members.Select(member => Text(member, "original_attempt_id")).SequenceEqual(ids, StringComparer.Ordinal)) return false;
            foreach (JsonElement member in members)
            {
                string original = Text(member, "original_attempt_id");
                string? resend = resends?[original];
                if (NullableText(member, "resend_attempt_id") != resend || member.GetProperty("sequence").GetInt64() != input.First.Attempts[original].Sequence ||
                    Text(member, "payload_sha256") != input.First.Attempts[original].PayloadSha256) return false;
                AbruptTerminal? terminal = resend is null ? null : terminals?.GetValueOrDefault(resend);
                JsonElement supplied = member.GetProperty("resend_terminal");
                if (terminal is null ? supplied.ValueKind != JsonValueKind.Null : supplied.ValueKind == JsonValueKind.Null ||
                    supplied.GetProperty("delivered").GetBoolean() != terminal.Delivered || supplied.GetProperty("error_code").GetInt32() != terminal.ErrorCode ||
                    (supplied.GetProperty("persistence_status").ValueKind == JsonValueKind.Null ? terminal.PersistenceStatus is not null :
                        supplied.GetProperty("persistence_status").GetInt32() != terminal.PersistenceStatus)) return false;
            }
        }
        return true;
    }

    internal VerifierHostOptions RepeatOptions(int number) => new()
    {
        RunId = options.RunId, ScenarioId = "B10", ConfigurationId = options.ConfigurationId, Phase = options.Phase, Repeat = number,
        SourceCommit = options.SourceCommit, VerifierCommit = options.VerifierCommit, LoadGenImage = options.LoadGenImage, VerifierImage = options.VerifierImage,
        MaxArtifactBytes = options.MaxArtifactBytes, MaxTotalInputBytes = options.MaxTotalInputBytes, MaxInputDepth = options.MaxInputDepth,
        MaxInputEntries = options.MaxInputEntries, MaxRowBytes = options.MaxRowBytes, MaxRowsPerArtifact = options.MaxRowsPerArtifact, MaxPartitions = options.MaxPartitions,
        MaxInventoryEntries = options.MaxInventoryEntries ?? options.MaxInputEntries,
        MaxNativeReportBytes = options.MaxNativeReportBytes ?? checked((int)Math.Min(options.MaxArtifactBytes!.Value, int.MaxValue)),
        MaxBrokerEntries = options.MaxBrokerEntries ?? checked((int)Math.Min(options.MaxInputEntries!.Value, int.MaxValue)),
        MaxOutputBytes = options.MaxOutputBytes, MaxDiagnosticsPerCheck = options.MaxDiagnosticsPerCheck, MaxDiagnosticBytes = options.MaxDiagnosticBytes
    };
    private bool BindsReceipt(WorkerReceipt receipt, string mode, int number, string job, int exit) => receipt.Mode == mode && receipt.ScenarioId == "B10" &&
        receipt.ConfigurationId == options.ConfigurationId && receipt.RunId == options.RunId && receipt.Phase == options.Phase && receipt.Repeat == number &&
        receipt.JobName == job && receipt.SourceCommit == options.SourceCommit && receipt.HandlerExitCode == exit && receipt.CleanupCompleted;
    private async Task<byte[]> RequiredProducer(string name, CancellationToken ct) =>
        await readProducer(name, limits.MaxArtifactBytes, ct).ConfigureAwait(false) ?? throw new InvalidDataException("producer_object_missing");
    private static bool SameSnapshot(PublicationAcceptanceSnapshot? left, PublicationAcceptanceSnapshot? right) => left is null ? right is null : right is not null &&
        left.ProducerPrefix == right.ProducerPrefix && left.ProducerIdentity == right.ProducerIdentity && left.SourceCommit == right.SourceCommit &&
        left.StartLine == right.StartLine && left.BrokerInventory == right.BrokerInventory && left.ProducerReceipt == right.ProducerReceipt && left.Objects.SequenceEqual(right.Objects);
    private static B10FinalProducerVersion Version(SealedRunManifest manifest) => new(manifest.Identity.InstanceId, manifest.GeneratorVersion,
        manifest.RuntimeVersions.GetValueOrDefault("confluent_kafka"), manifest.RuntimeVersions.GetValueOrDefault("librdkafka"), manifest.RuntimeVersions.GetValueOrDefault("dotnet"));
    internal static Dictionary<int, long>? Vector(IReadOnlyList<WatermarkOffset>? values) => values?.ToDictionary(value => value.Partition, value => value.Offset);
    private static Dictionary<int, long>? JsonVector(JsonElement values) => values.ValueKind == JsonValueKind.Null ? null :
        values.EnumerateArray().ToDictionary(value => value.GetProperty("partition").GetInt32(), value => value.GetProperty("offset").GetInt64());
    private static bool SameVector(Dictionary<int, long> left, Dictionary<int, long> right) => left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out long value) && value == pair.Value);
}
