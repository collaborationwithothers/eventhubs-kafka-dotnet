# README results index receipt

This receipt records the source checks and validation for issue #237. It describes the source commits and commands stated below.

## Authority and source

Approved plan: [README results index, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/237#issuecomment-6477867107), under standing approval after the [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/237#issuecomment-6477924740).

Decisions: [hand-written table](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/237#issuecomment-6477701900), [every inventory item](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/237#issuecomment-6477727218) and [observations and status wording](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/237#issuecomment-6477750132).

Pinned source: `4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`.

Validation source commit: `4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`, with the README and this receipt authored as uncommitted changes. Validation of this working tree does not claim that the source commit alone contains the changes.

## Row evidence

Each entry below checks the report's topic, literal verdict and the observation or limitation summarized in the README. Paths and line numbers refer to the pinned source above.

- `results/A01/2026-10-25.md`, lines 3-5: valid synthetic probe, no positive throttle sample and full workloads gated off; untestable.
- `results/B01/2026-10-27.md`, lines 3-5 and 50: large event failure after peer resets, successful control and exact size boundary not tested; confirmed.
- `results/B03/2026-10-27.md`, lines 3-5 and 75: gzip delivered, other named codecs rejected and throughput and compression ratios not tested; confirmed.
- `results/B04/2026-10-26.md`, lines 3-5, 69 and 71: idempotent production succeeded without fatal client errors or reorder; retry duplicate suppression and restart behaviour not tested; confirmed.
- `results/B05/2026-10-27.md`, lines 3-5 and 41: initialization returned without error; full transactions and exactly-once semantics not tested; confirmed.
- `results/C11/2026-10-27.md`, lines 1-5: trace only; pinned client exposes no commit metadata; untestable.
- `results/C12/2026-10-27.md`, lines 3-5 and 40-46: ASCII group ID boundary and subscribe-only scope; non-ASCII IDs not tested; confirmed.
- `results/C16/2026-10-27.md`, lines 1-5: trace only; needs client counts beyond build scale; untestable.
- `results/D04/2026-10-27.md`, lines 1-5: trace only; needs connection counts beyond build scale; untestable.
- `results/D05/2026-10-27.md`, lines 1-5: trace only; inventory reason is documentation trace only; untestable.
- `results/E05/2026-10-27.md`, lines 1-5: trace only; beyond build scale; untestable.
- `results/F04/2026-10-28.md`, lines 3-5 and 79-85: separate missing-role producer error, silent assigned read window and wrong-resource-token error; the read establishes neither denial nor success; undocumented.
- `results/G04/2026-10-27.md`, lines 1-5: trace only; single-region harness; untestable.
- `results/H01/2026-10-27.md`, lines 1-5: trace only; no throttle event to detect; untestable.
- `results/H03/2026-10-27.md`, lines 1-5: trace only; cannot be triggered deliberately; untestable.
- `results/K06/2026-10-27.md`, lines 1-5: trace only; harness uses normal pools; untestable.
- `results/K07/2026-10-27.md`, lines 1-5: trace only; zone outage cannot be induced; untestable.
- `results/K08/2026-10-27.md`, lines 1-5: trace only; platform freeze cannot be triggered; untestable.
- `results/K09/2026-10-28.md`, lines 1-5: trace only; LocalDNS changes the DNS path for every item; untestable.
- `results/K10/2026-10-27.md`, lines 1-5: trace only; exhaustion needs scale; untestable.

Unpublished topics and out-of-scope marks were checked against `docs/blueprint.md` section 12, including inventory amendment 1. The acceptance command verified 76 ordered inventory rows, 20 published report links and 56 unpublished rows, including the inventory's four out-of-scope items.

## Validation

- `git diff --exit-code 4a23cd823f462939de911d7ea8e61ccbbb4c5bb1 origin/main -- README.md docs/blueprint.md results/`: exit 0; pinned sources matched main before editing.
- `git ls-tree -r --name-only origin/main docs/operations/evidence/issue-237/`: exit 0 with no paths; the receipt destination did not exist on main.
- `npm ci --ignore-scripts`: exit 0; installed the locked documentation dependencies.
- The exact one-off Python block in the approved plan's Acceptance section: exit 0; verified the approved status, unchanged Read next section, complete ordered inventory, report paths, literal verdicts and unpublished placeholders.
- `git diff --check 4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`: exit 0; no whitespace errors.
- `npm run check:docs`, after staging both authored files: scan completed successfully; neither authored file had warnings. Existing warnings outside this diff remain unchanged and outside this PR's scope.
- `git diff --name-only 4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`: exit 0; only `README.md` and this receipt were authored.

Changed-file documentation warning dispositions: none.

## Cold review

Round 1 reviewed the complete staged diff against `origin/main` at `4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`, including this receipt. The fresh reviewer applied the repository's `repo-review` skill and reported `Findings: none at P0 or P1`, with no P2 findings. No findings required disposition, and the conditional prose follow-up round under AGENTS.md rule 8 did not apply.

The reviewer noted the pre-existing "No results exist yet" sentence in `docs/blueprint.md` section 7. It remains outside the approved README-only content change. A final full cold review includes this authored record; its result and the final head's CI run are recorded in the pull request description, without a receipt-only commit.
