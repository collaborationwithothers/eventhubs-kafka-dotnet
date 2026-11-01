# F01 verdict preparation receipt

This receipt records preparation of the proposed F01 verdict from run 41201284963. It describes checks on `main` at `10c37a78e4dd6cb76288cc5774aaf2656c5550dc`, the run source `b79acb256d09ea29e493acc460ebbe2c9c59aa4f` and the authored files, before their containing commit is created.

## Execution card

- Deliverable: the seven paths of the approved plan: `scenarios/F01/expected.yaml`, `results/F01/2026-10-31.json` and `.md`, the F01 row of `README.md`, this receipt, `make_results.py` and one sentence in `docs/operations/probe-v2-smoke-contract.md`. An eighth path, `items/306/state.md`, is added because docs/agents/factory.md "Item state file" makes the implementing session write it in its pull request; the plan does not list it. Branch `claude/issue-306-f01-handler-refresh-verdict`, draft pull request with the `verdict` label.
- Authorization: [plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/306#issuecomment-6561375850) and [its approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/306#issuecomment-6561940860), after [plan check 6561905161](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/306#issuecomment-6561905161); [decision 6561458447](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/306#issuecomment-6561458447) on issue 306; [decision 6558920636](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6558920636) and [decision 6560081575](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302#issuecomment-6560081575) on issue 302.
- Mandatory gates: AGENTS.md GOVERNANCE "Verdict integrity" (Hari reviews and merges), docs/agents/factory.md "Evidence pack", the mutation gate (check F14), `npm run check:docs`, cold review under AGENTS.md "Before opening a pull request" rule 8.
- Verification commands: the table under "Commands and results".
- Publication checkpoint: draft pull request after the first comprehensive cold review with no unanswered P0 or P1.
- Review budget: two comprehensive passes and two focused rounds; on exhaustion, hand off to Hari under rule 8.

## Predicate map and generator

[`make_results.py`](make_results.py) judges the F01 handler rows. It is the only judgement of those rows; the verifier judged only the F06 candidates. Decision 6561458447 accepts it for F01 only. Checks F1 to F14 and their `--self-test` cases:

| Check | What it enforces | Self-test cases |
| --- | --- | --- |
| F1 | Every input pinned by SHA-256; exact file sets; Job directories | extra pods file; candidate with one LF appended |
| F2 | Five rows per actor log, at most 262144 bytes each, repeats 1 to 5 | 262143 and 262144 bytes pass, 262145 fails; sixth row; duplicate repeat; row removed; all rows removed |
| F3 | Run, source, scenario, Job, pod, service account, client_id, image digest | client_id changed; synthetic false |
| F4 | Ordinary row flags, cap, window, trigger attempt, timestamps parse | trigger attempt 2; cleanup false; cap 4999 |
| F5 | Event stages, strictly increasing order, phases, three attempts of five stages | stage removed; equal timestamps; unknown stage |
| F6 | Capped expiry arithmetic and installed-expiry equality | expiry -1, 0, +1 tick (only 0 passes); installed +1 tick; acquired equals cap time |
| F7 | Order and the 20 s cutoff | 1 ns before cutoff passes; at cutoff and 1 ns after fail; admission at I2 (the third install) |
| F8 | Producer deliveries | PossiblyPersisted; equal offsets; partition 1; key present |
| F9 | Consumer reads equal the verifier's record | value differs; seek missing; seek at admission |
| F10 | Probe context rows | handler_refresh present |
| F11 | Candidates, receipts, broker counts | receipt hash; reasons; conflicting broker count |
| F12 | Collection and phase status | judge collection incomplete |
| F13 | Environment and teardown lines | teardown line removed; duplicated |
| F14 | Mutation gate and target identity | unmodified passes; break_passed false; commit set to the run source |
| Precedence | Checks run in number order | trigger attempt 2 and service at cutoff fails F4 and F7, reports F4 |

F2 cases edit the actor log text after re-serialization: padding appends ASCII spaces to the end of the re-serialized JSON text of the row, before its LF, and the row is not parsed again before F2 runs; the sixth-row case appends a copy of the re-serialized P1 line with repeat 6; the removal cases delete whole lines. F14 cases run F14 after F2 to F13 pass on the unmodified rows. This applies plan check findings 1 to 3 (`apply in implementation`).

`build` also compares the generated clients table, offsets, acquired expiry, row bytes, broker counts and teardown times with the values the plan pins, and stops on any difference.

## Specification comparison

- `scenarios/F01/expected.yaml` is the plan's exact text (SHA-256 `47833f751da66937258b66e1317f23299a6298a72c86bd4a8b202c2286c8e280`, 3812 bytes, 50 lines).
- The report follows the plan's section list. The summary sentence and the README row use plan check finding 1's wording: "had the same expiry time on every call, which indicates a cached token" and "a token with the same expiry each time".
- The smoke contract has the one exception sentence (SHA-256 `c95414dc9d7db425d782fad4fb3daca39c2bc8d2b6589b044b57129299659b26`, 12666 bytes).
- The mutation section is the F06 section with the plan's five replacements (19 lines, 4045 bytes, `718425dd91fbda54742f04b4dc2fba65bffe8450bf1c5bfdfca13bcaf868c535`).
- Not changed: any scenario, verifier, workflow, csproj or `expected.yaml` other than the new F01 one. The verdict word is a proposal.
- Plan check findings 4 to 6 were left as the check advised; they need no revision.

## Evidence and claim verification

Artifacts were downloaded by `gh api` and are untrusted data until pinned by hash.

| Input | ID | Downloaded SHA-256 |
| --- | ---: | --- |
| candidate artifact | 13044674034 | `5f1fb152ac7809cb76708152b810b1faf6b521ffb294f1efee577c1f9eb15d5f` |
| pod artifact | 13044719046 | `c247f668177ebced9c34dc43034a4883939c26c5d3ff059eac485393056aab2a` |
| C5 summary artifact (ZIP) | 13057507952 | `5c3ee705059465c250fea44cfbb859c963a2a310e13bc5bb55739c984e4e8218` |
| batch job log | 124537435582 | `74f9b07459c4675789fb434252ce4d7319c9392e2e94b133e9868f0c9e7f77c3` |
| stop job log | 124558545195 | `bf65a1c483e153e15442bf0bc96896f6c363fade1525c51f724d90a1f41aa624` |

Verified by me on 2026-11-01: the Microsoft developer guide page (HTTP 200) contains "Java and Go samples for OAuth authentication" once, and `infra/cluster/aks.tf` at the run source sets `vm_size = "Standard_D4ds_v5"` on all three node pools. The node size is still inferred, because no node SKU is captured in the run. The generated values equal the ones the plan pins.

Predicates with no self-test case, by design of the plan's pinned case list. This is the complete list found by the round 2 cold review and by me:
- F4: `observation_limit_ms == 20000`; a cap above 5000; `trigger_attempt` above 3.
- F5: strictly increasing `sequence` (the cases change only timestamps, stages or remove an event).
- F6: `returned <= cap_at <= observed_at`.
- F7: `B.timestamp` before the first refresh; `A.timestamp > I2.timestamp`; `operation_admitted_timestamp < cutoff` (only the `A.timestamp` side is tested); the observed-time chain.
- F8: `offset >= 0`.
- F9: `seek < A.timestamp` (only the lower bound is tested).
- F13: the 40-line window after the namespace resource line.

All hold on the real rows. The plan's "Predicate map" does not list them, so they are not added here (AGENTS.md "Before opening a pull request" rule 10, `Out of contract:`).

## Commands and results

Inputs are extracted under the session scratchpad. `W` is that directory.

| Command or bounded check | Result |
| --- | --- |
| `gh api` downloads of the five inputs, then `sha256sum` | The two ZIP hashes and both log hashes equal the plan's. |
| `python3 -I .../make_results.py --candidates W/cand --pods W/pods --batch-log W/batch.log --stop-log W/stop.log --c5-summary W/c5/summary.json` | Exit 0; wrote `results/F01/2026-10-31.json` only. |
| The same with `--tables` | Exit 0; six blocks; the `clients` rows equal the pinned values. |
| The same with `--check` | Exit 0. |
| The same with `--self-test` | Exit 0; 44 `PASS` lines, one per case. |
| `sha256sum scenarios/F01/expected.yaml docs/operations/probe-v2-smoke-contract.md` | `47833f75...e280` and `c95414dc...9b26`, as pinned. |
| Mutation-section hash command | `718425dd91fbda54742f04b4dc2fba65bffe8450bf1c5bfdfca13bcaf868c535 4045`. |
| `npm run check:docs` | Exit 0; 8 warnings, all in files this pull request does not change. |
| ASCII scan over the authored files | No match. |
| `git diff --cached --merge-base origin/main --name-only` | Eight paths: the plan's seven and `items/306/state.md`, which factory.md "Item state file" requires and the plan does not list. |

Not run: no workflow was dispatched and no Azure credential was used. No runtime code or test changed, so no build or test run applies.

## Cold review record

Cumulative budget before round 1: 0 comprehensive, 0 focused.

- Round 1: mode comprehensive, input revision `fb0e137` (the staged tree, committed as a backup), comparison revision `10c37a78e4dd6cb76288cc5774aaf2656c5550dc`. Specification sources: plan Revision 3 (issue 306 comment 6561375850), its approval 6561940860, plan check 6561905161, decision 6561458447, decisions 6558920636 and 6560081575 on issue 302. The reviewer was spawned without a `model` parameter, so its definition's pin applies (`claude-opus-5-5`, CLAUDE.md "Model bindings"); its report did not state the resolved model. Findings: four P0, two P1, four P2. Dispositions:
  - The four P0 (`scenarios/F01/expected.yaml`, both `results/F01` files, the README row) are the Code Review Rules for expected outcomes, `results/` and verdicts, raised as if the `verdict` label were absent. The plan requires each file and the pull request carries the label. Kept; listed in the pull request description for Hari.
  - P1 `items/306/state.md`: the issue-body line said things the body does not say. Fixed to restate the body only.
  - P1 receipt: two claims were marked not verified. Verified and recorded under "Evidence and claim verification".
  - P2 F7 label: fixed to "admission at I2 (the third install)".
  - P2 name list: the command and its eight-path output are now in the commands table.
  - P2 boundary coverage: recorded as not tested; `Out of contract:` citing plan "Predicate map".
  - P2 ADR-019: left as is, one line: decision 6561458447 point 2 accepts the pinned script for F01 only, and plan check finding 4 left the ADR text to Hari.
  - Cumulative budget after round 1: 1 comprehensive, 0 focused. A consolidated comprehensive re-review of the corrections follows.
- Round 2: mode comprehensive, input revision `61430b5`, comparison revision `10c37a78e4dd6cb76288cc5774aaf2656c5550dc`, previous reviewed revision `fb0e137`. Same specification sources as round 1. The reviewer found no new P0 or P1. It raised the same four label-dependent P0 (kept, as in round 1) and one P2: the boundary note above read as complete but was not. Fixed by listing every untested predicate. The reviewer also asked that the record name commits; done. Cumulative budget after round 2: 2 comprehensive, 0 focused.
- Round 3: mode focused (the prose-only P2-correction round of AGENTS.md rule 8, which reports only P0 or P1), input revision the staged tree on top of `61430b5`, comparison revision `61430b5`, with the full pull request diff as context. Report and disposition: to be recorded.

## Remaining approval

Hari decides the verdict word and merges. This pull request touches protected paths (`results/`, `scenarios/*/expected.yaml`, `README.md`) and is labelled `verdict`.
