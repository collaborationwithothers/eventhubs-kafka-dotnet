# State: #474 verifier mutation runtime

Covers: comment 6894509069 created 2026-11-15T12:44:48Z; read at 2026-11-15T12:55:16Z
Parent items: [#347](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/347)
Item plan: [mutation runtime plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876990691)
Implementing tool: Claude Code. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894078013) point 6, restated in the [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894509069)
Approved plan: [sizing-and-reuse revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893784498), commit `0e7e1276c0da24dc4ee3bfadb749741b163dc1db`, with fixes 1 and 2 of [handoff 6894460810](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894460810) adopted as written. Source: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894509069)
Decisions in force:
- Reduce mutation workflow runtime while preserving fresh coverage. Source: [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474)
- The cancelled mutation run is not acceptance evidence. Source: [cancellation](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876575032)
- Use bounded profiling, estimates and unchanged fresh mutation testing. Source: [design](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6876901367)
- Reassess all shard timing when any target changes. Source: [timing correction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877623126)
- Implement the approved two-pull-request plan, starting with bounded tooling. Source: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6877906971)
- Proceed with the measured rebalance after the tooling merge, preserving the shared measurement allowance. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6879235555)
- Accept a verified empty reconciled native target inventory without relaxing nonempty or combined populations. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6879907632)
- Accept source-bound minor static-owner overruns for two measured fields while retaining isolation and balancing other work. Sources: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880280289) and [clarification](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880342017)
- Use retained evidence and source-bound provisional qualification, then merge and measure on main. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6880732047)
- Update the two preserved-workflow digest fixtures for the intentional changes while retaining their assertions. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6881306738)
- Continue necessary correction reviews without further per-round approval stops. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6881800600)
- Allow the original live run to finish while monitoring at five-minute intervals. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6882208583)
- Rebalance the five slow targets and run one corrected-main measurement in parallel with the original run. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6883040373)
- Schedule the three codec static declarations as fresh isolated ID cohorts and retain exact logical ownership. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6883497065)
- Disable only Azure Monitor internal Statsbeat in C5 test processes, then run one new-profile main validation. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6884210117)
- Split the two measured tails, reclaim two light repeat spans, and validate the changed layout on main. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6884676429)
- Split the remaining repeat and codec tails, reclaim two classification slots, and validate the changed layout on main. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6885742783)
- Rebalance the six remaining measured tails as one 256-job batch using the stated two-worker sizing model. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6886943197)
- Rebalance the three-job codec neighborhood and measure the new layout on main. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6887981346)
- Scope is questions 1 and 3 of the issue body. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6893688070)
- No `since` or `with-baseline`; a change to any mutation input needs a new passing run on `main`; ownership checks and their tests stay. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894078013) points 4, 5 and 7
- Size-only split for new targets (option B) replaces point 1 of the previous decision; "Evidence pack is unchanged" there refers to incremental runs only, and point 5 changes the trigger. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894120017)
- Sizing is parked; only the acceptance trigger ships. Source: [decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894368044)
- Approve revision 3 with both handoff fixes (R2 wording; `dotnet.md:55` and `factory.md:54` added to scope). Source: [approval](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/474#issuecomment-6894509069)
Open questions:
- None. Source: [plan section 10](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0e7e1276c0da24dc4ee3bfadb749741b163dc1db/items/474/plan-sizing-and-reuse.md#10-open-questions-for-hari)
Next action: No mutation run is needed for this change (plan section 9). Sizing stays parked until a change adds a new mutation target (decision 6894368044). The `stale_inputs` blocker belongs to issue 500.
