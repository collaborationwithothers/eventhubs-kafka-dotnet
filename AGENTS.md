# AGENTS.md

Rules for every AI agent working in this repository, whatever the tool.

- Claude Code reads this file through CLAUDE.md, whose first line imports it.
- Codex reads it natively, including the `## Code Review Rules` section when reviewing pull requests.
- docs/blueprint.md is the design authority. Read it before planning any work. If this file and the blueprint disagree on design, the blueprint wins. If they disagree on process, this file wins.

How the factory runs, step by step, is in docs/agents/factory.md. Engineering standards are in docs/engineering/.

## GOVERNANCE (maintained by Hari only; do not edit)

Status: approved by Hari on 2026-10-12.

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
10. Never use `@codex fix`. Codex reviews only.
11. Never state a number that was not measured. Estimates say "estimate", their basis and the date. Workload data is synthetic and labelled synthetic.

### Verdict integrity

- Agents never edit a scenario's expected outcome, a verdict, or anything under `results/` to make a run pass.
- A run whose result differs from the expected outcome is a finding, not a failure to fix. Report it in the evidence pack.
- Verdict pull requests always go to Hari.

### Merge rule

An agent may merge its own pull request only when all of these hold:
- every required status check passes;
- no protected path is touched (the ruleset's required reviewers rule on `main` lists them and blocks the merge until Hari approves; `.github/CODEOWNERS` mirrors the list);
- Codex's review raised no P0 or P1 finding, or each finding is resolved in a later commit and Codex has re-reviewed;
- Codex's security review raised no finding at High severity, or each is resolved and re-reviewed;
- the pull request is within the size limit in docs/agents/factory.md.

Anything else waits for Hari. Never ask for the gate to be relaxed.

### Handoffs

A session that stops for any reason ends with exactly one comment on its issue or pull request, stating the decision it needs from Hari in one sentence, with the evidence linked. No silent stops.

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

## Engineering standards

docs/engineering/dotnet.md and docs/engineering/terraform.md are binding. The non-negotiables:

- Vertical slices by feature inside each service. `Parchwell.Kafka` is a shared library, not a slice.
- Seams at our own boundaries, not around Confluent.Kafka, which is the thing under test.
- Warnings are errors. Nullable reference types are on.
- Architecture tests and analyzers fail the build; a reviewer's opinion does not substitute for them.
- Terraform uses pinned Azure Verified Modules where they cover the need, `azurerm` where they do not, and `azapi` only where `azurerm` lacks the property (ADR-011).
- Runtime log messages are for operators. They do not carry documentation prose.

## Skills

Skills live in `.agents/skills/` (Codex) and `.claude/skills/` (Claude Code, symlinks to the same folders). Except for `repo-review` (below), they are third-party, pinned in `skills-lock.json`, and not edited in place. Where a skill conflicts with this file or docs/agents/factory.md, this file wins:

- Decisions are recorded as ADRs in docs/blueprint.md section 4, numbered from ADR-012, in its format (context, options, decision, consequences). Never create `docs/adr/`.
- The issue tracker is GitHub issues on this repository. Read issues as described under "GitHub issues and pull requests". This replaces `/setup-matt-pocock-skills`, which is not installed; ignore any skill step that points to it.
- Never apply the `ready-for-agent` label. Only Hari applies it; it is his plan approval.
- Skills that interview a human (`grilling`, `domain-modeling`) are for interactive sessions with Hari only. In an unattended run, write the open question as the handoff comment instead.

One skill is owned by this repository rather than pinned from a third party: `repo-review`. It is the rubric for the cold review that "Before opening a pull request" rule 8 defines. `repo-review` is not in `skills-lock.json` and changes only through a pull request to Hari.

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
- Read review threads: `gh api repos/{owner}/{repo}/pulls/<number>/ccr/review_threads`. This is a route of the cloud-session proxy, named in the 403 message. It is not a documented GitHub route.
- Merge a pull request: `gh api -X PUT repos/{owner}/{repo}/pulls/<number>/merge -f merge_method=<method>`. The parameters are in GitHub's docs (https://docs.github.com/en/rest/pulls/pulls, "Merge a pull request"). GitHub recommends its asynchronous merge API instead of this endpoint. It also says this endpoint does not support stacked pull requests or merging with a merge queue. Both statements are in the operation description at `paths["/repos/{owner}/{repo}/pulls/{pull_number}/merge"].put.description` in https://raw.githubusercontent.com/github/rest-api-description/main/descriptions/api.github.com/api.github.com.json (info.version 1.1.4, read 2026-10-13). The asynchronous API is documented at https://docs.github.com/en/rest/pulls/pulls#merge-a-pull-request-asynchronously. It has not been run. No repository document chooses a merge method. The repository allows merge commits, squash and rebase (`gh api repos/{owner}/{repo}`, fields `allow_merge_commit`, `allow_squash_merge` and `allow_rebase_merge`, read 2026-10-13). Which one agents use is Hari's decision.
- Whether an agent may merge is decided by GOVERNANCE "Merge rule". docs/agents/factory.md, "Sequence to start the factory", enables agent merge at step 6.
- Auto-merge is not available. The repository has it disabled (`allow_auto_merge` is `false` in `gh api repos/{owner}/{repo}`, read 2026-10-13).
- The 403 message also names proxy routes to resolve or unresolve review threads, to enable or disable auto-merge and to change draft state. They have not been run.

Evidence from 2026-10-13, in a cloud session:
- Run and working: the issue and comment reads, opening a pull request (PR #30 was opened this way), editing a pull request description, opening an issue (issue #35 was opened this way) (these three are recorded in https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/35#issuecomment-6149687846), commenting (the comment linked above), reading a pull request, and reading the review threads of PRs #6, #24 and #34.
- Run only against an already-merged pull request: the merge route. It returned HTTP 200 with the existing merge commit and left `main` unchanged. Merging an open pull request has not been run.
- Not run: no merge-method value has been observed to take effect, and the proxy routes to resolve or unresolve threads, enable or disable auto-merge and change draft state.

## Before opening a pull request

Applies to every pull request, documentation included. Learned on PR #6, which took nine Codex review rounds.

1. Verify every factual claim against its source at the pinned version before committing it. Never write a library or Azure behaviour from memory.
2. Mark a claim confirmed only if every part of it is stated by a cited source. Name any inferred part as inferred. Cite every source a claim depends on, in full.
3. Before every push, including pushes that only contain review fixes, re-read the whole diff against docs/blueprint.md's ADRs, docs/engineering/ and the merge rule. A fix can break another decision.
4. Compute counts, totals, cross-references and time differences with a script, never by hand.
5. When a reviewer finds one error, check every instance of that class, not only the one flagged.
6. If two review rounds in a row raise new substantive findings, stop and split the pull request. Moving a finding out of scope into a linked issue counts as a split. The threshold is a governance choice, not a measurement; its basis is PR #6, where design findings kept arriving until the pull request was split after round four.
7. Keep the description short: what changed, why, what to read first, what you are least sure about. Evidence lives in the repository (ADRs, citations, commit messages); the description links to it. If the change cannot be described in a few lines, split it.
8. Run a cold review before every push. A cold review is a fresh context that applies the `repo-review` skill and is given only the diff by the author. Fix every P0 and P1 it reports, except as the next paragraph allows. Fix each P2, or record it as an issue. Cold-review rounds do not count toward rule 6. If three cold-review rounds in total on the same pull request report P0 or P1 findings that must be fixed, stop and hand off under GOVERNANCE "Handoffs". Three is a governance choice, not a measurement; revisit it with the data recorded under issue #17.

   Some Code Review Rules flag work that only Hari can approve: changes to expected outcomes, verdicts or `results/`, and new projects, assembly references or NuGet packages. Keep such work only if the approved plan or the verdict step requires it. In that case, push, list the finding in the pull request description, and leave the merge to Hari. Revert any other such change before the push. This exception does not relax GOVERNANCE "Verdict integrity".
9. Fill in the pull request template, including its collapsed self-review record. Escapes start as "pending"; after the review rounds, classify every finding and update the count before merge (issue #17 defines how escapes are measured).

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

## Writing standards

- Documents (README, ADRs, scenario write-ups, pull request descriptions) open with two or three plain sentences that a reader could stop after and still leave right.
- One idea per sentence. Define unfamiliar terms at first use in documents. Never in log messages or code comments.
- ASCII punctuation only. No em or en dashes. Metric units.
