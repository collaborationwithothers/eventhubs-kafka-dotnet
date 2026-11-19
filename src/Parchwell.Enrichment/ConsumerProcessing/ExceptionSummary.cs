using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Confluent.Kafka;

namespace Parchwell.Enrichment.ConsumerProcessing;

/// <summary>
/// Builds a short, safe description of an exception chain for operator logs.
/// Pod logs can reach the public pod-evidence artifact, and Azure SDK messages can carry tenant IDs, client IDs,
/// request URLs and response bodies. So the summary never reads <see cref="Exception.Message"/> except to find an
/// <c>AADSTS</c> code in Azure.Identity exceptions, and it never prints stack traces.
/// </summary>
internal static partial class ExceptionSummary
{
    internal const int MaxChainDepth = 8;
    internal const int MaxBytes = 1024;
    private const int MaxTokenLength = 96;

    [GeneratedRegex("AADSTS[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex AadstsCode();

    public static string Describe(Exception? exception)
    {
        if (exception is null) return "none";
        var text = new StringBuilder();
        int depth = 0;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (depth == MaxChainDepth)
            {
                Append(text, "truncated");
                break;
            }
            Append(text, DescribeOne(current));
            depth++;
        }
        return Bound(text.ToString());
    }

    private static void Append(StringBuilder text, string part)
    {
        if (text.Length > 0) text.Append('>');
        text.Append(part);
    }

    private static string DescribeOne(Exception exception)
    {
        Type type = exception.GetType();
        string name = Token(type.FullName ?? type.Name);
        string? details = exception switch
        {
            RequestFailedException failed => $"status={failed.Status} error_code={Token(failed.ErrorCode)}",
            KafkaException kafka => $"error_code={kafka.Error.Code} is_fatal={kafka.Error.IsFatal}",
            SocketException socket => $"socket_error={socket.SocketErrorCode}",
            HttpRequestException http => $"http_error={http.HttpRequestError}",
            _ when type.Namespace == "Azure.Identity" => AadstsCode().Match(exception.Message) is { Success: true } match
                ? $"aadsts={match.Value}" : null,
            _ => null
        };
        return details is null ? name : $"{name}({details})";
    }

    private static string Token(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "none";
        var result = new StringBuilder(Math.Min(value.Length, MaxTokenLength));
        foreach (char character in value)
        {
            if (result.Length == MaxTokenLength) break;
            result.Append(character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-' or '`' or '+' ? character : '?');
        }
        return result.ToString();
    }

    private static string Bound(string value)
    {
        if (value.Length <= MaxBytes) return value;
        return value[..(MaxBytes - 3)] + "...";
    }
}
