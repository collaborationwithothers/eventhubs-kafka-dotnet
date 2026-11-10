# B10 restart execution part 1: stopped on a scope conflict

Implementation stopped under the approved plan's section 15, rule 1. The plan requires the two new chart modes to be allowed only with the diagnostic flag true, but its permitted schema edits leave both modes valid when the flag is false and the binding is absent. The partial implementation is unbuilt and saved only on the backup branch; no pull request exists.

## Execution card

- Deliverable: execution part 1 only, as [Revision 6](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/fc1a4f36452b2d302f1d73987e1ba5eb967b3d65/items/404/plan-b10-execution-loadgen.md) sections 2 to 4 specify. This card was recorded at the stop, after the initial partial edits.
- Authorization: [plan pointer](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6775850681), [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776159640), and decisions 6773282644, 6774480762, 6774981258, 6775474492, 6775679271, 6775792095 and 6776084683 linked by that approval, all read in full.
- Verified plan commit: `fc1a4f36452b2d302f1d73987e1ba5eb967b3d65`; exact-body SHA-256 recomputed with `sha256sum`: `728351add854338a840433ab47f4aad540cd4690ea2fbe0a17cbb01ca898272a`. Both approval and pointer match.
- Newest exact-revision [plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6776377996): blocking findings none. Its four apply-in-implementation findings remain mandatory.
- Source baseline: `2ed497d6a8d9ea9fe4b0c00ed4f1ce54f3f41e43`; prerequisite design PR 427 is merged. Branch: `codex/404-execution-loadgen`; backup: `codex/404-execution-loadgen-wip`.
- Mandatory gates and exact verification commands: plan sections 8 and 9, including all 21 manual mutations, real child/Kafka tests, `./scripts/verify-dotnet.sh`, chart/phase checks, ASCII and scope checks; then `npm run check:docs`, cold review, final-head CI and hosted Codex code/security review. None has been completed.
- Earliest non-backup publication: the tested and cold-reviewed checkpoint in docs/agents/pull-requests.md rule 8. Not reached. No pull request or merge is authorized at this stop.
- Review budget: at most two comprehensive and two focused cold-review rounds, cumulative across sessions. Consumed: zero comprehensive, zero focused. Exhaustion hands off to Hari under rule 8. The plan separately caps verification-failure and review-finding root causes at 20 each, with a backup push and next-session handoff on exhaustion; neither cap was exhausted.
- Predicate map: pinned plan sections 8.1, 8.2, 8.2a and 3.14. No verifier production predicate changes are permitted; the protected rejection test's specified adaptation is partially drafted.

## Blocking evidence and requested decision

Plan section 3.13 says: "Abrupt modes are `loadgen-restart` and `loadgen-successor`, allowed only with `b10_abrupt_diagnostic` true." Sections 2.1 item 4 and 2.2 permit only the bounds constant and four mode enums in `values.schema.json`, freezing every other line. The existing abrupt conditional's `else` only forbids `b10_abrupt_binding_configmap`; it does not restrict `runtime.mode` when the flag is false.

After exactly the approved enum and bounds edits, the following command was run from the repository root using the already available Python `jsonschema` package:

```sh
python - <<'PY'
import json
import sys
from jsonschema import Draft7Validator
sys.path.insert(0, 'charts/parchwell/tests')
from test_b10_abrupt_chart import diagnostic_cases
schema = json.load(open('charts/parchwell/values.schema.json'))
for case in diagnostic_cases():
    runtime = case['runtime']
    runtime['b10_abrupt_diagnostic'] = False
    runtime.pop('b10_abrupt_binding_configmap', None)
    errors = list(Draft7Validator(schema).iter_errors(case))
    print(runtime['mode'], len(errors))
PY
```

Local observation on 2026-11-10: each of the two modes had zero validation errors. This checks schema acceptance only; Helm is not installed in this workspace, and no render result is claimed. The template's command, abrupt environment and binding branches depend on the flag, so these schema-accepted inputs would select the ordinary template branches. That branch-selection conclusion is a source inference, not a measured render result.

Hari's decision needed: authorize a plan scope amendment allowing the abrupt conditional's false branch to reject the two new modes (while preserving the existing binding prohibition), with exact negative test inputs for false and absent flags. The amended plan needs the check and approval that factory.md "Item lifecycle" requires before execution resumes. No extra condition was added to the schema and no contract was weakened.

## Partial work preserved

- Chart values, four schema enums and bounds, helper role mapping, abrupt template branches and chart/host test expectations: drafted only.
- `phase.py`: only the approved render-hook condition changed. `b10_abrupt.py`: partial schema-3 reader adaptation; Python tests and producer checker are not yet adapted.
- LoadGen `B10AbruptOptions.cs`, `B10AbruptWire.cs` and `B10AbruptPublisher.cs`: partial schema-3 edits. Callers remain unadapted; the publisher references planned journal APIs that are not implemented. This tree is not claimed buildable.
- Protected `B10AbruptRejectionTests.cs`: draft 14-file export list, schema-3 expectations and journal-first-line coverage. Existing rejection variants and reader assertions retained. Production export remains unadapted.
- No LoadGen tests were changed or deleted. No Python test methods were deleted. The required per-method inventory remains undone.
- No build, full test suite, mutation run, documentation check, cold review, CI or hosted review was started. No program output is committed. The schema probe above is the only execution evidence claimed.

## Plan-check finding dispositions at this stop

Findings 1 to 4 are accepted as mandatory and remain unimplemented: in-process export within unchanged worker bounds; retained successor invocation checks and payload mutation; schema mutation targeting `first/termination.json`; real host composition for T-N3. Partial phase edits are not claimed to complete finding 2.

Finding 5: apply the per-method keep/adapt/delete rule to Python tests, with a receipt inventory; no deletion occurred. Finding 6: remove obsolete `collect_cohort_files` and its call with schema-3 conversion; not yet completed. Finding 7: treat `gap` as neither exclusively producing-thread nor evidence of producing-thread activity after `kill_intent`; no classifier has yet been implemented. Finding 8: keep `ChildCommand.ForRunningApphost` or adapt the startup hook without new configuration keys; neither has yet changed. Finding 9: documentation warnings remain an uncapped runtime-sized step under AGENTS.md rule 11; no measurement was made. Finding 10: use the actual authority snapshot and exact pinned plan; no change to the approved plan's prose is authorized here.

## Handoff

Resume only after the plan conflict is resolved through its amendment process. Finish the incomplete implementation and every mandatory acceptance gate, then start the first cold review. The final pull request must wait for Hari because it touches a protected verifier test. No part 2 implementation, offline end-to-end procedure, classification plan, live trial, workflow dispatch, result or expected-outcome change was performed.
