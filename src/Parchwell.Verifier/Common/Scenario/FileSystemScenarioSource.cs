using System.Text;
using Parchwell.Scenarios;

namespace Parchwell.Verifier.Common.Scenario;

/// <summary>Reads only loader-owned logical scenario files beneath one configured source root.</summary>
internal sealed class FileSystemScenarioSource : IScenarioSource
{
    private readonly string _root;
    private readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>Creates a source rooted at a scenario checkout or mounted data directory.</summary>
    public FileSystemScenarioSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException("scenario_source_root_not_found");
        RejectSymlink(_root);
    }

    /// <inheritdoc />
    public ValueTask<TextReader> OpenReadAsync(string logicalPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        string normalized = logicalPath.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(logicalPath) ||
            normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("scenario_logical_path_invalid");
        if (!normalized.StartsWith("scenarios/", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
            throw new InvalidDataException("scenario_logical_path_outside_loader_contract");
        string[] segments = normalized.Split('/');
        bool definitionPath = segments.Length == 3 && segments[2] == "scenario.yaml" && IsSlug(segments[1]);
        bool profilePath = segments.Length == 3 && segments[1] == "profiles" &&
            segments[2].EndsWith(".yaml", StringComparison.Ordinal) && IsSlug(segments[2][..^".yaml".Length]);
        if (!definitionPath && !profilePath)
            throw new InvalidDataException("scenario_logical_path_outside_loader_contract");

        string candidate = Path.GetFullPath(Path.Combine(_root,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, _pathComparison))
            throw new InvalidDataException("scenario_path_escapes_source_root");

        RejectPathLinks(candidate);
        var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult<TextReader>(new StreamReader(stream, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: false));
    }

    private void RejectPathLinks(string candidate)
    {
        string relative = Path.GetRelativePath(_root, candidate);
        string current = _root;
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            RejectSymlink(current);
        }
    }

    private static void RejectSymlink(string path)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { throw new FileNotFoundException("scenario_input_not_found"); }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("scenario_symlinks_are_not_allowed");
    }

    private static bool IsSlug(string value) => value.Length > 0 &&
        char.IsAsciiLetter(value[0]) &&
        value.All(character => char.IsAsciiLetter(character) || char.IsAsciiDigit(character) || character == '-');
}
