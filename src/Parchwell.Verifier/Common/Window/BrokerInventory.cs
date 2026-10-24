using Parchwell.Verifier.Common.Contracts;

namespace Parchwell.Verifier.Common.Window;

/// <summary>Passive independent Kafka metadata captured at the start-line boundary.</summary>
internal sealed record BrokerInventory(int SchemaVersion, RunIdentity Identity, IReadOnlyList<BrokerInventoryEntry> Brokers, string Source, string SourceCommit);

/// <summary>One broker from the independent start-line metadata snapshot.</summary>
internal sealed record BrokerInventoryEntry(int Id, string Host, int Port, string Name);
