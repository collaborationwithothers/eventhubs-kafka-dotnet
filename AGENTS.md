# AGENTS.md

Rules for every AI agent working in this repository, whatever the tool.

- Claude Code reads this file through CLAUDE.md, whose first line imports it.
- Codex reads it natively, including the `## Code Review Rules` section when reviewing pull requests.
- docs/blueprint.md is the design authority. Read it before planning any work. If this file and the blueprint disagree on design, the blueprint wins. If they disagree on process, this file wins.

How the factory runs, step by step, is in docs/agents/factory.md. Engineering standards are in docs/engineering/.

## GOVERNANCE (approved by Hari; agents may propose changes in a pull request, which only Hari merges)

Status: approved by Hari on 2026-10-12. Heading, merge rule and hard safety rule 10 amended for ADR-018 in the pull request for issue #40 (2026-10-13). Hari decided on 2026-10-14 that agents may author governance changes (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/40#issuecomment-6159904486). AGENTS.md is a protected path, so each such change reaches `main` only through his merge. Hard safety rule 10 and "Handoffs" amended in the pull request for issue #85 (2026-10-18).

### What this repo is

A public portfolio repository under Hari's name. It tests Microsoft's guidance for Kafka clients on Azure Event Hubs, using Confluent.Kafka on .NET 10, and publishes what actually happens. Its product is the results in `results/`, not the code. A wrong published result costs more than a slow one.

### Scope

- v1 is bounded by the frozen inventory in docs/blueprint.md section 12. Never create issues, branches or code for items in its deferred list. If work seems to need a deferred item, stop and comment on the issue.
- Scenarios are data: one definition file per inventory item, run by one harness. New code is added only when a scenario needs a harness capability that does not exist yet.

### Hard safety rules

1. Never run `terraform apply` or `terraform destroy` against `infra/persistent`. Only Hari applies the persistent layer.
2. The ephemeral layer (`infra/ephemeral`) is applied and destroyed only by the scenario workflow, never from an agent session.
3. Agent sessions never hold Azure credentials. Azure is reached only through the scenario workflow's OIDC identity.
4. Before every live run, the scenario workflow checks the item's cost limit: GBP 5 for an S item, GBP 15 for M, GBP 40 for L (docs/blueprint.md section 14 sizes). A run that would exceed the limit does not start; the agent comments on the issue and stops.
5. At most one ephemeral environment exists at a time.
6. Every scenario run ends with `terraform destroy`, whether the run passed or failed. A scheduled sweeper destroys any ephemeral environment older than 4 hours.
7. Never add secrets, keys, connection strings, tenant IDs or subscription IDs to the repository.
8. Never change files under `.github/workflows/`. The factory's GitHub App has no Workflows permission, so GitHub rejects such pushes; do not try to work around it.
9. No workflow that reaches Azure may trigger on `pull_request` or `pull_request_target`. The repository is public; fork pull requests must never run inside the network.
10. Never write `@codex` in an issue comment; Codex's connector replied to each issue comment that contained it (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6235975803, https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6245200871, https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6250013487). On a pull request, mention `@codex` only as `@codex review` or `@codex security review`. Any other `@codex` comment starts a Codex cloud chat on the pull request (ADR-018, docs/blueprint.md S32 and S47); review fixes are made by the session that owns the branch, whichever tool it runs.
11. Never state a number that was not measured. Estimates say "estimate", their basis and the date. Workload data is synthetic and labelled synthetic.

### Verdict integrity

- Agents never edit a scenario's expected outcome, a verdict, or anything under `results/` to make a run pass.
- A run whose result differs from the expected outcome is a finding, not a failure to fix. Report it in the evidence pack.
- Verdict pull requests always go to Hari.

### Merge rule

