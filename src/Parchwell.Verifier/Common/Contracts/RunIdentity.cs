namespace Parchwell.Verifier.Common.Contracts;

/// <summary>Identifies one captured producer run independently of its artifact representation.</summary>
/// <remarks>Uses the independent identity shape in the <see href="https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6245697297">approved PR 3 wire catalogue</see>.</remarks>
internal sealed record RunIdentity(
    string ScenarioId,
    string ConfigurationId,
    string RunId,
    string InstanceId,
    string ExpectedInstanceId,
    string ClientId);
