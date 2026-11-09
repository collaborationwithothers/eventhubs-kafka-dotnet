# Issue 412: plan-check findings on issue 404

This folder holds the classification behind [issue 412](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/412). It covers every finding in the 22 plan-check comments on issue 404, read on 2026-11-09. The classification is model judgement, not a measurement.

## Files

- `plan-check-findings.tsv`: one row per finding, 143 rows.
- `summarize.py`: recomputes the tables in the issue from the TSV. Run `python3 docs/operations/evidence/issue-412/summarize.py`.

## Method

Five fresh-context subagents (Claude Opus, model alias `opus`) each classified one group of check comments. Each worked from the saved comment bodies and the same written rubric. Three headline claims were checked against the check comments afterwards.

Columns:

- `group`: the subagent's group. G1 is the restart-pair plan, G2 the restart execution plan, G3 the offline reproduction plan, G4a the three small plans, G4b the comprehensive diagnostics plan.
- `check_id`, `finding_id`, `label_as_written`: the check comment ID on issue 404, the finding's name in that comment, and the severity the check gave it.
- `class`:
  - D1: behaviour, design, safety or verdict defect;
  - D2: build or test defect;
  - L: literal or bookkeeping mismatch;
  - G: governance or format;
  - S: a scope question for Hari, not an error.
- `found_by`: `read` or `exec`. `exec` means the check had to build, run or test something to find it.
- `later_gate`: whether the pull-request gates (build with warnings as errors, the plan's own tests, CI, cold review and Codex review) would likely have caught the finding anyway. The values are `yes-build`, `yes-test`, `yes-review`, `no` and `unclear`. For the offline reproduction, which is a procedure and not a pull request, `yes-test` means the run would have failed visibly and `no` means it would have produced misleading evidence silently.
- `resolution`: the decision or revision that resolved the finding, `implementer`, `unresolved` or `unknown`.
- `summary`: one line; the reason for `later_gate` is in square brackets.

## Limits

- The sample is one issue, the hardest item so far.
- `later_gate` is a judgement about a counterfactual.
- Rows marked "ambiguous" in `summary` sit between two classes.
- Plans that were edited in place keep only their final revision on GitHub. Findings on earlier revisions were therefore judged from the check text and the decisions that answered them.
