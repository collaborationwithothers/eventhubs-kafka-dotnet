using System.Text;
using Parchwell.Kafka.Statistics;

namespace Parchwell.LoadGen.Common.Monitoring;

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
    internal static Action<string> CreateSelective(NativeSourceCapture capture, NativeMonitoringHost? monitoring,
        KafkaNativeMonitoringLifetime? lifetime, int maxReportBytes) => raw =>
    {
        bool admitted = capture(raw, out long sequence);
        if (lifetime is null) return;
        try
        {
            if (new UTF8Encoding(false, true).GetByteCount(raw) > Math.Min(maxReportBytes, 262144))
            {
                lifetime.MarkIncomplete("report_limit");
                return;
            }
        }
        catch (EncoderFallbackException)
        {
            lifetime.MarkIncomplete("invalid_utf8");
            return;
        }
        if (admitted) monitoring!.Capture(lifetime, raw, sequence);
        else lifetime.CaptureNew(raw);
    };
}
