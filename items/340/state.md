# State: #340 C5: stop static-field mutant state from making other mutants look killed (#301 child 11)
Covers: comment 6603619206 created 2026-11-03T12:19:56Z; read at 2026-11-03T12:25:01Z
Parent items: [#301](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301)
Item plan: [hold decision 6602480631](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6602480631), which adds split entry 11 under the [split decision 6563452228](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6563452228)
Implementing tool: Claude Code. Source: no comment on the issue or its listed parents before `ready-for-agent`; Claude Code by default
Approved plan: [C5 static-field isolation implementation-grade plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603225182), updated_at 2026-11-03T12:04:33Z, approved under standing approval by [plan check 6603619206](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6603619206)
Decisions in force:
- Issue body: split entry 11 of #301; fixes the C5 measurement so static state left by one mutant cannot make other mutants look killed, corrects the "Issue 267 span shards" statement, and publishes no verdict. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340)
- Adopts, in creation order, the #301 decisions 6563515099, 6563540913, 6569906599 and 6602480631; under the override decision the plan may be approved under "Standing approval" within the hold decision's scope limits, and the #301 control session may merge. Source: [decision 6602486682](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/340#issuecomment-6602486682)
- Hold decision: no A02 or A04 verdict merges on campaign 41602164797; this child fixes the measurement; changing coverage analysis, adding mutant exclusions, changing the target set or the 60 break threshold, or changing any workflow trigger goes to Hari; survivors keep the cap of 50. Source: [hold decision 6602480631](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/301#issuecomment-6602480631)
Open questions:
- None
Next action: the #301 control session merges this pull request after the merge rule's checks, then dispatches the C5 campaign on `main` and reruns the survivor comparison.
