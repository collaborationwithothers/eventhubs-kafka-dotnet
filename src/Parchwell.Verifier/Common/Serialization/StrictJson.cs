using System.Text.Json;
using System.Text;

namespace Parchwell.Verifier.Common.Serialization;

/// <summary>Provides strict structural JSON parsing primitives for captured verifier inputs.</summary>
/// <remarks>Structural parsing is separate from the evidence decisions in ADR-012 and ADR-013.</remarks>
internal static class StrictJson
{
    private const string JsonRethrowableExceptionSource = "System.Text.Json.Rethrowable";

    /// <summary>Parses a bounded JSON document and rejects duplicate properties at every depth.</summary>
    internal static ParseResult<JsonDocument> ParseDocument(
        ReadOnlyMemory<byte> utf8,
        string artifact,
        InputLimits limits,
        int? checkNumber,
        long? row = null,
        bool requireObject = true)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(limits);
        if (utf8.Length > limits.MaxArtifactBytes)
        {
            throw new InputLimitExceededException($"{artifact} exceeds the configured byte limit.");
        }

        try
        {
            _ = new UTF8Encoding(false, true).GetString(utf8.Span);
            Preflight(utf8.Span, artifact, limits);
            JsonDocument parsed = JsonDocument.Parse(utf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = limits.MaxDepth
            });

            if (requireObject && parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                parsed.Dispose();
                return ParseResult<JsonDocument>.Failure(Problem("root_not_object", artifact, checkNumber, row));
            }

            StructuralProblem? duplicate = FindDuplicate(parsed.RootElement, artifact, checkNumber, row);
            if (duplicate is not null)
            {
                parsed.Dispose();
                return ParseResult<JsonDocument>.Failure(duplicate);
            }

