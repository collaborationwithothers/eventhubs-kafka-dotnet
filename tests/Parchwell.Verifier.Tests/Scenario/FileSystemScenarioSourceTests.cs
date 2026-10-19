using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parchwell.Kafka.Configuration;
using Parchwell.Scenarios;
using Parchwell.Verifier.Common.Scenario;
using Xunit;

namespace Parchwell.Verifier.Tests.Scenario;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
[Trait("Category", "Unit")]
public sealed class FileSystemScenarioSourceTests
{
    private static readonly string[] ApprovedConfigurations = ["C0", "C2", "C3"];
    [Theory]
    [InlineData("C0", "60000", "60000")]
    [InlineData("C2", "5000", "60000")]
    [InlineData("C3", "60000", "20000")]
    public async Task Given_ActualA01Files_When_LoadingAndResolving_Then_UsesApprovedProducerTimeoutsWithoutExpectedFile(
        string configuration, string requestTimeout, string socketTimeout)
    {
        string repository = Environment.GetEnvironmentVariable("PARCHWELL_REPOSITORY_ROOT") ?? FindRepositoryRoot();
        string root = Path.Combine(Path.GetTempPath(), "verifier-a01-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scenarios", "A01"));
        Directory.CreateDirectory(Path.Combine(root, "scenarios", "profiles"));
        File.Copy(Path.Combine(repository, "scenarios", "A01", "scenario.yaml"), Path.Combine(root, "scenarios", "A01", "scenario.yaml"));
        foreach (string profile in Directory.EnumerateFiles(Path.Combine(repository, "scenarios", "profiles"), "*.yaml"))
            File.Copy(profile, Path.Combine(root, "scenarios", "profiles", Path.GetFileName(profile)));
        // An expected.yaml directory is unreadable as a text file. Loading succeeds without touching it.
        Directory.CreateDirectory(Path.Combine(root, "scenarios", "A01", "expected.yaml"));
        try
        {
            var source = new FileSystemScenarioSource(root);
            var loader = new ScenarioLoader(source, Options.Create(new ScenarioLoadOptions
            {
                MaxDocumentCharacters = 65536, MaxNestingDepth = 32, MaxScalarCharacters = 4096, MaxParserEvents = 10000
            }));
            LoadedScenario loaded = await loader.LoadAsync("A01", TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            foreach (KafkaClientRole role in loaded.Definition.Roles)
                services.AddParchwellKafka(role, options =>
                {
                    options.NamespaceName = "synthetic-namespace";
                    options.ClientId = "synthetic-client";
                    options.EnrichmentGroupId = "synthetic-group";
                });
            using ServiceProvider provider = services.BuildServiceProvider();
            ResolvedScenario resolved = new ScenarioResolver(provider.GetRequiredService<KafkaConfigurationFactory>()).Resolve(loaded);
            Assert.Equal(ApprovedConfigurations, resolved.Configurations.Keys.Order(StringComparer.Ordinal));
            ResolvedRoleConfiguration producer = resolved.Configurations[configuration].Roles[KafkaClientRole.LoadGenProducer];
            Assert.Equal(requestTimeout, producer.Settings["request.timeout.ms"].Value);
            Assert.Equal(socketTimeout, producer.Settings["socket.timeout.ms"].Value);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("scenarios/A01/scenario.yaml")]
    [InlineData("scenarios\\A01\\scenario.yaml")]
    [InlineData("scenarios/profiles/a-1.yaml")]
    public async Task Given_ValidLoaderPath_When_Opening_Then_ReadsOnlyRequestedScenarioFile(string path)
    {
        string root = CreateScenarioRoot();
        try
        {
            var source = new FileSystemScenarioSource(root);
            Directory.CreateDirectory(Path.Combine(root, "scenarios", "profiles"));
            await File.WriteAllTextAsync(Path.Combine(root, "scenarios", "profiles", "a-1.yaml"), "id: A01", TestContext.Current.CancellationToken);
            using TextReader reader = await source.OpenReadAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal("id: A01", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("../outside.yaml")]
    [InlineData("scenarios/../outside.yaml")]
    [InlineData("/etc/passwd")]
    [InlineData("scenarios/A01/expected.yaml")]
    [InlineData("")]
    [InlineData("scenarios//scenario.yaml")]
    [InlineData("scenarios/./scenario.yaml")]
    [InlineData("scenarios/A01/../scenario.yaml")]
    [InlineData("scenarios/1A/scenario.yaml")]
    [InlineData("scenarios/A_01/scenario.yaml")]
    [InlineData("scenarios/A01/foreign.yaml")]
    [InlineData("scenarios/profiles/.yaml")]
    [InlineData("scenarios/profiles/a.json")]
    [InlineData("scenarios/profiles/a:b.yaml")]
    [InlineData("foreign/A01/scenario.yaml")]
    [InlineData("C:\\outside.yaml")]
    [InlineData("scenarios/profiles/a-.yaml/extra")]
    public async Task Given_OutsideOrNonLoaderPath_When_Opening_Then_RejectsPath(string path)
    {
        string root = CreateScenarioRoot();
        try
        {
            var source = new FileSystemScenarioSource(root);
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await source.OpenReadAsync(path, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Given_MissingRoot_When_Constructing_Then_RejectsBeforeAnyRead()
    {
        string missing = Path.Combine(Path.GetTempPath(), "verifier-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(() => new FileSystemScenarioSource(missing));
    }

    [Fact]
    public async Task Given_InvalidUtf8_When_Reading_Then_RejectsMalformedEncoding()
    {
        string root = CreateScenarioRoot();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, "scenarios", "A01", "scenario.yaml"), [0xff], TestContext.Current.CancellationToken);
            var source = new FileSystemScenarioSource(root);
            using TextReader reader = await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<DecoderFallbackException>(() => reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Given_CancelledToken_When_Opening_Then_ThrowsBeforeFileRead()
    {
        string root = CreateScenarioRoot();
        try
        {
            var source = new FileSystemScenarioSource(root);
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await source.OpenReadAsync("scenarios/A01/scenario.yaml", cancelled.Token));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Given_MissingScenarioFile_When_Opening_Then_ThrowsMissingInput()
    {
        string root = CreateScenarioRoot();
        try
        {
            File.Delete(Path.Combine(root, "scenarios", "A01", "scenario.yaml"));
            var source = new FileSystemScenarioSource(root);
            await Assert.ThrowsAsync<FileNotFoundException>(async () => await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Given_SymlinkedRootOrFile_When_Opening_Then_RejectsLink(bool rootLink)
    {
        string root = CreateScenarioRoot();
        string link = root + "-link";
        try
        {
            if (rootLink)
            {
                Directory.CreateSymbolicLink(link, root);
                Assert.Throws<InvalidDataException>(() => new FileSystemScenarioSource(link));
            }
            else
            {
                string file = Path.Combine(root, "scenarios", "A01", "scenario.yaml");
                File.Move(file, link);
                File.CreateSymbolicLink(file, link);
                var source = new FileSystemScenarioSource(root);
                await Assert.ThrowsAsync<InvalidDataException>(async () => await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            if (rootLink) Directory.Delete(link);
            else File.Delete(link);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Parchwell.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("repository_root_not_found");
    }

    [Fact]
    public async Task Given_SymlinkedScenarioDirectory_When_Opening_Then_RejectsAllSymlinks()
    {
        string root = CreateScenarioRoot();
        string outside = Path.Combine(Path.GetTempPath(), "verifier-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "scenario.yaml"), "id: A01",
            TestContext.Current.CancellationToken);
        try
        {
            Directory.Delete(Path.Combine(root, "scenarios", "A01"), recursive: true);
            Directory.CreateSymbolicLink(Path.Combine(root, "scenarios", "A01"), outside);
            var source = new FileSystemScenarioSource(root);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await source.OpenReadAsync("scenarios/A01/scenario.yaml", TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static string CreateScenarioRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "verifier-scenario-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "scenarios", "A01"));
        File.WriteAllText(Path.Combine(root, "scenarios", "A01", "scenario.yaml"), "id: A01");
        File.WriteAllText(Path.Combine(root, "scenarios", "A01", "expected.yaml"), "outcome: ignored");
        return root;
    }
}
#pragma warning restore CA1707
