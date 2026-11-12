# Plan: shorten the verification loop (pull request 1 of 3)

Issue: [#435](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/435). This file plans the verification script pull request in full and the two later pull requests in outline. It is directly authorized governance work with no approved item plan; the authorization is Hari's chat instruction of 2026-11-12 and his answers to three questions in that session (three pull requests, xUnit traits, issue plus plan file in the pull request). No plan approval is claimed.

## 1. Goal and authority

The mandatory .NET gate costs far more than its test time. A full run stops at its first failing project and cannot resume. A few Docker or host-dependent tests decide whether a session can pass at all. This pull request keeps every check and changes how failures are reported and how a subset is selected. The default invocation stays the full gate.

Authority: AGENTS.md "Before opening a pull request" rule 13 (repeat verification only for changed behaviour, a failure, new evidence or a mandatory gate) and Hari's instruction above. Evidence: measurements in the issue and `items/435/receipt.md`.

## 2. Scope

Files this pull request may change:

- `scripts/verify-dotnet.sh`
- `scripts/test-verify-dotnet.sh` (new)
- `docs/engineering/dotnet-verification.md`
- `items/435/` (this plan, the receipt)
- A `[Trait("Tier", ...)]` attribute line, and nothing else, in the 14 test files listed in the receipt.

Must not change: any assertion, expected value, test body or fixture; `.github/`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`; `results/`; `scenarios/`; any `.csproj`, `Directory.*.props` or lock file; the Stryker configuration.

## 3. External contracts

- `./scripts/verify-dotnet.sh` with no arguments behaves as before for a passing run: the same stages, the same pass line `Verification passed. Per-project reports identify the tests executed.`, exit 0. Hosted CI (`.github/workflows/dotnet-verification.yml`, line 27) calls it that way.
- Option names: `--tier fast|all` (the issue text listed `docker`; Hari decided in chat on 2026-11-12 to leave it out, and the issue text is corrected), `--project <name>` (repeatable), `-h`/`--help`. Environment variable: `PARCHWELL_BUILD_MAX_CPU` (positive integer).
- xUnit trait key `Tier` with values `Docker`, `Slow`, `Host`.
- Exit status: 0 on success; 2 for an unusable option or environment value; otherwise the exit status of the first failed stage.

## 4. Requirements

1. WHEN no option is given THEN no test filter is passed and every test project runs, and the pass line above is printed on success.
2. WHEN `--tier fast` is given THEN every `dotnet test` call receives `--filter-not-trait Tier=Docker Tier=Slow Tier=Host`, the spike-evidence publication is skipped, and the success line starts `Partial verification passed` and says the run is not the full gate.
3. WHEN `--tier` has a value other than `fast` or `all`, or `--project` names something that is not a test project, or an unknown argument is given, or `PARCHWELL_BUILD_MAX_CPU` is not a positive integer THEN the script exits 2 before restore, before any other `dotnet` call and before any test.
4. WHEN `--project` is given THEN only the named test projects run, the architecture project runs only if named, and the success line starts `Partial verification passed`.
5. WHEN a test project or the warning control fails THEN the script runs the remaining test projects and the warning control, prints the stage timing table and a list of failed stages, and exits with the first failure's status. A stage before testing (restore, build, Kafka XML inventory, project evaluation, scaffold inventory) still stops the run at once. The scaffold inventory now runs before the architecture tests, which ran before it on `main`; its failure therefore stops the architecture tests too.
6. WHEN the warning control's intentional warning is accepted, or fails for an unrelated cause, THEN that is recorded as a failed stage `Warning control`, as before it was a hard failure.
7. WHEN `PARCHWELL_BUILD_MAX_CPU=n` is set THEN `-maxcpucount:n` is added to restore, build, project evaluation and the warning control calls, and to no `dotnet test` or `dotnet publish` call.
8. WHEN a stage runs through `run_logged` THEN its label and whole seconds are printed and appended to `stage-timing.tsv` in the results directory.
9. Trait placement: `Tier=Docker` on the classes whose every test failed with no Docker daemon in the measured run (PhantomSpikeTests, VerifierWindowComponentTests, VerifierOutcomeComponentTests, VerifierOAuthComponentTests, B10AbruptEndToEndTests) and on B10AbruptOfflineImageTests, which needs Docker when its environment variable is set; on the individual tests that failed in classes that also hold passing tests; `Tier=Slow` on the three tests that took more than 30 s; `Tier=Host` on the one cgroup test.

## 5. Invariants

- The full gate runs every test that ran before this pull request.
- A fast or partial run can never print the full-gate pass line.
- No test is skipped, deleted or loosened.

## 6. Test map

| Requirement | Test in `scripts/test-verify-dotnet.sh` | Literal expectation |
|---|---|---|
| 1 | `default` | exit 0; 6 test calls (architecture plus 5 units); log has no `filter-not-trait`; output has the full-gate line |
| 2 | `fast` | 6 test calls each with `--filter-not-trait Tier=Docker Tier=Slow Tier=Host`; output has `Partial verification passed` and not the full-gate line |
| 3 | `unknown_project`, `unknown_tier`, `bad_cpu` (value `0`) | exit 2; the stub `dotnet` log is empty |
| 4 | `select` | exactly 1 test call, for Parchwell.Scenarios.Tests; partial line |
| 5 | `fail_late` | two named projects fail with exit 2; all 6 test calls still made; exit 2; both names listed; warning control still reached |
| 6 | `warning_accepted` | exit not 0; 6 test calls; `Warning control (exit` listed |
| 7 | `cpu` (value `3`) | restore and build lines carry `-maxcpucount:3`; no test line does |
| 8, 9 | measured once in the receipt: fast tier counts equal all-tier counts minus the marked tests (Verifier 7389 to 7375, LoadGen 1808 to 1797) | counts in the receipt |

Boundaries not covered by an automated test: `PARCHWELL_BUILD_MAX_CPU=1` (run once for real, receipt) and values with a leading zero (the pattern `^[1-9][0-9]*$` rejects them; no test). Negative controls: three one-line mutations of the script were each caught by the script test (receipt).

## 7. Deployment assumptions

- A1: `--filter-not-trait` with several name=value pairs excludes a test carrying any of them. Checked by the measured counts above. Not checked in Codex's environment.
- A2: `-maxcpucount:n` is accepted by `dotnet restore`, `build`, `msbuild`. Checked once with n=1 on SDK 10.0.402. Whether it prevents the MSBuild worker exits recorded in `items/404/b10-warm-leader-receipt.md` (V2) is not verified.

## 8. Acceptance commands

1. `./scripts/test-verify-dotnet.sh` exits 0 and prints `verify-dotnet.sh checks passed.`
2. `./scripts/verify-dotnet.sh --tier fast` in a session without Docker: every project passes, exit 0, partial line.
3. `./scripts/verify-dotnet.sh` (the full gate): required by hosted CI on the final head. It cannot pass in a session without a Docker daemon, which is the problem this item addresses; the receipt records its result in this session as the 22 expected Docker and host failures and nothing else.
4. `npm run check:docs`: warnings only in files this pull request does not change.

## 9. Sources

- `scripts/verify-dotnet.sh` on `main` at a5c2403 (line 2 `set -euo pipefail`; the `run_tests` loop).
- `.github/workflows/dotnet-verification.yml` line 27.
- `docs/engineering/dotnet-verification.md` "Hosted CI and completion".
- `items/404/b10-warm-leader-receipt.md`, failure-root ledger V2, V11, V14, V15 and "Current verification checkpoint".
- `dotnet test --help` output on SDK 10.0.402, 2026-11-12.

## 10. Later pull requests (outline, not planned in detail)

- Pull request 2, governance wording. Amend the plan template's acceptance wording so an acceptance gate may be met by the fast tier locally plus the full script in hosted CI on the final head. Touches `docs/agents/factory.md` and possibly AGENTS.md and `docs/agents/pull-requests.md`. Needs its own plan; Hari merges.
- Pull request 3, workflow trigger. Run `dotnet-verification.yml` on pushes to `*-wip` branches so an agent gets the full gate on a Docker-capable runner without publishing. Needs pull request 2 merged first, because AGENTS.md hard safety rule 8 bars agents from changing existing workflow triggers, and AGENTS.md "Before opening a pull request" rule 12 records that no workflow runs on such a branch. Also touches the architecture contract tests that read workflow files if they cover this workflow. Needs its own plan; Hari merges.

## 11. Plan-check questions

(a) Effort scaling with an unknown number: none. The script runs a fixed set of projects.
(b) Where stricter than a parent plan: not applicable; there is no parent plan.
(c) Checks depending on code neither on `main` nor in this pull request: none. The script test uses a stub `dotnet`.

## 12. Open questions for Hari

1. The guard you were offered with traits, an architecture test that fails when a class uses Testcontainers without `Tier=Docker`, is not in this pull request. It needs the architecture project to load the test assemblies, which adds an assembly reference (a P1 under "Code Review Rules"). The omission is safe, because an unmarked Docker test still runs in the full gate and fails visibly in a fast run without Docker. Do you want the guard as a separate pull request?
2. `B10AbruptOfflineImageTests` returns at once and passes when `PARCHWELL_B10_OFFLINE_IMAGE` is unset. In the full gate it therefore counts as a passing test without testing anything. Outside this item's scope; reported here so you can decide.
3. Answered by Hari in chat on 2026-11-12: leave the `docker` tier out. This pull request provides `fast` and `all`. A tier that runs only the marked tests would fail the minimum-expected-tests check in projects with no marked tests, and nothing needs it while CI runs `all`.
4. Answered by Hari in chat on 2026-11-12: publish from a branch named `claude/435-verification-tiers`, which carries the issue number. The session branch `claude/modest-allen-wcadc4` holds the same commits.
5. factory.md "Item state file" has the first implementation pull request of an issue create `items/<issue>/state.md`. This pull request adds none because it is directly authorized work, not an `implement-plan` session. Do you want one?

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement, or the scope in section 2.
