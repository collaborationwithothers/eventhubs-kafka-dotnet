using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.ProbeGate;

/// <summary>Supplies independently attributed repeats to the local gate judgement.</summary>
internal sealed record ProbeGateRequest(
    Parchwell.Scenarios.ProbeGate Gate,
    IReadOnlyList<RunIdentity> ExpectedIdentities,
    IReadOnlyList<ProbeGateInput> Inputs);

/// <summary>Retains a supplied path's read outcome without inventing an identity.</summary>
internal sealed record ProbeGateInput(string Path, ProbeGateInputStatus Status, VerifierResult? Result);

/// <summary>Distinguishes missing, unreadable and decoded repeat evidence.</summary>
internal enum ProbeGateInputStatus
{
    Readable,
    Missing,
    Unreadable
}
