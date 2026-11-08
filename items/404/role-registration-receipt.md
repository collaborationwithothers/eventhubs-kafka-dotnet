# B10 abrupt host role registration fix receipt

This change registers the two roles declared by B10 in every abrupt host role. The regression test builds the real host registration and resolves B10 without starting a host or contacting Azure.

## Execution card

- Deliverable: the four paths in the [approved plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6726723660).
- Authorization: [approval 6727064513](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6727064513), [scope decision 6726549867](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6726549867) and [option A decision 6727058383](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6727058383).
- Plan identity: `updated_at: 2026-11-08T20:30:52Z`; exact decoded REST body UTF-8 SHA-256 `15b7e22f3b11f12824d8115245f02582b3764b488ff4365e393f190c6dfd5b26`, including the server-appended footer, without trimming, normalization or an added newline. Verified before implementation.
- Carried check: [independent plan check 6726863708](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6726863708), P2-1 and P2-2 exactly. Option C and P2-3 do not apply. The verifier client ID is `b10-abrupt-unused-verifier`.
- Source: main `25cc301a04a02b64e5f03d82ac6e3c0b260c6ae5`; branch `codex/404-role-registration-fix`.
- Mandatory gates: the plan's four-case red check before the host fix; four-case green check; full unmodified foundation verification where Docker works; documentation/whitespace checks; applicable cold review; final-head CI and hosted Codex code/security review; exact-head cold Claude review before merge.
- Exact commands and expected evidence: the approved plan's "Exact verification and evidence" blocks. Commands run from the repository root with SDK 10.0.402. Program output is retained outside the repository under `/workspace/scratch/issue404-role-fix/` and the foundation script's printed log directory.
- Publication checkpoint: after the authorized fix, required local verification and applicable cold-review coverage. Hosted review and CI may run together. No merge without every required gate and an up-to-date branch.
- Budget: one initial verification sequence and at most two consolidated correction rounds. Verification-failure, changed-file documentation-warning and review-finding root causes each cap at 10. Exhaustion: `push -wip and hand off to the next session` with measured counts and a dated, basis-labelled estimate. Unchanged-file documentation warnings receive one blanket disposition outside that cap.
- Cold-review budget: at most two comprehensive and two focused rounds, cumulative for this PR. Follow the nonrepeating prose-only P2 correction procedure in `docs/agents/pull-requests.md` rule 8. Budget exhaustion or unsafe focus saves unpublished work under rule 12 and hands off to Hari; no session resets the counters.

## Verification

Verification source is the main base above plus the approved working-tree files: host SHA-256 `c5e6a8a20d1afff5b1da811e44a1b8d99d83223c8c1432ce06378c73e490d9f6` and test SHA-256 `82c919e2a4a122741ded6e542a6009907652295685a65c4b6f6654a7e7c62b5e`. The host diff is exactly 12 insertions and 2 deletions; the new test is exactly 98 lines, measured by script.

Commands ran from the repository root using retained SDK `/workspace/b10-offline-404/sdk` and package cache `/workspace/b10-offline-404/packages`. Logs below are under `/workspace/scratch/issue404-role-fix/code-tests/`.

| Command/stage | Exit and evidence |
| --- | --- |
| `dotnet --version` | 0; exactly 10.0.402 (`sdk-version.log`) |
| `dotnet restore Parchwell.slnx --locked-mode` | 0 (`restore.log`) |
| Initial `dotnet build Parchwell.slnx --configuration Release --no-restore` | 1; MSB4166 worker-process premature exits, no source/analyzer diagnostic (`red-build.log`) |
| Same build, correction round 1 | 0; no warnings/errors with `DOTNET_PROCESSOR_COUNT=2` (`red-build-correction1.log`) |
| Focused new-class check before any host edit | 2; total 4, failed 4, succeeded 0, skipped 0 (`red.log`) |
| Initial same Release build after the exact host fix | 1; same MSB4166 worker-exit symptom (`green-build.log`) |
| Same build, correction round 2 | 0; no warnings/errors with `DOTNET_PROCESSOR_COUNT=1` and `MSBUILDDISABLENODEREUSE=1` (`green-build-correction2.log`) |
| Same focused new-class check after the fix | 0; total 4, failed 0, succeeded 4, skipped 0 (`green.log`) |

The focused commands used these exact arguments; output was redirected to the corresponding external log. The prescribed command requests no reporter, so the focused result directories are empty and its four-case counts are in the logs.

```bash
dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --no-build --results-directory /workspace/scratch/issue404-role-fix/code-tests/red -- --filter-class '*B10AbruptHostRegistrationTests'
dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --no-build --results-directory /workspace/scratch/issue404-role-fix/code-tests/green -- --filter-class '*B10AbruptHostRegistrationTests'
```

The red child/successor cases each reported `Kafka role VerifierConsumer has not been registered.` The supervisor theory and supervisor-first equality fact each reported `Unable to resolve service for type 'Parchwell.Kafka.Configuration.KafkaConfigurationFactory' while attempting to activate 'Parchwell.Scenarios.ScenarioResolver'.` These are the approved premise failures, recorded before applying the host fix. They are not unexpected verification-failure root causes.

Two correction rounds are consumed, with one distinct unexpected failure class: MSBuild worker-process termination. Resource pressure or worker lifetime/reuse is a hypothesis, not a confirmed system cause. No source, analyzer, assertion, filter, skip or repository build setting was changed to address it. The successful environment controls are retained for the required full suite. No correction round remains.

