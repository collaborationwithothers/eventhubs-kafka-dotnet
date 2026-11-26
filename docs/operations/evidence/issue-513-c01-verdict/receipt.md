# C01 verdict preparation receipt

This receipt describes how the C01 evidence pack in `results/C01/2026-11-23.md` and `.json` was assembled from batch run 45124695288. The proposed verdict is `contradicted`: the outcome that `scenarios/C01/expected.yaml` selects for that run. Hari approves or rejects it and merges.

## Execution card

- Deliverable: five paths (the generator, this receipt, the two results files and README line 30) on branch `claude/513-c01-verdict`, pull request 596, labelled `verdict`.
- Authorization: [plan revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7127463739) (`items/513/plan-c01-verdict.md` at `dd29416a68006e53047b39da51f4b2a6db7b1f5b` on `claude/513-plan`), approved by [approval 7168831391](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7168831391) after [decision 7168528766](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7168528766) ("revert"), which supersedes decisions 7128051335 and 7128617765. The pull request does not edit `scenarios/C01/expected.yaml` or the test file that pins its hash; the plain-words points of [decision 7126257034](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/513#issuecomment-7126257034) are checked by the generator as required report text.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G10; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review and Codex security review in the form hard safety rule 10 allows.

## Inputs

Downloaded on 2026-11-25 (the revision 3 session) with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` and `.../actions/jobs/<id>/logs` into a scratch directory outside the repository. `sha256sum` of each file equalled the plan's section 4.1, and `make_results.py` pins the same values (check G1). The mutation ZIP's hash equals the artifact API `digest`; the generator holds it, and the summary's, as constants.

| File | Source | SHA-256 |
|---|---|---|
| `c01-C0-candidate.zip` | artifact 14867803464 | `bd388eac5dc682ca9f3d0a73d94847192aff6cbfef6d5cc120c70b84aac15fb3` |
| `c01-C1-candidate.zip` | artifact 14895402374 | `52105da54df8e4e95415c02eaf925163980e70479d5d35eaf48c4daff5fbb596` |
| `c01-C0-archive.zip` | artifact 14870530520 | `be25c6ac639f362a5716575f9becab797b66ff927fba25ad15354aedf6b77772` |
| `c01-C1-archive.zip` | artifact 14896405743 | `dac66b1feb1f5107e29dd2aec2e681db11b8869f85854e49c5fe040fd233a6ac` |
| `c01-C0-pods.zip` | artifact 14868618067 | `e49f1a3f2088318fe1ea88acd70f38ac46fce463c335a1b5c9c2dbe2fefd7ad5` |
| `c01-C1-pods.zip` | artifact 14895726646 | `aff647840c189209f12e6bdd0edfdba00c7a87a18e8da6833ed19ac92b19ba0a` |
| `probe.log` | job 137529776430 (`probe`, C0) | `655d77582f398d448fedd8a33c1e96da87f4e3ccce8c8aa5dbc60482a914fffe` |
| `full-C3.log` | job 138067157139 (`full-C3`, C1) | `ba2481fa746610661a8d94ec2d91a818bd9e39fbf3a2bc1c24af79efd0b92439` |
| `c5-summary.zip` | artifact 14997997843, `c5-summary-45555996117-1` | `764cdd3789329d07553303cd3b2508e640426499c6aaeee2f8224f01db04aae3` |

The eight pinned run members (the two `candidate.json`, the two archive receipts and manifests, the two `phase-status.json`) and the mutation `summary.json` (SHA-256 `bb52a814b734277bd088a8b3745db6041c2bf5bc58ac57fb240588068667282d`) are listed with their hashes in the generator's `MEMBER_PINS` and in the results JSON `inputs`. `scenarios/C01/expected.yaml` is unchanged by this pull request; its SHA-256 `7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb` is the one the run was judged against, and the generator pins it.

## Mutation acceptance

The plan's section 4.9 recorded that no `verifier-c5-mutation.yml` run covered the run head `2d370de00059df30506154c1fd504d0f81d237bc`, because mutation inputs changed after the newest passing run of that time (run 45028998990, head `9c7fd04eaf015fa0896fb89637fa99acec6ce051`). That finding is provenance only; it states no failure cause.

The accepted run is [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117), read on 2026-11-25: `event` `workflow_dispatch`, `head_branch` `main`, `head_sha` `e3c8eb9462a6ecae139a651c5eef39812be07801`, `status` `completed`, `conclusion` `success`, attempt 1. Its `summary.json` reports `measurement_complete` true, `break_passed` true, `incomplete_reasons` `[]` and `commit` `e3c8eb9462a6ecae139a651c5eef39812be07801`.

- `git merge-base --is-ancestor 2d370de00059df30506154c1fd504d0f81d237bc e3c8eb9462a6ecae139a651c5eef39812be07801`: exit 0.
- `git merge-base --is-ancestor e3c8eb9462a6ecae139a651c5eef39812be07801 origin/main`: exit 0 (`origin/main` was that commit).
- `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/verifier-c5-mutation.yml/runs?per_page=5` lists run 45555996117 first, `completed` and `success`; the four runs before it are `failure`.

## Generator

`make_results.py` runs G1 (every input and member hash, the mutation summary included, `expected.yaml` and `scenario.yaml` in the working tree), then G2 (both candidates' identity, source commit, statuses, reasons and repeats 1 to 5, equal versions), G3 (every rule of `expected.yaml` recomputed from the candidates, the verdict, and the five preconditions of the published sentences), G4 (root counts equal the sum over repeats), G5 (archive receipts), G6 (phase-status files, 31 mode entries each) and G10 (the mutation summary: run 45555996117, commit `e3c8eb94`, `measurement_complete` and `break_passed` true). The shape of each pods-artifact Job folder is also a `FAIL G1`, raised while the inputs are parsed, so after G1 and before G2. It then renders the report and checks G8 (ASCII) and G9 (the plain words come first, with no verifier term before them, every pinned paragraph and sentence present, the inference label in paragraphs 2, 6 and 7, and the README row phrases). With `--check`, G7 requires the committed report, JSON and README line 30 to equal the re-render.

`--self-test` runs the real inputs and then 36 single-change cases, each of which must fail at its named check; the G3 cases also compare the rule name or precondition number.

## Implementer choices

- Running the generator without a mode flag also replaces README line 30, which must be the existing C01 row; it prints the row as well. `--check` requires line 30 to equal it.
- `--self-test` runs the real inputs without G7 (plan R12). Only `report_edit` runs G7 and reads the committed files.
- G9 compares the rendered report with the pinned texts, filled from the data. The texts were copied from the plan's section 4.4 by script, with the plan's `{...}` fields as format fields.
- Precondition 2 reads `requested_partitions` as exactly the list 0 to 31, which every repeat of both configurations holds.
- The mutation ZIP file name in the inputs directory is `c5-summary.zip`.
- `make_results.py` has 1144 lines, more than the plan's estimate of about 800. The published text and the table-driven self-test account for the difference. The plan says not to stop for this.
- Revision 4 supersedes the revision 3 build of this pull request. Its commits that edited `scenarios/C01/expected.yaml` and `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs` are reverted by a new commit that restores both files from `origin/main` (acceptance step 9), and check G11 with its two cases is removed. The "Commit evidence" sentence and the plain apostrophe in the Sources bullet come from the `apply in implementation` findings of the plan checks.

## Source checks made in this session (2026-11-25)

- Before the revision 4 edits: `origin/main` is `e3c8eb9462a6ecae139a651c5eef39812be07801`, the revision 4 base, so the diff of the plan's files and contracts between the plan base and `main` is empty. Authority was refreshed from the issue's comments: decision 7168528766, plan check 7168805452 (`Blocking findings: none`) and approval 7168831391, posted after the plan comment's last edit and naming `dd29416a68006e53047b39da51f4b2a6db7b1f5b`.
- Pull requests 568, 572 and 573, the predecessors of #510, are merged (2026-11-21).
- `sha256sum` of `scenarios/C01/expected.yaml` (`7fa82b42f1d09d2ac9e2fa6c204dbdce18b80e8813bcff89c0a6f5249c6dd2fb`) and `scenarios/C01/scenario.yaml` (`555cb0be7f37c6e5d6a6e94f899875f0616fcafca49be34fab4ad70fda59efd2`) in the working tree equals the plan.
- The Terraform plan lines the report cites: `probe.log` lines 1590, 1599 and 1605 read `capacity = 1`, `location = "uksouth"` and `sku = "Premium"` (colour escapes stripped); `full-C3.log` lines 1396, 1405 and 1411 read the same.
- Mutation acceptance: see above. Run 45555996117 was still the newest completed `verifier-c5-mutation.yml` run on `main` when this session checked on 2026-11-25.
- librdkafka and Microsoft claims rest on plan section 4.6, which the plan checks verified against the pinned sources. This session did not repeat that read.

## Commands and results

Run from the repository root on the branch, with the inputs in a scratch directory outside the repository.

- `make_results.py --inputs "$IN"`: exit 0; wrote the two results files and README line 30; printed the row.
- `--check`: exit 0, `PASS check`.
- `--self-test`: exit 0; `PASS real_inputs` and 36 `PASS` lines, from `PASS hash_c0_candidate` to `PASS report_edit`.
- `python3 -m json.tool results/C01/2026-11-23.json`: exit 0.
- `git diff --name-only origin/main` lists exactly the five paths of the deliverable.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari approves or rejects the verdict and merges. The pull request touches `/results/` and `/*.md`, protected paths. It changes no expected outcome and no test.
