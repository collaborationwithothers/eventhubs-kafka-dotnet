# Receipt clarification and shellcheck coverage receipt

This pull request clarifies the historical worker-image receipt from PR #182. It also records the verification performed for the scope approved on issue #183.

## Authority and source commit

The work follows the [implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6401968926), [Hari's scope decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402008120), and the [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/183#issuecomment-6402045851). That check reports no blocking findings and gives the fixture presentation below as a P2 to apply in implementation.

Baseline: `94a01f41eb8786eb3a9c85db5d2c5320621d1f67`. Source commit containing the exact C1/C2 text tested locally: `2dc361953dbc57231bfb0e35188005f0d821e066`. The commands ran on that source text before the local commit was created. This receipt is added afterward and has not completed acceptance.

## Receipt corrections

The issue-179 receipt now defines A1b and A3H, identifies its decisive A3 stderr quotation as partial, and explains why the round 3 fixes required no follow-up issue. It adds the historical round 5 disposition with its later provenance and identifies the A9 quotations as message text without runner timestamps. The substitutions retain the recorded run and job IDs, SHAs, counts and R2 classification.

## Commands and observations

Pinned tools were installed outside the repository with `bash infra/scripts/lifecycle/install-tools.sh /tmp/issue-183-implementation/tools ci`, exit 0. Its verified ShellCheck version is 0.11.0. The source-tree enumeration matched the six top-level shell inputs listed in the plan. The existing recursive infrastructure inputs remain in place.

| Step | Command | Observation |
|---|---|---|
| A1, initial diff | `git diff --check` | Exit 0 on C1/C2 before this receipt was added |
| A1, preliminary documentation scan | `npm ci --ignore-scripts` and `npm run check:docs` | Both exited 0 before this receipt existed; this is not completed A1 acceptance for the final diff |
| A2, syntax | `bash -n infra/scripts/lifecycle/check-offline.sh` | Exit 0 |
| A2, offline baseline | `bash infra/scripts/lifecycle/check-offline.sh` with the pinned tools environment | Exit 1; one emitted ShellCheck diagnostic, `SC2154`, in `scripts/verify-a01-deployment.sh` at line 25 |
| A3 | Planned production-entry-point pass/fail/pass controls | Not run after the A2 stop |
| A4 | Planned lifecycle unittest discovery | Not run after the A2 stop |
| A5 | Hosted CI | Not run; no PR has been opened |

The new loop checked `scripts/test-kafka-coverage.sh` and `scripts/test-kafka-documentation.sh`, then stopped at `scripts/verify-a01-deployment.sh`. ShellCheck reported `status` as referenced but not assigned inside that script's EXIT trap. The diagnostic is in an existing script; this PR has not edited that file. Later inputs and the remaining offline checks were not reached, so no complete lint pass is claimed. The captured baseline emitted one diagnostic; this is not a count of all possible diagnostics in unexecuted inputs.

The plan's A2 failure action and AGENTS.md "Before opening a pull request" rule 11 require preservation and handoff at this measurement. No existing diagnostic repair or suppression was attempted. A continuation that edits `scripts/verify-a01-deployment.sh` touches the protected `/scripts/` path and needs an approved scope amendment.

The following fixture descriptions apply the plan-check P2. They document the direct checks reported by the linked independent plan check, not completed A3 integration acceptance.

The plan-check P2 prescribes the following introduction for completed A3 evidence: "The synthetic control used these file contents, each ending in a newline. The quoted file was restored for the final pass." That past-tense introduction is deferred because A3 has not run. Its planned fixture contents are shown below.

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

The plan-check session directly ran verified ShellCheck 0.11.0 on the quoted and unquoted fixtures: exits 0 and 1 respectively, with `SC2086` on the unquoted file. Restoring the quoted fixture remains part of the specified A3 sequence; that integration sequence has not run in this implementing session.

## Cold review

Not run. This work is saved only to the backup branch under AGENTS.md "Before opening a pull request" rule 12, which exempts a backup push from rule 8. No implementation PR, reviewed head or hosted CI result exists. Before a normal push, the continuation must complete the approved acceptance and rule 8 cold review; the final CI and review records then belong in the PR description.
