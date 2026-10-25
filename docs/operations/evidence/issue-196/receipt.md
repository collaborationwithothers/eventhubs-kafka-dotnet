# A01 draft verdict preparation receipt

This receipt records preparation and publication checks for the proposed A01 verdict from final run 40227770436. It records measured results only for the source commits and commands stated below.

## Source and commands

The branch base and preparation source are `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`, plus the three authored files in this pull request. The final-run source is `fa417d783e833ad222ab5173978433448e4aefbe`. Preparation follows [plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6403053075), updated `2026-10-25T21:21:49Z`, and its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/196#issuecomment-6403487457), which reports `Blocking findings: none` for the draft-preparation stage.

The official [final run](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436), attempt 1, used that final-run source after [verification run 40227421589](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227421589) succeeded. [Candidate artifact 12613440446](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436/artifacts/12613440446) contains `candidate.json`, SHA-256 `905faa04ef3833308629e66630e25446ba7b280e6b91379a5070ce219cb805da`. The candidate projection preserves its observed values, nulls and reasons. The [approved scenario](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fa417d783e833ad222ab5173978433448e4aefbe/scenarios/A01/scenario.yaml) and [expected outcome](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fa417d783e833ad222ab5173978433448e4aefbe/scenarios/A01/expected.yaml) are unchanged.

[Pod artifact 12613590239](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40227770436/artifacts/12613590239) retains worker logs and pod JSON. Both final-run artifacts have observed expiry `2027-01-23T19:48:27Z`. Raw candidate bytes, pod JSON, private infrastructure logs and temporary validation files remain outside git. The [report](../../../../results/A01/2026-10-25.md) identifies the exact environment evidence and its node-size inference, unavailable Confluent.Kafka runtime version, missing broker-count outputs and unavailable detailed verifier projections.

The following preparation commands exited 0 on source `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a` plus the authored three-file diff:

- `python /tmp/issue-196-acceptance.py prepare --candidate "$ISSUE196_CANDIDATE" --base "$ISSUE196_BASE"`: the real candidate hash, complete projection, all six count sums, report hash, pending gates, three-file scope and automated secret checks passed. Publication remains blocked pending C5 and broker-count output.
- `npm ci --ignore-scripts`: installed the locked documentation-check dependencies successfully.
- `git add results/A01/2026-10-25.json results/A01/2026-10-25.md docs/operations/evidence/issue-196/receipt.md`: staged only the approved files for documentation scanning and review.
- `npm run check:docs`: completed; both authored Markdown files had no issues. Warnings in unchanged historical files receive the blanket disposition below.
- `git diff --check` and `git diff --cached --check`: no whitespace errors.

`ISSUE196_BASE` was `fd0a65cdf7f72d2aea0e7e1f0cc2d490aa87430a`; `ISSUE196_CANDIDATE` was the actual extracted candidate at `/tmp/plan196-source/candidate-candidate.json`. The temporary validator is the exact body in approved Revision 3. Manual inspection of the three authored files found no credentials, connection strings, tenant/subscription IDs or raw private logs. No local .NET, infrastructure or mutation campaign is part of this documentation-only preparation. Publish acceptance was not run because its original evidence inputs are absent.

## Publication gate

C5 publication status: pending. No complete passing replacement summary has been supplied. This draft is not ready for approval or merge.

## Target identity

Replacement C5 source identity is pending; no passing replacement measurement has been asserted.

## Broker-count publication output

Broker-count publication status: unavailable. Per-repeat broker counts are not retained in the inspected artifacts; publication is held.

## Documentation and cold review

Documentation-check results and completed cold-review rounds are recorded here for their checked source. Final-head CI and the last cold-review result belong in the pull request description under AGENTS.md "Before opening a pull request" rule 8; this receipt need not describe its own containing commit.

Blanket disposition: documentation warnings in unchanged historical evidence files remain outside this PR's scope after Runner retirement. No changed-file warning was reported. The scan is advisory and does not establish factual accuracy; the candidate, cited source code, workflow evidence and authority decisions were checked separately.

Cold-review round 1 applied the repository's repo-review rubric to the three-file review diff and this authored receipt on the preparation source above. It independently checked the official final artifacts, run/job/log metadata, pinned source code and cited decisions. It reported the label-dependent P0 at `results/A01/2026-10-25.json:3`: both new results files require a PR labelled `verdict`, which the rubric treats as absent from a diff. Its result line was `Findings: 1 at P0 or P1`; it reported no independent factual defect or P2.

Disposition: keep the required verdict work under AGENTS.md "Before opening a pull request" rule 8's approved-plan/verdict-step exception. The PR must be labelled `verdict`, remain draft with both publication holds and go to Hari for verdict approval and protected-path merge. This kept finding does not clear either publication hold. No P2 prose fix or additional P2-only review round was required. The final review of this receipt update and final-head CI are recorded in the PR description.

The first-five-pull-request review window after #184 does not apply to this pull request. A REST-based script identified [#186](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/186) as that change and the next five merged pull requests as #188, #189, #191, #193 and #194, in merge-time order. This preparation adds no follow-up issue or scope beyond the approved three-file plan.
