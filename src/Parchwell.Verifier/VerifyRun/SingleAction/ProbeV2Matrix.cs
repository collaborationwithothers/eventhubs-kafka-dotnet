using System.Globalization;
using System.Text;
using Parchwell.Kafka.Configuration;

namespace Parchwell.Verifier.VerifyRun.SingleAction;

internal sealed record ProbeV2Matrix(string Action, string Account, string Topic, string PartitionMode, int? Partition,
    string ValueKind, string KeyRule, int WindowMs, string? SourceConfiguration, string? SourceStage)
{
    internal static string Job(string run, string item, string configuration, string stage) => $"{run}-{item.ToLowerInvariant()}-{configuration.ToLowerInvariant()}-{stage}";
    internal static string Prefix(string run, string item, string configuration) => $"{run}/single-action-v2/{item}/{configuration}/";
    internal static string[] Configurations(string item) => item switch { "B08" or "E01" => ["C0", "C1"], "F06" => Enumerable.Range(0,12).Select(x => "C" + x.ToString(CultureInfo.InvariantCulture)).ToArray(), _ => [] };
    internal static (string Configuration, string Stage)[] Dependencies(string item, string configuration)
    {
        var rows = new List<(string Configuration, string Stage)>();
        if (item == "E01") { rows.Add((configuration,"seed")); rows.Add((configuration,"seedread")); }
        var action = For(item,configuration,"action");
        if (item == "F06" && action.Action == "assign_consume")
        {
            rows.Add((action.SourceConfiguration!,"seed"));
            if (action.SourceConfiguration != configuration) rows.Add((action.SourceConfiguration!,"action"));
        }
        rows.Add((configuration,"action"));
        if (action.Action == "produce_one") rows.Add((configuration,"read"));
        return rows.Distinct().ToArray();
    }
    internal KafkaClientRole Role => Action == "produce_one" ? KafkaClientRole.LoadGenProducer : KafkaClientRole.VerifierConsumer;
    internal static ProbeV2Matrix For(string item, string configuration, string stage)
    {
        if (configuration.Length < 2 || configuration[0] != 'C' || !int.TryParse(configuration.AsSpan(1),
                NumberStyles.None, CultureInfo.InvariantCulture, out int number) || configuration != "C" + number.ToString(CultureInfo.InvariantCulture))
            throw new InvalidOperationException("v2_configuration");
        ProbeV2Matrix action = item switch
        {
            "B08" when number is 0 or 1 => new("produce_one", "loadgen", "document-events", "automatic", null, "marker", "b08_fixed", 330000, null, null),
            "E01" when number is 0 or 1 => new("produce_one", "probe", "document-state", "explicit", 0, number == 0 ? "empty" : "null", "e01_unique", 330000, configuration, "seed"),
            "F06" when number is >= 0 and <= 5 => new("produce_one", number % 2 == 0 ? "loadgen" : "probe", TopicFor(number / 2), "explicit", 0, "marker", "f06_unique", 330000, null, null),
            "F06" when number is >= 6 and <= 11 => new("assign_consume", number <= 8 ? "probe" : "enrichment-consumer", TopicFor((number - 6) % 3), "explicit", 0, "marker", "f06_seed", 120000, "C" + (6 + (number - 6) % 3).ToString(CultureInfo.InvariantCulture), "seed"),
            _ => throw new InvalidOperationException("v2_configuration")
        };
        return stage switch
        {
            "action" => action,
            "read" when action.Action == "produce_one" => action with { Action = "assign_consume", Account = "probe", PartitionMode = "explicit", WindowMs = 120000, SourceConfiguration = configuration, SourceStage = "action" },
            "seed" when item == "E01" || item == "F06" && number is >= 6 and <= 8 => action with
            { Action = "produce_one", Account = action.Topic == "document-events" ? "loadgen" : "enrichment-consumer", ValueKind = "marker", WindowMs = 330000, SourceConfiguration = null, SourceStage = null },
            "seedread" when item == "E01" => action with { Action = "assign_consume", Account = "probe", ValueKind = "marker", WindowMs = 120000, SourceConfiguration = configuration, SourceStage = "seed" },
            _ => throw new InvalidOperationException("v2_stage")
        };
    }
    internal static string TopicFor(int index) => index switch { 0 => "document-events", 1 => "document-state", 2 => "document-events-dlq", _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    internal byte[] Key(ProbeV2Options options, int repeat, string? configuration = null) => Encoding.UTF8.GetBytes(KeyRule switch
    {
        "b08_fixed" => "b08-synthetic-0",
        "f06_seed" => $"f06-seed:{options.RunId}:{Topic}:r{repeat}",
        _ => $"{options.ScenarioId}:{configuration ?? options.ConfigurationId}:{options.RunId}:r{repeat}"
    });
    internal byte[]? Value(ProbeV2Options options, int repeat, string stage, string? configuration = null) => ValueKind switch
    {
        "null" => null,
        "empty" => [],
        _ => Encoding.UTF8.GetBytes(KeyRule == "f06_seed" ? $"f06-seed:{options.RunId}:{Topic}:r{repeat}" :
            stage is "seed" or "seedread" ? $"seed:E01:{configuration ?? options.ConfigurationId}:{options.RunId}:r{repeat}" :
            $"{options.ScenarioId}:{configuration ?? options.ConfigurationId}:{options.RunId}:r{repeat}")
    };
}
