# Factory operating model

How agents build this repository, and where Hari's judgment stays.

Status: approved by Hari on 2026-10-12. Amended for ADR-018 in the pull request for issue #40 (2026-10-13): the "Decisions taken" date range and its "Tools" and "Where sessions run" rows, "Item lifecycle" step 2, "Escalation" layers 1 and 2, "Measuring the gate", the Copilot paragraph under "Ruleset", and "Sequence to start the factory" step 4. Hari's merge of that pull request is the approval. Amended in the pull request for issue #72 (2026-10-16): "Item lifecycle" step 1 and the plan template's "Enforced checks" field; amended again in the follow-up pull request for issue #72 (2026-10-17): "Item lifecycle" step 1 and the plan template's "Hypothesis" and "Decision rules" fields.

The factory exists to minimise Hari's hours per inventory item, not to minimise calendar time. Generating code is cheap; verifying it is the constraint. So Hari's attention goes into two decisions per item, and everything between them is checked by machines that are hard to fool.

## Decisions taken (2026-10-11 to 2026-10-13)

| Decision | Choice |
|---|---|
| Goal | Fewest of Hari's hours per inventory item |
| Hari's touchpoints per item | Approve the plan; approve the verdict from the evidence pack |
| Live Azure runs | Started by agents without per-run approval, within a cost limit per item |
| Merging | Agents merge their own pull requests under the merge rule in AGENTS.md |
| Escalation | Deterministic rules decide; the reviewer can add an escalation, never remove one |
| Tools | Either Claude Code or Codex implements, chosen per issue; Codex reviews every pull request, including its own. Both run on Hari's plans (Claude Max, ChatGPT Pro), not on metered API keys. Decided 2026-10-13, ADR-018 |
| Where sessions run | GitHub Actions, unattended, after one reference slice built interactively with Hari. Claude Code only, until a Codex path in Actions exists; Codex implements in sessions Hari starts himself (ADR-018) |
| Concurrency | 2 implementation sessions, 1 ephemeral environment |
| Architecture | Vertical slices per service; `Parchwell.Kafka` shared library; scenarios as data |
| Cost limits | GBP 5 (S), GBP 15 (M), GBP 40 (L) per item, estimates from the blueprint's sizing |

## Modes of work

Following Osmani's distinction between loops a human reviews and loops nobody reads:

| Work | Mode | Why |
|---|---|---|
| Item plan | Human-approved | Cheapest place for Hari's judgment: a page, not a diff |
| Harness code, Terraform for the ephemeral layer, KQL, scenario definitions | Unattended, merged by agents | Checked by build, analyzers, architecture tests, `terraform plan`, Codex review |
| Verifier logic | Human-reviewed (protected path) | It is the oracle for every result; also covered by mutation testing |
| Expected outcomes, verdicts, `results/` | Human-only approval | They are the public claims |
| Persistent infrastructure, identity, network, workflows, governance, ADRs | Human-reviewed (protected path) | Large blast radius, long-lived decisions |

## Item lifecycle

1. **Plan.** An agent drafts the plan as a comment on the item's issue, using the template below. Plan text follows AGENTS.md "Before opening a pull request" rules 1, 2 and 4. Before approval, a session other than the drafting one checks the plan against the template and its sources. It reports any field that is empty or open-ended, such as a validator without a numbered "Enforced checks" list, as a comment on the issue before Hari applies `ready-for-agent` (#72).
2. **Approve.** Hari applies `ready-for-agent`. This is approval of the plan and the trigger for implementation. Before applying the label, he names the implementing tool, Claude Code or Codex, in a comment on the issue; no such comment at the time the label is applied means Claude Code, and a session of the other tool does not claim the issue (ADR-018).
3. **Implement.** One session claims the issue, builds what the plan needs in pull requests within the size limit, and merges or escalates each under the merge rule.
4. **Run.** The agent starts the scenario workflow. The workflow checks the cost limit, applies the ephemeral layer, runs the repeats, collects evidence, and destroys.
5. **Verdict.** The agent opens a pull request labelled `verdict` with the evidence pack and the proposed verdict. Hari approves or rejects it.

## Plan template

```
Item: <ID> <title>
Hypothesis: <one or more labelled hypotheses: what the documentation claims, and what we expect to observe>
Decision rules: <for each hypothesis the item decides: the evidence, when observation ends, when that evidence counts as complete, and which outcome supports each verdict in "Evidence pack"; "recorded only" for a hypothesis with no verdict. The same rules are written into expected.yaml, the protected file that holds the hypotheses (ADR-014)>
Configurations: <settings varied, one line each>
Fault injected: <none, or which harness capability>
Measures: <metrics, verifier counts, KQL queries>
Repeats: <at least 5 per configuration>
Harness changes needed: <none, or the capability to add>
Enforced checks: <for a component that validates or rejects input, a numbered list of every check it enforces; "none" otherwise>
Estimated cost: <harness hours x GBP 1.56, plus Log Analytics> against limit <S/M/L>
Expected outcome file: scenarios/<ID>/expected.yaml (approved with this plan)
```

