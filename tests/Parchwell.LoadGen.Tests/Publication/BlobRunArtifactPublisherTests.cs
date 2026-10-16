using System.Collections.Immutable;
using Azure.Storage.Blobs;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Execution;
using Parchwell.LoadGen.Run.Publication;
using Xunit;

namespace Parchwell.LoadGen.Tests.Publication;

#pragma warning disable CA1707 // Repository test naming convention.

public sealed class BlobRunArtifactPublisherTests
{
    [Fact]
    public void GivenValidSdkClient_WhenConstructingPublisher_ThenItIsAvailableWithoutRequestingCredentials()
    {
        // The production adapter is considered available when its SDK destination is configured; token access happens on upload.
        var client = new BlobServiceClient(new Uri("https://storage.example.test"));
        var publisher = new BlobRunArtifactPublisher(client, TimeProvider.System);

        Assert.True(publisher.IsAvailable);
    }

    [Fact]
    public async Task GivenEndpointDifferentFromInjectedClient_WhenPublishing_ThenRejectsBeforeReadingTheRun()
    {
        // A deployment option cannot redirect a client that the composition root constructed for another account.
        BlobRunArtifactPublisher publisher = CreatePublisher("https://storage.example.test");
        RunExecutionResult incomplete = IncompleteResult();

        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(incomplete,
            new RunPublicationOptions("https://other.example.test", "ledger", TimeSpan.FromSeconds(5), 4096, 8192),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenFailedWorkerResult_WhenPublishing_ThenRejectsBeforeOpeningArtifactPaths()
    {
        // An incomplete worker cannot publish an accepted index even if a Blob destination is reachable.
        BlobRunArtifactPublisher publisher = CreatePublisher("https://storage.example.test");

        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishAsync(IncompleteResult(),
            new RunPublicationOptions("https://storage.example.test", "ledger", TimeSpan.FromSeconds(5), 4096, 8192),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://user:secret@storage.example.test")]
    [InlineData("https://storage.example.test?sig=secret")]
    [InlineData("https://storage.example.test/path")]
    public async Task GivenUnsafeEndpoint_WhenPublishing_ThenRejectsItBeforeArtifactAccess(string endpoint)
    {
        // The Blob path is a service-root HTTPS URI and never accepts embedded credentials or query data.
        BlobRunArtifactPublisher publisher = CreatePublisher("https://storage.example.test");

        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(IncompleteResult(),
            new RunPublicationOptions(endpoint, "ledger", TimeSpan.FromSeconds(5), 4096, 8192),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenContainerOutsideLedger_WhenPublishing_ThenRejectsItBeforeArtifactAccess()
    {
        // The worker credential is scoped to the one approved ledger container.
        BlobRunArtifactPublisher publisher = CreatePublisher("https://storage.example.test");

        await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(IncompleteResult(),
            new RunPublicationOptions("https://storage.example.test", "other-container", TimeSpan.FromSeconds(5), 4096, 8192),
            TestContext.Current.CancellationToken));
    }

    private static BlobRunArtifactPublisher CreatePublisher(string endpoint)
        => new(new BlobServiceClient(new Uri(endpoint, UriKind.Absolute)), TimeProvider.System);

    private static RunExecutionResult IncompleteResult()
    {
        var identity = new RunIdentity("A01", "C0", "publisher-run", "publisher-instance", "publisher-instance", "publisher-client");
        var manifest = new RunManifest(1, identity, "1.0", 0, 0, ImmutableArray<AttemptEntry>.Empty,
            1, 0, "failed", false, null, ImmutableArray<string>.Empty,
            ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, string>.Empty,
            ImmutableArray<string>.Empty);
        return new RunExecutionResult(1, manifest, ImmutableDictionary<string, string>.Empty);
    }
}
