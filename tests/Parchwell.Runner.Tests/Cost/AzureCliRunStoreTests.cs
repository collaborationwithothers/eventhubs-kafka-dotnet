#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text.Json;
using Xunit;
using Parchwell.Runner.Run.Contracts;
using Parchwell.Runner.Run.Cost;

namespace Parchwell.Runner.Tests.Cost;

public sealed class AzureCliRunStoreTests
{
    private static readonly string[] CreateConflictCommands = ["show", "upload"];
    private static readonly string[] UpdateConflictCommands = ["show", "download", "upload"];
    private static readonly string[] ExplicitReadCommands = ["show", "download"];

    [Fact]
    public async Task Given_Read_When_Downloaded_Then_UsesPropertyEtagAndExplicitEntraLogin()
    {
        var process = new RecordingProcess();
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        process.Handler = request =>
        {
            if (request.Arguments.Contains("show")) return new(0, "{\"properties\":{\"etag\":\"etag-a\",\"contentLength\":3}}", "");
            var arguments = request.Arguments.ToList();
            File.WriteAllBytes(arguments[arguments.IndexOf("--file") + 1], [1, 2, 3]);
            Assert.Equal("etag-a", arguments[arguments.IndexOf("--if-match") + 1]);
            return new(0, "", "");
        };
        var result = await store.ReadAsync("ledger", "A01/state.json", 1024, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Bytes);
        Assert.All(process.Requests, request =>
        {
            var arguments = request.Arguments.ToList();
            Assert.Equal("login", arguments[arguments.IndexOf("--auth-mode") + 1]);
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            Assert.DoesNotContain("--account-key", arguments);
            Assert.DoesNotContain("--sas-token", arguments);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_ConditionalWrite_When_Uploaded_Then_NoWriteRetryAndCorrectPrecondition(bool create)
    {
        var process = new RecordingProcess { Handler = _ => new(1, "", "ConditionNotMet (412)") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        var result = await store.WriteAsync("ledger", "A01/state.json", new byte[] { 1 }, create ? null : "etag-a", create, TestContext.Current.CancellationToken);
        Assert.Equal(ArtifactWriteOutcome.Conflict, result);
        var request = Assert.Single(process.Requests);
        Assert.Contains(create ? "--if-none-match" : "--if-match", request.Arguments);
        Assert.Contains(create ? "*" : "etag-a", request.Arguments);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_SyntheticCommittedWriteWithCli412_When_LedgerTransitions_Then_StopsWithoutReadbackOrReinvocation(bool create)
    {
        const string sourceCommit = "1111111111111111111111111111111111111111";
        const string initialETag = "synthetic-etag-before";
        var clock = new DeadlineRecordingClock();
        var process = new RecordingProcess();
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath(), clock);
        var ledger = new RunLedger(store, clock);
        var input = PhaseReservationTests.Input();
        byte[]? committedBytes = null;
        byte[]? uploadedBytes = null;
        string currentETag = initialETag;
        bool returnConflict = create;
        process.Handler = request =>
        {
            var arguments = request.Arguments.ToList();
            switch (arguments[2])
            {
                case "show":
                    return committedBytes is null ? new(1, "", "BlobNotFound") :
                        new(0, JsonSerializer.Serialize(new { properties = new { etag = currentETag, contentLength = committedBytes.Length } }), "");
                case "download":
                    Assert.NotNull(committedBytes);
                    Assert.Equal(currentETag, arguments[arguments.IndexOf("--if-match") + 1]);
                    File.WriteAllBytes(arguments[arguments.IndexOf("--file") + 1], committedBytes);
                    return new(0, "", "");
                case "upload":
                    uploadedBytes = File.ReadAllBytes(arguments[arguments.IndexOf("--file") + 1]);
                    committedBytes = uploadedBytes.ToArray();
                    // Synthetic CLI outcome: committed bytes followed by 412. This fake does not execute SDK HTTP retries.
                    currentETag = returnConflict ? "synthetic-etag-after-conflict" : initialETag;
                    return returnConflict ? new(1, "", "ConditionNotMet (412)") : new(0, "", "");
                default:
                    throw new InvalidOperationException("unexpected_synthetic_blob_command");
            }
        };
        if (!create)
        {
            await ledger.InitializeAsync(input, sourceCommit, true, true, TestContext.Current.CancellationToken);
            process.Requests.Clear();
            clock.DueTimes.Clear();
            returnConflict = true;
        }

        LedgerDocument? returned = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            returned = create
                ? await ledger.InitializeAsync(input, sourceCommit, true, true, TestContext.Current.CancellationToken)
                : await ledger.ReserveAsync("probe", "synthetic-env", input, TestContext.Current.CancellationToken);
        });

        Assert.Equal("ledger_etag_conflict", error.Message);
        Assert.Null(returned);
        Assert.NotNull(uploadedBytes);
        Assert.Equal(uploadedBytes, committedBytes);
        Assert.Equal(create ? CreateConflictCommands : UpdateConflictCommands,
            process.Requests.Select(request => request.Arguments[2]));
        var upload = Assert.Single(process.Requests, request => request.Arguments.Contains("upload"));
        var uploadArguments = upload.Arguments.ToList();
        Assert.Equal(create ? "*" : initialETag, uploadArguments[uploadArguments.IndexOf(create ? "--if-none-match" : "--if-match") + 1]);
        Assert.Equal(create ? "false" : "true", uploadArguments[uploadArguments.IndexOf("--overwrite") + 1]);
        Assert.DoesNotContain(create ? "--if-match" : "--if-none-match", uploadArguments);
        Assert.All(process.Requests, request =>
        {
            Assert.Equal("az", request.FileName);
            Assert.Equal(TimeSpan.FromSeconds(60), request.Timeout);
            var arguments = request.Arguments.ToList();
            Assert.Equal("ledger", arguments[arguments.IndexOf("--container-name") + 1]);
            Assert.Equal(RunLedger.BlobPath, arguments[arguments.IndexOf("--name") + 1]);
            Assert.Equal("login", arguments[arguments.IndexOf("--auth-mode") + 1]);
        });
        Assert.Equal(3, clock.DueTimes.Count);
        Assert.All(clock.DueTimes, dueTime => Assert.Equal(TimeSpan.FromSeconds(180), dueTime));

        // A later explicit read can observe the committed state without treating the failed transition as returned success.
        var observed = await ledger.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(uploadedBytes, JsonSerializer.SerializeToUtf8Bytes(observed, LedgerJson.Options));
        Assert.Equal(create ? 0 : 1, observed.Revision);
        Assert.Equal(create ? "not_started" : "reserved", observed.Phases["probe"].State);
        Assert.Equal(create ? null : "synthetic-env", observed.Phases["probe"].EnvironmentRunId);
        Assert.Equal(create ? 4 : 5, process.Requests.Count);
        Assert.Equal(ExplicitReadCommands, process.Requests.TakeLast(2).Select(request => request.Arguments[2]));
        Assert.Single(process.Requests, request => request.Arguments.Contains("upload"));
    }

    [Theory]
    [InlineData("https://stexample.blob.core.windows.net/?sig=private")]
    [InlineData("http://stexample.blob.core.windows.net")]
    [InlineData("https://stexample.blob.core.windows.net.attacker.invalid")]
    [InlineData("https://user:secret@stexample.blob.core.windows.net")]
    public void Given_UnsafeAccountUrl_When_Constructed_Then_Rejects(string url) =>
        Assert.Throws<InvalidDataException>(() => new AzureCliRunStore(new RecordingProcess(), url, Path.GetTempPath()));

    [Fact]
    public async Task Given_UnsupportedConditionalFlag_When_CliVerified_Then_BlocksDeployment()
    {
        var process = new RecordingProcess { Handler = _ => new(0, "{}", "") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.VerifyCliAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Given_MissingBlobOrUncertainUpload_When_Invoked_Then_ReturnsExplicitState()
    {
        var process = new RecordingProcess { Handler = _ => new(1, "", "BlobNotFound") };
        var store = new AzureCliRunStore(process, "https://stexample.blob.core.windows.net", Path.GetTempPath());
        Assert.Null(await store.ReadAsync("ledger", "A01/state.json", 1024, TestContext.Current.CancellationToken));
        process.Handler = _ => new(1, "", "connection ended after sending request");
        Assert.Equal(ArtifactWriteOutcome.Uncertain, await store.WriteAsync("ledger", "A01/state.json", new byte[] { 1 }, null, true, TestContext.Current.CancellationToken));
    }

    private sealed class RecordingProcess : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Func<ProcessRequest, ProcessResult> Handler { get; set; } = _ => new(0, "", "");
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Handler(request));
        }
    }

    private sealed class DeadlineRecordingClock : TimeProvider
    {
        public List<TimeSpan> DueTimes { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            DueTimes.Add(dueTime);
            return base.CreateTimer(callback, state, dueTime, period);
        }
    }
}
