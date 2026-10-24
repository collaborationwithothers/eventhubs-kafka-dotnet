# Terraform roots implementation receipt

The checked C1 plan is implemented as three Terraform roots with synthetic offline coverage. Formatting, initialization, validation and all native test runs passed; Azure lint remains blocked by this environment's network policy. No Azure deployment or live-readiness claim is made.

## Source and authority

Source: `e7ebd3dd7508b8735820f45704304ea0801606dd`, the C0 merge from [PR #165](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/165). Branch: `codex/157-terraform-roots`. This receipt describes the authored changes from that source, checked on 2026-10-24.

The [revision-3 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6385941149), updated `2026-10-24T15:29:00Z`, and [clean independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386117898) define the scope. [Retention Decision 6386010001](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386010001) selects the existing defaults. [Decision 6386075427](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386075427) authorizes the exact ADR-011 row and records Hari's cleanup evidence. [Decision 6386095404](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386095404) authorizes continuation after the fresh clean check; the historical open-question wording is not a new blocker.

The parent remains assigned to Codex and labelled `ready-for-agent`, with [revision-5 split approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384768840). Initial and refreshed REST snapshots of #157, #138 and #109 include their bodies and full comment ID/update sets. The refresh during implementation found no changed authority. No scope beyond the checked plan and narrow ADR-row decision was adopted.

## Commands and results

Terraform `1.16.6` and TFLint `0.64.0` were installed under `/tmp/issue157-tools` from their official release archives. Existing provider selections and checksums were retained. Each dependency installation and acceptance command had a 600 s cap and at most two attempts, following the plan. No test needed a retry.

Formatting edits used `/tmp/issue157-tools/terraform fmt -recursive infra` and `/tmp/issue157-tools/terraform fmt -recursive tests/terraform`. The following acceptance commands each exited zero on their first attempt:

```sh
timeout 600 /tmp/issue157-tools/terraform fmt -check -recursive infra
timeout 600 /tmp/issue157-tools/terraform fmt -check -recursive tests/terraform
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/persistent init -backend=false -input=false
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/cluster init -backend=false -input=false
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/ephemeral init -backend=false -input=false
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/persistent validate -no-color
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/cluster validate -no-color
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/ephemeral validate -no-color
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/persistent test -test-directory=tests -no-color
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/cluster test -test-directory=tests -no-color
timeout 600 /tmp/issue157-tools/terraform -chdir=infra/ephemeral test -test-directory=tests -no-color
```

A temporary Python inspection enumerated named runs from each test file and matched them to passing runs in the logs: persistent 10, cluster 13, ephemeral 10; total 33. There were no failed or skipped runs. The actual pinned modules emitted deprecation warnings for existing App Insights, Event Hubs and DNS-link arguments; provider or module upgrades are outside this plan.

The following commands exited one; they did not perform successful Azure lint:

```sh
timeout 600 /tmp/issue157-tools/tflint --init --config="$PWD/.tflint.hcl"
timeout 600 /tmp/issue157-tools/tflint --chdir=infra/persistent --config="$PWD/.tflint.hcl"
timeout 600 /tmp/issue157-tools/tflint --chdir=infra/cluster --config="$PWD/.tflint.hcl"
timeout 600 /tmp/issue157-tools/tflint --chdir=infra/ephemeral --config="$PWD/.tflint.hcl"
```

Plugin initialization failed while checking the checksums signature because `https://tuf-repo-cdn.sigstore.dev/15.root.json` returned `Forbidden`. The lint commands then reported the missing `azurerm` plugin. No trust check or network restriction was bypassed. These required checks remain missing, not passed.

`timeout 600 npm ci` installed the locked documentation dependencies successfully. `timeout 600 npm run check:docs` exited zero; the final scan and warning dispositions are below. `git diff --check` found no whitespace errors.

The one-off ownership inspection command is `python3 /tmp/issue157-evidence/inspect_ownership.py`. Its first attempt compared the existing ephemeral lockfile to persistent's byte representation and failed; the inspection was corrected to compare each existing root with its own source lockfile, and cluster with persistent's copy. This changed the temporary inspection only. Its second attempt passed with 35 authored paths in scope. It checked exact resource/module/output label sets, absent table overrides, unchanged Event Hubs/storage/nine-grant source, provider locks, test symlinks and the plan's authored-path set, plus the named run counts above. Its source accompanies the review diff outside the repository; neither it nor its generated output is committed.

## Documentation warnings

The scan reported 37 warnings. Individual inspection stopped at the approved cap of 20; the remaining 17 are uninspected, and no claim is made that they are harmless.

The first warning is the link from `docs/operations/a01-runner.md:127` to the now-deleted persistent storage file. It remains because C1 owns neither that legacy runbook nor its removal; the parent split assigns Runner removal to C7. The updated Terraform runbook explicitly forbids old Runner workflows against the new outputs and describes the C2 cutover. The next 19 inspected warnings are unresolved relative references in the unchanged copied upstream `long.md` and `tolong-function.md` sources under issue-114 evidence. They remain as historical source material outside this plan. Include these dispositions and the uninspected remainder in the pull request.

## Review and live limits

[Cold review round 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/157#issuecomment-6386204905) reviewed the complete uncommitted authored diff and reported the following P1 findings. These dispositions form the self-review record for the next review and pull request.

- **Planned test deletion: retained for Hari's review.** The finding is valid under AGENTS.md "Code Review Rules"; it is not dismissed or counted as a clean review. Revision-3 section 5 explicitly requires deletion of the budget/run-storage tests with their removed resources. Restoring those tests would contradict that approved removal and reference absent resources. The moved AKS, federation, network-role and App Insights assertions remain in their new roots. The planned deletion stays, and the pull request must list this P1 and link the approved plan for Hari's protected-path merge. This disposition follows the human-review treatment illustrated by [ADR-019's consequences](../../../blueprint.md#adr-019-the-verifier-is-the-only-authoritative-judge-of-a-live-runs-captured-evidence-its-checks-come-from-the-items-decision-rules-only-checked-evidence-is-published); it does not claim that rule 8's explicit exception list includes deleted tests, or independently waive the finding.
- **Container Insights source attribution: fixed.** The runbook now attributes the group stream, interval, namespace selection and ContainerLogV2 defaults to the pinned Microsoft collection settings table. The Terraform-template citation supports only addon/DCR/association wiring. Both sources were reread at their pinned commits; the Terraform configuration is unchanged.

Fix-round validation on 2026-10-24 ran `timeout 600 npm run check:docs`, `git diff --check`, `git diff --cached --check` and `python3 /tmp/issue157-evidence/inspect_ownership.py`; all exited zero. A script compared the documentation warning records with the earlier scan and found the same 37 warnings, so the dispositions and inspection cap above still apply. The ownership inspection reports 34 paths with Git's staged rename detection (the old/new AKS path pair is one rename) and matches the same 33 passing native runs in the existing logs. This prose-only fix did not rerun Terraform tests or retry blocked TFLint initialization. The current issue and parent snapshots differ from the implementation snapshots only by the implementation handoff and cold-review comments on #157; the plan and decisions are unchanged.

The revised complete authored diff and this receipt require a separate fresh `repo-review` before any normal push. No reviewed commit or clean-review claim is supplied by this fix round. The valid planned-test-deletion finding and missing TFLint checks remain visible for that review and for Hari; protected-path merge is not authorized here.

Mock-provider plan tests retain the real pinned AVMs. Only computed Azure outputs are synthetic; configured bodies, security fields and roles are checked against those modules. Zero UUIDs, `offline1`, documentation addresses and `.invalid` hosts are fixtures, not deployment values. Mock success proves no identity permission, ingestion, retention in deployed tables, CLI login, UK South patch availability, restart or live cleanup.

Hari applies persistent after the protected merge and refreshes private outputs. C2 owns compatible recovery and the first cluster/ephemeral smoke gate; later worker and telemetry gates supply their live evidence. No plan, state, operational output JSON, actual connection string or Azure credential is committed. Protected-path merge remains Hari's responsibility.
