# Protection-read repair receipt

The live cleanup run stopped before deleting any backup because it could not establish main protection. This repair handles GitHub's redacted bypass list without adding credentials or accepting an unknown policy version.

## Execution card

- Authorization: [Hari's direct repair instruction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/448#issuecomment-6851681477), after [the failed live measurement](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/43415354105). This is a directly authorized repair; it does not claim a new contract-grade plan approval.
- Scope: cleanup protection reads and diagnostics in `scripts/cleanup-wip-branches.py`, regression cases in its existing test module, this receipt and item state. Preserve main-only ancestry, exact-head leases, all other failure paths, workflow permissions and triggers. No ruleset or credential changes.
- Protection contract: keep validating a visible bypass list exactly as before. When GitHub omits the list, require `updated_at` to represent the exact UTC instant `2026-10-31T18:09:49.042Z` and `current_user_can_bypass` exactly `never` or `pull_requests_only`. That timestamp was read with the full list visible, and every actor had `bypass_mode: pull_request`. Equivalent `Z` and `+00:00` formatting must not change the decision. A changed or missing timestamp, unsafe or missing caller status, and explicit invalid bypass data all fail closed. The attested timestamp lives in the protected script and changes only through Hari's merge.
- Diagnostics: retain the exact existing failure reason and statuses; add redacted diagnostic text identifying the failed request or predicate. Never include response bodies, credentials or redirected URLs.
- Verification: `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v`; workflow mirror comparison; `git diff --check`; `npm run check:docs`; required cold review and hosted CI/reviews. Repeat broader local verification only for a failure or new behavior that justifies it.
- Publication: open the tested PR immediately after clean cold review; protected merge belongs to Hari. One additional post-merge validation is authorized by the repair instruction, with a further failure recorded and handed off instead of repeated.
- Budget: at most two comprehensive and two focused cold-review rounds for this new repair PR, with rule 8's backup/handoff on exhaustion or unsafe focus. Do not reset the merged PR's historical counters. For unrelated findings, record the gap and continue without expanding this repair.

## Sources and observed policy

- [GitHub rules API](https://docs.github.com/en/rest/repos/rules#get-a-repository-ruleset): `bypass_actors` is conditionally visible, and repository rulesets expose their update timestamp.
- [GitHub's ruleset API announcement](https://github.blog/changelog/2023-06-27-repository-rules-public-beta-updates/): the repository-level response includes the caller's bypass capability.
- `GET /repos/collaborationwithothers/eventhubs-kafka-dotnet/rulesets/24907776`, read on 2026-11-13: active branch ruleset, default-branch condition, deletion and non-fast-forward rules, updated at `2026-10-31T18:09:49.042Z`, with a complete visible bypass list containing only `pull_request` modes. The caller's status was `pull_requests_only`.
- The original job's generic error does not prove which predicate failed. Redacted bypass metadata is the documented compatibility case this repair addresses; added diagnostics retain evidence if a different request or predicate is responsible.

## Verification and review

Base source: `259a31fca2f53842385d820b5dba9f213d9e787e`, plus this repair's frozen staged diff. Generated output remains outside Git.

- `python3 -m unittest discover -s infra/scripts/tests -p test_cleanup_wip_branches.py -v`: 20 tests passed in 30.051 s. The original assertions remain unchanged. New cases cover hidden actor metadata, equivalent timestamp formats, policy revisions below/at/above the reviewed instant, unsafe and malformed caller states, explicit invalid actor data and a policy change before deletion.
- Read-only requests using the actual API version passed the live protection parser with the full policy response and with its actor list removed. No cleanup or Git mutation was invoked. This also exposed the API's equivalent `+00:00` timestamp formatting, which the regression now covers.
- `git diff --check` and workflow/mirror comparison passed. Workflow content, triggers and permissions are unchanged.
- `npm run check:docs` completed successfully with 9 advisory warnings, all in unchanged files; retain the blanket unchanged-file disposition. Broader local suites are not repeated for this isolated guard repair; required hosted CI verifies the published head.
- Review label `protection-read-r1`: initial comprehensive cold review of the complete frozen authored diff against base `259a31fca2f53842385d820b5dba9f213d9e787e`. The supplied snapshot and its index tree identify the revision. This new repair PR starts at comprehensive 1, focused 0; the merged implementation PR's historical comprehensive 2, focused 0 remains unchanged. The final report belongs in the PR self-review record.
