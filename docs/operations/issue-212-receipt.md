# Probe batch path smoke validation

S1 adds the shared probe-batch smoke entry, chart paths and scoped workload wiring. The offline checks passed for source commit `8958b3cb9ab95d46d3e8db72965b5a8141bdfa3a`; the live smoke awaits Hari's protected merge and persistent apply.

The [approved child plan, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/212#issuecomment-6423432624), updated 2026-10-26T17:59:21Z, has an [independent standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/212#issuecomment-6423579641) with no blocking findings. Its exact P2 replacement for the dispatch-options equality was applied in `WorkflowTests.test_mirror_and_dispatch_contract`. The source base is main `cb83254f1c2645acd86fe734b24e542e0956edd3`.

## Commands and observed results

Validation used the pinned CI installation from `bash infra/scripts/lifecycle/install-tools.sh /workspace/scratch/issue212/tools ci`, with its directory and `venv/bin` in PATH, `PARCHWELL_TOOLS_DIRECTORY` set to that directory, and `PYTHONDONTWRITEBYTECODE=1`. The commands below exited zero on 2026-10-26.

| Command | Observed result |
| --- | --- |
| `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | 95 tests passed, including the phase/workflow bridge |
| `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'` | 52 tests passed |
| `bash charts/parchwell/tests/check.sh` | Legacy 25 positive, 32 negative and 4 worker-setting regressions; new 33 positive and 8 negative cases passed |
| `bash infra/scripts/lifecycle/check-offline.sh` | Workflow structure, mirrors, shellcheck, actionlint, Helm and strict Kubernetes schema checks passed, including both chart suites |
| `terraform fmt -check -recursive infra tests/terraform` | Formatting passed |
| `bash infra/scripts/lifecycle/check-offline.sh --terraform-test-paths` | All root-local test symlinks passed |
| `cmp .github/workflows/scenario.yml docs/operations/workflows/scenario.yml.txt` | Copies were identical |
| `npm run check:docs` | Completed; three warnings in unchanged historical evidence files |
| `git diff --check` | Passed |

For each root in `persistent`, `cluster` and `ephemeral`, these commands passed, using the repository's existing 600-second command wrapper:

```sh
python3 infra/scripts/lifecycle/command.py 600 -- terraform -chdir=infra/ROOT init -backend=false -input=false -lockfile=readonly
python3 infra/scripts/lifecycle/command.py 600 -- terraform -chdir=infra/ROOT validate
python3 infra/scripts/lifecycle/command.py 600 -- terraform -chdir=infra/ROOT test -test-directory=tests -no-color
```

`ROOT` denotes each of the three literal root names above. Mock tests reported persistent 10 passed, cluster 13 passed and ephemeral 10 passed, with zero failures. The existing fixture assertions were extended with exact approved sets; no previous test case or assertion was removed. Provider/module deprecation warnings remain in the pinned dependencies.

The planned lint loop also passed:

```sh
repository="$PWD"
for root in persistent ephemeral cluster; do
  python3 infra/scripts/lifecycle/command.py 600 -- tflint --chdir="infra/$root" --config="$repository/.tflint.hcl" --init
  python3 infra/scripts/lifecycle/command.py 600 -- tflint --chdir="infra/$root" --config="$repository/.tflint.hcl"
done
```

The documentation warnings refer to broken links in unchanged issue-124/issue-140 evidence. They are left under AGENTS.md "Before opening a pull request" rule 8's blanket disposition. No .NET source, package or worker option contract changed; the SDK is unavailable locally, and required CI supplies the normal .NET checks.

## Scope and review

The action and seed paths use the existing LoadGen image and the judge path uses the existing Verifier image. Every new path passes only `--image-smoke`. The fixed matrix and separate enrichment-account seed produce 33 smoke Job paths; the chart and phase tests independently check that count and the names, roles and images. A01/B04 retain their legacy account projection and execution bounds.

New grants follow the [adopted child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/212#issuecomment-6423299188). Exact Terraform mock assertions cover the probe identity/federation, component-scoped telemetry grant, `single-action` container and three scoped data grants. These assertions do not establish live RBAC behavior. Runtime evidence uses the existing lifecycle collector's pod/log/status file shapes and the existing public pod artifact policy in [a01-workflow.md](a01-workflow.md#artifacts).

The cold review applies AGENTS.md, the blueprint ADRs and the Terraform engineering rules to the full implementation and this authored receipt. Cold-review findings and dispositions, final-head CI and the last review round are recorded in the pull request self-review record. Models: Codex authoring session and implementation subagents; subagents used high effort. The underlying inherited model identifier is not exposed to this session.

## Live continuation

No live dispatch or scientific verdict was produced. Hari must merge the protected workflow/identity changes, apply `infra/persistent`, and refresh `PARCHWELL_PERSISTENT_OUTPUTS` with the probe identity. Then the authorized main-branch dispatch uses `scenario=probe-batch` and `run_kind=smoke`, within the plan's existing attempt cap. The actual batch selection rejects before Azure login until S2 supplies the client actions and judgement.

Record the live run, immutable image digests, all pod outcomes and artifact links, ephemeral destroy, cluster stop and measured timings on issue #212. The image smoke establishes only the wiring and image startup it exercises; token acquisition, data permissions, telemetry ingestion, client outcomes and verifier judgement remain unmeasured here. [AGENTS.md](../../AGENTS.md) and the [factory lifecycle](../agents/factory.md#item-lifecycle) own the merge, execution and continuation rules.
