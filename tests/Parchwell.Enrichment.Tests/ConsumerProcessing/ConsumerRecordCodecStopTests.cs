#pragma warning disable CA1707
using System.Globalization;
using System.Text.Json;
using Confluent.Kafka;
using Parchwell.Enrichment.ConsumerProcessing;
using Xunit;

namespace Parchwell.Enrichment.Tests.ConsumerProcessing;

public sealed class ConsumerRecordCodecStopTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-11-28T10:00:05.1234567Z", CultureInfo.InvariantCulture);
    private static readonly string[] StopObservedKeys = ["schema_version", "kind", "instance_id", "row_sequence", "event_utc"];
    private static readonly string[] ConsumerErrorKeys = ["schema_version", "kind", "instance_id", "row_sequence", "source", "error_code", "is_fatal", "reason", "event_utc"];
    private static readonly string[] ReceiptKeys =
    [
        "schema_version", "kind", "run_id", "scenario_id", "configuration_id", "phase", "repeat_number", "role", "instance_id",
        "job_name", "pod_uid", "source_commit", "rows", "record_blobs", "messages_processed", "stop_observed_utc", "shutdown_mode",
        "shutdown_started_utc", "shutdown_ended_utc", "shutdown_outcome", "receipt_written_utc"
    ];

    [Fact]
    public void Registration_carries_shutdown_mode()
    {
        ConsumerProcessingOptions options = ConsumerTestHarness.ValidOptions();
        var plan = new ConsumerProcessingPlan { ConfigurationId = "C1", WindowMs = 1000, StopConfigured = true, ShutdownMode = "dispose" };
        using (JsonDocument document = JsonDocument.Parse(ConsumerRecordCodec.Registration(options, plan, new ConsumerConfig(), Time)))
        {
            List<string> expected = [.. ConsumerRecordCodecTests.RegistrationFields];
            expected.Insert(expected.IndexOf("effective_settings"), "shutdown_mode");
            Assert.Equal(expected, Keys(document.RootElement));
            Assert.Equal("dispose", document.RootElement.GetProperty("shutdown_mode").GetString());
        }
        using JsonDocument without = JsonDocument.Parse(ConsumerRecordCodec.Registration(options, plan with { StopConfigured = false, ShutdownMode = null }, new ConsumerConfig(), Time));
        Assert.Equal(ConsumerRecordCodecTests.RegistrationFields, Keys(without.RootElement));
    }

    [Fact]
    public void Reason_is_cut()
    {
        Assert.Equal(1024, ReasonLength(new string('a', 1025)));
        Assert.Equal(1024, ReasonLength(new string('a', 1024)));
    }

    [Fact]
    public void Given_cut_inside_surrogate_pair_When_serialized_Then_reason_keeps_1024_units_and_row_parses()
    {
        // The pair occupies code units 1023 and 1024, so the cut leaves a lone high surrogate. The serializer writes it as U+FFFD.
        string reason = new string('a', 1023) + "\U0001F600" + "tail";
        using JsonDocument document = JsonDocument.Parse(ErrorRow(reason));
        string written = document.RootElement.GetProperty("reason").GetString()!;
        Assert.Equal(1024, written.Length);
        Assert.Equal(new string('a', 1023), written[..1023]);
    }

    [Fact]
    public void Shapes()
    {
        ConsumerProcessingOptions options = ConsumerTestHarness.ValidOptions();
        var plan = new ConsumerProcessingPlan { ConfigurationId = "C1", WindowMs = 1000, StopConfigured = true, ShutdownMode = "close" };
        byte[] stopRow = ConsumerRecordCodec.StopObserved("pod-1", 4, Time);
        byte[] errorRow = ConsumerRecordCodec.ConsumerError("pod-1", 5, "consume", new Error(ErrorCode.Local_Transport, "synthetic", true), Time);
        foreach (byte[] row in new[] { stopRow, errorRow })
        {
            Assert.Equal((byte)'\n', row[^1]);
            Assert.Equal(1, row.Count(value => value == (byte)'\n'));
        }
        using JsonDocument stop = JsonDocument.Parse(stopRow);
        Assert.Equal(StopObservedKeys, Keys(stop.RootElement));
        Assert.Equal("stop_observed", stop.RootElement.GetProperty("kind").GetString());
        Assert.Equal("2026-11-28T10:00:05.1234567Z", stop.RootElement.GetProperty("event_utc").GetString());
        using JsonDocument error = JsonDocument.Parse(errorRow);
        Assert.Equal(ConsumerErrorKeys, Keys(error.RootElement));
        Assert.Equal("consume", error.RootElement.GetProperty("source").GetString());
        Assert.Equal(-195, error.RootElement.GetProperty("error_code").GetInt32());
        Assert.True(error.RootElement.GetProperty("is_fatal").GetBoolean());
        using JsonDocument receipt = JsonDocument.Parse(ConsumerRecordCodec.StopReceipt(options, plan, 40001, 7,
            Time, Time.AddSeconds(1), Time.AddSeconds(2), "timed_out", Time.AddSeconds(3)));
        Assert.Equal(ReceiptKeys, Keys(receipt.RootElement));
        Assert.Equal("consumer_stop_receipt", receipt.RootElement.GetProperty("kind").GetString());
        Assert.Equal(40001, receipt.RootElement.GetProperty("rows").GetInt64());
        Assert.Equal(2, receipt.RootElement.GetProperty("record_blobs").GetInt64());
        Assert.Equal(7, receipt.RootElement.GetProperty("messages_processed").GetInt64());
        Assert.Equal("close", receipt.RootElement.GetProperty("shutdown_mode").GetString());
        Assert.Equal("timed_out", receipt.RootElement.GetProperty("shutdown_outcome").GetString());
        Assert.Equal("2026-11-28T10:00:08.1234567Z", receipt.RootElement.GetProperty("receipt_written_utc").GetString());
    }

    private static byte[] ErrorRow(string reason) =>
        ConsumerRecordCodec.ConsumerError("pod-1", 0, "error_handler", new Error(ErrorCode.Local_Transport, reason, false), Time);

    private static int ReasonLength(string reason)
    {
        using JsonDocument document = JsonDocument.Parse(ErrorRow(reason));
        return document.RootElement.GetProperty("reason").GetString()!.Length;
    }

    private static string[] Keys(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];
}
#pragma warning restore CA1707
