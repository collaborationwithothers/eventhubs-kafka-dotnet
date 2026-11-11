namespace Parchwell.LoadGen.Run.Abrupt;

internal sealed record ChildCommand(string FileName, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string> ExtraEnvironment)
{
    internal static ChildCommand ForRunningApphost()
    {
        return ForRunningApphost(AppContext.BaseDirectory, Environment.ProcessPath);
    }

    internal static ChildCommand ForRunningApphost(string baseDirectory, string? processPath)
    {
        var expected = Path.Combine(baseDirectory, "Parchwell.LoadGen");
        if (processPath != expected)
            throw B10AbruptOptions.WithDetail(B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_configuration_invalid")),
                B10AbruptOptions.ValidationDetail.ApphostProcessPathMismatch);
        if (!File.Exists(expected))
            throw B10AbruptOptions.WithDetail(B10AbruptDiagnostics.Attach(new InvalidOperationException("b10_abrupt_configuration_invalid")),
                B10AbruptOptions.ValidationDetail.ApphostMissing);
        return new(expected, ["--b10-abrupt-child"], new Dictionary<string, string>());
    }
}
