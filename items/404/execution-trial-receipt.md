# B10 restart execution part 2 receipt

This pull request implements the approved trial orchestration plan. It adds the ordinary timeout Job before restart, proves its evidence stays diagnostic, and updates the four operations contracts. Hari merges the pull request; no trial or workflow dispatch is authorized here.

## Execution card

- Deliverable and scope: [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0c73e5139e1670d5b675a51fc52dbd99c4b1af49/items/404/plan-b10-execution-trial.md), sections 2 through 8.
- Current authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788005281), [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788464153), and decisions [6787849492](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787849492), [6787982173](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6787982173) and [6788215099](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788215099), point 3. The pinned file's SHA-256 was recomputed as `7339208e039f61ce1e41d4e057751aa5fba8e496cc5f5f676265a36396d41cc2`, matching the pointer, approval and task authorization.
- Mandatory gates: plan section 9; exact-head hosted Codex code and security reviews; authority refresh before publication; scope and protected-file checks. The trial remains gated by the separate offline test on main and Hari's dispatch decision.
- Exact verification commands: `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'`; `bash charts/parchwell/tests/check-loadgen-host.sh`; `./scripts/verify-dotnet.sh`; `bash infra/scripts/lifecycle/check-offline.sh`; `npm run check:docs`; `cmp .github/workflows/b10-abrupt-trial.yml docs/operations/workflows/b10-abrupt-trial.yml.txt && git diff --quiet origin/main -- .github src`.
- Earliest authorized publication checkpoint: all applicable local acceptance checks pass and the complete authored diff has clean applicable cold-review coverage under pull-request rule 8. Publish then, with CI and hosted reviews allowed to run together.
- Review budget: at most two comprehensive and two focused cold-review rounds cumulatively for this pull request, under [pull-request rule 8](../../docs/agents/pull-requests.md). Exhaustion or unsafe focus saves a backup branch and hands off to Hari. No earlier review exists for this new pull request.
- Runtime-sized work: [amendment 6788754429](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788754429), read in full, adds per-session caps of 20 verification-failure root causes and 20 review-finding root causes. Each exhaustion action is `push -wip and hand off to the next session`. A continuation session starts its own count. Scope, frozen paths, external contracts, the cumulative cold-review budget and Hari's merge remain unchanged. Before the recorded amendment, Hari directly instructed this run to fix the incomplete implementation and continue instead of handing it off at the first failure.
- Predicate map: plan section 8, with the exact P2-A and P2-B test additions below. No verifier predicate changes.

## Implementation choices required by the plan check

The [exact-revision plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6788427229) states `Blocking findings: none`. Apply all three findings as directed:

- P2-A: T-O1 records the timeout pod read before the restart render/apply, with event order `['metadata', 'loadgen', 'timeout-termination', 'loadgen-restart', 'termination', 'publication', 'loadgen-successor']`; the timeout payload has the specified ordinary owner, image, container and terminated status.
- P2-B: T-O4 includes `OSError` on the timeout pod read and expects outcome `unknown`, a safety stop, the timeout failure name and no restart.
- P2-C: both affected operations pages use the exact producer-pod wording from the check, including retained finished Jobs until cleanup. This describes the enforced sequencing accurately.

The phase implementation adds a private timeout-pod observer instead of changing the frozen restart observer. Only the ordinary pod's specified ownership, image, container/status and integer exit code are checked. Exceptions from the read become outcome `unknown` before the restart render. Existing collection mappings are reused.

The export reuses the ordinary first-worker fixture by changing only the accessibility of its nested `Fixture` and `MemoryStore` types to internal. No existing C# test method or assertion changes. The Python checker reuses the existing phase-test setup, creates ordinary C1 state, and then freezes trial markers in `phase.json`; it resets candidate files and outputs for each guarded call. All four operations documents were compared with plan section 3.6, the pinned part 1 interfaces, current ADR-021 and their corresponding implementation sources. The Kubernetes terminal-condition source was fetched on 2026-11-11.

| Requirements | Implementation and planned evidence |
| --- | --- |
| R1, R2 | Timeout-before-restart orchestration; T-O1 records both pod reads in the corrected event order |
| R3, R4, R10 | Timeout outcome branching and direct pod observation; T-O2/T-O4 include wait/apply/render failures and read `OSError` |
| R5 | Existing diagnostic collector extended to timeout mode; T-C1/T-C2 |
| R6, R7 | Unchanged production host's exported bytes through the new checker; T-S1/T-S2/T-S3 |
| R8 | Trial-only `b10d` prefix and existing scientific guard; T-P1/T-P2 |
| R9 | Four scoped operations updates with required source citations; documentation checks |

The plan section 8 predicate map, augmented by P2-A and P2-B, covers this change. No verifier predicate or production LoadGen file changes.

## Verification and specification comparison