`./scripts/verify-dotnet.sh` passed, exit 0, with the stable local environment above and working managed Docker. The unmodified script passed locked restore, Release solution build/analyzers, the 62-declaration Kafka XML inventory, project manifests/architecture checks, scaffold inventory, published LoadGen apphost fixture and the warning-as-error controls. The valid warning-control counterpart passed; the intentional CS1030 warning was rejected as required.

| Validated XML report | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Parchwell.ArchitectureTests | 16 | 0 | 0 |
| Parchwell.Enrichment.Tests | 56 | 0 | 0 |
| Parchwell.Kafka.Tests | 486 | 0 | 0 |
| Parchwell.LoadGen.Tests | 1615 | 0 | 0 |
| Parchwell.Scenarios.Tests | 340 | 0 | 0 |
| Parchwell.Verifier.Tests | 7389 | 0 | 0 |

Scripted total: 9902 passed, 0 failed, 0 skipped. External detailed evidence is `/tmp/parchwell-dotnet-verification.otqhaaU6/`, with each project's `results.xml`; the driver log/exit is `code-tests/verify-dotnet.log` and `verify-dotnet.exit` under this task's evidence directory. Both the driver's summaries and the six XML aggregate records were checked by script. Full verification was not repeated after it passed.

`npm ci --ignore-scripts` passed, exit 0; log: `/workspace/scratch/issue404-role-fix/npm-ci.log`. `npm run check:docs` and `git diff --check` passed, exit 0. The documentation scan reports eight advisory warnings, all in unchanged files; they remain under one blanket disposition because they predate and are outside this four-path change. Both changed Markdown files have no warning. Final frozen-candidate scan evidence is `/workspace/scratch/issue404-role-fix/docs-frozen.log`.

## Predicate and scope proof

The predicate is whether each role in the scenario's fixed declared role set is registered in the resolving host. Green covers both roles in supervisor, child and successor; red child/successor cover an unregistered verifier with the factory present; red supervisor covers absence of the factory. The [new test](../../tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptHostRegistrationTests.cs) equality fact checks the child's 34 producer settings against the supervisor's. Numerical boundaries and empty input do not apply to this fixed set; an empty scenario role list belongs to the unchanged loader. No verifier judgement predicate changed.

The scripted path audit found only the approved four paths. The host/test hashes match the plan's literal files. Both receipt consequence bullets and all six state lines match the check's required text; their source order is retained. The committed [C5 configuration](../../tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json) has project `Parchwell.Verifier.csproj` and 17 target paths; the final change set has no intersection. No C5 target or dispatch is required.

## Consequences

The following two bullets are carried verbatim from plan-check P2-1, as the approval requires. Their namespace-failure observations were measured offline by that plan check at main `25cc301a`, rather than re-executed by this implementing session.

- The supervisor's `host.StartAsync` now runs `ValidateOnStart` for both roles' Kafka options. A namespace name that is not a DNS label now stops the supervisor at host start, before any child exists, with `System.AggregateException` wrapping one `OptionsValidationException` per role; `ReportFailure` reports it as `b10_abrupt_capture_gap exception_type=System.AggregateException`. On main the supervisor started with such a name. No failure name is added or changed.
- The child and successor now also validate `VerifierConsumer` options at start. With the inputs above, the only failing input is the namespace check they already run, but it now fails both roles, so the reported type changes from `Microsoft.Extensions.Options.OptionsValidationException` to `System.AggregateException`; the failure name stays `b10_abrupt_capture_gap`. Measured offline in the plan check on main `25cc301a`.
- No consumer, producer, token, Blob or network call is added. The verifier configuration is resolved and discarded. Additional configuration-resolution CPU work is unmeasured.

## Scope and limits

Only the host registration, its new test, this receipt and the item state may change. The held inner-chain work is excluded. No contract, failure-name, project, package, workflow, Azure, trial, C5 or scientific change is authorized.

The live cause remains unverified. The supervisor's steps after `Resolve` (`ReadExpectedBrokersAsync`, `ValidateCapturedAsync`) and the child's steps after `Resolve` have not been run live or offline. This limitation concerns the sequenced end-to-end continuations; isolated helper tests do not establish them. A fourth trial may surface another defect there; this PR does not authorize that trial.

## Review record

The first comprehensive cold review (C1, Codex, high effort) is allocated for this publication candidate against main `25cc301a`; the frozen input is the local commit containing this receipt. Its complete report and the reviewed commit identity will be linked in the PR description after the report returns. This receipt does not claim its own containing head as already reviewed. Cumulative allocated review budget: 1/2 comprehensive, 0/2 focused. Exact-head cold Claude coverage remains a mandatory separate merge gate and can use the remaining comprehensive allowance; another Codex review cannot replace it.

Verification correction rounds: 2/2; unexpected verification-failure classes: 1/10; review-finding and changed-file documentation-warning root causes: 0/10 before their review/check results. No budget is reset by publication or another session. Final-head CI and hosted code/security reviews are separate gates, recorded in the PR description.

Models: Codex authoring and implementation subagent (high effort for the subagent); Codex cold reviewer (high effort). Concrete model identifiers are not exposed by this environment. No Claude review is claimed.
