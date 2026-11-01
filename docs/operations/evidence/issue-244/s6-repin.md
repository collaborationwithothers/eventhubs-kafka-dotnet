# Issue 244: S6 re-pin and dispatch result receipt

This pull request re-pins blueprint source S6 from the librdkafka master branch to the v2.16.0 tag and records the result of the native JWT-bearer dispatch in the operations doc. It changes no code, scenario, workflow, expected outcome or `results/` file. It authorizes no dispatch.

Base revision: `216bce2`
Branch: `claude/issue-244-s6-repin`

## Authority and scope

There is no approved item plan for this change. It is directly authorized work. The authorization is Hari's instruction in an interactive session on 2026-11-01: "do the S6 re-pin. I want it in the repo". It follows [decision 6547412663](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412663), answer 9, which makes the S6 re-pin a separate later change, and the [authored assessment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6557005071) of run 41296493195. I read "it" in the second sentence as the dispatch result, and I recorded that in the operations doc. If Hari meant something else, this is the part to change.

`docs/blueprint.md` is under `/docs/` in `.github/CODEOWNERS` (owner: Hari), so Hari merges this pull request. The implementing tool is Claude Code.

## Execution card

- Deliverable: the S6 row of `docs/blueprint.md` section "Sources"; two paragraphs after the first paragraph group of "Native client compatibility diagnostic" in `docs/operations/authentication-lifecycle-observation.md`; `items/244/state.md`; this receipt.
- Current authorization: the instruction above, for a documentation change only.
- Mandatory gates: AGENTS.md "Before opening a pull request" rules 1 and 2 (claims verified at the pinned version), rule 8 (cold review), hosted Codex review, CI, and Hari's merge.
- Verification: the commands in "Verification record".
- Earliest publication checkpoint: after the first comprehensive cold review and its corrections.
- Review budget: rule 8's, 2 comprehensive and 2 focused rounds. Exhaustion action: hand off to Hari.

## Specification comparison

| Change | Where |
|---|---|
| S6 row now names the v2.16.0 tag, the commit `c6c9973cf1b25394c58e0cade13a36f386856f77` and the re-pin date, and says it is the same document as S25 | `docs/blueprint.md` line 1183 |
| Result paragraphs, with links to the run, the assessment, run 41286058152 and two Microsoft Learn pages | `docs/operations/authentication-lifecycle-observation.md` |
| Covers, Approved plan, Next action | `items/244/state.md` |

S7 (STATISTICS.md, master) is not changed. Only S6 was in scope. No other file cites the S6 master URL (checked by grep).

## Claims verified at the pinned version

Source: [CONFIGURATION.md at tag v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), fetched 2026-11-01, SHA-256 `b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`, tag commit `c6c9973cf1b25394c58e0cade13a36f386856f77` (from `git ls-remote`). I parsed the property table by script and compared each default that the blueprint cites to S6. All matched.

| Claim cited to S6 | Blueprint place | v2.16.0 value |
|---|---|---|
| `request.timeout.ms` default 30000 | Section 12 "Documentation conflicts found while blueprinting" item 1; A01 | 30000 |
| `enable.idempotence` default `false` | ADR-005 context; conflicts item 3; B04 | false |
| `message.max.bytes` and `batch.size` default 1000000, producer "may exceed the maximum size by one message" | ADR-006 context; B02 | 1000000 and 1000000; the `message.max.bytes` text contains that phrase |
| `socket.keepalive.enable` default `false` | A02 | false |
| `metadata.max.age.ms` default 900000 | A03 | 900000 |
| `linger.ms` default 5 | B07 | 5 |
| `session.timeout.ms` default 45000 | C01 | 45000 |
| `auto.offset.reset` default `largest` | C15 | largest |
| `message.timeout.ms` default 300000 | B10 | 300000 |
| `partitioner` default `consistent_random`, `murmur2_random` exists | B08 | consistent_random; `murmur2_random` present |
| `connections.max.idle.ms` Azure-specific heuristic when left at default | A04; conflicts item 4 | description says heuristics identify "brokers on Azure" when left at default |
| `sasl.oauthbearer.metadata.authentication.type` has `azure_imds` | ADR-003 context; conflicts item 5; F02 | values `none, azure_imds, aws_iam`; `azure_imds` uses the Azure IMDS endpoint |
| Offset-store pattern recommended in the `max.poll.interval.ms` description | ADR-005 options, item 4 | the description recommends `enable.auto.offset.store=false` and storing offsets after processing |
| `enable.auto.offset.store` default `true`; `enable.auto.commit` default `true` | C10 | true and true |
| `group.protocol` default `classic`, KIP-848 text present; `partition.assignment.strategy` default `range,roundrobin`, `cooperative-sticky` present; `group.instance.id` present | C07, C08, C06 | all present as stated |

Claims on the Microsoft Learn pages in the result paragraphs were read on 2026-11-01: the On-Behalf-Of page says `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer` with `requested_token_use` "Required ... must be set to `on_behalf_of`", and "the OBO flow only works for user principals"; the error-code page lists AADSTS900144 as "The request body must contain the following parameter". Numbers in the result paragraphs (5 slots, 45 requests, 55 earlier lines, 0 trust-anchor lines) come from the artifacts of runs 41296493195 and 41286058152 as read in the assessment comment, which gives the counting method.

Not verified: claims in the inventory table rows that I did not find a v2.16.0 default for in this list. I checked every S6-citing sentence in the body text and every default named in an inventory row; I did not re-read each row's non-default prose.

## Verification record

Run in this session's container on the working tree that became the first commit of the branch.

| Command | Result |
|---|---|
| `npm ci --ignore-scripts`, `npm run test:docs` | passed |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, evidence for issues 124, 140 and 211); the three changed files report no issues |
| `git diff --check` | clean |

No code or test is changed, so no .NET, Python or chart suite was run for this change.

## Cold review record

Cumulative counts at the time of writing: comprehensive 0 of 2, focused 0 of 2. Round 1 is pending: comprehensive, input revision the commit that adds this receipt, comparison revision `216bce2`. Reports and dispositions are recorded in the pull request self-review record.
