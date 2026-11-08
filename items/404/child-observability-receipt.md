# B10 abrupt child diagnostic observability receipt

This pull request implements the approved diagnostic observability change for issue 404. It records exception types without messages or stacks, gives a failed child a bounded chance to report before EOF cleanup, and retains diagnostic failure and cleanup gates. The original live child's cause remains unknown.

## Execution card

- Deliverable: the nine-file scope in [Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6724791606), pinned to `updated_at: 2026-11-08T17:38:13Z`, exact decoded REST body UTF-8 SHA-256 `9dff938258269b7bde545143ffb9d746da5e0e484fd832788d35fadde3c955bb`, no added newline, trimming or normalization. Both pins matched before implementation.
- Authorization: [decision 6724662091](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6724662091), [approval 6724978056](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6724978056), and [plan check 6724881731](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6724881731). Apply the check's exact timer/TCS helper and two apphost expectations. Approval changes the proposed grace to 10 seconds everywhere; polling remains 10 milliseconds.
- Source: `main` at `b4d6f9e8c80a28e84ccca1aa9ffe9bcbb0250394`; branch `codex/404-child-observability`. The user's explicit implementation instruction and current approval name Codex. Parents are 347 and 348.
- Mandatory gates: approved regression checks 1-8, foundation verification, docs warning dispositions, scope/C5/failure-name comparison, applicable cold coverage, final-head CI and hosted Codex review, exact-head cold Claude review, and Hari merge. No Azure, trial, workflow change/dispatch, scientific activation, expected-outcome, verdict or results change.
- Commands: use the exact commands in the plan's Exact verification and evidence section, from repository root, with SDK 10.0.402 on PATH. Initial `dotnet --version`, `./scripts/verify-dotnet.sh`, `npm ci --ignore-scripts`, `npm run check:docs`, `git diff --check`; corrections use the plan's Release build/publish, filtered MTP run and ValidateReport commands with a fresh absolute evidence directory outside the repository.
- Earliest publication: tested diff and authored receipt with applicable cold coverage, then a draft PR as the user requests. Available cold coverage can permit that draft while the required Claude report is pending; no merge without every required head gate.
- Budget: one initial command invocation and at most two consolidated correction-verification rounds. Per PR, 20 docs-warning, 20 verification-failure and 20 review-finding root causes; first excess or unpassed mandatory check at the verification cap means `push -wip and hand off to the next session`. Record counts and dated remaining estimate/basis. Unchanged-file docs warnings receive one blanket disposition. Other uncapped runtime work stops at its first measurement under AGENTS rule 11.
- Review budget: at most two comprehensive and two focused rounds cumulatively, including the special prose-only P2 round, under [pull request rules](../../docs/agents/pull-requests.md). Exhausted required coverage or unsafe focus means save `-wip` and hand off to Hari, with unresolved gates, revisions and counters. No new session resets it. A third comprehensive pass requires Hari's explicit decision. Keep the first-clean-round P2 correction/reversion procedure and rule 10 dispositions. Hosted reviews are recorded separately.

## Implementation and evidence

Implementation and verification are in progress. No passing result or review is claimed yet. Evidence is retained outside the repository at `/workspace/scratch/issue404-child-implementation/`; program output is not committed.

### Approval choices and deadline arithmetic

The implementation applies P2-1's timer/TCS helper exactly, P2-2's two apphost expected strings exactly, and the approval's 10-second grace. The 10 ms poll is unchanged. The contract owns this failure-only policy; implementation and tests cite it through the approved specification.

The grace adds up to 10 seconds of configured allowance before cleanup only on clean pre-planned-kill EOF while the child is alive. An already exited child adds no wait; natural exit or enclosing cancellation truncates it. Scripted arithmetic for the listed failure-path terms changes from `0 + 10 + 10 = 20 s` to `10 + 10 + 10 = 30 s`: grace, existing cleanup exit observation, then the diagnostic-write allowance. This is configured-budget arithmetic, not a measured runtime or a hard wall-clock bound: existing synchronous signal/journal/file operations and scheduling are not made time-bounded by this PR. The failure path can therefore spend additional time without any deadline extension.

The successful-stage sum remains `4 * 10 + 300 + (30 + 120 + 300) + (30 + 10 + 10 + 10) + 6 * 30 = 1030 s`, recomputed by script. Existing 900 s process lifetime/watchdog, 1140 s Job deadline, 18000 s phase bound, release, kill/reap/post-kill EOF gates remain unchanged. The enclosing lifetime token cuts short grace. Abort retains its existing cancellation-independent mandatory-cleanup attempt; the unchanged process watchdog can still end the process before teardown/diagnostics finish. No guarantee of child report delivery or live failure cause is inferred.

Policy basis is [approval 6724978056](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6724978056): the 10 s choice equals an existing cleanup observation allowance and costs no successful-path wait. The single L1 offline observer interval of about 3.07 s is context from a different environment, not a live latency or a measured upper bound. No grace change beyond this approval is authorized.

### Predicate-to-test map

This is diagnostic runtime work, not changed verifier judgement logic. The plan nevertheless requires the following coverage map; unrelated fixture identities and bindings remain valid in each rejection case.

| Predicate or interaction | Regression evidence |
| --- | --- |
| Explicit configuration versus unknown/untrusted label | OptionsTests retains role/image/path rejection, exact closed validation detail, untrusted detail fallback, unknown prefix and known-name suffix redaction; ProcessTests retains both exact apphost failures |
| Type metadata versus message/stack/data | OptionsTests throws/catches secret-bearing outer/inner exceptions and asserts exact safe lines; ProcessTests checks exact startup/signal JSON members, type metadata and null/real signal observations |
| Local mkdir/CreateNew I/O versus configuration | ProducerTests uses a file/directory collision before producer construction, a preexisting durable destination whose bytes remain unchanged, a valid write counterpart and direct UnauthorizedAccessException reporting |
| Empty event stream, already exited | SupervisorTests clean EOF with no prepared/accepted frames skips delay and KILL, remains incomplete and publishes no successful artifact |
| Grace below/at/above boundary | SupervisorTests observes natural exit at `10 s - 1 tick`; live child at that instant has no KILL, final tick exhausts grace, and a further tick/Abort cannot retry cleanup. Scripted natural exit between expiry observation and Abort is rechecked before signaling |
| Enclosing cancellation versus EOF grace | SupervisorTests cancels a registered grace timer, retains deadline-exhausted diagnostics and mandatory cleanup |
| Fault precedence and planned KILL | Partial-frame EOF and guard failure clean up without grace; planned KILL bypasses pre-kill grace, and existing post-kill ten-second boundary tests remain unchanged |
| Startup before process return | SupervisorTests covers start, preflight and startup-cleanup variants, each with/without create-only diagnostic collision; original exception survives, process/exit fields remain null and startup cleanup is not repeated |
| Returned-child cleanup | Existing signal/reap failure assertions retain original cause, actual return/errno/nulls, identity and one cleanup; disposal is asserted once |
| Test-only release clock | ProducerTests freezes UTC across a synthetic one-day inner advance, resumes correctly and observes supplied UTC; existing completed-control and successor minus-one/exact/plus-one-tick assertions remain |

Negative grace is unreachable because it is a fixed positive constant, not input. No separate successful outcome exists at any grace boundary: every clean pre-planned-kill EOF remains an incomplete failure. The final timer's sub-millisecond interval uses the approval's exact clock timer/TCS helper without rounding the boundary.

## Review record

Comprehensive passes used: 0 of 2. Focused rounds used: 0 of 2. Hosted reviews requested: 0. No cold Claude report is yet available.
