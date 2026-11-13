# Receipt: #452 test shards

This is the single authored receipt of the pull request for issue #452. It names the approved plan, lists the implementer's choices and the fixture changes the plan's standing fixture rule allows, and records the commands run with their results. It records results for the commits it describes, not for the commit that contains it.

## Execution card

- Deliverable: the Verifier tests in three CI jobs and the LoadGen tests in two, by test class, from `scripts/dotnet-test-shards.tsv`; a method-level completeness check in the aggregate `.NET verification` job.
- Authorization: Hari's [approval 6858183518](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858183518) of plan r4, file `items/452/plan-test-shards.md` at commit `2226c77a98bcd937989a1062cae8c7f6e551c494` (plan comment [6856499810](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6856499810), plan check [6858081815](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858081815), `Blocking findings: none`, no `apply in implementation` findings). No later decision or approval exists on the issue (read 2026-11-13). The plan file in this pull request is that commit's file, byte for byte.
- Mandatory gates and order: docs/agents/pull-requests.md "Verification gates". Step 1 is A1 and A2 below. Step 2 is the cold review. Step 3 does not apply, as plan section 8 explains: the pull request changes no `.cs`, `.csproj` or test project. Step 4 is the hosted `.NET verification` run on the final head (A6).
- Exact verification commands: plan section 8, A1 to A8.
- Earliest authorized publication checkpoint: after the cold review has no open P0 or P1.
- Review budget: docs/agents/pull-requests.md rule 8. Exhaustion action: hand off to Hari. Hosted-run cap: plan section 11 (a), three failing A6 runs caused by this pull request and two flake reruns, exhaustion action push -wip and hand off to the next session.

## Implementer choices (factory.md "Item lifecycle" step 1)

The plan check left no `apply in implementation` finding. These are choices the plan leaves to the implementer:

1. A `<test>` element without a `type` or a `method` attribute is skipped when `check-dotnet-matrix.py` reads `results.xml` (R11). The plan defines the method string only for elements that carry both. A skipped element cannot hide a listed method: the method it should have counted then has no running element, so `method-not-run` is reported. xUnit's writer puts both attributes on every `<test>`.
2. The shard-file header comment (plan section 3.2, "the implementer's") is five `#` lines that describe the format and the reason for the Verifier split.
3. `verify-dotnet.sh` matches shard-file fields with literal character sets, not ranges, so the match does not depend on the locale's collation. The checker uses `re.fullmatch`, which has no trailing-newline case.
4. R13 emits `method-in-two-shards` findings before `method-not-run` findings within a project, each in ordinal order of method. The plan gives the two rules in that order and one ordering clause for both.
5. A local real run of one shard (A8 below) uses `b10-verify-b` under `--tier sandbox`, because the plan's A8 shard needs cgroup v2, which this session lacks.

## Standing fixture rule: changes to existing tests

Under plan section 6.2. No case was removed, no assertion loosened, no check changed.

- `scripts/tests/test_check_dotnet_matrix.py`, T1 to T24, rule (a): the fixture gains `methods.json`, the `shard` key, the `test list` stage and the shard-file argument that the new checks require. `T4` expected usage line, rule (b): the usage line now reads `usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result>...`, and the case "only the directory" became "directory and shard file only". `T10` also removes `methods.json`, and `T13` adds the two `shard` cases (plan T37 covers the same key); both extend the existing test.
- `scripts/test-verify-dotnet.sh`, rule (a): the stub gains a `run` case; the `default` case gains one assertion; the `help` case gains two. The existing assertions are unchanged.
- `infra/scripts/phase/tests/test_workflow.py`, rule (a): the hash and the comment line of R17.

## Results

Filled in below as each command runs.
