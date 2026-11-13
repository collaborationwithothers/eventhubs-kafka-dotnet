# Plan: order the verification gates and add the pre-PR local tier (gate-order)

Issue: [#440](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440). This contract-grade plan covers the whole issue as one pull request, `gate-order`. It writes the adopted gate order into `docs/agents/pull-requests.md`, makes `CLAUDE.md` and the `implement-plan` skill cite that one statement, and adds the `verify-dotnet.sh` tier that the adopted pre-PR gate needs, because no existing option expresses it. Nothing is open for Hari.

Revision: r2. Planned against `main` at `9b13ce03d916bbd14e8d92445966fcfc09f34cfa`. r1 was planned against `3cb6875b8af8daa0269bb6495134b83e91353153`; `git diff --stat 3cb6875b 9b13ce03` lists no file this plan changes or cites by line, so every line citation below holds on both commits.

## 1. Goal and authority

Goal: a full local test run starts only on source that cold review has cleared, and that run excludes only the tests a managed sandbox cannot host. Today `CLAUDE.md` "Before opening a pull request" item 1 asks for the full local suite before the cold review of item 2, so a review finding after the run throws the run away. The issue records two such full gates on pull request 439 that cold review findings stopped: gate 2, which ran 18:46:14 to 19:03:32 UTC (17 min 18 s, computed by script), and gate 3, whose start was not recorded. Gate 1 was lost to a workspace restart, not to a review finding.

Authority, in creation order:

1. The issue body (created 2026-11-13T07:05:50Z, `updated_at` 2026-11-13T08:27:59Z). Hari applied `ready-for-agent` at 2026-11-13T07:12:10Z; no comment on the issue names a tool, so Claude Code implements (docs/agents/factory.md "Item lifecycle" step 2, AGENTS.md "Implementer and reviewer"). The body states the evidence and proposes four changes.
2. Decision [6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415) (created 2026-11-13T08:27:59Z), headed "## Decision". It adopts proposals 1 and 2 of the body with this wording: gate order "focused tests, then cold review, then the pre-PR local gate once, on the reviewed source. No local gate starts while a review round is open on the same source."; pre-PR local gate "`./scripts/verify-dotnet.sh` with every test except `Tier=CGroupV2` (named `Tier=Host` until [#445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445) merges), recorded in the receipt as a partial run with that exclusion. Docker-tier tests stay in the local gate."; pre-merge gate "the hosted `.NET verification` full gate on the final head, every test including the cgroup tier. The merge rule is unchanged."; and the implementing scope "amends CLAUDE.md "Before opening a pull request" item 1, `docs/agents/pull-requests.md` and the `implement-plan` skill to match, and adds the script option the exclusion needs, if `--project` and `--tier` cannot express it". It closes: "Proposals 3 and 4 of the body are not decided here."
3. Decision [6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826) (created 2026-11-13T14:38:44Z), headed "## Decision", answering the plan check of r1. B1: "the pre-PR local gate reruns only for changes made before the pull request opens. After it opens, a correction needs cold-review coverage under docs/agents/pull-requests.md rule 8, and the hosted `.NET verification` on the final head is its verification gate; no local rerun is required." B2: "apply the plan check's citation text for CLAUDE.md item 1 and the rule 8 sentence". It also directs: "Apply the check's P2 findings in the same revision." It thereby adopts that text and those findings of [plan check 6854556195](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6854556195).

No approval comment exists on the issue. No item state file exists (`items/440/state.md` is absent on `main`). There is no parent item and no split, so the whole issue is the candidate (docs/agents/factory.md "Item lifecycle" step 3).

Issue #445's pull request, [451](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/451), merged at 2026-11-13T12:37:13Z as `112a63ba`; issue #445 itself is still open. On `main` the trait value is `CGroupV2` (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs` line 21, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` line 12, `scripts/verify-dotnet.sh` line 58), and `git grep CGroupV2 -- tests src` finds no other mark. The decision's parenthesis about `Tier=Host` is therefore spent, and this plan uses `Tier=CGroupV2` throughout.

Why a new option is needed. `scripts/verify-dotnet.sh` lines 56 to 63 offer two tiers: `all` adds no filter, `fast` passes `--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2`. `fast` also removes the Docker tests, which the decision keeps in the local gate, so it cannot express the adopted gate. `--project` selects test projects, not traits (lines 73 to 88, 341 to 349), and the two `Tier=CGroupV2` tests sit in `Parchwell.LoadGen.Tests` beside tests the gate must run, so dropping that project would drop far more than the exclusion. The condition in the decision ("if `--project` and `--tier` cannot express it") is therefore met.

## 2. Scope

Files this pull request may change:

- `docs/agents/pull-requests.md`: one new section, "Verification gates" (R1), the first sentence of line 3 (R1), and the two cross-reference sentences of R2. No rule text changes otherwise. Line 5 ("The text moved here unchanged from AGENTS.md ...") stays: it describes the move, and the new section's own "Basis:" line names where its text comes from.
- `CLAUDE.md`: item 1 of "Before opening a pull request" only (R3).
- `.agents/skills/implement-plan/SKILL.md`: steps 2 and 3 only (R4). `.claude/skills/implement-plan` is a symlink to this folder (`ls -la .claude/skills`), so no second copy is edited.
- `scripts/verify-dotnet.sh`: the usage text, the tier `case`, the unknown-tier message and the spike-evidence condition (R5 to R8).
- `scripts/test-verify-dotnet.sh`: the new cases and added assertions of section 6.
- `docs/engineering/dotnet-verification.md`: "Tiers, partial runs and failure reporting" and the `tier` row of the `summary.json` key table (R9).
- `items/440/`: this plan file, the authored receipt and the item state file.

Must not change:

- `AGENTS.md`. Its "Before opening a pull request" already says that rules 1 to 10 live in `docs/agents/pull-requests.md`, and this plan adds a named section, not a numbered rule, so no renumbering is needed and rules 11 to 13 keep their meaning.
- Anything under `.github/workflows/` and `docs/operations/workflows/` (AGENTS.md hard safety rule 8). The hosted gate is unchanged.
- `scripts/check-dotnet-matrix.py` and `scripts/tests/test_check_dotnet_matrix.py`. The aggregate checker requires `tier == "all"` in each hosted job's `summary.json` (`scripts/check-dotnet-matrix.py` line 57); hosted jobs pass only `--project` (`.github/workflows/dotnet-verification.yml` line 39), so the new tier never reaches it.
- Any .NET source, test, `.csproj`, `Directory.*.props`, lock file, `global.json`, `scripts/dotnet-verification.proj`, `scripts/verify-kafka-coverage.sh`, `scripts/verify-worker-images.sh`.
- `results/`, `scenarios/`, any `expected.yaml`, `docs/blueprint.md`, `skills-lock.json`, any other skill.

Non-goals, from decision 6847972415's last line:

- Proposal 3 of the issue body (plan-check lists the existing assertions and fixtures the planned checks will break) is not implemented. The `plan-check` skill and `docs/agents/factory.md` are not changed. This plan's own standing fixture rule in section 6 is the `plan-pr` skill's "Plan rules" rule 2, not that proposal.
- Proposal 4 of the issue body (control-session rules on analyzer-only builds, per-test triage and resuming agents after an environment transition) is not implemented.

Protected paths: every file this pull request changes except `items/440/` is protected. `.github/CODEOWNERS` on `main` lists `/docs/`, `/.*/` (which covers `.agents/`), `/*.md` (which covers `CLAUDE.md`) and `/scripts/`, and mirrors the ruleset's required reviewers rule (docs/agents/factory.md "Ruleset"). The pull request therefore waits for Hari's approval and merge under AGENTS.md GOVERNANCE "Merge rule"; the implementing session never merges it.

## 3. External contracts

### 3.1 The published gate order

`docs/agents/pull-requests.md` gains one section, placed after rule 10 at the end of the file. This is the single statement of the rule; `CLAUDE.md`, the `implement-plan` skill, `scripts/verify-dotnet.sh` and `docs/engineering/dotnet-verification.md` cite it by name and never restate the order (docs/agents/factory.md "Item lifecycle" step 3; AGENTS.md "Skills" for the skill). Exact text:

```markdown
## Verification gates

This section owns the order of the verification gates and what each one is. Other files cite it rather than restating it. Basis: [decision 6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415) on issue #440, which adopts proposals 1 and 2 of that issue's body, and [decision 6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826) on the same issue, which sets when the pre-PR local gate runs again. The order is a governance choice; the saving it is expected to produce is not measured.

1. Focused tests. Run the tests that cover the change while the change is still moving. `./scripts/verify-dotnet.sh --tier fast` and `./scripts/verify-dotnet.sh --project <name>` are partial runs for this step, and are cited as partial runs.
2. Cold review, under rule 8.
3. The pre-PR local gate, once, on the source the cold review cleared: `./scripts/verify-dotnet.sh --tier sandbox`, which runs every test except those marked with the xUnit trait `Tier=CGroupV2`. The Docker tests stay in it. The authored receipt records it as a partial run with that exclusion, never as the full gate.
4. The pre-merge gate: the hosted `.NET verification` check on the pull request's final head, which runs every test, the `Tier=CGroupV2` tests included. GOVERNANCE "Merge rule" is unchanged.

No local gate starts while a review round is open on the same source. A review round is open from the moment its inputs are frozen under rule 5 until its complete report arrives. A change made after step 3 and before the pull request opens is authored work: it needs the coverage rule 8 requires, and step 3 then runs again on the source that coverage cleared. After the pull request opens, a correction, including one from a review finding or a hosted finding, needs the cold-review coverage rule 8 requires, and step 4 on the final head is its verification gate; step 3 need not run again.

Steps 1 and 3 apply to a pull request that changes .NET source or tests. For any other change, rule 13 decides which verification suits it, and rule 8's documentation checks apply as they do to every pull request.
```

### 3.2 Cross-reference sentences in `docs/agents/pull-requests.md`

Exact text of each change outside the new section:

- Line 3, first sentence, becomes: `This file holds the rules for work that a pull request will publish: the execution card, the predicate map, rules 1 to 10 of AGENTS.md "Before opening a pull request" (including the cold-review protocol and budget in rule 8) and the order of the verification gates.` The rest of line 3 is unchanged.
- The paragraph that begins "Before implementation, put a short execution card" gains, as its last sentence: `The mandatory gates and their order are set in "Verification gates" below.`
- The paragraph of rule 8 that begins "**Cold-review protocol and cumulative budget.**" gains, as its last sentence: `The order of the cold review and the verification gates is set in "Verification gates" below.`

### 3.3 `CLAUDE.md` item 1 and the `implement-plan` skill

`CLAUDE.md` "Before opening a pull request" item 1 reads exactly (decision 6856109826 B2, adopting the text of plan check 6854556195 B2):

```markdown
1. Run the verification gates in the order docs/agents/pull-requests.md "Verification gates" sets; in an interactive session, its cold review runs as item 2 describes. Warnings are errors for builds; documentation warning dispositions follow AGENTS.md "Before opening a pull request" rule 8. Verification repetition follows that section's rule 13.
```

Step 2 of `.agents/skills/implement-plan/SKILL.md` reads exactly (its existing text, with one sentence added after the second):

```markdown
2. Implement on the branch under docs/agents/factory.md "Item lifecycle" step 3, including its reference to step 1's `apply in implementation` findings. Use subagents at high effort, including for the cold review under AGENTS.md "Before opening a pull request" rule 8. Run the verification gates in the order docs/agents/pull-requests.md "Verification gates" sets. Readers follow factory.md "Authority reads" "Reader subagents". Use rule 13 for long commands. Create or update the item state file at the time factory.md "Item state file" sets. For a contract-grade plan file, read it at the commit the approved plan comment names; a plan held as a comment is read from that comment. Apply the plan's stop rule under factory.md "Item lifecycle" step 3, and record in the authored receipt each choice factory.md "Item lifecycle" step 1 leaves to the implementer. Done when every requirement in the plan is met, every acceptance command has produced its stated evidence, and the state file is current.
```

Step 3 of the same file reads exactly (its existing text, with one sentence added at the end):

```markdown
3. Before each push, pull request creation or merge, refresh authority under factory.md "Authority reads". Repeat steps 1 and 2 for work affected by a change. Open the pull request and carry it to merge or handoff under factory.md "Item lifecycle" step 3 and AGENTS.md "Before opening a pull request". The pull request's pre-merge gate is step 4 of docs/agents/pull-requests.md "Verification gates".
```

These sentences cite the section by file and name and paraphrase none of its gates, as AGENTS.md "Skills" requires of a repository-owned skill.

### 3.4 `verify-dotnet.sh` command line

The script accepts `--tier sandbox` and `--tier=sandbox`, alongside the existing `all` and `fast`. Its behaviour:

| Property | Value for `sandbox` |
|---|---|
| Filter passed to every `dotnet test` call | `--filter-not-trait Tier=CGroupV2`, and no other filter argument |
| Spike-evidence publication | runs, as for `all` |
| Final line | the partial line, `Partial verification passed (tier sandbox, projects: ...). This is not the full gate: it does not satisfy a requirement to run ./scripts/verify-dotnet.sh with no options.` |
| `summary.json` `tier` | `"sandbox"` |
| `summary.json` `outcome` on a clean run | `"partial_passed"` |

The unknown-tier message becomes exactly `Unknown tier: <value> (expected all, fast or sandbox).` on stderr, with exit status 2 and no `dotnet` call, as today.

The usage text printed by `--help` becomes exactly the following. Against `main`, the synopsis, the `--tier sandbox` lines and the second-last line change, and the description column of the `--tier all`, `--tier fast` and `--project` lines moves right by two spaces to align with `--tier sandbox`; the other lines are unchanged.

```text
Usage: ./scripts/verify-dotnet.sh [--tier fast|sandbox|all] [--project <test project name>]...

With no options the script runs the full gate: every test project, every test.
  --tier all      Run every test (default).
  --tier fast     Leave out tests marked with the xUnit trait Tier=Docker, Tier=Slow or Tier=CGroupV2.
  --tier sandbox  Leave out only tests marked with the xUnit trait Tier=CGroupV2, which need the host's
                  cgroup v2 memory files. The pre-PR local gate of docs/agents/pull-requests.md "Verification gates".
  --project       Run only the named test project, for example Parchwell.Verifier.Tests. Repeatable.
Environment:
  PARCHWELL_BUILD_MAX_CPU  Positive integer. Adds -maxcpucount:<n> to restore, build and project
                           evaluation only. Tests keep the machine's real processor count.
A run with --tier fast, --tier sandbox or --project prints that it is not the full gate.
Each run writes summary.json and stage-timing.tsv to its results directory.
```

### 3.5 Two sentences of `docs/engineering/dotnet-verification.md`

Line 50's last sentence, today "These options exist for quicker feedback and never replace it:", becomes exactly:

```markdown
These options give partial runs, for quicker feedback or, with `--tier sandbox`, for the pre-PR local gate, and never replace it:
```

Line 58's sentence, today "A run with either option prints `Partial verification passed` and states that it is not the full gate.", becomes exactly:

```markdown
A run with `--tier fast`, `--tier sandbox` or `--project` prints `Partial verification passed` and states that it is not the full gate.
```

The rest of R9 states facts the section must carry and leaves their wording to the implementer.

### 3.6 Runner option

`--filter-not-trait` is the xUnit v3 runner option already used by the `fast` tier. Its help text on SDK 10.0.402, read in r1's session (section 9), is "Do not run any methods with a given trait value. Pass one or more name/value pairs (i.e., 'name=value')", so one pair is a valid argument list. The plan check of r1 also read "Do not run any test with a given trait value" on the xUnit v3 Microsoft Testing Platform page.

## 4. Requirements

- **R1 Gates section.** WHEN the pull request is complete, THEN `docs/agents/pull-requests.md` ends with the section of 3.1, byte for byte; line 3's first sentence is the text of 3.2; and no rule text in that file has changed other than that sentence and R2's two sentences.
- **R2 Gates section is reachable.** THEN the execution-card paragraph and the "Cold-review protocol and cumulative budget" paragraph of rule 8 in `docs/agents/pull-requests.md` each end with the sentence 3.2 gives for it, byte for byte. Neither sentence restates the order.
- **R3 CLAUDE.md item 1.** THEN item 1 of `CLAUDE.md` "Before opening a pull request" is the line of 3.3, byte for byte, and no other line of `CLAUDE.md` changes.
- **R4 implement-plan skill.** THEN steps 2 and 3 of `.agents/skills/implement-plan/SKILL.md` are the blocks of 3.3, byte for byte, and no other line of that file changes. In particular, step 2 keeps word for word its first sentence ("Implement on the branch under ... `apply in implementation` findings.") and its last ("Done when every requirement in the plan is met, ...").
- **R5 New tier accepted.** WHEN the script is run with `--tier sandbox` or `--tier=sandbox`, THEN every `dotnet test` call receives `--filter-not-trait Tier=CGroupV2` and no other filter argument, and the run is otherwise the run that `--tier all` performs for the same `--project` selection.
- **R6 Unknown tier.** WHEN `--tier` names any value other than `all`, `fast` or `sandbox`, THEN the script prints `Unknown tier: <value> (expected all, fast or sandbox).` to stderr, makes no `dotnet` call, creates no results directory and exits 2.
- **R7 Spike evidence.** WHEN the Kafka test project runs and the tier is `all` or `sandbox`, THEN the script publishes the spike evidence as it does today for `all`. WHEN the tier is `fast`, THEN it does not. The comment above that condition states the reason: the evidence comes from the Docker spike test, which only the fast tier leaves out.
- **R8 Usage text.** WHEN the script is run with `--help`, THEN it prints the usage text of 3.4 byte for byte, exits 0 and creates no results directory. The `--tier sandbox` description line starts with two spaces and `--tier sandbox`, in the format of the other option lines.
- **R9 Engineering doc.** THEN `docs/engineering/dotnet-verification.md` states, in "Tiers, partial runs and failure reporting": that `--tier sandbox` passes `--filter-not-trait Tier=CGroupV2` to every test project and leaves the Docker and Slow tests in; that it publishes the spike evidence, as `--tier all` does, and that `--tier fast` does not; that a `sandbox` run prints the partial line and is cited as a partial run; and that it is the pre-PR local gate defined in docs/agents/pull-requests.md "Verification gates", linked, which is where the rule lives. Line 50's last sentence and line 58's partial-run sentence become the texts of 3.5, byte for byte. The code block after line 50 gains the line `./scripts/verify-dotnet.sh --tier sandbox` after the `--tier fast` line. The `tier` row of the `summary.json` key table becomes `"all"`, `"fast"` or `"sandbox"`. The sentence that `./scripts/verify-dotnet.sh` with no options is the full gate is kept.
- **R10 Item state file.** THEN `items/440/state.md` exists in the fields and order of docs/agents/factory.md "Item state file", listing the issue body, decision 6847972415 and decision 6856109826 under "Decisions in force", "Open questions: None", and the implementing tool Claude Code with the source "no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default".

## 5. Invariants and safety rules

- The hosted pre-merge gate is unchanged: no file under `.github/workflows/` is touched, so AGENTS.md hard safety rules 8 and 9 are not engaged, and the required check keeps its name and its full-test run.
- No gate is weakened. The set of tests the pre-PR local gate runs grows against today's practical alternative on a sandbox (`--tier fast`), and every test still runs in the hosted gate before merge. The only tests the new tier never runs locally are the two marked `Tier=CGroupV2`. After the pull request opens, corrections keep cold-review coverage under rule 8 and the hosted gate on the final head (decision 6856109826 B1).
- The rule is stated once, in `docs/agents/pull-requests.md`; `CLAUDE.md`, the skill, the script's usage text and the engineering doc cite it by name and do not restate the order (docs/agents/factory.md "Item lifecycle" step 3).
- No number in the new text is a measurement. The only figures cited stay in the issue, and the published text calls the order a governance choice (AGENTS.md hard safety rule 11).
- No secret, credential, tenant or subscription identifier enters any changed file (hard safety rule 7). No Azure access is involved (hard safety rules 1 to 3).
- The pull request touches protected paths, so the implementing session does not merge it; it hands off to Hari under AGENTS.md GOVERNANCE "Handoffs" after Codex review.

## 6. Test map

All automated tests are cases of `scripts/test-verify-dotnet.sh`, which runs against the stub `dotnet` already in that file and needs no SDK and no Docker. `run_case` writes stdout and stderr together to `$work/<case name>.out` (line 79). Each new case changes one thing against the existing fixtures, so no other behaviour can mask it. Each assertion of new behaviour (T1 to T5 and T8 to T10) fails against `main`'s script; T6 and T7 pin existing behaviour that R7 must keep.

| Test | Requirement | Input | Expected |
|---|---|---|---|
| T1 | R5 | `run_case sandbox -- --tier sandbox` | exit 0; 6 `TEST ` lines; each of the 6 contains `--filter-not-trait Tier=CGroupV2`; the log contains neither `Tier=Docker` nor `Tier=Slow` |
| T2 | R5 | the T1 run | stdout contains `Partial verification passed (tier sandbox, projects: all). This is not the full gate` and not `Verification passed. Per-project reports` |
| T3 | R5 | the T1 run's `summary.json` | `.outcome == "partial_passed" and .tier == "sandbox" and .projects == null` |
| T4 | R5 | `run_case sandbox_equals -- --tier=sandbox` | exit 0; the same 6 filtered `TEST ` lines as T1; `.tier == "sandbox"` |
| T5 | R7 | the T1 run | the log holds exactly 1 line matching `msbuild scripts/dotnet-verification.proj -t:PublishSpikeEvidence` |
| T6 | R7 | the existing `default` case (tier `all`) | the same count is exactly 1 |
| T7 | R7 | the existing `fast` case | the same count is exactly 0 |
| T8 | R6 | the existing `unknown_tier` case, `--tier slow` | exit 2; no `dotnet` call; no results directory; `$work/unknown_tier.out` is exactly `Unknown tier: slow (expected all, fast or sandbox).` |
| T9 | R8 | the existing `help` case | exit 0; `$work/help.out` contains `[--tier fast|sandbox|all]`; `grep -Eq '^ *--tier sandbox .*Tier=CGroupV2' "$work/help.out"` succeeds; `$work/help.out` contains the line `A run with --tier fast, --tier sandbox or --project prints that it is not the full gate.`; the existing `summary.json` assertion still passes |
| T10 | R5 | `run_case sandbox_project -- --tier sandbox --project Parchwell.Scenarios.Tests` | exit 0; exactly 1 `TEST ` line, for `Parchwell.Scenarios.Tests`, carrying the `Tier=CGroupV2` filter; `.tier == "sandbox" and .projects == ["Parchwell.Scenarios.Tests"]` |

T9's `grep` pattern needs a line that starts with optional spaces and `--tier sandbox`; on `main` the only line naming `Tier=CGroupV2` starts with `--tier fast` (line 13), so the pattern fails there. T8 and T9 add assertions to cases that exist on `main` (lines 154 to 165 of `scripts/test-verify-dotnet.sh`); T6 and T7 add assertions to the `default` and `fast` cases (lines 106 to 134). No existing assertion is removed or loosened.

R1 to R4, R9 and R10 are text requirements with no automated test. They are checked by the acceptance commands A3, A5 and A6 and by the cold review against this plan.

Standing fixture rule. When an existing test's fixture is rejected by a check this plan adds, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it. The cold review and Codex review check each listed change. (Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2, as the `plan-pr` skill's "Plan rules" rule 2 requires.) No case on `main` is expected to need it: the new tier adds a value and changes no existing path except the spike condition, whose current behaviour for `all` and `fast` T6 and T7 pin, and the help text, whose only existing assertion (the `summary.json` sentence) R8 keeps.

