using System.Text.Json;

namespace Parchwell.ArchitectureTests;

/// <summary>
/// One evaluated Compile item: the file path and, for a file outside the project folder, the Link
/// path that places it inside the project's logical folders.
/// </summary>
internal sealed record SourceModel(string FullPath, string? Link);

/// <summary>
/// One project's evaluated Release configuration, read from the JSON manifest that
/// scripts/verify-dotnet.sh writes with dotnet msbuild -getProperty and -getItem. The architecture
/// tests check these effective values instead of trusting what the root files declare.
/// </summary>
internal sealed record ProjectModel(string Path, IReadOnlyDictionary<string, string> Properties, string[] References,
    string[] CompilerVisibleProperties, SourceModel[] Sources)
{
    internal string Name => Properties["AssemblyName"];

    internal static ProjectModel Read(string manifest)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var properties = document.RootElement.GetProperty(nameof(Properties)).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var references = document.RootElement.GetProperty("Items").GetProperty("ProjectReference")
            .EnumerateArray().Select(item => System.IO.Path.GetFullPath(item.GetProperty("FullPath").GetString()!)).ToArray();
        var compilerVisibleProperties = document.RootElement.GetProperty("Items").TryGetProperty("CompilerVisibleProperty", out var visible)
            ? visible.EnumerateArray().Select(item => item.GetProperty("Identity").GetString()!).ToArray()
            : [];
        var sources = document.RootElement.GetProperty("Items").TryGetProperty("Compile", out var compile)
            ? compile.EnumerateArray().Select(item => new SourceModel(item.GetProperty("FullPath").GetString()!,
                item.TryGetProperty("Link", out var link) ? link.GetString() : null)).ToArray()
            : [];
        return new ProjectModel(System.IO.Path.GetFullPath(properties["MSBuildProjectFullPath"]), properties, references, compilerVisibleProperties, sources);
    }

    internal static ProjectModel[] ReadManifests()
    {
        var manifests = Environment.GetEnvironmentVariable("PARCHWELL_PROJECT_MANIFEST_DIRECTORY")
            ?? throw new InvalidOperationException("Missing evaluated project manifests. Run ./scripts/verify-dotnet.sh.");
        return Directory.GetFiles(manifests, "*.json").Select(Read).ToArray();
    }
}

/// <summary>
/// The documented project inventory and dependency direction (docs/engineering/dotnet.md
/// "Solution layout" and "Architecture tests"), shared by the declared-reference and compiled checks.
/// </summary>
internal static class ProjectRules
{
    internal static readonly string[] SourceNames =
    ["Parchwell.Kafka", "Parchwell.Scenarios", "Parchwell.LoadGen", "Parchwell.Enrichment", "Parchwell.Verifier"];

    internal static readonly string[] ServiceNames =
    ["Parchwell.LoadGen", "Parchwell.Enrichment", "Parchwell.Verifier"];

    internal static string[] ForbiddenReferences(IEnumerable<ProjectModel> projects)
    {
        var projectList = projects.ToArray();
        var namesByPath = projectList.ToDictionary(project => project.Path, project => project.Name, StringComparer.Ordinal);
        return projectList.SelectMany(project => project.References.Select(reference =>
            (Origin: project.Name, Target: namesByPath.GetValueOrDefault(reference) ?? System.IO.Path.GetFileNameWithoutExtension(reference))))
            .Where(edge =>
                (edge.Origin == "Parchwell.Kafka" && edge.Target.StartsWith("Parchwell.", StringComparison.Ordinal)) ||
                (ServiceNames.Contains(edge.Origin, StringComparer.Ordinal) && ServiceNames.Contains(edge.Target, StringComparer.Ordinal) && edge.Origin != edge.Target))
            .Select(edge => $"Forbidden project reference: {edge.Origin} -> {edge.Target}").ToArray();
    }
}
