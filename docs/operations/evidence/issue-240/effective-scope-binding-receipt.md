# Effective-scope confirmation binding receipt

This pull request implements the approved effective-scope binding plan for the live probe batch. It admits version 2 confirmations bound to the deployed namespace and narrow-control principals while retaining the source binding on each run snapshot. It does not replace the operator secret or authorize a dispatch.

## Execution card

- Deliverable: the seven changes in the [implementation-grade plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6508909910), GitHub `updated_at: 2026-10-29T21:37:18Z`.
- Current authorization: [binding decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6508869803), then the recognized later [approval decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6509228575). The approval names Codex, authorizes this one pull request only and adopts the [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6509198445). Apply its P2-1 two-line replacement at change 5 and P2-3 protected-path correction. Secret replacement and dispatch remain outside authorization.
- Mandatory gates: exact old-text counts before editing; acceptance commands below; documentation warning dispositions; cold review under AGENTS.md "Before opening a pull request"; final-head hosted CI and Codex review; Hari's merge because `src/Parchwell.Verifier/` and `tests/Parchwell.Verifier.Tests/` are protected. `docs/operations/` has no code owner.
- Exact verification commands: plan Acceptance steps 1 through 6, reproduced below. The required post-merge mutation acceptance follows factory.md "Evidence pack" and remains outside this pull request's execution authorization.
- Earliest authorized publication: after local acceptance evidence and applicable clean cold-review coverage, publish the tested pull request so independent CI and hosted Codex review can run together.
- Review budget and exhaustion: AGENTS.md "Before opening a pull request" rule 8 permits at most two comprehensive and two focused rounds cumulatively. At exhaustion or unsafe focused scope, save unpublished work to the branch with `-wip` appended and hand off to Hari. Initial cumulative use: zero comprehensive, zero focused.
- Source commit: `95b99efe3a1ec89121cb76dd2f38c76eb2e4c051` (also the plan's source).

## Predicate-to-test preparation

The changed confirmation predicate requires exactly `schema_version`, `namespace_id`, and `accounts`, with integer version 2. Namespace, principal, account, managed-role and per-run snapshot checks retain their existing behavior. This map is prepared before the first cold review.

| Boundary or interaction | Evidence |
|---|---|
| Below, at and above version 2 | Python accepted-path test and new version 1/3 rejection subtests; verifier schema-boundary theory rows 1/2/3 |
| Version 2 with obsolete `source_commit` | Python new subtest rejects the extra key; verifier object construction cannot retain it because `SingleActionWire.JsonOptions` disallows unmapped members |
| Empty or unreadable confirmation | Existing Python missing-confirmation test and verifier missing-confirmation theory retain the permission-scope gap |
| False access attestation | Existing Python false-confirmation case and verifier `false_confirmation` row |
| Run-snapshot source mismatch | Existing verifier `source` row rejects it independently; snapshot `source_commit` remains present in Python |
| Schema versus collection-count precedence | Change 5 replaces the schema-boundary theory's two-line match only; the collection-count theory keeps its 0/1/2/3 rows, with otherwise valid fixture inputs |

## Verification commands and results

Run from the repository root. Detailed command output stays outside the repository; no program output is committed.

```bash
python3 -m unittest infra.scripts.phase.tests.test_probe_v2.ProbeV2Tests.test_scope_is_private_bound_and_created_immutably_once infra.scripts.phase.tests.test_probe_v2.ProbeV2Tests.test_missing_or_false_confirmation_stays_a_gap infra.scripts.phase.tests.test_probe_v2.ProbeV2Tests.test_superseded_or_commit_bound_confirmation_stays_a_gap
dotnet test tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj -c Release --filter "FullyQualifiedName~ProbeV2"
python3 -m unittest infra.scripts.phase.tests.test_probe_v2
./scripts/verify-dotnet.sh
git grep -n 'candidate\["source_commit"\]' -- infra/scripts
git grep -n "confirmation.SourceCommit" -- src
npm run check:docs
```

Verification used SDK 10.0.402, Helm 4.3.1 (download checked against the repository installer's SHA-256), and the managed local Docker daemon. The pinned SDK and Helm were installed outside the repository because they were initially absent.

- All pre-edit literal occurrence counts matched the plan as corrected by P2-1: one each except change 6's stated count of two. The separate collection-count theory's row stayed byte-for-byte unchanged.
- Acceptance step 1, with change 6 alone: three tests ran with exactly two failures, the accepted-path test and the version 1 subtest of the new rejection test. This is the required red result.
- Acceptance step 2, after change 1: the same three tests passed.
- Acceptance step 3: all 812 focused verifier tests passed, with no failures or skips.
- Acceptance step 4: all 27 Python module tests passed, including real Helm rendering; the full `./scripts/verify-dotnet.sh` passed locked restore, Release solution build, XML inventory validation, architecture and all paired suites, report validation, and warning-as-error controls.
- Acceptance step 5: both searches printed nothing and returned exit 1, as expected for no matches. `git diff --check` passed.
- Acceptance step 6: `npm run check:docs` completed. Its eight advisory warnings are all in unchanged files; no warning is in the authored receipt or smoke contract.

The full command's fresh XML reports were parsed by script:

| Suite | Passed | Failed | Skipped |
|---|---|---|---|
| Parchwell.ArchitectureTests | 12 | 0 | 0 |
| Parchwell.Enrichment.Tests | 56 | 0 | 0 |
| Parchwell.Kafka.Tests | 405 | 0 | 0 |
| Parchwell.LoadGen.Tests | 648 | 0 | 0 |
| Parchwell.Scenarios.Tests | 239 | 0 | 0 |
| Parchwell.Verifier.Tests | 4252 | 0 | 0 |

The full command passed 5612 tests. These local tests establish harness behavior, not live Event Hubs acceptance. Their source was the pinned source commit plus the seven implementation changes described here; the receipt itself does not change executable behavior.

Final-head CI and the last complete cold-review report belong in the pull request description, under AGENTS.md "Before opening a pull request" rule 8.

## Specification comparison and authority

The issue has no parent items or approved split. Its earlier smoke support and statistics repair are merged; the scientific experiment still requires separate approval after accepted smoke evidence. The selected current unmerged candidate is the effective-scope binding plan above. The issue's `ready-for-agent` label was applied after the approval named Codex, and the plan check and approval both identify the current plan revision and edit timestamp.

| Plan change | Implemented requirement and decision |
|---|---|
| 1 | Python accepts the closed version 2 confirmation shape without `source_commit`; its per-run snapshot retains the source field. Binding decision points 1 and 3 apply. |
| 2 and 3 | `ProbeV2Effective` drops only its confirmation source field, and `V2Scope` requires version 2 while retaining namespace, principal and run-snapshot comparisons. Binding decision points 1 and 3 apply. |
| 4 and 5 | Fixture uses version 2 and the schema-boundary theory checks versions 1, 2 and 3. The approval's adopted P2-1 uses the exact two-line old/new text from the plan check; the collection-count theory retains its 0/1/2/3 rows. The boundary shift and removed confirmation-source conjunct are explicitly required by the binding decision and approved plan. |
| 6 | Python accepted/false fixtures use version 2; the new rejection test covers obsolete version 1, an obsolete source key on version 2, and version 3. |
| 7 | The smoke contract owns the new format, reissue conditions, absence of automatic detection of later Azure access, and deployed-output validation. Binding decision points 1 through 4 apply. |

No expected outcomes, results, workflow files, permissions or network configuration change. Only the authored execution and verification receipt accompanies the seven planned files. The [approval decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/240#issuecomment-6509228575) is a recognized later decision under factory.md "Item lifecycle" step 3 and AGENTS.md "Before opening a pull request" rule 8, not merely background evidence.

The protected-path correction was also checked against the live `main-protection` ruleset's `required_reviewers` file patterns and `.github/CODEOWNERS` at the source commit. Both exempt `/docs/operations/` and include `Parchwell.Verifier*/`. Hari retains the merge decision.

Reviewed design and engineering sources: blueprint ADRs, especially authentication scope, the verifier's authority and workflow-owned lifecycle; docs/engineering/dotnet.md "Testing"; docs/engineering/documentation-checks.md; and the governance merge rule. The changed contract describes the approved attestation policy, not a new measured Azure capability. Its stated limitation remains explicit: later access changes are not detected. The verifier's strict JSON reader delegates through `ProbeV2Wire.Read` to `SingleActionWire.Read`, whose options disallow unmapped members.


## Frozen cold-review inputs

- Mode: comprehensive, first pass for this pull request.
- Input revision: local immutable tag `issue-240-effective-scope-review-1`, created on the commit containing this receipt immediately before review. The full SHA and complete report are recorded in the pull request self-review record.
- Comparison revision: `95b99efe3a1ec89121cb76dd2f38c76eb2e4c051`.
- Scope: the complete authored pull request diff, including this receipt. The reviewer receives only the frozen diff path and this receipt path, applies the repository-owned `repo-review` skill, and may read its cited sources.
- Cumulative budget on entry: zero completed comprehensive and zero focused rounds; this is comprehensive round one. No corrections are made until the complete report arrives. Later reports and dispositions belong in the pull request self-review record so the final report is not added to its own reviewed receipt.
