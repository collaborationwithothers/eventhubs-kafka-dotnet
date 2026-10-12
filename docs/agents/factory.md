# Factory operating model

How agents build this repository, and where Hari's judgment stays.

Status: approved by Hari on 2026-10-12.

The factory exists to minimise Hari's hours per inventory item, not to minimise calendar time. Generating code is cheap; verifying it is the constraint. So Hari's attention goes into two decisions per item, and everything between them is checked by machines that are hard to fool.

## Decisions taken (2026-10-11 to 2026-10-12)

| Decision | Choice |
|---|---|
| Goal | Fewest of Hari's hours per inventory item |
| Hari's touchpoints per item | Approve the plan; approve the verdict from the evidence pack |
| Live Azure runs | Started by agents without per-run approval, within a cost limit per item |
| Merging | Agents merge their own pull requests under the merge rule in AGENTS.md |
| Escalation | Deterministic rules decide; the reviewer can add an escalation, never remove one |
| Tools | Claude Code implements; Codex reviews only, on Hari's ChatGPT plan |
| Where sessions run | GitHub Actions, unattended, after one reference slice built interactively with Hari |
| Concurrency | 2 implementation sessions, 1 ephemeral environment |
| Architecture | Vertical slices per service; `Parchwell.Kafka` shared library; scenarios as data |
| Cost limits | GBP 5 (S), GBP 15 (M), GBP 40 (L) per item, estimates from the blueprint's sizing |

## Modes of work

Following Osmani's distinction between loops a human reviews and loops nobody reads:

| Work | Mode | Why |
|---|---|---|
| Item plan | Human-approved | Cheapest place for Hari's judgment: a page, not a diff |
| Harness code, Terraform for the ephemeral layer, KQL, scenario definitions | Unattended, merged by agents | Checked by build, analyzers, architecture tests, `terraform plan`, Codex review |
| Verifier logic | Human-reviewed (CODEOWNERS) | It is the oracle for every result; also covered by mutation testing |
| Expected outcomes, verdicts, `results/` | Human-only approval | They are the public claims |
| Persistent infrastructure, identity, network, workflows, governance, ADRs | Human-reviewed (CODEOWNERS) | Large blast radius, long-lived decisions |

## Item lifecycle

1. **Plan.** An agent drafts the plan as a comment on the item's issue, using the template below.
2. **Approve.** Hari applies `ready-for-agent`. This is approval of the plan and the trigger for implementation.
3. **Implement.** One session claims the issue, builds what the plan needs in pull requests within the size limit, and merges or escalates each under the merge rule.
4. **Run.** The agent starts the scenario workflow. The workflow checks the cost limit, applies the ephemeral layer, runs the repeats, collects evidence, and destroys.
5. **Verdict.** The agent opens a pull request labelled `verdict` with the evidence pack and the proposed verdict. Hari approves or rejects it.

## Plan template

```
Item: <ID> <title>
Hypothesis: <what the documentation claims, and what we expect to observe>
Configurations: <settings varied, one line each>
Fault injected: <none, or which harness capability>
Measures: <metrics, verifier counts, KQL queries>
Repeats: <at least 5 per configuration>
Harness changes needed: <none, or the capability to add>
Estimated cost: <harness hours x GBP 1.56, plus Log Analytics> against limit <S/M/L>
Expected outcome file: scenarios/<ID>/expected.yaml (approved with this plan)
```

## Evidence pack

Every verdict pull request contains:
- the scenario definition and expected outcome as approved;
- raw results per repeat in `results/<ID>/<utc-date>.json`;
- verifier counts (gaps, duplicates, reorders) per repeat;
- the KQL queries and their output;
- Confluent.Kafka and librdkafka versions, PU count, node size, region, run timestamps;
- actual cost of the runs from Cost Management;
- the proposed verdict (confirmed, contradicted, undocumented, untestable) and a three-sentence summary.

## Escalation

Layer 1 decides; layer 2 can only add.

1. **Deterministic rules.** `.github/CODEOWNERS` lists every escalated path. A ruleset on `main` requires code-owner review, so GitHub blocks the merge until Hari approves. Rules that paths cannot express are checked by a required status check: pull request size, new NuGet packages, and changes to assertions in tests.
2. **Codex review.** Any unresolved P0 or P1 code review finding, or High security review finding, blocks agent merge. Codex reads docs/threat-model.md for security reviews once it exists; until then it generates its own threat model on every review.

To verify during the pilot, before agents merge unattended:
- that a ruleset can require code-owner review while requiring zero other approvals;
- whether Codex posts anything when it finds no issues, and whether its reviews can block a merge, so the merge step cannot race ahead of the review.

## Measuring the gate

- Every pull request's gate decisions are logged: which rules fired, Codex's findings, who merged.
- Each week Hari spot-checks about 5 agent-merged pull requests and records yes or no: should this have come to me? (About 10 minutes.)
- Metrics: Hari's minutes per item, cost per merged pull request, gate misses, false escalations.

## Deferred: Jev as the second escalation layer

Jev (TypeSafe AI) is deferred. It is trialled only when all of these hold:
1. at least about 50 labelled pull requests exist from the spot-checks;
2. the current gate shows a measured problem: one miss, or false escalations costing Hari more than about 30 minutes a week over a month;
3. Jev is generally available, or access is committed with published pricing and data handling.

The trial runs in shadow mode, logging scores without gating. Jev is adopted only if, on the shadow set, it catches every high-risk pull request the current gate missed without missing any it caught, raises fewer false escalations than Codex, and its probabilities are calibrated. Even then it only adds escalations, replacing Codex in that layer.

## Sequence to start the factory

1. Hari approves this document, AGENTS.md's GOVERNANCE section, CLAUDE.md model bindings, and CODEOWNERS.
2. Hari merges them to `main` and creates the ruleset.
3. The reference slice (A01) is built interactively with Hari, at code level. It sets the patterns agents copy.
4. Workflows are added: CI, implementation, scenario run with cost check, sweeper.
5. Pilot on 3 items with agent merge disabled, to verify the open questions above.
6. Agent merge enabled. Factory runs.
