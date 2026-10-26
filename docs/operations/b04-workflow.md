The shared scenario workflow runs B04's C0 and C1 phases, five repeats each, when its scenario input is B04. It reuses the existing lifecycle and immutable worker images, destroys each job's ephemeral layer, and stops AKS when the dispatch ends.

Dispatch the workflow from `main` with the B04 choice:

```sh
gh api -X POST repos/collaborationwithothers/eventhubs-kafka-dotnet/actions/workflows/scenario.yml/dispatches -f ref=main -f 'inputs[scenario]=B04'
```

The `scenario` input is required, defaults to `A01`, and allows only `A01` and `B04`. Runtime validation checks that selection against the phase bridge's scenario allowlist before Azure login or private-input materialisation.

The shared workflow keeps its existing job IDs. For B04, `probe` runs phase C0, `full-C0` is skipped, and `full-C3` runs phase C1. C0 runs all five repeats, judges the phase and publishes its candidate and pod evidence. The C0 candidate does not gate C1. C1 is admitted when both worker image outputs from the initial job are immutable SHA-256 digests matching the existing digest format; this image gate does not require C0 success. A01 keeps its probe/C0/C3 gates.

After expression evaluation, the B04 artifacts are named `b04-C0-candidate-<run_id>-<run_attempt>`, `b04-C0-pods-<run_id>-<run_attempt>`, `b04-C1-candidate-<run_id>-<run_attempt>` and `b04-C1-pods-<run_id>-<run_attempt>`. The run ID and attempt make artifacts from reruns distinct. The existing [`scenario.yml` sweeper registration](../../infra/scripts/lifecycle/dispatch-state.py) keeps active shared-workflow dispatches visible to cleanup coordination.

The post-merge C5 mutation workflow must pass for the changed verifier targets, with `measurement_complete: true` and `break_passed: true`, before a B04 evidence pack can be published. K-53 remains excluded from B04 and stays untested there; N1 remains assigned to issue [#204](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/204). The baseline remains unchanged and false. This document records workflow behavior, not a live result or verdict.

Governing authority is the [approved #207 Revision 1 implementation plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411873981), the [shared-workflow decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411483242), and the [test migration decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/207#issuecomment-6411671441).
