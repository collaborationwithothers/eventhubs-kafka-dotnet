using Parchwell.Kafka.Statistics;
using Xunit;

namespace Parchwell.Kafka.Tests.Statistics;

#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.

// Protects the native field contract so result consumers do not infer evidence from unselected fields.
[Trait("Category", "Unit")]
public sealed class KafkaStatisticsParserTests
{
    [Fact]
    public void Given_CapturedProducerReport_When_Parsing_Then_CopiesSelectedFieldsAndNativeUnits()
    {
        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(StatisticsFixtures.CapturedProducer, 1);

        Assert.Empty(diagnostics);
        Assert.NotNull(snapshot);
        Assert.Equal("producer", snapshot.Type);
        Assert.Equal(6195073766, snapshot.TimestampMicroseconds);
        Assert.Equal(1791999554, snapshot.TimeSeconds);
        Assert.Equal(1000928, snapshot.AgeMicroseconds);
        var broker = Assert.Single(snapshot.Brokers);
        Assert.Equal("captured-broker/1", broker.Name);
        Assert.Equal(845055, broker.StateAgeMicroseconds);
        Assert.Equal(0, broker.RequestTimeouts);
        Assert.Equal(0, broker.Disconnects);
        Assert.Equal(0, broker.TransmissionRetries);
        Assert.Equal(0, broker.TransmissionErrors);
        Assert.Equal(0, broker.ReceiveErrors);
        Assert.Equal(new KafkaWindowStatistics(0, null), broker.Throttle);
        Assert.Equal(new KafkaWindowStatistics(4, 99327), broker.RoundTripTime);
    }

    [Fact]
    public void Given_SyntheticConsumerReportWithUnknownFields_When_Parsing_Then_KeepsSelectedValuesAndIgnoresExtensions()
    {
        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(StatisticsFixtures.SyntheticConsumer, 1);

        Assert.Empty(diagnostics);
        Assert.NotNull(snapshot);
        Assert.Equal("consumer", snapshot.Type);
        var broker = Assert.Single(snapshot.Brokers);
        Assert.Equal(4, broker.RequestTimeouts);
        Assert.Equal(3, broker.Disconnects);
        Assert.Equal(2, broker.TransmissionRetries);
        Assert.Equal(1, broker.TransmissionErrors);
        Assert.Equal(5, broker.ReceiveErrors);
        Assert.Equal(new KafkaWindowStatistics(2, 17), broker.Throttle);
        Assert.Equal(new KafkaWindowStatistics(0, null), broker.RoundTripTime);
    }

    [Theory]
    [InlineData("{", "invalid_json")]
    [InlineData("[]", "invalid_root")]
    [InlineData("null", "invalid_root")]
    public void Given_InvalidJsonDocument_When_Parsing_Then_ReturnsOneStableDiagnostic(string json, string code)
    {
        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(code, diagnostic.Code);
    }

    [Theory]
    [InlineData("\"ts\": 6195073766", "\"ts\": 6195073766.5", "ts")]
    [InlineData("\"age\": 1000928", "\"age\": -1", "age")]
    [InlineData("\"time\": 1791999554", "\"time\": 9223372036854775808", "time")]
    [InlineData("\"req_timeouts\": 0", "\"req_timeouts\": -1", "req_timeouts")]
    [InlineData("\"disconnects\": 0", "\"disconnects\": 1.25", "disconnects")]
    [InlineData("\"txretries\": 0", "\"txretries\": 9223372036854775808", "txretries")]
    [InlineData("\"txerrs\": 0", "\"txerrs\": -1", "txerrs")]
    [InlineData("\"rxerrs\": 0", "\"rxerrs\": 1.1", "rxerrs")]
    [InlineData("\"stateage\": 845055", "\"stateage\": -1", "stateage")]
    [InlineData("\"nodeid\": 1", "\"nodeid\": -2", "nodeid")]
    public void Given_InvalidIntegralOrRangeValue_When_Parsing_Then_RejectsField(string original, string changed, string field)
    {
        string json = StatisticsFixtures.CapturedProducer.Replace(original, changed, StringComparison.Ordinal);

        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("invalid_or_missing_field", diagnostic.Code);
        Assert.Equal(field, diagnostic.Field);
    }

