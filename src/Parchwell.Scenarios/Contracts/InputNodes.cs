using System.Collections.Immutable;

namespace Parchwell.Scenarios;

internal abstract record InputNode(SourceLocation Location);
internal sealed record MapNode(ImmutableDictionary<string, InputNode> Fields, SourceLocation Location) : InputNode(Location);
internal sealed record SequenceNode(ImmutableArray<InputNode> Items, SourceLocation Location) : InputNode(Location);
internal sealed record ScalarNode(string Value, bool IsPlain, SourceLocation Location) : InputNode(Location);
