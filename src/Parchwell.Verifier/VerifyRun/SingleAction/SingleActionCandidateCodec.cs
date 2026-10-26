using Parchwell.Verifier.Common.SingleAction;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal static class SingleActionCandidateCodec
{
    internal static byte[] Write(SingleActionCandidate candidate) => SingleActionWire.Write(candidate,16777216);
    internal static SingleActionCandidate Read(byte[] bytes) => SingleActionWire.Read<SingleActionCandidate>(bytes,16777216);
}
