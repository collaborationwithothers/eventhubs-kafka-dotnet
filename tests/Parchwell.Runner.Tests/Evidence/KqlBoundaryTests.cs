#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;
using Parchwell.Runner.Run.Evidence;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

// These synthetic rows retain the flat string-column shape established by the existing CLI recording test.
public sealed class KqlBoundaryTests
{
    private const string SyntheticRow = "{\"TableName\":\"PrimaryResult\",\"ErrorCount\":\"1\",\"Detail\":\"synthetic\"}";

    [Theory]
    [InlineData(9999, "available", "corroborating_only")]
    [InlineData(10000, "available", "corroborating_only")]
    [InlineData(10001, "incomplete", "query_row_limit")]
    public void Given_RowCountBoundary_When_Parsed_Then_RetainsRowsAndEnforcesLimit(int count, string status, string reason)
    {
        byte[] output = Encoding.UTF8.GetBytes("[" + string.Join(',', Enumerable.Repeat(SyntheticRow, count)) + "]");
        var receipt = KqlCollector.Parse(output, "synthetic retained query");
        Assert.Equal(status, receipt.Status);
        Assert.Equal(reason, receipt.Reason);
        Assert.Equal(count, receipt.Rows);
        Assert.Same(output, receipt.OriginalOutput);
        Assert.Equal("synthetic retained query", receipt.Query);
    }

    [Theory]
    [InlineData(16_777_215, true)]
    [InlineData(16_777_216, true)]
    [InlineData(16_777_217, false)]
    public void Given_OutputByteBoundary_When_Parsed_Then_OnlyBoundedOriginalBytesRetained(int size, bool allowed)
    {
        byte[] output = Pad("[" + SyntheticRow + "]", size);
        var receipt = KqlCollector.Parse(output, "synthetic retained query");
        Assert.Equal(allowed ? "available" : "incomplete", receipt.Status);
        Assert.Equal(allowed ? "corroborating_only" : "query_byte_limit", receipt.Reason);
        Assert.Equal(allowed ? 1 : 0, receipt.Rows);
        if (allowed) Assert.Same(output, receipt.OriginalOutput);
        else Assert.Empty(receipt.OriginalOutput);
        Assert.Equal("synthetic retained query", receipt.Query);
    }

