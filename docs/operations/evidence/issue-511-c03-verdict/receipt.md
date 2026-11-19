# C03 verdict preparation receipt

This receipt describes how the C03 evidence pack in `results/C03/2026-11-19.md` and `.json` was assembled from full run 44562323424. The proposed verdict is `confirmed`: the outcome that `scenarios/C03/expected.yaml` selects for that run. Hari approves or rejects it and merges.

## Execution card

- Deliverable: six paths (the generator, this receipt, the two results files, README line 32 and `items/511/state.md`) on branch `claude/511-c03-verdict`, as a pull request labelled `verdict`.
- Authorization: [plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022218042) (`items/511/plan-c03-verdict.md` at `c2dcc47de88cf9b3b177d91df956e071fd2e9d40` on `claude/511-plan`), approved by [approval 7022931112](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022931112) with its six edits, with [plan check 7022568885](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022568885). The tool is Claude Code under [comment 7022347026](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022347026).
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; generator checks G1 to G10; `npm run check:docs`; cold review (docs/agents/pull-requests.md rule 8); Codex review and Codex security review in the form hard safety rule 10 allows.
- No `scripts/verify-dotnet.sh` run: the pull request changes no file under `src/` or `tests/` (docs/agents/pull-requests.md "Verification gates").

## Inputs

Downloaded on 2026-11-19 with `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/<id>/zip` and `.../actions/jobs/<id>/logs` into a scratch directory outside the repository. `sha256sum` of each file equalled the plan's section 4.1, and `make_results.py` pins the same values (check G1).

| File | Source | SHA-256 |
|---|---|---|
| `c03-C0-candidate.zip` | artifact 14557021737 | `f264f1a0077ccc97650163db6a9809a0442d1d3936185f5016f540c7ed7de11b` |
| `c03-C1-candidate.zip` | artifact 14566219424 | `b104889647747d834724353e95db86c5f26f90086d5c59c2969f5cb9a8891c53` |
| `c03-C0-archive.zip` | artifact 14558051290 | `ecb35b70d1c0761ff312af7f76abdebcd00617e41eba17a72be5917d8a06b15c` |
| `c03-C1-archive.zip` | artifact 14568811998 | `507565ac9b104a296810822685fe39a77ad43a6f258eca50d7ad381b437cd394` |
| `c03-C0-pods.zip` | artifact 14556692046 | `5680e14ded810a42610a80d06768f4ffb4fdd0ea3c6c4d4bf98989a9e6b205d6` |
| `c03-C1-pods.zip` | artifact 14567423531 | `2e92c81db1194c38eb799bf896305ebf4e60acaef358cf38e4ed33d3fe726e13` |
| `c5-summary.zip` | artifact 14477182821 | `bc72e78858246b9e560587fcf191c067cbf0746d5b09c8b0e8dda4dda56c03f7` |
| `probe.log` | job 135694703105 (`probe`, C0) | `8aed0608dc5d8381ba8a933a1d793375f663ce412bc546e884643f596852d84e` |
| `full-C3.log` | job 135754218668 (`full-C3`, C1) | `357f8f9dd65d45f98a340c7c99b57112528aefa0e305b251349bbf85239e593c` |

The nine pinned members (the two `candidate.json`, the two archive receipts and manifests, the two `phase-status.json` and `summary.json`) are listed with their hashes in the generator's `MEMBER_PINS` and in the results JSON `inputs`.

## Earlier full runs, provenance only

No data from these runs is used or published. Each fact below comes from `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/<id>` and `.../jobs`, read on 2026-11-19. This receipt states no failure cause, because none was read from a source.

- Run 44461695288 (head `b4cf81945a7fd2130825cb59d10509187cddfeca`, created 2026-11-19T04:56:17Z, conclusion `failure`, job `full-C3` failed). Nothing from this run is published; the published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").
- Run 44488066541 (head `46295ce81471bb20de3c9975bbe6e342efebc46e`, created 2026-11-19T08:29:43Z, conclusion `failure`, job `full-C3` failed). Nothing from this run is published; the published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").
- Run 44248514485 (head `730581540a7225a3a27b27a626f03fe2ea78de83`, the merge commit of pull request 545, created 2026-11-18T06:03:20Z, conclusion `success`, jobs `probe` and `full-C3` succeeded). It ran before pull request 547 (merge commit `e85e651af7e291ccdab240c46cdb89edc7ba4c63`) added the `membership` observation that H1 to H3 read. Nothing from this run is published; the published result shows the final run only (AGENTS.md GOVERNANCE "Verdict integrity").

## Generator

