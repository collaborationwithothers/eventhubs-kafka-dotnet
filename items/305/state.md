# State: #305 F02: restore the two IMDS arms beside nativejwt for one final run (#302 child 3)
Covers: comment 6560508780 created 2026-11-01T16:14:10Z; read at 2026-11-01T16:30:00Z
Parent items: [#302](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/302)
Item plan: none
Implementing tool: Claude Code. Source: [comment 6560164776](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560164776), created 8 s after `ready-for-agent`; with no earlier naming comment the default is Claude Code
Approved plan: [f02-three-native-arms plan, Revision 1](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560249552), updated_at 2026-11-01T15:52:18Z, approved by [comment 6560508780](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560508780)
Decisions in force:
- Issue body: adopts split entry 3 of #302 (restore `nativeimds` and `nativeimdsnode` beside `nativejwt`, keep the explicit CA file); no predecessor pull request; the dispatch needs Hari's separate approval after this pull request merges; the earlier IMDS run is not pooled with the new one. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305)
- Approves plan Revision 1 for implementation and offline verification only, given in light of plan check 6560365744 (`Blocking findings: none`, no `apply in implementation` finding). It approves no dispatch, Azure action, spend, expected outcome or verdict. Whether a new expectation line is needed stays open until before the new dispatch. Source: [approval 6560508780](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305#issuecomment-6560508780)
Open questions:
- Whether a new expectation line is needed for the later dispatch. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/305) ("Not decided")
Next action: Hari reviews and merges the pull request; the dispatch needs his separate approval after the merge.
