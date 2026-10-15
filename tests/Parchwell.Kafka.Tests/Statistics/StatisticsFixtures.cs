namespace Parchwell.Kafka.Tests.Statistics;

internal static class StatisticsFixtures
{
    // Sanitized selected fields from Trials[0].Producer.Statistics[0] in
    // docs/operations/phantom-spike/evidence/2026-10-14/experiment.json.gz.
    // Root and broker identifiers were anonymized; selected native values are retained verbatim.
    internal const string CapturedProducer = """
        {
          "name": "captured-client#producer",
          "client_id": "captured-client",
          "type": "producer",
          "ts": 6195073766,
          "time": 1791999554,
          "age": 1000928,
          "brokers": {
            "captured-broker/1": {
              "name": "captured-broker/1",
              "nodeid": 1,
              "state": "UP",
              "stateage": 845055,
              "req_timeouts": 0,
              "disconnects": 0,
              "txretries": 0,
              "txerrs": 0,
              "rxerrs": 0,
              "throttle": { "cnt": 0, "max": 0 },
              "rtt": { "cnt": 4, "p99": 99327 }
            }
          }
        }
        """;

    // Explicitly synthetic consumer report used to exercise positive values and empty windows.
    internal const string SyntheticConsumer = """
        {
          "name": "synthetic-consumer",
          "client_id": "synthetic-consumer-id",
          "type": "consumer",
          "ts": 9000001,
          "time": 1791999600,
          "age": 7200000,
          "brokers": {
            "synthetic-broker/2": {
              "name": "synthetic-broker/2",
              "nodeid": 2,
              "state": "UP",
              "stateage": 1200000,
              "req_timeouts": 4,
              "disconnects": 3,
              "txretries": 2,
              "txerrs": 1,
              "rxerrs": 5,
              "throttle": { "cnt": 2, "max": 17 },
              "rtt": { "cnt": 0, "p99": 0 },
              "future_native_field": "ignored"
            }
          },
          "future_root_field": { "version": 99 }
        }
        """;
}