`make_results.py` runs G1 (every input and member hash, `expected.yaml` at the base commit and in the working tree, `scenario.yaml` at the base commit, and the shape of each pods-artifact Job folder), G2 (both candidates' identity, source commit, statuses, reasons and repeats 1 to 5, equal versions), G3 (every rule of `expected.yaml` recomputed from the candidates, the verdict, and the preconditions of the published sentences), G4 (root counts equal the sums of the repeats), G5 (archive receipts), G6 (phase-status files), G10 (mutation summary), G8 (ASCII and no leftover token), G9 (pinned title, summary, plain-words paragraph, H3 line, K-47 reason and the README sentence) and, with `--check`, G7 (byte equality with the committed files). It writes nothing unless every check passes.

The takeover range in the summary, the H1 line and the README row is rendered from the C0 `witness_takeover_microseconds` values (rounded down to two decimals). The pinned summary of G9 holds the real-data range, so a changed takeover time fails G9 after G3.

`--self-test` runs the real inputs and then 23 single-change cases, each of which must fail at its named check: `hash_c0_candidate`, `expected_yaml_edit`, `c0_repeat_status`, `versions_differ`, `h1_takeover_at_threshold` (G9), `h1_takeover_below`, `h1_no_lost`, `h1_no_reprocess`, `h3_commit_after_lost`, `h2_lost`, `h2_witness_assigned`, `loss_nonzero`, `slow_rows`, `node_split`, `root_consumer_sum`, `root_producer_sum`, `archive_gap`, `archive_phase`, `phase_safety_stop`, `mutation_incomplete`, `non_ascii`, `missing_plain_words` and `report_edit`.

## Changes made at implementation under the approval

[Approval 7022931112](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/511#issuecomment-7022931112) adopts these edits, applied with no new plan revision:

1. H2 line: "the witness received no partition assignment after its initial one" in place of "the witness was assigned nothing" (`witness_assigned_rows` excludes the witness's initial assignment, docs/operations/verifier-contract.md line 128).
2. Summary: ", and the slow message was processed a second time." in place of ", and processed that message a second time." (no metric says which member processed the slow message the second time).
3. README row: ", and the slow message was processed again." in place of ", and processed that message again."
4. Edits 4 to 6 of the approval fix the plan text only (section 14's tool sentence, section 1's finding 1 bullet, and the backtick note for the pinned texts). They change no published file. The tool is determined by the `Tool: Claude` comment 7022347026, and the generator holds the pinned texts with plain backticks.

## Implementer choices

- Running the generator without a mode flag also replaces README line 32, which must be the existing C03 row; it prints the row as well. `--check` requires line 32 to equal it.
- `--self-test` runs the real inputs without G7 (plan R12). Only `report_edit` runs G7 and reads the committed files.
- G9 pins the real-data takeover range inside the summary; the rendered summary takes its range from the data. This is how the plan's section 4.4 sentence and the `h1_takeover_at_threshold` row fit together.
- The pod table lists the member path of the pod log and `pods.json` of each of the five Jobs of every repeat and of each phase's judge Job. The node table lists the node of each consumer pair.

## Source checks made in this session (2026-11-19)

- Before the first edit: `origin/main` equalled the plan's base `a1cf429dc7a57827c2a3d5bb658ae290f5ea9145`, so the diff of the plan's changed files and external contracts between the base and current `main` is empty. Pull requests 545 and 547 are merged (merge commits `730581540a7225a3a27b27a626f03fe2ea78de83` and `e85e651af7e291ccdab240c46cdb89edc7ba4c63`).
- `sha256sum` of `scenarios/C03/expected.yaml`, `scenarios/C03/scenario.yaml` (also pinned by G1) and `scenarios/profiles/c03-consumer.yaml` equals section 2 of the plan.
- The Terraform plan lines the report cites: `probe.log` lines 1547, 1556 and 1562 read `capacity = 1`, `location = "uksouth"` and `sku = "Premium"` (colour escapes stripped); `full-C3.log` lines 1382, 1391 and 1397 read the same.
- `git merge-base --is-ancestor e85e651af7e291ccdab240c46cdb89edc7ba4c63 a1cf429dc7a57827c2a3d5bb658ae290f5ea9145` exits 0, and `c5_runtime.py check --base e85e651af7e291ccdab240c46cdb89edc7ba4c63 --head a1cf429dc7a57827c2a3d5bb658ae290f5ea9145 --qualification <q.json from the head>` prints `{"reasons": [], "status": "not_applicable"}`.
- librdkafka and Confluent.Kafka claims rest on plan section 4.6, which plan check 7022568885 verified against the pinned v2.16.0 sources; this session did not repeat that read.

## Commands and results

Run from the repository root on the branch, with the inputs in a scratch directory outside the repository.

- `make_results.py --inputs "$IN"`: exit 0; wrote the two results files and README line 32; printed the row.
- `--check`: exit 0, `PASS check`.
- `--self-test`: exit 0; `PASS real_inputs` and 23 `PASS` lines, from `PASS hash_c0_candidate` to `PASS report_edit`.
- `python3 -m json.tool results/C03/2026-11-19.json`: exit 0.

## Cold review record

Recorded in the pull request description, not here, so that this file does not describe its own review.

## Remaining approval

Hari approves or rejects the verdict and merges. The pull request touches `/results/` and `/*.md`, protected paths.
