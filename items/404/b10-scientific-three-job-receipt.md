# B10 scientific three-Job implementation receipt

This change implements the scientific producer sequence and per-repeat evidence for B10. Final sweep, judgement and publication remain a separate approved change. Hari merges this pull request because it changes protected verifier paths.

## Execution card

- Deliverable: Revision 7, sections 2 to 6, of the [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/4196a0325fd91534239efbc70ef3dbafd6dfe10e/items/404/plan-b10-scientific-three-job.md).
- Authorization: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6848300952), recognizing decisions [scope and caps](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916), [C0 and implementation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676), [completion receipt](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), [C0 nonfinal shape](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445) and [scientific run-ID gate](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6847743695), each read in full in creation order.
- Source: `main` at `2077964b`; branch `codex/404-b10-scientific-three-job`. The pinned plan file was read with `git show`; Python `hashlib.sha256` computed `11efb0f8ac45a92434e3ba5ad868829d0b0f0b76b347305546bb7e9e7ef57e44`, equal to the approved hash before implementation.
- Prerequisite: the managed local Docker socket passed `docker info`; `docker run --rm hello-world` pulled and ran successfully before any repository edit.
- Mandatory gates: plan acceptance A1 to A5, documentation-warning dispositions, predicate coverage, cold review under [pull-request rule 8](../../docs/agents/pull-requests.md), final-head required CI and hosted code and security reviews. A6 is Hari's post-merge mutation gate on `main`; this session dispatches nothing. No merge, live run, result, verdict or expected-outcome change is authorized.
- Verification commands: `./scripts/verify-dotnet.sh`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/phase/tests -p 'test_*.py'`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; `git diff --name-only origin/main...HEAD`, with a scripted section-2 scope check.
- Earliest publication: after implementation, required local verification and applicable cold-review coverage, publish the tested branch and open the pull request; required CI and hosted reviews may run together. Hari receives the protected-path merge handoff after the gates are recorded.
- Work caps: 20 verification-failure root causes, 20 review-finding root causes and 40 existing fixture-rule cases per plan sections 2.4 and 9. Each exhaustion is `push -wip and hand off to the next session`. Shared fixture support with unchanged expectations is excluded as section 2.4 states. These caps do not reset the pull request's cold-review budget.
- Cold-review budget: at most two comprehensive and two focused rounds across all sessions, with the exhaustion action in pull-request rule 8: save the backup and hand off to Hari. No rounds have started.

## Predicate map and implementation choices

Plan section 6 is the starting predicate-to-test map. The completed map will record test locations for each requirement and the empty, boundary and precedence cases before the first cold review.

- Plan-check P2-A applies: for repeat 2 or later without a drained window, check 30 is not evaluated, adds no problem 10, and `earlier_repeat_records` is null. Repeat 1 remains zero.
- Plan-check P2-B applies: the new literals include `verifier_termination_unavailable` from section 3.2, alongside section 3.7's `b10_abrupt_verify_earlier_repeat_unavailable`, `b10_c1_final_judgement_unavailable` and `b10_c0_final_judgement_unavailable`.
- Plan-check P2-C choice: retain the existing cumulative `B10_LIFECYCLE_SECONDS` policy bound of 120 seconds for lifecycle pod reads, including C1 producers and verifiers. It keeps evidence collection bounded without extending the approved orchestration deadline. A lifecycle gap makes `run-repeats` exit 1: a missing observed termination is incomplete orchestration evidence, which item (b) must see as a gap. It does not invent a worker outcome or prevent the current repeat's required verifier after an accepted first publication.
- Legacy verifier fixtures with non-scientific run IDs retain their existing contracts. Every fixture adaptation will be inventoried before review; no assertion may be loosened or removed.

## Validation and review record

Implementation and validation are pending. No generated program output is committed. Complete reports, source revisions, measured results, fixture inventory and cumulative review counts will be recorded here before publication; the final head's CI and last cold review belong in the pull request description.
