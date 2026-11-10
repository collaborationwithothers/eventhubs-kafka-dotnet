# B10 restart redesign implementation receipt

This pull request applies Appendix A of the approved B10 restart redesign plan to ADR-021 and the B10 exceptions in ADR-012, ADR-013, ADR-019 and ADR-020, and adds one status paragraph to each B10 operations document. It changes design text only. Every policy value in it is Hari's choice, not a measurement.

## Execution card

- Deliverable and authorization: [Revision 9](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/d175ed609717610d39b028253603d5a1c9eba820/items/404/plan-b10-restart-redesign.md), exact-body SHA-256 `9153e4367bfb11a24fab4a613a1202abb02b263b51f1e3267258a27a20dda377` (re-computed here from the file at that commit), selected by [pointer 6768580945](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768580945), checked in [6770329866](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6770329866) (`Blocking findings: none`) and approved in [6770899438](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6770899438) under [6769877985](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769877985). The approval was posted at 15:07:06Z, after the pointer's last edit (14:38:10Z) and the check (14:44:03Z), and names the same commit.
- Recognized decisions: [6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960), [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785), [6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811), [6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013), [6769416981](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769416981), [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939), [6769760685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769760685), [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) (which replaces 6769808426), read in full. Each is already applied in the plan's appendix A; plan section 14.1 records every answer. Decision 6769057785 Q13 names Claude Code as implementer.
- Mandatory gates: plan section 13, commands 1 to 5; cold review under docs/agents/pull-requests.md rule 8 on the exact head; final-head CI; Codex code and security reviews of the exact head. `docs/blueprint.md` is protected, so the pull request waits for Hari under GOVERNANCE "Merge rule".
- Exact verification commands: plan section 13, commands 1 to 5, with Appendix B's script run on the plan file as it is (no slot remains).
- Earliest publication checkpoint: after acceptance commands 1 to 5 pass and the cold review reports no P0 or P1; publish the pull request then, while CI and the Codex reviews run.
- Review budget: two comprehensive cold-review passes, then at most two focused correction rounds, cumulative. Exhaustion or unsafe focus saves `claude/404-b10-restart-redesign-wip` and hands off to Hari (rule 8). Documentation-warning dispositions: at most 20 root causes; exhaustion: `push -wip and hand off to the next session`.
- Predicate map: not applicable. No verifier predicate or code changes. Plan section 12's text checks are the test map.

## Implementation choices and verification

Baseline: `origin/main` at `8b459e260558033552f61faa35921439441a4355`. The three target files are byte-identical there to `0f769fb240d1de50192f4c8eefaf12698002efc7` (`git diff --stat 0f769fb2 origin/main -- <the three files>` printed nothing), so stop rule 3 does not fire.

- Acceptance command 2: Appendix B part 1 extracted from the plan file at the approved commit and run as `python3 -I apply.py <plan file> <output dir>`. It printed `bad 0`: all 66 old strings occur exactly once (67 edits with block P), block P's old lines hash to `d0b6fca6...ab1df4`, and no `[Q` remains. The three output files were copied into the working tree; `diff -r` of the output against the working tree printed nothing.
- Acceptance command 3: `grep -n '\[Q[0-9]'` over the three files printed nothing.
- Acceptance command 4: Appendix B part 2 printed `ordinary ok`.
- Acceptance command 5: `LC_ALL=C grep -n -P '[^\x00-\x7F]'` over the three files printed nothing.
- Acceptance command 1: `npm run check:docs` reports `no issues found` for `docs/blueprint.md`, `docs/operations/b10-abrupt-contract.md`, `docs/operations/b10-workflow.md` and the item-404 files. The eight warnings it prints are in files this pull request does not change; blanket disposition: pre-existing, not changed here.
- The diff of the three files is 73 insertions and 68 deletions in total (`git diff --stat`).
- Plan check P2-2 (applies in implementation): `items/404/state.md` is updated as docs/agents/factory.md "Item state file" requires: the `Covers` line, the implementing tool, the approved plan, one "Decisions in force" line per recognized decision since 6768228960 and the approval, and "Next action". The plan's section 2.1 wording ("one line plus Next action") is not followed where factory.md requires more; factory.md governs. Choice and reason recorded here for the cold review and Codex review.
- Plan check P2-1 (trial-bound wording) and P2-3 (DA1 on the AKS node) are not marked `apply in implementation` and change no text. P2-1 is Hari's decision, and the appendix text is applied verbatim.
- Choice left to the implementer: the branch name `claude/404-b10-restart-redesign` (tool prefix plus a name that includes the issue number).
- `README.md` and `docs/agents/factory.md` are untouched, as decision 6769843248 chose.
- No live infrastructure, workflow dispatch, trial, result or expected-outcome change was made.

## Cold review inputs

Initial mode: comprehensive; comparison revision `origin/main` at `8b459e260558033552f61faa35921439441a4355`. The reviewer gets the staged review diff and this receipt only. Cumulative budget before this review: zero comprehensive, zero focused. Complete reports, dispositions, cumulative counts and the final reviewed SHA are recorded in the pull request self-review record, not in this receipt.