## 7. Deployment assumptions

No container, image, chart or cluster is involved. Two assumptions about environments outside this pull request remain, and recorded output settles both, so neither is open. No network connection is under test in either, so the connection state of `plan-pr` "Plan rules" rule 3 does not apply; each row states how its evidence was taken and whether load was recorded.

| Assumption | Recorded evidence |
|---|---|
| The hosted gate runs and passes the `Tier=CGroupV2` tests, so step 4 of section 3.1 is true as published | Current workflow: run [43432170016](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43432170016) (`push` to `main` at `3cb6875b`, conclusion `success`). Its job 131908094495 `.NET tests (Parchwell.LoadGen.Tests)` and its aggregate job 131914367582 `.NET verification` concluded `success`. Its artifact `dotnet-verification-Parchwell.LoadGen.Tests-attempt-1` (id 14042441256) holds `summary.json` with `tier` `"all"`, `projects` `["Parchwell.LoadGen.Tests"]` and `source_sha` `3cb6875b8af8daa0269bb6495134b83e91353153`, and `results.xml` in which both tests carrying `Tier=CGroupV2`, `B10AbruptLocalCgroupCaptureTests.Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` and `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes`, have `result="Pass"` (read by script in this session). The workflow's test step is `./scripts/verify-dotnet.sh --project "$PARCHWELL_TEST_PROJECT"` (`.github/workflows/dotnet-verification.yml` line 39 on `main`, unchanged since `3cb6875b`), which adds no trait filter. Earlier, before the per-project matrix: run [43426723220](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43426723220) on `main` at `112a63ba`, job 131889819833 `.NET verification` `success`, whose step ran `./scripts/verify-dotnet.sh` with no options (`.github/workflows/dotnet-verification.yml` at `112a63ba`, line 27). How taken: recorded output of completed hosted runs, read after the fact, not a live probe; the runner's load during those runs was not recorded. |
| A managed cloud sandbox can fail the `Tier=CGroupV2` tests, which is why the pre-PR gate excludes them | `items/445/receipt.md` command 6 (line 30): `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests` reported 2 failures out of 1810, both carrying `Tier=CGroupV2` and printing `b10_abrupt_cgroup_unavailable`; no failing test lacked the trait. How taken: recorded output of a full run of that project in the cloud session of pull request 451. The receipt lists the run after command 5, which started the Docker daemon (line 29); that the run came after it is inferred from the list order. The receipt does not record the load during the run. |

