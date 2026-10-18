using System.Text;
using Parchwell.Verifier.Common.Serialization;

namespace Parchwell.Verifier.Tests.Support;

/// <summary>Provides fixed test inputs whose expected bytes do not come from production codecs.</summary>
internal static class RecordedEvidence
{
    /// <summary>Returns strict limits large enough for small synthetic parser cases.</summary>
    public static InputLimits SmallLimits { get; } = new(
        MaxDepth: 16,
        MaxArtifactBytes: 16_384,
        MaxTotalInputBytes: 32_768,
        MaxRowBytes: 4_096,
        MaxRowsPerArtifact: 16,
        MaxEntries: 512,
        MaxDiagnosticBytes: 256);

    /// <summary>Returns literal UTF-8 bytes for a minimal independent identity object.</summary>
    public static ReadOnlyMemory<byte> IdentityJson { get; } = Encoding.UTF8.GetBytes(
        "{\"scenario_id\":\"A01\",\"configuration_id\":\"C0\",\"run_id\":\"run-1\",\"instance_id\":\"instance-1\",\"expected_instance_id\":\"instance-1\",\"client_id\":\"producer-1\"}");

    /// <summary>Returns literal bytes with a nested duplicate property for parser regressions.</summary>
    public static ReadOnlyMemory<byte> NestedDuplicateJson { get; } = Encoding.UTF8.GetBytes(
        "{\"outer\":{\"field\":1,\"field\":2}}");

    /// <summary>Returns the path to an unchanged committed golden fixture file.</summary>
    public static string FixturePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(AppContext.BaseDirectory, "fixtures", "verifier-v1", relativePath);
    }
}
