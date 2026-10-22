#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Text;
using System.Text.Json;
using Parchwell.Runner.Run.Evidence;
using Xunit;

namespace Parchwell.Runner.Tests.Evidence;

// Check 9 permits a public summary with the verifier's six outcome counts and no private evidence.
public sealed class CandidateContractTests
{
    private const string Subscription = "abcdef01-abcd-abcd-abcd-abcdef012345";
    private static readonly int[] ExpectedCounts = [11, 12, 13, 14, 15, 16];
    private static readonly string[] CountKeys = ["ok", "loss", "phantom", "reported_failure", "duplicate", "reorder"];
    private static readonly int[] ExpectedErrorCodes = [-184, 5];
    private static readonly string[] CompletedReasons = ["actual_cost_received", "complete_valid"];
    private static readonly string[] UnavailableReasons = ["actual_cost_unavailable"];

    [Fact]
    public void Given_PublicSummaryAndPrivateExtras_When_Projected_Then_OnlyContractFieldsSurvive()
    {
        var source = new
        {
            synthetic = false,
            scenario_id = "A01",
            configuration_id = "C3",
            run_id = "synthetic-contract-run",
            source_commit = new string('a', 40),
            status = "completed",
            gate = "pass",
            versions = new { runner = "1.0.0", loadgen = "1.1.0", verifier = "1.2.0", workload_generator = "1.3.0", confluent_kafka = "2.16.0", librdkafka = "2.16.0", dotnet = "10.0.0", client_id = "private-version-client" },
            image_digests = new { loadgen = "sha256:" + new string('b', 64), verifier = "sha256:" + new string('c', 64), registry_url = "private-registry.example.invalid" },
            timings = new { started_utc = "2026-10-22T10:00:00Z", ended_utc = "2026-10-22T10:01:30Z", duration_seconds = 90, raw_stopwatch = "private-stopwatch" },
            counts = new { ok = 11, loss = 12, phantom = 13, reported_failure = 14, duplicate = 15, reorder = 16, attempted = 101, acknowledged = 102, consumed = 103, missing = 104, duplicates = 105, out_of_order = 106, raw = "private-counts" },
            costs = new { estimate_gbp = 2.50m, actual_gbp = 2.25m, actual_available = true, resource_id = "private-cost-resource" },
            diagnostics = new { error_codes = ExpectedErrorCodes, max_throttle_ms = 20001, request_timeout_increment = 2, disconnect_increment = 3, rtt_p99_ms = 4.5m, query = "private-query-text", exception = "private-exception-text" },
            reasons = CompletedReasons,
            raw = new { body = "private-raw-body" },
            runtime = new { tenant_id = "private-tenant", client_id = "private-client" },
            resource_id = "/subscriptions/" + Subscription + "/resourceGroups/private-group",
            query = "private-top-level-query"
        };

        byte[] bytes = PublicCandidate.Create(JsonSerializer.SerializeToUtf8Bytes(source), Subscription);
        using var document = JsonDocument.Parse(bytes);
        JsonElement candidate = document.RootElement;

        AssertKeys(candidate, "synthetic", "scenario_id", "configuration_id", "run_id", "source_commit", "status", "gate", "versions", "image_digests", "timings", "counts", "costs", "diagnostics", "reasons");
        Assert.True(candidate.GetProperty("synthetic").GetBoolean());
        Assert.Equal("A01", candidate.GetProperty("scenario_id").GetString());
        Assert.Equal("C3", candidate.GetProperty("configuration_id").GetString());
        Assert.Equal("synthetic-contract-run", candidate.GetProperty("run_id").GetString());
        Assert.Equal(new string('a', 40), candidate.GetProperty("source_commit").GetString());
        Assert.Equal("completed", candidate.GetProperty("status").GetString());
        Assert.Equal("pass", candidate.GetProperty("gate").GetString());
        AssertKeys(candidate.GetProperty("versions"), "runner", "loadgen", "verifier", "workload_generator", "confluent_kafka", "librdkafka", "dotnet");
        Assert.Equal("2.16.0", candidate.GetProperty("versions").GetProperty("confluent_kafka").GetString());
        Assert.Equal("2.16.0", candidate.GetProperty("versions").GetProperty("librdkafka").GetString());
        AssertKeys(candidate.GetProperty("image_digests"), "loadgen", "verifier");
        Assert.Equal("sha256:" + new string('b', 64), candidate.GetProperty("image_digests").GetProperty("loadgen").GetString());
        Assert.Equal("sha256:" + new string('c', 64), candidate.GetProperty("image_digests").GetProperty("verifier").GetString());
        AssertKeys(candidate.GetProperty("timings"), "started_utc", "ended_utc", "duration_seconds");
        Assert.Equal("2026-10-22T10:00:00Z", candidate.GetProperty("timings").GetProperty("started_utc").GetString());
        Assert.Equal("2026-10-22T10:01:30Z", candidate.GetProperty("timings").GetProperty("ended_utc").GetString());
        Assert.Equal(90, candidate.GetProperty("timings").GetProperty("duration_seconds").GetInt32());
        AssertKeys(candidate.GetProperty("counts"), "ok", "loss", "phantom", "reported_failure", "duplicate", "reorder");
        Assert.Equal(ExpectedCounts, CountKeys.Select(key => candidate.GetProperty("counts").GetProperty(key).GetInt32()));
        AssertKeys(candidate.GetProperty("costs"), "estimate_gbp", "actual_gbp", "actual_available");
        Assert.Equal(2.50m, candidate.GetProperty("costs").GetProperty("estimate_gbp").GetDecimal());
        Assert.Equal(2.25m, candidate.GetProperty("costs").GetProperty("actual_gbp").GetDecimal());
        Assert.True(candidate.GetProperty("costs").GetProperty("actual_available").GetBoolean());
        AssertKeys(candidate.GetProperty("diagnostics"), "error_codes", "max_throttle_ms", "request_timeout_increment", "disconnect_increment", "rtt_p99_ms");
        Assert.Equal(ExpectedErrorCodes, candidate.GetProperty("diagnostics").GetProperty("error_codes").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(20001, candidate.GetProperty("diagnostics").GetProperty("max_throttle_ms").GetInt32());
        Assert.Equal(2, candidate.GetProperty("diagnostics").GetProperty("request_timeout_increment").GetInt32());
        Assert.Equal(3, candidate.GetProperty("diagnostics").GetProperty("disconnect_increment").GetInt32());
        Assert.Equal(4.5m, candidate.GetProperty("diagnostics").GetProperty("rtt_p99_ms").GetDecimal());
        Assert.Equal(CompletedReasons, candidate.GetProperty("reasons").EnumerateArray().Select(value => value.GetString()));
        Assert.DoesNotContain("private-", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.DoesNotContain(Subscription, Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_UnavailableMeasurements_When_Projected_Then_NullsAndCostAvailabilityArePreserved()
    {
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            versions = new { runner = (string?)null, loadgen = (string?)null, verifier = (string?)null, workload_generator = (string?)null, confluent_kafka = (string?)null, librdkafka = (string?)null, dotnet = (string?)null },
            image_digests = new { loadgen = (string?)null, verifier = (string?)null },
            timings = new { started_utc = (string?)null, ended_utc = (string?)null, duration_seconds = (decimal?)null },
            gate = (string?)null,
            counts = new { ok = (long?)null, loss = (long?)null, phantom = (long?)null, reported_failure = (long?)null, duplicate = (long?)null, reorder = (long?)null },
            diagnostics = new { error_codes = (int[]?)null, max_throttle_ms = (decimal?)null, request_timeout_increment = (long?)null, disconnect_increment = (long?)null, rtt_p99_ms = (decimal?)null },
            costs = new { estimate_gbp = 2.50m, actual_gbp = (decimal?)null, actual_available = false },
            reasons = UnavailableReasons
        });

        using var document = JsonDocument.Parse(PublicCandidate.Create(input, Subscription));
        JsonElement candidate = document.RootElement;

        foreach (string key in new[] { "versions", "image_digests", "timings", "counts", "diagnostics" })
            Assert.All(candidate.GetProperty(key).EnumerateObject(), property => Assert.Equal(JsonValueKind.Null, property.Value.ValueKind));
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("gate").ValueKind);
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("costs").GetProperty("actual_gbp").ValueKind);
        Assert.False(candidate.GetProperty("costs").GetProperty("actual_available").GetBoolean());
        Assert.Equal("actual_cost_unavailable", Assert.Single(candidate.GetProperty("reasons").EnumerateArray()).GetString());
    }

