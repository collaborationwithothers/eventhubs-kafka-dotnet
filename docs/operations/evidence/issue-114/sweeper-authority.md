# Independent authority reconciliation: #114 sweeper identity finding

The second environment/FIC design is already expressly authorized by Hari. The current literal P0 finding nevertheless remains supportable under the unchanged review process. I do not find authority to call this a clean review, retain it under the approval-only exception, or silently substitute a different identity design. The missing authority concerns disposition of this exact review-rule conflict, not whether Hari intended the second environment.

## Read evidence and freshness

Read AGENTS.md, docs/agents/factory.md, repository repo-review and implement-plan skills, origin/main versions of the governing review rule and blueprint section 9, the current blueprint/workflow/operations statements, the entire #114 issue body and all eight complete comments (including all 64,187 characters of plan comment 6321098789 Revision 2), and relevant listed-parent bodies/decisions on #109/#14. Read-only REST requests freshly fetched each of #114/#109/#14 and all their comment pages. Their issue updated_at values and complete comment ID/updated_at sets match /workspace/scratch/issue-114-authority-during-review-10. No new or edited authority was found. #114 retains ready-for-agent. Current plan edit is 2026-10-22T04:14:53Z; its clean standing-approval check 6332608289 was created at 2026-10-22T04:20:29Z. No source edits, tests, builds, commits, pushes, posts or live operations were performed.

## What is authorized

Factory "Item lifecycle" step 3 says:

> Authority starts with the issue body. A comment changes that authority only when its heading starts "## Decision" or "## Approval" and it records Hari's decision. Apply those comments in creation order; a later one wins where they differ.

This is a recognition and precedence rule. It makes #114 comment [6332413257](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6332413257) binding issue authority rather than an informal recommendation. Its point 4 explicitly decides:

> The sweeper runs under a second GitHub environment with no required reviewers and a deployment branch policy of main only, using a second federated credential subject on the existing app registration. No new identity or role assignment. The sweeper workflow is destroy-only. Hari creates the environment and the federated credential outside the repo (ADR-010); the plan references their names.

Revision 2 faithfully turns that into `parchwell-sweeper`, its exact environment subject, no reviewers/main-only policy, the same existing app/private runner/backend, and sweep-only invocation. Its "Rule ownership and acceptance" section expressly directs replacement of the blueprint's sole-environment sentence with the current second-environment sentence and extension of the runner allowlist to scenario.yml and sweeper.yml. Check 6332608289 identifies that current revision after its edit, states "Blocking findings: none", and says it qualifies for standing approval. The plan says "Open questions for Hari: None". Thus this is an approved, narrow protected design amendment. It is wrong to describe its existence as an unapproved invention or to reopen the design question as though Hari had never answered it.

Factory also says:

> After Hari approves the plan in a comment on the issue, or the plan is approved under "Standing approval", a session implements exactly that plan, with the later decisions recognized above.

Consequently, reverting the second environment/FIC, adding required reviewers back to it, creating another app or role assignment, replacing it with a different cleanup service, or removing the sweeper are not routine authorized fixes. They contradict the selected plan/decision, and the original plan-check evidence explains why the same reviewed environment cannot provide unattended four-hour cleanup.

## What actually changes

Origin/main blueprint section 9 says:

> Terraform apply and destroy run through GitHub OIDC federation, with the federated credential trusting only the protected scenario-runner environment, which requires Hari's approval; branches and pull requests are not trusted subjects.

The proposed design adds a second trusted federation subject on the same app and removes per-run required-human-review protection for that subject. Its token represents the same app, whose existing permissions allow scenario deployment. "Destroy-only" constrains this workflow's commands; it is not a second Azure identity or a reduced Azure permission set. Main-only policy, exact approved SHA guard, specific runner-workflow allowlist, shared concurrency and guarded ephemeral destruction are meaningful limits, but they do not make the accepted federation-subject set unchanged. No new app/RBAC assignment is a true, narrower statement and does not prove unchanged identity trust scope. Public endpoint exposure and workload RBAC are not being broadened by this particular amendment.

## Why the literal P0 still applies

The baseline AGENTS.md Code Review Rules says:

> Flag as P0 any change that weakens identity scope, network exposure, or private endpoint configuration.

It says "any change", without an approved-design exception. The actual review11-infra finding expressly acknowledges that the trust expansion is intentional and authorized; it does not claim divergence from the plan. Its factual premise is therefore not disproved by the decision. The repo-review skill step 2 requires the origin/main version of a changed governing document:

> ... so a change cannot weaken the rules it is reviewed against.

AGENTS.md's opening rule distinguishes design and process:

> If this file and the blueprint disagree on design, the blueprint wins. If they disagree on process, this file wins.

The recognized decision and selected plan resolve the design conflict with the old blueprint; they do not expressly change the flag/disposition process. The same factory implementation paragraph that requires exact plan implementation also says the session "follows AGENTS.md in full, including "Before opening a pull request" rules 8 and 13". Therefore "later one wins where they differ" cannot be read as a general erasure of unchanged AGENTS review obligations by every approved implementation detail.

Rule 8 expressly enumerates approval-only cases:

> Some Code Review Rules flag work that only Hari can approve: changes to expected outcomes, verdicts or `results/`, and new projects, assembly references or NuGet packages.

