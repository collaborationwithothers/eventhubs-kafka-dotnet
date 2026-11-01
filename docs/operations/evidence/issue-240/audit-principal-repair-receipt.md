# Issue 240 audit principal repair receipt

This receipt records a fix to the F06 permission audit, `infra/scripts/phase/probe_v2_access.py`. Its `main()` compared the audited principal IDs with a `principal_id` key that the real cluster output does not have, so the audit stopped before any Graph or ARM request. The fix takes the comparison IDs from the persistent layer's outputs, and a new test runs `main()` against files shaped like the real Terraform outputs.

Source commit for the commands below: `6ebaed0755b09becb75a47500ecff4196ded3302` (`origin/main`) plus the working-tree changes of this branch.

## Execution card

- Deliverable: the three items of Hari's task brief of 2026-11-01, quoted under "Authorization".
- Authorization: directly authorized work under [issue 240](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240), with no approved item plan for this fix. Hari's words in an interactive Claude Code session on 2026-11-01: "fix the F06 audit bug and open a PR". The task brief names the defect, the three deliverables and the limits: no workflow dispatch, no Azure credentials, no change under `results/`, to any `expected.yaml` or to any verdict, and no `.github/workflows/` change unless needed. This pull request does not authorize another F06 run; Hari decides that separately on issue 240.
- Defect evidence: [scientific run 41292426657](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41292426657) on `main` at `8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1`, and the [analysis comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6556466439). That comment is evidence, not a decision.
- Mandatory gates: the commands below; `npm run check:docs`; the cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review; the merge under AGENTS.md GOVERNANCE "Merge rule".
- Verification commands: listed under "Commands and results".
- Publication checkpoint: open the draft pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; the exhaustion action is its review-budget handoff to Hari.

## Specification comparison

The approved [Revision 7 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6490062145) says the CLI "reads phase/ephemeral/cluster outputs, checks identity/topic bindings". After this fix `main()` reads phase, ephemeral and persistent outputs, and no longer reads the cluster output. This is a deliberate deviation, made under the task brief, which names `persistent-output.json` as a candidate source. The plan names no source for the principal cross-check itself.

## Choice of source

`main()` builds the audited binding from the ephemeral output `probe_v2_role_scope.accounts.*.principal_id`. The cross-check needs a second source for the same IDs.

| Candidate | Has principal IDs | Change needed | Independent of the ephemeral value |
|---|---|---|---|
| `cluster-output.json`, `workload_identity.service_accounts` | No (`infra/cluster/outputs.tf`, lines 5 to 15) | Terraform output change in `infra/cluster`, then a cluster apply by a workflow | No. `infra/cluster/locals.tf` line 2 and `infra/ephemeral/locals.tf` line 2 both read the same persistent remote state |
| `persistent-output.json`, `workload_identities` | Yes (`infra/persistent/outputs.tf`, lines 26 to 29: `id`, `client_id`, `principal_id`) | None | Yes, by path. It is the `PARCHWELL_PERSISTENT_OUTPUTS` secret, which `infra/scripts/phase/phase.py` lines 345 to 379 write to the private directory. The ephemeral value is read from remote state during the run's apply |

