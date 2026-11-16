namespace Parchwell.Verifier.Common.Consumer;

/// <summary>Defines the fixed row and blob bounds of the consumer record wire format.</summary>
internal static class ConsumerRecordLimits
{
    internal const int MaximumRowBytes=65536;
    internal const int MaximumRowsPerBlob=40000;
    internal const long MaximumBlobBytes=(long)MaximumRowBytes*MaximumRowsPerBlob;
}
