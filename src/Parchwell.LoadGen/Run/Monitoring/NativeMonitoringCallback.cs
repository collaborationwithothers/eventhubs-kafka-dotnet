using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Run.Monitoring;

internal delegate bool NativeSourceCapture(string raw, out long sequence);

internal static class NativeMonitoringCallback
{
    internal static Action<string> Create(NativeSourceCapture capture, NativeMonitoringHost? monitoring,
        KafkaNativeMonitoringLifetime? lifetime) => raw =>
    {
        bool admitted = capture(raw, out long sequence);
        if (lifetime is null) return;
        if (admitted) monitoring!.Capture(lifetime, raw, sequence);
        else lifetime.MarkIncomplete("raw_capture_rejected");
    };
}
