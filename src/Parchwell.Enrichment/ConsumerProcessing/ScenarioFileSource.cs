using System.Text;
using Parchwell.Scenarios;

namespace Parchwell.Enrichment.ConsumerProcessing;

/// <summary>Reads the two logical scenario files the loader asks for from beneath one root directory, and nothing else.</summary>
internal sealed class ScenarioFileSource : IScenarioSource
{
    private readonly string _root;

    internal ScenarioFileSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        string[] segments = logicalPath.Split('/');
        // Only scenarios/<ID>/scenario.yaml and scenarios/profiles/<slug>.yaml, with slug-shaped names, can name a file.
        bool definition = segments is ["scenarios", var id, "scenario.yaml"] && IsSlug(id);
        bool profile = segments is ["scenarios", "profiles", var file] && file.EndsWith(".yaml", StringComparison.Ordinal) && IsSlug(file[..^".yaml".Length]);
        if (!definition && !profile) throw new InvalidDataException("scenario_logical_path_outside_loader_contract");
        string path = Path.Combine([_root, .. segments]);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult<TextReader>(new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, false));
    }

    private static bool IsSlug(string value) => value.Length > 0 && char.IsAsciiLetter(value[0]) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
