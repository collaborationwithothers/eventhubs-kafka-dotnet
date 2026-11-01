# Issue 280 receipt

This receipt records the change to `docs/operations/evidence/issue-275/replay.py` for issue 280. Its `run` command now checks the three target files on the tree it replays, and states that it runs the tests committed at HEAD. No result, verdict, disposition or evidence pack changes.

Source commit for the commands below: `8078e6fcc16b1115899fbfb3edfbb1299cb8eaf1` (`origin/main`) plus the commits of this branch.

## Execution card

- Deliverable: file changes 1 to 4 of the [replay-head-guard plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6555881449).
- Authorization: that plan revision (`updated_at` 2026-11-01T09:20:01Z), approved by [approval 6556152255](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6556152255); the [issue 280](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280) body; the tool decision [6555751683](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6555751683); the revision decision [6555985151](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6555985151); the merge-method [decision 6556139255](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6556139255).
- Mandatory gates: the acceptance commands below; the cold review under AGENTS.md "Before opening a pull request" rule 8; hosted CI; Codex review; the merge under AGENTS.md GOVERNANCE "Merge rule". No changed path is protected (`docs/operations/` and `items/`; docs/agents/factory.md "Ruleset"), so the implementing session merges when every condition of that rule holds, through the REST merge route with `merge_method` `squash`.
- Verification commands: the "Acceptance commands" block of the [replay-head-guard plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/280#issuecomment-6555881449), run exactly as written; the literal results are under "Commands and results" below.
- Publication checkpoint: open the pull request after the first cold review with no P0 or P1.
- Review budget: rule 8 as written; exhaustion action is its review-budget handoff to Hari.

## Predicate map

Predicate map: not applicable; no verifier predicate changes.

## Commands and results

Run from the repository root with SDK 10.0.402. The five report ZIPs were downloaded through the REST artifact route into a directory outside the repository.

| Command | Literal result |
| --- | --- |
| `git diff --quiet 2dc9715e3de176f7e1cd55eee9c251e43c29c746 origin/main -- src/Parchwell.Verifier src/Parchwell.Kafka src/Parchwell.Scenarios tests/Parchwell.Verifier.Tests Directory.Build.props Directory.Packages.props global.json && echo unchanged` | `unchanged` |
| `sha256sum` of the five ZIPs | phase `30ee41906b695893da4e4ff6fe51c9ec8446c53b3ed565f6d02cde81374b4067`; publication `0958c1cbe3aa2b2b0b1e3fc6a4827689ca56253b423958c2ffb031164fe1a715`; repeat-p1 `d46046521cd7dec9fe61d62c14b19d97377734ae57a4858cbb3b9de303fa5fb4`; repeat-p2 `982718362f8adfd6a4ab9da3c5113642e5f0788f2f953d121f63310da190acb4`; repeat-p3 `24f7de948380c665a4d026ff99c9cbc3ecfe578af8fcd736acfeb6dc50da9500` |
| `replay.py run ... 1/31` with a line appended to `Judge/PhaseJudge.cs` in the working copy only | exit 0; stdout `run replays the tests committed at HEAD, which may differ from the tests of run 41232286130; a difference from the recorded dispositions is expected when the verifier source or tests have changed since that run`, then `4994 survived 0`; record `{"failed_methods": [], "id": "4994", "status": "survived"}`, as disposition E1 expects |
| `replay.py run ... 1/31` with that line committed | exit 1; stdout `run replays the tests committed at HEAD, which may differ from the tests of run 41232286130; a difference from the recorded dispositions is expected when the verifier source or tests have changed since that run`; stderr `['Judge/PhaseJudge.cs'] at HEAD differ from the source the report measured; the mutant locations no longer apply`; no output file |
| `git diff --name-only origin/main...HEAD` | 4 names: `docs/operations/evidence/issue-275/receipt.md`, `docs/operations/evidence/issue-275/replay.py`, `docs/operations/evidence/issue-280/receipt.md`, `items/280/state.md` |
| `git diff --check origin/main...HEAD` | no output |
| `npm ci --ignore-scripts && npm run check:docs` | `8 warnings`, none in a changed file. Blanket line: the 8 baseline warnings are in unchanged files (`docs/agents/factory.md` 2, `docs/operations/evidence/issue-124/README.md` 1, `docs/operations/evidence/issue-140/README.md` 2, `docs/operations/evidence/issue-211/receipt.md` 3) |
| `gh api repos/collaborationwithothers/eventhubs-kafka-dotnet/rulesets/24907776 --jq '.rules[]\|select(.type=="pull_request")\|.parameters.required_reviewers[].file_patterns\|join(" ")'` | `/docs/ !/docs/operations/ /.*/ /*.md /skills-lock.json /infra/persistent/ /infra/ephemeral/identity*.tf /infra/ephemeral/network*.tf /scripts/ /results/ expected.yaml Parchwell.Verifier*/ /Directory.*.props *.sln* *.csproj` |
| `git diff --name-only origin/main...HEAD \| grep -v -E '^(docs/operations/\|items/)' \|\| echo "no protected path"` | `no protected path` |
