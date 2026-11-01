# Issue 244: S6 re-pin and dispatch result receipt

This pull request re-pins blueprint source S6 from the librdkafka master branch to the v2.16.0 tag and records the result of the native JWT-bearer dispatch in the operations doc. It changes no code, scenario, workflow, expected outcome or `results/` file. It authorizes no dispatch.

Base revision: `216bce2`
Branch: `claude/issue-244-s6-repin`

## Authority and scope

There is no approved item plan for this change. It is directly authorized work. The authorization is Hari's instruction in an interactive session on 2026-11-01: "do the S6 re-pin. I want it in the repo". It follows [decision 6547412663](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6547412663), answer 9, which makes the S6 re-pin a separate later change, and the [authored assessment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/244#issuecomment-6557005071) of run 41296493195. The sentence "I want it in the repo" answers my immediately preceding message, which proposed the S6 re-pin and a question: "Not asked yet: where the finding gets stated publicly. It isn't in `results/`, so only the issue comment records it. If you want it in the repo, I'd propose a short docs note". So I read "it" as the dispatch finding, which is why the operations doc now records it. That reading is mine, and Hari's wording does not name the finding; if he meant only the re-pin, the two result paragraphs are the part to remove. The dispatch of run 41296493195 was authorized by Hari in the same session ("then do a dispatch", 2026-11-01); no comment on issue 244 records that authorization.

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
| Covers, Open questions and Next action (the Approved plan line is unchanged by this pull request) | `items/244/state.md` |

S7 (STATISTICS.md, master) is not changed. Only S6 was in scope. No other file cites the S6 master URL (the `grep` in "Reproduction").

## Claims verified at the pinned version

Source: [CONFIGURATION.md at tag v2.16.0](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md), fetched 2026-11-01, SHA-256 `b68b3e195018f0b77f32ddf9f2dde9c71642631e963571e5e382255a5d326f89`, tag commit `c6c9973cf1b25394c58e0cade13a36f386856f77` (from `git ls-remote`). I parsed the property table and compared each default that the blueprint cites to S6 with the first script in "Reproduction". All matched.

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
| `sasl.mechanisms` supports `OAUTHBEARER`; the `oauthbearer_token_refresh_cb` callback exists | F01 | `sasl.mechanisms` text: "Supported: GSSAPI, PLAIN, SCRAM-SHA-256, SCRAM-SHA-512, OAUTHBEARER"; `oauthbearer_token_refresh_cb` present |
| `enable.auto.offset.store` default `true`; `enable.auto.commit` default `true` | C10 | true and true |
| `group.protocol` default `classic`, KIP-848 text present; `partition.assignment.strategy` default `range,roundrobin`, `cooperative-sticky` present; `group.instance.id` present | C07, C08, C06 | all present as stated |

Claims on the Microsoft Learn pages in the result paragraphs were read on 2026-11-01: the On-Behalf-Of page says `grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer` with `requested_token_use` "Required ... must be set to `on_behalf_of`", and "the OBO flow only works for user principals"; the error-code page lists AADSTS900144 as "The request body must contain the following parameter". Numbers in the result paragraphs (5 slots, 45 requests, 55 earlier lines, 0 trust-anchor lines) come from the artifacts of runs 41296493195 and 41286058152; the second script in "Reproduction" counts them.

Coverage audit (the last part of the first script in "Reproduction", run after round 2): the blueprint cites S6 on 8 body lines (196, 232, 238, 249, 943, 945, 946, 947) and in 20 inventory rows (A01, A02, A03, A04, B02, B04, B05, B07, B08, B10, C01, C03, C06, C07, C08, C10, C15, F01, F02, G01). Each is covered by a row of the table above or by the checks below.

Also covered by the first script: `transactional.id` exists (B05); `max.poll.interval.ms` exists with default 300000 (C03); `metadata.recovery.strategy` exists with default `rebootstrap` (G01).

