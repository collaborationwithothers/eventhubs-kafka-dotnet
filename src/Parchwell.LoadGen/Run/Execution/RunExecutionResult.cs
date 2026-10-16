using System.Collections.Immutable;
using Parchwell.LoadGen.Run.Contracts;
using Parchwell.LoadGen.Run.Evidence;

namespace Parchwell.LoadGen.Run.Execution;

/// <summary>Exit status and independently persisted artifacts from one finite worker execution.</summary>
public sealed record RunExecutionResult(int ExitCode, RunManifest Manifest,
    ImmutableDictionary<string, string> ArtifactPaths, RunEvidenceExpectations? EvidenceExpectations = null);
