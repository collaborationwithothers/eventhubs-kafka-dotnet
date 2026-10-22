using System.Text.Json;
using Parchwell.Verifier.Common.Contracts;

namespace Parchwell.Verifier.Common.Serialization;

/// <summary>Writes bounded deployment receipts using explicit wire field names.</summary>
internal static class DeploymentJson
{
    internal static byte[] Write(long maximumBytes, Action<Utf8JsonWriter> write)
    {
        using var stream = new LimitedStream(maximumBytes);
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return stream.ToArray();
    }

    internal static void WriteIdentity(Utf8JsonWriter writer, RunIdentity identity)
    {
        writer.WritePropertyName("identity");
        writer.WriteStartObject();
        writer.WriteString("scenario_id", identity.ScenarioId);
        writer.WriteString("configuration_id", identity.ConfigurationId);
        writer.WriteString("run_id", identity.RunId);
        writer.WriteString("instance_id", identity.InstanceId);
        writer.WriteString("expected_instance_id", identity.ExpectedInstanceId);
        writer.WriteString("client_id", identity.ClientId);
        writer.WriteEndObject();
    }

    private sealed class LimitedStream(long maximumBytes) : MemoryStream
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
            if (count < 0 || Length > maximumBytes - count)
                throw new InputLimitExceededException("Deployment receipt exceeds its output byte limit.");
        }
    }
}
