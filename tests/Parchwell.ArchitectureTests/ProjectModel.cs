using System.Text.Json;

namespace Parchwell.ArchitectureTests;

internal sealed record ProjectModel(string Path, IReadOnlyDictionary<string, string> Properties, string[] References)
{
    internal string Name => Properties["AssemblyName"];

    internal static ProjectModel Read(string manifest)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifest));
        var properties = document.RootElement.GetProperty(nameof(Properties)).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!, StringComparer.Ordinal);
        var references = document.RootElement.GetProperty("Items").GetProperty("ProjectReference")
            .EnumerateArray().Select(item => System.IO.Path.GetFullPath(item.GetProperty("FullPath").GetString()!)).ToArray();
        return new ProjectModel(System.IO.Path.GetFullPath(properties["MSBuildProjectFullPath"]), properties, references);
    }
}

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
