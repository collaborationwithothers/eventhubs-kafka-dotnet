# Issue 184 validation receipt

This pull request writes the adopted review-process changes and their amendments into the owning rules. This receipt records local validation of the process text and documentation-scanner change.

Source commit: `906624418df32ba4168ee712a2ccfb59fb58a765`.

Authority: [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397502199), [independent Revision 2 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397654013), [adoption decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397456399), [B amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397554085), and [scanner and severity amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397583676).

## Local validation

The following checks ran on 2026-10-25 against the staged Revision 2 implementation based on the source commit above, before this receipt's final wording. No program output is committed. The pull request description records the final receipt-covered scan, last cold review and final-head CI under AGENTS.md "Before opening a pull request" rule 8.

| Command or inspection | Observed result |
|---|---|
| `git diff --check` and `git diff --cached --check` | Passed without whitespace diagnostics. |
| `npm ci --ignore-scripts` | Completed using the existing lock file. The existing transitive `glob@10.5.0` emitted its deprecation notice; dependency changes are outside this plan. |
| `node --test --test-name-pattern='discovery excludes raw evidence' scripts/tests/docs-check.test.mjs` before the scanner edit | The new regression failed because the old scanner reported the synthetic raw-capture identifiers. This was the expected pre-fix result. |
| `npm run test:docs` after the scanner edit | All 28 tests passed, with no failures, cancellations or skips, including the new discovery regression. |
| `npm run check:docs` | Completed successfully with no warnings. The named authored-file warnings are absent. |
| `git diff --cached --name-only`, compared as sets in Python | Exactly the ten planned paths were staged. Git comparisons confirmed no raw evidence or npm manifest changes. |
| Staged diff inspection against the plan's exact substitutions | A1 and C are retained; B, D, the scanner boundary, regression, contract and historical citations match Revision 2. |
| `git cat-file -e` and `git show` for the historical citation targets | Both targets exist at `f79c74cf23f5d0a1d30b399728f265c82208e676`; their contents support the historical storage configuration and comparison-test references. |

Test totals come from the test runner. File and warning counts were checked with Python against Git's staged path set and the scan log. The new fixture proves that raw captures are excluded while adjacent authored Markdown, a similarly named file and a raw directory outside the evidence tree remain scanned. No existing assertion was changed.

## Semantic acceptance

These are inspections of the rules' stated behavior, not observations of future review or merge outcomes.

| Planned case | Inspection result |
|---|---|
| Exact check P2 fix in a planned file, with no enforced-check change | Factory step 1 permits the marker for the identified revision; step 3 and implement-plan consume it. |
| Unplanned file or enforced-check change | Outside A1's permission. |
| Open PR-affecting question or current blocking finding | Existing approval and handoff conditions remain. |
| Prose P2 in an existing diff file at the first clean round | Rule 8 requires its fix and an additional review when the diff changes. |
| Additional round's reporting and repetition | Rule 8 limits reporting to P0/P1 and disallows a repeated additional round. |
| Additional round reports P0/P1 | Rule 8 reverts the post-clean-round P2 fixes and allows the clean-reviewed diff without another round only when the findings go away. Surviving findings take rule 11's backup and handoff route; reverted P2s follow rule 10. |
| New-scope P2 | Rule 8 permits one follow-up issue for the PR; rule 10 still applies. |
| In-scope P2 that is not prose-only | Rule 10 disposition remains, without automatic new-scope issue creation. |
| Receipt describes an earlier commit or pre-commit tree | Rule 8 allows the actual source without requiring its containing head. |
| Final-head CI and final cold review | Rule 8 places these in the PR description. |
| Wording point names no affected reader or future change | AGENTS.md Code Review Rules excludes it from P2 reporting; repo-review cites that owner. Codex's future compliance is not established. |
| First five PRs merged after this PR | Rule 8 records per-PR rounds, follow-ups, whether B's additional round ran and what it reported, then sends the evidence to issue 184 for Hari's decision. |
| Raw capture versus adjacent authored Markdown | The approved exclusion and regression implement that distinction while preserving advisory exit status. |
| Warnings in unchanged files | Rule 8 requires one blanket PR line and excludes them from disposition caps. |

## Earlier validation record

The Revision 1 implementation was saved in [backup commit fe861cc](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/commit/fe861cc) with a [continuation handoff](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184#issuecomment-6397538759). Its npm installation, whitespace checks and five-path scope comparison passed. Its documentation scan completed with 38 advisory warnings, all in then-unchanged files; it preceded the final backup receipt wording and B paragraph placement correction. That scan is historical evidence, not Revision 2 acceptance.

Under the third decision, the earlier per-warning dispositions collapse into the unchanged-file blanket line in the PR description and no longer consume the disposition cap. Revision 2 excludes raw captures and repairs the two named authored-document links. It does not edit the captured sources. The later five-PR observation sample is not yet available; issue 184 remains open for that review point, and Hari retains the protected-path merge.
