using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Rehearsal;

public sealed record WireFile(string Path, long SizeBytes, string Sha256);
public sealed record WireManifest(int SchemaVersion, string SourceCommit, WireFile[] Files);

public sealed class WireFixtures
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };
    private readonly Dictionary<string, byte[]> files;
    public string SourceCommit { get; }
    private WireFixtures(string sourceCommit, Dictionary<string, byte[]> files) { SourceCommit = sourceCommit; this.files = files; }
    public byte[] Get(string path) => files.TryGetValue(path, out byte[]? bytes) ? bytes : throw new InvalidDataException("wire_fixture_missing");
    public static string BuiltSourceCommit()
    {
        string version = typeof(WireFixtures).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        string source = version.Split('+').Last();
        return source.Length >= 40 && source[..40].All(Uri.IsHexDigit) ? source[..40] : throw new InvalidDataException("build_source_commit_missing");
    }
    public static async Task<WireFixtures> LoadAsync(string directory, CancellationToken cancellationToken)
    {
        string manifestPath = Path.Combine(directory, "wire-manifest.json");
        if (new FileInfo(manifestPath).Length > 1048576) throw new InvalidDataException("wire_manifest_limit");
        WireManifest manifest = JsonSerializer.Deserialize<WireManifest>(await File.ReadAllBytesAsync(manifestPath, cancellationToken).ConfigureAwait(false),
            ManifestOptions)
            ?? throw new InvalidDataException("wire_manifest_missing");
        if (manifest.SchemaVersion != 1 || manifest.SourceCommit != BuiltSourceCommit() || manifest.Files.Length is < 1 or > 256)
            throw new InvalidDataException("wire_manifest_source_or_count");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long total = 0;
        foreach (WireFile file in manifest.Files)
        {
            if (Path.IsPathRooted(file.Path) || file.Path.Split('/').Any(part => part is "." or ".." or "") || file.Path.Contains('\\', StringComparison.Ordinal))
                throw new InvalidDataException("wire_fixture_path");
            string path = Path.Combine(directory, file.Path);
            var info = new FileInfo(path);
            if (info.LinkTarget is not null || info.Length != file.SizeBytes || info.Length > 16777216) throw new InvalidDataException("wire_fixture_size");
            total = checked(total + info.Length);
            if (total > 268435456) throw new InvalidDataException("wire_fixture_total_limit");
            byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256) throw new InvalidDataException("wire_fixture_hash");
            files.Add(file.Path, bytes);
        }
        return new(manifest.SourceCommit, files);
    }
    public WorkerIdentity Identity(string phase, int ordinal)
    {
        using JsonDocument result = JsonDocument.Parse(Get(phase + "/r" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/result.json"));
        return WorkerIdentity.Read(result.RootElement.GetProperty("identity"));
    }
}
