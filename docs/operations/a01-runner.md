# A01 Runner retirement

The A01 Runner and Helm values generator have been removed under [ADR-020](../blueprint.md#adr-020-the-workflow-owns-the-infrastructure-lifecycle-no-net-code-orchestrates). Use the [scenario workflow](a01-workflow.md) and [lifecycle commands](lifecycle.md#commands) to run the infrastructure and phase Jobs.

Workers resolve the packaged scenario and profile in-process through the [scenario contract](scenario-contract.md). The [chart contract](chart-contract.md) owns deployment inputs, the [verifier contract](verifier-contract.md) owns phase judgement, and [worker image smoke](../engineering/dotnet-verification.md#worker-image-smoke) checks that both published images start.

## Validation receipt

Validation pending in this implementing session; this note claims no test result.