Baseline: `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. Initial code commit: `b0e1bbc50550495efde957d316bcbbb72980e910`.

The first `./scripts/verify-dotnet.sh` attempt restored successfully, then failed the build at the new export's two async file writes. Scripted counting found two sites of the same analyzer root, xUnit1051. Both now pass `TestContext.Current.CancellationToken`; no assertion or production behavior changed. Reports are retained outside the repository at `/tmp/parchwell-dotnet-verification.7bGVImXh`.

The first phase-suite command ran 346 tests in 47.113 s and reported 38 failures and 103 errors. The controller omitted the installed tools from PATH. Scripted counting found 101 direct missing-helm errors and one each for actionlint and kubeconform; the assertion failures have not yet been independently attributed. The corrected invocation supplies the pinned tools and their Python virtual environment through PATH. No test expectation is changed to accommodate this setup failure.

The corrected phase-suite invocation passed all 346 tests in 60.275 s without a phase-code change between attempts. The corrected full .NET verification completed with zero build warnings/errors; its checked reports and a separate XML aggregation both show 10021 passed tests, zero failed, errors, skips or not-run cases. The new `B10TimeoutEvidenceExportTests` case passed and was not skipped. Reports are retained outside the repository at `/tmp/parchwell-dotnet-verification.En354LNR`.

The initial documentation scan passed with nine advisory warnings, all in unchanged files; no warning was reported in the four changed operations documents. The scan checks local links, not external URLs. Offline lifecycle/chart verification completed its workflow mirror, lint, rendering and schema checks; the command exit is recorded with the final results below.

The initial chart-host attempt passed the ordinary host groups, then failed the existing apphost guard with exit 131 because the controller had not supplied `DOTNET_ROOT` for the custom SDK installation. Setting it to the observed installation at `/home/agent/.parchwell/dotnet` fixes that setup. The unchanged command then exited 0, including the exact final timeout-evidence rejection line and the bridge's asserted producer/checker outputs.

The tool environment for phase, lifecycle and chart-host checks uses `PARCHWELL_TOOLS_DIRECTORY=/tmp/404-tools` and prepends that directory and its `venv/bin` to PATH. The repository installer verified the pinned tools and PyYAML 6.0.3; .NET SDK 10.0.402 was already installed.

## Per-session failure and finding ledger

| Root | Measured failure | Disposition |
| --- | --- | --- |
| V1 | xUnit1051 at two asynchronous export writes | Both now pass the current test cancellation token; corrected build passes |
| V2 | Controller PATH omitted the pinned tools: 101 missing-helm errors and one each for actionlint and kubeconform, with downstream assertions | Supply the pinned tool directory and virtual environment in PATH; all 346 phase tests pass |
| V3 | Existing chart-host apphost guard exited 131 because custom .NET runtime discovery had no `DOTNET_ROOT` | Supply the observed runtime root; the full chart-host bridge passes without a code change |
| R1 | Cold-review P2: the abrupt contract attributed run-ID separation to the production-byte checker | Correct the prose to attribute separation to T-P1's phase test |

Current distinct verification roots: 3 of 20. Current review-finding roots: 1 of 20. The counts cover this session from its initial attempts, not just attempts after the amendment. These caps do not replace pull-request rule 8's cumulative cold-review limits.

## Acceptance results

Runtime code was tested from `d224f33d9ca2eaf0af7228c5231e1aac5fdb3a40`. Documentation and host/lifecycle checks used `8753c175a066e2371d6a020f1a8052e5e3f4abcc`, whose runtime code is identical. Later authored edits record evidence and current item state only.

| Command from plan section 9 | Result |
| --- | --- |
| `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Exit 0; 346 tests passed, including the added byte-independent phase cases |
| `bash charts/parchwell/tests/check-loadgen-host.sh` | Exit 0 with the exact required final line; producer-byte bridge and timeout checker outputs asserted |
| `./scripts/verify-dotnet.sh` | Exit 0; 10021 passed tests; zero build warnings/errors and no failed/skipped/not-run tests; new exporter passed |
| `bash infra/scripts/lifecycle/check-offline.sh` | Exit 0; workflow byte equality, lint, chart and schema checks passed |
| `npm run check:docs` | Exit 0; nine advisory warnings in unchanged files; none in the changed operations documents |
| `cmp .github/workflows/b10-abrupt-trial.yml docs/operations/workflows/b10-abrupt-trial.yml.txt && git diff --quiet origin/main -- .github src` | Exit 0 |

A separate static comparison found all 12 changed paths permitted, all seven frozen `Trial` methods unchanged, and the existing C# test file identical except for the two allowed fixture-type accessibility changes. No program output is committed. No trial or workflow was dispatched. No verifier production code, expected outcome, verdict, result, workflow, chart template or package changed.

## Cold-review inputs and record

Round 1 was a fresh-context comprehensive review of `ad269dba65c6f4630cf53a7f07b928f54a9253d2` against baseline `3a4ee0b140a209c2cdeae0002a77c5349a6aabc7`. Its complete report found no P0 or P1 and no missing requirement or scope creep. It found one P2: `b10-abrupt-contract.md` attributed run-ID separation to the production-byte checker instead of the phase test. That prose correction is applied. The reviewer independently checked all changed paths, the frozen methods and fixture differences, retained verification reports and the official Kubernetes terminal-condition source.

The next round is the single focused P2-correction review required by pull-request rule 8 after a first clean comprehensive review. Its comparison revision is R1's `ad269dba65c6f4630cf53a7f07b928f54a9253d2`; the full PR diff against the baseline remains context. Its input revision is identified by the full SHA in both diff filenames. It reviews the prose correction, updated evidence/state records and affected surrounding contracts, reporting only P0 or P1. It does not repeat the comprehensive review. If it reports P0 or P1, revert the post-clean P2 changes and apply rule 8's remaining instructions.

Cumulative completed comprehensive/focused counts before the focused round: 1/0. The pull request self-review record holds the complete reports, dispositions, final-head CI and hosted review evidence, so the final report is not added to its own reviewed receipt. Runtime code and the passing acceptance evidence remain unchanged after R1.

Authoring used this Codex control session and fresh-context subagents at high effort. Their exclusive areas were phase orchestration/tests, C# export, Python checker/host bridge, and operations documents. The cold reviewer also runs with fresh context at high effort. Exact model identifiers are not exposed here.
