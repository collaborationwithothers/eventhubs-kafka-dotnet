# Idle-connection design receipt

This PR records the approved packet-observation design and its evidence boundaries. It implements no capture capability and supplies no experimental result.

## Execution card

- Deliverable: the three documentation paths in [design plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6614415345), updated 2026-11-03T20:23:49Z, "Exact changes".
- Authorization: [standing decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6596595315), the plan's four linked N1-N4 adoptions, and [Hari's subsequent approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6615675298). That approval resolves both reserved questions and adopts the [identifying independent check's](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/334#issuecomment-6614660435) exact citation corrections. No new plan revision is claimed.
- Source: main `788574480788e74b6b937e7d03c93d0d99e056e1`. Registration PR #338 is merged at `a5bbb24bd9156c8c15d246c85782edf36724f16c` and is an ancestor. Changes since the plan's pinned main concern the separate published-result correction notes, not the design inputs or governing rules.
- Mandatory gates: AGENTS.md GOVERNANCE "Merge rule", "Handoffs" and "Hard safety rules"; docs/agents/pull-requests.md rules 3, 5, 7, 8, 9 and 10; factory.md "Escalation". No workflow dispatch belongs to this PR.
- Exact verification: stage the three paths; `npm ci --ignore-scripts`; `npm run check:docs`; `git diff --check`; the plan's verbatim Python scope/ASCII/headings/arithmetic assertion; byte comparison against the plan's fenced design with only the approval-status replacements and exact check corrections; state/receipt references checked against the saved complete API snapshot.
- Earliest publication checkpoint: successful acceptance and applicable cold-review coverage, followed by authority refresh. CI and hosted review follow publication.
- Review budget and exhaustion: docs/agents/pull-requests.md rule 8 owns the cumulative budget and required Hari handoff. "At most two comprehensive passes run per PR across all sessions." After those passes, "allow at most two focused correction-review rounds"; its prose-only P2 exception also applies. "When another required review would exceed the budget, or when focus is unsafe, save unpublished work under rule 12 and hand off to Hari."
- Other runtime-sized dispositions: plan "Bounded work" caps actionable documentation-warning and non-cold-review findings at 20 combined; exhaustion action is `push -wip and hand off to the next session`. Unchanged-file warnings and cold-review work retain rule 8's exclusions.
- Predicate map: Not applicable: documentation-only design proposal; no verifier predicate changes.
- Models: Codex authoring session (model/effort not exposed); source-verification reader at high effort. Cold review remains separate.

## Source verification

Read on 2026-11-03 UTC. The bounded reader verified the eleven pinned external sources below; the implementing session checked the relied-upon claims against the quoted source locations. These establish mechanisms, not actual AKS permissions, filter correctness, image compatibility or measured clock precision.

| Source | Verified claim and limit |
|---|---|
| [K1](https://github.com/kubernetes/website/blob/8f6d9762bfd9e130d63f678c2e0f9b048bc3373e/content/en/docs/concepts/workloads/pods/_index.md) | Pod networking: shared network namespace; observer admission remains unproved. |
| [L1](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/net/packet/af_packet.c) | packet_create: CAP_NET_RAW and protocol-zero hook suppression; packet_rcv: network-layer framing and filter trim before enqueue; PACKET_STATISTICS: reset-on-read. |
| [R1](https://www.rfc-editor.org/rfc/rfc9293.html#section-3.1) | Section 3.1 Data Offset: variable TCP header boundary includes options. |
| [L4](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/Documentation/networking/proc_net_tcp.rst) | Endpoint/inode fields and deprecation of proc TCP tables. |
| [L5](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/Documentation/filesystems/proc.rst) | Section 3.13: process-owned fd links with bracketed identifiers; joining to the inode column remains inference and requires smoke. |
| [L3](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/include/uapi/linux/if_packet.h) | sockaddr_ll direction, PACKET_OUTGOING, PACKET_STATISTICS and tpacket_stats fields. |
| [D1](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Stopwatch.Unix.cs) | Stopwatch.GetTimestamp invokes Interop.Sys.GetTimestamp; frequency uses nanoseconds. |
| [D2](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/native/libs/System.Native/pal_time.c) | SystemNative_GetTimestamp invokes minipal_hires_ticks. |
| [D3](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/native/minipal/time.c) | HAVE_CLOCK_MONOTONIC branch uses CLOCK_MONOTONIC and nanosecond units; deployed build remains to be verified. |
| [L2](https://github.com/torvalds/linux/blob/adc218676eef25575469234709c2d87185ca223a/Documentation/networking/filter.rst) | Introduction: SO_ATTACH_FILTER and SO_LOCK_FILTER semantics. |
| [D4](https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/Common/src/Interop/Unix/System.Native/Interop.GetTimestamp.cs) | LibraryImport explicitly maps Interop.Sys.GetTimestamp to SystemNative_GetTimestamp. |

Repository inputs were checked at the source commit above: `scenarios/A02/scenario.yaml` and `BaselineSettingsCatalog.cs` for configuration fidelity; `idle-observation-contract.md` and `idle-observation-runtime-limits.json` for record shape and phase bounds; `charts/parchwell/templates/_idle-observation.tpl` and `infra/cluster/aks.tf` for security/resource context; `infra/scripts/phase/idle_observation.py` and `scenario.yml` for admission/reservations. These are existing harness settings, not new claims about service behavior. The relevant blueprint boundaries are ADR-003/004/008, ADR-014/015, ADR-019/020 and section 12.

The document is the plan's fenced text with only approval-status updates authorized by the subsequent approval and the check's exact D4/inode-join corrections. Historical headings remain to preserve the exact acceptance command; the status section explicitly resolves their former proposals. No design mechanism, schema, numerical bound or execution scope changed. The approval's requirement for later exact storage/publication paths is explicit without inventing a destination.

Authority catch-up saved the issue body, all comment metadata pages, selected full bodies and all label-event pages. No `ready-for-agent` event was returned; direct item-level authorization and Codex assignment are standing-decision points 1-3, followed by Hari's explicit Revision 3 approval. The check identifies the unchanged plan timestamp, and the later approval resolves both of its blocking findings. The state index now records that ordering. Other-issue silent-drop/timer evidence and the separate control-session summary were read as evidence, not adopted authority. REST confirms registration and the separate judgment/verdict PRs are merged. No artifact was reinterpreted as packet evidence.

## Verification

Execution card prepared before implementation. Against source `788574480788e74b6b937e7d03c93d0d99e056e1`, the following acceptance completed on 2026-11-03:

| Command/check | Actual result |
|---|---|
| `npm ci --ignore-scripts` | Exit 0; locked dependencies installed. Existing glob deprecation notice retained without a dependency change. |
| `npm run check:docs` | Exit 0; eight warnings, all in unchanged files. No warning in an authored path. |
| `git diff --check` and `git diff --cached --check` | Exit 0; no whitespace errors. |
| Verbatim Python command in plan "Acceptance and publication" | Exit 0; exact three-path set, ASCII, section uniqueness and budget arithmetic passed. |
| Byte comparison with Revision 3's fenced design plus explicit approval-status and check-correction substitutions | Passed; no other differences. |
| State/receipt authority-reference script | Passed against saved full snapshot; current Revision 3 timestamp and later approval/check relationships verified. |
| Pre-review authority refresh | No issue-body, comment-ID or comment-update changes. |

The arithmetic assertions verify five-pod resource totals, event/file ceilings, group reservations, total dispatch work and twenty-pod smoke/batch cap. These are approved policy budgets, not measured runtime performance. No runtime tests or live runs were requested by this documentation plan. Raw API responses, downloaded sources and generated output remain outside the repository.

The unchanged-file documentation warnings are pre-existing and outside this PR. Actionable warning/non-cold-review finding dispositions in the three authored paths: zero at this checkpoint.

## Review record

Review input: `design-docs-r1`, the frozen staged diff against source `788574480788e74b6b937e7d03c93d0d99e056e1`, including this receipt and the current state index. Mode: initial comprehensive review of all authored work using `repo-review`; comparison revision: source main. The diff's content hash is retained with the review report.

At input freeze no cold round has completed; this is comprehensive round one, with zero focused rounds consumed. The merged registration PR's independent budget is not reused. The complete report, dispositions, cumulative counts and reviewed revision will be linked from the PR self-review record, rather than adding the final report to its own reviewed receipt. Final-head CI and hosted review belong in the PR description. No later authored change may reuse coverage without the applicable rule 8 review.
