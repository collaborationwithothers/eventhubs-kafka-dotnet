using Parchwell.Verifier.Common.Phase;

namespace Parchwell.Verifier.Judge;

internal sealed record B10RegistrationJudgement(
    bool? JobBinding, bool? RegisteredProducerCount,
    ProducerRegistration? First, ProducerRegistration? Restart);

internal static class B10JudgeChecks
{
    internal static B10RegistrationJudgement EvaluateRegistrations(
        VerifierHostOptions options, int repeat, bool listingComplete,
        IReadOnlyList<(string PathInstance, ProducerRegistration Registration)> registrations,
        int listedRegistrationCount, bool malformedRegistration)
    {
        string firstJob = PhaseIdentity.JobName(options.RunId!, options.Phase!, repeat);
        string restartJob = PhaseIdentity.RestartJobName(options.RunId!, options.Phase!, repeat);
        bool? binding = null;
        foreach ((string pathInstance, ProducerRegistration registration) in registrations)
        {
            var identity = registration.Identity;
            if (identity.ScenarioId != options.ScenarioId || identity.ConfigurationId != options.ConfigurationId ||
                identity.RunId != options.RunId || registration.Phase != options.Phase || registration.RepeatNumber != repeat ||
                registration.SourceCommit != options.SourceCommit || identity.InstanceId != pathInstance ||
                identity.InstanceId != identity.ExpectedInstanceId || identity.InstanceId != registration.PodName ||
                (registration.JobName != firstJob && registration.JobName != restartJob) ||
                (registration.JobName == firstJob && registration.PredecessorInstanceId is not null))
                binding = false;
        }
        ProducerRegistration[] first = registrations.Select(x => x.Registration).Where(x => x.JobName == firstJob).ToArray();
        ProducerRegistration[] restart = registrations.Select(x => x.Registration).Where(x => x.JobName == restartJob).ToArray();
        if (first.Length > 1 || restart.Length > 1) binding = false;
        ProducerRegistration? firstRecord = first.Length == 1 ? first[0] : null;
        ProducerRegistration? restartRecord = restart.Length == 1 ? restart[0] : null;
        bool duplicate = registrations.Select(x => x.Registration.Identity.InstanceId).Distinct(StringComparer.Ordinal).Count() != registrations.Count;
        if (duplicate) binding = false;
        if (firstRecord is not null && restartRecord is not null)
        {
            if (restartRecord.PredecessorInstanceId != firstRecord.Identity.InstanceId) binding = false;
            else if (binding is not false) binding = true;
        }
        bool? count = listedRegistrationCount > 2 || malformedRegistration || duplicate ? false :
            listingComplete && listedRegistrationCount == 2 && registrations.Count == 2 ? true : null;
        return new(binding, count, firstRecord, restartRecord);
    }

    internal static bool BindsIntake(B10IntakeEvidence intake, ProducerRegistration first, ProducerRegistration restart) =>
        intake.Phase == first.Phase && intake.Phase == restart.Phase &&
        intake.RepeatNumber == first.RepeatNumber && intake.RepeatNumber == restart.RepeatNumber &&
        intake.Producers.Count == 2 && Binds(intake.Producers[0], first) && Binds(intake.Producers[1], restart);

    private static bool Binds(B10ProducerEvidence producer, ProducerRegistration registration) =>
        producer.Identity == registration.Identity && producer.JobName == registration.JobName &&
        producer.PredecessorInstanceId == registration.PredecessorInstanceId;

    internal static bool SameIntake(B10IntakeEvidence left, B10IntakeEvidence right) =>
        left.Phase == right.Phase && left.RepeatNumber == right.RepeatNumber &&
        left.Producers.Count == right.Producers.Count && left.Producers.Zip(right.Producers).All(pair =>
            pair.First.Identity == pair.Second.Identity && pair.First.JobName == pair.Second.JobName &&
            pair.First.PredecessorInstanceId == pair.Second.PredecessorInstanceId &&
            SameSnapshot(pair.First.PublicationAcceptance, pair.Second.PublicationAcceptance));

    private static bool SameSnapshot(PublicationAcceptanceSnapshot? left, PublicationAcceptanceSnapshot? right) =>
        left is null ? right is null : right is not null && left.ProducerPrefix == right.ProducerPrefix &&
        left.ProducerIdentity == right.ProducerIdentity && left.SourceCommit == right.SourceCommit &&
        left.StartLine == right.StartLine && left.BrokerInventory == right.BrokerInventory &&
        left.ProducerReceipt == right.ProducerReceipt && left.Objects.SequenceEqual(right.Objects);
}
