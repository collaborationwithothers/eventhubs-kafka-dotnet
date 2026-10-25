# Receipt clarification and shellcheck coverage receipt

This pull request clarifies the historical worker-image receipt from PR #182. It also records the verification performed for the scope approved on issue #183.

## Authority and source commit

The work follows the [implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6401968926), [Hari's scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402008120), and the [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402045851). Later approvals add the exact [status initialization](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402294479) and [warning-control reporting cleanup](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402317036). The `/scripts/` changes require Hari's merge under AGENTS.md GOVERNANCE "Merge rule".

Baseline: `94a01f41eb8786eb3a9c85db5d2c5320621d1f67`. The initial C1/C2 source was committed as `2dc361953dbc57231bfb0e35188005f0d821e066`; its failing A2 observation was saved to the WIP branch. The candidate source commit for the completed controls is recorded after the local commit below. This receipt records observations, with raw output retained outside the repository.

## Receipt corrections

The issue-179 receipt now defines A1b and A3H, identifies its decisive A3 stderr quotation as partial, and explains why the round 3 fixes required no follow-up issue. It adds the historical round 5 disposition with its later provenance and identifies the A9 quotations as message text without runner timestamps. The substitutions retain the recorded run and job IDs, SHAs, counts and R2 classification.

The added ShellCheck loop uses the existing flags, pinned tool and deadline wrapper for top-level `scripts/*.sh`. It preserves the existing recursive infrastructure loop. The deployment script initializes `status` before installing its EXIT trap; that trap still captures the actual exit code. The .NET script retains its unexpected-error predicate and exit status, and prints the diagnostic log after closing the read loop.

## Commands and observations

Pinned tools were installed outside the repository with `bash infra/scripts/lifecycle/install-tools.sh /tmp/issue-183-implementation/tools ci`, exit 0. Its verified ShellCheck version is 0.11.0. Tests use `PARCHWELL_TOOLS_DIRECTORY=/tmp/issue-183-implementation/tools`, with that directory and its `venv/bin` prepended to PATH.

The enumerated top-level inputs were `scripts/test-kafka-coverage.sh`, `scripts/test-kafka-documentation.sh`, `scripts/verify-a01-deployment.sh`, `scripts/verify-dotnet.sh`, `scripts/verify-kafka-coverage.sh` and `scripts/verify-worker-images.sh`. The source enumeration matched the plan's input list.

| Step | Command | Observation |
|---|---|---|
| A1 | `git diff --check` | Exit 0 after both approved script cleanups; final documentation scan recorded below |
| A2, tool and syntax | `shellcheck --version`; `bash -n infra/scripts/lifecycle/check-offline.sh` | ShellCheck 0.11.0; syntax exit 0 |
| A2, completed offline baseline | `bash infra/scripts/lifecycle/check-offline.sh` | Exit 0; all selected shell inputs, workflow validation and chart checks completed |
| A3 | `bash infra/scripts/lifecycle/check-offline.sh` in an exported candidate, with the fixed synthetic fixture sequence | Pending integration controls |
| A4 | `python3 infra/scripts/lifecycle/command.py 600 -- python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | Pending regression suite |
| A5 | Exact-head hosted run and job REST reads specified by the plan | Recorded in the PR description after the PR exists |

The successful A2 run reported chart acceptance with 21 positive, 32 negative and 4 worker-setting regressions. Each of the two smoke-chart renders passed strict Kubernetes 1.35.0 schema validation. These are offline measurements; they establish no live scenario result.

### Initial findings and approved repairs

The first A2 attempt exited 1 on `SC2154` at `scripts/verify-a01-deployment.sh:25`. The added input loop stopped there; later inputs were not measured by that attempt. The scope handoff is [recorded on #183](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402084010). Hari then approved the initialization.

The rerun passed that input and exposed `SC2094` at the .NET script's log read loop and diagnostic printing, two locations belonging to the same loop. A direct scan of all planned top-level scripts reported only those locations after the initialization. Hari approved the prepared reporting cleanup. No diagnostic suppression or severity filter was added.

Before applying that cleanup, the actual original and proposed scan blocks were run against fixed synthetic logs: expected diagnostic only, an additional unrelated error, and an additional unrelated final line without a newline. Original and proposed blocks produced identical stdout, stderr and exits for each case: 0, 1 and 1 respectively. These controls isolate the changed scan block; hosted CI runs the full .NET verification script.

### A3 synthetic fixture presentation

Pending A3 execution. The planned files, each ending in a newline, are shown below; the quoted file will be restored for the final pass. This presentation applies the independent plan check's P2 without changing an enforced check.

Quoted file:

```bash
#!/bin/bash
value=$1
printf '%s\n' "$value"
```

Unquoted file:

```bash
#!/bin/bash
value=$1
printf '%s\n' $value
```

The fixture is named `scripts/issue183-shellcheck-control.sh` in the temporary exported tree. The real offline entry point performs the checks; the fixture itself is not executed. Temporary files and logs are not committed.

## Cold review

The final cold-review round and hosted CI run are recorded in the PR description under AGENTS.md "Before opening a pull request" rule 8. This receipt need not describe the commit that contains the receipt itself.
