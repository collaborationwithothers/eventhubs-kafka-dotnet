using System.Collections.Immutable;

namespace Parchwell.Scenarios;

/// <summary>
/// A node of the small, data-only tree that <see cref="YamlInput"/> builds from one YAML document.
/// The readers validate this tree instead of binding YAML straight to types, so every rejected value
/// can be reported with its field path and position.
/// </summary>
internal abstract record InputNode(SourceLocation Location);
internal sealed record MapNode(ImmutableDictionary<string, InputNode> Fields, SourceLocation Location) : InputNode(Location);
internal sealed record SequenceNode(ImmutableArray<InputNode> Items, SourceLocation Location) : InputNode(Location);
/// <summary>A YAML scalar.</summary>
/// <remarks>
/// <see cref="ScalarNode.IsPlain"/> is true when the scalar was written without quotes. Numeric fields require a
/// plain scalar, so a quoted number such as "5" is rejected rather than read as 5.
/// </remarks>
internal sealed record ScalarNode(string Value, bool IsPlain, SourceLocation Location) : InputNode(Location);