    [Theory]
    [InlineData(true, 65_535, true)]
    [InlineData(true, 65_536, true)]
    [InlineData(true, 65_537, false)]
    [InlineData(false, 1_048_575, true)]
    [InlineData(false, 1_048_576, true)]
    [InlineData(false, 1_048_577, false)]
    public async Task Given_PrivateInputByteBoundary_When_Collected_Then_OnlyBoundedInputsInvokeTransport(bool sourceBoundary, int size, bool allowed)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var token = TestContext.Current.CancellationToken;
            byte[] parameters = JsonSerializer.SerializeToUtf8Bytes(new KqlParameters("00000000-0000-0000-0000-000000000001",
                "/subscriptions/synthetic/resourceGroups/synthetic/providers/Microsoft.EventHub/namespaces/synthetic",
                new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 22, 1, 0, 0, TimeSpan.Zero)), LedgerJson.Options);
            byte[] source = Encoding.UTF8.GetBytes("// synthetic boundary query\nprint synthetic='synthetic'");
            if (sourceBoundary) source = Pad(Encoding.UTF8.GetString(source), size);
            else parameters = Pad(Encoding.UTF8.GetString(parameters), size);
            string sourcePath = Path.Combine(directory, "synthetic.kql");
            string parametersPath = Path.Combine(directory, "synthetic.private.json");
            await File.WriteAllBytesAsync(sourcePath, source, token);
            await File.WriteAllBytesAsync(parametersPath, parameters, token);
            string output = Path.Combine(directory, "output");
            var process = new SyntheticQueryProcess();
            var collector = new PrivateEvidenceCollector(new KqlCollector(process, TimeProvider.System), parametersPath, sourcePath);
            if (!allowed)
            {
                var exception = await Assert.ThrowsAsync<InvalidDataException>(() => collector.CollectAsync(output, token));
                Assert.Equal("private_kql_input_limit", exception.Message);
                Assert.Empty(process.Requests);
                Assert.False(File.Exists(Path.Combine(output, "kql", sourceBoundary ? "source.kql" : "parameters.private.json")));
                return;
            }
            await collector.CollectAsync(output, token);
            var request = Assert.Single(process.Requests);
            Assert.Equal("az", request.FileName);
            Assert.Contains(Encoding.UTF8.GetString(source), request.Arguments[request.Arguments.ToList().IndexOf("--analytics-query") + 1], StringComparison.Ordinal);
            Assert.Equal(source, await File.ReadAllBytesAsync(Path.Combine(output, "kql", "source.kql"), token));
            Assert.Equal(parameters, await File.ReadAllBytesAsync(Path.Combine(output, "kql", "parameters.private.json"), token));
            Assert.Equal(Encoding.UTF8.GetBytes("[" + SyntheticRow + "]"), await File.ReadAllBytesAsync(Path.Combine(output, "kql", "cli-output.json"), token));
            using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "kql", "receipt.json"), token));
            Assert.Equal("available", receipt.RootElement.GetProperty("status").GetString());
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MissingPrivateParameters_When_Collected_Then_UnavailableReceiptIsRetainedWithoutQuery(bool parentDirectoryAbsent)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var token = TestContext.Current.CancellationToken;
            byte[] source = Encoding.UTF8.GetBytes("// synthetic boundary query\nprint synthetic='synthetic'");
            string sourcePath = Path.Combine(directory, "synthetic.kql");
            string parametersPath = Path.Combine(parentDirectoryAbsent ? Path.Combine(directory, "absent") : directory, "synthetic.private.json");
            await File.WriteAllBytesAsync(sourcePath, source, token);
            string output = Path.Combine(directory, "output");
            var process = new SyntheticQueryProcess();
            var collector = new PrivateEvidenceCollector(new KqlCollector(process, TimeProvider.System), parametersPath, sourcePath);

            await collector.CollectAsync(output, token);

            Assert.Empty(process.Requests);
            Assert.Equal(source, await File.ReadAllBytesAsync(Path.Combine(output, "kql", "source.kql"), token));
            using var receipt = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, "kql", "receipt.json"), token));
            Assert.Equal("unavailable", receipt.RootElement.GetProperty("status").GetString());
            Assert.Equal("kql_parameters_file_missing", receipt.RootElement.GetProperty("reason").GetString());
            Assert.Equal(0, receipt.RootElement.GetProperty("rows").GetInt32());
            Assert.True(receipt.RootElement.GetProperty("corroborating_only").GetBoolean());
            Assert.False(File.Exists(Path.Combine(output, "kql", "parameters.private.json")));
            Assert.False(File.Exists(Path.Combine(output, "kql", "query.kql")));
            Assert.False(File.Exists(Path.Combine(output, "kql", "cli-output.json")));
            Assert.False(File.Exists(Path.Combine(output, "kql", "cli-error.txt")));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task Given_MalformedPrivateParameters_When_Collected_Then_JsonFailureIsNotReclassifiedAsAbsent()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var token = TestContext.Current.CancellationToken;
            byte[] source = Encoding.UTF8.GetBytes("// synthetic boundary query\nprint synthetic='synthetic'");
            string sourcePath = Path.Combine(directory, "synthetic.kql");
            string parametersPath = Path.Combine(directory, "synthetic.private.json");
            await File.WriteAllBytesAsync(sourcePath, source, token);
            await File.WriteAllBytesAsync(parametersPath, Encoding.UTF8.GetBytes("{"), token);
            string output = Path.Combine(directory, "output");
            var process = new SyntheticQueryProcess();
            var collector = new PrivateEvidenceCollector(new KqlCollector(process, TimeProvider.System), parametersPath, sourcePath);

            await Assert.ThrowsAnyAsync<JsonException>(() => collector.CollectAsync(output, token));

            Assert.Empty(process.Requests);
            Assert.False(File.Exists(Path.Combine(output, "kql", "receipt.json")));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static byte[] Pad(string jsonOrQuery, int size)
    {
        byte[] bytes = new byte[size];
        bytes.AsSpan().Fill((byte)' ');
        Encoding.UTF8.GetBytes(jsonOrQuery).CopyTo(bytes, 0);
        return bytes;
    }

    private sealed class SyntheticQueryProcess : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new ProcessResult(0, "[" + SyntheticRow + "]", ""));
        }
    }
}
