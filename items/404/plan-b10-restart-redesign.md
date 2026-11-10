# B10 restart redesign contract-grade plan

Revision 9. Issue 404, pull request short name `B10 restart redesign`. Baseline: `main` at `0f769fb240d1de50192f4c8eefaf12698002efc7`; `main` has since moved to `8b459e260558033552f61faa35921439441a4355` (the storage readiness fix, PR 426), on which all three files this plan changes are byte-identical (checked by `git diff 0f769fb2 8b459e26` on those paths, 2026-11-10). Status: Revision 8 posted as pointer [6768580945](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768580945) and checked in [6768732716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768732716); this revision is not posted, not checked, not approved.

This pull request changes design text only. It rewrites ADR-021's restart protocol and the B10 exceptions in ADR-012, ADR-013, ADR-019 and ADR-020 to the simplified restart that Hari chose in [decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530), and marks the two B10 operations documents as describing the superseded protocol. It changes no code, test, workflow, chart or Terraform; `docs/blueprint.md` is protected, so Hari approves and merges, and his merge is the recorded amendment that ADR-021 protocol rule 15 requires.

The replacement text in appendix A is complete: every former slot is filled with the string Hari decided (section 14.1), and the appendix applies with no slot left. Implementing tool: Claude Code (decision [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785), Q13). Hari merges, because `docs/blueprint.md` is a protected path.

## 0. Revision history

- Revision 1 (`3b5e00399a6c8af4dd58eb796a9e93372e3390ff`): first revision, written by a planning session started by the #347 control session on 2026-11-10. A fresh reproduction check, relayed by the control session the same day, reported "Verdict: fail".
- Revision 2 (`b15944c56fbbff8dad0fc2cc521e30bc874d2cd5`): answers that check. A delta check, relayed by the control session on 2026-11-10, reported "Verdict: fail" on two blocking findings; it passed the script, the diff, the docs check, the term sweep and the Revision 1 findings B2 to B5 and N1 to N8.
- Revision 3 (`ac7612c1e379959125b502f7d0b8e015aa4c8784`): answers the delta check. A narrow delta check, relayed by the control session on 2026-11-10, reported "Verdict: fail"; the script, the docs check and both named fixes reproduced, and the normal-run example passed.
- Revision 4 (`41e367cafce2ca0b8c7aa842bf79f09929927be4`): answers the narrow delta check. A further narrow delta check, relayed by the control session on 2026-11-10, reported "Verdict: fail" on two blocking findings; the script, the docs check and the Revision 3 fixes passed, and the normal-run example passed every rule.
- Revision 5 (`e7034cabb8900a24f5dd67866dc935851c2ef1ac`): answers that check. A delta check, relayed by the control session on 2026-11-10, reported "Verdict: fail" on one blocking finding; the script, the docs check, the successor-condition fix, the Q21 framing and examples (a), (d) and (e) passed.
- Revision 6 (`337ac352792f79f192b1c23cec01caeb2f4e1cf5`): answers that check. A delta check, relayed by the control session on 2026-11-10, reported "Verdict: fail" on one blocking finding; the script, the docs check, examples (a), (d) and (e) and the Q21 framing passed, and the bounded clauses feed no verdict, eligibility, comparison or count.
- Revision 7 (`4a6c172ec22ae0f460b33b5bbaebe813fbb0280e`): answers that check. Its narrow delta check found blocking findings at lines 420, 455 and 682 that Hari's Q21 decision resolves.
- Revision 8 (`b471d02d4b77d48d3c122102a0ae22b4b4835f09`): applies Hari's answer to Q21, decision [6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960) (option A(ii)), and folds in that check's non-blocking points. Its delta check passed; posted as pointer [6768580945](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768580945); plan check [6768732716](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768732716) reported three blocking findings, all decisions for Hari.
- Revision 9: records Hari's answers and folds in the plan check's text fixes. Not posted, not checked.

### 0.8 Changes since Revision 8

| Source | Change in Revision 9 |
|---|---|
| Decisions [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785), [6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811), [6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013), [6769416981](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769416981), [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939), [6769760685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769760685), [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) | Every slot filled with the decided string: `[Q1]` `4`, `[Q2]` `100`, `[Q4]` `` `FileStream.Flush()` ``, `[Q6]` option A, `[Q8a]` `120 s`, `[Q8b]` the recommended text, `[Q8c]` the existing `ProducerDrainTimeout`, `[Q9]` option A, `[Q10]` `3`, `[Q11]` option A, `[Q15]` option A, `[Q18a]` option A, `[Q18c]` `3`, `[Q19]` option A, `[Q20]` option B. Section 14 now reads None; section 14.1 records each answer and its decision. Q3, Q5, Q7, Q13, Q14, Q16, Q17 and the fixed successor condition are recorded there too; their text was already written for the chosen options. |
| Plan check B1 (open questions) | Resolved by those decisions. |
| Plan check B2, decision [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) (option B, replacing [6769808426](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769808426)) | No edit. `README.md` line 90 and `docs/agents/factory.md` line 158 stay unchanged: they stay literally true for every repeat that reaches the planned self-kill and do not mention the no-kill exception. Section 2.2 records it. |
| Plan check B3, decision [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939) (Q12) | Section 10: DA1 and DA5, and the time from the cut original's `accepted` record to the self-SIGKILL, are preconditions owned by the offline end-to-end test plan, to be met before the one-repeat trial. DA2, DA3 and DA4 are accepted by name for the one-repeat trial only, with the decision's basis. |
| Plan check P2-1, P2-2, P2-3 (`apply in implementation`) | Applied now, in the exact text the check gave: A12-17 reads "intentionally self-killed first restart instance"; block P rule 15 says "the decision rules (rules 3 and 4 narrowed only for the no-kill end of rule 7)"; L12 cites the no-kill decision. |
| Plan check P2-4, P2-5 | Recorded as notes in section 17: per-instance invalidity is for the execution and verifier plans to pin with a test; stop rule 3 governs over R1's "later `main`" clause. |
| Plan check, stale prose | Section 6's Classification row and the paragraph after the table now state the wider Q19 A scope; the status line is updated. |


### 0.7 Changes since Revision 7

| Source | Change in Revision 8 |
|---|---|
| Decision [6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960) (Q21 option A(ii)) | A no-kill end makes the first restart instance's restart evidence invalid and diagnostic only: no classification, no finding (including no lost-resend finding), not counted toward three-of-five. Block P rule 7 states this and cites the decision; rule 14 drops the control-arm exception; rule 15's amendment record cites the decision. The bounded no-kill clause is removed from A12-2 (line 383), A12-15 (409), A12-18 (425), A13-9 (447), A19-2 (601) and A20-6 (645), which now read as in Revision 5; no ADR-012, ADR-013, ADR-019 or ADR-020 exception sentence is extended for the no-kill end. New A21-21 (decision rule 3, line 694) and A21-22 (decision rule 4, line 695) state the narrowing of decision 6707290153 for the no-kill end only. An intended self-kill, including an empty kill set, keeps its substitution. Q21 is moved to answered; options A(i) and B are dropped from section 14, R6 and stop rule 4. |
| Revision 7 check, B1 and B2 (lines 420, 455, 682) | Resolved by the decision: a no-kill end is invalid there, which now agrees with rules 7 and 14. |
| Revision 7 check, N1 (line 418 cross-restart-only wording) | A12-17 now reads "For B10's first restart instance, ADR-021 substitutes independent abrupt reconciliation of both probe arms;". |
| Revision 7 check, N2 (line 446) | No change: line 446 allows termination without `Flush` or `Dispose` only under the checked intentional self-termination protocol, so the post-admission no-kill exit falls outside it and is invalid, as the decision requires. |
| Revision 7 check, N3 ("only classification") | Moot; the sentence is removed with the A(i) clauses. |
| Section 2.3 storage row | Updated to the approved scope: Revision 7 at `658500af`, approved in 6768160441; the wait at the end of `check_dns`; no `docs/operations/a01-workflow.md` change (decision 6767479810). |
| Q19 | The note on Q21 now says that under A(ii) Q19's options give the same authoritative results, because an incomplete control arm ends on the no-kill path. |


### 0.6 Changes since Revision 6

| Finding | Fix in Revision 7 |
|---|---|
| B1 (blocking): the A(i) partial substitution was still denied at ADR-012 lines 406/409 and 425 and ADR-020 line 645, so examples (b) and (c) were invalid with no classification while rule 7 published a lost control resend | The same bounded clause, "for a no-kill end under its protocol rule 7 only ... for its lost-resend findings", is added at A12-15 (line 409), A12-18 (line 425) and A20-6 (line 645), as at A12-2 (383), A13-9 (447) and A19-2 (601). Line 406, the ordinary "A run is invalid, and produces no classification, if any of these hold:", stays verbatim; for B10 it is read with its B10 sub-sentence at line 409, which now grants that one substitution, so the lost-resend finding is the only classification such a repeat produces. Q21 A(i) and its recommendation now say six sentences gain the clause. |
| N1: scope wording | "lost-resend findings" is used at 383, 409, 425, 447, 601 and 645 and in block P rules 7 and 14, matching rule 7's scope. |
| N2: the resend link lives in `prepared` | Rule 7 now names the control arm's `prepared`, `accepted` and `terminal` records, which carry each resend's link to its original (rule 3). |
| Authority refresh: six storage readiness decisions (6767436384 to 6767526008) | Not authority for this plan; DA4 now cites 6767436384 and 6767506812 for where the 2-minute wait runs. |
| N3: Q21 A(i) said Q19's options can differ for these findings | Corrected: under A(i), Q19's options give the same authoritative results, because the lost-resend finding uses terminal records, not the unreported classes. |


### 0.5 Changes since Revision 5

| Finding | Fix in Revision 6 |
|---|---|
| B1 (blocking): Q21 option A contradicted kept decision rules 3 and 4 (a lost resend is always published, [6707290153](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707290153) point 3; [6707814559](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707814559) point 1), because a no-kill end made all first-restart evidence non-authoritative | Q21 option A now has two sub-options with their consequences. The text is written for A(i), recommended: the authenticated journal's control-arm `terminal` records and the sweep's observations of control resends stay authoritative for the loss findings of decision rules 3 and 4 only. Block P rules 7 and 14 state it. The exception sentences are rechecked and three gain one bounded clause each: A12-2 (ADR-012 line 383), A13-9 (ADR-013 line 447) and A19-2 (ADR-019 line 601). Lines 409, 425 and 645 need none, because they say a missing substitute invalidates the repeat, which still holds. A(ii), a no-kill repeat publishing no loss finding, narrows a kept decision rule and is listed in R6 and stop rule 4. |
| N1: rule 15's journal success for example (a) | Rule 15 now says a no-kill end, including one at an empty control set before the control drain, is not journal success; the trial records its named outcome. |
| N2: is a no-kill repeat's C1 timeout comparison published? | Rule 7: yes, under decision rule 1, by the C1 timeout instance's own checks. |
| N3: line 723 unconditional | A21-14 now says "then, when protocol rule 10 allows, replaces it". |
| N4: crash between `no_kill` and exit on the post-admission path | Rule 7's crash disclosure now covers both paths. |
| N5: example (b) can spend two `ProducerDrainTimeout` bounds | Disclosed in Q18a's counterargument with the policy arithmetic. |
| Section 2.3 shared-file row | The storage readiness plan (Revision 6, pointer 6766962854) no longer touches `docs/operations/b10-workflow.md`; its Q4 targets `docs/operations/a01-workflow.md`. Row corrected; this plan shares no file with it. |


### 0.4 Changes since Revision 4

| Finding | Fix in Revision 5 |
|---|---|
| 1 (blocking): the no-kill end had no evidence substitution in ADR-012, ADR-013, ADR-019 and ADR-020, so the same run was invalid there and a valid ineligible repeat under ADR-021 | New Q21 with two options and their consequences. The text is written for option A, recommended: a no-kill end invalidates the first restart instance's restart evidence, which is diagnostic only. Block P rule 7 states it, and says the no-kill exit code does not gate and that a crash during the no-kill `Flush` or `Dispose` is not distinguishable but invalid either way. Rule 14 no longer treats a no-kill end's first-restart evidence as authoritative; an intended self-kill without a successor (empty kill set) stays authoritative under the existing self-termination exceptions. No ADR-012, ADR-013, ADR-019 or ADR-020 exception is extended. Option B, with every sentence it would extend, is listed in R6 and stop rule 4 as needing a revision. |
| 2 (blocking): successor sentences still unconditional | A20-7 (line 656), A21-1 (674), A21-19 (725), A21-5 (682: the control resends its set; the successor, when started, resends the kill set), A12-2 (383), A13-1 (446), A13-2 (454) and A13-4 (455) now apply to the successor only when it started. A script sweep of lines 380 to 730 finds no remaining unconditional successor requirement. |
| `[Q18c]` had no consumer; a crash inside Q18a's `Flush`/`Dispose` was undisclosed | Rule 7: the no-kill exit code is a labelled local observation and does not gate; the crash case is disclosed and, under Q21 A, cannot produce authoritative evidence. |
| Q20 B against the fixed "Policy cap: one trial"; unknown termination | Q20 now says that option B narrows what the fixed cap counts, rather than leaving the cap untouched. It also says that a trial with unknown termination (`safety_stop`) counts as the one trial under both options. |
| Line 681 "cross-restart-arm observation gaps" under Q19 A | A21-17 now reads "probe-arm observation gaps". |
| Decision rule 5 (line 696) and control resends without terminal records | A21-20 now reads "An accepted journaled probe attempt without a terminal record has no persistence status". |
| Section 6 "Restart (683)" row | Updated for up to three Jobs and two registered instances without a successor. |
| `[Q18a]`-before-`[Q18c]` fill order | Stated in the header and in Q18. |


