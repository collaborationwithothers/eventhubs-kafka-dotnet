namespace Parchwell.Verifier.Common.Contracts;

/// <summary>Identifies one captured producer run independently of its artifact representation.</summary>
/// <remarks>Its six fields are the identity shape specified by ADR-012.</remarks>
internal sealed record RunIdentity(
    string ScenarioId,
    string ConfigurationId,
    string RunId,
    string InstanceId,
    string ExpectedInstanceId,
    string ClientId);
