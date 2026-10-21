# A01 Terraform foundation verification

The persistent and ephemeral roots passed offline initialization, validation, linting and native mock tests. The formatted implementation passed both native suites again. No Azure login, live plan, apply, destroy, backend readiness check or scenario experiment was run.

Authority is the [approved Revision 3 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318201099), its [independent check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6319018427) and [Hari's tags/external-backend decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448). The [operator guide](../../terraform.md) documents private settings and the entry C interface. Actual backend connectivity and authorization remain deployment prerequisites.

## Evidence

[raw-manifest.json](raw-manifest.json) gives command, source context, byte length and SHA-256 for each raw file. Authoring validate/lint logs correspond to the unformatted implementation captured in commit `aa095eb753f178a15cec72692c1ffd98581fb142`; their commands ran before that commit was created. Commit `fa39ef80b7509c023799ca0717266d11e507faa0` changes only Terraform formatting. The final native tests and formatter checks were run against that committed formatted source; initialization was subsequently repeated against the unchanged provider/module graph. The empty [whitespace-insensitive diff](raw/format-equivalence.log) records the formatter-only comparison.

Native results are [persistent](raw/persistent-test.log) and [ephemeral](raw/ephemeral-test.log). [summary.json](raw/summary.json) reports the names, pass/fail/skip counts and source declaration counts, computed by [count-tests.py](count-tests.py). The counter supports the conventional one-line run declarations used by these formatted files and the captured native no-color output; it reports missing success summaries as null. Terraform's captured result and exit status establish success, not the counter alone.

Separate raw logs preserve each root's successful init, validate and lint output, tool/plugin versions, recursive formatter checks, plugin initialization, documentation scan and diff checks. The initial documentation scan found local heading anchors, which were corrected before the final scan. A full-tree whitespace check reports the native validator logs' trailing blank lines; these raw bytes are preserved. The authored diff check uses the same generated-raw exclusion as the required review diff and passes. Pinned upstream AzAPI and Application Insights deprecated-attribute warnings remain visible; vendored module files were not changed. The direct compacted topic has a resource-local lint suppression for `prevent_destroy`, because the approved teardown contract expressly forbids that lifecycle guard.

## Mechanical execution corrections

The resource and security contract remains the approved plan's contract. These corrections resolve fixture syntax, producer addresses and execution mechanics discovered by the pinned tools:

- The positive synthetic suffix is `offline1`, satisfying the approved naming validation. The plan's `offline01` typo does not satisfy that same validation. All dependent synthetic names and IDs were corrected together; no deployment validator was widened.
- Root-local `tests` symlinks point to the canonical files under `tests/terraform/`. The command uses `-test-directory=tests`; the pinned Terraform rejects an out-of-root testing-directory argument before reading tests.
- Counted AVM child overrides use their actual indexed resource addresses. Resource nested-block mock overrides use an object with the same configured fields and computed private IP; data-source computed lists retain their schema shape. This follows [the pinned mock-value implementation](https://github.com/hashicorp/terraform/blob/7b8f1669b95119ac6021e05c9d10eae7cf75f60b/internal/moduletest/mocking/values.go).
- The Blob endpoint read derives its name from the actual endpoint ID and uses the identical configured resource-group name from `local.names`. That avoids a broad module/resource dependency forcing an unknown read during a mock plan. Its endpoint ID still supplies the creation dependency; neither lookup identity nor an IP assertion was replaced. See [the pinned data-source deferral implementation](https://github.com/hashicorp/terraform/blob/7b8f1669b95119ac6021e05c9d10eae7cf75f60b/internal/terraform/node_resource_abstract_instance.go) and [managed-resource reference handling](https://github.com/hashicorp/terraform/blob/7b8f1669b95119ac6021e05c9d10eae7cf75f60b/internal/terraform/transform_reference.go).
- Exact tag equality compares canonical JSON representations of provider maps and HCL objects. The complete expected key/value map remains required.

No assertion, role-scope requirement, access restriction or negative input case was removed. Mocks establish configured contracts only; they do not establish Azure permissions, network reachability, Kubernetes GA availability, ingestion, watermark authorization or an Event Hubs result.

## Review record

The [cold review report](cold-review.md) records the independent review of the implementation and raw manifest. The pull request's self-review record records the final review of the branch state, including that report. The pull request requires Hari's merge because it changes persistent infrastructure, network/identity files and protected owner documents.
