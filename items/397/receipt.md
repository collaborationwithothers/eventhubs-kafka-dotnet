# C5 runtime investigation receipt

This first investigation measures the existing workload and documents the remaining completion-receipt coupling. It changes no production source, tests, assertions, mutation configuration or workflow and claims no runtime reduction.

## Execution card

- Deliverable: the [investigation](investigation.md), B10 runbook sentence and item-state follow-ups in the five paths of [Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683195297).
- Authorization: [issue scope](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397), [runtime decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683039430), direct user instruction to investigate first without dispatch, and [standing-approval check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/397#issuecomment-6683257235). The check identifies Revision 1 updated 2026-11-06T10:55:26Z. No selection decision is exercised.
- Mandatory gates: script-derived measurements, explicit gaps, `npm run check:docs`, `git diff --check`, exact five-file scope, cold review, hosted code/security review and separate Claude review. Protected prose merge remains Hari's.
- Verification commands: exact diagnostic commands and authored parsers are in the investigation; acceptance is `npm run check:docs`, `git diff --check`, and `git diff --name-only` with new files staged. Diagnostic runs are not mutation acceptance.
- Earliest publication: first clean applicable cold review after the documentation checks. No new .NET run is needed for the prose edits.
- Review budget: docs/agents/pull-requests.md rule 8, at most two comprehensive and two focused rounds cumulatively. Exhaustion or unsafe focus saves a backup and hands off to Hari. No prior review round belongs to this new PR; the PR 393/396 review budget is not reused or reset.
- Measurement caps and exhaustion: plan procedure; one baseline and one capacity run, with retained A01 artifacts only. No new mutation run. Gaps are recorded and work continues under the approved caps.

## Source and results

Measured source: `29e3f0a9b17c3cf06b97bcc5c7125731a23dcc44`. The branch is `codex/397-c5-runtime-investigation`. The locked restore and mutation-only Release build passed; build reported zero warnings/errors. Full mutation-only MTP baseline passed all 6555 cases; native capacity measurement passed all six cases. Raw output stays under `/workspace/scratch/397`, outside git. The investigation records input hashes, exact commands, units, timing pairs and limitations.

Scope items 1 and 2 are investigated: repeated suite cost is supported by local XML and comparable A01 artifacts; coverage-off history and pinned MTP support are verified. Scope item 3 is an options/recommendation record, not an implemented selection change. Scope item 4 is the state correction, timing-coupling sentence and external peak-memory measurement, with no new memory assertion. PR 396 was still open when the state was authored, head `e1b6ec30f67f07d2a0d0055fe01fcb5953b4df10`; no result here measures its correction.

The initial direct-DLL attempt rejected the MTP XML flag before tests started. One command correction used the existing repository MTP invocation, then the baseline passed. Its original error log was overwritten when reusing the baseline pathname; the investigation records this retention gap and exact observed error. Native capacity ran once with its supported arguments. No result was retried or suppressed. No historical suite or coverage experiment ran.

The configured mutation-only compile property excludes the partition-readiness component tests. PR 392 shared-helper/controller cost is not isolated by current test durations. PR 388 lies between the hosted commits, so the full slowdown cannot be assigned to PRs 391-393. Cancelled shard times are censored. No generated output is committed.

## Source verification and predicate map

The nonblocking `apply in implementation` citation fix is applied: the source is `src/Parchwell.Verifier/VerifyRun/B10SweepHandler.cs::ExecuteAsync`, not a nonexistent RunAsync member, and `src/Parchwell.Verifier/Program.cs::RunAsync`. Projection and logging precede receipt publication; watchdog disposal follows it. The runbook states the residual timing risk without changing finalization or validity rules.

Pinned Stryker documentation, CoverageAnalysisInput, CoverageAnalyser and MicrosoftTestPlatformRunnerPool were read at `6e77a3451bac4793e9c839c3ff4055c1b30ca3af`. The historical decisions and direct source links are in the investigation. Blueprint scope, verifier oracle/lifecycle rules, .NET standards and merge requirements remain unchanged.

Predicate map: not applicable. No verifier predicate or test changes. The measurements are observations, not new checks on published evidence.

## Validation and review

Documentation and whitespace checks and the review report are recorded below before publication. Hosted final-head CI and reviews belong in the PR description. Models: Codex author; high-effort Codex planning, plan-check and cold-review subagents. Exact model identifiers are not exposed in this session.

The staged five-file documentation scan passed with no warnings in changed files. Eight warnings in unchanged files retain the blanket disposition under rule 8. `git diff --cached --check` passed. All three authored Python blocks compiled, and both parsing blocks executed against retained inputs without rerunning capacity tests. Scope is exactly the five planned paths. The first documentation scan did not include untracked new files; staging them and rerunning the scan supplied the required coverage.

Cold-review input: comprehensive pass 1, full staged authored diff against main `29e3f0a9b17c3cf06b97bcc5c7125731a23dcc44`, including this receipt. Freeze that input until the complete report returns. Cumulative usage at submission: one comprehensive, zero focused. The complete report and its disposition will be linked in the PR description, without appending the report to its own reviewed receipt.
