using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Parchwell.Verifier.Common.Serialization;
using Xunit;

namespace Parchwell.Verifier.Tests.Host;

#pragma warning disable CA1707 // Repository test naming convention.
public sealed class VerifierCompletionTests
{
    [Theory]
    [InlineData("start-line", 0)]
    [InlineData("verify", 0)]
    [InlineData("verify", 1)]
    [InlineData("probe-gate", 0)]
    [InlineData("probe-gate", 1)]
    public async Task Given_NormalReturn_When_StopAndDisposalFinish_Then_EmitsOneReceiptWithHandlerCode(string mode, int code)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var cleanup = new ObservedCleanup(output);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor(mode, "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new Handler(Parse(mode), _ => Task.FromResult(code)));
            services.AddSingleton<IHostedService>(_ => cleanup);
        }, completionOutputForTests: output);
        Assert.Equal(code, exit);
        Assert.True(cleanup.Stopped);
        Assert.True(cleanup.Disposed);
        if (mode != "probe-gate")
        {
            Assert.Equal("", output.ToString());
            return;
        }
        string line = Assert.Single(output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.StartsWith("PARCHWELL_COMPLETION ", line, StringComparison.Ordinal);
        using JsonDocument json = JsonDocument.Parse(line["PARCHWELL_COMPLETION ".Length..]);
        Assert.Equal(1, json.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(mode, json.RootElement.GetProperty("mode").GetString());
        Assert.Equal(code, json.RootElement.GetProperty("handler_exit_code").GetInt32());
        Assert.True(json.RootElement.GetProperty("cleanup_completed").GetBoolean());
        Assert.Equal("test-commit", json.RootElement.GetProperty("source_commit").GetString());
        JsonElement identity = json.RootElement.GetProperty("identity");
        Assert.Equal("A01", identity.GetProperty("scenario_id").GetString());
        Assert.Equal(mode == "probe-gate" ? 1 : 6, identity.EnumerateObject().Count());
        if (mode != "probe-gate") Assert.Equal("run-1", identity.GetProperty("run_id").GetString());
    }

    [Theory]
    [InlineData("handler")]
    [InlineData("cancel")]
    [InlineData("stop")]
    [InlineData("dispose")]
    [InlineData("startup")]
    public async Task Given_ExecutionOrCleanupFailure_When_ProcessEnds_Then_NoReceipt(string failure)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(provider => new Handler(VerifierMode.ProbeGate, _ =>
            {
                if (failure == "handler") throw new IOException("synthetic handler failure");
                if (failure == "cancel") provider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
                return Task.FromResult(1);
            }));
            services.AddSingleton<IHostedService>(_ => new FailingCleanup(failure));
        }, completionOutputForTests: output);
        Assert.Equal(1, exit);
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ReceiptWriteOrFlushFailure_When_ProcessEnds_Then_ReturnsOne(bool flush)
    {
        using var writer = new BrokenWriter(flush);
        int exit = await Program.RunAsync(VerifierHostTests.ArgumentsFor("probe-gate", "unused"), services =>
        {
            services.RemoveAll<IVerifierModeHandler>();
            services.AddSingleton<IVerifierModeHandler>(new Handler(VerifierMode.ProbeGate, _ => Task.FromResult(0)));
        }, completionOutputForTests: writer);
        Assert.Equal(1, exit);
    }

    private static VerifierMode Parse(string mode) => mode switch
    {
        "start-line" => VerifierMode.StartLine, "verify" => VerifierMode.Verify, _ => VerifierMode.ProbeGate
    };
    private sealed class Handler(VerifierMode mode, Func<CancellationToken, Task<int>> execute) : IVerifierModeHandler
    {
        public VerifierMode Mode => mode;
        public Task<int> ExecuteAsync(CancellationToken cancellationToken) => execute(cancellationToken);
    }
    private sealed class ObservedCleanup(StringWriter output) : IHostedService, IDisposable
    {
        public bool Stopped { get; private set; }
        public bool Disposed { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken)
        {
            Assert.Equal("", output.ToString());
            Stopped = true;
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            Assert.Equal("", output.ToString());
            Disposed = true;
        }
    }
    private sealed class FailingCleanup(string failure) : IHostedService, IDisposable
    {
        public Task StartAsync(CancellationToken cancellationToken) => failure == "startup"
            ? Task.FromException(new IOException("synthetic start failure")) : Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => failure == "stop"
            ? Task.FromException(new IOException("synthetic stop failure")) : Task.CompletedTask;
        public void Dispose() { if (failure == "dispose") throw new IOException("synthetic disposal failure"); }
    }
    private sealed class BrokenWriter(bool flush) : StringWriter(CultureInfo.InvariantCulture)
    {
        public override Task WriteLineAsync(string? value) => flush ? base.WriteLineAsync(value)
            : Task.FromException(new IOException("synthetic write failure"));
        public override Task FlushAsync() => Task.FromException(new IOException("synthetic flush failure"));
    }
}
#pragma warning restore CA1707
