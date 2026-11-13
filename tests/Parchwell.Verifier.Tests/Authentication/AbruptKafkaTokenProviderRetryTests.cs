using System.Diagnostics;
using System.Net;
using System.Text;
using Azure.Core.Pipeline;
using Parchwell.Kafka.Authentication;
using Parchwell.Verifier.VerifyRun.Abrupt;
using Xunit;

namespace Parchwell.Verifier.Tests.Authentication;

#pragma warning disable CA1707 // Repository test naming convention.
/// <summary>Runs the abrupt-verify token provider's real credential against a scripted token endpoint.</summary>
[Trait("Tier", "Slow")]
public sealed class AbruptKafkaTokenProviderRetryTests : IDisposable
{
    private const string Scope = "https://eventhubs.azure.net/.default";
    private readonly string tokenFile = Path.Combine(Path.GetTempPath(), "abrupt-token-" + Guid.NewGuid().ToString("N"));

    public AbruptKafkaTokenProviderRetryTests() => File.WriteAllText(tokenFile, "synthetic-assertion");

    public void Dispose() => File.Delete(tokenFile);

    [Fact]
    public async Task Given_FirstPostBlocks_When_TokenRequested_Then_OneRetrySucceeds()
    {
        using var handler = new ScriptedHandler((number, token) => number == 1 ? Block(token) : Task.FromResult(Token("synthetic-2")));
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var watch = Stopwatch.StartNew();
        var token = Provider(handler).GetToken(Scope, caller.Token);
        watch.Stop();
        Assert.Equal("synthetic-2", token.Token);
        Assert.Equal(2, handler.Posts);
        Assert.True(watch.Elapsed >= TimeSpan.FromSeconds(1.9), watch.Elapsed.ToString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), watch.Elapsed.ToString());
        Assert.False(caller.IsCancellationRequested);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Given_EveryPostBlocks_When_TokenRequested_Then_FailsAfterTwoPosts()
    {
        using var handler = new ScriptedHandler((_, token) => Block(token));
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var watch = Stopwatch.StartNew();
        Assert.ThrowsAny<Exception>(() => Provider(handler).GetToken(Scope, caller.Token));
        watch.Stop();
        Assert.Equal(2, handler.Posts);
        Assert.True(watch.Elapsed >= TimeSpan.FromSeconds(3.9), watch.Elapsed.ToString());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), watch.Elapsed.ToString());
        Assert.False(caller.IsCancellationRequested);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Posts);
    }

    [Fact]
    public async Task Given_EveryPostAnswers503_When_TokenRequested_Then_FailsAfterTwoPosts()
    {
        using var handler = new ScriptedHandler((_, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable, "{\"error\":\"temporarily_unavailable\"}")));
        using var caller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.ThrowsAny<Exception>(() => Provider(handler).GetToken(Scope, caller.Token));
        Assert.Equal(2, handler.Posts);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(2, handler.Posts);
    }

    [Fact]
    public async Task Given_CallerCancelsDuringFirstPost_When_TokenRequested_Then_NoSecondPostIsSent()
    {
        using var caller = new CancellationTokenSource();
        // The 0.5 s starts when the first POST arrives, so the cancellation always lands during that attempt.
        // A timer started before GetToken can fire during credential start-up, before any POST is sent.
        using var handler = new ScriptedHandler((_, token) =>
        {
            caller.CancelAfter(TimeSpan.FromMilliseconds(500));
            return Block(token);
        });
        Assert.ThrowsAny<Exception>(() => Provider(handler).GetToken(Scope, caller.Token));
        Assert.Equal(1, handler.Posts);
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Posts);
    }

    private AbruptKafkaTokenProvider Provider(ScriptedHandler handler) => new(
        new KafkaWorkloadIdentityOptions
        {
            TenantId = "11111111-1111-1111-1111-111111111111",
            ClientId = "22222222-2222-2222-2222-222222222222",
            TokenFilePath = tokenFile
        },
        new HttpClientTransport(handler.Client));

    // "Blocks": waits until the handler's own token is cancelled, then throws for that token.
    private static async Task<HttpResponseMessage> Block(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("Unreachable.");
    }

    private static HttpResponseMessage Token(string accessToken) =>
        Json(HttpStatusCode.OK, "{\"token_type\":\"Bearer\",\"expires_in\":3600,\"access_token\":\"" + accessToken + "\"}");

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> post;
        private int posts;

        internal ScriptedHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> post)
        {
            this.post = post;
            Client = new HttpClient(this, disposeHandler: false);
        }

        internal HttpClient Client { get; }

        internal int Posts => Volatile.Read(ref posts);

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            SendAsync(request, cancellationToken).GetAwaiter().GetResult();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/oauth2/v2.0/token", StringComparison.Ordinal))
                return post(Interlocked.Increment(ref posts), cancellationToken);
            return Task.FromResult(Json(HttpStatusCode.OK, "{}"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Client.Dispose();
            base.Dispose(disposing);
        }
    }
}
#pragma warning restore CA1707
