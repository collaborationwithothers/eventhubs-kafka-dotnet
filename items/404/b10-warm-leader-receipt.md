# B10 warm leader implementation receipt

This pull request implements the approved warm leader plan for issue #404. It adds per-arm warm-up and leader readiness before restart originals, with bounded child lifetime and offline proof.

## Execution card

- Deliverable and scope: Revision 3, sections 1 to 6 of [the approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/34aae8485053429a5b183e86a5f2c39dda570d25/items/404/plan-b10-warm-leader.md). Baseline `e74849ea716b6c4b8fa6c5143c6c4175c7038eea`.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6798220099), [Hari's approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801162806), and its recognized decisions. Plan commit and exact-body SHA-256 `4f76d94238a1fc5efdcc27ea69f808d81eb553758eae58bcdea48ac9bdf66b64` verified. The approval retains [the Revision 2 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801044256); Revision 3 incorporates its P2-A to P2-D and [the accepted margin and test bounds](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6801095762).
- Mandatory gates and exact verification commands: plan section 8: `./scripts/verify-dotnet.sh`; `python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `bash charts/parchwell/tests/check-loadgen-host.sh`; `npm run check:docs`; two Docker image-test runs and one run each of M1, M2 and M3 using the existing workflow's build/run commands by hand. No agent dispatch.
- Earliest publication: after required local verification and clean applicable cold-review coverage. Required CI and hosted Codex code/security review on the final head follow publication. Hari merges the protected blueprint change.
- Runtime fix budgets: plan section 8 permits 20 verification-failure roots and 20 review-finding roots per implementing session; either exhaustion pushes `-wip` and hands off to the next session.
- Cold-review budget: docs/agents/pull-requests.md rule 8: at most two comprehensive and two focused passes cumulatively; exhaustion or unsafe focus pushes `-wip` and hands off to Hari.
- Predicate map: plan section 6 covers the changed journal and phase predicates. No verifier predicate changes.
- Models: Codex control session; fresh-context high-effort Codex subagents for implementation and cold review. Exact model identity is not exposed by the harness.

## Verification and implementation choices

The effective probe settings are composed inside the abrupt host, preserving the ordinary resolver and the C1 timeout Job. The child settings guard uses its effective values; the successor selects the same baseline-valued, unoverridden timeout that it publishes. Warm-up callbacks use the existing bounded journal queue; durable journal frames, rather than callback arrival alone, release the producing thread.

The deadline is anchored to the actual `child_started` frame's monotonic timestamp. Every child wait uses the remaining deadline, and the final no-kill flush uses the full remaining time, clamped to zero, rather than a renewed drain budget. The leader predicate accepts JSON integer leader/node IDs without adding a width contract; partition and offset use their specified signed widths.

For M3, the mutation bypasses the cross-restart warm-up and binds the earlier report, preserving the valid control arm so the existing journal-publication assertions observe the missing-warm-up gap. This selects the gate under test without introducing another mutation.

Policy timing was recomputed by script from waits `[20, 90, 20, 120, 300, 20, 90, 20]`: cumulative seconds `[20, 110, 130, 250, 550, 570, 660, 680]`; the deadline plus cleanup is 700 s and the parent margin is 30 s. These are policy bounds, not runtime measurements.

Verification is in progress. The final authored results below will identify the committed source tested; raw reports and generated artifacts stay outside the repository.
