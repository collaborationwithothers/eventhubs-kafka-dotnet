#!/usr/bin/env python3
"""Replays the 149 C5 survivors of run 41183628251 as source edits (issue 268).

Diagnostic evidence only; never C5 acceptance. From the repository root:
  replay.py run REPORT_DIR OUTPUT_JSONL K/N [ID ...]   replay every Nth survivor from K (0-based)
  replay.py check OUTPUT_JSONL...             compare results with the disposition table
REPORT_DIR holds phase.json, publication.json, a01.json and repeat.json. Write output
outside the repository.
"""
import hashlib, json, os, re, subprocess, sys, tempfile

REPORTS = {
    "phase": ("Judge/PhaseJudge.cs", "51c2b2bf4c26262cabd2f0da5c9f8db910f6b84c614d3d85b3ec2a5f8e4c2be7"),
    "publication": ("VerifyRun/Publication/PublicationAcceptance.cs", "262ffb95e6a1a1f8362632d4b258b69354ff63013957ac47b2aab3a5fa241ac2"),
    "a01": ("VerifyRun/A01/A01EvidenceRecorder.cs", "86184deea45521b85e3f16a50c42f6ae463c6fb9be3b9aa21cd0c79a3c3eb967"),
    "repeat": ("VerifyRun/SingleAction/SingleActionRepeatJudgement.cs", "9056569722f55b72f3f58ff8ffc6c51ffe5cb84f5e803b9e3a612a6a54ff22eb"),
}
# Stryker replaces a syntax node; parentheses keep an expression replacement one node.
PLAIN = ("Statement", "Block", "String", "Linq")
# The report prints these nodes without inner parentheses. Their left operands are the
# spans of mutants 13747, 14218 and 14287 in the same reports.
OVERRIDES = {
    "13746": "(!allowed || row.ClientId is not null) && row.Terminal",
    "14217": "(!root.TryGetProperty(\"name\",out var name) || name.ValueKind != JsonValueKind.String) && "
             "!name.GetString()!.StartsWith(row.ClientId + \"#\" + kind + \"-\",StringComparison.Ordinal)",
    "14286": "(FormatException or JsonException) and InvalidOperationException",
}
TEST = ["dotnet", "test", "--project", "tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj", "-c", "Release",
        "-p:TreatWarningsAsErrors=false", "-p:WarningsAsErrors=", "-p:CodeAnalysisTreatWarningsAsErrors=false"]
TABLE = "docs/operations/verifier-mutation.md"
HEADING = "## Issue 268 survivor dispositions"
PREFIX = "Parchwell.Verifier.Tests."
ALIASES = {
    "PJ1": "Judge.PhaseJudgeTests.Given_PhaseEvidenceCase_When_Judged_Then_ReturnsExactStatusGateAndReasons",
    "PJ2": "Judge.PhaseJudgeTests.Given_TrustedWorkloadObservation_When_Judged_Then_PreservesEachRepeatObservation",
    "PJ3": "Judge.PhaseJudgeTests.Given_NullEvidence_When_Judged_Then_ThrowsForEvidenceParameter",
    "PJ4": "Judge.PhaseJudgeTests.Given_UnsupportedPhase_When_Judged_Then_ThrowsWithPhaseMessage",
    "PA1": "Judge.PublicationAcceptanceTests.Given_SingleAcceptanceFault_When_Accepting_Then_ReturnsExactCodesAndCompleteness",
    "A1": "A01.A01EvidenceRecorderTests.Given_NegativePlannedAttemptCount_When_Recorded_Then_RetainsWorkloadDiagnostic",
    "SB1": "SingleAction.SingleActionJudgementBoundaryAuditTests.Given_ReportHiddenBehindWrapSeparatorOrLaterRow_When_Judged_Then_P6GapRemains",
}
for number, name in enumerate([
        "Given_UnconstructedSlotAdmission_When_Checked_Then_ReasonFollowsAdmission",
        "Given_ConfigurationOutsideDeclaration_When_Checked_Then_FidelityFails",
        "Given_MetadataPartitionDefect_When_Checked_Then_DefectKeepsItsOwnReason",
        "Given_FailedDeliveryPersistenceStatus_When_Checked_Then_OnlyDeclaredStatusNamesAreWellFormed",
        "Given_SuccessWithoutObservedLocation_When_Checked_Then_LocationGapIsIncomplete",
        "Given_SilencePreconditionFails_When_Judged_Then_NoAbsenceOutcomeIsAssigned",
        "Given_CleanupFailureAmongOtherDiagnostics_When_Checked_Then_CompletionGapIsKept",
        "Given_NonemptyRecordValue_When_Checked_Then_ValueKindIsBytes",
        "Given_RequiredSlotWithoutClientIdentity_When_Checked_Then_NoClientIsInvented",
        "Given_FatalCallbackAroundActionWindow_When_Checked_Then_OnlyLaterCallbackCannotPrecedeDelivery",
        "Given_Base64WithIgnoredWhitespace_When_Checked_Then_NoncanonicalPayloadIsMalformed",
        "Given_SilenceStatisticsSequenceAroundZero_When_Checked_Then_OnlyNegativeIsMalformed",
        "Given_UnusableNativeReport_When_Checked_Then_NoBrokerCountIsInvented",
        "Given_BrokerSourcesAndStates_When_Checked_Then_OnlyThreeUpSourcesCount"], start=1):
    ALIASES[f"SA{number}"] = "SingleAction.ProbeV2ComparatorAuditTests." + name


