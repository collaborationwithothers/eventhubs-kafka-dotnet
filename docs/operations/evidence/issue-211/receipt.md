# S0 governance validation receipt

This receipt records validation of the S0 documentation changes for issue #211. It contains no live scenario result.

## Sources

Authority is the [S0 Revision 1 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/211#issuecomment-6423829569), independently checked under standing approval in [6423894283](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/211#issuecomment-6423894283), and the [adopted child decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/211#issuecomment-6423734358). The baseline and source commit for validation is `cb83254f1c2645acd86fe734b24e542e0956edd3`; validation runs on the uncommitted documentation changes against that commit, including this receipt.

C11 was verified against [TopicPartitionOffset.cs at v2.16.0](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/TopicPartitionOffset.cs) and [SafeKafkaHandle.cs at v2.16.0](https://github.com/confluentinc/confluent-kafka-dotnet/blob/v2.16.0/src/Confluent.Kafka/Impl/SafeKafkaHandle.cs#L1344-L1376). The managed offset type exposes no commit metadata. The native conversion writes offset and optional leader epoch without assigning commit metadata. The inventory's existing tested-or-traced effort totals therefore remain unchanged by the T-to-D classification.

## Validation

The locked dependency setup command `npm ci --ignore-scripts` exited 0. No dependency manifest or lockfile changed.

The exact command block in the approved plan's "Acceptance commands and evidence" was executed unchanged. Its commands produced these results on the uncommitted files described above:

| Command | Result |
| --- | --- |
| `git add docs/agents/factory.md docs/blueprint.md docs/operations/evidence/issue-211/receipt.md` | Exit 0; staged exactly the planned files. |
| `git diff --check origin/main` | Exit 0, with no whitespace diagnostic. |
| `npm run check:docs` | Exit 0, with eight advisory warnings. |
| Exact `python3 - <<'PY'` heredoc from the linked plan | Exit 0; scope, evidence-boundary and inventory assertions passed. The inventory has 76 unique entries with effort sizes S 32, M 32, L 8 and four out-of-scope entries. |

The five changed-file warnings concern the approved split-entry identifier: the factory status link and evidence-pack link, and this receipt's required heading, opening and plan link. The adopted child decision defines that identifier. These are false positives from the checker's inventory-identifier rule and stand; they use five of the plan's 20 finding/warning dispositions. The three warnings in unchanged archival documents take the blanket disposition under AGENTS.md rule 8 and remain outside this change's scope.

## Review

This receipt and the complete authored diff are submitted together for the cold review required by AGENTS.md "Before opening a pull request" rule 8. No finding has been reported before this initial submission. The final head's CI run, last cold-review result and any later dispositions are recorded in the pull request description.

Authoring used Codex; the runtime model identifier and authoring effort were not exposed. Planning, independent plan checking and cold review use fresh Codex agents with high effort and the inherited model.
