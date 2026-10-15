namespace Parchwell.Scenarios;

/// <summary>Required per-document parsing budgets; zero is deliberately invalid.</summary>
public sealed class ScenarioLoadOptions
{
    public int MaxDocumentCharacters { get; set; }
    public int MaxNestingDepth { get; set; }
    public int MaxScalarCharacters { get; set; }
    public int MaxParserEvents { get; set; }
}
