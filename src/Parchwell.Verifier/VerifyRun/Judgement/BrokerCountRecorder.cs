using Parchwell.Verifier.Common.Contracts;
using Parchwell.Verifier.Common.Results;
using Parchwell.Verifier.VerifyRun.Input;

namespace Parchwell.Verifier.VerifyRun.Judgement;

/// <summary>Records sampled broker connection lower bounds after the statistics checks pass.</summary>
internal static class BrokerCountRecorder
{
    /// <summary>Returns measured counts only when every check from 18 through 26 passes.</summary>
    public static BrokerConnectionCount? Record(RunJudgementRequest request, IReadOnlyList<CheckAssessment> assessments)
    {
        for (int number = 18; number <= 26; number++)
        {
            CheckAssessment? assessment = assessments.FirstOrDefault(item => item.CheckNumber == number);
            if (assessment?.Evaluation != AssessmentEvaluation.Evaluated || assessment.Disposition != AssessmentDisposition.Pass)
                return null;
        }

        long boundary = request.Capabilities.Value!.StatisticsCompletion.Value!.DrainBoundaryMicroseconds.Value;
        long? maximum = null;
        long? postDrain = null;
        foreach (ReportAssociation association in request.Reports)
        {
            NativeClientReport report = association.Native.Value!;
            long count = report.Brokers.Value!.Values.LongCount(broker =>
                StatisticsChecks.IsQualifyingSource(broker.Source.Value) && string.Equals(broker.State.Value, "UP", StringComparison.Ordinal));
            maximum = maximum is long previous ? Math.Max(previous, count) : count;
            if (postDrain is null && report.TimestampMicroseconds.Value >= boundary) postDrain = count;
        }
        return new BrokerConnectionCount("LoadGenProducer", request.Identity.ClientId, maximum, postDrain, LowerBound: true);
    }
}
