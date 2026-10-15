namespace Parchwell.Scenarios;

/// <summary>Required per-document parsing budgets; zero is deliberately invalid.</summary>
/// <remarks>
/// Each budget bounds the work one YAML document can cause. A document over any budget is rejected,
/// never truncated. There are no defaults, so the caller must choose every budget
/// (docs/operations/scenario-contract.md, "Input and ownership").
/// </remarks>
public sealed class ScenarioLoadOptions
{
    /// <summary>Maximum characters read from one document, counted as UTF-16 reader characters.</summary>
    public int MaxDocumentCharacters { get; set; }
    /// <summary>Maximum nesting of mappings and sequences.</summary>
    public int MaxNestingDepth { get; set; }
    /// <summary>Maximum characters in one scalar, including mapping keys.</summary>
    public int MaxScalarCharacters { get; set; }
    /// <summary>Maximum YAML parser events of any kind in one document.</summary>
    public int MaxParserEvents { get; set; }
}
