using System.Collections.Immutable;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Parchwell.Scenarios;

/// <summary>Reads bounded YAML into a small data-only input tree.</summary>
/// <remarks>
/// <para>
/// Every document is held to the <see cref="ScenarioLoadOptions"/> budgets. It must be exactly one
/// document with no anchors, aliases, merge keys, explicit tags or duplicate keys, so the file means
/// what it plainly says (docs/operations/scenario-contract.md, "Input and ownership").
/// </para>
/// <para>
/// Diagnostic codes from this parser are PascalCase, unlike the snake_case codes of the readers and
/// the resolver. Issue 61 left these spellings unchanged (docs/operations/scenario-contract.md).
/// </para>
/// </remarks>
internal static class YamlInput
{
    internal static async ValueTask<InputNode> ReadAsync(TextReader reader, string logicalPath,
        ScenarioLoadOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(logicalPath);
        ArgumentNullException.ThrowIfNull(options);

        ValidateOptions(options, logicalPath);
        var yaml = await ReadBoundedAsync(reader, logicalPath, options.MaxDocumentCharacters, cancellationToken)
            .ConfigureAwait(false);

        var parser = new Parser(new StringReader(yaml));
        return new EventTreeReader(parser, logicalPath, options, cancellationToken).Read();
    }

    private static void ValidateOptions(ScenarioLoadOptions options, string logicalPath)
    {
        if (options.MaxDocumentCharacters <= 0)
            throw Error(logicalPath, "$", "InvalidLimit", "MaxDocumentCharacters must be positive.");
        if (options.MaxNestingDepth <= 0)
            throw Error(logicalPath, "$", "InvalidLimit", "MaxNestingDepth must be positive.");
        if (options.MaxScalarCharacters <= 0)
            throw Error(logicalPath, "$", "InvalidLimit", "MaxScalarCharacters must be positive.");
        if (options.MaxParserEvents <= 0)
            throw Error(logicalPath, "$", "InvalidLimit", "MaxParserEvents must be positive.");
    }