            return ParseResult<JsonDocument>.Success(parsed);
        }
        catch (DecoderFallbackException)
        {
            return ParseResult<JsonDocument>.Failure(Problem("invalid_utf8", artifact, checkNumber, row));
        }
        catch (JsonException)
        {
            return ParseResult<JsonDocument>.Failure(Problem("malformed_json", artifact, checkNumber, row));
        }
    }

    /// <summary>Returns problems for absent required properties and undeclared properties.</summary>
    internal static IReadOnlyList<StructuralProblem> ValidateProperties(
        JsonElement value,
        string path,
        IReadOnlySet<string> required,
        IReadOnlySet<string> allowed,
        string artifact,
        int? checkNumber,
        long? row = null)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return [Problem("expected_object", artifact, checkNumber, row, path)];
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            names.Add(property.Name);
        }

        var problems = new List<StructuralProblem>();
        foreach (string name in required)
        {
            if (!names.Contains(name))
            {
                problems.Add(Problem("missing_property", artifact, checkNumber, row, Join(path, name)));
            }
        }

        foreach (string name in names)
        {
            if (!allowed.Contains(name))
            {
                problems.Add(Problem("unknown_property", artifact, checkNumber, row, Join(path, name)));
            }
        }

        return problems;
    }

    /// <summary>Splits strict LF-terminated JSON Lines input while preserving each row's bytes.</summary>
    internal static ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>> ReadJsonLines(
        ReadOnlyMemory<byte> utf8,
        string artifact,
        InputLimits limits,
        int? checkNumber)
    {
        if (utf8.Length > limits.MaxArtifactBytes)
        {
            throw new InputLimitExceededException($"{artifact} exceeds the configured byte limit.");
        }

        if (utf8.Length == 0 || utf8.Span[^1] != (byte)'\n')
        {
            return ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>>.Failure(Problem("jsonl_missing_final_lf", artifact, checkNumber));
        }

        var rows = new List<ReadOnlyMemory<byte>>();
        int start = 0;
        long entries = 0;
        for (int index = 0; index < utf8.Length; index++)
        {
            if (utf8.Span[index] != (byte)'\n')
            {
                continue;
            }

            int length = index - start;
            long rowNumber = rows.Count + 1L;
            if (length == 0)
            {
                return ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>>.Failure(Problem("jsonl_blank_row", artifact, checkNumber, rowNumber));
            }

            if (length > limits.MaxRowBytes)
            {
                throw new InputLimitExceededException($"{artifact} row {rowNumber} exceeds the configured row byte limit.");
            }

            ReadOnlyMemory<byte> rowBytes = utf8.Slice(start, length);
            ParseResult<JsonDocument> parsed = ParseDocument(rowBytes, artifact, limits with { MaxArtifactBytes = limits.MaxRowBytes }, checkNumber, rowNumber);
            if (!parsed.IsSuccess)
            {
                return ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>>.Failure(parsed.Problems);
            }

            parsed.Value!.Dispose();
            rows.Add(rowBytes);
            entries = checked(entries + 1);
            if (entries > limits.MaxRowsPerArtifact || entries > limits.MaxEntries)
            {
                throw new InputLimitExceededException($"{artifact} exceeds the configured row or entry limit.");
            }

            start = index + 1;
        }

        return ParseResult<IReadOnlyList<ReadOnlyMemory<byte>>>.Success(rows);
    }

    /// <summary>Reads a JSON string without accepting implicit coercion.</summary>
    internal static bool TryGetString(JsonElement value, out string result)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            result = value.GetString()!;
            return true;
        }

        result = string.Empty;
        return false;
    }

    /// <summary>Reads a signed 32-bit JSON integer without accepting numeric strings or fractions.</summary>
    internal static bool TryGetInt32(JsonElement value, out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result);
    }

    /// <summary>Reads a signed 64-bit JSON integer without accepting numeric strings or fractions.</summary>
    internal static bool TryGetInt64(JsonElement value, out long result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out result);
    }

    /// <summary>Reads a finite JSON number representable as a double.</summary>
    internal static bool TryGetFiniteDouble(JsonElement value, out double result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out result) && double.IsFinite(result))
        {
            return true;
        }

        result = 0;
        return false;
    }

    /// <summary>Returns the structural problem for a field with an unexpected JSON kind.</summary>
    internal static StructuralProblem WrongKind(string artifact, int? checkNumber, string field, long? row = null) =>
        Problem("wrong_json_kind", artifact, checkNumber, row, field);

    private static StructuralProblem? FindDuplicate(
        JsonElement value,
        string artifact,
        int? checkNumber,
        long? row)
    {
        var pending = new Stack<(JsonElement Element, string Path)>();
        pending.Push((value, "$"));
        while (pending.Count > 0)
        {
            (JsonElement element, string path) = pending.Pop();
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    string propertyPath = Join(path, property.Name);
                    if (!names.Add(property.Name))
                    {
                        return Problem("duplicate_property", artifact, checkNumber, row, propertyPath);
                    }

                    pending.Push((property.Value, propertyPath));
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    pending.Push((item, $"{path}[{index++}]"));
                }
            }
        }

        return null;
    }

    private static void Preflight(ReadOnlySpan<byte> utf8, string artifact, InputLimits limits)
    {
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = int.MaxValue
        });

        var containers = new Stack<JsonTokenType>();
        long entries = 0;
        while (reader.Read())
        {
            bool startsContainer = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
            int nestingDepth = reader.CurrentDepth + (startsContainer ? 1 : 0);
            if (nestingDepth > limits.MaxDepth)
            {
                throw new InputLimitExceededException($"{artifact} exceeds the configured nesting depth.");
            }

            if (reader.TokenType == JsonTokenType.PropertyName ||
                (containers.Count > 0 && containers.Peek() == JsonTokenType.StartArray &&
                 reader.TokenType is not JsonTokenType.EndArray and not JsonTokenType.EndObject))
            {
                entries = checked(entries + 1);
                if (entries > limits.MaxEntries)
                {
                    throw new InputLimitExceededException($"{artifact} exceeds the configured entry limit.");
                }
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                ValidateUtf16String(ref reader);
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                ValidateUtf16String(ref reader);
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                containers.Push(reader.TokenType);
            }
            else if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                if (containers.Count > 0)
                {
                    containers.Pop();
                }
            }
        }
    }

    private static void ValidateUtf16String(ref Utf8JsonReader reader)
    {
        try
        {
            _ = reader.GetString();
        }
        catch (InvalidOperationException exception) when (
            string.Equals(exception.Source, JsonRethrowableExceptionSource, StringComparison.Ordinal))
        {
            // Pinned .NET 10 routes invalid escaped UTF-16 through this marked exception.
            // Convert only at this string-decoding boundary so ParseDocument can return malformed_json.
            throw new JsonException("The JSON string contains an invalid UTF-16 escape sequence.", exception);
        }
    }

    private static StructuralProblem Problem(string code, string artifact, int? checkNumber, long? row = null, string? field = null) =>
        new(checkNumber, code, artifact, row, field, null);

    private static string Join(string path, string property) => path == "$" ? property : $"{path}.{property}";
}

