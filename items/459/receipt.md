# Verification and handoff documentation corrections

This receipt covers the wording corrections requested in [issue #459](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/459). It records direct authorization, source checks and documentation verification; it claims no runtime test result.

## Execution card

- Deliverable: the issue's four corrections in `docs/engineering/dotnet-verification.md`, `docs/agents/pull-requests.md` and `CLAUDE.md`, plus this receipt and the required item state file.
- Authorization: Hari's issue body and the interactive instruction `Implement-issue 459`. This is directly authorized documentation/governance work without an approved item plan, under `docs/agents/pull-requests.md` "Rules". No plan approval is claimed. The issue names Codex in [the tool comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/459#issuecomment-6858168945); the interactive request authorizes this Codex session even though that comment followed the label event.
- Mandatory gates: documentation checks, fresh-context `repo-review`, hosted Codex review and final-head CI. Hari merges the protected documentation paths under AGENTS.md GOVERNANCE "Merge rule".
- Exact local verification commands: `git diff --check`, `npm run test:docs`, `npm run check:docs`. No .NET source, test or build input changes, so the local .NET gate is not applicable under `docs/agents/pull-requests.md` "Verification gates".
- Earliest publication checkpoint: passing documentation verification and complete applicable cold review, after refreshing issue authority. Open the pull request then so CI and hosted review can run together.
- Review budget: at most two comprehensive and two focused rounds cumulatively, with the prose-only P2 correction procedure in `docs/agents/pull-requests.md` rule 8. Exhaustion or unsafe focused review: save work to the branch's `-wip` backup and hand off to Hari under that rule.
- Predicate map: not applicable; no verifier behavior changes.
- Scope: no gate ordering, script, hook, test, workflow, expected outcome or result changes. Check the four issue items and the two process documents; leave unrelated advisory warnings with one blanket disposition under rule 8.

## Source comparison and implementation choices

Source revision: `662685a0d54ced8fc97a438bf6fac56023fa0b90` on `origin/main`, read in this session.

1. Move the `PARCHWELL_BUILD_MAX_CPU=1` example out of the partial-run block and explicitly call it a full gate. `scripts/verify-dotnet.sh:25` defaults to `tier=all`; lines 68-73 set only MSBuild CPU arguments, and lines 452-457 distinguish full and partial outcomes.
2. Replace ".NET source or tests" with the issue's explicit file scope in `docs/agents/pull-requests.md:59`: any path under `src/` or `tests/`, `scripts/verify-dotnet.sh`, any `*.csproj`, `Directory.*.props`, `packages*.lock.json`, or `global.json`. This identifies the existing rule's coverage without adding a gate or changing its order.
3. Match the image smoke stdout to `scripts/verify-worker-images.sh:33`: `{"native_loaded":true,"scenario_files":3,"uid_expected":1654}`.
4. Name `handoff-session-start.sh` and `require-handoff.sh` explicitly in the handoff paragraph. `.claude/settings.json:8` and line 24 register those handoff hooks; line 13 registers the separate Docker hook. `.claude/hooks/handoff-lib.sh:7-9` defines the shared handoff enforcement condition.

No library, Kafka or Azure capability claim is added. The choices above use the alternatives the issue explicitly allows. No later decision or approval comment exists in the initial authority read.

## Verification

Commands ran on base `662685a0d54ced8fc97a438bf6fac56023fa0b90` plus the staged documentation corrections and initial receipt/state files:

- `git diff --check`: passed.
- `npm run test:docs`: passed; 28 tests, zero failures and zero skips.
- `npm run check:docs`: completed successfully; no warnings in changed files. The advisory warnings are confined to unchanged files and are left outside this wording-only scope under rule 8.

Cold review mode: comprehensive, comparing the complete authored diff against base `662685a0d54ced8fc97a438bf6fac56023fa0b90`. This verification paragraph is authored after the scan; its source comparison and links are included in the cold-review inputs. The reviewed commit, complete cold-review report, cumulative round counts, final-head CI and hosted review are recorded in the pull request description, without adding a review report to its own reviewed receipt.
