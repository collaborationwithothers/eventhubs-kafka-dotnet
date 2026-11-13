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

## Commands and results (source: commit e82380eaa70fb192db01c4162652223bb87cf3a7 on `claude/440-gate-order`, on `origin/main` 9b13ce03; its tree is the reviewed diff)
- A1: `./scripts/test-verify-dotnet.sh` printed `verify-dotnet.sh checks passed.` Against `main`'s `verify-dotnet.sh` the same test file printed 16 `FAIL:` lines, so the new assertions test the change.
- A3: `npm run check:docs`: 9 warnings, none in a file this pull request changes (one blanket line in the pull request).
- A5 and A6: a script compared the section of 3.1, the line 3 sentence, CLAUDE.md item 1, skill steps 2 and 3, the `--help` output and the two dotnet-verification.md sentences with the plan's pinned blocks: all equal.
- ASCII scan of the changed files: no non-ASCII byte.

## Cold review
- Round 1: mode comprehensive; input revision: the index of branch `claude/440-gate-order` against merge base `origin/main` 9b13ce03d916bbd14e8d92445966fcfc09f34cfa (the review diff file); specification sources: plan r2 at b76e0ba1db16b36b6ce418dd43b318af73784457 (`git show b76e0ba1db16b36b6ce418dd43b318af73784457:items/440/plan-gate-order.md` in this repository), the issue body, [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415), [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826) and [plan check 6856461667](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856461667). Counts after this round: comprehensive 1, focused 0.

- A2: `./scripts/verify-dotnet.sh --tier sandbox` on e82380ea (tree clean), started 2026-11-13T15:01:06Z, ended 15:23:40Z, exit 0; last line `Partial verification passed (tier sandbox, projects: all). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.` `summary.json`: tier `sandbox`, outcome `partial_passed`, `failed_stages` `[]`, `source_dirty` false. By script over every `results.xml`: 11142 executed tests, 0 carrying the trait `Tier=CGroupV2`, 1826 in `Parchwell.LoadGen.Tests`. This is a partial run that excludes `Tier=CGroupV2` and is cited as such, not as the full gate. The Docker daemon was reachable (`docker info` succeeded).
- A4: `git diff --stat origin/main...HEAD` lists only `.agents/skills/implement-plan/SKILL.md`, `CLAUDE.md`, `docs/agents/pull-requests.md`, `docs/engineering/dotnet-verification.md`, `items/440/receipt.md`, `items/440/state.md`, `scripts/test-verify-dotnet.sh` and `scripts/verify-dotnet.sh`: no path under `.github/workflows/`, `docs/operations/workflows/`, `results/`, `scenarios/` or `AGENTS.md`.

## Cold review round 1: report and dispositions (comprehensive; no P0 or P1)
- P2 (dotnet-verification.md line 50: "give partial runs" also covers the `PARCHWELL_BUILD_MAX_CPU=1 ./scripts/verify-dotnet.sh` block line, which is a full-gate run): not valid as a change in this pull request. Rule 10 reason: the sentence is pinned by plan 3.5 and the plan check's P2-2, so a change needs a plan amendment under plan section 12; "never replace the full gate" was already on `main`; the review marks it a P2 that cannot be fixed inside the plan. Raised for Hari to decide in review.
- P2 (state.md line 10 widened the decision): fixed; the line now names check 6854556195 and the two texts decision 6856109826 adopts.
- P2 (receipt results attributed to a non-commit): fixed; the section heading names commit e82380ea.
- Pre-existing items the review lists (pull-requests.md line 5, the AGENTS.md intro, the unset of `PARCHWELL_SPIKE_EVIDENCE_DIRECTORY` under `--tier fast`): not changed; plan section 2 keeps line 5 and bars AGENTS.md changes, and the variable has no observed effect.
- Round 2: mode focused P2-correction; reports P0 or P1 only. Changes since round 1: the two prose fixes above and the receipt additions.
