#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text.RegularExpressions;
using Xunit;

namespace Parchwell.ArchitectureTests;

public sealed class B10AbruptFailureSiteTests
{
    [Fact]
    public void Given_AbruptFailureExceptions_When_SitesAreScanned_Then_EverySiteCarriesCompilerCallerInformation()
    {
        string root = Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT")
            ?? throw new InvalidOperationException("Run ./scripts/verify-dotnet.sh to identify the repository root.");
        var pattern = new Regex("""new\s+(?:[\w.]+)?Exception\s*\(\s*(?:\w+\s*\?\s*)?"(?:b10_abrupt_|kill_too_)|new\s*\(\s*"(?:b10_abrupt_|kill_too_)|new\s+(?:OwnedProducerProcess\.)?SignalException\s*\(""", RegexOptions.CultureInvariant);
        var violations = new List<string>();
        foreach (string file in new[] { "B10AbruptSupervisor.cs", "B10AbruptWire.cs", "B10AbruptPublisher.cs" })
        {
            string[] lines = File.ReadAllLines(Path.Combine(root, "src/Parchwell.LoadGen/Run/Abrupt", file));
            int count = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal) || !pattern.IsMatch(line)) continue;
                count++;
                if (!line.Contains("Attach(", StringComparison.Ordinal) && !line.Contains("[CallerLineNumber]", StringComparison.Ordinal)) violations.Add($"{file}:{index + 1}");
            }
            Assert.True(count > 0, $"No failure site was checked in {file}.");
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }
}
