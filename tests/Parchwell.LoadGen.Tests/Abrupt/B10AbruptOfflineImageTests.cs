using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Docker.DotNet;
using DockerModels = Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Parchwell.LoadGen.Run.Abrupt;
using Parchwell.LoadGen.Tests.Component;
using Xunit;

namespace Parchwell.LoadGen.Tests.Abrupt;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// The image gate observes the real self-kill and production cgroup capture with both real release delays.
public sealed class B10AbruptOfflineImageTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved()
    {
        string? image = Environment.GetEnvironmentVariable("PARCHWELL_B10_OFFLINE_IMAGE");
        if (string.IsNullOrEmpty(image)) return;
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(900));
        CancellationToken cancellation = deadline.Token;
        string? commit = Environment.GetEnvironmentVariable("PARCHWELL_B10_OFFLINE_SOURCE_COMMIT");
        Assert.True(commit is { Length: 40 } && commit.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "b10_offline_image_source_commit_invalid");
        string? evidence = Environment.GetEnvironmentVariable("PARCHWELL_B10_OFFLINE_EVIDENCE");
        Assert.False(string.IsNullOrEmpty(evidence), "b10_offline_image_evidence_missing");
        Assert.False(File.Exists(evidence), "b10_offline_image_evidence_exists");

        await using var run = new B10OfflineImageRun(image, commit!, output);
        string? observation = null;
        bool passed = false;
        try
        {
            await run.InitializeAsync(cancellation);
            observation = await run.ObserveAsync(cancellation);
            passed = true;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            Assert.Fail("b10_offline_image_bound_exceeded\n" + await run.OutputTailsAsync());
        }
        finally
        {
            try { await run.CleanupAsync(passed); }
            finally
            {
                Assert.True(elapsed.Elapsed <= TimeSpan.FromSeconds(900),
                    "b10_offline_image_bound_exceeded\n" + run.LastOutput);
            }
        }
        await using var file = new FileStream(evidence!, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await file.WriteAsync(Encoding.UTF8.GetBytes(observation! + "\n"), cancellation);
        output.WriteLine(observation!);
    }
}

