using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Parchwell.ArchitectureTests;

internal static class SourceNamespaceRules
{
    internal static IEnumerable<string> Validate(ProjectModel project)
    {
        var projectDirectory = Path.GetDirectoryName(project.Path)!;
        if (!LanguageVersionFacts.TryParse(project.Properties["LangVersion"], out var languageVersion))
        {
            yield return $"{project.Name}: unsupported C# language version {project.Properties["LangVersion"]}.";
            yield break;
        }
        var symbols = project.Properties["DefineConstants"].Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new CSharpParseOptions(languageVersion, preprocessorSymbols: symbols);
        foreach (var source in project.Sources)
        {
            var relativeSource = LogicalPath(source, projectDirectory);
            if (relativeSource is null)
            {
                yield return $"{source.FullPath}: external source requires a relative Link path inside {projectDirectory}.";
                continue;
            }
            var directory = Path.GetDirectoryName(relativeSource);
            var folders = string.IsNullOrEmpty(directory) ? [] : directory.Split(Path.DirectorySeparatorChar);
            if (folders.Any(folder => folder.StartsWith('@') || !SyntaxFacts.IsValidIdentifier(folder)))
            {
                yield return $"{source.FullPath}: service folders must be single, unescaped C# namespace identifiers.";
                continue;
            }
            var expectedNamespace = string.Join('.', new[] { project.Properties["RootNamespace"] }.Concat(folders));
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(source.FullPath), options, source.FullPath);
            var errors = tree.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
            {
                foreach (var error in errors)
                {
                    yield return $"{source.FullPath}: source namespace validation could not parse active Release code: {error}.";
                }
                continue;
            }
            foreach (var declaration in tree.GetRoot().DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
            {
                var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                    .Select(declarationNamespace => string.Concat(declarationNamespace.Name.DescendantTokens().Select(token => token.ValueText)));
                var actualNamespace = string.Join('.', namespaces);
                if (actualNamespace != expectedNamespace)
                {
                    var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    yield return $"{source.FullPath}:{line}: declared type namespace '{actualNamespace}' must match logical folder namespace '{expectedNamespace}'.";
                }
            }
        }
    }

    private static string? LogicalPath(SourceModel source, string projectDirectory)
    {
        var physicalRelative = Path.GetRelativePath(projectDirectory, source.FullPath);
        if (physicalRelative != ".." && !physicalRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return physicalRelative;
        }
        if (string.IsNullOrEmpty(source.Link))
        {
            return null;
        }
        var logical = source.Link.Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(logical))
        {
            return null;
        }
        var normalized = Path.GetRelativePath(projectDirectory, Path.GetFullPath(logical, projectDirectory));
        return normalized == ".." || normalized.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? null
            : normalized;
    }
}
