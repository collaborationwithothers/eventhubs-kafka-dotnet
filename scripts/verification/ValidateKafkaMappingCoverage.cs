using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

public sealed class ValidateKafkaMappingCoverage : Microsoft.Build.Utilities.Task
{
    [Required] public string CoverageReport { get; set; }
    [Required] public string KafkaAssembly { get; set; }
    [Required] public string VerificationAssembly { get; set; }

    public override bool Execute()
    {
        try
        {
            var verifier = Assembly.LoadFrom(Path.GetFullPath(VerificationAssembly))
                .GetType("Parchwell.ArchitectureTests.Verification.KafkaCoverageVerifier", true);
            var lines = (int)verifier.GetMethod("Verify").Invoke(null, new object[] { CoverageReport, KafkaAssembly });
            Log.LogMessage(MessageImportance.High, "Parchwell.Kafka: " + lines + " instrumented mapping source lines covered; branch completeness is checked by behavioral tests.");
            return true;
        }
        catch (Exception exception)
        {
            Log.LogError("Kafka mapping coverage rejected: " + exception.GetBaseException().Message);
            return false;
        }
    }
}