/// <summary>Represents an optional JSON property without collapsing absent and explicit null.</summary>
internal readonly record struct ParsedField<T>(FieldPresence Presence, T? Value)
{
    /// <summary>Creates the absent-property state.</summary>
    internal static ParsedField<T> Missing() => new(FieldPresence.Missing, default);

    /// <summary>Creates the explicit JSON null state.</summary>
    internal static ParsedField<T> Null() => new(FieldPresence.Null, default);

    /// <summary>Creates the present-value state.</summary>
    internal static ParsedField<T> FromValue(T value) => new(FieldPresence.Value, value);
}

/// <summary>Identifies the three JSON property presence states.</summary>
internal enum FieldPresence
{
    Missing,
    Null,
    Value
}

/// <summary>A bounded structural failure attributed to its future numbered-check owner.</summary>
internal sealed record StructuralProblem(
    int? CheckNumber,
    string Code,
    string Artifact,
    long? Row,
    string? Field,
    string? Snippet);

/// <summary>Classifies a structural parse failure or an input-level execution failure.</summary>
internal enum ParseFailureKind
{
    Structural,
    Execution
}

/// <summary>Contains a parsed value, recoverable evidence, or a classified parse failure.</summary>
/// <remarks>Execution classification is an input outcome only; it does not judge run validity.</remarks>
internal readonly record struct ParseResult<T>(
    T? Value,
    IReadOnlyList<StructuralProblem> Problems,
    ParseFailureKind? FailureKind = null)
{
    /// <summary>Gets whether parsing produced no structural problems.</summary>
    internal bool IsSuccess => Problems.Count == 0;

    /// <summary>Gets whether this result explicitly represents an execution failure.</summary>
    internal bool IsExecutionFailure => FailureKind == ParseFailureKind.Execution;

    /// <summary>Creates a successful parse result.</summary>
    internal static ParseResult<T> Success(T value) => new(value, Array.Empty<StructuralProblem>());

    /// <summary>Creates a structural parse failure.</summary>
    internal static ParseResult<T> Failure(StructuralProblem problem) =>
        new(default, [problem], ParseFailureKind.Structural);

    /// <summary>Creates a structural parse failure containing multiple problems.</summary>
    internal static ParseResult<T> Failure(IReadOnlyList<StructuralProblem> problems) =>
        new(default, problems, ParseFailureKind.Structural);

    /// <summary>Creates an execution failure retaining its input problem.</summary>
    internal static ParseResult<T> ExecutionFailure(StructuralProblem problem) =>
        new(default, [problem], ParseFailureKind.Execution);

    /// <summary>Creates an execution failure retaining all input problems.</summary>
    internal static ParseResult<T> ExecutionFailure(IReadOnlyList<StructuralProblem> problems) =>
        new(default, problems, ParseFailureKind.Execution);
}

/// <summary>Supplies hard bounds for verifier input parsing.</summary>
internal sealed record InputLimits(
    int MaxDepth,
    long MaxArtifactBytes,
    long MaxTotalInputBytes,
    int MaxRowBytes,
    long MaxRowsPerArtifact,
    long MaxEntries,
    int MaxDiagnosticBytes);

/// <summary>Supplies output and diagnostic bounds to the passive result codecs.</summary>
internal sealed record OutputLimits(
    long MaxOutputBytes,
    int MaxDiagnosticsPerCheck,
    int MaxDiagnosticBytes);

/// <summary>Signals that a configured input resource bound was exceeded.</summary>
internal sealed class InputLimitExceededException(string message) : Exception(message);

/// <summary>Tracks aggregate input bytes and entries across related artifacts.</summary>
internal sealed class InputBudget(InputLimits limits)
{
    private long _bytes;
    private long _entries;

    internal void ConsumeBytes(long count)
    {
        try
        {
            _bytes = checked(_bytes + count);
        }
        catch (OverflowException exception)
        {
            throw new InputLimitExceededException($"The combined verifier inputs exceed the configured byte limit: {exception.Message}");
        }

        if (_bytes > limits.MaxTotalInputBytes)
        {
            throw new InputLimitExceededException("The combined verifier inputs exceed the configured byte limit.");
        }
    }

    internal void ConsumeEntries(long count)
    {
        try
        {
            _entries = checked(_entries + count);
        }
        catch (OverflowException exception)
        {
            throw new InputLimitExceededException($"The combined verifier inputs exceed the configured entry limit: {exception.Message}");
        }

        if (_entries > limits.MaxEntries)
        {
            throw new InputLimitExceededException("The combined verifier inputs exceed the configured entry limit.");
        }
    }
}