def survivors(report_dir):
    found = []
    for shard, (target, digest) in REPORTS.items():
        raw = open(os.path.join(report_dir, shard + ".json"), "rb").read()
        if hashlib.sha256(raw).hexdigest() != digest:
            sys.exit(f"{shard}: report hash mismatch")
        for path, entry in json.loads(raw)["files"].items():
            if path.replace("\\", "/").endswith(target):
                found += [(target, m) for m in entry["mutants"] if m["status"] == "Survived"]
    if len(found) != 149:
        sys.exit(f"expected 149 survivors, found {len(found)}")
    return found


def run(report_dir, output, part, ids):
    k, n = (int(x) for x in part.split("/"))
    work = tempfile.mkdtemp(prefix="issue-268-replay-")
    subprocess.run(f"git archive HEAD | tar -x -C {work}", shell=True, check=True)
    env = dict(os.environ, ParchwellVerifierMutationOnly="true")
    for target, mutant in survivors(report_dir)[k::n]:
        if ids and mutant["id"] not in ids:
            continue
        path = os.path.join(work, "src/Parchwell.Verifier", target)
        original = open(path, newline="").read()
        lines = original.split("\n")
        def offset(position):
            return sum(len(x) + 1 for x in lines[:position["line"] - 1]) + position["column"] - 1
        start, end = offset(mutant["location"]["start"]), offset(mutant["location"]["end"])
        replacement = OVERRIDES.get(mutant["id"], mutant["replacement"])
        if not mutant["mutatorName"].startswith(PLAIN):
            replacement = "(" + replacement + ")"
        open(path, "w", newline="").write(original[:start] + replacement + original[end:])
        try:
            result = subprocess.run(TEST, cwd=work, env=env, capture_output=True, text=True)
        finally:
            open(path, "w", newline="").write(original)
        text = result.stdout + result.stderr
        failed = sorted({re.sub(r"\(.*", "", x) for x in re.findall(r"^failed (\S+)", text, re.M)})
        status = "compile_error" if re.search(r"error CS\d+", text) else ("killed" if result.returncode else "survived")
        with open(output, "a", encoding="utf-8") as sink:
            sink.write(json.dumps({"id": mutant["id"], "status": status, "failed_methods": failed}, sort_keys=True) + "\n")
        print(mutant["id"], status, len(failed), flush=True)


def check(outputs):
    section = open(TABLE, encoding="utf-8").read().split(HEADING, 1)[1].split("\n## ", 1)[0]
    rows = {r[1]: (r[4], r[5]) for r in (re.split(r"\s*\|\s*", line.strip())[1:-1] for line in section.splitlines())
            if len(r) == 6 and r[1].isdigit()}
    results = {}
    for output in outputs:
        for line in open(output, encoding="utf-8"):
            record = json.loads(line)
            results[record["id"]] = record
    problems = [f"{i}: missing" for i in rows if i not in results] + [f"{i}: not in table" for i in results if i not in rows]
    new = {PREFIX + name for name in ALIASES.values()}
    for i, (disposition, reference) in rows.items():
        record = results.get(i)
        if record is None:
            continue
        failed = set(record["failed_methods"])
        if disposition == "missing test":
            ok = record["status"] == "killed" and PREFIX + ALIASES[reference] in failed and failed <= new
        elif disposition == "equivalent":
            ok = record["status"] == ("compile_error" if reference == "E11" else "survived")
        else:
            ok = record["status"] == "killed" and bool(failed - new)
        if not ok:
            problems.append(f"{i}: {disposition} {reference} but {record['status']} {sorted(failed)}")
    print(f"rows {len(rows)} results {len(results)} problems {len(problems)}")
    print("\n".join(problems))
    return 1 if problems else 0


if __name__ == "__main__":
    if sys.argv[1] == "run":
        run(sys.argv[2], sys.argv[3], sys.argv[4], set(sys.argv[5:]))
    else:
        sys.exit(check(sys.argv[2:]))
