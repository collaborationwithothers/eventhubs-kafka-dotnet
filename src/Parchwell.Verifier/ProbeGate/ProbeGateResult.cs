using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;

namespace Parchwell.Verifier.ProbeGate;

/// <summary>Contains a gate outcome without run verdicts or outcome totals.</summary>
internal sealed record ProbeGateResult(string Outcome, IReadOnlyList<string> Reasons,
    IReadOnlyList<ProbeGateRepeat> Repeats, string VerifierCommit);

/// <summary>Retains one input's attribution, supplied flags and selected observation.</summary>
internal sealed record ProbeGateRepeat(string Path, RunIdentity? Identity, bool Usable,
    bool Qualifying, string Reason, ProbeRepeatObservation? Observation);

/// <summary>Writes complete gate metadata within the configured output byte budget.</summary>
internal static class ProbeGateResultCodec
{
    internal static byte[] Write(ProbeGateResult result, long maxOutputBytes)
    {
        using var stream = new BoundedMemoryStream(maxOutputBytes);
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", 1);
            writer.WriteString("scenario_id", "A01");
            writer.WriteString("outcome", result.Outcome);
            writer.WriteStartArray("reasons");
            foreach (string reason in result.Reasons) writer.WriteStringValue(reason);
            writer.WriteEndArray();
            writer.WriteStartArray("repeats");
            foreach (ProbeGateRepeat repeat in result.Repeats)
            {
                writer.WriteStartObject();
                writer.WriteString("path", repeat.Path);
                writer.WritePropertyName("identity");
                if (repeat.Identity is not { } identity) writer.WriteNullValue();
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("scenario_id", identity.ScenarioId);
                    writer.WriteString("configuration_id", identity.ConfigurationId);
                    writer.WriteString("run_id", identity.RunId);
                    writer.WriteString("instance_id", identity.InstanceId);
                    writer.WriteString("expected_instance_id", identity.ExpectedInstanceId);
                    writer.WriteString("client_id", identity.ClientId);
                    writer.WriteEndObject();
                }
                writer.WriteBoolean("usable", repeat.Usable);
                writer.WriteBoolean("qualifying", repeat.Qualifying);
                writer.WriteString("reason", repeat.Reason);
                if (repeat.Observation is { } observation)
                {
                    writer.WriteStartObject("observation");
                    writer.WriteBoolean("usable", observation.Usable);
                    writer.WriteBoolean("qualifying", observation.Qualifying);
                    if (observation.Broker is not null) writer.WriteString("broker", observation.Broker);
                    if (observation.ReportIndex is { } index) writer.WriteNumber("report_index", index);
                    if (observation.WindowCount is { } count) writer.WriteNumber("window_count", count);
                    if (observation.WindowMaximum is { } maximum) writer.WriteNumber("window_maximum", maximum);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("verifier_commit", result.VerifierCommit);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private sealed class BoundedMemoryStream(long maximumBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            base.Write(buffer);
        }
        public override void WriteByte(byte value)
        {
            Check(1);
            base.WriteByte(value);
        }
        private void Check(int count)
        {
            if (count > maximumBytes - Length) throw new InvalidOperationException("The gate output exceeds its configured byte limit.");
        }
    }
}