An agent may merge its own pull request only when all of these hold:
- every required status check passes;
- no protected path is touched (the ruleset's required reviewers rule on `main` lists them and blocks the merge until Hari approves; `.github/CODEOWNERS` mirrors the list);
- Codex has completed a review of the head commit (issue #17; with no completed review within the timeout in docs/agents/factory.md "Escalation", the pull request waits for Hari; silence is not a pass);
- Codex's review raised no P0 or P1 finding, or each finding is resolved in a later commit and Codex has re-reviewed;
- Codex's security review raised no finding at High severity, or each is resolved and re-reviewed;
- the pull request is within the size limit in docs/agents/factory.md.

Anything else waits for Hari. Never ask for the gate to be relaxed.

### Handoffs

A session that stops for any reason ends with exactly one comment on its issue or pull request, stating the decision it needs from Hari in one sentence, with the evidence linked. A session that has merged its pull request and has no pending decision instead posts one completion comment on the originating issue, stating that no decision is needed and linking the evidence. No silent stops.

### Truth and verification

- Before writing any Azure capability, limit, SKU, price, Kafka or librdkafka behaviour into code or docs, verify it against current official documentation and cite it. If it cannot be verified, it does not ship.
- If you do not know, say so in the pull request. A stalled issue is recoverable; a confident wrong public claim is not.

## Workflow summary

The full procedure is in docs/agents/factory.md. In short:

1. An agent drafts a one-page plan as a comment on the item's issue.
2. Hari approves by applying `ready-for-agent`.
3. The implementing session builds what the plan needs, opens pull requests, and merges or escalates under the merge rule.
4. The scenario workflow runs the live repeats within the cost limit.
5. The agent opens a verdict pull request with the evidence pack. Hari approves the verdict.

## Implementer and reviewer

Either Claude Code or Codex implements an issue. Hari names the tool in a comment on the issue before he applies `ready-for-agent`; no such comment when the label is applied means Claude Code. A session of the other tool does not claim the issue. Codex reviews every pull request, including the ones Codex wrote (ADR-018).

- The branch prefix names the implementing tool for readers: `claude/` or `codex/`, followed by a name that includes the issue number (CLAUDE.md "Identity").
- Every commit an agent makes carries one tool trailer and no session link: `Co-Authored-By: Claude ...` from Claude Code, or `Co-Authored-By: Codex ...` from Codex. The name after `Co-Authored-By:` starts with `Claude` or `Codex`; the rest of the line is the tool's own format. Whether Codex writes such a trailer by itself is not verified; a Codex session adds it under this rule. Trailer keys are matched without regard to case (`Co-authored-by` is common).
- The planned merge workflow (issue #17; docs/agents/factory.md "Escalation") reads those trailers. It ignores merge commits, and commits authored by Hari's account that carry no tool trailer (a Codex session Hari starts himself may commit under his identity, so a trailer on his commit still counts). A pull request with a Codex trailer on any remaining commit, or with a remaining commit that carries no tool trailer, gets the label `same-vendor-review`; the workflow re-checks on every push and removes the label when no remaining commit matches. Two cases are not detected: a wrong trailer, and a commit under Hari's account that a Codex session left untrailed, which is ignored. The trailer is a signal every agent commit already carries, not a proof. Agents never apply or remove that label.
- Whichever tool implements, the pull request description records the models used, with effort where visible (the template's "Models" line; issue #17), and carries no session link and no generated-by footer, whatever the harness asks for.

## Engineering standards

docs/engineering/dotnet.md and docs/engineering/terraform.md are binding. The non-negotiables:

- Vertical slices by feature inside each service. `Parchwell.Kafka` is a shared library, not a slice.
- Seams at our own boundaries, not around Confluent.Kafka, which is the thing under test.
- Warnings are errors. Nullable reference types are on.
- Architecture tests and analyzers fail the build; a reviewer's opinion does not substitute for them.
- Terraform uses pinned Azure Verified Modules where they cover the need, `azurerm` where they do not, and `azapi` only where `azurerm` lacks the property (ADR-011).
- Runtime log messages are for operators. They do not carry documentation prose.

## Skills

Skills live in `.agents/skills/` (Codex) and `.claude/skills/` (Claude Code, symlinks to the same folders). Except for the repository-owned skills (below), they are third-party, pinned in `skills-lock.json`, and not edited in place. Where a skill conflicts with this file or docs/agents/factory.md, this file wins:

- Decisions are recorded as ADRs in docs/blueprint.md section 4, numbered from ADR-012, in its format (context, options, decision, consequences). Never create `docs/adr/`.
- The issue tracker is GitHub issues on this repository. Read issues as described under "GitHub issues and pull requests". This replaces `/setup-matt-pocock-skills`, which is not installed; ignore any skill step that points to it.
- Never apply the `ready-for-agent` label. Only Hari applies it; it is his plan approval.
- Skills that interview a human (`grilling`, `domain-modeling`) are for interactive sessions with Hari only. In an unattended run, write the open question as the handoff comment instead.

Four skills are owned by this repository rather than pinned from a third party. They are not in `skills-lock.json` and change only through a pull request to Hari.

- `repo-review` is the rubric for the cold review that "Before opening a pull request" rule 8 defines.
- `plan-pr`, `plan-check` and `implement-plan` run the planning, plan-check and implementing sessions of docs/agents/factory.md "Item lifecycle" (#85 decision 7). The prompt that starts a session names the skill and the issue number.

A repository-owned skill, and any prompt that starts a session, cites a rule by file and section and never paraphrases a rule that stops, caps or gates work (#85 decision 3). An implementation prompt names `implement-plan` and the issue number.

Adding or removing a skill is a governance change and goes to Hari.

## GitHub issues and pull requests

Read, open and comment on issues and pull requests, and edit and merge pull requests, through the REST API.

`gh issue view`, `gh pr view`, `gh pr create`, `gh issue comment` and `gh pr merge` use GitHub GraphQL. Claude Code cloud sessions block it. In a cloud session these five fail with HTTP 403, "GitHub GraphQL is not available from Claude Code sessions". All five failures were observed on 2026-10-13: the first four are recorded in PR #30's description (`gh pr create` was run as `--dry-run`), and `gh pr merge` in https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/35#issuecomment-6149284076. Issue #23 first reported the failure. Other `gh issue` and `gh pr` subcommands may fail the same way.

- Read an issue: `gh api repos/{owner}/{repo}/issues/<number>`.
- Read its comments: `gh api 'repos/{owner}/{repo}/issues/<number>/comments?per_page=100'`. GitHub documents 100 as the maximum page size (https://docs.github.com/en/rest/issues/comments, "List issue comments"). If 100 come back, fetch again with `&page=2`, and so on.
- Do not use `gh api --paginate`. Its next-page URLs use numeric repository IDs. The proxy refuses them with HTTP 403, "Numeric-ID repository paths (repositories/{id}/...) are not supported through this proxy". This was observed on 2026-10-13.
- Open a pull request: write the description to a file, then run `gh api -X POST repos/{owner}/{repo}/pulls -f title='<title>' -f head=<branch> -f base=main -F body=@<file>`.
- Edit a pull request description: `gh api -X PATCH repos/{owner}/{repo}/pulls/<number> -F body=@<file>`. GitHub documents the route at https://docs.github.com/en/rest/pulls/pulls ("Update a pull request").
- Open an issue: `gh api -X POST repos/{owner}/{repo}/issues -f title='<title>' -F body=@<file>`. GitHub documents the route at https://docs.github.com/en/rest/issues/issues ("Create an issue").
- Comment on an issue or pull request: `gh api -X POST repos/{owner}/{repo}/issues/<number>/comments -F body=@<file>`. A pull request takes general comments on the same route. GitHub documents the route at https://docs.github.com/en/rest/issues/comments ("Create an issue comment").
- Read a pull request: `gh api repos/{owner}/{repo}/pulls/<number>`.
- Reply inside a review thread: `gh api -X POST repos/{owner}/{repo}/pulls/<number>/comments/<comment id>/replies -F body=@<file>`. GitHub documents the operation as "Create a reply for a review comment" (https://docs.github.com/en/rest/pulls/comments#create-a-reply-for-a-review-comment). This path was run and worked on 2026-10-13: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/pull/24#discussion_r4256651187.
- Read review threads: `gh api repos/{owner}/{repo}/pulls/<number>/ccr/review_threads`. This is a route of the cloud-session proxy, named in the 403 message. It is not a documented GitHub route.
- Merge a pull request: `gh api -X PUT repos/{owner}/{repo}/pulls/<number>/merge -f merge_method=<method> -f sha=<verified sha>`.
  - `<verified sha>` is the head commit you recorded when you verified the required checks and Codex's review. Take it from the commit those results name, not from a fresh read of the pull request head just before the merge. A fresh read returns whatever was pushed last, so it would defeat the check.
  - `sha` is documented as the SHA the pull request head must match to allow the merge. The 409 response is documented as "Conflict if sha was provided and pull request head did not match". `merge_method` takes `merge`, `squash` or `rebase`.
  - Inferred, not stated by the source: with a recorded `sha`, a push after verification makes the merge fail instead of merging unreviewed code.
  - Source: the operation at `paths["/repos/{owner}/{repo}/pulls/{pull_number}/merge"].put` in https://raw.githubusercontent.com/github/rest-api-description/2297ff28f52e233d97bd482eb43bdaa5723add16/descriptions/api.github.com/api.github.com.json (upstream commit 2297ff28f52e233d97bd482eb43bdaa5723add16, info.version 1.1.4, read 2026-10-13).
  - The same operation's description says GitHub recommends the asynchronous merge API instead. It also says this endpoint does not support stacked pull requests or merging with a merge queue.
  - The asynchronous API is documented at https://docs.github.com/en/rest/pulls/pulls#merge-a-pull-request-asynchronously. It has not been run, and neither has the `sha` parameter.
  - No repository document chooses a merge method. The repository allows merge commits, squash and rebase (`gh api repos/{owner}/{repo}`, fields `allow_merge_commit`, `allow_squash_merge` and `allow_rebase_merge`, read 2026-10-13). Which one agents use is Hari's decision.
- Whether an agent may merge is decided by GOVERNANCE "Merge rule". docs/agents/factory.md, "Sequence to start the factory", enables agent merge at step 6.
- Auto-merge is not available. The repository has it disabled (`allow_auto_merge` is `false` in `gh api repos/{owner}/{repo}`, read 2026-10-13).
- The 403 message also names proxy routes to resolve or unresolve review threads, to enable or disable auto-merge and to change draft state. They have not been run.

Evidence from 2026-10-13, in a cloud session:
- Run and working: opening a pull request (PR #30 was opened this way) and opening an issue (issue #35 was opened this way). Both are recorded in https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/35#issuecomment-6149687846.
- Run and reachable: editing a pull request description. The recorded run sent an unchanged body (same comment), so it shows the route is reachable, not that a change took effect.
- Run and working: commenting. The test is https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/35#issuecomment-6149284076.
- Run and working: the reads (issue, issue comments, pull request, and review threads of PRs #6, #24 and #34). They returned HTTP 200 when re-run at 2026-10-13T16:17Z. The status lines are in https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/35#issuecomment-6150030404.
- Run only against an already-merged pull request: the merge route. It returned HTTP 200 with the existing merge commit and left `main` unchanged. Merging an open pull request has not been run.
- Not run: no merge-method value has been observed to take effect, and the proxy routes to resolve or unresolve threads, enable or disable auto-merge and change draft state.

## Before opening a pull request

Applies to every pull request, documentation included. Learned on PR #6, which took nine Codex review rounds. Authority refreshes follow docs/agents/factory.md "Authority reads".

1. Verify every factual claim against its source at the pinned version before committing it. Never write a library or Azure behaviour from memory.
2. Mark a claim confirmed only if every part of it is stated by a cited source. Name any inferred part as inferred. Cite every source a claim depends on, in full.
3. Before every push, including pushes that only contain review fixes, re-read the review diff defined by rule 8 against docs/blueprint.md's ADRs, docs/engineering/ and the merge rule. A fix can break another decision.
4. Compute counts, totals, cross-references and time differences with a script, never by hand.
5. When a reviewer finds one error, check every instance of that class, not only the one flagged. For recurring findings, consider automated prevention with an existing tool first. A new check needs explicit supported inputs, failure conditions and regression examples. Add it to the same pull request only when it fits the approved scope; otherwise record the proposal on the issue. A finding does not automatically require a new checker capability. Documentation checks start as advisory; making them blocking requires Hari's approval based on observed usefulness and false positives. The local command and supported checks are in [Documentation checks](docs/engineering/documentation-checks.md).
6. If two review rounds in a row raise new substantive findings, stop and split the pull request. A substantive finding is a P0 or P1. Moving a finding out of scope into a linked issue counts as a split. The threshold is a governance choice, not a measurement; its basis is PR #6, where design findings kept arriving until the pull request was split after round four.
7. Keep the description short: what changed, why, what to read first, what you are least sure about. Evidence lives in the repository (ADRs, citations, commit messages); the description links to it. If the change cannot be described in a few lines, split it.
8. Run a cold review before every push, except a backup push under rule 12. Before the cold review, run `npm run check:docs`; fix each warning or explain in the pull request why it stands. A cold review is a fresh context that applies the `repo-review` skill and is given only the review diff and raw-evidence manifest by the author. The review diff contains all authored work, including scripts, judgements, prose and cold-review records wherever they live. Programs write their output under a `raw/` folder in the evidence directory. Mark only `docs/operations/evidence/**/raw/**` as `linguist-generated` in `.gitattributes` and exclude only that pathspec from the review diff. A `raw/` folder anywhere else is authored work and stays in the review diff. Basis for this scope: #85, decision of 2026-10-20 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6286458075). The raw-evidence manifest lists each raw file's path, producer, byte size and SHA-256. Keep the manifest as raw-manifest.json beside raw/, with a files array whose entries use path, producer, size_bytes and sha256; producer records the exact command and source commit. Keep authored files outside raw folders. The reviewer opens a raw file only when an authored file cites it. The complete pull request still includes the raw files. Basis: #85, decision of 2026-10-20, decision 1 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629).

   Fix every P0 and P1 the cold review reports, or answer it `Not valid:` with evidence under rule 10, except as the next paragraph allows. For counting under this rule, that exception paragraph applies first. Cold-review rounds do not count toward rule 6. The loop ends at the first round with no P0 or P1. That round's P2s go to one follow-up issue for the pull request, except a P2 that a script both fixes and checks. An automatic P2 fix changes the diff and needs a cold review before it is pushed. Answer other cold-review P2s under rule 10. The churn breaker stops the session when the same file or component gets P0 or P1 findings in two cold-review rounds; hand off under GOVERNANCE "Handoffs". The two rounds need not be consecutive; a finding still counts after it is fixed or disputed, except for the approval-only findings excluded below. Only a `## Decision` on the issue overrides this breaker. That override lasts until the pull request is clean. List in the pull request description every trip passed under it. The override does not change rule 6 or rule 10's Codex review-round cap. Basis: #85, decision of 2026-10-20, decisions 6 to 8 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629).

   Some Code Review Rules flag work that only Hari can approve: changes to expected outcomes, verdicts or `results/`, and new projects, assembly references or NuGet packages. Keep such work only if the approved plan or the verdict step requires it. In that case, push, list the finding in the pull request description, and leave the merge to Hari. A P1 kept this way, including one on a new project, reference, package or tool that the approved plan requires, never counts toward any review-round stop limit: not the churn breaker in rule 8, not rule 6, and not rule 10's handoff after the third Codex round (#85 decision 3). Revert any other such change before the push. This exception does not relax GOVERNANCE "Verdict integrity".
9. Fill in the pull request template, including its collapsed self-review record. Reply to every Codex finding. When a finding is wrong against its source, start the reply with `Not valid:` and give the evidence. Reply inside the finding's review thread, so the reply stays attached to the finding. Escapes are not counted by hand. A script will compute them (docs/agents/factory.md, "Measuring the gate", "Escapes").

10. Answer review findings by severity. This applies to Codex code review and the cold review, in every round, subject to rule 8's disposition of P2s at the first clean cold-review round. Codex security review findings are answered under rule 9, and GOVERNANCE "Merge rule" decides whether they block a merge. Basis: the review rounds on #70 (#72).
    - P0 or P1: fix it, or answer `Not valid:` with evidence (rule 9). Rule 8's second paragraph still applies.
    - P2 that asks for a check, guard or hardening the approved plan does not list: do not fix it. Reply `Out of contract:` and name the plan field or section. If its absence could make a published result wrong, also comment on the issue being implemented so Hari can decide whether the check joins the plan.
    - Any other P2: fix it, or give a one-line reason for leaving it.

    The pull request description links its approved plan. A pull request with no approved plan, such as a governance or documentation change, has no plan checks, so its P2 findings take the last case. The checks a plan lists are its "Enforced checks" field or, for a plan approved before that field existed, the checks its text names. Codex findings are answered in their review thread and cold-review findings in the self-review record. A cold-review P0 or P1 answered `Not valid:` is also listed in the pull request description, so Hari sees the dispute.

    A pull request gets at most three completed Codex review rounds. A round is one completed Codex code review of a head commit, however it was triggered; a security review is not a round. When the third completes and a finding still needs a code change, push nothing further and hand off under GOVERNANCE "Handoffs". Only Hari's decision on the issue being implemented, recognized under docs/agents/factory.md "Item lifecycle" step 3 and naming the new cap and the pull requests it covers, raises the cap. Three is a governance choice, not a measurement.

11. Stop at the first measurement of work sized at runtime. If a step's workload depends on a count known only at runtime, such as mutants, survivors, findings, retries or files, and the approved plan gives no cap or sample size for it, stop after the first measurement. Hand off under GOVERNANCE "Handoffs" with the count and an estimate of the time to finish (hard safety rule 11), and continue only after Hari records the answer on the issue as a decision recognized under docs/agents/factory.md "Item lifecycle" step 3. Required authority reads for planning, plan checking or implementation are exempt: the issue body, its comments, the plan and later decisions. Reading the files and sources needed to write or independently check a plan under docs/agents/factory.md "Item lifecycle" steps 1 and 3 is also exempt. Execution work and finding dispositions identified by those reads remain subject to this rule. Basis: #85 decision 1; the [PR 3 replay decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/80#issuecomment-6247408154) records 621 survivors and 267 reported replays when Hari changed the requirement to a sample.

12. A backup push saves work in progress. A session may push its work to `<its branch>-wip`, its own branch name with `-wip` appended. It never opens a pull request from that branch, and deletes it once its own branch is pushed. A backup push is exempt from rule 8; every other rule applies. No workflow runs on it: on `main` at 0e9b78be2a3a175c6a27926479fe5cfe96ab1888, `dotnet-verification.yml` triggers on pull requests, pushes to `main` and manual dispatch, and `docs-advisory.yml` on pull requests. Basis: #85 decision 5; validated commits were lost when a Codex session resumed in a fresh workspace.

13. Long jobs run outside the agent. For any command with a cap over 600 s, print the exact command and end the turn; Hari runs it and replies. Use GOVERNANCE "Handoffs" for the session's handoff. CI execution is deferred to #89's baseline decision. Basis: #85, decision of 2026-10-20, decision 9 (https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6284948629).

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

## Writing standards

- Documents (README, ADRs, scenario write-ups, pull request descriptions) open with two or three plain sentences that a reader could stop after and still leave right.
- One idea per sentence. Define unfamiliar terms at first use in documents. Never in log messages.
- Code comments are written for an engineer new to the repository, as [Code comments](docs/engineering/code-comments.md) describes.
- ASCII punctuation only. No em or en dashes. Metric units.