/// <summary>Runs the image gate's parent and fresh successor against one local broker and shared file storage.</summary>
internal sealed class B10OfflineImageRun(string image, string commit, ITestOutputHelper output) : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "b10-offline-image-" + Guid.NewGuid().ToString("N"));
    private readonly string containerPrefix = "b10-offline-image-" + Guid.NewGuid().ToString("N");
    private readonly List<IContainer> containers = [];
    private readonly List<(string Name, string Id)> helpers = [];
    private readonly DockerClient docker = TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder()
        .WithTimeout(TimeSpan.FromMinutes(2)).Build();
    private LocalKafkaFixture? kafka;
    private string imageId = "";
    private string originalHookOwner = "";
    private bool restoreHookOwner;
    private bool cleaned;
    internal string LastOutput { get; private set; } = "";
    private string Storage => Path.Combine(directory, "storage");
    private string FirstPublication => Path.Combine(Storage, "offline-image", "C1", "r1", "b10-abrupt-diagnostic", "first");

    internal async Task InitializeAsync(CancellationToken cancellation)
    {
        try { await docker.System.PingAsync(cancellation); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new InvalidOperationException("b10_offline_image_docker_unreachable", exception); }
        try { imageId = (await docker.Images.InspectImageAsync(image, cancellation)).ID; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { throw new InvalidOperationException("b10_offline_image_image_absent", exception); }
        Assert.True(imageId.StartsWith("sha256:", StringComparison.Ordinal) && imageId.Length == 71 &&
            imageId[7..].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'), "b10_offline_image_image_id_invalid");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("b10_offline_image_linux_required");
        File.SetUnixFileMode(AppContext.BaseDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        File.SetUnixFileMode(typeof(B10AbruptProcessHarness).Assembly.Location,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        // Keep the runner as owner so it can remove the empty directory after uid 1654 cleans its contents.
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        Directory.CreateDirectory(Storage);
        foreach (string role in new[] { "parent", "successor" })
            foreach (string mount in new[] { "work/loadgen", "tmp", "bindings" })
                Directory.CreateDirectory(Path.Combine(directory, role, mount));
        kafka = await LocalKafkaFixture.StartAsync(cancellation);
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafka.BootstrapServers }).Build();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = "document-events", NumPartitions = 1, ReplicationFactor = 1 }]);
        Metadata metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));
        string[] brokerNames = metadata.Brokers.Select(broker => $"{broker.Host}:{broker.Port}/{broker.BrokerId}").ToArray();
        Assert.NotEmpty(brokerNames);
        File.WriteAllBytes(Path.Combine(directory, "parent", "bindings", "start-line.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            start_line = "synthetic-start-line", completion = "synthetic-completion",
            broker_inventory = JsonSerializer.Serialize(new { schema_version = 1,
                identity = new { scenario_id = "B10", configuration_id = "C1", run_id = "offline-image" },
                brokers = brokerNames.Select(name => new { name }), source = "kafka_metadata", source_commit = commit })
        }));
        foreach (string role in new[] { "parent", "successor" })
            File.WriteAllBytes(Path.Combine(directory, role, "work", "loadgen", "test-control.json"),
                JsonSerializer.SerializeToUtf8Bytes(new B10AbruptProcessHarness.Control("image-kafka", "/storage", kafka.BootstrapServers, RealTime: true)));
        // Normalize the host's umask before ownership changes so both the runner and uid 1654 can read evidence.
        foreach (string path in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        File.SetUnixFileMode(Path.Combine(directory, "successor", "bindings"), UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        originalHookOwner = (await HelperAsync("hook-owner", "0:0", false, "stat -c '%u:%g' /hook", cancellation)).Trim();
        string[] owner = originalHookOwner.Split(':');
        Assert.True(owner.Length == 2 && owner.All(value => uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _)),
            "b10_offline_image_hook_owner_invalid");
        restoreHookOwner = true;
        await HelperAsync("ownership", "0:0", true,
            "chown -R 1654:1654 /owned/parent /owned/successor /owned/storage; chown 1654:1654 /hook", cancellation);
    }

    internal async Task<string> ObserveAsync(CancellationToken cancellation)
    {
        (string Stdout, string Stderr) parentLogs = await RunRoleAsync("parent", cancellation);
        byte[] terminationBytes = await File.ReadAllBytesAsync(Path.Combine(FirstPublication, "termination.json"), cancellation);
        JsonElement data = B10AbruptWire.ReadEnvelope(terminationBytes, "termination").GetProperty("data");
        output.WriteLine("termination=" + data.GetRawText());
        Assert.True(data.GetProperty("exit_observed").GetBoolean(), "b10_offline_image_exit_unobserved");
        AssertHooks();
        JsonElement exit = data.GetProperty("exit_code_local_observation");
        Assert.True(exit.ValueKind == JsonValueKind.Number && exit.GetInt32() == 137,
            "b10_offline_image_exit_code:" + exit.GetRawText());
        Assert.Equal("intended_self_kill", data.GetProperty("termination").GetString());
        Assert.True(data.GetProperty("successor_permitted").GetBoolean());
        Assert.Empty(data.GetProperty("capture_gaps").EnumerateArray());
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("cgroup_before").ValueKind);
        Assert.True(JsonElement.DeepEquals(data.GetProperty("cgroup_before"), data.GetProperty("cgroup_after")));
        string[] killSet = data.GetProperty("kill_unconfirmed_attempt_ids").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.NotEmpty(killSet);

        byte[] journal = await File.ReadAllBytesAsync(Path.Combine(FirstPublication, "journal.jsonl"), cancellation);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(directory, "parent", "work", "loadgen", "abrupt", "journal.jsonl"), cancellation), journal);
        JsonElement descriptor = B10AbruptWire.ReadEnvelope(
            await File.ReadAllBytesAsync(Path.Combine(FirstPublication, "abrupt-manifest.json"), cancellation), "abrupt_manifest")
            .GetProperty("data").GetProperty("journal");
        Assert.Equal(journal.LongLength, descriptor.GetProperty("length").GetInt64());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(journal)), descriptor.GetProperty("sha256").GetString());
        Assert.NotEmpty(journal);
        Assert.Equal((byte)'\n', journal[^1]);
        foreach (string line in Encoding.UTF8.GetString(journal).Split('\n')[..^1])
            _ = B10AbruptWire.ReadFrame(Encoding.UTF8.GetBytes(line));
        var identity = new B10AbruptWire.Identity("offline-image", "B10", "C1", "C1", 1, commit,
            "parchwell/loadgen@" + imageId, "offline-image-c1-r1-loadgen-restart", "first", "first-uid", "first", null);
        B10AbruptJournal.Snapshot snapshot = B10AbruptJournal.Read(journal, identity);
        Assert.Empty(snapshot.Gaps);
        Assert.Empty(snapshot.Failures);
        Assert.Equal(killSet, snapshot.KillUnconfirmedAttemptIds);
        B10AbruptWire.Frame intent = Assert.Single(snapshot.Frames, frame => frame.Kind == "kill_intent");
        Assert.Equal(intent, snapshot.Frames.Last(frame => B10AbruptWire.IsProducingKind(frame.Kind)));
        JsonElement[] stages = parentLogs.Stderr.Split('\n')
            .Where(line => line.StartsWith("b10_abrupt_stage ", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[17..]).RootElement.Clone())
            .Where(stage => stage.GetProperty("role").GetString() == "parent").ToArray();
        JsonElement observedExit = Assert.Single(stages, stage => stage.GetProperty("stage").GetString() == "exit_observed");
        int previous = -1;
        foreach (string stage in new[] { "parent_registered", "child_started", "exit_observed", "journal_read", "publication_complete" })
        {
            int index = Array.FindIndex(stages, value => value.GetProperty("stage").GetString() == stage);
            Assert.True(index > previous, "b10_offline_image_stage_order:" + stage);
            previous = index;
        }
        string cutAttempt = intent.Data.GetProperty("cut_attempt_id").GetString()!;
        B10AbruptWire.Frame accepted = Assert.Single(snapshot.Frames, frame => frame.Kind == "accepted" && frame.Data.GetProperty("attempt_id").GetString() == cutAttempt);
        decimal acceptedToKill = ((decimal)intent.MonotonicTicks - accepted.MonotonicTicks) * 1000000 / intent.ClockFrequency;
        decimal killToExit = ((decimal)observedExit.GetProperty("ticks").GetInt64() - intent.MonotonicTicks) * 1000000 / intent.ClockFrequency;
        Assert.True(acceptedToKill >= 0, "b10_offline_image_negative_accepted_to_kill");
        Assert.True(killToExit >= 0, "b10_offline_image_negative_kill_to_exit");
        await WritePredecessorAsync(terminationBytes, intent, cancellation);
        // The successor binding is created after setup, so give this new file the same mount ownership.
        await HelperAsync("successor-ownership", "0:0", true,
            "chown -R 1654:1654 /owned/successor", cancellation);
        await RunRoleAsync("successor", cancellation);
        int consumed = await AssertResendsAsync(snapshot, killSet, cancellation);
        return $"b10_offline_image source_commit={commit} image_id={imageId} runtime={data.GetProperty("runtime_version").GetString()} kernel={File.ReadAllText("/proc/sys/kernel/osrelease").Trim()} parent_exit=0 exit_code_local_observation=137 termination=intended_self_kill journal_records={snapshot.Frames.Count} kill_unconfirmed={killSet.Length} cut_accepted_to_kill_intent_microseconds={acceptedToKill.ToString(CultureInfo.InvariantCulture)} kill_intent_to_exit_observed_microseconds={killToExit.ToString(CultureInfo.InvariantCulture)} successor_exit=0 consumed_resends={consumed}";
    }

    private void AssertHooks()
    {
        JsonElement[] hooks = File.ReadLines(Path.Combine(directory, "parent", "work", "loadgen", "hook-loaded.jsonl"))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone()).ToArray();
        JsonElement parent = Assert.Single(hooks, hook => hook.GetProperty("role").GetString() == "parent");
        Assert.Equal(1, parent.GetProperty("pid").GetInt32());
        AssertHook(parent);
        JsonElement[] children = hooks.Where(hook => hook.GetProperty("role").GetString() == "child").ToArray();
        Assert.NotEmpty(children);
        foreach (JsonElement child in children)
        {
            Assert.NotEqual(1, child.GetProperty("pid").GetInt32());
            AssertHook(child);
        }
    }

    private static void AssertHook(JsonElement hook)
    {
        Assert.Equal(1654, hook.GetProperty("uid").GetInt32());
        Assert.Equal("/app/Parchwell.LoadGen", hook.GetProperty("process_path").GetString());
        Assert.Equal("/app/Parchwell.LoadGen.dll", hook.GetProperty("loadgen_assembly").GetString());
    }

    private async Task WritePredecessorAsync(byte[] termination, B10AbruptWire.Frame intent, CancellationToken cancellation)
    {
        using JsonDocument start = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(directory, "parent", "bindings", "start-line.json"), cancellation));
        string path = Path.Combine(directory, "successor", "bindings", "predecessor.json");
        await File.WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(new
        {
            first_pod = new { job_name = "offline-image-c1-r1-loadgen-restart", pod_name = "first", pod_uid = "first-uid",
                image_reference = "parchwell/loadgen@" + imageId, source_commit = commit, finished_utc = B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()) },
            termination = B10AbruptPublisher.Describe("termination.json", termination), start_line_binding = start.RootElement,
            release_utc = intent.Data.GetProperty("release_utc").GetString(), runner_utc = B10AbruptPublisher.Utc(TimeProvider.System.GetUtcNow()),
            runner_monotonic_ticks = Stopwatch.GetTimestamp(), cross_host_timing = "not_verified"
        }, B10AbruptWire.JsonOptions), cancellation);
        if (OperatingSystem.IsLinux())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
    }

    private async Task<int> AssertResendsAsync(B10AbruptJournal.Snapshot snapshot, string[] killSet, CancellationToken cancellation)
    {
        string completion = Path.Combine(Storage, "offline-image", "C1", "r1", "b10-abrupt-diagnostic", "successor", "completion.json");
        JsonElement receipt = B10AbruptWire.ReadEnvelope(await File.ReadAllBytesAsync(completion, cancellation), "successor_receipt").GetProperty("data");
        JsonElement[] invocations = receipt.GetProperty("invocations").EnumerateArray().ToArray();
        Assert.Equal(killSet, invocations.Select(invocation => invocation.GetProperty("predecessor_attempt_id").GetString()!));
        using var consumer = new ConsumerBuilder<byte[], byte[]>(new ConsumerConfig
        {
            BootstrapServers = kafka!.BootstrapServers, GroupId = "synthetic-b10-image-" + Guid.NewGuid().ToString("N"),
            EnableAutoCommit = false, AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        consumer.Assign(new TopicPartitionOffset("document-events", new Partition(0), Offset.Beginning));
        var observed = new Dictionary<string, Message<byte[], byte[]>>(StringComparer.Ordinal);
        var readTime = Stopwatch.StartNew();
        while (readTime.Elapsed < TimeSpan.FromSeconds(20) && invocations.Any(invocation => !observed.ContainsKey(invocation.GetProperty("attempt_id").GetString()!)))
        {
            cancellation.ThrowIfCancellationRequested();
            ConsumeResult<byte[], byte[]>? record = consumer.Consume(TimeSpan.FromMilliseconds(100));
            if (record?.Message.Headers is null) continue;
            observed.TryAdd(Encoding.UTF8.GetString(record.Message.Headers.GetLastBytes("attempt-id")), record.Message);
        }
        foreach (JsonElement invocation in invocations)
        {
            string predecessor = invocation.GetProperty("predecessor_attempt_id").GetString()!;
            string attempt = invocation.GetProperty("attempt_id").GetString()!;
            Assert.NotEqual(predecessor, attempt);
            JsonElement original = Assert.Single(snapshot.Frames, frame => frame.Kind == "prepared" && frame.Data.GetProperty("attempt_id").GetString() == predecessor).Data;
            Assert.True(observed.TryGetValue(attempt, out Message<byte[], byte[]>? resend), "successor_attempt_not_consumed:" + attempt + "\n" + LastOutput);
            Assert.Equal(Encoding.UTF8.GetBytes(original.GetProperty("key").GetString()!), resend.Key);
            Assert.Equal(original.GetProperty("sequence").GetInt64().ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(resend.Headers.GetLastBytes("sequence")));
            Assert.Equal(original.GetProperty("payload_sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(resend.Value)));
            Assert.Equal("successor", Encoding.UTF8.GetString(resend.Headers.GetLastBytes("instance-id")));
            if (observed.TryGetValue(predecessor, out Message<byte[], byte[]>? originalMessage)) Assert.Equal(originalMessage.Value, resend.Value);
        }
        return invocations.Length;
    }

    private async Task<(string Stdout, string Stderr)> RunRoleAsync(string role, CancellationToken cancellation)
    {
        IContainer container = BaseContainer(role, "1654:1654")
            .WithEntrypoint("/app/Parchwell.LoadGen")
            .WithCommand("--b10-abrupt-" + role)
            .WithEnvironment(EnvironmentValues(role))
            .WithBindMount(Path.Combine(directory, role, "work"), "/work")
            .WithBindMount(Path.Combine(directory, role, "tmp"), "/tmp")
            .WithBindMount(Path.Combine(directory, role, "bindings"), "/bindings/b10-abrupt", AccessMode.ReadOnly)
            .WithBindMount(Storage, "/storage")
            .WithBindMount(AppContext.BaseDirectory, "/hook", AccessMode.ReadOnly)
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig!.NanoCPUs = 3000000000;
                parameters.HostConfig.NetworkMode = "host";
                parameters.HostConfig.Memory = 12L * 1024 * 1024 * 1024;
                parameters.HostConfig.CgroupnsMode = "private";
                parameters.HostConfig.Init = false;
                parameters.HostConfig.PidMode = "";
            }).Build();
        await RunContainerAsync(container, role, cancellation);
        return await container.GetLogsAsync(timestampsEnabled: false, ct: cancellation);
    }

    private ContainerBuilder BaseContainer(string suffix, string user) => new ContainerBuilder(image)
        .WithName(containerPrefix + "-" + suffix)
        .WithImagePullPolicy(PullPolicy.Never)
        .WithCreateParameterModifier(parameters =>
        {
            parameters.User = user;
            parameters.HostConfig ??= new Docker.DotNet.Models.HostConfig();
            parameters.HostConfig.ReadonlyRootfs = true;
            parameters.HostConfig.CapDrop = ["ALL"];
            parameters.HostConfig.SecurityOpt = ["no-new-privileges"];
        });

    private async Task RunContainerAsync(IContainer container, string role, CancellationToken cancellation)
    {
        containers.Add(container);
        try
        {
            await container.StartAsync(cancellation);
            await docker.Containers.WaitContainerAsync(container.Id, cancellation);
        }
        catch (OperationCanceledException exception)
        {
            LastOutput = await ContainerTailAsync(container);
            throw new OperationCanceledException("b10_offline_image_bound_exceeded\n" + LastOutput, exception, cancellation);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            string tail = await ContainerTailAsync(container);
            throw new InvalidOperationException("b10_offline_image_container_exit:" + role + "\n" + tail, exception);
        }
        (string stdout, string stderr) = await container.GetLogsAsync(timestampsEnabled: false, ct: cancellation);
        LastOutput = Tail(stdout + stderr);
        output.WriteLine(role + "_output_tail=" + LastOutput);
        long exitCode = await container.GetExitCodeAsync(cancellation);
        Assert.True(exitCode == 0, "b10_offline_image_container_exit:" + role + ":" + exitCode.ToString(CultureInfo.InvariantCulture) + "\n" + LastOutput);
    }

    private Dictionary<string, string> EnvironmentValues(string role)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enabled"] = "true", ["Purpose"] = "diagnostic_trial", ["Role"] = role, ["RunId"] = "offline-image",
            ["ScenarioId"] = "B10", ["ConfigurationId"] = "C1", ["Phase"] = "C1", ["Repeat"] = "1",
            ["SourceCommit"] = commit, ["ImageReference"] = "parchwell/loadgen@" + imageId,
            ["JobName"] = "offline-image-c1-r1-loadgen-" + (role == "parent" ? "restart" : "successor"),
            ["PodName"] = role == "parent" ? "first" : "successor", ["PodUid"] = role == "parent" ? "first-uid" : "successor-uid",
            ["NamespaceName"] = "parchwell", ["BlobEndpoint"] = "https://example.blob.core.windows.net",
            ["ScenarioSourceRoot"] = "/app", ["OutputRoot"] = "/work/loadgen",
            [role == "parent" ? "StartLineBindingPath" : "PredecessorBindingPath"] = "/bindings/b10-abrupt/" + (role == "parent" ? "start-line.json" : "predecessor.json")
        }.ToDictionary(pair => "B10Abrupt__" + pair.Key, pair => pair.Value, StringComparer.Ordinal);
        values["DOTNET_STARTUP_HOOKS"] = "/hook/Parchwell.LoadGen.Tests.dll";
        values["Telemetry__ConnectionString"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/;LiveEndpoint=https://127.0.0.1:1/";
        values["AZURE_TENANT_ID"] = "synthetic-tenant";
        values["AZURE_CLIENT_ID"] = "synthetic-client";
        values["AZURE_FEDERATED_TOKEN_FILE"] = "/work/unused-token";
        return values;
    }

    private async Task<string> HelperAsync(string suffix, string user, bool chown, string command, CancellationToken cancellation)
    {
        string name = containerPrefix + "-" + suffix;
        // Track the name before create so cancellation of the response cannot hide a created container.
        helpers.Add((name, name));
        try
        {
            DockerModels.CreateContainerResponse created = await docker.Containers.CreateContainerAsync(new DockerModels.CreateContainerParameters
            {
                Name = name, Image = image, User = user, Entrypoint = ["/bin/sh"], Cmd = ["-c", "set -e; " + command],
                HostConfig = new DockerModels.HostConfig
                {
                    ReadonlyRootfs = true, CapDrop = ["ALL"], CapAdd = chown ? ["CHOWN"] : [],
                    SecurityOpt = ["no-new-privileges"], NetworkMode = "none",
                    Mounts = [new DockerModels.Mount { Type = "bind", Source = directory, Target = "/owned" },
                        new DockerModels.Mount { Type = "bind", Source = AppContext.BaseDirectory, Target = "/hook" }]
                }
            }, cancellation);
            if (!await docker.Containers.StartContainerAsync(created.ID, new DockerModels.ContainerStartParameters(), cancellation))
                throw new InvalidOperationException("b10_offline_image_helper_start_failed");
            DockerModels.ContainerWaitResponse result = await docker.Containers.WaitContainerAsync(created.ID, cancellation);
            if (result.StatusCode != 0)
                throw new InvalidOperationException("b10_offline_image_helper_exit_code:" + result.StatusCode.ToString(CultureInfo.InvariantCulture));
            return await HelperTailAsync(created.ID);
        }
        catch (OperationCanceledException exception)
        {
            LastOutput = await HelperTailAsync(name);
            throw new OperationCanceledException("b10_offline_image_bound_exceeded:" + suffix + "\n" + LastOutput, exception, cancellation);
        }
        catch (Exception exception)
        {
            LastOutput = await HelperTailAsync(name);
            throw new InvalidOperationException("b10_offline_image_helper_exit:" + suffix + "\n" + LastOutput, exception);
        }
    }

    private async Task<string> HelperTailAsync(string id)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            using MultiplexedStream logs = await docker.Containers.GetContainerLogsAsync(id,
                new DockerModels.ContainerLogsParameters { ShowStdout = true, ShowStderr = true }, timeout.Token);
            (string stdout, string stderr) = await logs.ReadOutputToEndAsync(timeout.Token);
            return Tail(stdout + stderr);
        }
        catch (Exception exception) { return "container_output_unavailable:" + exception.Message; }
    }

    private static async Task<string> ContainerTailAsync(IContainer container)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            (string stdout, string stderr) = await container.GetLogsAsync(timestampsEnabled: false, ct: timeout.Token);
            return Tail(stdout + stderr);
        }
        catch (Exception exception) { return "container_output_unavailable:" + exception.Message; }
    }

    internal async Task<string> OutputTailsAsync()
    {
        var tails = new StringBuilder();
        foreach (IContainer container in containers)
            tails.AppendLine(container.Name + "\n" + await ContainerTailAsync(container));
        foreach ((string name, string id) in helpers)
            tails.AppendLine(name + "\n" + await HelperTailAsync(id));
        LastOutput = tails.ToString();
        return LastOutput;
    }

    internal async Task CleanupAsync(bool passed)
    {
        if (cleaned) return;
        List<Exception> failures = [];
        // Collect every removal failure so one unavailable container never prevents the others' cleanup.
        foreach (IContainer container in containers.ToArray())
        {
            try { await DisposeContainerAsync(container); }
            catch (Exception exception) { failures.Add(exception); }
        }
        await RemoveHelpersAsync(failures);
        if (kafka is not null)
        {
            try { await kafka.DisposeAsync(); }
            catch (Exception exception) { failures.Add(exception); }
        }
        if (restoreHookOwner)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await HelperAsync("restore-hook-owner", "0:0", true, "chown " + originalHookOwner + " /hook", timeout.Token);
                restoreHookOwner = false;
            }
            catch (Exception exception) { failures.Add(exception); }
            finally { await RemoveHelpersAsync(failures); }
        }
        if (passed && failures.Count == 0)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await HelperAsync("cleanup", "1654:1654", false, "rm -rf /owned/parent /owned/successor /owned/storage", timeout.Token);
                Directory.Delete(directory);
            }
            catch (Exception exception) { failures.Add(exception); }
            finally
            {
                await RemoveHelpersAsync(failures);
            }
        }
        else output.WriteLine("preserved_directory=" + directory);
        cleaned = true;
        docker.Dispose();
        if (failures.Count != 0) throw new AggregateException("b10_offline_image_cleanup", failures);
    }

    private async Task RemoveHelpersAsync(List<Exception> failures)
    {
        foreach ((string name, string id) in helpers.ToArray())
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await docker.Containers.RemoveContainerAsync(id, new DockerModels.ContainerRemoveParameters { Force = true, RemoveVolumes = true }, timeout.Token);
                helpers.Remove((name, id));
            }
            catch (DockerContainerNotFoundException) { helpers.Remove((name, id)); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }

    private static async Task DisposeContainerAsync(IContainer container)
    {
        try
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await container.StopAsync(stop.Token).WaitAsync(stop.Token);
        }
        finally
        {
            using var dispose = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await container.DisposeAsync().AsTask().WaitAsync(dispose.Token);
        }
    }

    public async ValueTask DisposeAsync() => await CleanupAsync(passed: false);
    private static string Tail(string value) => value.Length <= 16384 ? value : value[^16384..];
}