Identity trust expansion is absent. Its following stop-limit exclusion is for "A P1 kept this way". Treating this P0 as an approval-only P1 would generalize the exception. Rule 9 describes `Not valid:` for a finding "wrong against its source"; Rule 10 permits that evidence-based answer. Here the exact decision proves authorization, while the review finding already accepts authorization and applies a separate literal baseline flag. Authorization alone does not make this particular factual/process finding wrong.

## Comparison, not substitute authority

Parent #109 amendment [6313841552](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/109#issuecomment-6313841552) both approves the registry exposure and expressly says:

> A Code Review Rules finding on this network exposure is answered `Not valid:` with a link to this decision.

That sentence is direct disposition authority for that registry exposure only. It demonstrates a narrow way the repository has already recorded such an exception; it is not a blanket waiver for all approved identity/network amendments, and its subject does not include the new sweeper credential. My conclusion does not depend solely on the absence of matching words in #114; it follows from the review rule, process precedence, exception enumeration and the actual finding's premise above.

The later #114 decision 6333713470 overrides the rule 8 churn breaker for the named cold-review-2 P1s and preserves other rules. It neither amends Code Review Rules nor provides disposition of this identity P0.

## Concrete disposition

1. Preserve the implementation as the already-authorized design proposal. Do not claim a new architectural choice is necessary or silently change it.
2. Preserve the cold-review finding/dispute accurately. Under rule 10, the author may record a narrowly reasoned `Not valid:` dispute citing point 4 and the approved blueprint amendment; that is an author disagreement, not established invalidity of the literal flag. Do not call it the rule 8 approval-only exception or suppress its stop-limit implications.
3. A clean disposition requires Hari to expressly resolve this exact review-rule conflict (for example, a recognized decision that this specific point-4 amendment's P0 is answered `Not valid:`, with no broader identity-scope exception), or a properly authorized governance amendment. This needs no renewed decision on the architecture itself. Factory "Escapes" already says: "An author's `Not valid:` reply makes a finding disputed, not invalid" and "A `Not valid:` reply does not clear a P0 or P1 for merge ... a disputed finding waits for Hari." The protected PR and eventual workflow/prerequisite installation remain Hari's responsibility regardless.
4. I found no already-authorized code change that simultaneously retains the exact required second-subject/no-reviewer design and makes its authentication trust scope identical to the old sole protected subject. A new required-reviewer policy or separate restricted app would require changing point 4, not merely correcting implementation.

This report is independent reconciliation evidence, not a post, new approval request, or instruction to start an additional approval flow. The implementing session must apply the already-active review/churn rules to its actual review history and preserve all other completed acceptance work.

## Follow-up: ordinary push, draft PR and CI are distinct from clearance

On the narrow publication question, yes: a reviewed ordinary push and draft PR with this evidence-backed disputed P0 are available, subject to the already-active churn override and all other unchanged pre-push requirements. My earlier conclusion about clean disposition should not be read as prohibiting this concrete review work. Nothing in this dispute requires waiting for a new architectural decision before creating the reviewable PR or obtaining existing PR CI evidence.

AGENTS.md rule 8 gives two dispositions for every non-excepted P0/P1:

> Fix every P0 and P1 the cold review reports, or answer it `Not valid:` with evidence under rule 10 ...

Rule 10 repeats the alternative, then explicitly prescribes its publication:

> A cold-review P0 or P1 answered `Not valid:` is also listed in the pull request description, so Hari sees the dispute.

Factory "Escapes" distinguishes the next boundary explicitly:

> A `Not valid:` reply does not clear a P0 or P1 for merge. The merge rule in AGENTS.md still applies, so a disputed finding waits for Hari.

Reading these together, a disputed finding may reach the PR, remain visible there, and wait for Hari at merge. Reading "The loop ends at the first round with no P0 or P1" as an absolute ban on any ordinary push containing any disclosed dispute would frustrate both the expressly allowed alternative answer and the required PR-description disclosure. It is the clean-review stopping condition, not an additional express prohibition against publishing a disputed PR for human review. The author must not say the loop achieved a clean round, lower the finding's severity, relabel it approval-only, or assert that the disagreement establishes invalidity. A draft PR can concretely carry the unresolved review record to Hari while the clean-review/merge condition remains unsatisfied.

The current finding can receive this precise, transparent author disposition: `Not valid: disputed as a required implementation change. Decision 6332413257 point 4 and approved Revision 2 explicitly require this exact second environment/FIC and protected blueprint amendment. The author retains that required design for Hari's review. The reviewer correctly identifies expanded federation trust under the literal baseline P0 rule; this response does not claim the finding has been invalidated or cleared for merge.` This is an evidence-backed dispute about required corrective action, with the reviewer's contrary rule interpretation preserved. It must appear in the self-review record and PR description alongside the original P0 and remain for Hari.

The dispute still counts for churn: rule 8 expressly says a finding counts after it is "fixed or disputed". Continue to apply the existing recognized override exactly as the implementing session has established its coverage, and list every trip passed under it; this follow-up creates no broader override. The ordinary Codex round cap, the rule 6 conditions applicable to completed Codex reviews, and all protected-path/merge restrictions remain. Where the current work changes authored files after the last reviewed diff, the required pre-push cold review still applies.

Therefore the already-authorized next concrete path is: preserve the P0 and evidence-backed dispute, complete ordinary pre-push checks and authority refresh, publish the reviewed branch/draft PR, obtain exact-head CI and the required review evidence, and leave merger/resolution to Hari. Draft PR status does not itself waive a gate; it makes the remaining decision and acceptance evidence reviewable without claiming clearance.
