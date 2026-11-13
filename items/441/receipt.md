# Receipt: #441 dotnet-matrix

## Sources
- Plan: `items/441/plan-dotnet-matrix.md` r2 at commit bdc098b966759472725a0d7275dbdf2545f9508c; plan comment https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6851421057.
- Approval: standing approval; plan check https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6852015890 (`Blocking findings: none`, fresh-context subagent).
- Decision applied: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/441#issuecomment-6851788370 (no sharding).
- Base: `main` at 259a31fca2f53842385d820b5dba9f213d9e787e.

## Execution card
- Deliverable: workflow job split, `scripts/check-dotnet-matrix.py` and tests, hash update, two doc updates.
- Authorization: the approved plan above. The workflow path is protected; Hari merges.
- Mandatory gates: plan acceptance commands A1 to A6; cold review; Codex review of the head.
- Earliest authorized publication checkpoint: after a cold review round with no unanswered P0 or P1 and the local gates in Commands run; the hosted run (A5) needs a pushed head, so the pull request is opened at that point.
- Review budget: two comprehensive passes, then two focused rounds (rule 8). Exhaustion: hand off to Hari.
- Hosted-run cap (plan section 10a): three failing A5 runs caused by this change, then push -wip and hand off.

## Models
Plan drafting and plan check: Opus (subagents). Implementation: Sonnet 5.5.

## Test map
The plan's section 6 (T1 to T24) is the map; each test is implemented in `scripts/tests/test_check_dotnet_matrix.py` under the same number.

## Choices left to the implementer (plan check findings marked apply in implementation)
1. Checker: a missing `summary.json` yields only the `missing-file` finding (no R8 or R9 finding); a missing `unit-projects.txt` yields only `missing-file` (no inventory finding). This is what T10 and T11 require.
2. `docs/engineering/dotnet-verification.md` "Hosted CI and completion" lists the upload-artifact v4.6.2 pin as well as the download-artifact pin. Tags checked with `git ls-remote` on 2026-11-13; the release pages were not read.
3. Step 7 `run` block text (plan 3.1 leaves it to the implementer): uses `if` statements, not `[ ] && cp`, so the step's `-e` shell does not fail on an absent optional file.
4. The checker reports `missing-file` for `results.xml` and the other three files in the order summary.json, stage-timing.tsv, unit-projects.txt, results.xml (plan R7 order).

## Correction to the approved plan (not an amendment)
Plan section 9's estimate says the new workflow uses eight runners at once instead of three. Counted by script from the head workflow (jobs without `needs`, matrix legs expanded): nine jobs start at once (six test legs, worker image smoke, lifecycle verification, Terraform verification), against three today. The plan file is not edited, because that would be an amendment. Nothing in the plan relies on the figure.

## Fixture changes forced by new checks (standing fixture rule)
- `infra/scripts/phase/tests/test_workflow.py`: the SHA-256 for `dotnet-verification.yml` takes the new file's hash (rule (a)); the assertion is unchanged. The comment line gains the issue reference (R12).

## Commands run (head before the receipt commit: see git log)
- A1: `python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py -v`: 24 tests, OK.
- A2: `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`: 380 tests, 43 failures and 103 errors in this container, in the same 149 test IDs before and after the change (compared by script on `main` and the branch). The hash test `B10AbruptWorkflowTests` passes (6 tests, OK). Likely cause: this container lacks the pinned tools and CPython 3.12; not investigated.
- A3: `check-offline.sh` not run: `install-tools.sh` stops with "PyYAML wheel requires CPython 3.12". Instead ran pinned actionlint 1.7.12 (sha256 checked) on the workflow: no findings. The hosted Lifecycle verification job runs the full script on the pull request.
- A4: PyYAML comparison of `name`, `on`, `permissions`, `jobs.lifecycle-verification`, `jobs.terraform-verification` between `origin/main` and the working tree: all equal.
- A5: pending, hosted run on the pull request head.
- A6: `npm run check:docs`: 9 warnings, all in files this pull request does not change.
- Local full gate `./scripts/verify-dotnet.sh` not repeated: no .NET source or test changed (plan section 8, rule 13).