Not verified: the free prose of inventory rows C06, C07 and similar, which states behaviour and design questions beyond the settings and defaults above. The S6 row in the blueprint says only that the settings, defaults and option values were rechecked.

## Predicate-to-test map

Not applicable. This pull request changes documentation only and touches no verifier, predicate or test.

## Reproduction

Run from the repository root, with `gh` authenticated and `SCRATCH` set to an empty directory outside the repository. The first block checks the v2.16.0 document and audits which blueprint lines cite S6; the second counts the dispatch evidence quoted in the operations doc.

```bash
curl -sSL https://raw.githubusercontent.com/confluentinc/librdkafka/v2.16.0/CONFIGURATION.md -o "$SCRATCH/CONFIGURATION-2.16.0.md"
sha256sum "$SCRATCH/CONFIGURATION-2.16.0.md"
git ls-remote https://github.com/confluentinc/librdkafka refs/tags/v2.16.0
grep -rn "librdkafka/blob/master/CONFIGURATION" . --exclude-dir=node_modules --exclude-dir=.git --exclude=s6-repin.md
python3 -I - "$SCRATCH/CONFIGURATION-2.16.0.md" <<'EOF'
import re, sys
rows = {}
for line in open(sys.argv[1], encoding="utf-8"):
    p = line.rstrip("\n").split("|", 5)
    if len(p) == 6 and re.match(r"^[a-z][a-z0-9_.\-]+\s*$", p[0]):
        rows[p[0].strip()] = [x.strip() for x in p]
defaults = {"request.timeout.ms": "30000", "enable.idempotence": "false", "message.max.bytes": "1000000",
    "batch.size": "1000000", "socket.keepalive.enable": "false", "metadata.max.age.ms": "900000", "linger.ms": "5",
    "session.timeout.ms": "45000", "auto.offset.reset": "largest", "message.timeout.ms": "300000",
    "partitioner": "consistent_random", "connections.max.idle.ms": "0", "group.protocol": "classic",
    "partition.assignment.strategy": "range,roundrobin", "enable.auto.offset.store": "true",
    "enable.auto.commit": "true", "max.poll.interval.ms": "300000", "metadata.recovery.strategy": "rebootstrap"}
for key, value in defaults.items():
    assert rows[key][3] == value, (key, rows[key][3])
text = open(sys.argv[1], encoding="utf-8").read()
for key, needle in [("message.max.bytes", "may exceed the maximum size by one message"),
    ("connections.max.idle.ms", "brokers on Azure"), ("max.poll.interval.ms", "enable.auto.offset.store=false"),
    ("sasl.oauthbearer.metadata.authentication.type", "azure_imds"), ("sasl.mechanisms", "OAUTHBEARER")]:
    assert needle in rows[key][5], (key, needle)
for needle in ("murmur2_random", "cooperative-sticky", "KIP-848", "group.instance.id", "oauthbearer_token_refresh_cb"):
    assert needle in text, needle
assert "azure_imds" in rows["sasl.oauthbearer.metadata.authentication.type"][2]
assert "transactional.id" in rows
lines = open("docs/blueprint.md", encoding="utf-8").read().splitlines()
body = [n for n, l in enumerate(lines, 1) if re.search(r"\bS6\b", l) and not l.startswith("|")]
inventory = [l.split("|")[1].strip() for l in lines if re.search(r"\bS6\b", l) and l.startswith("|") and not l.startswith("| S6 ")]
print("defaults and texts: all match")
print("body lines citing S6:", body)
print("inventory rows citing S6:", len(inventory), inventory)
EOF
```

