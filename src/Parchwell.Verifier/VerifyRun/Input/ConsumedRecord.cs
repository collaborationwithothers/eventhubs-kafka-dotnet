using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.VerifyRun.Input;

/// <summary>Retains one already window-limited consumed record and its header or decoding problems.</summary>
/// <remarks>Repeated or missing identity headers remain explicit problems rather than being collapsed into a dictionary.</remarks>
internal sealed record ConsumedRecord(
    string Topic,
    int Partition,
    long Offset,
    string? Key,
    string? InstanceId,
    string? AttemptId,
    long? Sequence,
    IReadOnlyList<StructuralProblem> Problems);