    private static async ValueTask<string> ReadBoundedAsync(TextReader reader, string logicalPath, int limit,
        CancellationToken cancellationToken)
    {
        var buffer = new char[Math.Min(4096, limit)];
        var text = new StringBuilder(Math.Min(4096, limit));
        var count = 0;
        while (count < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requested = Math.Min(buffer.Length, limit - count);
            var read = await reader.ReadAsync(buffer.AsMemory(0, requested), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                return text.ToString();

            text.Append(buffer, 0, read);
            count += read;
        }

        cancellationToken.ThrowIfCancellationRequested();
        // The limit has been reached. Read one more character to tell a document of exactly the limit from
        // a longer one, which is rejected rather than truncated.
        var probe = new char[1];
        if (await reader.ReadAsync(probe.AsMemory(), cancellationToken).ConfigureAwait(false) != 0)
            throw Error(logicalPath, "$", "DocumentTooLarge",
                $"The YAML document exceeds the {limit} character limit.");
        return text.ToString();
    }

    private static ScenarioValidationException Error(string source, string fieldPath, string code, string message,
        Mark? mark = null)
    {
        // YamlDotNet Mark.Line and Mark.Column are one-based (pinned v18.1.0 Mark.cs:
        // https://github.com/aaubry/YamlDotNet/blob/748334a8fa7c227740018b284b71ad95cc6b7fc7/YamlDotNet/Core/Mark.cs#L34-L67).
        return new ScenarioValidationException([new ScenarioDiagnostic(source, fieldPath, code, message,
            mark.HasValue ? checked((int)mark.Value.Line) : null,
            mark.HasValue ? checked((int)mark.Value.Column) : null)]);
    }

    private sealed class EventTreeReader(Parser parser, string logicalPath, ScenarioLoadOptions options,
        CancellationToken cancellationToken)
    {
        private ParsingEvent? _current;
        private int _eventsRead;
        private bool _ended;

        internal InputNode Read()
        {
            if (!HasNext || Take() is not StreamStart)
                throw Error(logicalPath, "$", "InvalidYaml", "A YAML stream was not found.");

            if (!HasNext || Take() is not DocumentStart documentStart)
                throw Error(logicalPath, "$", "InvalidYaml", "A YAML document was not found.");

            cancellationToken.ThrowIfCancellationRequested();
            if (!HasNext || Peek() is DocumentEnd or StreamEnd)
                throw Error(logicalPath, "$", "EmptyDocument", "The YAML document must contain a value.", documentStart.Start);

            var root = ReadNode("$", 0);
            if (!HasNext || Take() is not DocumentEnd documentEnd)
                throw Error(logicalPath, "$", "InvalidYaml", "The YAML document did not end correctly.", CurrentMark());

            if (!HasNext)
                throw Error(logicalPath, "$", "InvalidYaml", "The YAML stream did not end correctly.", documentEnd.End);
            var end = Take();
            if (end is DocumentStart)
                throw Error(logicalPath, "$", "MultipleDocuments", "Only one YAML document is allowed.", end.Start);
            if (end is not StreamEnd)
                throw Error(logicalPath, "$", "InvalidYaml", "Unexpected content followed the YAML document.", end.Start);
            if (HasNext)
                throw Error(logicalPath, "$", "InvalidYaml", "Unexpected content followed the YAML stream.", CurrentMark());
            return root;
        }

        private InputNode ReadNode(string fieldPath, int parentDepth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasNext)
                throw Error(logicalPath, fieldPath, "InvalidYaml", "Expected a YAML value.");

            var item = Take();
            switch (item)
            {
                case Scalar scalar:
                    CheckNodeEvent(scalar, fieldPath);
                    CheckScalar(scalar, fieldPath);
                    return new ScalarNode(scalar.Value, scalar.Style == ScalarStyle.Plain, Location(scalar.Start));
                case SequenceStart sequenceStart:
                {
                    CheckNodeEvent(sequenceStart, fieldPath);
                    var depth = CheckDepth(parentDepth, fieldPath, sequenceStart.Start);
                    var items = ImmutableArray.CreateBuilder<InputNode>();
                    while (HasNext && Peek() is not SequenceEnd)
                        items.Add(ReadNode(fieldPath + "[" + items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", depth));
                    if (!HasNext)
                        throw Error(logicalPath, fieldPath, "InvalidYaml", "The YAML sequence did not end correctly.", sequenceStart.Start);
                    Take();
                    return new SequenceNode(items.ToImmutable(), Location(sequenceStart.Start));
                }
                case MappingStart mappingStart:
                {
                    CheckNodeEvent(mappingStart, fieldPath);
                    var depth = CheckDepth(parentDepth, fieldPath, mappingStart.Start);
                    var fields = ImmutableDictionary.CreateBuilder<string, InputNode>(StringComparer.Ordinal);
                    while (HasNext && Peek() is not MappingEnd)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (Peek() is AnchorAlias keyAlias)
                            throw Error(logicalPath, fieldPath, "AliasNotAllowed", "YAML aliases are not allowed.", keyAlias.Start);
                        if (Peek() is not Scalar key)
                            throw Error(logicalPath, fieldPath, "NonScalarMappingKey", "Mapping keys must be scalar values.", Peek().Start);

                        Take();
                        CheckNodeEvent(key, fieldPath);
                        CheckScalar(key, fieldPath);
                        if (key.Value == "<<")
                            throw Error(logicalPath, ChildPath(fieldPath, key.Value), "MergeKeyNotAllowed",
                                "YAML merge keys are not allowed.", key.Start);
                        var childPath = ChildPath(fieldPath, key.Value);
                        if (fields.ContainsKey(key.Value))
                            throw Error(logicalPath, childPath, "DuplicateKey", $"The mapping key '{key.Value}' occurs more than once.", key.Start);
                        if (!HasNext || Peek() is MappingEnd)
                            throw Error(logicalPath, childPath, "InvalidYaml", "A mapping key has no value.", key.Start);
                        fields.Add(key.Value, ReadNode(childPath, depth));
                    }
                    if (!HasNext)
                        throw Error(logicalPath, fieldPath, "InvalidYaml", "The YAML mapping did not end correctly.", mappingStart.Start);
                    Take();
                    return new MapNode(fields.ToImmutable(), Location(mappingStart.Start));
                }
                default:
                    if (item is AnchorAlias valueAlias)
                        throw Error(logicalPath, fieldPath, "AliasNotAllowed", "YAML aliases are not allowed.", valueAlias.Start);
                    throw Error(logicalPath, fieldPath, "InvalidYaml", "Expected a scalar, sequence or mapping value.", item.Start);
            }
        }

        private void CheckNodeEvent(NodeEvent nodeEvent, string fieldPath)
        {
            if (!nodeEvent.Anchor.IsEmpty)
                throw Error(logicalPath, fieldPath, "AnchorNotAllowed", "YAML anchors are not allowed.", nodeEvent.Start);
            if (!nodeEvent.Tag.IsEmpty)
                throw Error(logicalPath, fieldPath, "ExplicitTagNotAllowed", "Explicit YAML tags are not allowed.", nodeEvent.Start);
        }

        private int CheckDepth(int parentDepth, string fieldPath, Mark mark)
        {
            if (parentDepth >= options.MaxNestingDepth)
                throw Error(logicalPath, fieldPath, "NestingTooDeep",
                    $"The YAML mapping/sequence nesting exceeds the {options.MaxNestingDepth} level limit.", mark);
            return parentDepth + 1;
        }

        private void CheckScalar(Scalar scalar, string fieldPath)
        {
            if (scalar.Value.Length > options.MaxScalarCharacters)
                throw Error(logicalPath, fieldPath, "ScalarTooLarge",
                    $"The YAML scalar exceeds the {options.MaxScalarCharacters} character limit.", scalar.Start);
        }

        // YamlDotNet Mark.Line and Mark.Column are already one-based (pinned YamlDotNet 18.1.0 Mark.cs).
        private SourceLocation Location(Mark mark) => new(logicalPath,
            checked((int)mark.Line), checked((int)mark.Column));

        private static string ChildPath(string parent, string key)
        {
            if (key.Length > 0 && (char.IsAsciiLetter(key[0]) || key[0] == '_') &&
                key.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_' || character == '-'))
                return parent + "." + key;

            var escaped = key.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            return parent + "['" + escaped + "']";
        }

        private bool HasNext
        {
            get
            {
                if (_current is not null)
                    return true;
                if (_ended)
                    return false;

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!parser.MoveNext())
                    {
                        _ended = true;
                        return false;
                    }
                }
                catch (YamlException exception)
                {
                    throw Error(logicalPath, "$", "InvalidYaml", exception.Message, exception.Start);
                }

                var next = parser.Current!;
                if (_eventsRead >= options.MaxParserEvents)
                    throw Error(logicalPath, "$", "TooManyParserEvents",
                        $"The YAML document exceeds the {options.MaxParserEvents} parser event limit.", next.Start);
                _eventsRead++;
                _current = next;
                return true;
            }
        }

        private ParsingEvent Peek() => HasNext ? _current! : throw new InvalidOperationException("No YAML event remains.");

        private ParsingEvent Take()
        {
            var result = Peek();
            _current = null;
            return result;
        }

        private Mark? CurrentMark() => HasNext ? Peek().Start : null;
    }
}