```bash
gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13080457579/zip > "$SCRATCH/new.zip"
gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/artifacts/13076353402/zip > "$SCRATCH/old.zip"
python3 -I - "$SCRATCH/new.zip" "$SCRATCH/old.zip" <<'EOF'
import re, sys, zipfile
def log(path):
    z = zipfile.ZipFile(path)
    name = [n for n in z.namelist() if "c6-nativejwt" in n and n.endswith(".containers.loadgen.log")][0]
    return z.read(name).decode("utf-8").splitlines()
def count(lines, pattern, repeat=None):
    return sum(1 for l in lines if re.search(pattern, l) and (repeat is None or f"repeat={repeat} " in l))
new, old = log(sys.argv[1]), log(sys.argv[2])
retrieve = r"Failed to retrieve JWT token from"
print("new run per-slot retrieve lines:", [count(new, retrieve, r) for r in range(1, 6)], "total", count(new, retrieve))
print("new run lines ending (400):", sum(1 for l in new if re.search(retrieve, l) and l.rstrip().endswith("(400)")))
print("new run retrieve lines with AADSTS900144:", count(new, retrieve + ".*AADSTS900144"), " trust-anchor lines:", count(new, "error adding trust anchors"))
print("new run configured-location lines:", count(new, "CA certs from configured location"))
print("old run trust-anchor retrieve lines:", sum(1 for l in old if re.search(retrieve, l) and "error adding trust anchors" in l))
EOF
```

The artifact IDs are those of `probe-batch-pods-41296493195-1` (new run) and `probe-batch-pods-41286058152-1` (earlier run). Run both blocks to reproduce every count and list in the sections above.

## Verification record

Run in this session's container. The `npm` and `git diff --check` results below were taken on the working tree that became commit `80181d5` (the tree of round 2) and `check:docs` was rerun after each later commit, which changed only this receipt. Both scripts in "Reproduction" were extracted from this file and run on the tree that became the commit after `aeb211d`; they printed the values and counts quoted above.

| Command | Result |
|---|---|
| `npm ci --ignore-scripts`, `npm run test:docs` | passed |
| `npm run check:docs` | 8 warnings, all in files this pull request does not change (`docs/agents/factory.md`, evidence for issues 124, 140 and 211); the four changed files report no issues |
| `git diff --check` | clean |

No code or test is changed, so no .NET, Python or chart suite was run for this change.

## Cold review record

Cumulative counts at the time of writing: comprehensive 2 of 2, focused 1 of 2 reported and the second (the last) pending. The final report of each round is recorded in the pull request self-review record, not here, because it reviews this receipt.

| Round | Mode | Input revision | Comparison revision | Findings | Disposition |
|---|---|---|---|---|---|
| 1 | comprehensive | `68dea93` | `216bce2` | P1: the S6 row said every claim was rechecked, which the receipt contradicted. P1: the result paragraphs lacked a recorded authorization. P1: no predicate-to-test map entry. P2: the receipt said the state file's Approved plan line changed. P2: Open questions in the state file said None while the assessment lists two decisions. P2: the sentence on Microsoft's documentation said more than the source | Fixed in `80181d5`: the S6 row now says settings, defaults and option values; B05, C03 and G01 settings are checked in the receipt; the authorization context is recorded; "not applicable" map added; receipt wording corrected; state file lists the two open questions; the documentation sentence names the inferred part |
| 2 | comprehensive (consolidated re-review) | `80181d5` | `216bce2` | P1: inventory row F01 cites S6 and was missing from the claim list (same class as round 1's first P1). P2: the verification record did not name the commit it describes | Fixed in `aeb211d`: F01 added to the claim table, a coverage audit of all 8 body lines and 20 inventory rows that cite S6 added, and the verification record names `80181d5` |
| 3 | focused | `aeb211d` | `80181d5` | P1: the receipt stated script and grep results without giving the commands (also at the two earlier lines that said "by script" and "checked by grep") | Fixed in the commit that adds row 4: a "Reproduction" section holds both scripts, every such line points to it, the scripts were run from this file, and one count that included a non-request line was narrowed to request lines |
| 4 | focused (last of 2) | the commit that adds this row | `aeb211d` | Pending; recorded in the pull request self-review record | Not applicable |
