# State: #531 Consumer evidence archive: archive support for C01, C03 and C10 (#499 entry 12)
Covers: comment 6956497218 created 2026-11-17T18:01:42Z; read at 2026-11-17T18:11:25Z
Parent items: [#499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499) (split entry 12). The pull request of [#507](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/507) is a predecessor, not a parent; it has merged
Item plan: [proposal revision 2 of #499](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6894560135), split entry 12 added by [decision 6926376436](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6926376436); this issue's own plan is under Approved plan
Implementing tool: Claude Code. Source: [decision 6955981131](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6955981131)
Approved plan: [plan comment](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943199291), revision 3, approved by [approval 6956497218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956497218)
Decisions in force:
- Issue body: archive support for C01, C03 and C10. Source: [issue 531](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531)
- Adopts the standing decision, decision 6894614673 points 1 and 2, and the entry decision. Source: [6926376950](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6926376950)
- Q1 (record blobs probed by name when no completion) and Q3 (`enrichment` image for consumer scenarios) adopted. Source: [6943488058](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943488058)
- Q2 (no consumed-metadata capture for consumer verification) and Q4 option A (deadline risk accepted by name). Source: [6943512296](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6943512296)
- Claude Code implements; Codex reviews. Source: [6955981131](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6955981131)
- Plan revision 3 at `d1ab65da` approved against `main` `3f46ccc0`. Source: [6956497218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956497218)
Open questions:
- None. D2 (a self-killed subject leaves no native-monitoring index) and DA1 (archive deadline estimate) are deferred to Hari, not blocking this pull request. Source: [6956497218](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/531#issuecomment-6956497218)
Next action: cold review of the implementation diff, then open the pull request.
