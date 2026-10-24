# C0 rules and design authority

This is a documentation and scenario-policy change for #156, the C0 child of #138. No live validation was performed.

## Authority and scope

The source baseline is `1367d760ef36b1bab998034511eddad16815d8d9`. Implementation follows the [checked Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384803329), its [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384828745), and [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384861716). The [C0 freeze exception](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6384844065) was read before edits.

The [later C0 decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384901513) waives only the pre-edit cleanup-evidence prerequisite. Cleanup was not verified. Before edits, the repository Actions runs REST queries for `queued`, `in_progress`, `waiting`, `requested` and `pending` each returned zero active runs. No workflow was dispatched and no Azure deployment or cleanup action was performed.

ADR-020 records the target architecture. The later children implement and validate it. [C5/#162](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/162) retains ownership of the executable judge/verifier change and `verifier-contract.md`. The expected-outcome amendment is limited to the approved [no-positive-sample probe policy](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384660536). It does not change the scientific hypotheses, threshold, repeat count, load or component expectations.

## Acceptance

The following commands ran against the baseline plus this authored patch. The new receipt was staged before the documentation scan so that the scanner included it.

| Command | Observed result |
|---|---|
| `git diff --check` | Exit 0 |
| `npm ci --ignore-scripts` | Exit 0; locked dependencies installed; npm reported the existing glob 10.5.0 deprecation |
| `npm run check:docs` | Exit 0; no warning in any authored document; 36 advisory warnings in unchanged historical upstream captures |
| The checked plan's temporary Python byte-comparison command | Exit 0; expected.yaml differs only by the exact approved probe-usability block, and scenario.yaml is byte-for-byte unchanged |
| `git diff --stat`, `git diff --name-only`, and their `--cached` equivalents | The seven owned existing files and this new receipt only |
| Temporary Python path-set comparison of `git diff --name-only HEAD` | Exact approved set of 8 paths |

The byte-comparison command reported: "Expected-outcome delta is exactly the approved probe policy; scenario unchanged." It compares against the source baseline above and adds no YAML parser or runtime check.

Warning counts were computed from the scanner log. The first 30 distinct warnings were inspected, reaching the plan's per-invocation cap; the remaining 6 were not individually inspected. All warnings belong to unchanged historical upstream captures under `docs/operations/evidence/issue-114/raw/review-10-fixes/`. The inspected warnings are relative links to files or headings in the upstream documentation tree, absent from these preserved captures. They remain because C0 does not edit historical evidence or expand its scope. The remaining inspection gap is recorded under the plan's "record the gap and continue" exhaustion action. The advisory scan does not check external URLs, architecture, runtime behavior or live gate success.

No .NET tests, Terraform plans or infrastructure execution were required or performed for this documentation-only change. Program output and temporary validation scripts remain outside the repository.

## Review

The [first cold review](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384965944) applied `repo-review` in a fresh context to the full staged diff and this receipt, against the source baseline above. Its reviewed diff SHA-256 was `7b6f9176e52fcfb6220841c8910b69b476b4c98d9630c28d5161ec4dbd763ea8`; that identifies the patch before this review record was added, not a reviewed head commit.

The review raised one P0: the probe-usability insertion in `scenarios/A01/expected.yaml` triggers AGENTS.md "Code Review Rules" for an expected-outcome change outside a verdict pull request. Disposition: retain the approved work under "Before opening a pull request" rule 8's approval-only exception. Section 7 of the [checked plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384803329) requires the exact insertion under the [probe decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6384660536), and the [C0 approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/156#issuecomment-6384861716) approves that plan. This is an approval-only finding, not a disputed finding or a defect fixed by this record. The PR description must retain it and its authority. Hari's issue-specific conditional agent-merge permission still requires fixed Codex review comments, required checks and review of the final head.

The review reported no additional P0/P1 and no P2 findings. After applying the approved-work exception, it required no patch fix. It independently confirmed the whitespace, documentation, exact probe-insertion and unchanged-scenario checks; the historical documentation warnings and deferred C5 implementation remain as described above. It did not verify runtime behavior, live gates or cleanup.

This continuation changes only this receipt. The affected acceptance commands were rerun after the record was added: `git diff --check`, `git diff --cached --check`, `npm run check:docs`, the plan's temporary Python byte comparison, and the working/staged diff summaries and exact path-set comparison. They completed successfully with the same 36 historical advisory warnings and no authored-document warning; the first 30 distinct warnings were inspected, with the remaining 6 recorded under the plan's exhaustion action. Dependencies were unchanged, so the earlier successful `npm ci --ignore-scripts` remains the installation evidence. The updated complete diff and receipt need the separately orchestrated cold review before push. No reviewed head commit or pull request has been produced by this continuation.
