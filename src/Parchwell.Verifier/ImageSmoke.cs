using Confluent.Kafka;

namespace Parchwell.Verifier;

/// <summary>Checks packaged native and scenario inputs without constructing service clients.</summary>
internal static class ImageSmoke
{
    internal static async Task<int> RunAsync(TextWriter output, string root)
    {
        _ = Library.VersionString;
        foreach (string relative in new[] { "scenarios/A01/scenario.yaml", "scenarios/profiles/saturation-ramp.yaml" })
        {
            var file = new FileInfo(Path.Combine(root, relative));
            if (!file.Exists || file.LinkTarget is not null
                || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidOperationException("A packaged scenario input is not a regular file.");
        }
        await output.WriteLineAsync("{\"native_loaded\":true,\"scenario_files\":2,\"uid_expected\":1654}").ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return 0;
    }
}