    [Theory]
    [InlineData("\"max\": 0", "\"max\": -1", "throttle")]
    [InlineData("\"cnt\": 4", "\"cnt\": -1", "rtt")]
    [InlineData("\"p99\": 99327", "\"p99\": 1.5", "rtt")]
    public void Given_InvalidWindowCountOrValue_When_Parsing_Then_RejectsWindow(string original, string changed, string field)
    {
        string json = StatisticsFixtures.CapturedProducer.Replace(original, changed, StringComparison.Ordinal);

        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        Assert.Equal(field, Assert.Single(diagnostics).Field);
    }

    [Fact]
    public void Given_MissingSelectedBrokerField_When_Parsing_Then_DoesNotTreatItAsZero()
    {
        string json = StatisticsFixtures.CapturedProducer.Replace("\"disconnects\": 0,", string.Empty, StringComparison.Ordinal);

        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        Assert.Equal("disconnects", Assert.Single(diagnostics).Field);
    }

    [Theory]
    [InlineData("\"client_id\": \"captured-client\"", "\"client_id\": 17", "client_id", "invalid_or_missing_field")]
    [InlineData("\"name\": \"captured-broker/1\"", "\"name\": \"other-broker/1\"", "name", "broker_name_mismatch")]
    public void Given_MissingOrMismatchedIdentityField_When_Parsing_Then_RejectsAmbiguousReport(
        string original,
        string changed,
        string field,
        string expectedCode)
    {
        string json = StatisticsFixtures.CapturedProducer.Replace(original, changed, StringComparison.Ordinal);

        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(field, diagnostic.Field);
    }

    [Fact]
    public void Given_DuplicateKnownOrUnknownProperties_When_Parsing_Then_RejectsTheAmbiguousPayload()
    {
        string duplicateKnown = StatisticsFixtures.CapturedProducer.Replace(
            "\"ts\": 6195073766,", "\"ts\": 6195073766, \"ts\": 6195073766,", StringComparison.Ordinal);
        string duplicateUnknown = StatisticsFixtures.CapturedProducer.Replace(
            "\"age\": 1000928,", "\"age\": 1000928, \"future\": 1, \"future\": 2,", StringComparison.Ordinal);

        AssertDuplicate(duplicateKnown, "ts");
        AssertDuplicate(duplicateUnknown, "future");
    }

    [Fact]
    public void Given_BrokerNameDisagreesWithDictionaryKey_When_Parsing_Then_RejectsThatBroker()
    {
        string json = StatisticsFixtures.CapturedProducer.Replace(
            "\"name\": \"captured-broker/1\"", "\"name\": \"different-broker/1\"", StringComparison.Ordinal);

        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("broker_name_mismatch", diagnostic.Code);
        Assert.Equal("captured-broker/1", diagnostic.Broker);
    }

    [Theory]
    [InlineData(0, "invalid_broker_series_limit")]
    [InlineData(-1, "invalid_broker_series_limit")]
    [InlineData(1, "broker_series_limit_exceeded")]
    public void Given_BrokerSeriesLimitCannotContainInput_When_Parsing_Then_RejectsReport(int limit, string expectedCode)
    {
        const string twoBrokers = """
            {"name":"x","client_id":"x","type":"producer","ts":1,"time":1,"age":0,
             "brokers":{"b1":{"name":"b1"},"b2":{"name":"b2"}}}
            """;
        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(twoBrokers, limit);

        Assert.Null(snapshot);
        Assert.Equal(expectedCode, Assert.Single(diagnostics).Code);
    }

    private static void AssertDuplicate(string json, string property)
    {
        var (snapshot, diagnostics) = KafkaStatisticsParser.Parse(json, 1);

        Assert.Null(snapshot);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("duplicate_property", diagnostic.Code);
        Assert.Equal(property, diagnostic.Field);
    }
}
