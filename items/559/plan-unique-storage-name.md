# unique-storage-name contract-grade plan

Revision: r1. Issue: [#559](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/559) "Give each ephemeral storage account a unique name". Base commit on `main`: `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`. Written by the planning subagent of an `implement-plan` session under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval", model `claude-opus-5-5`.

This plan gives the ephemeral storage account a name with a five-character random part that Terraform creates on each apply from empty state and deletes on each destroy. Nothing else in the repository reads the fixed name, so the change is two Terraform lines, test fixtures and two new tests. The plan is not approvable yet: it changes live infrastructure, and docs/agents/factory.md "Item lifecycle" step 1 requires a live smoke run of the design first; that and one named risk are under "Open questions for Hari".

## 1. Goal and authority

Goal: every ephemeral apply that starts from empty state names its storage account `stparchwell<naming suffix><5 random lowercase letters or digits>`, so the second phase of a two-phase run no longer reuses the name that the first phase deleted minutes earlier.

Authority, in creation order:
- The issue body, read at `updated_at` 2026-11-19T21:00:37Z. It carries the `ready-for-agent` label. The issue had no comments when this plan was written (catch-up below), so no decision, approval or tool-naming comment exists. Under docs/agents/factory.md "Item lifecycle" step 2, no tool-naming comment means Claude Code.
- No item state file exists at `items/559/state.md` on the base commit. That is the normal case before the first implementation pull request merges (docs/agents/factory.md "Item state file").

Catch-up snapshot (docs/agents/factory.md "Authority reads"): issue body `updated_at` 2026-11-19T21:00:37Z; comment set empty (page 1 of `GET /repos/collaborationwithothers/eventhubs-kafka-dotnet/issues/559/comments?per_page=100` returned no comments).

Candidate selection (docs/agents/factory.md "Item lifecycle" step 3): no split is approved on the issue and no plan comment exists, so `plan-pr` plans the whole issue as one pull request. Its short name is `unique-storage-name`. The work is one coherent piece (one external contract change, the storage account name, with its tests), so there is no split.

## 2. Scope

Files allowed to change:
- `infra/ephemeral/locals.tf`: the `names.storage` entry only.
- `infra/ephemeral/storage.tf`: add the `random_string.storage` resource.
- `tests/terraform/ephemeral/main.tftest.hcl` (reached also through the `infra/ephemeral/tests` symlink): fixture names, one file-level override block, one new `run` block.
- `infra/scripts/phase/tests/test_phase.py`: one new test method.
- `infra/scripts/phase/tests/test_consumer_repeat.py`: one new test method.
- `items/559/state.md`: created by the implementing session under docs/agents/factory.md "Item state file".

Paths that must not change:
- `infra/persistent/**`, including the archive account name `stparchwellarc<naming suffix>` (`infra/persistent/locals.tf` line 4). Out of scope in the issue body.
- `infra/ephemeral/identity*.tf` and `infra/ephemeral/network*.tf` (protected paths in `.github/CODEOWNERS`). Requirement R5 holds without editing them.
- `infra/ephemeral/eventhubs.tf`, `var.eventhub_namespace_name` and every Event Hubs name. Out of scope in the issue body.
- `infra/ephemeral/versions.tf`, `infra/ephemeral/.terraform.lock.hcl`, `infra/ephemeral/providers.tf`: `hashicorp/random` is already pinned at `= 3.9.1` (`versions.tf` line 6), locked (`.terraform.lock.hcl` line 65) and configured (`providers.tf` line 10).
- `infra/ephemeral/variables.tf`: no new variable. `naming_suffix` keeps its `^[a-z0-9]{8}$` validation.
- `infra/ephemeral/outputs.tf`: the `blob` output already derives `account_url` from the module (`outputs.tf` line 12).
- `infra/scripts/phase/*.py`, `infra/scripts/archive/*.py`, `infra/scripts/lifecycle/*`, `charts/**`, `src/**`, `.github/workflows/**`, `docs/**`, `results/**`, every `expected.yaml`.
- The storage readiness wait of decision 6765011642 (`phase.py` `STORAGE_READINESS_WAIT_SECONDS`, 120 s) stays as it is. Removing or changing it is not part of this issue.

Because no protected path changes, the pull request is eligible for the agent merge under AGENTS.md GOVERNANCE "Merge rule", subject to every other condition there.

### Inventory of the fixed name (issue requirement 2)

Found by `grep -rn "stparchwell\|names.storage"` over the base commit, plus a search for every reader of the ephemeral `blob.account_url` output and of storage account names. The pull request description repeats this list with what was done at each place.

| Place | What it does with the name | Action |
|---|---|---|
| `infra/ephemeral/locals.tf` line 6 | Defines `stparchwell${var.naming_suffix}` | Changed (R1) |
| `infra/ephemeral/storage.tf` line 3 | Passes `local.names.storage` to the storage module | Unchanged; follows R1 |
| `infra/ephemeral/outputs.tf` line 12 | `account_url = "https://${module.storage.fqdn.blob}"` | Unchanged; derived from the module |
| `infra/ephemeral/identity.tf` lines 4-21, 48-66 | Role scopes are `module.storage.containers.<key>.id` | Unchanged; follow the module (R5) |
| `infra/ephemeral/network.tf` lines 9-12 | Private endpoint lookup by `basename(module.storage.private_endpoints.blob.id)` | Unchanged; the endpoint keeps the name `pe-blob-<naming suffix>` |
| `infra/scripts/phase/phase.py` lines 628-633 | `blob_host()` reads `account_url` from `ephemeral-output.json`, accepts `[a-z0-9]{3,24}\.blob\.core\.windows\.net` | Unchanged; covered by T4 |
| `infra/scripts/phase/consumer_repeat.py` line 238 | Account name for `az storage blob list` is `blob_host()` up to the first dot | Unchanged; covered by T5 |
| `infra/scripts/phase/archive.py` lines 782-784, 2195 | Source account is `account_url` host up to the first dot; refuses a source equal to the archive account | Unchanged (see section 3, archive) |
| `infra/scripts/archive/smoke.py` line 128 | Source endpoint from `blob.account_url` | Unchanged |
| `charts/parchwell/templates/*.tpl`, `jobs.yaml`, `values.schema.json` lines 286-292 | Pass `blob.account_url` through; schema pattern `^https://[A-Za-z0-9.-]+/?$` | Unchanged |
| `src/Parchwell.Verifier/VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` line 180 | Derives the storage account resource ID from the `BlobServiceUri` host | Unchanged; derived |
| `.github/workflows/sweeper.yml` line 118, `infra/scripts/lifecycle/lifecycle.sh` lines 155-160 | Sweeper runs `terraform destroy` and then requires empty state | Unchanged; `random_string.storage` is in state and is destroyed with it |
| `docs/operations/*.md`, `docs/blueprint.md` | No occurrence of `stparchwell` or of the ephemeral account name | Unchanged |
| `tests/terraform/ephemeral/main.tftest.hcl` (36 occurrences on 30 lines) | Synthetic fixture IDs and the expected `storage_config.name` | Changed (R6, fixture rule) |
| `tests/terraform/persistent/main.tftest.hcl` lines 179-229 | Archive account `stparchwellarcoffline1` | Unchanged; persistent layer |
| Python and .NET test fixtures (`synthetic`, `source123`, `archive123` hosts) | Synthetic account hosts, none uses the fixed pattern | Unchanged |

## 3. External contracts

- Storage account name (Azure resource name, read by Azure and by every consumer above): exactly `stparchwell` + `var.naming_suffix` (8 characters, `^[a-z0-9]{8}$`) + `random_string.storage.result` (5 characters). Total length 24 characters, computed by script (`len('stparchwell')` = 11, `len('stparchwelloffline1x7k2q')` = 24). Character set lowercase letters and digits only.
- `random_string.storage` configuration, exactly: `length = 5`, `lower = true`, `upper = false`, `numeric = true`, `special = false`, no `keepers`, no `min_*` arguments, no `lifecycle` block. Resource address `random_string.storage`, in `infra/ephemeral/storage.tf`.
- `local.names.storage` is the only place the name is composed: `"stparchwell${var.naming_suffix}${random_string.storage.result}"` (docs/engineering/terraform.md "Rules": names come from one `locals` naming function).
- Lifetime of the random part: created by the first apply into empty ephemeral state, reused unchanged by any later apply into the same state, deleted by `terraform destroy`. A new random part is drawn only by an apply that follows a destroy. Source: hashicorp/random 3.9.1 `docs/index.md` "Resource Keepers" (below).
- Terraform outputs: unchanged. `blob.account_url` keeps the form `https://<account>.blob.core.windows.net` (from the module's `fqdn.blob`), and `blob.containers` is unchanged.
- Archive layout (issue requirement 3): object paths are `v1/<source commit>/<GitHub run ID>/<run attempt>/<run ID>/<phase>/objects/<container>/<name>` and `.../runner/<name>` (`infra/scripts/phase/archive.py` lines 2170-2171 and 658). Manifest `destination.account_host` is the archive account, not the source (line 2177). Object `source` records hold `container`, `name`, `etag` (lines 655-657) and projection headers hold `container`, `name`, `etag`, `bytes`, `sha256` (line 141). No archive object path, manifest field or receipt includes the ephemeral source account name; the name is used only as the `--account-name` argument of `az storage blob` transport calls (lines 73-77). The archive layout does not change. The pull request states this.
- The archive's guard that the source host differs from the archive host (`archive.py` line 784) cannot be tripped by this change: the source name is 24 characters and the archive name `stparchwellarc<8>` is 22 characters (computed by script).

## 4. Requirements

- R1. WHEN the ephemeral root is planned, THEN `local.names.storage` equals `"stparchwell${var.naming_suffix}${random_string.storage.result}"` and `module.storage` receives it as `name` through `local.storage_config.name`.
- R2. WHEN `random_string.storage` is created, THEN its configuration is exactly the one in section 3, so `result` is 5 characters from `[a-z0-9]` and the account name is 24 characters, the Azure maximum.
- R3. WHEN the ephemeral root is planned with `random_string.storage.result` unknown, as on every real first apply, THEN the plan succeeds: no `count`, `for_each` key or variable validation in the root or in `Azure/avm-res-storage-storageaccount/azurerm` 0.10.0 depends on the account name (docs/engineering/terraform.md "Rules", last bullet).
- R4. WHEN `terraform destroy` of the ephemeral root completes, THEN `random_string.storage` is removed from state with the other resources, and the existing empty-state check (`lifecycle.sh` lines 157-161) still passes without change. Failure path: if any resource remains, that check fails as today with `ephemeral state is not empty`.
- R5. WHEN the name changes, THEN the blob private endpoint keeps the name `pe-blob-<naming suffix>`, the same subnet, the same `privatelink.blob.core.windows.net` zone group and `private_endpoints_manage_dns_zone_group = true`; every storage role assignment keeps its principal, role and container scope expressed through `module.storage.containers.<key>.id`; `public_network_access_enabled = false`, `shared_access_key_enabled = false`, `default_to_oauth_authentication = true` and every other `local.storage_config` field are unchanged except `name`.
- R6. WHEN the phase scripts read `ephemeral-output.json` whose `blob.account_url` host is a 24-character account name of the new form, THEN `check-dns` resolves that host and exits 0, and the consumer readiness poll passes that account name to `az storage blob list --account-name`. Failure path: WHEN the host's account part is 25 characters, THEN `check-dns` exits 2 with `invalid blob host` on standard error before any `resolve` command runs.
- R7. WHEN the pull request is opened, THEN its description lists every place in the section 2 inventory with what was done there, states that the archive object paths, manifest and receipts do not include the source account name, and states the Microsoft documentation result of section 9 ("Sources", name reuse).

## 5. Invariants and safety rules

- No change to identity scope, network exposure or private endpoint configuration (AGENTS.md "Code Review Rules", P0 rule). R5 and test T2 pin this.
- No new project, assembly reference, NuGet package or Terraform provider. `hashicorp/random` 3.9.1 is already required and locked.
- AGENTS.md hard safety rules 1 to 3: the implementing session runs no `terraform apply`, `destroy` or Azure-backed `plan`, and holds no Azure credentials. Every check in this plan is offline.
- AGENTS.md hard safety rule 7: fixtures stay synthetic (zero GUID subscription, `offline1` suffix).
- AGENTS.md hard safety rule 11: the only numbers are lengths and a probability computed by script (section 8, D1).
- AGENTS.md GOVERNANCE "Verdict integrity": nothing under `results/` and no `expected.yaml` changes.

## 6. Test map

All Terraform tests run in plan mode with the existing mock providers. "Fixture rule" below governs existing tests.

- T1 (R1, R2). File-level block added to `tests/terraform/ephemeral/main.tftest.hcl`:

  ```hcl
  override_resource {
    target          = random_string.storage
    override_during = plan
    values          = { result = "x7k2q" }
  }
  ```

  It is required, not optional: a mocked provider generates "a random 8 character alphanumeric string" for computed strings (Terraform mocking documentation, section 9), which would make the name 27 characters, fail the module's `^[a-z0-9]{3,24}$` validation and fail every run. In `run "foundation"`, the existing assertion `jsonencode(local.storage_config) == jsonencode({...})` changes only its `"name"` value from `"stparchwelloffline1"` to `"stparchwelloffline1x7k2q"` (boundary: exactly 24 characters). Add one assertion to `run "foundation"`:
  `random_string.storage.length == 5 && random_string.storage.lower && !random_string.storage.upper && random_string.storage.numeric && !random_string.storage.special && length(random_string.storage.keepers) == 0`
  with error message `Storage name random part must be 5 lowercase letters or digits without keepers.`. If `keepers` is null rather than empty when unset, the implementer uses `random_string.storage.keepers == null` instead and records the choice in the PR implementation notes.
- T2 (R5). The existing assertions of `run "foundation"` on `local.storage_config` (whole-object equality, including `private_endpoints.blob.name = "pe-blob-offline1"`, its subnet and its DNS zone ID), on `module.storage.resource.body.properties` (shared key, OAuth default, public network access, TLS, HTTPS, public blob access), and on `azurerm_role_assignment.workload`, `.consumer_record` and `.workflow_evidence_reader` scopes stay in force. Their only change is the fixture rename under the fixture rule; every principal, role, container and property value stays literally as it is.
- T3 (R3). New run in `tests/terraform/ephemeral/main.tftest.hcl`:

  ```hcl
  run "storage_name_unknown_at_plan" {
    command = plan
    override_resource {
      target          = random_string.storage
      override_during = apply
      values          = { result = "x7k2q" }
    }
    assert {
      condition     = random_string.storage.length == 5 && !random_string.storage.upper && !random_string.storage.special
      error_message = "The plan must succeed while the storage name is unknown."
    }
  }
  ```

  Expected: the run passes, which shows that a plan with an unknown account name succeeds. The run-level `override_during = apply` takes precedence over the mock provider's `plan` setting (Terraform mocking documentation, "Overrides Syntax"), so the result is unknown during plan. If Terraform 1.16.6 reports the result as known in this run, or rejects the run for a reason other than an assertion, the implementer applies the stop rule (section 11); it does not weaken or delete the run.
- T4 (R6). New method `test_check_dns_takes_the_storage_account_from_outputs_only` in `infra/scripts/phase/tests/test_phase.py`, class `PhaseTests`, using its existing `prepare()`, `ScriptedCommands` and patched `Phase.sleep`. Input: `ephemeral-output.json` equal to the module constant `EPHEMERAL` except `blob.value.account_url`.
  - Subtest at the boundary, `account_url = "https://stparchwelloffline1x7k2q.blob.core.windows.net"` (24-character account): `phase.main(['check-dns'])` returns `0`; the hosts passed as the fourth argument of the `resolve` commands, in order, are exactly `['eh-synthetic.servicebus.windows.net', 'stparchwelloffline1x7k2q.blob.core.windows.net']`.
  - Subtest above the boundary, `account_url = "https://stparchwelloffline1x7k2q9.blob.core.windows.net"` (25 characters): `phase.main(['check-dns'])` returns `2`; standard error contains `invalid blob host`; no `resolve` command was requested; `Phase.sleep` was not called.
- T5 (R6). New method `test_poll_lists_the_account_named_by_the_outputs` in `infra/scripts/phase/tests/test_consumer_repeat.py`, using that file's existing harness and `ready_layout()`. Input: the harness's `ephemeral-output.json` with `blob.value.account_url = "https://stparchwelloffline1x7k2q.blob.core.windows.net"`. Expected: the poll returns `'ready'`, and the recorded `az storage blob list` command contains the adjacent arguments `'--account-name', 'stparchwelloffline1x7k2q'`.

Fixture rule (existing tests). When an existing test's fixture is rejected by a new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Model: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

Applied here: in `tests/terraform/ephemeral/main.tftest.hcl`, every occurrence of `stparchwelloffline1` (36 occurrences on 30 lines on the base commit, counted by script) becomes `stparchwelloffline1x7k2q`, under case (a). The forcing check for the expected `"name"` value is R1; for the mocked account, container and role-scope IDs and the mocked blob endpoint it is consistency with R1, so that the mocked resources describe the account the configuration names. The replacement is mechanical and is verified by script: after it, `grep -c 'stparchwelloffline1x7k2q'` gives 30 lines, and `grep -o 'stparchwelloffline1' | wc -l` still gives 36. No Python or .NET fixture needs a change; if one fails, the same rule applies.

## 7. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

- (a) Steps whose effort scales with a number unknown at planning time: none. The inventory was taken on the base commit and is closed; the fixture rename is a fixed 36 occurrences. The implementing session re-runs the inventory search once on its own base; any new occurrence is added to the PR list and handled under the same requirements, or, if it needs a contract change, under the stop rule.
- (b) Where the plan is stricter than its authority (the issue body): it fixes the random part at exactly 5 characters with no keepers, where the issue says "for example a `random_string`"; it adds T3 (unknown name at plan) and T5 (consumer poll) beyond the issue's "one new test", because the issue's requirement 2 names more than one script consumer and docs/engineering/terraform.md bans `for_each` and `count` keyed on apply-time values; it pins R5's unchanged fields by the existing whole-object assertion.
- (c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request:
  - `Azure/avm-res-storage-storageaccount/azurerm` 0.10.0 (Git tag `v0.10.0`, commit `5695e5ec21744a3765051677d9e82bfba16b5d98`): read `variables.tf` lines 10-18 (name validation), `main.tf` line 3, `main.privateendpoint.tf` lines 1-28 and every `for_each`/`count` in `main.tf`, `main.containers.tf`, `main.privateendpoint.tf`, `main.role_assignments.tf`. The name is used only as `name`, in the lock name `lock-${var.name}` (no lock is configured here) and as the private endpoint fallback name `pe-${var.name}` (not used: `storage.tf` line 42 sets the endpoint name). No `for_each` or `count` depends on it. T3 checks this offline.
  - hashicorp/random 3.9.1 (tag `v3.9.1`, commit `b59b1791a688fe2737b5b70ab05fdd0a4190190e`): argument names and defaults, and the lifetime rule. Checked against its source docs (section 9). T1 checks the configuration.
  - Terraform 1.16.6 test mocking: generated strings and override precedence, checked against the Terraform documentation (section 9). T1 and T3 depend on it; if behaviour differs, the stop rule applies.
  - Azure behaviour on a new, unused name: no offline test covers it. Listed in section 8 as D1 and D2.

## 8. Deployment assumptions

None of these has live evidence. No live run was taken for this plan, so no evidence was taken on an open or a fresh connection, idle or under load (plan rule 3). Each is listed under "Open questions for Hari" until he accepts it by name.

- D1. A 24-character name `stparchwell<suffix><5 random>` is free in Azure's global namespace at apply time. Evidence: documentation only (storage account names are globally unique, 3 to 24 lowercase letters and digits). The chance that one draw repeats the previous phase's random part is 1 in 60,466,176 (36 to the power 5, computed by script). A collision with an account outside this repository is not measured; Azure would reject the apply with `StorageAccountAlreadyTaken` (Microsoft troubleshooting page, section 9), which fails the phase loudly, not silently.
- D2. A storage account under a name that was never deleted does not show the two failures in the issue (container creation `RESPONSE 404` in run 44488066541, and 24 minutes of failed first writes to `consumer-record` in run 44461695288). Evidence: the issue's inference from three C03 runs on 2026-11-19, two failing in phase C1 and one passing; no Microsoft documentation found states that a reused name carries stale state (section 9). This change removes name reuse whether or not the inference is right; it cannot be shown to remove the failures until a live run uses it.

## 9. Sources

Documentation tools used: the Microsoft Learn MCP server (`microsoft_docs_search`), Git clones of the pinned module and provider at their tags, and an HTTPS fetch of the Terraform documentation page. Read 2026-11-19.

- Storage account name rules: [Naming rules and restrictions for Azure resources, Microsoft.Storage](https://learn.microsoft.com/azure/azure-resource-manager/management/resource-name-rules#microsoftstorage), "storageAccounts | global | 3-24 | Lowercase letters and numbers". Same rule in [Storage account overview, Storage account name](https://learn.microsoft.com/azure/storage/common/storage-account-overview#storage-account-name). Confirmed.
- Name collision error: [Resolve errors for storage account names](https://learn.microsoft.com/azure/azure-resource-manager/troubleshooting/error-storage-account-name), `Code=StorageAccountAlreadyTaken`. Confirmed.
- Name reuse after deletion (issue requirement: look for it and cite it, or say none was found): [Recover a deleted storage account](https://learn.microsoft.com/azure/storage/common/storage-account-recover) states that a deleted account can be recovered within 14 days only if "a new storage account with the same name has not been created since the original account was deleted", and that "when a storage account is deleted, any linked private endpoints are also removed". It does not say that a recreated account under the same name has stale resource provider, data plane or DNS state. No Microsoft documentation page stating such stale state was found. Microsoft Q&A answers returned by the search describe name holds after deletion; they are community answers, not documentation, and are not relied on. The cause in D2 stays inferred.
- hashicorp/random 3.9.1, `docs/resources/string.md` (commit `b59b1791a688fe2737b5b70ab05fdd0a4190190e`): `length` required; `lower`, `upper`, `numeric`, `special` default `true`; `keepers` optional; `result` read-only. `docs/index.md` "Resource Keepers": "the random resources generate randomness only when they are created; the results produced are stored in the Terraform state and re-used until the inputs change". Confirmed.
- Terraform test mocking, [developer.hashicorp.com/terraform/language/tests/mocking](https://developer.hashicorp.com/terraform/language/tests/mocking): "Strings will be a random 8 character alphanumeric string"; `override_resource` may be placed at file level and in `run` blocks; "The override_during attribute is also optional, and if specified it will override the override_during attribute in the mock_provider block". Confirmed. The page is not versioned; that it describes 1.16.6 behaviour exactly is inferred, and T3's stop condition covers a difference.
- `Azure/avm-res-storage-storageaccount/azurerm` 0.10.0 source, commit `5695e5ec21744a3765051677d9e82bfba16b5d98`, files listed in section 7 (c).
- Repository files on base commit `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`: every path and line cited in sections 2 and 3; docs/engineering/terraform.md "Rules" and "Checks"; docs/operations/terraform.md lines 134-156 (offline commands).

## 10. Acceptance commands

Run from the repository root with the pinned tools (`infra/scripts/lifecycle/install-tools.sh`, as the `Terraform verification` job does). Evidence for each is its exit status in the PR's self-review record, and the hosted run on the final head.

1. `terraform fmt -check -recursive infra tests/terraform`: exit 0.
2. `terraform -chdir=infra/ephemeral init -backend=false -input=false` then `terraform -chdir=infra/ephemeral validate -no-color`: exit 0.
3. `tflint --init --config="$PWD/.tflint.hcl"` then `tflint --chdir=infra/ephemeral --config="$PWD/.tflint.hcl"`: exit 0, no findings.
4. `terraform -chdir=infra/ephemeral test -test-directory=tests -no-color`: exit 0; output lists `run "storage_name_unknown_at_plan"` as passed and every previously existing run as passed, with no skipped run.
5. `bash infra/scripts/lifecycle/check-offline.sh --terraform-test-paths`: exit 0.
6. `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: exit 0, including the two new methods of T4 and T5 (the phase suite is reached through `infra/scripts/tests/test_phase_bridge.py`).
7. `npm run check:docs` and `git diff --check`: exit 0; warnings handled under docs/agents/pull-requests.md rule 8.
8. Script checks of section 6: occurrence counts of the fixture rename.
9. The hosted `.NET verification` workflow's `Terraform verification` and lifecycle jobs on the pull request's final head: success.

docs/agents/pull-requests.md "Verification gates" steps 1 and 3 apply, because the pull request changes a file under `tests/` (`tests/terraform/ephemeral/main.tftest.hcl`): commands 1 to 8 are the focused tests of step 1, and step 3 runs `./scripts/verify-dotnet.sh --tier sandbox` once on the source the cold review cleared, recorded as a partial run that excludes the `Tier=CGroupV2` tests.

## 11. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract (section 3), a requirement (section 4), a test's inputs or literal expected values (section 6), or the scope (section 2). This includes: T3 cannot be made to run with an unknown name as written; any consumer of the fixed name is found that needs a change outside the allowed files; evidence is found that the Event Hubs namespace has the same problem (the issue says to record that on the issue and not change it here).

## 12. Deferred work

- First live use of the unique name. Evidence: D1 and D2 above. On the verdict path: yes, the next two-phase scenario dispatch (C03) depends on the ephemeral apply. Deferral reason: a live run is outside an agent pull request (AGENTS.md hard safety rules 2 and 3) and the issue lists "Any rerun" as out of scope. Responsible: the control session of the scenario issue that next dispatches a two-phase run, after this pull request merges, as the next authorized run step (docs/agents/factory.md "Item lifecycle" step 4). Workload bound: one dispatch on `main`; exhaustion action `record the gap and continue`. Completion evidence: the run's job logs show both phase applies succeeding, with the account host differing between phases, and the first consumer writes in phase C1 succeeding without the 24-minute failure window. Gate it blocks: it does not block this merge; it is the live smoke run that docs/agents/factory.md "Item lifecycle" step 1 requires, whose timing is open question Q1.

## 13. Open questions for Hari

- Q1. Live smoke before approval. docs/agents/factory.md "Item lifecycle" step 1 says: "A plan that changes live infrastructure is not approvable until a live smoke run of that design exists. A smoke-only pull request may precede plan approval for this purpose." This plan changes the live ephemeral storage account name, so under that rule it cannot be approved, under standing approval or otherwise, until such a run exists. No run of this design exists. An agent can dispatch only against `main` (AGENTS.md hard safety rule 8), so a smoke run needs the change merged first. Options:
  - (A, recommended) Hari approves this plan and merges its pull request, with the first two-phase dispatch after merge (section 12) named as the live smoke run, accepting by name that the plan is approved before that run. Effect: the earliest path to the next C03 run; risk limited to that run failing at apply, which the job's destroy cleans up (AGENTS.md hard safety rule 6).
  - (B) A smoke-only pull request with the same Terraform change merges first, a smoke dispatch runs on `main` (`cluster-smoke.yml` applies and destroys the ephemeral layer, lines 158 and 199, but once per dispatch, so it does not exercise a destroy followed by a second apply; a two-phase scenario dispatch does), and this plan is approved after it. Effect: the same code reaches `main` before approval, plus one extra run and one extra pull request.
  - (C) Hari rules that the step 1 rule does not apply to a name-only change of an existing resource. This is a reading of a governance rule, which only Hari makes.
- Q2. Accept D1 by name: the 24-character name is assumed free in Azure's global namespace; a collision fails the apply loudly with `StorageAccountAlreadyTaken`.
- Q3. Accept D2 by name: the change is made on an inferred cause (stale state after name reuse) that no Microsoft documentation confirms; whether it removes the two C1 failures is known only after the first live run.

## 14. Size estimate

Estimate, made 2026-11-19 from the base commit by counting the places this plan changes, not measured: 5 files changed plus `items/559/state.md` created; about 75 changed lines (Terraform 2 lines, test fixtures 30 lines, T1 and T3 about 25 lines, T4 and T5 about 35 lines), plus about 15 lines of state file. Size is reported, not enforced.
