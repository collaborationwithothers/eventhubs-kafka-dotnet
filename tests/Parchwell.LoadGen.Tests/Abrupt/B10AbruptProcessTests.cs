using System.Globalization;
using System.Text;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

public sealed class B10AbruptProcessTests
{
    [Theory]
    [InlineData("worker", "123")]
    [InlineData("worker (nested) space", "9223372036854775807")]
    [InlineData("a ) ) ( b", "0")]
    public void Given_CommContainsParentheses_When_ParseStartTime_Then_UsesLastDelimiter(string comm, string ticks)
    {
        var fields = Enumerable.Repeat("0", 23).ToArray();
        fields[0] = "S"; fields[19] = ticks;
        Assert.Equal(long.Parse(ticks, CultureInfo.InvariantCulture), B10AbruptProc.ReadStartTimeTicks(Encoding.ASCII.GetBytes("7 (" + comm + ")   " + string.Join("  ", fields) + "  ")));
    }

    [Theory]
    [InlineData("no delimiter")]
    [InlineData("1 (x) S 1")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 -1")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 9223372036854775808")]
    [InlineData("1 (x) S 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1.5")]
    public void Given_MalformedStat_When_Parse_Then_Rejects(string stat) =>
        Assert.Equal("b10_abrupt_child_identity_mismatch", Assert.Throws<InvalidDataException>(() => B10AbruptProc.ReadStartTimeTicks(Encoding.ASCII.GetBytes(stat))).Message);

    [Theory]
    [InlineData(65535, true)]
    [InlineData(65536, true)]
    [InlineData(65537, false)]
    public void Given_ProcReadByteBoundary_When_Read_Then_RejectsExcess(int count, bool valid)
    {
        string path = Path.Combine(Path.GetTempPath(), "b10-proc-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllBytes(path, new byte[count]);
            if (valid) Assert.Equal(count, B10AbruptProc.ReadBounded(path).Length);
            else Assert.Throws<IOException>(() => B10AbruptProc.ReadBounded(path));
        }
        finally { File.Delete(path); }
        Assert.Throws<FileNotFoundException>(() => B10AbruptProc.ReadBounded(path));
    }

    [Fact]
    public void Given_SdkDotnetParent_When_ApphostCommandIsValidated_Then_RejectsWithNamedDetail()
    {
        var productionFailure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost("/app/", "/usr/share/dotnet/dotnet"));
        Assert.Equal("ApphostProcessPathMismatch", B10AbruptOptions.ReadValidationDetail(productionFailure));
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        string expected = Path.Combine(directory, "Parchwell.LoadGen");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(expected, "synthetic-apphost");
            var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(directory, "/usr/share/dotnet/dotnet"));
            using var errors = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(1, B10AbruptHost.ReportFailure(failure, errors));
            B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_configuration_invalid validation_detail=ApphostProcessPathMismatch exception_type=System.InvalidOperationException");
            ChildCommand command = ChildCommand.ForRunningApphost(directory, expected);
            Assert.Equal(expected, command.FileName);
            Assert.Equal(["--b10-abrupt-child"], command.Arguments);
            Assert.Empty(command.ExtraEnvironment);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void Given_MissingPublishedApphost_When_ApphostCommandIsValidated_Then_RejectsWithDistinctDetail()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(directory, Path.Combine(directory, "Parchwell.LoadGen")));
        using var errors = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(1, B10AbruptHost.ReportFailure(failure, errors));
        B10AbruptDiagnosticsTests.AssertPlainReport(errors.ToString(), "b10_abrupt_configuration_invalid validation_detail=ApphostMissing exception_type=System.InvalidOperationException");
    }

    internal static B10AbruptOptions Options(string bindingPath = "/work/start-line.json") => B10AbruptOptions.Read(new Dictionary<string, string>
    {
        ["B10Abrupt__Enabled"] = "true", ["B10Abrupt__Purpose"] = "diagnostic_trial", ["B10Abrupt__Role"] = "parent",
        ["B10Abrupt__RunId"] = "test", ["B10Abrupt__ScenarioId"] = "B10", ["B10Abrupt__ConfigurationId"] = "C1", ["B10Abrupt__Phase"] = "C1",
        ["B10Abrupt__Repeat"] = "1", ["B10Abrupt__SourceCommit"] = new('a', 40), ["B10Abrupt__ImageReference"] = "example/image@sha256:" + new string('b', 64),
        ["B10Abrupt__JobName"] = "test-c1-r1-loadgen-restart", ["B10Abrupt__PodName"] = "first", ["B10Abrupt__PodUid"] = "uid",
        ["B10Abrupt__NamespaceName"] = "parchwell", ["B10Abrupt__BlobEndpoint"] = "https://example.blob.core.windows.net",
        ["B10Abrupt__ScenarioSourceRoot"] = "/scenarios", ["B10Abrupt__OutputRoot"] = "/work/loadgen", ["B10Abrupt__StartLineBindingPath"] = bindingPath,
    });

}

public sealed class B10AbruptPublishedApphostTests
{
    [Fact]
    public void Given_ChartSelectedPublishedApphost_When_PathGuardRuns_Then_SelectsOnlyThatChildExecutable()
    {
        string? apphost = Environment.GetEnvironmentVariable("PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST");
        Assert.SkipUnless(apphost is not null, "Set PARCHWELL_B10_ABRUPT_PUBLISHED_APPHOST via check-loadgen-host.sh.");
        Assert.True(File.Exists(apphost));
        Assert.Equal("Parchwell.LoadGen", Path.GetFileName(apphost));
        ChildCommand command = ChildCommand.ForRunningApphost(Path.GetDirectoryName(apphost)!, apphost);
        Assert.Equal(apphost, command.FileName);
        Assert.Equal(["--b10-abrupt-child"], command.Arguments);
        Assert.Empty(command.ExtraEnvironment);
        var failure = Assert.Throws<InvalidOperationException>(() => ChildCommand.ForRunningApphost(Path.GetDirectoryName(apphost)!, "/usr/share/dotnet/dotnet"));
        Assert.Equal("ApphostProcessPathMismatch", B10AbruptOptions.ReadValidationDetail(failure));
    }
}
