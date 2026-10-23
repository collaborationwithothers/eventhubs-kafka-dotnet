# Runner recovery fixes evidence

This implements [issue #140 Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/140#issuecomment-6369893038), approved in [6370132290](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/140#issuecomment-6370132290). Offline build, Runner tests and Terraform validation/mock tests pass; TFLint remains blocked by the environment's denial of its signature-verification endpoint. No live Azure operation or experimental result is claimed.

## Implementation and authority

Base: `41f546d673a3609f79fb689401bef8737758241c`, the merged predecessor PR #139. Implementation commit: `5d5c168cf855134dbdac9128154c38c448887bc8`. Analyzer correction: `a20dad4b736c81956961a2be57abc347a450d0a8`; this only moves the exact kubelogin argument array into a static readonly field. Later evidence-only commits do not change the tested implementation. The final reviewed head and PR workflow run belong in the PR self-review and handoff, so they can name the commit containing this evidence without a self-referential hash.

Read #140, parents #109/#114 and recursive parent #14, all their comment pages and linked authority. Revision 2 was edited at 2026-10-23T16:50:23Z; its approval was posted at 2026-10-23T16:53:08Z. Hari explicitly authorized this implementation session and assigned Codex; the earlier session's preparation boundary does not apply. Historical PR #115 review exceptions do not transfer. Authority refresh follows factory.md "Authority reads" before publication.

Four high-effort Codex subagents implemented disjoint A/B/C/D ownership. The parent integrated their changes, authored the runbook and evidence, and owns commits and the single PR. The patch changes no trigger or job timeout, package, project, scenario, expected outcome, verdict, public result, identity, grant or persistent provider.

## Validation

All planned acceptance commands were invoked, each with the plan's 600-second cap. The [manifest](raw-manifest.json) records exact commands, source commits, byte sizes and SHA-256 values. Logs retain command output and exit status. Terraform checks used synthetic mocks and backend-disabled initialization; neither establishes live deletion behavior.

| Check | Evidence | Result |
|---|---|---|
| Pinned tools | [versions](raw/tool-versions.txt) | .NET 10.0.402, Terraform 1.16.6, TFLint 0.64.0 |
| Locked solution restore | [restore](raw/dotnet-restore.txt) | Exit 0 |
| Release solution build | [final build](raw/dotnet-build-final.txt) | Exit 0, zero warnings/errors |
| Runner suite | [tests](raw/runner-tests.txt) | 1130 passed, zero failed/skipped |
| Terraform formatting | [infra](raw/terraform-fmt-infra.txt), [tests](raw/terraform-fmt-tests.txt) | Both exit 0 |
| Locked backend-disabled init | [persistent](raw/terraform-persistent-init.txt), [ephemeral](raw/terraform-ephemeral-init.txt) | Both exit 0 |
| Terraform validate | [persistent](raw/terraform-persistent-validate.txt), [ephemeral](raw/terraform-ephemeral-validate.txt) | Both exit 0 |
| Native mock tests | [persistent](raw/terraform-persistent-test.txt), [ephemeral](raw/terraform-ephemeral-test.txt) | 12 and 15 passed; zero failures |
| TFLint installation | [init](raw/tflint-init.txt) | Exit 1: `tuf-repo-cdn.sigstore.dev/15.root.json` denied during signature verification |
| TFLint roots | [persistent](raw/tflint-persistent.txt), [ephemeral](raw/tflint-ephemeral.txt) | Both exit 1: required azurerm plugin unavailable |
| Documentation | [final check](raw/check-docs-final.txt) | Exit 0; retained advisory warnings explained below |
| Diff whitespace | [check](raw/diff-check.txt) | Exit 0 |
| Workflow budgets/copies | [arithmetic](raw/budget-arithmetic.txt) | Exact planned totals and byte-identical installed/copy pairs |

The [first build](raw/dotnet-build.txt) failed CA1861 on the constant kubelogin test array. The correction preserves its exact expected values; the successful build and Runner run cover the corrected code. No failed validation was hidden or rerun unchanged. TFLint signature checks were not bypassed; installation and both root lint checks remain outstanding. Existing pinned AVM modules emit deprecation warnings in Terraform mock tests; pins and modules are unchanged by this repair.

The [initial documentation scan](raw/check-docs.txt) ran before the manifest existed and reported its missing link. The manifest is now present. The remaining advisories are in three unchanged upstream Markdown captures under `issue-114/raw/review-10-fixes/`: `long.md`, `tolong-function.md` and `union-operator.md`. Their relative upstream includes/links are absent locally, and one upstream punctuation warning remains. Preserve these historical raw bytes instead of editing evidence to clear advisory diagnostics; the PR description carries this disposition.

The Runner suite establishes the focused argument, budget and missing-parameter contracts, including malformed present JSON still failing. It does not establish solution-wide component coverage. The existing PR workflow supplies its normal solution-wide verification on the final head; its result is reported separately in the handoff.

## Budget arithmetic and limits

[The script output](raw/budget-arithmetic.txt) reproduces the approved values from the final workflows: probe `14310 < 14400` seconds with 90 remaining; each full phase `12960 < 14400` with 1440 remaining; sweep `3090 < 3600` with 510 remaining. The command is reproduced in the manifest, not added as a new checker. Shell-only sums are 12930, 12840, 12840 and 3090 seconds; the existing Python publication/readback loops add 1380, 120, 120 and zero seconds respectively.

Hari's [reported teardown intervals](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/140) sum to 990 seconds; the selected 1050-second allowance has 60 seconds of margin. These are attributed observations and budget arithmetic, not a live performance guarantee. Phase work remains 8400 seconds; the strict apply/export/destroy reserve boundary is 2250 seconds. Unwrapped setup/login/upload and Helm's existing 60-second download share the probe's tight 90-second remainder. No job timeout was increased.

## Source checks

- [Kubelogin v0.2.21 Azure CLI documentation](https://github.com/Azure/kubelogin/blob/v0.2.21/docs/book/src/concepts/login-modes/azurecli.md), [retained source](raw/sources/kubelogin-azurecli.md.txt): uses the existing login; an explicit subscription overrides the active subscription. The get-credentials subscription selection remains exact.
- [AzureRM v4.81.0 features guide](https://github.com/hashicorp/terraform-provider-azurerm/blob/v4.81.0/website/docs/guides/features-block.html.markdown#resource_group), [retained source](raw/sources/azurerm-features.md.txt): the default-true resource-group pre-deletion check requires contained resources removed first. The flag choice follows #140; no alternative managing the auto-created rule is claimed verified.
- [Terraform v1.16.6 force-unlock](https://github.com/hashicorp/terraform/blob/v1.16.6/internal/command/unlock.go), [retained source](raw/sources/terraform-unlock.go.txt), and [Azure backend](https://github.com/hashicorp/terraform/blob/v1.16.6/internal/backend/remote-state/azure/client.go), [retained source](raw/sources/terraform-azure-client.go.txt): manual unlock targets configured state and checks the lock identity before releasing the Blob lease. Recovery remains Hari's manual operation.
- [EvidenceExporter](../../../../src/Parchwell.Runner/Run/Evidence/EvidenceExporter.cs) passes container `evidence` separately from prefix `A01/<environment_run_id>/`; [RunLedger](../../../../src/Parchwell.Runner/Run/Cost/RunLedger.cs) uses blob `A01/state.json` in container `ledger`. Reset documentation preserves this distinction and prior spend.

All external sources above were read on 2026-10-23 at their stated pins. Offline checks do not prove live token acquisition, tenant access, setup duration, teardown completion or recovery after a cancelled destroy.
