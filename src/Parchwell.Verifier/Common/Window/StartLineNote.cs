using Parchwell.Verifier.Common.Contracts;

namespace Parchwell.Verifier.Common.Window;

/// <summary>Records the starting partition high watermarks for one verifier run.</summary>
internal sealed record StartLineNote(
    int SchemaVersion,
    RunIdentity Identity,
    string Topic,
    IReadOnlyList<PartitionHigh> PartitionHighs,
    string CapturedAt,
    string VerifierCommit,
    string ConfluentKafkaVersion,
    string LibrdkafkaVersion);

/// <summary>Records one partition's high watermark at the start boundary.</summary>
internal sealed record PartitionHigh(int Partition, long High);
