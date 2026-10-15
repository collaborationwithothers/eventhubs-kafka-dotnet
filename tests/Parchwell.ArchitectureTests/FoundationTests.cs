using System.Diagnostics;
using System.Xml.Linq;
using Xunit;

// The repository requires Given_When_Then test names (docs/engineering/dotnet.md).
#pragma warning disable CA1707

namespace Parchwell.ArchitectureTests;

// The build foundation from issue #47 (docs/engineering/dotnet-verification.md). Tests read the project
// manifests that scripts/verify-dotnet.sh evaluates, so they see effective settings, not root declarations.
public sealed class FoundationTests
{
    private static readonly string[] ProjectFolders = ["src", "tests"];
    private static string RepositoryRoot => Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT")
        ?? throw new InvalidOperationException("Run ./scripts/verify-dotnet.sh to collect evaluated project configuration.");

    private static ProjectModel[] Projects => ProjectModel.ReadManifests();

    // dotnet.md "Solution layout": one test project per source project. dotnet-verification.md also
    // requires every project in Parchwell.slnx, because verification builds and tests only the solution.
    [Fact]
    public void Given_DocumentedLayout_When_ProjectsAreEvaluated_Then_AllSourceAndTestProjectsBelongToSolution()
    {
        var expected = ProjectRules.SourceNames.Select(name => $"src/{name}/{name}.csproj")
            .Concat(ProjectRules.SourceNames.Select(name => $"tests/{name}.Tests/{name}.Tests.csproj"))
            .Append("tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj").Order(StringComparer.Ordinal).ToArray();
        var solutionProjects = XDocument.Load(Path.Combine(RepositoryRoot, "Parchwell.slnx"))
            .Descendants("Project").Select(project => project.Attribute("Path")!.Value.Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, solutionProjects);
        var evaluated = Projects.Select(project => Path.GetRelativePath(RepositoryRoot, project.Path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, evaluated);
        foreach (var path in expected)
        {
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, path)), $"Missing project: {path}");
        }
        var discovered = ProjectFolders.SelectMany(folder => Directory.GetFiles(Path.Combine(RepositoryRoot, folder), "*.csproj", SearchOption.AllDirectories))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, discovered);
    }

    // dotnet.md "Correctness settings", checked per project so a local override cannot weaken them. Services
    // also need the settings IDE0130 uses to match namespaces to folders (dotnet-verification.md).
    [Fact]
    public void Given_SourceAndTestProjects_When_ReleaseSettingsAreEvaluated_Then_CorrectnessSettingsAreEnforced()
    {
        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TargetFramework"] = "net10.0",
            ["Nullable"] = "enable",
            ["TreatWarningsAsErrors"] = "true",
            ["AnalysisLevel"] = "latest-recommended",
            ["EnableNETAnalyzers"] = "true",
            ["RunAnalyzersDuringBuild"] = "true",
        };
        foreach (var project in Projects)
        {
            Assert.False(project.Properties["RunAnalyzers"].Equals("false", StringComparison.OrdinalIgnoreCase),
                $"{project.Name}: RunAnalyzers=false overrides analyzer enforcement.");
            foreach (var (property, value) in required)
            {
                Assert.True(project.Properties.TryGetValue(property, out var actual) && actual.Equals(value, StringComparison.OrdinalIgnoreCase),
                    $"{project.Name}: expected effective {property}={value}; got {actual ?? "<missing>"}.");
            }
            if (ProjectRules.ServiceNames.Contains(project.Name, StringComparer.Ordinal))
            {
                Assert.Equal(project.Name, project.Properties["RootNamespace"]);
                Assert.True(project.Properties["EnforceCodeStyleInBuild"].Equals("true", StringComparison.OrdinalIgnoreCase),
                    $"{project.Name}: namespace/folder analysis must run during the build.");
                Assert.Equal(Path.GetDirectoryName(project.Path), project.Properties["ProjectDir"].TrimEnd(Path.DirectorySeparatorChar));
                Assert.Contains("RootNamespace", project.CompilerVisibleProperties);
                Assert.Contains("ProjectDir", project.CompilerVisibleProperties);
            }
        }
    }

    // dotnet.md "Architecture tests" on declared references: a forbidden reference fails even when no
    // type from it is used, which the compiled checks cannot see.
    [Fact]
    public void Given_DeclaredProjectReferences_When_DependencyRulesRun_Then_KafkaAndServicesRespectDirection()
    {
        Assert.Empty(ProjectRules.ForbiddenReferences(Projects));
        foreach (var name in ProjectRules.SourceNames)
        {
            var test = Assert.Single(Projects, project => project.Name == $"{name}.Tests");
            var source = Assert.Single(Projects, project => project.Name == name);
            Assert.Contains(source.Path, test.References);
        }
        var architecture = Assert.Single(Projects, project => project.Name == "Parchwell.ArchitectureTests");
        foreach (var source in Projects.Where(project => ProjectRules.SourceNames.Contains(project.Name, StringComparer.Ordinal)))
        {
            Assert.Contains(source.Path, architecture.References);
        }
    }

    // Negative control approved in issue #47 (dotnet-verification.md "Approved controls"): proves the real
    // validator rejects a forbidden edge, not only that the repository currently has none.
    [Fact]
    public void Given_AllowedAndUnusedForbiddenReferenceFixtures_When_RealGraphValidatorRuns_Then_OnlyForbiddenEdgeIsRejected()
    {
        var fixtureRoot = Path.Combine(Path.GetTempPath(), $"parchwell-reference-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureRoot);
        try
        {
            foreach (var file in Directory.GetFiles(Path.Combine(RepositoryRoot, "tests/verification-fixtures/references"), "*.fixture"))
            {
                File.Copy(file, Path.Combine(fixtureRoot, Path.GetFileNameWithoutExtension(file)));
            }
            var valid = EvaluateFixture(Path.Combine(fixtureRoot, "Allowed.csproj"));
            var invalid = EvaluateFixture(Path.Combine(fixtureRoot, "Forbidden.csproj"));
            var kafka = EvaluateFixture(Path.Combine(fixtureRoot, "Parchwell.Kafka.csproj"));
            var enrichment = EvaluateFixture(Path.Combine(fixtureRoot, "Parchwell.Enrichment.csproj"));
            Assert.Empty(ProjectRules.ForbiddenReferences([valid, kafka, enrichment]));
            var violation = Assert.Single(ProjectRules.ForbiddenReferences([invalid, kafka, enrichment]));
            Assert.Equal("Forbidden project reference: Parchwell.LoadGen -> Parchwell.Enrichment", violation);
        }
        finally
        {
            Directory.Delete(fixtureRoot, recursive: true);
        }
    }

    private static ProjectModel EvaluateFixture(string projectPath)
    {
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "msbuild", projectPath, "-p:Configuration=Release", "-getProperty:MSBuildProjectFullPath,AssemblyName", "-getItem:ProjectReference", "-nologo" })
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet msbuild.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = outputTask.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, $"Fixture evaluation failed: {errorTask.GetAwaiter().GetResult()} {output}");
        var manifest = Path.ChangeExtension(projectPath, ".json");
        File.WriteAllText(manifest, output);
        return ProjectModel.Read(manifest);
    }
}