    [Fact]
    public void Given_EmbeddedCaseVariants_When_Projected_Then_EveryRetainedStringRedactsTheSubscription()
    {
        string embedded = "prefix-" + Subscription.ToUpperInvariant() + "-middle-" + Subscription + "-suffix";
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            scenario_id = embedded, configuration_id = embedded, run_id = embedded, source_commit = embedded, status = embedded, gate = embedded,
            versions = new { runner = embedded, loadgen = embedded, verifier = embedded, workload_generator = embedded, confluent_kafka = embedded, librdkafka = embedded, dotnet = embedded },
            image_digests = new { loadgen = embedded, verifier = embedded },
            timings = new { started_utc = embedded, ended_utc = embedded },
            reasons = new[] { "reason_" + Subscription.ToUpperInvariant(), "reason_" + Subscription }
        });

        byte[] bytes = PublicCandidate.Create(input, Subscription);
        using var document = JsonDocument.Parse(bytes);
        string redacted = "prefix-[REDACTED_SUBSCRIPTION]-middle-[REDACTED_SUBSCRIPTION]-suffix";

        foreach (string key in new[] { "scenario_id", "configuration_id", "run_id", "source_commit", "status", "gate" })
            Assert.Equal(redacted, document.RootElement.GetProperty(key).GetString());
        foreach (string key in new[] { "versions", "image_digests", "timings" })
            Assert.All(document.RootElement.GetProperty(key).EnumerateObject(), property => Assert.Equal(redacted, property.Value.GetString()));
        Assert.All(document.RootElement.GetProperty("reasons").EnumerateArray(), reason => Assert.Equal("reason_[REDACTED_SUBSCRIPTION]", reason.GetString()));
        Assert.DoesNotContain(Subscription, Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Given_JsonEscapedSubscription_When_Projected_Then_DecodedStringIsRedacted()
    {
        const string input = "{\"run_id\":\"prefix-\\u0061bcdef01-abcd-abcd-abcd-abcdef012345-suffix\"}";

        byte[] bytes = PublicCandidate.Create(Encoding.UTF8.GetBytes(input), Subscription);
        using var document = JsonDocument.Parse(bytes);

        Assert.Equal("prefix-[REDACTED_SUBSCRIPTION]-suffix", document.RootElement.GetProperty("run_id").GetString());
        Assert.DoesNotContain(Subscription, Encoding.UTF8.GetString(bytes), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void Given_OutcomeCountBoundary_When_Projected_Then_OnlyNonnegativeCountsAreAccepted(int count, bool accepted)
    {
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { counts = new { ok = count, loss = count, phantom = count, reported_failure = count, duplicate = count, reorder = count } });
        if (!accepted)
        {
            Assert.Equal("candidate_number_invalid", Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, Subscription)).Message);
            return;
        }
        using var document = JsonDocument.Parse(PublicCandidate.Create(input, Subscription));
        Assert.All(document.RootElement.GetProperty("counts").EnumerateObject(), property => Assert.Equal(count, property.Value.GetInt32()));
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Given_DiagnosticCodeCountBoundary_When_Projected_Then_OnlyBoundedArraysAreAccepted(int count, bool accepted)
    {
        int[] codes = Enumerable.Range(0, count).Select(value => -value).ToArray();
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { diagnostics = new { error_codes = codes } });
        if (!accepted)
        {
            Assert.Equal("candidate_codes_invalid", Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, Subscription)).Message);
            return;
        }
        using var document = JsonDocument.Parse(PublicCandidate.Create(input, Subscription));
        Assert.Equal(codes, document.RootElement.GetProperty("diagnostics").GetProperty("error_codes").EnumerateArray().Select(code => code.GetInt32()));
    }

    [Theory]
    [InlineData(99, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Given_ReasonCountBoundary_When_Projected_Then_OnlyBoundedArraysAreAccepted(int count, bool accepted)
    {
        string[] reasons = Enumerable.Repeat("evidence_unavailable", count).ToArray();
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { reasons });
        if (!accepted)
        {
            Assert.Equal("candidate_reasons_invalid", Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, Subscription)).Message);
            return;
        }
        using var document = JsonDocument.Parse(PublicCandidate.Create(input, Subscription));
        Assert.Equal(reasons, document.RootElement.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()));
    }

    [Theory]
    [InlineData("/subscriptions/abcdef01-abcd-abcd-abcd-abcdef012345/resourceGroups/synthetic")]
    [InlineData("/SUBSCRIPTIONS/ABCDEF01-ABCD-ABCD-ABCD-ABCDEF012345/resourceGroups/synthetic")]
    [InlineData("ConnectionString=private")]
    [InlineData("AccountKey=private")]
    public void Given_PrivateRetainedString_When_Projected_Then_RejectsTheCandidate(string text)
    {
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { versions = new { runner = text } });

        Assert.Equal("candidate_private_string", Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, Subscription)).Message);
    }

    [Theory]
    [InlineData("SELECT * FROM private_table")]
    [InlineData("exception: private failure")]
    [InlineData("line1\nline2")]
    public void Given_QueryOrLogTextAsReason_When_Projected_Then_RejectsUnstableDiagnosticText(string reason)
    {
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new { reasons = new[] { reason } });

        Assert.Equal("candidate_reason_invalid", Assert.Throws<InvalidDataException>(() => PublicCandidate.Create(input, Subscription)).Message);
    }

    private static void AssertKeys(JsonElement value, params string[] expected) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal), value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
}
