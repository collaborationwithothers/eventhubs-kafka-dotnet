using System.Globalization;
using System.Text;
using Confluent.Kafka;
using Parchwell.Verifier.Common.Serialization;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Window;

internal static class MessageIdentityReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static ConsumedRecord Read(ConsumeResult<byte[], byte[]> record)
    {
        var problems = new List<StructuralProblem>();
        string? key = Decode(record.Message?.Key, "key", problems);
        string? instance = Header(record.Message?.Headers, "instance-id", problems);
        string? attempt = Header(record.Message?.Headers, "attempt-id", problems);
        string? sequenceText = Header(record.Message?.Headers, "sequence", problems);
        long? sequence = null;
        if (sequenceText is not null)
        {
            if (long.TryParse(sequenceText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) &&
                string.Equals(sequenceText, value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                sequence = value;
            else problems.Add(Problem("sequence", "noncanonical_sequence"));
        }
        return new(record.Topic, record.Partition.Value, record.Offset.Value, key, instance, attempt, sequence, problems);
    }

    private static string? Header(Headers? headers, string name, List<StructuralProblem> problems)
    {
        IHeader[] matches = headers is null ? [] : headers.Where(h => string.Equals(h.Key, name, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            problems.Add(Problem(name, matches.Length == 0 ? "missing_header" : "duplicate_header"));
            return null;
        }
        return Decode(matches[0].GetValueBytes(), name, problems);
    }

    private static string? Decode(byte[]? bytes, string field, List<StructuralProblem> problems)
    {
        if (bytes is null)
        {
            problems.Add(Problem(field, "null_identity_bytes"));
            return null;
        }
        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException)
        {
            problems.Add(Problem(field, "invalid_utf8"));
            return null;
        }
    }

    private static StructuralProblem Problem(string field, string code) => new(6, code, "consumed", null, field, null);
}
