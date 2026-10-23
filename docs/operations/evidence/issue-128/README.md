# A01 default-deny removal evidence

The [checked Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/128#issuecomment-6358560976) removes A01's NetworkPolicy and service-tag dependencies while retaining private-endpoint deployment data. Local restore, build and Runner tests passed; actual chart-render acceptance is blocked by the official Helm download returning HTTP 403.

The [removal decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6358477325) authorizes the policy and related assertion removals. The [independent plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/128#issuecomment-6358729935) identifies Revision 2. These are offline checks with synthetic test inputs, not live Azure results.

## Local acceptance

Source revision: `d945d14f2c0b39dce738ab994b79d4fcf13a9b53`. All acceptance commands use the plan's 600-second cap. The [manifest](raw-manifest.json) records exact producer commands, source revisions, sizes and SHA-256 hashes.

| Check | Outcome | Evidence |
|---|---|---|
| Locked solution restore | Passed; project and dependency files unchanged from baseline | [Restore log](raw/restore.log) |
| Release solution build | Passed, zero warnings and errors | [Build log](raw/build.log) |
| Runner tests | 1045 passed, 0 failed, 0 skipped, 0 errors | [Test log](raw/runner-tests.log), [xUnit report](raw/runner-results.xml) |
| Obsolete workflow references | Absent; exit code 0 | [Reference check](raw/workflow-references.log) |
| Documentation scan | Completed; 36 existing warnings in historical source captures | [Documentation log](raw/docs.log) |
| Diff whitespace | Passed; exit code 0 | [Whitespace log](raw/diff-check.log) |
| Actual Helm chart renders | Blocked; official archive acquisition returned HTTP 403, curl exit 22 | [Acquisition log](raw/helm-acquisition.log) |

The [initial build](raw/build-initial.log) failed CA1822 after removing the fixture's service-tag path made its input method independent of instance data. The final source uses the fixture's existing scenario root for the unchanged limits path, and the subsequent build and tests passed. No analyzer was disabled.

The new preflight test observes the deliberately missing archive filename after the real scenario and limits checks; it does not validate Helm successfully. No generated render, Helm lint result or successful chart assertion receipt is claimed. The official archive remains required before the plan's chart acceptance command can run.

The complete solution test suite is assigned to existing pull-request CI by the plan. Its head-specific run and review status are recorded in the pull request, rather than claimed by these local Runner tests. Hari reviews and merges the protected changes and installs the updated scenario workflow text.

Documentation warnings in the retained scan concern copied upstream Markdown under `issue-114/raw/review-10-fixes/`: missing upstream relative-link targets and original punctuation. They remain unchanged historical evidence, outside this removal plan. The [initial documentation scan](raw/docs-initial.log) also ran before the new manifest and whitespace receipt existed; both local targets are now present.
