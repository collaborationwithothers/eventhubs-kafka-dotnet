namespace Parchwell.Verifier.Common.SingleAction;

/// <summary>Fixed transport and process bounds for the single-action judge.</summary>
internal sealed class SingleActionLimits
{
    public long? MaxCaptureBytes { get; set; }
    public int? MaxRegistrationBytes { get; set; }
    public int? MaxCompletionBytes { get; set; }
    public int? MaxSeedReceiptBytes { get; set; }
    public int? MaxSeedPodBytes { get; set; }
    public int? MaxCandidateBytes { get; set; }
    public long? MaxTotalInputBytes { get; set; }
    public int? MaxInputDepth { get; set; }
    public int? MaxSettingsKeys { get; set; }
    public int? MaxSettingsBytes { get; set; }
    public int? MaxErrorBytes { get; set; }
    public int? MaxDiagnosticBytes { get; set; }
    public int? MaxDiagnostics { get; set; }
    public int? MaxClientErrors { get; set; }
    public int? MaxStatisticsReports { get; set; }
    public int? MaxStatisticsReportBytes { get; set; }
    public int? CallbackQueueCapacity { get; set; }
    public int? MaxBrokerEntries { get; set; }
    public int? MaxMatchedValueBytes { get; set; }
    public int? MaxNonmatchingRecords { get; set; }
    public int? ProcessLifetimeSeconds { get; set; }
    public int? IoSeconds { get; set; }
    public int? CleanupSeconds { get; set; }

    internal bool IsFixed()=>MaxCaptureBytes==134217728&&MaxRegistrationBytes==65536&&MaxCompletionBytes==65536&&MaxSeedReceiptBytes==65536&&MaxSeedPodBytes==65536&&MaxCandidateBytes==16777216&&MaxTotalInputBytes==268435456&&MaxInputDepth==32&&MaxSettingsKeys==128&&MaxSettingsBytes==16384&&MaxErrorBytes==4096&&MaxDiagnosticBytes==2048&&MaxDiagnostics==100&&MaxClientErrors==64&&MaxStatisticsReports==64&&MaxStatisticsReportBytes==262144&&CallbackQueueCapacity==128&&MaxBrokerEntries==64&&MaxMatchedValueBytes==1100000&&MaxNonmatchingRecords==4096&&ProcessLifetimeSeconds==90&&IoSeconds==10&&CleanupSeconds==10;
}