### 0.3 Changes since Revision 3

| Finding | Fix in Revision 4 |
|---|---|
| B1: Permitted conditions (line 681) still named "unconfirmed cross-restart originals" | A21-4 now reads "accepted journaled probe attempts without terminal records (protocol rule 3)", the same scope as rule 3 under Q19 A. Section 6 row corrected. |
| B2: sentences assumed the successor always starts | Made conditional on the successor having started: A21-6 (three registered C1 instances when the successor started, two when rule 10 did not start it; "up to three" Jobs; successor's ordinary files "when it started"), A21-7, A21-12, A19-2, A12-6, A12-8, A12-9, A12-10, A12-16, A20-4, A20-5, and block P rules 1, 5, 8, 10, 12 and 14. Rule 14 now accepts, for a repeat without a successor, the outcome that rule 6 or 7 names and no successor evidence, so its per-arm counts stay authoritative. Rule 15's journal success criteria require successor release only when rule 10 starts the successor; whether a trial that ends without a successor counts as the one trial is new slot `[Q20]` and Q20. |
| B3: Q18a option A (Flush and Dispose) contradicted rule 7's no-Flush rule after cross-restart admission | Rule 7 now splits the no-kill path: before the cross-restart arm's first original is admitted, the child ends under `[Q18a]`; after it, the child exits without `Flush` or `Dispose`, as rule 7 already requires. The exit code is new slot `[Q18c]`. |
| B4: Q18b option B contradicted rule 6 | Q18b is removed. Rule 10 states the successor condition as fixed text; starting a successor on an empty kill set is listed in R6 and stop rule 4 as needing a revision, with the sentences it changes (Q18). |
| N1: rule 7's trigger matched every control arm | Now "when the child ends without reaching `kill_intent`". |
| N2: R6 and stop rule 4 disagreed | Both now list the same questions whose answers need text outside a slot: Q1 (B, C), Q3 (B), Q5 (other than the default `emptyDir`), Q10 (second option), Q16 (B), Q18 (a successor on an empty kill set), Q19 (B). Q6, Q9 and Q15 are removed from that list because every one of their options fits its slot. In Revision 3 stop rule 4 listed Q6 and R6 did not, and Revision 2's lists had included Q9 and Q15. |
| Example (b): control resends without a terminal record had no class | The unreported scope now covers every accepted journaled probe attempt (originals and control resends) without a terminal record. Q19's fills, A12-11, A12-12, A12-13, A12-19, A13-3, A20-9, A21-4, A21-18 and rules 13 and 14 say "accepted journaled probe attempts". Successor resends stay in the successor's ordinary ledger. Decision rule 3 still counts only cross-restart originals for the positive pair. |


### 0.2 Changes since Revision 2

| Finding | Fix in Revision 3 |
|---|---|
| 1 (blocking): the no-kill end is undefined and contradicts itself | Block P rule 6 sends an empty control set, an incomplete control arm and any other failure before `kill_intent` to a defined no-kill path. Rule 7 defines it: a durable `no_kill` record naming the outcome, then the end in slot `[Q18a]`. An exit with `no_kill` and no `kill_intent` is a known termination, not an unexpected exit, and fails eligibility; an exit with neither record is unexpected. Rule 9's "Unexpected crash/prior exit" points to rule 7. Rule 10: a no-kill end does not set `safety_stop`, and the workflow starts the successor only under slot `[Q18b]`; A21-6 cites rule 10. New Q18 (how the child ends, its exit code, and when the successor starts) and a section 6 row. |
| 2 (blocking): incomplete-control originals classified inconsistently | Rule 3 now puts the scope of `unreported_seen`/`unreported_unseen` in slot `[Q19]`. Option A, written through ADR-012, ADR-013, ADR-020 and the Classification bullet: every accepted probe original without a terminal record, in either arm. A complete control arm has none, so only cross-restart originals can have them in an eligible repeat. A12-11, A12-12, A12-13, A12-19, A13-3, A20-9 and A21-18 now say "accepted probe originals without a terminal record". Decision rule 3 (A21-10) keeps the positive pair to cross-restart originals. Rule 13 and the rule 14 bound are per arm. Section 6's protocol 14 row is corrected. New Q19. |
| NB1: sigaction leftover | Rule 6's sentence is removed; rule 7 cites sigaction only for "SIGKILL cannot be caught or ignored". |
| NB2: is `synchronous_rejection` a terminal record? | Rule 3 now says it is, and that its attempt has no `accepted` record and so belongs to no unconfirmed set. "Accepted" is restored in every unreported-class sentence and in rules 13 and 14. |
| NB3: exact slot strings; Q8b named a failure | Section 14 gives exact fill strings for every slot. Q8b's fill names no failure, so section 3 stays true. |
| NB4: Q15 option A versus rule 2 | Rule 2 now says the exit code is a labelled local observation and that rule 7 states whether it gates. Q15's native-crash statement is labelled inference. |
| NB5: "an cross-restart original" at line 454 | A13-3 now covers "an" and reads "an accepted probe original without a terminal record". |
| NB6: line 659 heading "in a second Job" | Kept for its anchors (section 2.2); Q6 now discloses that under option A the successor is the third Job. |
| NB7: decision rule 5 "A no-report original is `unconfirmed`" | New A21-20: "An accepted original without a terminal record has no persistence status and is never inferred `PossiblyPersisted`." |
| NB8: empty kill set and the successor | Q18b's options decide it; rule 10 and A21-6 no longer start the successor unconditionally. |


### 0.1 Changes since Revision 1

| Finding | Fix in Revision 2 |
|---|---|
| B1: unconfirmed control originals classified two ways; rule 14 bound fails every eligible repeat | "Unconfirmed" is now set membership only (block P rule 3). `unreported_seen`/`unreported_unseen` and the rule 14 bound apply only to originals with no complete terminal record in the journal, which rule 5 confines to the cross-restart arm. A12-11, A12-12, A12-13, A13-3, A20-9, A21-10 and A21-11 narrowed; new A12-19 (line 429) and A21-18 (Classification, line 685); rule 13 counts split into unconfirmed-set size and originals without a terminal record. Section 6 rows added. |
| B2: line 725 "both Jobs' expected names" | New A21-19: "all three C1 Jobs' expected names". |
| B3: stale "first producer", "either", "kill probe" | New A12-15 (409), A12-16 (410), A12-17 (418), A12-18 (425), A13-8 (435), A13-9 (447), A19-3 (601), A20-10 (656), A21-17 (681); A19-2 widened to cover 601's first instance. A script sweep of lines 360 to 735 after applying finds no remaining B10 use of these terms. "First restart producer" is used throughout, because under Q6 option A "first producer" would name the timeout producer. |
| B4: undisclosed weakening of termination evidence | Rule 7 now states that the journal holds no record of the signal request, so a crash between `kill_intent` and the signal is not distinguishable by journal evidence, and adds slot `[Q15]` for the parent recording the observed exit status as competing evidence. Rule 9's limits sentence names this gap. Rule 15 restores main's sentence "It cannot test future verifier kill/crash separation; production-byte synthetic tests and science cover that." Section 6 rows for protocol 2, 7, 9 and 15 now state the evidence effect. New Q15. |
| B5: parent does more than point 4's "only" | New Q16 puts the interpretation of point 4 against point 7 to Hari. |
| N1: dropped trial criteria | Rule 15 restates journal success criteria for the new path and "No C0 or replacement trial." |
| N2: double period at the Q9 slot | Slot now reads `[Q9] Reconcile`; each Q9 option ends with its own period. |
| N3: Q2 has no recommended value; Q12 note has no options | Q2 now gives a policy recommendation with its basis, labelled estimate. The legacy three-Job ownership is now Q17 with options and a recommendation. |
| N4: Q3 option B citation | Option B now also cites 6706730103 point 6. |
| N5: protected-path statements | `docs/operations/` is not protected (`.github/CODEOWNERS` lists it with no owner). Sections 2.1 and 8 corrected; section 8 step 6 names `Parchwell.Verifier*/`. |
| N6: carry-over overstated | Section 7 names the PR3b arm-object and envelope members that change. |
| N7: appendix B writes files after FAIL | Part 1 now writes nothing and exits 1 on any failure. |
| N8: process text in OW-1 | The trial-gate sentence is removed from OW-1; ADR-021 rule 15 states it once. |


## 1. Goal and authority

Goal: state the redesigned B10 restart test once, in ADR-021, so that the implementation pull requests have a governing design text, and so that no ADR sentence still describes the stop/checkpoint supervisor protocol as the design.

Authority, in creation order (catch-up of issue 404 on 2026-11-10: 197 comments, read by script across two pages; state file on `main` covers comment 6761783572; new comments 6761981257, 6762055013, 6762156869, 6762516530, 6764508653, 6764643941, 6765011642, all read in full):

1. Issue body of #404 and the decisions in `items/404/state.md` "Decisions in force" on `main`. Those that bind this plan, each read in full:
   - [6705498674](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705498674): B10 must test the producer restart (K-53); `not_tested` is not acceptable.
   - [6705801800](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800): kill a producer while sends are unconfirmed, then resend from a new producer; separate restart verdict; fatal capture; each ADR amendment listed with the sentence it replaces; no `docs/adr/`.
   - [6706730103](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6706730103): no post-acceptance statistics gate; the pre-acceptance report stays; 3 of 5 eligibility; one diagnostic trial before science; held restart verdict; publications state a process was killed, not a pod or node; keep the matched same-instance control.
   - [6707290153](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707290153): ordinary timeouts retry in the same instance; C0 has no probes and no kill; lost resend is a separate finding.
   - [6707306269](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707306269): the complete matched control is an eligibility gate (point 1); post-trial changes limited to implementation bugs and mechanical budgets (point 3); .NET 10, `Process.Start`, libc `kill` via `DllImport`, `WaitForExit`, no new project or package (point 4).
   - [6707314831](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707314831): exact text with positions; protocol values labelled policy.
   - [6707814559](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707814559): ordinary ADR-012, ADR-013 and ADR-020 sentences verbatim, B10 exceptions appended; loss in ineligible repeats; smoke-only rule.
   - [6739289644](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739289644), [6739625843](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739625843), [6739632202](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739632202), [6739644842](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739644842): the burst, `ProbeBurstSize` 5, one selected original resent per arm. Superseded by item 2 below, points 2, 4 and 5 (section 6 states each supersession).
   - [6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101): the next step is a one-repeat diagnostic observation, not a verdict.
2. [Decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530) (2026-11-10), option 2, points 1 to 7, its consequences and "Still for Hari". Verified against the control session's restatement: the seven points match. Its "Still for Hari" list adds "the kill point's policy (position, seed and rate)" and "the journal's durability on `emptyDir` across SIGKILL (unverified)".
3. [Decision 6765011642](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6765011642) (2026-11-10): the storage readiness fix is a fixed 2-minute wait after the ephemeral apply and before the first worker Job.
4. Decision [6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960) (2026-11-10): this plan's Q21, option A(ii). A no-kill end makes the first restart instance's restart evidence invalid and diagnostic only, with no classification and no finding, including no lost-resend finding, and it does not count toward three-of-five. No ADR-012, ADR-013, ADR-019 or ADR-020 exception sentence is extended for it. This narrows, for the no-kill end only, decision 6707290153's rule that a lost resend is always published, and ADR-021 decision rules 3 and 4 accordingly. An intended self-kill, including one with an empty kill set, keeps its substitution. The C1 timeout instance keeps its own checks and comparison.

5. Decisions on this plan's open questions, all 2026-11-10: [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785), [6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811), [6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013), [6769416981](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769416981), [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939), [6769760685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769760685), [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) (which replaces [6769808426](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769808426)). Section 14.1 records each. Decision [6769877985](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769877985) approves this revision conditionally: the control session posts the `## Approval` only if a fresh check finds that it folds in only those answers and the Revision 8 plan check's named text fixes, and the newest `## Plan check` names its commit with `Blocking findings: none`.

Not authority for this plan: [6762055013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762055013) (Q13, which 6762516530 says was misframed and will be asked again); [standing decision 6752430614](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6752430614) (four named pull requests; this is not one); the design review findings relayed in the planning prompt (input to verify, checked in section 11).

## 2. Scope

### 2.1 Files allowed to change

1. `docs/blueprint.md`: exactly the 64 sentence edits and the one block replacement (block P) in appendix A, at the lines given there on `0f769fb2`. Protected path (`/docs/` in `.github/CODEOWNERS`).
2. `docs/operations/b10-abrupt-contract.md`: one status paragraph after line 3 (edit OC-1). Not protected: `.github/CODEOWNERS` lists `/docs/operations/` with no owner.
3. `docs/operations/b10-workflow.md`: one paragraph after line 59 (edit OW-1). Not protected, as item 2. It states what the section describes; the trial gate is stated once, in ADR-021 rule 15, not here.
4. A receipt, `items/404/restart-redesign-receipt.md`, under docs/agents/pull-requests.md rule 8.

No state-file change: the implementing session of this design pull request updates `items/404/state.md` only if docs/agents/factory.md "Item state file" makes it the writer for this pull request; it is an implementation pull request of the issue, so it does, and the update is one "Decisions in force" line for this approval plus "Next action".

### 2.2 Paths that must not change

`src/**`, `tests/**`, `charts/**`, `infra/**`, `.github/**`, `docs/operations/workflows/**`, `scenarios/**` (no `expected.yaml` exists or is created), `results/**`, `README.md`, `docs/agents/**`, `AGENTS.md`, `CLAUDE.md`, every other file under `docs/operations/`, and every line of `docs/blueprint.md` outside appendix A. The ADR-021 heading (line 659) stays unchanged because three operations documents link to its anchor (`loadgen-contract.md`, `verifier-contract.md` line 211, `b10-workflow.md` line 55); "one producer restart in a second Job" stays true, since the successor is still a later Job.

README.md lines 28 and 90 and docs/agents/factory.md "Evidence pack" lines 156 and 158 stay unchanged. The verdict words, the subject, `held`, and "same-instance and cross-restart pair evidence, intended-termination evidence" are unchanged. Line 90 of README.md and line 158 of factory.md say ADR-021 rules 3 and 4 publish lost-resend findings from eligible and ineligible C1 repeats. Under decision [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248) (option B) they stay as written: literally true for every repeat that reaches the planned self-kill, and silent on the no-kill exception of decision 6768228960. Hari chose this having been told that a reader could expect a finding from every ineligible repeat.

### 2.3 Files shared with other open plans on issue 404

| Other plan | Shared file | What it may change there | Effect on this plan |
|---|---|---|---|
| Storage readiness race fix (`items/404/plan-storage-readiness-race-fix.md`, Revision 7 at `658500af`, approved in [6768160441](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768160441)) | none | `infra/scripts/phase/phase.py` (the wait at the end of `check_dns`), `infra/scripts/phase/tests/test_phase.py`, a new workflow-order test, the state file and a receipt; no file under `docs/` (decision 6767479810). | None; stop rule 3 still applies if it ever changes one of this plan's files. |
| Storage readiness race fix | `docs/blueprint.md` | Listed as must-not-change there. | None. |
| PR3b classification (Revision 2 at `37128767`, paused) | `docs/operations/b10-abrupt-contract.md` | A new section "Nonfinal abrupt result" and a correction of line 71. | Stop rule 3. |
| PR3b classification | `docs/blueprint.md` | Must-not-change there. | None. |
| Offline end-to-end test (Revision 1 at `f75d6ac3`, held) | none | Test files only. | None. |

## 3. External contracts

This pull request pins design text, which every implementation plan and the verifier's closed check list (ADR-019) read. The pinned items:

- The ADR text in appendix A, verbatim, with each slot filled by Hari's answer.
- Failure and outcome names used in ADR-021, all retained from `main`: `b10_abrupt_exposure_inconclusive`, `kill_too_late` with detail `no_unconfirmed_attempt`, `kill_too_early`, `safety_stop`, `unknown_due_to_intended_kill`, `no_cross_restart_duplicate_observed`, verdict words `confirmed`, `untestable`, `held`. No failure name is added to ADR text. The operations contract's other names are the implementation pull request's contract (Q7).
- Probe keys `B10/{run_id}/{configuration}/{repeat}/same_instance` and `.../cross_restart`, unchanged; probe sequence changes from burst index to probe ordinal.
- Arm names `same_instance` and `cross_restart`, and journal record kinds named in ADR-021 rules 3 and 7: `prepared`, `accepted`, `synchronous_rejection`, `terminal`, `cut`, `kill_intent`, `no_kill`. The wire members of each kind belong to the execution pull request's contract.
- The restart subject sentence (line 690), the decision rules' verdict logic and the three-of-five threshold: unchanged.

## 4. Requirements

R1. WHEN the implementer applies appendix A to the three files at `0f769fb2`, or at a later `main` on which those three files are byte-identical to `0f769fb2` (stop rule 3), THEN every listed old string is replaced by its new string, lines 702 to 718 are replaced by block P, and no other byte changes.

R2. WHEN any old string of appendix A occurs zero or more than once in the file on the implementer's `main`, THEN the implementer stops under stop rule 2.

R3. WHEN the replacement is complete, THEN no `[Q` slot remains in any changed file (acceptance command 3).

R4. WHEN the changed files are scanned, THEN they contain only ASCII characters and no em or en dash.

R5. WHEN `docs/operations/b10-abrupt-contract.md` and `docs/operations/b10-workflow.md` are changed, THEN only edits OC-1 and OW-1 are applied, and every other line, including the wire, policy and failure-name tables, stays byte-identical, because they describe the code on `main`.

R6. WHEN Hari records a later decision that changes any answer in section 14.1, THEN the plan is revised before implementation (design-changing under factory.md "Item lifecycle" step 1), not edited in implementation.

## 5. Invariants and safety rules

- No code, test, workflow, chart, Terraform, scenario or results change. No `expected.yaml`. No trial, dispatch or Azure access (AGENTS.md hard safety rules 1 to 9).
- Every ordinary (non-B10) sentence of ADR-012, ADR-013, ADR-019 and ADR-020 stays verbatim (decision 6707814559 point 4). Appendix A edits only B10-exception sentences; acceptance command 4 checks that each edited line already contained "B10" or "ADR-021" on `0f769fb2`, except the amendment log line 12.
- No number is presented as measured. Every policy value in the new text is a slot filled by Hari, or an existing policy value carried unchanged and labelled policy (record 65536 bytes, queue 256, journal 16777216 bytes and 4096 records).
- The new text never claims process-death durability, emptyDir behaviour or exposure likelihood as fact beyond its cited source; the inferred parts are labelled.
- The Codex mention is never written (hard safety rule 10).

## 6. Map of every current ADR-021 rule

Line numbers are on `0f769fb2`. "Evidence effect" says what the change does to the evidence a verdict rests on.

| Rule (line) | Change | How or why | Evidence effect |
|---|---|---|---|
| Context (661, 663) | Unchanged | Still true; no new Kafka claim needed. | None. |
| Option 7 sentence (674) | Reworded (A21-1) | Child kills itself. | None. |
| Option 9 (677) | Kept, option 10 added (A21-2) | Records the simplification as an option. | None. |
| Decision (679) | Reworded (A21-3) | Option 10 replaces 6 and 9; cites 6762516530. | None. |
| Scope (680) | Unchanged | | None. |
| Permitted conditions (681) | Reworded (A21-4, A21-17) | "stop/termination" to "self-termination"; "accepted-unconfirmed kill originals" to "accepted journaled probe attempts without terminal records (protocol rule 3)". | The exception covers the same attempts that rule 3 classes as unreported, in either arm. |
| Retries (682) | Reworded (A21-5) | Timeout retries in the timeout instance; successor and control resend every unconfirmed member once (6762516530 point 5, superseding 6739644842's single selected original). | More resends per repeat; more pairs possible. |
| Restart (683) | Reworded (A21-6) | Up to three Jobs in C1; parent/child; no stop; the successor starts only as protocol rule 10 allows. | Three registered C1 instances with a successor, two without; the first restart instance has no ordinary seal; a no-kill end gives no authoritative restart evidence (Q21). |
| Per-instance evidence (684) | Reworded (A21-7) | Substitution moves from "kill probe" to "first restart instance". | Control evidence comes from the journal, not a seal. |
| Classification (685) | Reworded (A21-18) | Unreported classes apply to accepted journaled probe attempts without a terminal record, in either arm (Q19 A); set membership is not a class. | Wider than main, which limited them to accepted kill originals; a complete control arm has none, and a no-kill end is invalid. |
| Late-write scan (686) | Unchanged | 6762516530 point 7 keeps the sweep. | None. |
| Subject (690) | Unchanged | | None. |
| Decision rule 1 (692) | Reworded (A21-8) | "first instance" to "timeout instance". | C1 timeout counts come from their own Job. |
| Decision rule 2 (693) | Reworded (A21-9) | Three identities; journal unconfirmed set; one resend per member. | Eligibility gate unchanged in kind. |
| Decision rule 3 (694) | Reworded (A21-10, A21-21) | "selected original without a genuine terminal report" to "cross-restart original without a terminal record". | Any such member can form the positive pair, not only the lowest-ordinal one. A21-21 narrows the always-published lost-resend rule for the no-kill end only (decision 6768228960). |
| Decision rule 4 (695) | Reworded (A21-22) | A no-kill end produces no loss finding (decision 6768228960). | Narrows decision 6707290153 for the no-kill end only: a real lost resend in such a run is not reported. |
| Decision rule 5 (696) | Reworded (A21-11, A21-20) | Count names; "no-report original is unconfirmed" becomes "accepted original without a terminal record has no persistence status". | None. |
| Decision rule 6 (697) | Reworded (A21-12) | Seals removed (point 4); timeout instance keeps ordinary drain; control completeness from rule 5. | Control completeness is checked from the journal. |
| Decision rule 7 (698) | Reworded (A21-13) | Disclose self-kill, cut position and rate. | Publication wording. |
| Protocol intro (702) | Reworded (block P) | Policy list updated; "PR2" to "the implementation pull requests". | None. |
| Protocol 1 (704) | Reworded | Fixed-rate probes to a cut ordinal, slots Q1, Q2, Q6; burst retired. | Exposure now depends on rate and cut (risk, section 9). |
| Protocol 2 (705) | Reworded | Supervisor becomes a parent that starts, waits and publishes (Q16 on what else it does); child self-kill; `/proc` stop confirmation removed. | Weaker intended-termination evidence: main required intent, an owned stopped target, a recorded signal request, observed death and competing evidence; the new chain has intent, no later producing-thread record, observed death and competing evidence, and no record of the signal request (Q15). |
| Protocol 3 (706) | Reworded | Durable `prepared`/`accepted` now in the child's own journal; callbacks enqueue only; slots Q4, Q9; "unconfirmed" defined as set membership, not a class. | "Durable" now rests on the local file across SIGKILL (DA1). Unreported classes stay limited to originals with no terminal record. |
| Protocol 4 (707) | Reworded | IPC frame/queue become record/callback-queue limits; values kept. | None. |
| Protocol 5 (708) | Reworded | Pre-acceptance gate kept (6706730103 point 1); report bound in the journal instead of a pipe hand-off; control completeness defined (slot Q8c). | Same gate. |
| Protocol 6 (709) | Reworded | Stop, checkpoint and 30 s arm/stop budget removed (point 4); cuts defined on journal order; unconfirmed defined as point 6 states. | Kill set is "at death"; control set is "before the cut record" (asymmetry, Q3 counterargument). |
| No-kill path (rules 6, 7, 9, 10, 14, 15) | New (decision 6768228960, Q21 A(ii)) | Defines the end of the child when it ends without reaching `kill_intent` (slots Q18a, Q18c); rule 10 starts the successor only after an intended self-kill with a nonempty kill set. | A named exposure outcome is no longer read as an unexpected exit. The first restart instance's restart evidence is invalid and diagnostic only, with no classification and no finding, including no lost-resend finding; the repeat has two registered C1 instances and does not count toward three-of-five; the C1 timeout instance keeps its own checks and comparison. Whether such a trial uses the one trial is Q20. |
| Protocol 7 (710) | Reworded | No SIGCONT; control resend at cut plus `ReleaseDelay` (slot Q8a); kill/reap/EOF 10 s budgets removed; exit wait slot Q8b; the crash-before-signal gap stated, slot Q15. | A crash after `kill_intent` and before the signal would pass as an intended kill on journal evidence; its unconfirmed set would be resent and could form a positive pair. Q15 option A narrows this; nothing closes it. |
| Protocol 8 (711) | Reworded | Post-death drain to EOF removed (no pipe); successor recomputes the set from the blob journal and resends all members. | Successor no longer depends on a final selection file. |
| Protocol 9 (712) | Reworded | OOM check moves to the parent, before start and after death; limits sentence adds the crash-before-signal gap. | Same OOM evidence; one more disclosed limit. |
| Protocol 10 (713) | Reworded | Parent publishes journal after death; emptyDir loss on pod removal stated. | Same publication gate. |
| Protocol 11 (714) | Reworded | "one checkpoint per arm" to "one cut per arm"; stop failure removed. | None. |
| Protocol 12 (715) | Reworded | Timeout/control seals removed (point 4); independence of the instances kept, the successor's only when it started. | Removes seal-based evidence; control evidence is journal-based. |
| Protocol 13 (716) | Reworded | Per-arm counts split into unconfirmed-set size and originals without a terminal record. | None. |
| Protocol 14 (717) | Reworded | Schema version slot Q10, distinguishing schema-2 freeze evidence; the bound is per arm: unreported-seen plus unseen equal to accepted originals without a terminal record. | The bound holds in every arm, complete or not; a complete control arm has a count of 0. |
| Protocol 15 (718) | Reworded | Records the 2026-11-10 amendment, retires the burst, restates the smoke-only and one-trial rule, the journal success criteria (successor release only when rule 10 starts it) and "No C0 or replacement trial" for the new path, keeps main's kill/crash-separation sentence, slots Q11 and Q20. | Trial criteria change with the protocol: no stop/resume or control seal; adds control completeness and the self-kill. |
| Consequences (721, 722, 724, 726, 727) | Unchanged | | None. |
| Consequence (723) | Reworded (A21-14) | Resend every unconfirmed member. | As rule 3. |
| Consequence (725) | Reworded (A21-15, A21-16, A21-19) | Self-termination; "second Job" to "successor Job"; three C1 Jobs' names. | None. |

In ADR-012, ADR-013 and ADR-020, the B10 sentences that named "accepted-unconfirmed" or "unconfirmed" originals as an outcome now name "accepted journaled probe attempts without a terminal record" (A12-11, A12-12, A12-13, A12-19, A13-3, A20-9), the wider scope of Q19 option A.

ADR-020's process-launch exception (line 637) narrows: the parent may only start, wait and publish, and sends no signal; the child may signal only itself (A20-3).

What the removals cost the evidence: without the stop, no one observes the child's state at the kill instant; without seals, the control arm has no separately sealed inventory, only the journal and the topic read-back; without the post-death EOF drain, a callback whose record was still queued at death is unconfirmed by definition (point 6), which is in the restart subject's scope ("durably identified but unconfirmed"), not a harness error.

## 7. What carries over

From merged code on `0f769fb2` (inference from file names and the contract; the execution plan confirms each): registration (`B10AbruptHost`, registration marker), wire envelope and identity (`B10AbruptWire`), journal record writing (`B10AbruptJournal`, now in the child), statistics capture and pre-acceptance selection (`B10AbruptStatistics`), fatal and native-error capture, cgroup reads (`B10AbruptCgroup`), create-only publication and read-back (`B10AbruptPublisher`), the successor's binding and resend path (with a new input: the journal, not `kill-selection.json`), the workflow's first-pod termination observation in `infra/scripts/phase/b10_abrupt.py`, and failure-report observability. Removed: `OwnedProducerProcess` STOP/CONT and `/proc` stop polling, `B10AbruptChildChannel` pipe framing, checkpoint and seal objects in `B10AbruptSupervisor`, and their tests.

From the held plans: PR3b Revision 2 (`37128767`): readers, independent reconciliation, topic read-back with value hash and per-arm classification carry over (decision 6762516530 consequences). Its inputs and its `nonfinal_result` arm object change. Inputs: its envelope set (`timeout-seal.json`, `control-seal.json`, `kill-selection.json`; its lines 107 and 182 to 184) and the checkpoint become the journal, the abrupt manifest, the termination receipt and the C1 timeout Job's ordinary files. Arm object (its line 131): `checkpoint_unconfirmed`, `selected_attempt_id`, `selected_sequence`, `selected_original_seen`, `resend_attempt_id`, `resend_invoked` and `resend_terminal` assume one selected original and one resend per arm; under "resend every member" they become per-member lists or counts. Offline end-to-end test Revision 1 (`f75d6ac3`): its retargeted test (real child, real local broker, real self-SIGKILL, `FileStorage` publication, real successor) is the main offline test of the new path; its literals wait for this text.

## 8. Implementation sequence (estimates)

Every effort figure is an estimate, made 2026-11-10 by this planning session, based on the line counts of the files each pull request replaces, counted by script on `0f769fb2` (for example `B10AbruptSupervisor.cs` 827 lines, `B10AbruptSupervisorTests.cs` 1573, `B10AbruptProducer.cs` 727, `b10_abrupt.py` 722, `b10-abrupt-trial.yml` 273), and on the earlier pull requests of this issue. No duration was measured.

1. This design pull request. Protected (`docs/`). Estimate: one session.
2. Storage readiness race fix (already planned; independent; must merge before any trial under 6762516530).
3. Execution: child journal, cuts, self-kill, parent start/wait/publish, successor from journal, C1 timeout Job, chart and phase changes, operations contract rewrite, removal of the stop protocol. Paths: `src/Parchwell.LoadGen/**`, `tests/Parchwell.LoadGen.Tests/**`, `charts/**`, `infra/scripts/phase/**`, `docs/operations/**`, and `.github/workflows/b10-abrupt-trial.yml` with its byte-identical copy under `docs/operations/workflows/` (AGENTS.md hard safety rule 8) if the timeout Job is added there. Protected among these: only `.github/workflows/` (`/.*/` in `.github/CODEOWNERS`), and any new `*.csproj` (none is planned); `charts/`, `tests/`, `infra/scripts/` and `docs/operations/` have no owner there. If the workflow changes, Hari merges. Estimate: 2 to 4 sessions, possibly split into a child/parent pull request and a workflow/phase pull request.
4. Offline end-to-end test, retargeted (held plan). Estimate: one session after 3.
5. Live diagnostic trial (Hari dispatches; new explicit approval needed).
6. PR3b classification, revised to journal-derived sets; then PR3c and the five-repeat work as already split (6750021224). Paths: `src/Parchwell.Verifier/**` and `tests/Parchwell.Verifier.Tests/**`, protected (`Parchwell.Verifier*/` in `.github/CODEOWNERS`); Hari merges. Estimate for the PR3b revision: one planning session.

The legacy two-Job scenario path (`scenario.yml`, `B10TwoInstanceVerify`, `B10ExpectedProducerJobs`) assumes two C1 producer Jobs; the three-Job C1 repeat needs its own change before the five-repeat governed run, not before the one-repeat trial. Which pull request owns it is Q17.

## 9. Never-ran inventory for the redesigned path

None of these stages has executed live, and most have not executed at all. Each implementation plan must give an offline test with real processes for its stages.

| Stage | Executed anywhere? | Offline test the implementation plans must give |
|---|---|---|
| C1 timeout cohort in its own Job | Not as a separate Job; the cohort ran inside the first instance in trials | Phase/chart test plus the existing ordinary-producer tests on the C1 configuration |
| Parent writes registration, starts child, waits | Partly (old supervisor start path) | Real child process start and `WaitForExit` |
| Child journal: prepared/accepted by producing thread, terminals by writer thread, flush per record | Scratch probes only (section 11) | Real child with real Confluent.Kafka producer against local Kafka |
| Control cut, release, resend, drain, completeness | No | Same test, with the control set asserted from the journal |
| Kill intent then self-SIGKILL | Scratch probes only | Same test, asserting exit and journal content |
| Exposure: nonempty kill set at death | Not on Event Hubs; locally only under injected delay (planning-session report, not re-run here) | Same test with Toxiproxy latency, plus the live trial |
| Parent reads, hashes and publishes the journal after death | No | `FileStorage` publication in the offline end-to-end test |
| Workflow first-pod termination check with the new pod shape | Old shape ran in trials | Existing phase tests, updated |
| Successor reads journal from blob and resends all members | No (old successor read a selection file) | Offline end-to-end test |
| Verifier join of three instances | No | PR3b revision tests on produced bytes |

## 10. Deployment assumptions

Hari's decision [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939) (Q12) settles each. DA1 and DA5 are preconditions owned by the offline end-to-end test plan (`items/404/plan-offline-e2e-test.md`, retargeted to this redesign): that test must show them before the one-repeat trial, and it also measures the time from the cut original's `accepted` record to the self-SIGKILL, on which DA2 depends. DA2, DA3 and DA4 are accepted by name for the one-repeat trial only. The decision's basis for DA2, from earlier diagnostic trials on this namespace and stated there as single readings, not a measured distribution: trial 5 recorded an original acknowledged 11.9 ms after produce; trial 7 had 2 of 5 originals unconfirmed at a stop 5 ms after acceptance; trial 8 logged `produce_to_terminal_milliseconds` 10. Its inference: the cut original is unconfirmed at the kill if the acceptance-to-kill time is below about 10 ms.

- DA1. On the AKS node, bytes the child wrote to its `emptyDir` journal and flushed under the Q4 rule before its self-SIGKILL are all present when the parent reads the file after death. Basis: POSIX `write()` Rationale (inference); scratch measurement on ext4 here (section 11). Not tested on AKS.
- DA2. On Event Hubs, with the Q1 cut and Q2 rate, at least one cross-restart original has no durable terminal record at death in most repeats. Not measured; the three-of-five rule absorbs some empty kills, and fewer than three eligible repeats holds the verdict.
- DA3. `memory.events.local` is readable from the parent in the deployed pod (carried from current rule 9; not verified).
- DA4. The successor's Blob read of the journal, written by the parent minutes earlier in the same account, needs no readiness handling beyond decision 6765011642's 2-minute wait, which decisions [6767436384](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6767436384) (at the end of `Phase.check_dns`) and [6767506812](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6767506812) (every job that runs `check-dns`, including `b10-abrupt-trial.yml` job `trial`) place before the first worker Job of the B10 trial. Basis: the wait precedes the first worker Job; every B10 blob write follows it; Azure Storage documents strong consistency for reads after a completed write. Inference, not tested live.
- DA5. `Process.WaitForExit` observes the child's death without a parent-side poll of any pipe. The parent holds no pipe, so the EINTR race on `poll(fd, 1, 0)` reported for `OwnedProducerProcess.EventsReady` cannot recur (signal(7) lists `poll` among calls never restarted; inferred from the planning-session report, not re-read here).

## 11. Verification of the input findings

Run in this session's scratch workspace on 2026-11-10; nothing committed.

- Journal durability across self-SIGKILL. Scratch .NET 10 program: a `FileStream` with a 4096-byte buffer, a background thread appending records concurrently through a lock, the main thread appending 2000 `prepared` and 2000 `accepted` records, one `kill_intent`, then `kill(getpid(), 9)`. Filesystem ext4, kernel 6.18. Results: `Flush()` after each record, 20 of 20 runs kept all 4001 main-thread records; no flush, 19 of 20; `Flush(true)` after each record, 20 of 20. Every run exited 137. Measured here only; not `emptyDir` on AKS. Consistent with the earlier report (50 of 50 with `Flush()`), which this run does not replace. The Revision 1 reproduction check reran it: `Flush()` and `Flush(true)` 20 of 20, no flush 14 of 20. The no-flush variant is not recommended and ships in no text.
- librdkafka v2.16.0 CONFIGURATION.md, `transactional.id`: "used to identify the same transactional producer instance across process restarts"; "If no transactional.id is provided, then the producer is limited to idempotent delivery". INTRODUCTION.md: the protocol sequence number "is reset each time the Producer's ID (PID) or Epoch changes". Neither page states that a new process gets a fresh producer ID; that part rests on KIP-98 (not re-read here) and K-53 in `docs/research/kubernetes-kafka-clients.md`. The new ADR text adds no Kafka claim, so nothing ships on it.
- Microsoft Learn's conflict on Event Hubs transactions: not re-read; already recorded in section 12 (B05 row) and not used by the new text.
- Rule list "1, 2, 4, 6, 7, 8, 10, 11 and 15": verified and extended. Rules 3, 5, 9, 12, 13 and 14 also change, because they name the supervisor, seals, selection or the pipe hand-off.
- Exposure under local latency (planning-session report: about 7 ms acknowledgements, nothing unconfirmed until 30 ms of injected delay): not re-run; no Docker daemon in this workspace. Where that kill point sat relative to `Produce` return is not stated in the report, so it does not settle DA2.
- `emptyDir`: Kubernetes volumes page read by script: "When a Pod is removed from a node for any reason, the data in the emptyDir is deleted permanently." "The data in an emptyDir volume is safe across container crashes." Medium "Memory" mounts tmpfs whose files "count against the memory limit of the container that wrote them". The loadgen Job already mounts `/work` as `emptyDir: {sizeLimit: 8Gi}` (`charts/parchwell/templates/jobs.yaml` lines 219 to 230 on `0f769fb2`), so no new volume is needed for the recommended answer.
- Documentation checks: with appendix A applied (slots filled with dummy text) to a temporary working-tree copy, `npm run check:docs` reported `no issues found` for `docs/blueprint.md`, `docs/operations/b10-abrupt-contract.md` and `docs/operations/b10-workflow.md`; the copy was restored. The appendix B script, extracted from this file and run against `origin/main`, printed `FAIL slot` with the slots unfilled, and `bad 0` and `ordinary ok` with them filled.
- Storage readiness: see DA4. The parent's journal upload and the successor's read are later blob operations in the same account, after the 2-minute wait.

## 12. Test map

This pull request changes no code. Its checks are text checks:

| Requirement | Check | Input | Expected |
|---|---|---|---|
| R1, R2 | Acceptance command 2 | Appendix A edit list against `main` | Each old string count 1 before; each new string count 1 after; block P lines equal |
| R3 | Acceptance command 3 | Changed files | `grep -c '\[Q' ` returns 0 for each file |
| R4 | Acceptance command 5 | Changed files | No byte above 0x7F |
| R5 | Acceptance command 2 | Contract and workflow files | `git diff` shows only OC-1 and OW-1 insertions |
| Invariant: ordinary sentences verbatim | Acceptance command 4 | Every changed line of `docs/blueprint.md` outside ADR-021 | Each old line contains `B10`, `ADR-021` or `supervisor`; prints `ordinary ok` |

## 13. Acceptance commands

1. `npm run check:docs` before the cold review; each warning in a changed file fixed or explained under docs/agents/pull-requests.md rule 8.
2. The edit script of appendix B, run from the repository root against `git show origin/main:<file>`, applying appendix A and printing `bad 0`, then `diff` of its output against the working tree printing nothing.
3. `grep -n '\[Q[0-9]' docs/blueprint.md docs/operations/b10-abrupt-contract.md docs/operations/b10-workflow.md` printing nothing.
4. The check in appendix B, part 2: every removed line of `docs/blueprint.md` outside ADR-021 (lines 659 to 728 on `0f769fb2`) contains `B10`, `ADR-021` or `supervisor`; it prints `ordinary ok`.
5. `LC_ALL=C grep -n -P '[^\x00-\x7F]' <changed files>` printing nothing.

## 14. Open questions for Hari

None.

### 14.1 Answers recorded

| Question | Answer | Decision |
|---|---|---|
| Q1 | `CutOrdinal` 4, the fifth original; no seed | [6769269811](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769269811) |
| Q2 | `ProbeRate` 100 originals per second | [6769312013](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769312013) |
| Q3 | option A, the same-instance control arm | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q4 | option A, `FileStream.Flush()` after each record | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q5 | the existing `/work` `emptyDir` on the node's default medium | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q6 | option A, timeout Job, first restart Job, successor Job | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q7 | option A, retire the removed failure names; the execution plan pins any new names | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q8a | `120 s` | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q8b | the recommended text | [6769416981](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769416981) |
| Q8c | the existing `ProducerDrainTimeout` | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q9 | option A | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q10 | `3` | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q11 | option A | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q12 | DA1, DA5 and the acceptance-to-kill time by the offline test before the trial; DA2 to DA4 accepted by name for the one-repeat trial only | [6769742939](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769742939) |
| Q13 | Claude Code for this design pull request; the other pull requests' tools not decided | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q14 | option A, status paragraphs now, full contract rewrite with the execution pull request | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q15 | option A | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q16 | option A | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q17 | option B, a separate pull request after the one-repeat trial | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q18 | `[Q18a]` option A, `[Q18c]` `3`, and the fixed successor condition | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q19 | option A | [6769057785](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769057785) |
| Q20 | option B | [6769760685](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769760685) |
| Q21 | option A(ii) | [6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960) |
| Plan check B2 | option B, leave README.md line 90 and factory.md line 158 unchanged | [6769843248](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769843248), replacing [6769808426](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6769808426) |

Every value set by these decisions is a policy choice, not a measurement.

## 15. Plan-check questions (factory.md "Item lifecycle" step 1)

(a) Runtime-sized steps: none in this pull request. The edit count (67: 64 sentence edits and block P in `docs/blueprint.md`, and one edit in each operations document) and the scratch measurement are fixed. Disposition caps: up to 20 documentation-warning root causes; exhaustion: `push -wip and hand off to the next session`.

(b) Stricter than the parent plan: the stop rule also covers changes by the storage readiness and PR3b plans to shared files (section 2.3), as the planning prompt requires.

(c) Dependencies outside `main` and this pull request: none for the text checks. The design relies on DA1 to DA5, listed in section 10 and Q12.

## 16. Sources

Read 2026-11-10:
- librdkafka v2.16.0 [CONFIGURATION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/CONFIGURATION.md) (`transactional.id`, `enable.idempotence`) and [INTRODUCTION.md](https://github.com/confluentinc/librdkafka/blob/v2.16.0/INTRODUCTION.md) ("Idempotent Producer", "Old producer fencing").
- POSIX.1-2024 [write](https://pubs.opengroup.org/onlinepubs/9799919799/functions/write.html) (Rationale), [kill](https://pubs.opengroup.org/onlinepubs/9799919799/functions/kill.html), [sigaction](https://pubs.opengroup.org/onlinepubs/9799919799/functions/sigaction.html).
- Linux [signal(7)](https://man7.org/linux/man-pages/man7/signal.7.html): SIGKILL and SIGSTOP "cannot be caught, blocked, or ignored".
- Kubernetes [volumes, emptyDir](https://kubernetes.io/docs/concepts/storage/volumes/#emptydir).
- .NET [FileStream.Flush](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0).
- Azure Storage [Manage concurrency in Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage) (strong consistency sentence).
- Already cited by ADR-021 on `main` and carried: Process.Start, WaitForExit, DllImport, ExitCode, librdkafka STATISTICS.md, cgroup-v2, Kubernetes pod lifecycle.

## 17. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when:
1. it needs to change a contract, a requirement or the scope, or any text in appendix A other than filling a slot with Hari's recorded answer;
2. any old string of appendix A does not occur exactly once on its `main`;
3. the PR3b classification work or any other change has altered `docs/blueprint.md`, `docs/operations/b10-abrupt-contract.md` or `docs/operations/b10-workflow.md` on `main` since `0f769fb2`, even where every old string still matches;
4. Hari records a decision that changes an answer in section 14.1.

Notes from the plan check, for later plans (no text change here): the execution and verifier plans pin, with a test, that a no-kill end's invalidity applies per instance, so the C1 timeout instance keeps its own checks and comparison (P2-4); and stop rule 3 governs over R1's "later `main`" clause (P2-5).

## Appendix A. Exact replacement text

Each entry gives the file, the line on `0f769fb2` where the old text starts, the old text (which occurs exactly once in that file) and the new text. Apply them in the order listed; each old string still occurs exactly once when its turn comes, and none overlaps another. Fenced blocks carry the info string `old <id>` or `new <id>` so that the script in appendix B can read them from this file.

### L12: `docs/blueprint.md` line 12

```old L12
and [ordinary-rule preservation, loss scope and smoke/publication](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707814559).
```

```new L12
and [ordinary-rule preservation, loss scope and smoke/publication](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6707814559). The B10 restart simplification amends the B10 exceptions of ADR-012, ADR-013, ADR-019 and ADR-020 and replaces ADR-021's restart protocol under the [simplification decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530) and, for the no-kill end, the [no-kill decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960); Hari's merge of its pull request records the amendment that ADR-021 protocol rule 15 requires.
```

### A12-1: `docs/blueprint.md` line 382

```old A12-1
For B10's killed first producer, ADR-021 substitutes its durable kill-cohort journal, preserving genuine terminal outcomes and accepted-unconfirmed attempts without invented outcomes or persistence statuses.
```

```new A12-1
For B10's self-killed first restart producer, ADR-021 substitutes its durable probe journal, preserving genuine terminal outcomes and unconfirmed attempts without invented outcomes or persistence statuses.
```

### A12-2: `docs/blueprint.md` line 383

```old A12-2
For B10 probes only, ADR-021 substitutes durable pre-call registration and acceptance journaling; timeout/control cohorts and the successor retain independent inventory checks. For B10's intentionally killed first producer, ADR-021 substitutes the supervisor-sealed abrupt manifest and journal after confirmed death for the child's normal end-of-production manifest.
```

```new A12-2
For B10 probes only, ADR-021 substitutes durable pre-call and acceptance journaling; the timeout cohort and, when it started, the successor retain independent inventory checks. For B10's intentionally self-killed first restart producer, ADR-021 substitutes the abrupt manifest and journal that its parent process publishes after observed death for the child's normal end-of-production manifest.
```

### A12-3: `docs/blueprint.md` line 384

```old A12-3
including genuine terminal records; it rejects a dropped known outcome even without a consumed message.
```

```new A12-3
including genuine terminal records, and recomputes the unconfirmed sets from the journal; it rejects a dropped known outcome even without a consumed message.
```

### A12-4: `docs/blueprint.md` line 384

```old A12-4
the verifier independently downloads and reconciles ADR-021's required journal, manifest, selection and receipt,
```

```new A12-4
the verifier independently downloads and reconciles ADR-021's required journal, abrupt manifest and termination receipt,
```

### A12-5: `docs/blueprint.md` line 385

```old A12-5
For B10 probe callbacks, ADR-021 substitutes its bounded external-capture path without waiting on file or network I/O; the surviving supervisor records capture gaps and seals only after the defined channel boundary.
```

```new A12-5
For B10 probe callbacks, ADR-021 substitutes a bounded in-memory queue drained by the child's journal writer thread, without file or network I/O on the callback path; capture gaps are journaled and sticky.
```

### A12-6: `docs/blueprint.md` line 387

```old A12-6
ADR-021 permits one sequential restarted instance, in a second load generator Job, in a B10 repeat. For B10 under ADR-021's redesign, that sequential second instance is C1-only; C0 retains one ordinary instance.
```

```new A12-6
ADR-021 permits, in a B10 C1 repeat only, up to three sequential load generator Jobs with one instance each: the C1 timeout instance, the first restart instance and, when ADR-021 starts it, its successor. C0 retains one ordinary instance.
```

### A12-7: `docs/blueprint.md` line 389

```old A12-7
For B10's intentional first-producer termination, the surviving supervisor publishes the required abrupt journal, manifest and termination receipt from storage outside the child after confirmed death. Transfer failure invalidates that experiment; supervisor transfer success does not assert producer cleanup.
```

```new A12-7
For B10's intentional first-restart-producer self-termination, the parent process reads the child's journal from the pod's local volume after it observes the child's death and publishes the required journal, abrupt manifest and termination receipt. Transfer failure invalidates that experiment; parent transfer success does not assert producer cleanup.
```

### A12-8: `docs/blueprint.md` line 393

```old A12-8
or for B10 against the two instances ADR-021 permits, each bound to its own expected load generator Job.
```

```new A12-8
or for B10 C1 against the instances ADR-021 permits (three, or two when it starts no successor), each bound to its own expected load generator Job.
```

### A12-9: `docs/blueprint.md` line 410

```old A12-9
(two for B10, under ADR-021)
```

```new A12-9
(for B10 C1, three, or two when ADR-021 starts no successor)
```

### A12-10: `docs/blueprint.md` line 412

```old A12-10
For B10 under ADR-021's redesign, require two independently registered C1 instances and one ordinary C0 instance;
```

```new A12-10
For B10 under ADR-021's redesign, require three independently registered C1 instances, or two when ADR-021 starts no successor, and one ordinary C0 instance;
```

### A12-11: `docs/blueprint.md` line 415

```old A12-11
For B10, only ADR-021's accepted-unconfirmed kill originals may join a checked journal entry without a final callback row;
```

```new A12-11
For B10, only ADR-021's accepted journaled probe attempts without a terminal record may join a checked journal entry without a final callback row;
```

### A12-12: `docs/blueprint.md` line 403

```old A12-12
Accepted-unconfirmed originals are separately reported as unreported_seen or unreported_unseen through the phase sweep;
```

```new A12-12
Accepted journaled probe attempts without a terminal record are separately reported as unreported_seen or unreported_unseen through the phase sweep;
```

### A12-13: `docs/blueprint.md` line 422

```old A12-13
For B10, a record joined to an ADR-021 accepted-unconfirmed original is unreported_seen, not phantom;
```

```new A12-13
For B10, a record joined to an ADR-021 accepted journaled probe attempt without a terminal record is unreported_seen, not phantom;
```

### A12-14: `docs/blueprint.md` line 428

```old A12-14
For B10, ADR-021 now permits the checked first-producer termination and sequential fresh-pod resend protocol; no other scenario gains an exception.
```

```new A12-14
For B10, ADR-021 now permits a separate C1 timeout Job and the checked first-restart-producer self-termination and sequential fresh-pod resend protocol; no other scenario gains an exception.
```

### A13-1: `docs/blueprint.md` line 446

```old A13-1
For B10, only the first kill probe terminates without normal Flush or Dispose under ADR-021's checked intentional-termination protocol; timeout/control cohorts and the second producer retain normal drain requirements.
```

```new A13-1
For B10, only the cross-restart arm of the first restart producer terminates without normal Flush or Dispose under ADR-021's checked intentional self-termination protocol; the C1 timeout instance, the control arm and, when it started, the successor retain normal drain requirements.
```

### A13-2: `docs/blueprint.md` line 454

```old A13-2
For B10's kill probe, ADR-021 substitutes independent guard capture in the supervisor-sealed journal/abrupt manifest and closed pre-stop/termination checks; ordinary cohorts retain sealed guard evidence.
```

```new A13-2
For B10's cross-restart arm, ADR-021 substitutes independent guard capture in the child's journal and the parent-published abrupt manifest and closed pre-acceptance/termination checks; the C1 timeout instance and, when it started, the successor retain ordinary guard evidence.
```

### A13-3: `docs/blueprint.md` line 454

```old A13-3
an accepted-unconfirmed kill original has no callback status to invent.
```

```new A13-3
an accepted journaled probe attempt without a terminal record has no callback status to invent.
```

### A13-4: `docs/blueprint.md` line 455

```old A13-4
For B10's kill probe only, ADR-021 applies its pre-acceptance coverage boundary; ordinary timeout/control cohorts and the successor retain the stable-set, expected-broker and post-drain checks above.
```

```new A13-4
For B10's cross-restart arm only, ADR-021 applies its pre-acceptance coverage boundary; the C1 timeout instance, the control arm and, when it started, the successor retain the stable-set, expected-broker and post-drain checks above.
```

### A13-5: `docs/blueprint.md` line 455

```old A13-5
For B10's kill probe only, ADR-021 substitutes pre-acceptance statistics/guard checks and an unknown native tail.
```

```new A13-5
For B10's cross-restart arm only, ADR-021 substitutes pre-acceptance statistics/guard checks and an unknown native tail.
```

### A13-6: `docs/blueprint.md` line 461

```old A13-6
For B10, the deliberately killed producer claims no normal completion; its surviving supervisor must seal and publish ADR-021's required artifacts after confirmed death.
```

```new A13-6
For B10, the deliberately self-killed producer claims no normal completion; its parent process must publish ADR-021's required artifacts after it observes the child's death.
```

### A13-7: `docs/blueprint.md` line 466

```old A13-7
- For B10's kill probe, the interval from the last statistics report to the kill and the killed native/callback tail remain unobserved.
```

```new A13-7
- For B10's cross-restart arm, the interval from the last statistics report to the kill and the killed native/callback tail remain unobserved.
```

### A19-1: `docs/blueprint.md` line 597

```old A19-1
abrupt attempt journal, selection and termination bookkeeping
```

```new A19-1
abrupt attempt journal, cut and termination bookkeeping
```

### A19-2: `docs/blueprint.md` line 601

```old A19-2
For B10's intentionally killed first producer only, ADR-021's authenticated abrupt manifest, journal, selection and termination receipt replace the ordinary final ledger and successful producer-completion receipt; a missing required substitute cannot authorize a complete repeat. The second producer still requires its ordinary completion receipt.
```

```new A19-2
For B10's intentionally self-killed first restart producer only, ADR-021's authenticated journal, abrupt manifest and termination receipt replace the ordinary final ledger and successful producer-completion receipt; a missing required substitute cannot authorize a complete repeat. The C1 timeout producer and, when it started, the successor still require their ordinary completion receipts.
```

### A20-1: `docs/blueprint.md` line 625

```old A20-1
B10 needs a surviving evidence publisher after an intentional producer-child kill, followed by a fresh producer pod.
```

```new A20-1
B10 needs a surviving evidence publisher after an intentional producer-child self-kill, followed by a fresh producer pod.
```

### A20-2: `docs/blueprint.md` line 633

```old A20-2
4. For B10 only, supervise this repository's fixed producer child while workflows retain Job sequencing and infrastructure ownership.
```

```new A20-2
4. For B10 only, start this repository's fixed producer child and publish its evidence after it exits, while workflows retain Job sequencing and infrastructure ownership.
```

### A20-3: `docs/blueprint.md` line 637

```old A20-3
The sole production process-launch exception is ADR-021's B10 supervisor. It may start only this repository's fixed built producer child, stop/resume or intentionally kill that owned child, and publish its evidence. It may not start any CLI tool or other process, invoke a shell, use the Kubernetes API, manage pods or nodes, or act as an in-cluster orchestrator.
```

```new A20-3
The sole production process-launch exception is ADR-021's B10 parent process. It may start only this repository's fixed built producer child, wait for that child's death and publish its evidence. It sends no signal to the child and holds no channel to it. The child may send SIGKILL only to itself, once, under ADR-021. Neither may start any CLI tool or other process, invoke a shell, use the Kubernetes API, manage pods or nodes, or act as an in-cluster orchestrator.
```

### A20-4: `docs/blueprint.md` line 643

```old A20-4
ADR-021 permits two sequential load generator Jobs, each with one registered instance, in a B10 repeat.
```

```new A20-4
ADR-021 permits up to three sequential load generator Jobs, each with one registered instance, in a B10 C1 repeat.
```

### A20-5: `docs/blueprint.md` line 643

```old A20-5
For B10, supervisor and child share the first-pod producer identity; authenticated registration precedes child producer construction. The supervisor is not a registered Kafka producer. For B10 under ADR-021's redesign, require exactly two sequential registered C1 instances and one ordinary C0 instance.
```

```new A20-5
For B10, parent and child share the first restart pod's producer identity; authenticated registration precedes child producer construction. The parent is not a registered Kafka producer. For B10 under ADR-021's redesign, require exactly three sequential registered C1 instances, or two when ADR-021 starts no successor, and one ordinary C0 instance.
```

### A20-6: `docs/blueprint.md` line 645

```old A20-6
For B10's killed producer only, ADR-021 substitutes its termination receipt and abrupt bookkeeping; the supervisor completes transfers before its own receipt without asserting child cleanup.
```

```new A20-6
For B10's self-killed producer only, ADR-021 substitutes its termination receipt and abrupt bookkeeping; the parent completes transfers before its own receipt without asserting child cleanup.
```

### A20-7: `docs/blueprint.md` line 656

```old A20-7
For B10, ADR-021 now permits the checked first-producer termination protocol followed by that second sequential Job and instance.
```

```new A20-7
For B10, ADR-021 now permits a separate C1 timeout Job and instance, and the checked first-restart-producer self-termination protocol followed, when ADR-021 starts it, by the successor Job and instance.
```

### A20-8: `docs/blueprint.md` line 657

```old A20-8
A successful supervisor receipt never asserts successful child cleanup.
```

```new A20-8
A successful parent receipt never asserts successful child cleanup.
```

### A20-9: `docs/blueprint.md` line 646

```old A20-9
For B10 this applies to each separately checked cohort; accepted-unconfirmed observations never become known-terminal counts.
```

```new A20-9
For B10 this applies to each separately checked cohort; observations of accepted journaled probe attempts without a terminal record never become known-terminal counts.
```

### A21-1: `docs/blueprint.md` line 674

```old A21-1
The selected redesign kills the producer child under its existing first-pod identity, then starts the successor in a fresh second pod.
```

```new A21-1
The selected redesign has the producer child kill itself under its existing first-pod identity, then, when protocol rule 10 allows, starts the successor in a fresh pod.
```

### A21-2: `docs/blueprint.md` line 677

```old A21-2
9. Redesign option 6 for C1 with checked unconfirmed exposure, a matched same-instance resend and a separate scoped verdict; keep C0 on its ordinary timeout path.
```

```new A21-2
9. Redesign option 6 for C1 with checked unconfirmed exposure, a matched same-instance resend and a separate scoped verdict; keep C0 on its ordinary timeout path.
10. As option 9, with the C1 timeout cohort in its own ordinary producer Job, a producer child that journals its own probes on local disk and kills itself at a point fixed before the run, and a parent that only starts it, waits for its death and publishes its journal, with no stop, checkpoint, pipe framing or pre-kill cohort seal.
```

### A21-3: `docs/blueprint.md` line 679

```old A21-3
For C1, option 9 replaces option 6, through the checked termination protocol and separate restart verdict below, under the [restart redesign decisions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800).
```

```new A21-3
For C1, option 10 replaces options 6 and 9, through the checked self-termination protocol and separate restart verdict below, under the [restart redesign decisions](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6705801800) and the [simplification decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530).
```

### A21-4: `docs/blueprint.md` line 681

```old A21-4
The only new exceptions are accepted-unconfirmed kill originals without terminal reports, replacement of the kill probe's normal post-drain/cleanup requirements by the closed stop/termination protocol,
```

```new A21-4
The only new exceptions are accepted journaled probe attempts without terminal records (protocol rule 3), replacement of the cross-restart arm's normal post-drain/cleanup requirements by the closed self-termination protocol,
```

### A21-5: `docs/blueprint.md` line 682

```old A21-5
Ordinary permitted timeout retries occur inside the first instance. The latest earlier timeout-cohort attempt with the same key and sequence number, in that instance's invocation order, must have a permitted timeout row. The second instance resends only the sealed kill selection and never ordinary timed-out attempts. The matched same-instance probe and cross-restart probe each use their own authenticated accepted-unconfirmed selection and exactly one application resend.
```

```new A21-5
Ordinary permitted timeout retries occur inside the timeout instance: C0's one instance, or the C1 timeout Job's instance. The latest earlier timeout-cohort attempt with the same key and sequence number, in that instance's invocation order, must have a permitted timeout row. The successor resends only the cross-restart arm's journaled unconfirmed set and never ordinary timed-out attempts. The same-instance control resends every member of its unconfirmed set exactly once; the successor, when protocol rule 10 starts it, resends every member of the kill unconfirmed set exactly once.
```

### A21-6: `docs/blueprint.md` line 683

```old A21-6
- Restart. A C1 repeat runs two load generator Jobs in sequence, between its start line and its verifier. C0 runs one ordinary producer Job with same-instance timeout retries, no probes, no kill and no successor Job. Each Job keeps ADR-020's shape, one completion, one pod and no retries, and its instance ID stays its pod name. For C1, the first producer finishes and seals its ordinary timeout cohort and matched same-instance control before the distinct kill probe. The supervisor stops and kills only its owned producer child under the protocol below, retains captured outcomes, publishes abrupt evidence after confirmed death and exits. The workflow verifies first-pod termination before starting the second Job. Only then does the workflow start the second Job, whose pod registers a second instance. Its registration marker names the first instance as its predecessor. Each instance is bound to its own Job's expected name. For C1 abrupt restart, reject other than two registered instances, duplicate instance IDs, a second instance that does not name the first, or missing first-instance bookkeeping. Require the authenticated abrupt manifest, journal, genuine terminal records, sealed kill selection and termination receipt. The second instance retains its ordinary manifest, ledger and completion receipt. C0 requires exactly one registered instance with ordinary manifest, ledger and completion receipt. Archived clean-restart evidence retains its legacy dialect and cannot authorize the new restart claim.
```

```new A21-6
- Restart. A C1 repeat runs up to three load generator Jobs in sequence, between its start line and its verifier: the C1 timeout Job, the first restart Job and the successor Job, in the order of protocol rule 1. C0 runs one ordinary producer Job with same-instance timeout retries, no probes, no kill and no successor Job. Each Job keeps ADR-020's shape, one completion, one pod and no retries, and its instance ID stays its pod name. The C1 timeout Job is an ordinary producer Job with no probes and no kill. In the first restart pod, a parent process starts this repository's fixed producer child, which runs the matched same-instance control and then the distinct cross-restart arm and kills itself under the protocol below; the parent observes its death, publishes the child's journal and abrupt evidence and exits. The workflow verifies first-pod termination and publication before starting the successor Job, which it starts only as protocol rule 10 allows, and whose pod registers a further instance. Its registration marker names the first restart instance as its predecessor. Each instance is bound to its own Job's expected name. For C1, reject other than three registered instances when the successor Job started, or two when protocol rule 10 did not start it, duplicate instance IDs, a successor that does not name the first restart instance, or missing first-restart bookkeeping. Require the authenticated journal, abrupt manifest and termination receipt. The C1 timeout instance and, when it started, the successor retain their ordinary manifest, ledger and completion receipt. C0 requires exactly one registered instance with ordinary manifest, ledger and completion receipt. Archived clean-restart evidence and evidence of the superseded stop protocol retain their dialects and cannot authorize the new restart claim.
```

### A21-7: `docs/blueprint.md` line 684

```old A21-7
Only the C1 kill probe substitutes abrupt bookkeeping and pre-acceptance statistics coverage; ordinary timeout/control cohorts and the second instance retain their normal completion and coverage requirements.
```

```new A21-7
Only the first restart instance substitutes abrupt bookkeeping and pre-acceptance statistics coverage, with the control arm's completeness under protocol rule 5; the C1 timeout instance and, when it started, the successor retain their normal completion and coverage requirements.
```

### A21-8: `docs/blueprint.md` line 692

```old A21-8
Ordinary permitted timeouts retry inside the first instance. All five C0 repeats
```

```new A21-8
Ordinary permitted timeouts retry inside the timeout instance. All five C0 repeats
```

### A21-9: `docs/blueprint.md` line 693

```old A21-9
Each requires valid configuration, authenticated independent first/second identities, verified intended termination, nonempty sealed accepted-unconfirmed kill selection, exactly its fresh-instance resends,
```

```new A21-9
Each requires valid configuration, authenticated independent timeout, first-restart and successor identities, verified intended self-termination, a nonempty kill unconfirmed set in the authenticated journal, exactly one fresh-instance resend of each of its members,
```

### A21-10: `docs/blueprint.md` line 694

```old A21-10
at least one eligible repeat must have a selected original without a genuine terminal report consumed (`unreported_seen >= 1`)
```

```new A21-10
at least one eligible repeat must have an accepted cross-restart original without a terminal record consumed (`unreported_seen >= 1`)
```

### A21-11: `docs/blueprint.md` line 696

```old A21-11
Read each arm's accepted-unconfirmed original, invoked resend, observed original/resend and pair counts.
```

```new A21-11
Read each arm's unconfirmed-set, invoked-resend, observed original/resend and pair counts.
```

### A21-12: `docs/blueprint.md` line 697

```old A21-12
Timeout/control require pre-kill seals with ordinary drain and post-drain statistics, authenticated by the supervisor's later receipt. No successful first-child cleanup receipt is fabricated; the successor requires ordinary completion.
```

```new A21-12
The C1 timeout instance requires its ordinary drain, post-drain statistics and completion receipt; the control arm requires the completeness of protocol rule 5 in the authenticated journal. No successful first-child cleanup receipt is fabricated; when it started, the successor requires ordinary completion.
```

### A21-13: `docs/blueprint.md` line 698

```old A21-13
and discloses process kill followed by pod replacement, unknown native tail and fixed arm order.
```

```new A21-13
and discloses process self-kill followed by pod replacement, unknown native tail, the fixed cut position and probe rate, and fixed arm order.
```

### A21-14: `docs/blueprint.md` line 723

```old A21-14
- The C1 restart test kills the first producer with sends unconfirmed at its authenticated application capture cutoff, then replaces it in a fresh pod and resends only the sealed eligible kill selection.
```

```new A21-14
- The C1 restart test has the first restart producer kill itself with sends unconfirmed in its authenticated journal at death, then, when protocol rule 10 allows, replaces it in a fresh pod and resends every member of that unconfirmed set.
```

### A21-15: `docs/blueprint.md` line 725

```old A21-15
Only this deliberate producer-child termination and sequential fresh-pod replacement shape is permitted.
```

```new A21-15
Only this deliberate producer-child self-termination and sequential fresh-pod replacement shape is permitted.
```

### A21-16: `docs/blueprint.md` line 725

```old A21-16
- The second Job, its name and the predecessor field in its registration marker belong to B10's producer-restart pull request,
```

```new A21-16
- The successor Job, its name and the predecessor field in its registration marker belong to B10's producer-restart pull request,
```

### A12-15: `docs/blueprint.md` line 409

```old A12-15
For B10's intentionally killed first producer only, ADR-021 substitutes its required abrupt bookkeeping artifacts;
```

```new A12-15
For B10's intentionally self-killed first restart producer only, ADR-021 substitutes its required abrupt bookkeeping artifacts;
```

### A12-16: `docs/blueprint.md` line 410

```old A12-16
(either permitted instance for B10;
```

```new A12-16
(any permitted C1 instance for B10;
```

### A12-17: `docs/blueprint.md` line 418

```old A12-17
For B10's kill probe, ADR-021 substitutes independent abrupt reconciliation;
```

```new A12-17
For B10's intentionally self-killed first restart instance, ADR-021 substitutes independent abrupt reconciliation of both probe arms;
```

### A12-18: `docs/blueprint.md` line 425

```old A12-18
replace only the intentionally killed first producer's ordinary completion artifacts;
```

```new A12-18
replace only the intentionally self-killed first restart producer's ordinary completion artifacts;
```

### A13-8: `docs/blueprint.md` line 435

```old A13-8
The B10 kill probe cannot finish normal producer drain or capture the native tail through death.
```

```new A13-8
The B10 cross-restart arm cannot finish normal producer drain or capture the native tail through death.
```

### A13-9: `docs/blueprint.md` line 447

```old A13-9
For B10's killed first producer only, ADR-021 substitutes its checked abrupt manifest and termination receipt;
```

```new A13-9
For B10's self-killed first restart producer only, ADR-021 substitutes its checked abrupt manifest and termination receipt;
```

### A19-3: `docs/blueprint.md` line 601

```old A19-3
abrupt bookkeeping for B10's intentionally killed first producer; no ordinary success receipt is fabricated.
```

```new A19-3
abrupt bookkeeping for B10's intentionally self-killed first restart producer; no ordinary success receipt is fabricated.
```

### A20-10: `docs/blueprint.md` line 656

```old A20-10
ADR-021 later permits B10's second, sequential load generator Job and its instance.
```

```new A20-10
ADR-021 later permits B10's sequential successor load generator Job and its instance.
```

### A21-17: `docs/blueprint.md` line 681

```old A21-17
except only for the expressly enumerated kill-probe observation gaps.
```

```new A21-17
except only for the expressly enumerated probe-arm observation gaps.
```

### A21-18: `docs/blueprint.md` line 685

```old A21-18
Accepted kill originals without a report use separate unreported_seen and unreported_unseen observations.
```

```new A21-18
Accepted journaled probe attempts without a terminal record use separate unreported_seen and unreported_unseen observations, under protocol rule 3; unconfirmed-set membership is not an outcome class.
```

### A21-19: `docs/blueprint.md` line 725

```old A21-19
The start line and the verifier's options must then carry both Jobs' expected names.
```

```new A21-19
The start line and the verifier's options must then carry the expected names of all three C1 Jobs, including a successor Job that protocol rule 10 does not start.
```

### A12-19: `docs/blueprint.md` line 429

```old A12-19
An unconfirmed original observed by the sweep is not a phantom; one not observed is not a proven loss.
```

```new A12-19
An accepted journaled probe attempt without a terminal record observed by the sweep is not a phantom; one not observed is not a proven loss.
```

### A21-20: `docs/blueprint.md` line 696

```old A21-20
A no-report original is `unconfirmed`, never inferred `PossiblyPersisted`.
```

```new A21-20
An accepted journaled probe attempt without a terminal record has no persistence status and is never inferred `PossiblyPersisted`.
```

### A21-21: `docs/blueprint.md` line 694

```old A21-21
A lost resend in any C1 repeat, eligible or ineligible, is always a separate published finding.
```

```new A21-21
A lost resend in any C1 repeat, eligible or ineligible, is always a separate published finding, except after a no-kill end under protocol rule 7, whose restart evidence is invalid and produces no finding ([decision 6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960)).
```

### A21-22: `docs/blueprint.md` line 695

```old A21-22
Loss confined to ineligible repeats remains a published finding and does not itself hold or select the restart verdict.
```

```new A21-22
Loss confined to ineligible repeats remains a published finding and does not itself hold or select the restart verdict; a no-kill end produces no loss finding (protocol rule 7).
```

### OC-1: `docs/operations/b10-abrupt-contract.md` line 3

```old OC-1
It is diagnostic-only and not active for scientific publication. ADR-021 in [the blueprint](../blueprint.md) owns the scientific rules; this page restates none of them.
```

```new OC-1
It is diagnostic-only and not active for scientific publication. ADR-021 in [the blueprint](../blueprint.md) owns the scientific rules; this page restates none of them.

Status: superseded protocol. This page describes the stop/checkpoint supervisor protocol that `main` still implements. ADR-021's 2026-11-10 amendment ([decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530)) replaces that protocol with a self-killing producer child and a parent that only starts it and publishes its journal. No B10 trial runs on this protocol. The implementation pull requests of the amendment replace this page's wire, state machine, policy and failure names; until then, this page stays true of the code.
```

### OW-1: `docs/operations/b10-workflow.md` line 59

```old OW-1
The trial cannot test fatal-capture completeness, the verifier's separation of an intended kill from a crash, broker persistence, pod or node loss, or a verdict.
```

```new OW-1
The trial cannot test fatal-capture completeness, the verifier's separation of an intended kill from a crash, broker persistence, pod or node loss, or a verdict.

This diagnostic trial runs the stop/checkpoint supervisor protocol that ADR-021's 2026-11-10 amendment ([decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530)) replaces. The implementation pull requests replace this section.
```

### P: `docs/blueprint.md` lines 702 to 718, ADR-021 "Restart protocol and policy" block

Old: lines 702 to 718 inclusive on `0f769fb2`, from `  Every value and gate below is policy fixed before any run:` to the end of protocol rule 15. SHA-256 of those 17 lines, each with its newline: `d0b6fca6471a62c1dbbb257f9ac3122608d23a4fd8d39311539b1b2664ab1df4`. Line 701 (blank) and line 719 (blank) stay. Replace the 17 lines with the 17 lines below; block P is applied after the sentence edits, which touch none of lines 702 to 718.

```new P
  Every value and gate below is policy fixed before any run: order, probe rate, cut positions, keys, journal limits and flush rule, release and lifecycle budgets, pre-acceptance samples, cgroup/OOM exclusions, capture/guard gates, witnesses and trial bound. These are not measured capabilities, latencies or rates. Feasibility is not verified until implementation tests and the diagnostic trial demonstrate it. Post-trial changes require the recorded decision specified below. In ADR-021, in the B10 exceptions of ADR-012, ADR-013, ADR-019 and ADR-020, and in the implementation pull requests, "authenticated" means checked SHA-256 and byte-length binding to the accepted publication manifest/receipt plus exact run, configuration, repeat, source/image, instance and Job identity agreement. It does not mean cryptographic signing, signer identity or protection against a writer able to replace both bytes and their binding; no signing guarantee is claimed.

  1. C0 has one ordinary producer Job with same-instance timeout retries, no probes, kill or successor. C1 runs up to three producer Jobs in sequence, in the order the C1 timeout Job, then the first restart Job, then the successor Job; rule 10 decides whether the successor Job starts. The C1 timeout Job is an ordinary producer Job like C0's: ordinary timeout cohort, same-instance timeout retries, no probes, no kill, ordinary manifest, ledger, drain, post-drain statistics and completion receipt. In the first restart pod the producer child runs the `same_instance` arm, then the `cross_restart` arm; only the cross-restart arm's unconfirmed set reaches the successor. In each arm the child prepares, invokes and journals probe originals at a fixed rate, `ProbeRate` = 100 originals per second, with ordinals from 0 to `CutOrdinal` = 4, and invokes no further original in that arm after the cut. Both arms use equal configured payload shape, settings, rate and cut ordinal. Fixed synthetic keys are `B10/{run_id}/{configuration}/{repeat}/same_instance` and `B10/{run_id}/{configuration}/{repeat}/cross_restart`, using authenticated identity strings and invariant decimal repeat; probe IDs equal those keys, sequence is the original's ordinal, and keys are disjoint from timeout keys. Every original and resend of an arm uses identical payload bytes/hash. Fixed ordering and different native/broker state remain confounds, not a universal causal contrast.
  2. In the first restart pod a parent process writes the registration marker, then uses .NET 10 `Process.Start` for only this repository's fixed built producer child, observes its death with `WaitForExit`, and then reads, hashes and publishes the child's journal and abrupt evidence. The parent sends no signal to the child, holds no pipe or other channel to it, reads no child output as protocol evidence, and is not a Kafka client or separately registered producer. Parent and child share the first restart pod's producer identity; bind run/scenario/configuration/repeat, source/image, Job, pod UID/name and child start identity before producer construction. The child sends SIGKILL only to itself, once, through libc `kill` via `DllImport` with its own process ID; POSIX `kill` sends the signal to the process whose ID equals `pid` ([kill](https://pubs.opengroup.org/onlinepubs/9799919799/functions/kill.html)). Forbid shells/CLI, Kubernetes API, PID discovery, signals to any other process, group/name signals, pod/node management and infrastructure control. Keep SDK, framework, projects and packages unchanged. Microsoft documents [Start](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start?view=net-10.0), [WaitForExit](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=net-10.0) and [DllImport](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.dllimportattribute?view=net-10.0), not this protocol's runtime interoperability. [ExitCode](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.exitcode?view=net-10.0) defines no SIGKILL encoding: record it as a labelled local observation on the pinned runtime; rule 7 states whether it gates intended termination.
  3. The child appends its own journal on the pod's local `emptyDir` work volume. The producing thread appends `prepared` before invocation (attempt/logical identity, arm, payload hash and ordinal), `accepted` only after `Produce` returns, `synchronous_rejection` when it throws, each arm's `cut` and the `kill_intent`. Delivery and error callbacks only enqueue to a bounded in-memory queue, with no file or network I/O; one journal writer thread appends their `terminal` and native-error records. After writing each record the child calls `FileStream.Flush()` before the next record or action. A record is durably journaled when it is complete in the journal file that the parent reads after the child's death; Kubernetes keeps `emptyDir` data across container crashes and deletes it permanently when the pod is removed from its node ([volumes](https://kubernetes.io/docs/concepts/storage/volumes/#emptydir)). Survival of completed writes across the child's SIGKILL is inferred from POSIX `write()` ([write](https://pubs.opengroup.org/onlinepubs/9799919799/functions/write.html), Rationale: a read proven to occur after a `write()` "must reflect that write()"), not stated by a cited source, and not verified on the deployed node. A torn or malformed record, including a torn final record, is a sticky capture gap and fails eligibility. Reconcile callback-before-acceptance by identity. Prepared-only ambiguity invalidates and does not prove invocation. Never suppress callbacks to obtain exposure. A `terminal` or `synchronous_rejection` record is a terminal record for its attempt; an attempt with a `synchronous_rejection` has no `accepted` record and belongs to no unconfirmed set. "Unconfirmed" names membership of an arm's unconfirmed set under rule 6 and is not an outcome class. Genuine terminal attempts retain the four classes, including control originals that were unconfirmed at the cut and completed later. Only accepted journaled probe attempts, originals and control resends, with no complete terminal record anywhere in the journal, in either arm, are `unreported_seen` or `unreported_unseen`, never phantom or loss; when the control arm is complete under rule 5, only cross-restart originals can be.
  4. Charge policy limits before admission: record 65536 bytes, callback queue 256 entries, journal 16777216 bytes and 4096 records. Gaps, overflow and writer errors are sticky. Exhaustion: `record the gap and continue` to publication of diagnostics and cleanup, with capture invalid and authoritative fields absent; no truncation into success. Raw statistics retain their existing separate bounds and authenticated hashes.
  5. Require a parsed statistics report after construction and before each arm's first original acceptance; the child journals a binding of the latest such report (name, length, SHA-256, sequence and time) before that acceptance. No post-acceptance report gate; `statistics.interval.ms` stays unchanged. Apply expected-broker, settings, mandatory-field, stable-identity and monotone-counter checks to available reports. Reject counter reset, nonzero `txerrs`/`rxerrs`, disallowed delivery errors, error-level native callbacks and missing status on genuine reports. The last-report-to-kill interval is unobserved. A sampled gauge cannot prove kill-instant wire state (inference from [statistics](https://github.com/confluentinc/librdkafka/blob/v2.16.0/STATISTICS.md)). The control arm is complete only when, after its resends, the child's producer `Flush` returns within the existing `ProducerDrainTimeout` with an empty out queue, the journal holds a terminal record for every control original and resend, and a parsed statistics report follows that drain; the child admits no cross-restart original before the control arm is complete. The C1 timeout instance and, when it started, the successor retain ordinary post-drain coverage.
  6. Each arm's cut is the `accepted` record of the original with ordinal `CutOrdinal`. In the control arm the child then appends a `cut` record; the control unconfirmed set is, in invocation order, the arm's accepted originals with no complete terminal record before that `cut` record in journal order. In the cross-restart arm the child appends `kill_intent` immediately after the cut and then kills itself (rule 7); the kill unconfirmed set is, in invocation order, the arm's accepted originals with no complete terminal record anywhere in the journal at death. An empty control set is `b10_abrupt_exposure_inconclusive`; an empty kill set is `kill_too_late`/`no_unconfirmed_attempt`; a cross-restart arm with no durably accepted original is `kill_too_early`. None is a pass or a protocol failure; each fails eligibility, records the gap and continues to publication of diagnostics and cleanup only, and never triggers another original, arm or kill. An empty control set, an incomplete control arm (rule 5) and any other failure before `kill_intent` end the child on the no-kill path of rule 7. This defines application-capture unconfirmed exposure, not transmission, broker acceptance, wire-inflight state, persistence status or loss.
  7. The control arm keeps running after its cut and retains subsequent outcomes and fatals. At the `cut` record's UTC plus `ReleaseDelay` = 120 s, the child resends each member of the control unconfirmed set once, in invocation order, with new attempt IDs and the same instance, even if a member has since completed. In the cross-restart arm the producing thread, immediately after the durable `kill_intent`, sends SIGKILL to itself once; it calls no `Flush` or `Dispose` after the arm's first original is admitted. A `kill` call that returns records its return value and errno and fails the chain. Intended termination requires a durable `kill_intent`, no producing-thread record after it, the parent's observed death within the remaining LoadGen process lifetime less the time its publication needs and the competing-evidence checks of rule 9. No-kill path: when the child ends without reaching `kill_intent`, it appends a durable `no_kill` record naming the outcome or failure. If the cross-restart arm's first original has not been admitted, the child then calls the producer's `Flush` within the existing `ProducerDrainTimeout`, disposes the producer and exits with exit code 3. If it has, the child exits with exit code 3 without calling `Flush` or `Dispose`, as this rule requires after that admission. A child that exits with a durable `no_kill` record and no `kill_intent` has ended on the no-kill path. That is a known termination, not an unexpected exit, but ADR-021 grants it no evidence substitution: the first restart instance's restart evidence is invalid and diagnostic only, the repeat is not eligible and its reason is the outcome the `no_kill` record names, and the C1 timeout instance keeps its own checks and its comparison is published under decision rule 1. It produces no classification and no finding, including no lost-resend finding under decision rules 3 and 4, and it does not count toward three-of-five eligibility ([decision 6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960)). A real lost resend in such a run is therefore not reported. An intended self-kill, including one with an empty kill set, is not a no-kill end. The no-kill path's exit code is a labelled local observation and does not gate. A crash after the durable `no_kill` record, during the pre-admission `Flush` or `Dispose` or before the post-admission exit, is not distinguishable from a no-kill end by journal evidence; that restart evidence is invalid and diagnostic only either way. A child that exits with neither record is an unexpected exit. [POSIX sigaction](https://pubs.opengroup.org/onlinepubs/9799919799/functions/sigaction.html) says SIGKILL cannot be caught or ignored, so no handler in the child can intercept its own kill. The journal holds no record of the signal request itself, so a runtime or native crash after the durable `kill_intent` and before the signal takes effect is not distinguishable from the intended kill by journal evidence; the parent therefore also requires the child's exit code, recorded under rule 2, to equal the value that the execution pull request's offline test records for a self-SIGKILL on the pinned runtime, and any other value fails intended termination. Exit code alone never proves intended termination.
  8. When rule 10 starts it, after the parent's publication and the workflow's first-pod termination check, the fresh successor reads the authenticated journal from Blob storage, recomputes the kill unconfirmed set under rule 6, and resends every member once, in invocation order, preserving logical identity/payload with new instance and attempt IDs, at the `kill_intent` record's UTC plus `ReleaseDelay`. Azure Storage documents a strong consistency model in which, after an insert or update, subsequent read or list operations return the latest update ([concurrency](https://learn.microsoft.com/en-us/azure/storage/blobs/concurrency-manage)). It never resends ordinary timeouts or control attempts. Missing readiness at that boundary makes comparison incomplete, with no late substitute, extra probe or replacement.
  9. Install native capture before use; retain actual code/name/IsError/IsFatal, ordered sequence/time, instance, start/cutoff and dropped/writer-error status. Captured fatal remains fatal after later kill. The parent requires stable memory-cgroup identity and readable `memory.events.local` `oom_kill` before it starts the child and after it observes death, without reset/increment. [Kernel documentation](https://docs.kernel.org/admin-guide/cgroup-v2.html#memory-interface-files) makes this group-level evidence, not victim identity. Runtime availability is not verified; absent support fails eligibility. Unexpected crash/prior exit (rule 7), OOM, node/pod loss or conflicting evidence fails intended termination. Even zero delta and the full chain cannot exclude indistinguishable external SIGKILL, or a crash between `kill_intent` and the signal (rule 7); disclose both limits.
  10. After observed child death the parent reads the journal from the local volume, applies rule 4's limits, hashes it, publishes create-only the journal, abrupt manifest and termination receipt, then exits; it asserts no child cleanup. Workflow verifies actual first-pod termination and publication before the fresh successor pod, and starts the successor only when the termination receipt records an intended self-kill with a nonempty kill unconfirmed set. Unknown termination preserves `safety_stop` and blocks successor; a no-kill end is a known termination and does not set `safety_stop`. When started, the successor retains ordinary registration/drain/ledger/manifest/completion. This does not simulate parent/pod/node loss; [Kubernetes lifecycle](https://kubernetes.io/docs/concepts/workloads/pods/pod-lifecycle/) does not make deletion proof of process death. Pod or node loss before publication deletes the `emptyDir` journal and leaves the repeat ineligible.
  11. Policy caps: one cut per arm, one kill per repeat, no exposure retry or replacement. Budget exhaustion, signal/death failure, capture gaps, missing publication or late successor: `record the gap and continue` only to diagnostics/sweep/judge/cleanup with affected evidence unavailable. Block successor if death or first-pod termination/publication is unverified. Failed global gates clear affected counts; workflow cleanup remains mandatory.
  12. The C1 timeout instance, the first restart instance and, when it started, the successor each pass independent checks; none repairs another's missing evidence. No probe enters timeout totals. Shared identity corruption, missing required coverage or global sweep failure still blocks affected evidence.
  13. Publish only checked fields consumed by these rules: timeout six counts (rule 1); termination identity, cuts, unconfirmed sets and coverage (2/6); per-arm unconfirmed-set size, accepted journaled probe attempts without a terminal record, invoked-resend, original/resend seen, pair and unreported-seen/unseen counts (2/3/5); actual fatal events/coverage (6); separate resend loss (3/4); comparison (5); declared/eligible count and reasons (2/4). Retain one checked pair witness per arm/repeat when present, chosen lexicographically by original attempt ID, resend attempt ID and original/resend coordinates. Count all pairs. Excess witnesses: `record the gap and continue`, without losing counts. Never turn unknown into zero or a persistence status.
  14. Closed `b10_abrupt` schema 3 distinguishes journal, manifest, termination receipt, nonfinal result and final sweep projection from legacy evidence, including schema-2 evidence of the superseded stop protocol. Bind hash/length, run/configuration/repeat, source/image and ordered identities; charge existing byte/depth/entry budgets before filtering/allocation. Enforce closed members/types/version, nonnegative checked Int64 counts, exact identities/arm/predecessor/invocations, independent bookkeeping, for a repeat whose successor started a nonempty kill unconfirmed set and exactly one linked resend per member, for an intended self-kill without a successor the outcome that rule 6 names and no successor evidence, and for a no-kill end no authoritative first-restart evidence (rule 7), pair subsets bounded by originals/resends seen, seen bounded by unconfirmed/invoked, per arm, unreported-seen plus unseen equal to accepted journaled probe attempts without a terminal record, deterministic witnesses/distinct coordinates, guard/fatal/termination gates and sweep finality. Missing/null/unknown required fields, fractional/overflow counts, conflicting terminals or failed bounds reject authoritative evidence. Old readers reject abrupt dialect; new readers retain schema-one behavior. No fabricated compatibility success, mixed source/image/dialect phase or promotion of legacy results to the new claim. Test actual produced bytes in both directions before publication.
  15. Amendment of 2026-11-10 ([decision 6762516530](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6762516530), with the no-kill end of [decision 6768228960](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6768228960)): nine diagnostic trials failed on harness machinery, none on the behaviour under test. Rules 1 to 14 above replace the stop/checkpoint/supervisor protocol, its pipe framing, freeze, checkpoint and drain budgets and pre-kill cohort seals, and the burst of the 2026-11-09 amendment ([decision 6739289644](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739289644), [decision 6739625843](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739625843), [decision 6739632202](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6739632202)), whose `ProbeBurstSize` is retired. Registration, authenticated hashes, the workflow's first-pod termination check, the late-write sweep, the decision rules (rules 3 and 4 narrowed only for the no-kill end of rule 7), three-of-five eligibility and fatal capture are kept. The restart subject is unchanged. No B10 trial runs until the storage readiness race fix merges and this protocol is implemented. The pull request that implements this protocol's execution is the inactive smoke-only pull request permitted by factory.md "Item lifecycle" step 1; one diagnostic trial follows its merge, policy workload one C1 repeat with its timeout Job, control arm and kill/replacement arm, read back as a one-repeat diagnostic observation ([decision 6749995101](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6749995101)), never a published result. Journal success requires authenticated identities, durable acceptance records, the control arm's completeness, the self-kill with a nonempty kill unconfirmed set or its named exposure outcome, observed death, publication, first-pod termination and, when rule 10 starts the successor, successor release within bounds, with required capture/guards. A no-kill end, including one at an empty control set before the control drain, is not journal success; the trial records its named outcome. A trial that ends on the no-kill path or with an empty kill set does not count toward the one-trial cap below, and at most one further trial follows only under Hari's recorded decision; a trial with unknown termination counts. No C0 or replacement trial. Policy cap: one trial. On failure or exhaustion, record the gap and continue to diagnostics and cleanup only, with no scientific activation and no automatic second trial; a second trial spends money, so the session hands off to Hari. It cannot test future verifier kill/crash separation; production-byte synthetic tests and science cover that. Afterwards only implementation bugs and the mechanical `ReleaseDelay`, parent exit-wait and journal budgets may change, each by Hari's recorded decision before science and disclosed in the pack. Eligibility threshold, pre-acceptance statistics gate, control gate, verdict rules, restart subject and expectations may not change.
```

## Appendix B. Edit script

Part 1 applies appendix A. Run from the repository root with Python 3, `python3 -I apply.py <plan file> <output dir>`. It reads each file from `origin/main`, checks every count, the block P hash and that no `[Q` slot remains, and only then writes the edited files under `<output dir>`. On any failure it prints each `FAIL`, writes nothing and exits 1. The implementer runs it on a copy of this file in which the slots are filled with Hari's recorded answers.

```python
import hashlib, os, re, subprocess, sys
plan, outdir = sys.argv[1], sys.argv[2]
text = open(plan, encoding='ascii').read()
blocks = dict(((k, i), v) for k, i, v in re.findall(r'```(old|new) (\S+)\n(.*?)\n```', text, re.S))
heads = re.findall(r'^### (\S+): `([^`]+)`', text, re.M)
files = {}
bad = 0
for ident, path in heads:
    ident = ident.rstrip(':')
    if ident == 'P':
        continue
    if path not in files:
        files[path] = subprocess.run(['git', 'show', 'origin/main:' + path], capture_output=True, text=True, check=True).stdout
    old, new = blocks[('old', ident)], blocks[('new', ident)]
    if files[path].count(old) != 1:
        print('FAIL', ident, files[path].count(old)); bad += 1; continue
    files[path] = files[path].replace(old, new)
bp = files['docs/blueprint.md'].split('\n')
start = next(i for i, l in enumerate(bp) if l.startswith('  Every value and gate below is policy fixed before any run:'))
old_block = '\n'.join(bp[start:start + 17]) + '\n'
if hashlib.sha256(old_block.encode()).hexdigest() != 'd0b6fca6471a62c1dbbb257f9ac3122608d23a4fd8d39311539b1b2664ab1df4':
    print('FAIL P hash'); bad += 1
bp[start:start + 17] = blocks[('new', 'P')].split('\n')
files['docs/blueprint.md'] = '\n'.join(bp)
for path, body in files.items():
    if '[Q' in body:
        print('FAIL slot', path); bad += 1
if bad:
    print('bad', bad)
    sys.exit(1)
for path, body in files.items():
    os.makedirs(os.path.dirname(os.path.join(outdir, path)), exist_ok=True)
    open(os.path.join(outdir, path), 'w', encoding='ascii').write(body)
print('bad 0')
```

Then `diff -r <output dir>/docs docs` for the three files prints nothing once the working tree holds the change.

Part 2, acceptance command 4:

```python
import difflib, subprocess
old = subprocess.run(['git', 'show', 'origin/main:docs/blueprint.md'], capture_output=True, text=True, check=True).stdout.split('\n')
new = open('docs/blueprint.md').read().split('\n')
bad = 0
for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(None, old, new, autojunk=False).get_opcodes():
    if tag in ('replace', 'delete'):
        for n in range(i1, i2):
            if 658 <= n <= 727:
                continue
            if not any(t in old[n] for t in ('B10', 'ADR-021', 'supervisor')):
                print('ordinary line changed', n + 1); bad += 1
print('ordinary ok' if bad == 0 else 'ordinary FAIL')
```

Part 2's line range 659 to 728 is ADR-021 on `0f769fb2`; if `main` has moved, the stop rule applies before this check.
