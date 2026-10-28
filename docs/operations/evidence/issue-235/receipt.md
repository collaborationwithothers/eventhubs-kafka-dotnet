# C08 final-run publication receipt

C08 publishes only final run 40722187672, attempt 1, while preserving its original incomplete assessments. Hari's C08-only waiver authorizes the separate proposed `undocumented` verdict; the coordinator SASL transport FAIL cause remains unknown. This receipt records publication checks, not a new scientific rule or live run.

The final-run source is `20f17794330b60d77a75bf5788ee442cc8baa1f6`; the publication branch starts at current main `4a23cd823f462939de911d7ea8e61ccbbb4c5bb1`. Authority is [decision 6477476542](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477476542), its [answer-leading amendment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477608404), the [counter-correction decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477856898), [plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477632754) (`updated_at: 2026-10-28T18:56:57Z`), its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477945421), and [exact-revision approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/235#issuecomment-6477970380). The earlier Revision 1 counting blocker is superseded by that authorized correction. Hari reviews and merges the verdict-labelled PR.

The [report](../../../../results/C08/2026-10-28.md) records the scoped source-supported inference and [JSON](../../../../results/C08/2026-10-28.json) retains complete decoded candidate objects, the C0 receipt and the C1 transport gap. The waiver is `incomplete_configuration: no_verdict`, for C08 only. C1's original null verdict, undecided hypotheses, incomplete aggregate and null fields remain unchanged; the published class is exactly "no assignment and no error within the window". No scenario, expected outcome, harness, infrastructure, package or workflow changes belong to this PR. No new workflow was dispatched and no further spend or SASL investigation was performed.

## Inputs and hashes

Retained downloads were hash-checked before reuse. The [candidate artifact 12834018507](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40722187672/artifacts/12834018507) ZIP SHA-256 is `a59b5cb6c9acaacb455eb2b9ef662961a67f89c8f15ae8dd3cfa30e46493bd14`; the [pod artifact 12835061323](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40722187672/artifacts/12835061323) ZIP SHA-256 is `bd40891e17c72c7fce5b701e992fb424dafcfc42b1e1f15e7fcbd2a07c0e8bef`.

| Candidate member | SHA-256 |
|---|---|
| C08-C0.json | 6f926d6df9e358d7f39da0ea40a637a4947139f6980de0e364a1f1cdb37cc5e6 |
| C08-C1.json | 0b7808af8396fca11a79c0f326c1303267c6edad1868adadc15781f59b157c22 |
| C08-C0-judge-completion.json | 5fd07845a6b2f1ba7aec06f9cba536ab18e3b5e0b601f79edaf1019984896986 |
| C08-C1-gap.json | 0c77c92ac3ecf4d301109220fbf9e33180b50b31e18fb6813bea5fd4f1321222 |

The unchanged local YAML bytes equal `git show` at the final-run source. SHA-256 is `061416216f9acbac792d61f85df0d5be90043b67e1bb4fdddc055bac5fcfcd47` for `scenarios/C08/scenario.yaml` and `396688c66141e4112bfbf476a7a027fd024cfa4978d33be994f12a4c2846227b` for `scenarios/C08/expected.yaml`.

## Commands and measured results

Commands ran from the repository root with retained artifact inputs under `/tmp/issue235-verification/`. Temporary authoring and checking scripts are included in the complete cold-review material and are not committed; downloaded bytes and program output are not committed. The authoring script `/tmp/issue235-implementation/author.py` transcribes the approved content and parses candidate objects without changing them. Its SHA-256 is `1f2964fcc5d291e868fd647f2b025d6774b7ade47844214b199b217c27a46ea7`.

```sh
python /tmp/issue235-verification/verify.py
python /tmp/issue235-verification/verify-mutation.py
python /tmp/issue235-verify.py /tmp/issue235-verification/candidate-40722187672.zip /tmp/issue235-verification/pods-40722187672.zip results/C08/2026-10-28.json
git merge-base --is-ancestor 1f14866faf395ecb28d3b9009986c1b332074386 20f17794330b60d77a75bf5788ee442cc8baa1f6
git diff --check
git diff --cached --check
git diff --name-only main
git diff main -- scenarios/C08/scenario.yaml scenarios/C08/expected.yaml
npm run check:docs
```

The first two scripts completed with all 176 artifact/evidence comparisons and 20 mutation-summary/ancestry comparisons matching. Their respective script SHA-256 values are `e2b21b4a7e762b801a45fcad4c78739af2069b13a0b5b8b280e5645be2f12e25` and `a150b1ca99957e2e8b0697beec215ec92556360a04b0056e4478a0a94b4f13ee`. Those checks are bounded by the retained evidence and do not independently recount individual mutant reports.

The finite publication checker `/tmp/issue235-verify.py` completed with `C08 final-run evidence checks passed`. It checks ZIP/member hashes, decoded-object equality, ten literal report rows, absent C1 receipt, unchanged aggregate assessment, publication waiver and class, exact debug predicates, four pod identities/accounts/times/exits and zero restarts, collection completeness, no capture-log cap markers, absence of ADR-012 numeric counts and unchanged YAML bytes. It preserves missing evidence rather than filling zeros. Its SHA-256 is `5fe04f2b2bcb70c9619f9c41b5caa55b4e04bbd09caad8f020fa94d5e7c5db01`.

Final C0 has five complete successes with broker-count lower bound 17 each. Final C1 repeats 1 and 5 are complete `no_outcome` with lower bound 2 each; repeats 2, 3 and 4 remain incomplete with `p4_no_terminal_outcome` and `p6_broker_count`, null outcome and null broker/statistics fields. The published quoted COORD-event counts are `[1,1,1,1,1]`; the same client suffixes give bare unsupported-API substring counts `[2,2,2,2,2]` and coordinator SASL FAIL counts `[0,1,1,1,0]`. The checker applies the exact Revision 2 predicate, including repeat prefix, client suffix, COORD tag, coordinator-dead text and unsupported-API substring. C0 has neither substring. The report distinguishes those measurements from the pinned-source inference.

For corroboration only, the independently rechecked [first run's pod artifact](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40693356087/artifacts/12821252751) yields quoted COORD-event counts `[1,1,1,1,1]` using that same predicate, separately from bare-substring counts `[2,2,2,2,2]` in its five C1 repeats. These measurements supply no published final-run repeat totals and no repeats are pooled.

The actual [C5 summary](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/40642331044/artifacts/12808596014) records source `1f14866faf395ecb28d3b9009986c1b332074386`, `measurement_complete:true`, `break_passed:true`, and six shards. Its ZIP SHA-256 is `0d16bc3acb830b1691efd1c517a6c34bd72e7e0314e60faeab6e23929d0bc83a`. A fresh workflow-run API read confirmed 40642331044 remains the latest C5 run. The ancestor command exited 0. An argument-list invocation of `git diff --exit-code` compared all six `mutate` entries resolved under `src/Parchwell.Verifier/`, plus the Stryker configuration and `.github/workflows/verifier-c5-mutation.yml`, from that measured commit to the final source and to the publication base; both comparisons exited 0. This PR changes none of those paths. Reproduction of the argument-list comparison:

```python
import json, subprocess
config = json.load(open('tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json'))
paths = ['src/Parchwell.Verifier/' + p for p in config['stryker-config']['mutate']]
paths += ['tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json', '.github/workflows/verifier-c5-mutation.yml']
for target in ['20f17794330b60d77a75bf5788ee442cc8baa1f6', '4a23cd823f462939de911d7ea8e61ccbbb4c5bb1']:
    subprocess.run(['git', 'diff', '--exit-code', '1f14866faf395ecb28d3b9009986c1b332074386', target, '--', *paths], check=True)
```

The batch destroyed-resource plan supplies Premium/1 PU/uksouth; pinned cluster source plus its no-change plan supports the labelled node-SKU inference. Observed node size remains unavailable. The linked batch/stop jobs record cleanup; no infrastructure command was executed in this session. Current official Microsoft guidance and pinned native/source producer paths were reviewed for the report's claims; a wire API response and exact raw callback records remain unavailable.

## Documentation warnings and cold review

The exact six approved paths were staged individually before the documentation scan. Diff whitespace checks and the six-path/YAML scope checks passed. `npm run check:docs` completed successfully with no warning in a changed file. Existing warnings in files outside this PR remain unchanged and are outside its scope; this blanket disposition follows AGENTS.md rule 8. The local `main` ref was absent, so it was created at the fetched `origin/main` solely for the plan's literal diff commands.

The complete authored review material includes the six-file diff, temporary authoring/check scripts, source-verification brief and earlier planning/check prose and scripts. Cold-review round 1 reviewed every supplied path and found only the mandatory P0 for adding results before a PR can carry the verdict label; it found no other substantive or P2 finding. That P0 is retained under AGENTS.md rule 8's approved-verdict exception, listed in the PR description, and resolved by a verdict-labelled PR reviewed and merged by Hari. The final P0/P1-only round is pending after a prose correction to already-reviewed material. The PR description records the final reviewed head and last cold-review round, without a receipt-only commit; Hari remains the merger.