Acceptance command A2 runs on the implementing session's own workspace. Whether that workspace has a reachable Docker daemon is not assumed: `docs/engineering/dotnet-verification.md` line 70 records that the `SessionStart` hook starts `dockerd` and that `docker info` fails until it is ready. A2's disposition below covers the case where it is not.

## 8. Acceptance commands

| # | Command | Evidence it must produce |
|---|---|---|
| A1 | `./scripts/test-verify-dotnet.sh` | ends with `verify-dotnet.sh checks passed.`; T1 to T10 among its cases; output in the receipt |
| A2 | `./scripts/verify-dotnet.sh --tier sandbox` | the last stage line, the partial line naming tier `sandbox`, and `summary.json` with `tier` `"sandbox"`, `outcome` `"partial_passed"`, `failed_stages` `[]`; by script over each `results.xml`, 0 executed tests carrying the trait `Tier=CGroupV2` and more than 0 executed tests in `Parchwell.LoadGen.Tests`; the exclusion recorded in the receipt as a partial run |
| A3 | `npm run check:docs` | each warning in a file this pull request changes fixed or explained, within the cap of section 10 (a); warnings in other files covered by one blanket line (rule 8) |
| A4 | `git status --porcelain` and `git diff --stat origin/main...HEAD` before the push | no path under `.github/workflows/`, `docs/operations/workflows/`, `results/`, `scenarios/` or `AGENTS.md` in the diff; output in the receipt |
| A5 | a script that extracts the new section of `docs/agents/pull-requests.md` and compares it with the block in section 3.1 of this plan | byte-equal; output in the receipt |
| A6 | a script that compares, byte for byte, each other pinned text of sections 3.2 to 3.5 with the changed file (the three `pull-requests.md` sentences, `CLAUDE.md` item 1, the two skill steps, the usage text from `./scripts/verify-dotnet.sh --help`, and the two `dotnet-verification.md` sentences) | each comparison equal; output in the receipt |