## Evidence pack

Every verdict pull request contains:
- the scenario definition and expected outcome as approved;
- raw results per repeat in `results/<ID>/<utc-date>.json`;
- per-repeat run validity and completion status, reasons for any invalid or incomplete result, and worker exit outcomes;
- verifier counts for every complete, valid repeat: `ok`, `loss`, `phantom`, `reported_failure`, `duplicate` and `reorder` (ADR-012); invalid or incomplete repeats retain diagnostic records with these authoritative counts absent, never substituted with zeros;
- the KQL queries and their output;
- Confluent.Kafka and librdkafka versions, PU count, node size, region, run timestamps;
- actual cost of the runs from Cost Management;
- the proposed verdict (confirmed, contradicted, undocumented, untestable) and a three-sentence summary.

## Escalation

Layer 1 decides; layer 2 can only add.

1. **Deterministic rules.** The `main-protection` ruleset's required reviewers rule lists every protected path and requires one approval from the `repo-owners` team (Hari) when a pull request touches one. `.github/CODEOWNERS` mirrors the same list so GitHub requests Hari's review automatically, but it is not the gate (see "Ruleset" below). Rules that paths cannot express are checked by a required status check: pull request size, new NuGet packages, and changes to assertions in tests. Two more deterministic rules belong to the merge workflow planned under issue #17 (ADR-018):
   - Implementer. The merge workflow, planned under issue #17 and added by Hari at step 4 of "Sequence to start the factory", will read the `Co-Authored-By` trailers on the pull request's commits, ignoring merge commits and untrailed commits authored by Hari's account, and label the pull request `same-vendor-review` when any remaining commit carries a Codex trailer or no tool trailer, re-checking on every push and removing the label when no remaining commit does (AGENTS.md "Implementer and reviewer" defines the trailers and the ignored commits). Agents never apply or remove that label. Neither the label nor the workflow exists yet.
   - Review unavailable. The same workflow will post `**Review unavailable:**` on the pull request when Codex has completed no review of the head commit within 60 minutes of the push; the pull request then waits for Hari. This covers a Codex usage-limit refusal (ADR-018, S35) and any other silence. The marker differs from the session handoff marker `**Decision needed:**` so that the workflow's comment never satisfies, or doubles, a session's Stop hook (CLAUDE.md "Handoffs"). 60 minutes is a governance choice, not a measurement; revisit it with the data recorded under issue #17. The check depends on Codex posting a completed review on a clean pull request, which the pilot verifies (third bullet below). Until then agents do not merge (step 5 of "Sequence to start the factory"), so the timeout has nothing to enforce; a session whose pull request waits for a review that does not come hands off under GOVERNANCE "Handoffs". If the pilot finds that Codex posts only a reaction on a clean pull request, the merge workflow must obtain a completed review, for example by commenting `@codex review`, before the merge rule can pass; that is a design item under issue #17.
2. **Codex review.** Any unresolved P0 or P1 code review finding, or High security review finding, blocks agent merge. Codex reads docs/threat-model.md for security reviews once it exists; until then it generates its own threat model on every review. Codex reviews every pull request, whichever tool implemented it (ADR-018).

To verify during the pilot, before agents merge unattended:
- that a pull request opened by the factory's GitHub App on a protected path is blocked until Hari approves (tested so far only with Hari as author);
- that a pull request touching only `docs/operations/` is not blocked (the negation pattern);
- whether Codex posts anything when it finds no issues, and whether its reviews can block a merge, so the merge step cannot race ahead of the review.

## Ruleset

`main-protection` on the default branch. Settings and the reason for each, as tested on 2026-10-12 with throwaway pull requests #1 and #2:

| Setting | Value | Reason |
|---|---|---|
| Bypass | Repository admin and organization admin, "for pull requests only" | Hari cannot approve his own pull requests, so his governance changes merge through bypass; nobody can push to `main` directly |
| Restrict deletions, block force pushes | On | |
| Required approvals | 0 | Agent pull requests on unprotected paths merge without a human |
| Required reviewers | Team `repo-owners`, 1 approval, 15 file patterns | The gate for protected paths. GitHub caps the list at 15 patterns per reviewer, so the list is full; a new protected path means merging patterns or adding a second team. `/.*/` covers every root dot-folder (`.github`, `.agents`, `.claude`); `/*.md` covers every root Markdown file, including README.md |
| Require review from Code Owners | On, but not relied on | Test: with 0 required approvals, a code-owned pull request was mergeable without review. Cause not isolated (zero approvals, or author was the only code owner) |
| Dismiss stale approvals on push | On | A push after Hari's approval clears it |
| Require approval of the most recent push | **Off** | Test: it required an approval from someone other than the last pusher on every pull request, so agents could never merge |
| Require conversation resolution | On | Review threads must be resolved. Not a hard gate: the bot can resolve threads |
| Additional approval for unattributed Copilot pull requests | On | Preview setting; affects only pull requests Copilot authors |

