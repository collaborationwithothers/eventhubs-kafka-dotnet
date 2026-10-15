using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Parchwell.Scenarios;

internal sealed class NodeValidation(string source)
{
    private static readonly Regex SlugPattern = new(@"\A[A-Za-z][A-Za-z0-9_-]*\z", RegexOptions.CultureInvariant);
    private static readonly Regex PositiveIntegerPattern = new(@"\A[0-9]+\z", RegexOptions.CultureInvariant);
    private static readonly Regex SignedIntegerPattern = new(@"\A-?[0-9]+\z", RegexOptions.CultureInvariant);
    private readonly List<ScenarioDiagnostic> _diagnostics = [];

    internal string Source { get; } = source;
    internal bool HasErrors => _diagnostics.Count != 0;

    internal MapNode? Map(InputNode? node, string path)
    {
        if (node is MapNode map) return map;
        Error(node, path, "mapping_required", "Expected a mapping.");
        return null;
    }

    internal SequenceNode? Sequence(InputNode? node, string path, bool requireItems = true)
    {
        if (node is SequenceNode sequence)
        {
            if (requireItems && sequence.Items.IsEmpty) Error(node, path, "empty_sequence", "The sequence must not be empty.");
            return sequence;
        }
        Error(node, path, "sequence_required", "Expected a sequence.");
        return null;
    }

    internal bool CheckFields(MapNode map, string path, params string[] expected)
    {
        var valid = true;
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        foreach (var key in map.Fields.Keys)
        {
            if (expectedSet.Contains(key)) continue;
            Error(map.Fields[key], Join(path, key), "unknown_field", $"Unknown field '{key}'.");
            valid = false;
        }
        foreach (var key in expected)
        {
            if (map.Fields.ContainsKey(key)) continue;
            Error(map, Join(path, key), "required_field", $"Required field '{key}' is missing.");
            valid = false;
        }
        return valid;
    }

    internal bool TryFields(MapNode map, string path, IReadOnlyCollection<string> allowed, IReadOnlyCollection<string> required)
    {
        var valid = true;
        foreach (var key in map.Fields.Keys)
        {
            if (allowed.Contains(key, StringComparer.Ordinal)) continue;
            Error(map.Fields[key], Join(path, key), "unknown_field", $"Unknown field '{key}'.");
            valid = false;
        }
        foreach (var key in required)
        {
            if (map.Fields.ContainsKey(key)) continue;
            Error(map, Join(path, key), "required_field", $"Required field '{key}' is missing.");
            valid = false;
        }
        return valid;
    }

    internal string? String(MapNode? map, string key, string path)
    {
        if (map is null || !map.Fields.TryGetValue(key, out var node)) return null;
        if (node is ScalarNode scalar) return scalar.Value;
        Error(node, Join(path, key), "scalar_required", "Expected a scalar.");
        return null;
    }

    internal bool TryInt32(MapNode? map, string key, string path, bool positive, out int value)
    {
        value = 0;
        if (map is null || !map.Fields.TryGetValue(key, out var node)) return false;
        if (node is not ScalarNode { IsPlain: true } scalar)
        {
            Error(node, Join(path, key), "plain_integer_required", "Expected a plain decimal integer.");
            return false;
        }
        var pattern = positive ? PositiveIntegerPattern : SignedIntegerPattern;
        if (!pattern.IsMatch(scalar.Value) || !int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            Error(node, Join(path, key), positive ? "positive_int32_required" : "int32_required",
                positive ? "Expected a positive plain decimal Int32." : "Expected a plain decimal Int32.");
            value = 0;
            return false;
        }
        if (positive && value <= 0)
        {
            Error(node, Join(path, key), "positive_int32_required", "Expected a positive plain decimal Int32.");
            value = 0;
            return false;
        }
        return true;
    }

    internal bool TryInt32(InputNode? node, string path, bool positive, out int value)
    {
        value = 0;
        if (node is not ScalarNode { IsPlain: true } scalar)
        {
            Error(node, path, "plain_integer_required", "Expected a plain decimal integer.");
            return false;
        }
        var pattern = positive ? PositiveIntegerPattern : SignedIntegerPattern;
        if (!pattern.IsMatch(scalar.Value) || !int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value))
        {
            Error(node, path, positive ? "positive_int32_required" : "int32_required",
                positive ? "Expected a positive plain decimal Int32." : "Expected a plain decimal Int32.");
            value = 0;
            return false;
        }
        if (positive && value <= 0)
        {
            Error(node, path, "positive_int32_required", "Expected a positive plain decimal Int32.");
            value = 0;
            return false;
        }
        return true;
    }

    internal bool IsSlug(string? value, InputNode? node, string path)
    {
        if (value is not null && SlugPattern.IsMatch(value)) return true;
        Error(node, path, "invalid_identifier", "Expected an ASCII identifier beginning with a letter and containing only letters, digits, underscore or hyphen.");
        return false;
    }

    internal void Error(InputNode? node, string path, string code, string message)
    {
        var location = node?.Location;
        _diagnostics.Add(new ScenarioDiagnostic(Source, path, code, message, location?.Line, location?.Column));
    }

    internal void Error(SourceLocation location, string path, string code, string message) =>
        _diagnostics.Add(new ScenarioDiagnostic(Source, path, code, message, location.Line, location.Column));

    internal void ThrowIfErrors()
    {
        if (_diagnostics.Count != 0) throw new ScenarioValidationException(_diagnostics.ToImmutableArray());
    }

    internal static string Join(string path, string key) => path.Length == 0 ? key : path + "." + key;
}
