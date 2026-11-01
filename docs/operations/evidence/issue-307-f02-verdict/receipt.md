# F02 verdict preparation receipt

This receipt records preparation of the proposed F02 verdict from authentication-controls smoke run 41331232103. It describes checks on `main` at `1f361d6` (after the merge of the F01 pull request), the run source `99131dee48677b971f0b2299d243779151ebe700` and the authored files, before their containing commit is created.

## Execution card

- Deliverable: the eight paths of the approved plan: `scenarios/F02/expected.yaml`, `results/F02/2026-11-01.json` and `.md`, the F02 row of `README.md`, this receipt, `make_results.py`, one sentence in `docs/operations/probe-v2-smoke-contract.md`, and `items/307/state.md`. Branch `claude/issue-307-f02-native-arms-verdict`, draft pull request with the `verdict` label.
- Authorization: [plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6562030111) (`updated_at` 2026-11-01T18:42:56Z) and [its approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6562348532), after [plan check 6562327602](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6562327602); [approval 6561311090](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6561311090), [decision 6562091612](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6562091612) and [decision 6562117618](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/307#issuecomment-6562117618) on issue 307; [decision 6558920636](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6558920636) and [split decision 6560081575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6560081575) on issue 302. The plan check's findings 1 to 3 are `apply in implementation` and are applied. Hari's instruction in the session ("306 has merged so refresh from main branch") moved the smoke-contract edit to plan case B.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity" (Hari reviews and merges), docs/agents/factory.md "Evidence pack", the mutation gate (check G12), `npm run check:docs`, cold review under AGENTS.md "Before opening a pull request" rule 8.
- Verification commands: the table under "Commands and results".
- Publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: two comprehensive passes and two focused rounds; on exhaustion, hand off to Hari under rule 8.

## Predicate map and generator

[`make_results.py`](make_results.py) judges the F02 native slots. It is the only judgement of those slots; the verifier judged only the F06 candidates. Decision 6562091612 accepts it for F02 only. Checks G1 to G12 and their `--self-test` cases (31 cases, one `PASS` line each):

| Check | What it enforces | Self-test cases |
| --- | --- | --- |
| G1 | Every input pinned by SHA-256; exact file sets; Job directories; git pins | extra pods file; `F06-C6.json` with one LF appended |
| G2 | Phase status, collection status, metadata collection | `nativeimds` outcome `failed`; `safety_stop` true; incomplete collection status |
| G3 | Capture identity, pod binding, repeats 1 to 5, client_id, image digest | sixth repeat; repeats 1,1,3,4,5; repeat 2 `-r3`; second capture line; no capture line |
| G4 | Literal native settings per stage | `nativejwt` without `https.ca.location`; `nativeimds` with an added client ID |
| G5 | Control; a failed control marks every native slot not decisive | seed repeat 3 `PossiblyPersisted` |
| G6 | Slot evidence, class, lower bound, answers, principals | token-endpoint line without its status; lower bound at `LOG_LIMIT - 1` and `LOG_LIMIT` |
| G7 | Per-slot decisions for the four hypotheses | status 399, 400, 500; class precedence 2.3, 2.2, 2.1; principals `[workload]`, `[empty]`, none; 2.4 marker and 2.3 precedence; `Multiple user assigned identities exist` at 400 and 399; H-jwt and H-ca rules 7 and 8 |
| G8 | Run agreement | one slot undecided; `not_attempted_cleanup`; all five agree |
| G9 | Candidates, receipts, broker counts | receipt hash digit changed; conflicting C9 broker count |
| G10 | Environment and teardown lines | duplicated `aks_stopped` line |
| G11 | Generated `slots`, `decisions` and `broker-counts` blocks equal the expected blocks | the failed-control case above |
| G12 | Mutation gate and target identity | run on the real inputs only (needs git objects) |
| Precedence | Checks run in number order | `client_id` and `https.ca.location` both broken report G3 |

Two deviations from the plan's wording of the self-test list, both of form only. The "padded to `LOG_LIMIT - 1` bytes" case calls the slot evaluation with the byte count, not a padded file, because the evaluation takes the Job log size as an argument and the two are the same input to it. Cases that stop at G11 run without G12, so `render_mutation` returns an empty block when G12 has not run.

The pinned tables are checked by the generator itself: `slots`, `decisions` and `broker-counts` against the expected blocks (G11), and all six printed blocks against the report in `--check`. The `jobs` and `control` tables follow plan check findings 2 and 3. The `jobs` table here includes the Job exit code only in the JSON, not in the table, as the check's row format has no such column.

## Specification comparison

- `scenarios/F02/expected.yaml` is the plan's exact text (SHA-256 `392ffbe2653d1539b069564a9cdd85aebb63d5bbc2a087f51e5a481f496d8796`, 8064 bytes, 67 lines).
- The summary, the verdict paragraph, the "Not tested" list and the README row were extracted from the plan comment by script and written unchanged. The "Not tested" source sentence applies plan check finding 1.
- The report adds one sentence the plan does not pin, in "Assessment against expected.yaml": decision 6562091612 was recorded after the run, and it supplied the rules that let the run decide H-jwt and H-ca. This is the point of the later independent read (comment 6562376463, finding 6); it is a disclosure, adds no number, and changes no decision. Hari can remove it. The same read's findings 1 to 5 are recorded for Hari and not acted on: they concern enforced checks or the pinned `expected.yaml` and need an amendment.
- The smoke contract has the case B sentence (SHA-256 `a90ab93058add4fd79d496254917ccd02c12a1c372e5d411c92cfcd2bc9788bf`, 12910 bytes), because the F01 pull request merged.
- The mutation section is the F06 section with the plan's four replacements (19 lines, 4045 bytes, `e904cd3ca5b6e2f8e85bf5a13d305633651581e3be336e19fb17d94be765da7b`), the last section of the report.
- The `slots`, `decisions` and `broker-counts` blocks the generator produces equal the plan's expected blocks. No finding for Hari arises from G11.
- Not changed: any scenario, verifier, workflow, csproj or `expected.yaml` other than the new F02 one. The verdict word is a proposal.

## Evidence and claim verification

Artifacts were downloaded by `gh api` and are untrusted data until pinned by hash.

| Input | ID | Downloaded SHA-256 |
| --- | ---: | --- |
| candidate artifact | 13094681168 | `ae68a24e7072c30c06682692f427f11cae0463bb8bc24b9c845d84cbcc88e72e` |
| pod artifact | 13095030533 | `4fc71fd159f2d5d05e630ac6885992db02c26e585fff4309627c4e382a1d326a` |
| C5 summary artifact (ZIP) | 13057507952 | not hashed; the `summary.json` member is pinned: `3f1ba194816bab19b31b900c9084839d2f8250359b115bfe2df0304f505c6bec` |
| batch job log | 124921593777 | `c7a9b48e57c8a5d5de8aa3681b5fdacad01cab9c3ca6b349ff2555b96aed051a` |
| stop job log | 124931950159 | `9d95d6a62d0d1cff3f5f0170608c06bb30ad2360d4a538bcfa8d871045031dcf` |

Verified by me on 2026-11-01: the Microsoft workload identity page for AKS (HTTP 200) contains "workload identity uses the Microsoft Entra v2 token endpoint rather than the IMDS resource flow used by managed identity" (the page shows `resource` in code formatting, which plain text drops). The batch log ends its phase list with `batch: failure`, then `upload_candidates`, `upload_pods`, `delete_workload` and `destroy` each `success`, so "every cleanup phase success" holds. The generated values equal the ones the plan pins. The node size `Standard_D4ds_v5` stays inferred from `infra/cluster/aks.tf`.

The blueprint quotes (line 1033, line 1104) were read at `1f361d6`.

## Commands and results

Inputs are extracted under the session scratchpad. `W` is that directory.

| Command or bounded check | Result |
| --- | --- |
| `gh api` downloads of the five inputs, then `sha256sum` | The two ZIP hashes and both log hashes equal the plan's. |
| `python3 -I .../make_results.py --candidates W/cand --pods W/pods --batch-log W/batch.log --stop-log W/stop.log --c5-summary W/c5/summary.json` | Exit 0; wrote `results/F02/2026-11-01.json` only. |
| The same with `--tables` | Exit 0; six blocks; `slots`, `decisions` and `broker-counts` equal the plan's blocks. |
| The same with `--check` | Exit 0 (`check passed`). |
| The same with `--self-test` | Exit 0; 31 `PASS` lines, one per case. |
| `sha256sum scenarios/F02/expected.yaml docs/operations/probe-v2-smoke-contract.md` | `392ffbe2...6d8796` and `a90ab930...88bf` (case B), as pinned. |
| `yaml.safe_load` of `scenarios/F02/expected.yaml` | Parses. |
| Mutation-section hash command | `e904cd3ca5b6e2f8e85bf5a13d305633651581e3be336e19fb17d94be765da7b 4045`. |
| `npm run check:docs` | Exit 0; 8 warnings, all in files this pull request does not change. |
| ASCII scan over the authored files | No match. |
| `git diff --cached --merge-base origin/main --name-only` | Exactly the eight paths. |

Not run: no workflow was dispatched and no Azure credential was used. No runtime code or test changed, so no build or test run applies.

## Cold review record

Cumulative budget before round 1: 0 comprehensive, 0 focused.

Rounds are recorded below as they complete.

- Round 1 (in progress): mode comprehensive, input revision the staged tree on top of `1f361d6`, comparison revision `1f361d6` (`origin/main`). Specification sources: plan Revision 3 (issue 307 comment 6562030111, `updated_at` 2026-11-01T18:42:56Z), its approval 6562348532, plan check 6562327602 (findings 1 to 3 apply), approval 6561311090, decisions 6562091612 and 6562117618, decisions 6558920636 and 6560081575 on issue 302. The full plan text is not part of the diff; the sources above are the issue comments.

## Remaining approval

Hari decides the verdict word and merges. This pull request touches protected paths (`results/`, `scenarios/*/expected.yaml`, `README.md`) and is labelled `verdict`.