Protected paths use `.gitignore` syntax. `/docs/` is protected except `docs/operations/`, which agents own. Runbooks go in `docs/operations/`; any other document under `docs/` comes to Hari.

GitHub Copilot code review is not part of the factory. It reviewed the test pull requests although no ruleset requested it (most likely a personal setting) and was switched off on 2026-10-12, because its threads would block agent merges through conversation resolution. The second reason recorded on 2026-10-12, that it answers to none of the rules in AGENTS.md, was wrong: Copilot code review has read AGENTS.md since 2026-06-18 (ADR-018, S38). It stays off because of the conversation-resolution problem and because of cost: reviews of bot-authored pull requests "bill the usage directly to your organization" (S40). Inferred, not stated by S40 or S44: organization billing needs AI-credit paid usage enabled, as S44 requires for its Organization billing option. Either way it is spend beyond the one seat Hari already pays for (ADR-018).

## Measuring the gate

- Every pull request's gate decisions are logged: which rules fired, Codex's findings, who merged.
- Each week Hari spot-checks about 5 agent-merged pull requests and records yes or no: should this have come to me? (About 10 minutes.) Pull requests labelled `same-vendor-review` come first in the sample, but the sample keeps at least one unlabelled pull request when the week has one.
- Metrics: Hari's minutes per item, cost per merged pull request, gate misses, false escalations, review-unavailable escalations. Gate misses and escapes are reported split by `same-vendor-review` and the rest (ADR-018).

### Escapes

An escape is a valid P0 or P1 finding that the cold review should have caught but did not. It shows whether self-review is improving. The cases below also count findings on pushes that skipped the cold review. That is by design: a skipped review is a self-review failure too.

- Pre-merge escape: a valid P0 or P1 that Codex raises on any pushed commit.
- A push without a cold review breaks AGENTS.md "Before opening a pull request" rule 8. It is recorded as a process breach in the pull request description, and Codex's findings on it still count as escapes.
- Not an escape: a finding the author pushed knowingly under the escalation exception in AGENTS.md "Before opening a pull request" rule 8, listed in the pull request description.
- Post-merge escape: a P0 or P1 defect on `main` that a cold review of its pull request should have caught. It is recorded as an issue labelled `escape` that links the pull request that introduced it. The label does not exist yet. Who applies it is open (issue #17).
- Only valid findings count. An author's `Not valid:` reply makes a finding disputed, not invalid. A disputed finding counts as an escape unless Hari agrees with the dispute in the same thread.
- A `Not valid:` reply does not clear a P0 or P1 for merge. The merge rule in AGENTS.md still applies, so a disputed finding waits for Hari.
- P2 findings do not count.
- A pull request's count is provisional, because post-merge escapes can appear weeks later.
- Escapes are not counted by hand. A script will compute them from the Codex review threads and their replies, the pull request descriptions (escalation exceptions and process breaches) and the `escape` issues. The script and the normalisation (per inventory item or per finding) will be designed with the merge workflow (issue #17). Until then, the threads are the record.

## Deferred: Jev as the second escalation layer

Jev (TypeSafe AI) is deferred. It is trialled only when all of these hold:
1. at least about 50 labelled pull requests exist from the spot-checks;
2. the current gate shows a measured problem: one miss, or false escalations costing Hari more than about 30 minutes a week over a month;
3. Jev is generally available, or access is committed with published pricing and data handling.

The trial runs in shadow mode, logging scores without gating. Jev is adopted only if, on the shadow set, it catches every high-risk pull request the current gate missed without missing any it caught, raises fewer false escalations than Codex, and its probabilities are calibrated. Even then it only adds escalations, replacing Codex in that layer.

## Sequence to start the factory

1. Hari approves this document, AGENTS.md's GOVERNANCE section, CLAUDE.md model bindings, and CODEOWNERS. Done 2026-10-12.
2. Hari merges them to `main` and creates the ruleset. Done 2026-10-12; tested as recorded under "Ruleset".
3. The reference slice (A01) is built interactively with Hari, at code level. It sets the patterns agents copy.
4. Workflows are added: CI, implementation, scenario run with cost check, sweeper, and the merge workflow (`same-vendor-review` label, review-unavailable escalation; issue #17).
5. Pilot on 3 items with agent merge disabled, to verify the open questions above.
6. Agent merge enabled. Factory runs.