Chosen: `persistent-output.json`. It needs no Terraform change and no apply in any layer. `docs/operations/a01-workflow.md` line 19 says the secret "must contain the `terraform output -json` text of `infra/persistent`". The [Terraform `output` command page](https://developer.hashicorp.com/terraform/cli/commands/output), read 2026-11-01, says `-json` gives "a JSON object, with a key per output", and that it shows `sensitive` values in plain text. That page does not describe the per-output `value` wrapper. The wrapper used in the test is inferred from the repository's existing `unwrap` in `infra/scripts/phase/probe_v2.py` line 122, which accepts both forms.

A mismatch now means the secret and the ephemeral layer's remote-state read disagree, for example after an identity was recreated and the secret was not refreshed. The audit then fails closed with `permission_evidence_incomplete`, before any request.

Not verified: that the current `PARCHWELL_PERSISTENT_OUTPUTS` secret contains `workload_identities`. No agent session can read it. `docs/operations/terraform.md` line 109 says Hari refreshed it "with the new probe identity", which suggests it does. If it does not, the audit still fails closed with the same catch-all line.

## Changes

- `infra/scripts/phase/probe_v2_access.py`: `main()` reads `persistent-output.json` with a 4194305-byte limit (the 4 MiB secret limit in `phase.py` plus the newline `write_private` adds), and compares each principal through a new `applied_principal` helper. The helper raises `Gap('input_invalid')` when the entry or its `principal_id` is missing or not a string. Without it a non-string value would raise `AttributeError`, which the catch-all in `main()` does not catch.
- `infra/scripts/phase/tests/test_probe_v2_access.py`: new `MainTests`. It writes `phase.json`, `ephemeral-output.json`, `cluster-output.json` and `persistent-output.json` in the `terraform output -json` shapes of the three `outputs.tf` files, with synthetic GUIDs, and runs `main()` with the synthetic Graph and ARM reader.
- `infra/scripts/phase/tests/test_probe_v2.py`: its `setUp` wrote `cluster-output.json` with a `principal_id` in each service account, which the real output lacks. It now writes only `name`, `client_id` and `subject`. This is the same fixture defect class (AGENTS.md "Before opening a pull request" rule 5).

No Terraform, workflow, chart, .NET, `results/`, `expected.yaml` or verdict file changes. No item state file is written: docs/agents/factory.md "Item state file" makes the `implement-plan` session its only writer, and this is not such a session.

## Audit of other keys `main()` reads

Each key was checked against the code that writes the file.

| File and key | Writer | Matches |
|---|---|---|
| `phase.json` `run_id`, `source_commit` | `infra/scripts/phase/phase.py` line 380 | Yes |
| `ephemeral-output.json` `probe_v2_role_scope` `namespace_id`, `topics` (keys `document-events`, `document-state`, `document-events-dlq`), `accounts.loadgen` and `accounts.enrichment-consumer` `principal_id` | `infra/ephemeral/outputs.tf` lines 24 to 50 | Yes |
| `cluster-output.json` `workload_identity.service_accounts.*.principal_id` | `infra/cluster/outputs.tf` lines 5 to 15 | No. This was the defect; `main()` no longer reads it |
| `persistent-output.json` `workload_identities.loadgen` and `.enrichment-consumer` `principal_id` | `infra/persistent/outputs.tf` lines 26 to 29, keys from `infra/persistent/locals.tf` line 2 | Yes |
| Environment `PARCHWELL_ARTIFACT_DIRECTORY` | `infra/scripts/phase/phase.py` line 393 | Yes |
| Repository root `Path(__file__).resolve().parents[3]` | file at `infra/scripts/phase/` | Yes |

Inferred, not verified: that the applied Azure IDs of the namespace and hubs pass `validate_binding`. That depends on provider ID formats, which no offline test here exercises.

## Predicate map

The changed predicate is the principal cross-check in `main()`. Each case is a subtest of `MainTests`, with all other files left valid:

| Case | Expected | Test |
|---|---|---|
| Equal IDs | exit 0, `{"permission_check":"complete","reasons":[]}` | `test_main_with_real_output_shapes` |
| Equal except case | exit 0, complete | `test_main_principal_cross_check` `uppercased` |
| Different GUID for one account | exit 1, `permission_evidence_incomplete`, no reader built, no output files | `different` |
| `principal_id` is `null` | same as above | `none` |
| Account entry missing | same as above | `entry_missing` |
| `workload_identities` missing | same as above | `key_absent` |
| `persistent-output.json` missing | same as above | `file_absent` |

Before the fix, the failing-input cases also failed, but for the wrong reason: the missing cluster key. The `uppercased` and equal cases are the ones that show the defect and the fix.

## Commands and results

Run from the repository root with Python 3 from the container. The phase tests run in no workflow. The command follows `docs/operations/a01-workflow.md`, with Helm 4.3.1 from `infra/scripts/lifecycle/install-tools.sh` line 23 (SHA-256 checked) on `PATH`.

| Command | Literal result |
| --- | --- |
| `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_probe_v2_access.py' -v`, new tests present, fix absent | `Ran 33 tests`, `FAILED (failures=3, errors=1)`: `test_main_with_real_output_shapes` (exit 1, not 0), `test_main_principal_cross_check [uppercased]`, and `test_both_streams_capped (stream='stderr')` failure and error |
| The same command, fix applied | `Ran 33 tests`, `FAILED (failures=1, errors=1)`: only `test_both_streams_capped (stream='stderr')` |
| `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py' -v`, this branch | `Ran 187 tests`, `FAILED (failures=1, errors=1)`: only `test_both_streams_capped (stream='stderr')` |
| The same suite on `origin/main` (changes stashed) | `Ran 185 tests`, `FAILED (failures=1, errors=1)`: the same test |
| `git diff --check` | no output |

`test_both_streams_capped (stream='stderr')` fails on `origin/main` too, with `Gap not raised`. This change does not touch `ProductionReader`. Its cause in this container is not diagnosed.

`npm ci --ignore-scripts && npm run check:docs` gives `8 warnings`, none in a changed file. Blanket line: the 8 baseline warnings are in unchanged files (`docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3).

## Cold review input

Round 1: comprehensive review of the whole authored diff, `git diff --cached --merge-base origin/main`, with merge base `6ebaed0755b09becb75a47500ecff4196ded3302`. Specification sources: the task brief and issue 240 above, and the Revision 7 plan for the deviation recorded under "Specification comparison".

## Models

Code and tests: a Claude Code subagent on the `sonnet` alias (Sonnet 5.5), under CLAUDE.md "Model bindings", Implementation. Analysis, source choice, this receipt and the pull request text: the authoring session on Opus 5.5. The first part of the session ran on Opus before Hari pointed out the binding; it made no file edits before the delegation.

The cold-review record and the final-head CI run are recorded in the pull request description.
