# Issue 185 validation receipt

This change shortens AGENTS.md's GitHub reference section and moves hard safety rule 8's supporting history to the factory document. This receipt records verification of the adopted preservation conditions.

Source commit: `f49d45c9f9ac52ec16a641a6ad1d4f646eeb8b54`.

Authority: [Revision 2 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/185#issuecomment-6397877187), [qualifying plan check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/185#issuecomment-6399553353), [adopted decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/185#issuecomment-6397658044), and [continuation and merge-method decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/185#issuecomment-6399516221).

## Validation

| Command or inspection | Observed result |
|---|---|
| `npm ci --ignore-scripts` | Exit zero using the locked dependencies. npm reported the existing glob deprecation warning; dependency files were unchanged. |
| `python3 /tmp/accept185.py` (the exact Python acceptance block in the linked Revision 2 plan) | Exit zero. Complete file comparisons matched the planned replacements, the retained rule 8 prefix and relocated basis were preserved verbatim, all remaining authority text was unchanged, and the staged paths matched the plan. |
| `git diff --check` and `git diff --cached --check` | Both exited zero without whitespace diagnostics. |
| `npm run check:docs` | Exit zero with no documentation warnings. Both new local links resolved, and the scan printed its documented completion line. |

The final cold-review round and final-head CI evidence belong in the pull request description under AGENTS.md "Before opening a pull request" rule 8. This receipt records the commands above against the source commit and authored changes, without a receipt-only commit to describe its own head.

## Limits

This pull request does not establish new GitHub or Azure behavior. It preserves the rule text and moves its dated supporting record verbatim. Hari merges the protected paths; the selected merge method for this pull request is squash.
