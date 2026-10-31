# AGENTS.md

Rules for every AI agent working in this repository, whatever the tool.

- Claude Code reads this file through CLAUDE.md, whose first line imports it.
- Codex reads it natively, including the `## Code Review Rules` section when reviewing pull requests.
- docs/blueprint.md is the design authority. Read it before planning any work. If this file and the blueprint disagree on design, the blueprint wins. If they disagree on process, this file wins.

How the factory runs, step by step, is in docs/agents/factory.md. Engineering standards are in docs/engineering/. The basis, citations and decision history of the rules here, where recorded, are in [factory record](docs/agents/factory.md#hard-safety-rule-8-basis-and-decision-history) for hard safety rule 8, and otherwise in [AGENTS.md at 8cbde949d8fca424dda33accbd01c1a995495678](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/8cbde949d8fca424dda33accbd01c1a995495678/AGENTS.md) and later Git history. Read them only when a rule's basis is in question.

## GOVERNANCE (approved by Hari; agents may propose changes in a pull request, which only Hari merges)

AGENTS.md is a protected path, so each governance change reaches `main` only through Hari's merge.

### What this repo is

A public portfolio repository under Hari's name. It tests Microsoft's guidance for Kafka clients on Azure Event Hubs, using Confluent.Kafka on .NET 10, and publishes what actually happens. Its product is the results in `results/`, not the code. A wrong published result costs more than a slow one.

### Scope

- v1 is bounded by the frozen inventory in docs/blueprint.md section 12. Never create issues, branches or code for items in its deferred list. If work seems to need a deferred item, stop and comment on the issue.
- Scenarios are data: one definition file per inventory item, run by one harness. New code is added only when a scenario needs a harness capability that does not exist yet.

### Hard safety rules

1. Never run `terraform apply` or `terraform destroy` against `infra/persistent`. Only Hari applies the persistent layer.
2. Agent sessions never apply or destroy `infra/ephemeral`. Only this repository's workflows, or Hari by hand, apply or destroy the ephemeral layer.
3. Agent sessions never hold Azure credentials. Azure is reached only through the scenario workflow's OIDC identity.
5. At most one ephemeral environment exists at a time.
6. Each workflow job destroys its ephemeral layer when it ends, whether it passed or failed. AKS stops once when the whole dispatch ends, whether it passed or failed. When no dispatch is active, the sweeper destroys a leftover ephemeral environment on the first sweep and stops a running AKS cluster 60 minutes after the last dispatch ended. The workflow reads dispatch completion from the GitHub Actions runs and jobs APIs; the lifecycle implementation defines the bounded lookup. The interval is design policy from [requirements 13-14](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6382769026) and their [adoption](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383731545); the lookup follows the [parent C2 interface](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/138#issuecomment-6383206899).
7. Never add secrets, keys, connection strings, tenant IDs or subscription IDs to the repository.
8. Agents change files under `.github/workflows/` only through a pull request that Hari merges; the path is protected. A change to a workflow that has a committed copy under `docs/operations/workflows/` updates both in the same pull request, byte for byte. An agent may add a workflow whose only trigger is `workflow_dispatch`; Hari merges it. An agent never changes an existing workflow's triggers: the workflows that reach Azure carry only `workflow_dispatch` and `schedule` (docs/blueprint.md section 3 "Scenario runner"; rule 9 bars the pull-request triggers), and `dotnet-verification.yml` and `docs-advisory.yml` keep theirs. An agent never dispatches a workflow against any ref but `main`. Basis and decision history: [factory record](docs/agents/factory.md#hard-safety-rule-8-basis-and-decision-history).
9. No workflow that reaches Azure may trigger on `pull_request` or `pull_request_target`. The repository is public; fork pull requests must never run inside the network.
10. Never write `@codex` in an issue comment; Codex's connector replied to each issue comment that contained it (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6235975803, https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6245200871, https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6250013487). On a pull request, mention `@codex` only as `@codex review` or `@codex security review`. Any other `@codex` comment starts a Codex cloud chat on the pull request (ADR-018, docs/blueprint.md S32 and S47); review fixes are made by the session that owns the branch, whichever tool it runs.
11. Never state a number that was not measured. Estimates say "estimate", their basis and the date. Workload data is synthetic and labelled synthetic.

### Verdict integrity

- Agents never edit a scenario's expected outcome, a verdict, or anything under `results/` to make a run pass.
- A run whose result differs from the expected outcome is a finding, not a failure to fix. Report it in the evidence pack.
- Verdict pull requests always go to Hari.
- If a rerun is inconclusive, investigate. Hari may approve scenario changes to obtain a clear answer. A published result shows the final run only. This does not authorize agents to change expectations to make a run pass.

### Merge rule

An agent may merge its own pull request only when all of these hold:
- every required status check passes;
- no protected path is touched (the ruleset's required reviewers rule on `main` lists them and blocks the merge until Hari approves; `.github/CODEOWNERS` mirrors the list);
- Codex has completed a review of the head commit (issue #17; with no completed review within the timeout in docs/agents/factory.md "Escalation", the pull request waits for Hari; silence is not a pass);
- Codex's review raised no P0 or P1 finding, or each finding is resolved in a later commit and Codex has re-reviewed;
- Codex's security review raised no finding at High severity, or each is resolved and re-reviewed;
- the pull request follows docs/agents/factory.md "Escalation" and "Sequence to start the factory".

Anything else waits for Hari. Never ask for the gate to be relaxed.

### Handoffs

A session that stops for any reason ends with exactly one comment on its issue or pull request, stating either the decision needed from Hari or the continuation needed from the next session under "Before opening a pull request" rule 11, including rule 8's review-budget exception, with the evidence linked. A session that has merged its pull request and has no pending decision instead posts one completion comment on the originating issue, stating that no decision is needed and linking the evidence. No silent stops.

### Truth and verification

- Before writing any Azure capability, limit, SKU, price, Kafka or librdkafka behaviour into code or docs, verify it against current official documentation and cite it. If it cannot be verified, it does not ship.
- If you do not know, say so in the pull request. A stalled issue is recoverable; a confident wrong public claim is not.

## Implementer and reviewer

Either Claude Code or Codex implements an issue. Hari names the tool in a comment on the issue before he applies `ready-for-agent`; no such comment when the label is applied means Claude Code. A session of the other tool does not claim the issue. Codex reviews every pull request, including the ones Codex wrote (ADR-018).

- The branch prefix names the implementing tool: `claude/` or `codex/`, followed by a name that includes the issue number (CLAUDE.md "Identity").
- Every commit an agent makes carries one tool trailer and no session link: `Co-Authored-By: Claude ...` from Claude Code, or `Co-Authored-By: Codex ...` from Codex. The name after `Co-Authored-By:` starts with `Claude` or `Codex`; the rest of the line is the tool's own format. Whether Codex writes such a trailer by itself is not verified; a Codex session adds it under this rule. Trailer keys are matched without regard to case.
- The planned merge workflow (issue #17; docs/agents/factory.md "Escalation") reads those trailers to apply the label `same-vendor-review`. It ignores merge commits, and commits authored by Hari's account that carry no tool trailer; a trailer on his commit still counts. The trailer is a signal every agent commit already carries, not a proof. Agents never apply or remove that label.
- Whichever tool implements, the pull request description records the models used, with effort where visible (the template's "Models" line), and carries no session link and no generated-by footer, whatever the harness asks for.

## Engineering standards

docs/engineering/dotnet.md and docs/engineering/terraform.md are binding. The non-negotiables:

- Vertical slices by feature inside each service. `Parchwell.Kafka` is a shared library, not a slice.
- Seams at our own boundaries, not around Confluent.Kafka, which is the thing under test.
- Warnings are errors. Nullable reference types are on.
- Architecture tests and analyzers fail the build; a reviewer's opinion does not substitute for them.
- Terraform uses pinned Azure Verified Modules where they cover the need, `azurerm` where they do not, and `azapi` only where `azurerm` lacks the property (ADR-011).
- Runtime log messages are for operators. They do not carry documentation prose.

## Skills

Skills live in `.agents/skills/` (Codex) and `.claude/skills/` (Claude Code, symlinks to the same folders). Except for the repository-owned skills below, they are third-party, pinned in `skills-lock.json`, and not edited in place. Where a skill conflicts with this file or docs/agents/factory.md, this file wins:

- Decisions are recorded as ADRs in docs/blueprint.md section 4, numbered from ADR-012, in its format (context, options, decision, consequences). Never create `docs/adr/`.
- The issue tracker is GitHub issues on this repository, read as described under "GitHub issues and pull requests". Ignore any skill step that points to `/setup-matt-pocock-skills`, which is not installed.
- Never apply the `ready-for-agent` label. Only Hari applies it; it is his plan approval.
- Skills that interview a human (`grilling`, `domain-modeling`) are for interactive sessions with Hari only. In an unattended run, write the open question as the handoff comment instead.

Four skills are owned by this repository. They are not in `skills-lock.json` and change only through a pull request to Hari.

- `repo-review` is the rubric for the cold review that "Before opening a pull request" rule 8 defines.
- `plan-pr`, `plan-check` and `implement-plan` run the planning, plan-check and implementing sessions of docs/agents/factory.md "Item lifecycle", or one session under its step 3 "Standing approval". The prompt that starts a session names the skill and the issue number.

A repository-owned skill, and any prompt that starts a session, cites a rule by file and section and never paraphrases a rule that stops, caps or gates work.

Adding or removing a skill is a governance change and goes to Hari.

## GitHub issues and pull requests

- Use `gh api` REST routes for issues and pull requests; `gh` subcommands that use GraphQL fail in cloud sessions.
- Do not use `gh api --paginate`: its next-page URLs use numeric repository IDs, which the proxy refuses. Page with `per_page=100&page=N`.
- Merge only through the REST merge route with the `sha` recorded when the required checks and Codex review were verified, under GOVERNANCE "Merge rule". Do not take that `sha` from a fresh read of the pull request head. The merge method agents use is Hari's decision.

## Before opening a pull request

Applies to every pull request, documentation included. Authority refreshes follow docs/agents/factory.md "Authority reads".

Before implementation, put a short execution card in the pull request's single authored receipt: deliverable, current authorization, mandatory gates, exact verification commands, earliest authorized publication checkpoint, and review budget with its exhaustion action. Reference approved plan fields instead of copying them. For directly authorized governance work without an approved item plan, record the actual task brief, issue and authorization without claiming plan approval.

Before the first cold review of verifier work, map each changed or affected predicate to its tests: below, at and above each boundary, empty input and precedence interactions. Explain unreachable cases. Keep unrelated evidence valid so another invalid field cannot mask the predicate under test. Record the map in the authored receipt or link its authored location; record not applicable for other work.

1. Verify every factual claim against its source at the pinned version before committing it. Never write a library or Azure behaviour from memory.
2. Mark a claim confirmed only if every part of it is stated by a cited source. Name any inferred part as inferred. Cite every source a claim depends on, in full.
3. Before every push, including pushes that only contain review fixes, re-read the applicable review scope defined by rule 8 against docs/blueprint.md's ADRs, docs/engineering/ and the merge rule. A fix can break another decision.
4. Compute counts, totals, cross-references and time differences with a script, never by hand.
5. Freeze the review inputs, identify the reviewed revision and wait for complete reports from all reviews started on that revision before editing. Group findings by root cause, audit every affected instance, and consolidate corrections before the next review. When a reviewer finds one error, check every instance of that class, not only the one flagged. For recurring findings, consider automated prevention with an existing tool first. A new check needs explicit supported inputs, failure conditions and regression examples. Add it to the same pull request only when it fits the approved scope; otherwise record the proposal on the issue. A finding does not automatically require a new checker capability. Documentation checks start as advisory; making them blocking requires Hari's approval based on observed usefulness and false positives. The local command and supported checks are in [Documentation checks](docs/engineering/documentation-checks.md).
6. If two review rounds in a row raise new substantive findings, stop and split the pull request. A substantive finding is a P0 or P1. Moving a finding out of scope into a linked issue counts as a split. The threshold is a governance choice, not a measurement.
7. Keep the description short: what changed, why, what to read first, what you are least sure about. Evidence lives in the repository (ADRs, citations, commit messages); the description links to it. If the change cannot be described in a few lines, split it.
8. Every non-backup push needs applicable cold-review coverage under the protocol below; rule 12 defines backup pushes. Before the cold review, run `npm run check:docs`; fix each warning or explain in the pull request why it stands. Warnings in files the pull request does not change take one blanket line in the pull request, not one disposition each, and do not count against a plan's disposition cap. A cold review is a fresh context that applies the repository-owned `repo-review` skill and is given only the review diff and authored receipt by the author. The receipt identifies the originating approved plan/specification and recognized later decisions, or the actual task brief and issue for directly authorized work without an approved item plan. Compare implementation against those sources for missing, partial or incorrect requirements and scope creep, with evidence. Recognize governed plans and later decisions under docs/agents/factory.md "Item lifecycle" step 3. The review diff contains all authored work, including scripts, judgements, prose and cold-review records wherever they live. Agents commit no program output. Each pull request keeps one authored receipt stating the commands run, source commit and results. The receipt records results for the commits it describes; it need not record the head commit that contains the receipt itself. Record the final head's CI run and the last cold-review round in the pull request description. Do not create a receipt-only commit to make the receipt describe its own head.

   Fix every P0 and P1 the cold review reports, or answer it `Not valid:` with evidence under rule 10, except as the next paragraph allows. For counting under this rule, that exception paragraph applies first. Cold-review rounds do not count toward rule 6. At the first round with no P0 or P1, fix the P2s whose fixes touch only prose in files already in the diff. If those fixes change the diff, run one focused P2-correction review before pushing; that round reports only P0 or P1 and does not repeat. It counts toward the focused-review limit below, including when the initial comprehensive review is clean and no second comprehensive pass is needed. If that round reports a P0 or P1, revert the P2 fixes made after the clean round. Then push the diff that the clean round reviewed, with no further cold-review round. Dispose of each reverted P2 under rule 10. If the P0 or P1 remains after reverting the P2 fixes, use the review-budget handoff below. If no focused round remains for these P2 fixes, use that handoff before making them. Other P2s follow rule 10. Open one follow-up issue for the pull request only for P2s that need new scope.

   Some Code Review Rules flag work that only Hari can approve: changes to expected outcomes, verdicts or `results/`, and new projects, assembly references or NuGet packages. Keep such work only if the approved plan or the verdict step requires it. In that case, push, list the finding in the pull request description, and leave the merge to Hari. A P1 kept this way, including one on a new project, reference, package or tool that the approved plan requires, does not count toward rule 6's review-round stop limit. Revert any other such change before the push. This exception does not relax GOVERNANCE "Verdict integrity".

   **Cold-review protocol and cumulative budget.** Start with one fresh-context comprehensive review of the entire authored PR diff. If corrections are needed, allow one consolidated comprehensive re-review. At most two comprehensive passes run per PR across all sessions. After those passes, allow at most two focused correction-review rounds; the P2-correction exception above may use a focused round after a clean initial pass without requiring another comprehensive pass. These numbers are governance policy choices, not measured token savings.

   A focused round reviews changes since the last reviewed revision, unresolved findings, and affected surrounding code, callers, tests and contracts. Supply the full PR diff as context, but do not repeat a comprehensive full-PR review. Identify the mode, input revision and comparison revision in the authored receipt. Record complete reports, dispositions and cumulative comprehensive/focused counts in the PR's self-review record; link earlier reports rather than adding the final review report to its own reviewed receipt. Reviewed unchanged work retains coverage; every later authored change needs applicable coverage. Starting a new session, splitting corrections into pushes, or renaming a review does not reset the counters. Cold-review rounds remain excluded from rule 6; that rule's substantive-finding split still applies to the reviews it counts.

   Stop early if substantial changes to design, scope or assumptions make safe focused review impossible. When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari. This is an explicit exception to rule 11's automatic next-session continuation. State the unresolved gate, findings, reviewed revisions, cumulative budget and proposed next action. A third comprehensive pass requires Hari's explicit decision. Never weaken a required gate to fit the budget.

   Publish the tested PR at the earliest authorized review checkpoint. Independent CI and hosted Codex review may run together. Hosted review and GOVERNANCE "Merge rule" final-head requirements remain mandatory and are recorded separately from cold-review counts; avoid duplicate discretionary review requests. Corrections from hosted findings consume the remaining cold-review budget before any non-backup push. If none remains, use the handoff above.

   Count the first five pull requests merged after the #184 pull request merged, whether or not the prose-only P2 round ran in them. For each one, record in its description the cold-review rounds, the P2 follow-up issues opened, and whether the prose-only P2 round above ran and what it reported. After those five have merged, record that evidence per pull request, with links, on [issue #184](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/184). Hari then decides whether the prose-only P2 round stays.
9. Fill in the pull request template, including its collapsed self-review record. Reply to every Codex finding, inside the finding's review thread. When a finding is wrong against its source, start the reply with `Not valid:` and give the evidence. Escapes are not counted by hand; a script will compute them (docs/agents/factory.md, "Measuring the gate", "Escapes").

10. Answer review findings by severity. This applies to Codex code review and the cold review, in every round, subject to rule 8's disposition of P2s at the first clean cold-review round. Codex security review findings are answered under rule 9, and GOVERNANCE "Merge rule" decides whether they block a merge.
    - P0 or P1: fix it, or answer `Not valid:` with evidence (rule 9). Rule 8's third paragraph still applies.
    - P2 that asks for a check, guard or hardening the approved plan does not list: do not fix it. Reply `Out of contract:` and name the plan field or section. If its absence could make a published result wrong, also comment on the issue being implemented so Hari can decide whether the check joins the plan.
    - Any other P2: fix it, or give a one-line reason for leaving it.

    The pull request description links its approved plan. A pull request with no approved plan, such as a governance or documentation change, has no plan checks, so its P2 findings take the last case. The checks a plan lists are its "Enforced checks" field or, for a plan approved before that field existed, the checks its text names. Codex findings are answered in their review thread and cold-review findings in the self-review record. A cold-review P0 or P1 answered `Not valid:` is also listed in the pull request description, so Hari sees the dispute.

    A pull request that cannot clear Codex review uses rule 11, subject to rule 8's review-budget handoff to Hari.

11. Stop at the first measurement of work sized at runtime. If a step's workload depends on a count known only at runtime, such as mutants, survivors, findings, retries or files, and the approved plan gives no cap or sample size for it, stop after the first measurement. Every cap in an approved plan states its exhaustion action: `record the gap and continue` or `push -wip and hand off to the next session`. At an uncapped first measurement, record the count and an estimate of the remaining time under hard safety rule 11, then push -wip and hand off to the next session. Except for rule 8's review-budget handoff, a session hands off to Hari only when the next step spends money, changes a public claim (`results/`, `expected.yaml`, or a verdict), or touches a protected path. A pull request that cannot clear Codex review pushes -wip and hands off to the next session with the findings, except that rule 8's review-budget exhaustion or unsafe focus hands off to Hari; a new session cannot reset that budget. Otherwise no cap raise is requested. Rule 12 defines the backup branch. Required authority reads for planning, plan checking or implementation are exempt: the reads docs/agents/factory.md "Authority reads" lists for the step, including its catch-up. Reading the files and sources needed to write or independently check a plan under docs/agents/factory.md "Item lifecycle" steps 1 and 3 is also exempt. Execution work and finding dispositions identified by those reads remain subject to this rule.

12. A backup push saves work in progress. A session may push its work to `<its branch>-wip`, its own branch name with `-wip` appended. It never opens a pull request from that branch, and deletes it once its own branch is pushed. A backup push is exempt from rule 8; every other rule applies. No workflow runs on it: on `main` at 8cbde949d8fca424dda33accbd01c1a995495678, `dotnet-verification.yml` triggers on pull requests, pushes to `main` and manual dispatch, and `docs-advisory.yml` on pull requests.

13. Long jobs run where the environment supports them: the agent's Linux workspace, or GitHub Actions once the required workflow exists. There is no session time or token budget; the vendor allowance is the budget. Its use per session is not measured routinely. In one [Codex CLI sample on 2026-10-31](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6543624566), the plan allowance display moved only in whole percentage points, and credits did not move because they are drawn only after the allowance runs out. Per-item measurement follows the [#264 decision rules](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6542232891), whose Codex method the [CLI replay decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6546378413) replaces, as amended by the [second-change decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/264#issuecomment-6548603494), decision 3. Rule 8 separately caps cold-review work. Repeat verification only for changed behavior, a failure, new evidence or a mandatory gate; required initial verification must suit the change. A cost complaint immediately stops discretionary expansion and delegation, preserves completed work and identifies the smallest authorized remainder with unresolved mandatory gates. It is not cancellation. Keep progress updates concise and factual; do not invent speculative work while waiting.

## Code Review Rules

These rules apply when Codex reviews a pull request in this repository, and in the cold review ("Before opening a pull request", rule 8).

- Flag as P0 any change that edits an expected outcome, a verdict, or a file under `results/`, outside a pull request labelled `verdict`.
- Flag as P0 any change that weakens identity scope, network exposure, or private endpoint configuration.
- Flag as P0 any workflow change that adds a `pull_request` or `pull_request_target` trigger to a job that uses Azure credentials.
- Flag as P1 any test whose assertion was loosened, deleted or skipped in the same pull request as the code it tests.
- Flag as P1 any number in documentation that is not traceable to a measurement in `results/` or a cited source.
- Flag as P1 any new project, assembly reference or NuGet package.
- Flag as P1 any new `docs/adr/` file, or any change that applies the `ready-for-agent` label.
- Flag as P1 any use of a Kafka client setting that contradicts docs/blueprint.md without a linked inventory item.
- Do not flag style that the analyzers already enforce.
- Do not flag the presence, absence or style of code comments ([Code comments](docs/engineering/code-comments.md) "Review"). Two exceptions: a comment that states something false about the code or a source, and a public member of `Parchwell.Kafka` without documentation, which docs/engineering/dotnet.md requires.
- A P2 must name a reader it could mislead or a future change it could break. Do not report other wording or clarity points.

## Writing standards

- Documents (README, ADRs, scenario write-ups, pull request descriptions) open with two or three plain sentences that a reader could stop after and still leave right.
- One idea per sentence. Define unfamiliar terms at first use in documents. Never in log messages.
- Code comments are written for an engineer new to the repository, as [Code comments](docs/engineering/code-comments.md) describes.
- ASCII punctuation only. No em or en dashes. Metric units.
- When talking to Hari in an interactive session, never use a bare repository identifier: an inventory item code such as `F04`, a scenario configuration label such as `C2`, an issue or pull request number, a comment ID or a rule number. State what the thing is or tests, in plain words. Where traceability needs the identifier, put it in brackets after the description, never in place of it. This covers the session's chat replies only, not text posted to GitHub.
