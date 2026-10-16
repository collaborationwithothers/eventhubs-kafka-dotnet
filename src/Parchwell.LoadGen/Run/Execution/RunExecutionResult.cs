using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Exit status and independently persisted artifacts from one finite worker execution.</summary>
public sealed record RunExecutionResult(int ExitCode, RunManifest Manifest,
    ImmutableDictionary<string, string> ArtifactPaths);