A2 is this pull request's own pre-PR local gate under the rule it publishes, run after the cold review clears the source. If the workspace has no reachable Docker daemon when A2 runs, the implementer starts it as `docs/engineering/dotnet-verification.md` line 70 describes and waits for `docker info` to succeed before A2; if it still cannot be reached, A2 is recorded as blocked with the exact error and the session hands off under AGENTS.md GOVERNANCE "Handoffs", because a partial run that silently drops the Docker tests is not the gate this plan publishes.

## 9. Sources

r1 sources were read 2026-11-13 in r1's session; r2's additions were read 2026-11-13 in this session. Tools: `gh api` REST routes for the issue, its comments, its events, pull request 451, issue 445, and the Actions runs, jobs and artifacts (artifact 14042441256 downloaded with `gh api .../actions/artifacts/14042441256/zip` and read with a Python script); `git show`, `git diff` and file reads for `main` at `3cb6875b` and `9b13ce03`; `npm run check:docs` on `main` at `9b13ce03`; `dotnet test --help` on the workspace SDK (r1); `curl` for the pinned Microsoft Testing Platform page (r1).

- Issue #440 body, decision [6847972415](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6847972415), decision [6856109826](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6856109826) and plan check [6854556195](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/440#issuecomment-6854556195), quoted in section 1. The `ready-for-agent` label event is 2026-11-13T07:12:10Z, the only `labeled` event on the issue.
- Pull request [451](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/451): `merged_at` 2026-11-13T12:37:13Z, `merge_commit_sha` `112a63ba945ecaa9d70fdd7b360fb1b6d1e92976`. Issue #445: `state` `open`.
- `main` (identical at `3cb6875b` and `9b13ce03` for these files): `scripts/verify-dotnet.sh` lines 7 to 21 (usage, with line 18 the partial-run sentence), 56 to 63 (tier case and unknown-tier message), 73 to 88 and 341 to 349 (`--project`), 306 to 339 (`run_tests`, with the spike condition at 325 to 330), 449 to 455 (final lines); `scripts/test-verify-dotnet.sh` lines 17 to 61 (stub), 67 to 85 (`run_case`, output file at line 79), 106 to 134 (`default` and `fast` cases), 145 to 165 (`select`, `unknown_project`, `unknown_tier`, `bad_cpu`, `help`); `scripts/check-dotnet-matrix.py` line 57; `.github/workflows/dotnet-verification.yml` line 39; `docs/agents/pull-requests.md` whole file; `CLAUDE.md` lines 28 to 36 (item 1 at line 32); `.agents/skills/implement-plan/SKILL.md` whole file (steps 2 and 3 at lines 17 and 18); `docs/engineering/dotnet-verification.md` lines 48 to 97 (lines 50 and 58 quoted in R9) and 155 to 169; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs` line 21; `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` line 12; `.github/CODEOWNERS`.
- `dotnet test --help`, SDK 10.0.402 on r1's workspace, 2026-11-13: `--filter-not-trait` "Do not run any methods with a given trait value. Pass one or more name/value pairs (i.e., 'name=value'). Wildcard '*' is supported at the beginning and/or end of the trait name and/or value. Note: Specifying more than one is an AND operation."
- [The pinned Microsoft Testing Platform command page](https://github.com/dotnet/docs/blob/b6c9cb41353de8cd352c499be9e49ea6b4825bae/docs/core/tools/dotnet-test-mtp.md), fetched with `curl` at that commit: it does not mention `--filter-not-trait`, which is a runner option passed after `--`, so the help output above is this plan's source for that option, as `docs/engineering/dotnet-verification.md` line 58 already records for the `fast` tier.
- Run [43432170016](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43432170016), its jobs 131908094495 and 131914367582 and its artifact 14042441256; run [43426723220](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43426723220), its job 131889819833, and `.github/workflows/dotnet-verification.yml` at `112a63ba` line 27; all for the hosted assumption in section 7.
- `items/445/receipt.md` lines 29 and 30 (commands 5 and 6) and line 32 (command 7), for the sandbox assumption in section 7.
- `npm run check:docs` on `main` at `9b13ce03`, 2026-11-13: 9 warnings, none in a file this plan changes (`CLAUDE.md`, `docs/agents/pull-requests.md`, `docs/engineering/dotnet-verification.md` and `.agents/skills/implement-plan/SKILL.md` each "no issues found").
- Governance read for this plan: AGENTS.md GOVERNANCE "Hard safety rules" 7 to 11, "Merge rule", "Handoffs" and "Skills"; AGENTS.md "Before opening a pull request" rules 11 and 13; docs/agents/pull-requests.md rules 5, 8 and 10; docs/agents/factory.md "Authority reads", "Item state file", "Item lifecycle" steps 1 to 3 and "Ruleset"; the `plan-pr` skill's "Plan rules".

## 10. Plan-check questions

(a) Effort that scales with a number unknown at planning time: the number of test failures A2 reports, and the number of `npm run check:docs` warnings in files this pull request changes (A3).

- Cap for A2: the implementer triages the failures once by script into those carrying `Tier=CGroupV2` (which R5 makes impossible) and the rest; a failure this pull request did not cause is not fixed here. Exhaustion action: `push -wip and hand off to the next session`, after recording the count and an estimate of the remaining time under AGENTS.md hard safety rule 11 (AGENTS.md "Before opening a pull request" rule 11).
- Cap for A3: at most 5 warnings in files this pull request changes are disposed of, each fixed or explained. On `main` at `9b13ce03` those files carry 0 warnings (section 9), so the cap allows for warnings the new text introduces; the number 5 is a policy choice, not a measurement. Warnings in other files take one blanket line and do not count against the cap (rule 8). Exhaustion action: `push -wip and hand off to the next session`, recording the count, because rule 8 requires every warning in a changed file to be fixed or explained before the cold review.
- No other step scales: the requirements, the text blocks, the 10 test cases and the 6 acceptance commands are fixed.

(b) Stricter than the parent: there is no parent plan. Against the decisions, this plan is stricter in two places, each recorded here: the published section states when a review round counts as open (rule 5's frozen inputs), which decision 6847972415 leaves to the reader; and it names the new tier `sandbox` and pins its output and usage text, which that decision delegates ("adds the script option the exclusion needs"). When the pre-PR local gate runs again, and that it need not run again after the pull request opens, is decision 6856109826 B1, not a choice of this plan. The citation texts of 3.2 and 3.3 for `CLAUDE.md` item 1 and rule 8 are decision 6856109826 B2; the other pinned citation sentences follow the same form. The plan decides nothing either decision reserves: proposals 3 and 4 are untouched.

(c) Dependencies on code, data or behaviour that is neither on `main` nor in this pull request: the hosted runner's behaviour and a cloud sandbox's missing cgroup v2 files. Both are in section 7 with recorded output that settles them, so neither is a deployment assumption left open. Everything else the plan touches is on `main` and cited by file and line in section 9. The `--filter-not-trait` behaviour is the runner's, verified from its own help output and already relied on by the `fast` tier on `main`.

## 11. Open questions for Hari

None.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, or the scope in section 2.
