# Authored receipt: #440

## Execution card
- Deliverable: the "Verification gates" section of docs/agents/pull-requests.md and its three cross-reference sentences; CLAUDE.md item 1 and two sentences of the implement-plan skill citing it; the `--tier sandbox` option of scripts/verify-dotnet.sh with its test cases; the matching sentences of docs/engineering/dotnet-verification.md; this receipt and the state file.
- Authorization: [plan r2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6854384408), file `items/440/plan-gate-order.md` at commit b76e0ba1db16b36b6ce418dd43b318af73784457 on branch `claude/440-plan`, approved under standing approval by [plan check 6856461667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856461667) (`Blocking findings: none`), with [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415) and [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826). Source base: `origin/main` 9b13ce03d916bbd14e8d92445966fcfc09f34cfa.
- Gates: plan acceptance commands A1 to A6; `npm run check:docs` before the cold review (rule 8); cold review under rule 8 (2 comprehensive, 2 focused; exhaustion: push -wip and hand off to Hari); the pre-PR local gate once on the reviewed source; Codex review; merge only by Hari (protected paths).
- Earliest authorized publication checkpoint: push after a review round with no P0 or P1 and A1 to A6 pass.
- Predicate map: plan section 6 (T1 to T10); R1 to R4, R9, R10 are text requirements checked by A5 and A6.

## Plan check findings left to the implementer
- P2-2 of check 6856461667 (`apply in implementation`): applied literally. `docs/engineering/dotnet-verification.md` line 50 ends "and never replace the full gate:", not the plan section 3.5 text "never replace it:". A6 compares against the check's text.
- P2-1 of that check (scope sentence ".NET source or tests"): not marked; left as the plan pins it, for Hari to raise on review.

## Standing fixture rule (plan section 6)
No existing case needed a fixture change. The new assertions were added to existing cases (default, fast, unknown_tier, help) without removing or loosening any assertion.

## Commands and results (source: `origin/main` 9b13ce03 plus the working tree of this branch)
- A1: `./scripts/test-verify-dotnet.sh` printed `verify-dotnet.sh checks passed.` Against `main`'s `verify-dotnet.sh` the same test file printed 16 `FAIL:` lines, so the new assertions test the change.
- A3: `npm run check:docs`: 9 warnings, none in a file this pull request changes (one blanket line in the pull request).
- A5 and A6: a script compared the section of 3.1, the line 3 sentence, CLAUDE.md item 1, skill steps 2 and 3, the `--help` output and the two dotnet-verification.md sentences with the plan's pinned blocks: all equal.
- ASCII scan of the changed files: no non-ASCII byte.

## Cold review
- Round 1: mode comprehensive; input revision: the index of branch `claude/440-gate-order` against merge base `origin/main` 9b13ce03d916bbd14e8d92445966fcfc09f34cfa (the review diff file); specification sources: plan r2 at b76e0ba1db16b36b6ce418dd43b318af73784457 (`git show b76e0ba1db16b36b6ce418dd43b318af73784457:items/440/plan-gate-order.md` in this repository), the issue body, [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415), [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826) and [plan check 6856461667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856461667). Counts after this round: comprehensive 1, focused 0.
