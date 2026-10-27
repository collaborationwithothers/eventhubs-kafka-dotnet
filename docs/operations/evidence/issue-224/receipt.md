# Trace batch documentation receipt

This receipt records validation of the trace batch documentation changes for issue #224. It contains no live scenario result.

## Sources

- Plan: [Trace batch implementation-grade plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438840070), `Revision: 2.`, `updated_at` 2026-10-27T12:57:51Z. It was approved under docs/agents/factory.md "Item lifecycle" step 3 "Standing approval" by the [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442677677) of 2026-10-27T13:08:43Z, which states `Blocking findings: none`.
- Decisions, in creation order: [6438353573](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6438353573), [amendment 6442095091](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442095091), which adopts [#155 decision 6442094735](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/155#issuecomment-6442094735) items 2 and 5, [6442153525](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6442153525), and Hari's decision records from this session's chat, [6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624) and [6450806971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450806971).
- Baseline: `main` at `39e2cbba73f28855d3d0bbc3511d977294b8ba09`.
- K09 variant: B. `origin/main:infra/cluster/aks.tf` has 0 matches of the plan's LocalDNS pin pattern and no `local_dns` text.
- The plan script was taken unmodified from the posted plan comment and saved outside the repository.
- Plan-check P2s marked `apply in implementation`: the Q1 and FM-01 sentence is the last sentence of the H01 write-up's "Metric export finding". The exit-4 K09 quote fix did not arise, because the variant did not change. The H01 report authority fix belongs to the verdict pull request.

## Validation

All commands ran in the cloud session workspace on 2026-10-27, on branch `claude/issue-224-trace-batch` based on `39e2cbb`, with the files staged and not yet committed.

- `npm ci --ignore-scripts` exited 0, because `node_modules` was absent.
- `python3 trace_224.py pr1 --write` exited 0 and printed `trace batch PR 1 write (variant B): ok`. It ran once.
- `python3 trace_224.py pr1 --check` exited 0 and printed `trace batch PR 1 check (variant B): ok`.
- `git diff --cached --check --merge-base origin/main` exited 0 and printed nothing.
- `npm run check:docs` exited 0. `npm run check:docs` reports 8 warnings. Two are `Undefined identifier: S0` in `docs/agents/factory.md`, which this pull request changes, at lines 7 and 92; both exist on `39e2cbb` (lines 7 and 90), come from the #211 amendment's `S0` decision label, and are left as they stand because this pull request does not change those sentences. The other 6 are in files this pull request does not change.
- The disposition above is the plan's text with `S0` set as code. In plain text the label raised a ninth `Undefined identifier: S0` warning, in this receipt.

- After each round of review edits, `pr1 --check`, `git diff --cached --check --merge-base origin/main` and `npm run check:docs` were rerun on the staged tree before its commit (`3c100ae`, `e6a4e9c`, `0baccfa` and the round 4 commit). Each printed the same results as above: `trace batch PR 1 check (variant B): ok`, nothing, and the same 8 warnings.

- After Hari's added changes (below), the plan's `pr1 --check` exits 1 at its blueprint confinement assertion, because the plan allowed only the K09, H01, D01 and G01 lines. On the basis of [decision record 6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624) item 2, a copy of the script with only `BP_EDITS` extended by the five added line prefixes (`Inventory frozen: `, `- For an invalid or incomplete run,`, `- Inventory: B09, C05`, `The Event Hubs metrics reference marks`, `| C11 |`) prints `trace batch PR 1 check (variant B): ok`. Every other assertion is unchanged. On each commit from `7e4acfa` on, the extended check, `git diff --cached --check --merge-base origin/main` and `npm run check:docs` were run on the staged tree before the commit, with the results above: `ok`, nothing, and the same 8 warnings. The unmodified plan check still exits 1 there, and the pull request description lists that for Hari.

## Review edits

Cold review round 1, on commit `2306b27`:
- P1, `docs/traces/H01.md`: the metrics reference marks more than four metrics DS Export `No`. The sentence now says the four named metrics are among them.
- P1, `docs/traces/H01.md`: the opening stated as fact what the body calls inference. The opening now marks it as inference.
- P1, `docs/traces/H03.md`: the opening and the Symptom bullet stated the -1 mapping as fact. Both now mark it as inference, and the Errors.java `forCode` fallback at lines 507 to 514 is added.
- P1, `docs/traces/K08.md`: the Design bullet named librdkafka as the actor. It now says the broker removes the member, as CONFIGURATION.md line 131 states.
- P2, `docs/agents/factory.md`: the traced-item rule now names "Inventory amendment 1" as well, because K06 to K10 are typed D there.
- P2, `docs/traces/C16.md`, `D04.md` and `H01.md`: line numbers in relatively linked repository documents now name commit `39e2cbb`.
- P2, `docs/traces/K08.md`: the Runbook bullet no longer credits ADR-015, whose baseline covers Kafka client settings only.
- P2, `docs/traces/H03.md`: the SeedF04Worker sentence now includes its "construction" branch, and "four sources" is now "three sources, and one inference".
- P2, `docs/traces/K09.md`: #225 is described as the issue that will add the pin, and no pull request for it exists on 2026-10-27.
- P2, `docs/traces/K09.md`: the opening and the prerequisites section say the operating system prerequisite is met by inference.
- Pre-existing, not blocking, `docs/traces/K09.md`: the compatibility-check sentence now lists the checks it covers, including line 53, NodeLocal DNSCache.

Cold review round 2, on commit `3c100ae`:
- P1, `docs/traces/K06.md`: the Why section said a spot eviction cannot be triggered. It now cites the documented simulated eviction (spot eviction guide line 114, Spot CLI page line 68), and keeps the section 12 reason.
- P1, `docs/traces/H01.md`: the Terraform bullet stated as fact what the body calls inference. It now marks the inference and notes that #155 leaves it unconfirmed.
- P2, `docs/traces/H01.md`: the opening now says the #155 observation leaves the export inference unconfirmed.
- P2, `docs/traces/K07.md`: the Kubernetes quote's range is now lines 224 to 229.
- P2, `docs/traces/K09.md`: the Claim section names line 97, the Azure CLI prerequisite, and why the prerequisites section leaves it out.

Cold review round 3, on commit `e6a4e9c`:
- Three P1s, one per file, `docs/traces/C16.md`, `D04.md` and `E05.md`: each opening stated as fact a reason its body marks as inference or does not support. Each opening now gives the section 12 reason instead.

The plan's cap of 20 dispositions was used up after round 3. Round 4's findings were handed off ([comment 6444163539](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6444163539)). On 2026-10-27 Hari approved two more review rounds in this session's chat ("I approve 2 more review rounds do it"), recorded on #224 as [decision record 6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624).

Cold review round 4, on commit `0baccfa`:
- P1, `docs/traces/K09.md`: the Security bullet stated an unsourced claim about LocalDNS. It now uses the K06 to K08 wording, and so do the Security bullets of C16, D04 and E05 (rule 5).
- P2, `docs/traces/K09.md`: "or result" is removed, because the verdict pull request adds a report.
- P2, `docs/traces/K09.md`: the geo-replication line 267 citation now names its file.
- P2, `docs/traces/D04.md`: 588 is now "at most about 588", because 17 is a lower bound.

Changes Hari added on 2026-10-27 in this session's chat ("Stale blueprint texting fix it", "c11 fix it"), beyond the plan's file content, recorded in [decision record 6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624):
- `docs/blueprint.md` line 4 now records the type and reason changes to section 12 since amendment 1: C11 (PR #218), H01 (#224), and K09's corrected reason (#224). It also records #224's D01 and G01 notes, the C11 citation fix, and the ADR-013, FM-01 and Q1 edits below.
- ADR-013 no longer lists H01 among the items that may retain throttling observations from a run.
- FM-01 no longer lists H01 among the items that test it; it names the H01 write-up as a trace.
- Query Q1 gains a note: `ThrottledRequests` has DS Export `No`, so by inference `platformThrottled` stays empty from the workspace.
- The C11 row and `docs/traces/C11.md` now cite SafeKafkaHandle.cs lines 1342 to 1373, the extent of `GetCTopicPartitionList` at v2.16.0, instead of lines 1344 to 1376.

Cold review round 5, on commit `073b631`:
- P1, `docs/traces/K07.md` and P1, `docs/traces/K08.md`: the round 2 fix to the K07 range (lines 224 to 229, not 228) and the round 1 removal of the plan's ADR-015 statement from K08 changed plan-required elements without Hari's approval (plan "How text is written and accepted" item 5). Both fixes are correct against their sources. Hari approved both as plan amendments ([decision record 6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624)).
- P2, `docs/blueprint.md` and `docs/traces/C11.md`: the stale H01 references, line 4 and the C11 range, already fixed by Hari's added changes above.
- P2, this receipt: the cap approval was a chat quote only. It is now recorded on #224 as [decision record 6450522624](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450522624).
- P2, this receipt: the round 3 entry now says it is three findings.
- P2, `docs/traces/C11.md` and `docs/traces/H03.md`: the bare "none." Design and Security bullets now give a reason.

Cold review round 6, on commit `fa6237c` (rounds 6 to 9 are approved in [decision record 6450806971](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/224#issuecomment-6450806971) and 6450522624):
- P1, `docs/traces/K09.md`: the compatibility-check sentence stated the version and cluster type matches as facts. It now says they would match a pool at 1.37 or later, and marks the cluster type as inference.
- P2, `docs/blueprint.md` line 4: it now records the ADR-013, FM-01 and Q1 edits and links Hari's decision record.
- P2, this receipt: the line 4 entry no longer says K09's type changed.
- P2, this receipt: the validation entry names the commits, the three commands and the basis for the extended check.

Cold review round 7, on commit `712f406`: no P0 or P1. Its three P2s are prose fixes in files already in the diff, applied under rule 8:
- P2, `docs/blueprint.md` line 4: it now also names the D01 and G01 notes and the C11 citation fix.
- P2, this receipt: `## Sources` now lists both decision records.
- P2, `docs/traces/K09.md`: the opening now says 1.37 turns LocalDNS on only for pools that pass the compatibility checks.

## Review

The cold review result and its dispositions are recorded in the pull request description.
