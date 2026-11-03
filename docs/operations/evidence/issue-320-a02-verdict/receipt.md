# A02 verdict preparation receipt

This receipt records how the A02 evidence pack of issue 320 was prepared from batch run 41602168787 and mutation campaign 41602164797. The generator wrote both results files and the README row.

## Execution card

- Deliverable: six paths (the generator, this receipt, `results/A02/2026-11-03.json` and `.md`, README line 14 and `items/320/state.md`) on branch `claude/issue-320-a02-verdict`, as a draft pull request labelled `verdict`.
- Authorization: the [plan, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/320#issuecomment-6586425054), approved under standing approval by [plan check 6600765094](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/320#issuecomment-6600765094), and decisions 6563546020, 6569917006, 6588208278, 6591864593 and 6596432641 on issue 320 with the issue 301 decisions they adopt.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity"; docs/agents/factory.md "Evidence pack"; the generator checks G1 to G13, including the mutation gate G10; `npm run check:docs`; cold review under docs/agents/pull-requests.md rule 8; Codex review in the form AGENTS.md hard safety rule 10 allows.
- Verification commands: the plan's "Acceptance" section.
- Earliest publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: docs/agents/pull-requests.md rule 8, two comprehensive passes and two focused rounds; the exhaustion action is the handoff to Hari that rule 8 defines.

## Predicate map and generator

`make_results.py` runs these checks, in order:

- G1 Pins: the candidate listing is exactly the three files and every input hashes to its pin.
- G2 Receipt: the judge receipt matches the run, the judge pod and the A02 and A04 candidates.
- G3 Candidate identity: keys, versions, image digests, `adr012_counts` and the `k37_not_tested` note.
- G4 Selected outcome: the verdict is `contradicted` and the reason `closed_with_keepalive`.
- G5 Configurations: C0 then C1, five repeats each, with exact keys, statuses, client IDs and window fields.
- G6 Recomputation: every class, summary and the item assessment, and the summaries under the class rule before `timed_out_on_resume`.
- G7 Phase and batch: the phase status and the batch record.
- G8 Pods: collection status, pod identity, restarts, exit codes, image digests and container times.
- G9 Logs: the pinned lines of the batch and stop job logs.
- G10 Mutation gate: summary, 18 shards, seven targets, break threshold, Stryker configuration and the 89 survivors with their 50-entry sample.
- G11 Sources: the pinned scenario, expected outcome and cluster files and the pinned lines of expected.yaml.
- G12 Event recomputation: quiet maxima, disconnect and request timeout rows, window sums and the resume timeout sequence from every statistics report.
- G13 Output (`--check` only): the results files and README line 14 equal the rendered bytes.

`--self-test` runs the real inputs once and then 25 single-change cases, each of which must fail at its stated check: G1-append, G1-extra, G2-exit, G2-hash, G3-notes, G3-kind, G4-word, G5-status, G5-client, G5-window, G6-class, G6-hypothesis, G7-stop, G7-selected, G7-evidence, G8-restart, G8-gap, G9-teardown, G10-break, G10-commit, G10-survivor, G10-cap, G12-max, G12-timeout, G11-expected. No other self-test case exists.

## Specification comparison

- Verdict word from the rules (decision 6563540913 point 1, adopted by 6563546020): G4 pins `contradicted`, `closed_with_keepalive`, and G6 recomputes it from the window counts.
- K-37 not tested (decision 6562996229 point 3): the report states that K-37 was not tested and implies no answer; the candidate note `k37_not_tested` is checked by G3.
- Run 41602168787 only and campaign 41602164797 (decision 6596426514 points 1 and 4, adopted by 6596432641): every run value comes from that run; G10 requires the campaign to have measured the run's own source commit.
- Rule history stated (decision 6596426514 point 2, expected.yaml `text_must_state`): the report and the README row state that `timed_out_on_resume` was designed after an earlier run was seen; G6 recomputes the summaries without it.
- Nothing from earlier runs (decisions 6588208278 and 6591864593): no value of runs 41483886512 or 41534423570 is used as evidence.

## Evidence and claim verification

Acceptance step 1 printed these hashes, equal to the plan's values: `99b0f3e3ab24c97594ee775eadfc297c7885158390ee2925df5b0f8efb62ec63` (cand.zip), `2233339c5361c6dd2b23f4cb276aeab49976159155f69b8a435be1631b86b459` (pods.zip), `db220b179377791eda68796f8d6eb011906cc1eee44084f6a25cc850af7ae08a` (c5.zip), `a747f60c3568fa287f91869c38dec5dc1706679b1dcb653b2c699f9b10b95632` (batch.log) and `76bd6dde11f5d8f866276bfdd3ec15d964d1e1dcd9d690b0660f0a71545fafe6` (stop.log). Every run value in the report is read by the generator from a pinned input or from the pinned constants in the generator. Node size is stated as inferred.

## Commands and results

Run from the repository root at base `69102fe874698ab16839fe789b0bb54f158d535a` with the artifacts and logs in a scratch directory outside the repository.

- Acceptance step 1: the five `gh api` downloads each exited 0 and the extraction exited 0. `sha256sum` printed the five values above.
- Generator with no mode flag: exit 0; wrote only the two results files.
- `--readme-row`: exit 0; one line, used as README line 14.
- `--check`: exit 0.
- `--self-test`: exit 0; 25 `PASS` lines from `PASS G1-append` to `PASS G11-expected`.
- `npm run check:docs`: exit 0 with 8 warnings, all in files this pull request does not change (docs/agents/factory.md, docs/operations/evidence/issue-124, issue-140 and issue-211).
- Acceptance step 7: after `git add` of exactly the six paths, `git fetch origin main && git diff --cached --merge-base origin/main --name-only` listed exactly those six paths.
- Acceptance step 8: the ASCII scan over the report, the evidence folder, `README.md` and `items/320/state.md` printed nothing.

## Cold review record

No cold review round has run at the time of writing. Each round is recorded here with its mode, reviewed revision, resolved model and outcome by the session that runs it.

## Remaining approval

The #301 control session merges with the `verdict` label under decision 6563540913 points 1 and 2 when every condition of decision 6563515099 point 1 holds; otherwise the pull request waits for Hari.
