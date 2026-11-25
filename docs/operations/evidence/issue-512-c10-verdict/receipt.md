# C10 verdict preparation receipt

This receipt describes how the C10 evidence pack in `results/C10/2026-11-22.md` and `.json` was assembled from batch run 45074268881. The proposed verdict is `confirmed`: the outcome that `scenarios/C10/expected.yaml` selects for that run. Hari approves or rejects it and merges.

## Execution card

- Deliverable: seven paths (the generator, this receipt, the two results files, README line 39, `scenarios/C10/expected.yaml` and line 148 and the comment line 153 of `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs`) on branch `claude/512-c10-verdict`, as a pull request labelled `verdict`.
- Authorization: [plan revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7126541550) (`items/512/plan-c10-verdict.md` at `8668204b62ae240b95061348d5cf3214056a0654` on `claude/512-plan`), checked by [plan check 7128203308](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7128203308) and approved by [approval 7128240203](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7128240203). The tool is Claude Code under [decision 7127777487](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7127777487) point 4 and [comment 7127777927](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7127777927). [Decision 7128617336](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/512#issuecomment-7128617336) adds the rewrite of the test comment on line 153.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G11; `npm run check:docs`; the verification gates of docs/agents/pull-requests.md; cold review (docs/agents/pull-requests.md rule 8); Codex review and Codex security review in the form hard safety rule 10 allows.

## Inputs

Downloaded on 2026-11-25 with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` and `.../actions/jobs/<id>/logs` into a scratch directory outside the repository. `sha256sum` of each of the nine files the plan pins in section 4.1 equalled the plan's value, and `make_results.py` pins the same values (check G1). The plan checks matched each of those ZIP hashes against the artifact API `digest`. The mutation ZIP is not pinned by the plan: its SHA-256, given below, equals the `digest` that `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/14997997843` returned on 2026-11-25 (`sha256:764cdd3789329d07553303cd3b2508e640426499c6aaeee2f8224f01db04aae3`, `expired` false), and the generator checks it against `--mutation-zip-sha256` (G1).

| File | Source | SHA-256 |
|---|---|---|
| `c10-C0-candidate.zip` | artifact 14776044642 | `14f18dd838c796e5fddb8b96bf15dfaacfb4fb51a893c8e0cec01176e5a80953` |
| `c10-C1-candidate.zip` | artifact 14793443758 | `483c030bcbcc856d1725913a894c834b552217574c787f48564c23a88096beee` |
| `c10-C0-archive.zip` | artifact 14776529837 | `e0c7d4f3a730c78890d7c05457dae561f18ebf455ade07e0fe1fed8abef4cddb` |
| `c10-C1-archive.zip` | artifact 14794880764 | `36962b576f1ab0f3007e24c54785354fa3c0748e2c16f35c9d3c6229a1706b8d` |
| `c10-C0-pods.zip` | artifact 14775989790 | `9de6a66e8972befb2f5669316a7658f1f94a04d323bf984e56849584a81bbe9e` |
| `c10-C1-pods.zip` | artifact 14793209973 | `d22a170953de53161cfc639aa96b08ec6e29541dd804e8d65e69ad9f8750a33d` |
| `probe.log` | job 137367883128 (`probe`, C0) | `c5708404195a7f9b7e07f7fbb555865238b10e486db38dce2ddd0e73bd307069` |
| `full-C3.log` | job 137388417872 (`full-C3`, C1) | `3985285031dcb90ac3bea90fb5e6ed0e973f09d1830c1b9d94ad74d5b723ee82` |
| `c5-summary.zip` | artifact 14997997843 (`c5-summary-45555996117-1`), mutation run 45555996117 | `764cdd3789329d07553303cd3b2508e640426499c6aaeee2f8224f01db04aae3` |
| `expected-judged.yaml` | `git show f23fa035405cb13699235b03e98f058b3cd6cf25:scenarios/C10/expected.yaml` | `21097f98e69c3a8e2f77247c0f5ccd2d0b6bd5ef8b638e8866f2f838f364f34c` |

The eight pinned members of the candidate, archive and pods ZIPs, and the mutation `summary.json` (SHA-256 `bb52a814b734277bd088a8b3745db6041c2bf5bc58ac57fb240588068667282d`), are listed with their hashes in the generator's `MEMBER_PINS` and in the results JSON `inputs`. `scenarios/C10/expected.yaml` after the plan's section 4.10 edit has SHA-256 `261e0a2bc6b439b92bd40cfe4f7e25dc5cfb62bd2999c640950b2d9da4251adb`, and `scenarios/C10/scenario.yaml` has `d72b4ab55fb1c05b7ef3312ced62e2ae95a65903a8c9266bda785fcf5647d290`.

Run 45074268881 as read from the API on 2026-11-25: `workflow_dispatch`, `main`, head `f23fa035405cb13699235b03e98f058b3cd6cf25`, attempt 1, created 2026-11-22T21:49:48Z, conclusion `success`. Jobs `probe` and `full-C3` succeeded; `batch`, `batch-stop` and `full-C0` were skipped.

## Mutation acceptance (plan section 4.9)

- Run [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117): `verifier-c5-mutation.yml`, event `workflow_dispatch`, `head_branch` `main`, attempt 1, conclusion `success`, head `e3c8eb9462a6ecae139a651c5eef39812be07801`, created 2026-11-25T05:47:26Z.
- Its `summary.json`: `commit` `e3c8eb9462a6ecae139a651c5eef39812be07801`, `run_id` "45555996117", `run_attempt` "1", `measurement_complete` true, `break_passed` true. Generator check G10 reads these.
- `git merge-base --is-ancestor f23fa035405cb13699235b03e98f058b3cd6cf25 e3c8eb9462a6ecae139a651c5eef39812be07801` exited 0, and `git merge-base --is-ancestor e3c8eb9462a6ecae139a651c5eef39812be07801 origin/main` exited 0 (`origin/main` was `e3c8eb9462a6ecae139a651c5eef39812be07801`).
- `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/verifier-c5-mutation.yml/runs?per_page=5&branch=main&status=completed` listed 45555996117 first, with `success`; the four runs before it, 45469622107, 45459740602, 45449734959 and 45374369955, concluded `failure`. This receipt states no failure cause.

## Generator

`make_results.py` runs G1 (every input and member hash, both mutation hashes against the arguments, `expected.yaml` and `scenario.yaml` in the working tree, and the shape of each pods-artifact Job folder), G11 (the edited `expected.yaml` equals the judged one in every key except `verdict.text_must_state`, whose first five items are unchanged and whose remaining items are exactly the six of plan section 4.10), G2 (both candidates' identity, source commit, statuses, reasons and repeats 1 to 5, equal versions), G3 (H1, H2 and the verdict recomputed from the candidates, and the preconditions of the published sentences), G4 (root counts equal the sums of the repeats), G5 (archive receipts), G6 (phase-status files), G10 (mutation summary), G8 (ASCII report and README row), G9 (the pinned summary, plain-words paragraphs and their order, the quotations, the H2 line, the `configured_self_kill` sentence, the Not tested lines and the README sentence) and, with `--check`, G7 (byte equality with the committed files). It writes nothing unless every check before G7 passes.

`--self-test` runs the real inputs and then 30 single-change cases, each of which must fail at its named check: `hash_c0_candidate`, `expected_yaml_edit`, `mutation_hash`, `c1_repeat_status`, `versions_differ`, `h1_loss_one`, `h1_precondition_failed`, `h2_loss_one`, `h2_precondition_failed`, `extra_rows_differ`, `processed_again_zero`, `kill_count`, `offset_read_failed`, `subject_exit_zero`, `node_split`, `self_kill_missing`, `root_consumer_sum`, `root_producer_sum`, `archive_gap`, `archive_phase`, `phase_safety_stop`, `subject_mode_complete`, `mutation_incomplete`, `mutation_run_id`, `non_ascii`, `missing_plain_words`, `verifier_term_early`, `expected_rule_changed`, `expected_item_missing` and `report_edit`.

## Changes made at implementation under the approval

- `tests/Parchwell.Scenarios.Tests/KillConsumerProcessingDefinitionTests.cs`, existing test `Given_ApprovedExpectedOutcomes_When_ReadingTheCommittedFile_Then_BytesEqualTheApprovedText`, case `C10`, as decision 7128050969 directs. It is not a fixture change under plan rule 2: no new check rejects the test, and its input and assertions are unchanged. Line 148's expected hash changes from `21097f98...` to `261e0a2b...`, the hash of the `expected.yaml` that the plan's section 4.10 edit produces, with a trailing comment citing decision 7127777487 point 1. The test still reads the committed file and checks the hash byte for byte. No case is removed and no assertion is loosened. Line 149 (`C01`) is not changed. Line 153's comment becomes the text of decision 7128617336. Forced by: the approved `expected.yaml` edit (plan section 4.10; decisions 7127777487 and 7128050969).

## Implementer choices

- Mutation `run_id` comparison (plan check of revision 1, `apply in implementation`): the summary's `run_id` is the JSON string "45555996117"; check G10 compares it with the decimal string of `--mutation-run`, and requires `--mutation-run` and `--mutation-artifact-id` to be decimal IDs.
- Mutation artifact ID (plan check of revision 2, `apply in implementation`): the generator takes `--mutation-artifact-id` and renders the JSON `inputs` source as `artifact <id>, c5-summary-<run_id>-<run_attempt>`, both from the summary.
- Self-test order (plan check of revision 1, `apply in implementation`): the three R1 cases (`hash_c0_candidate`, `expected_yaml_edit`, `mutation_hash`) change their input in memory before G1 and print `PASS <case>` when G1 fails; every other case changes its value after G1. `expected_rule_changed` and `expected_item_missing` change the parsed edited file between G1 and G11.
- Running the generator without a mode flag also replaces README line 39, which must be the existing C10 row; it prints the row as well. `--check` requires line 39 to equal it.
- `--self-test` runs the real inputs without G7 (plan R12). Only `report_edit` runs G7, against copies of the rendered files.
- The generator reads the YAML files with PyYAML `safe_load`; PyYAML 6.0.1 was installed in this session. The plan names PyYAML 6.0.3, which was not installed here.
- The pod table lists the member path of the pod log and `pods.json` of each of the five Jobs of every repeat and of each phase's judge Job. The node table lists the node of each consumer pair.

## Source checks made in this session (2026-11-25)

- Before the first edit: `origin/main` was `e3c8eb9462a6ecae139a651c5eef39812be07801`. `git diff 3e7163dc641e724437025d917032fd40419e6c4d origin/main` over `README.md`, `scenarios/C10`, `scenarios/profiles/c03-consumer.yaml`, the test file, docs/operations/verifier-contract.md, the C03 evidence folder, `src/Parchwell.Verifier/VerifyRun/Consumer`, `BaselineSettingsCatalog.cs`, `src/Parchwell.Enrichment/ConsumerProcessing`, `infra/cluster/aks.tf`, docs/agents/factory.md, docs/agents/pull-requests.md, AGENTS.md and `results` printed nothing, and the diff of docs/blueprint.md had no changed line. Pull requests 568, 572 and 573 are merged.
- The Terraform plan lines the report cites: `probe.log` lines 1582, 1567 and 1576 read `sku = "Premium"`, `capacity = 1` and `location = "uksouth"` (colour escapes stripped); `full-C3.log` lines 1409, 1394 and 1403 read the same. They are plan lines, not apply results.
- The librdkafka and Confluent.Kafka claims rest on plan section 4.6, which the plan checks verified against the pinned v2.16.0 sources. This session did not repeat that read.

## Commands and results

Run from the repository root on the branch, with the inputs in a scratch directory outside the repository.

- `make_results.py --inputs "$IN" --mutation-run 45555996117 --mutation-artifact-id 14997997843 --mutation-zip-sha256 764cdd3789329d07553303cd3b2508e640426499c6aaeee2f8224f01db04aae3 --mutation-summary-sha256 bb52a814b734277bd088a8b3745db6041c2bf5bc58ac57fb240588068667282d`: exit 0; wrote the two results files and README line 39; printed the row.
- The same command with `--check`: exit 0, `PASS check`.
- The same command with `--self-test`: exit 0; `PASS real_inputs` and 30 `PASS` lines, from `PASS hash_c0_candidate` to `PASS report_edit`.
- `python3 -m json.tool results/C10/2026-11-22.json`: exit 0.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari approves or rejects the verdict and merges. The pull request touches `/results/`, `/*.md` and `scenarios/C10/expected.yaml`, protected paths.
