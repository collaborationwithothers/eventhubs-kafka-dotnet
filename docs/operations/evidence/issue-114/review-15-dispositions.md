# PR 115 comment repairs

The direct `fixcomments` instruction resumes the repairs under
[decision 6337640356](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/114#issuecomment-6337640356).
The confirmed admission projection P1 and retry-summary clarification are
addressed; approval-only findings and the namespace dispute remain disclosed.

The original [review](raw/review-15-fixes/external-review.json) and
[findings/replies](raw/review-15-fixes/review-comments.json) concern head
`534ab8f70059f37bdd027781573023d971058b0b`. Separate projected volumes could
expose a permit before matching comparison bytes. This is a source-backed
execution failure path, not a live reproduction or measured failure rate.

The producer now mounts one admission ConfigMap volume. Release patches the
permit and both exact full comparison files together, then requests annotation
refresh on the independently observed producer name and UID. Later start-line
and verify chart applications leave that object untouched. Mutation failure
stops without retry. One Runner admission deadline spans creation through
start-line and release/refresh; it is not reset. The watch and later producer
execution retain their enclosing repeat token. The worker's existing deadline,
poll/byte limits and immediate identity/hash rejection remain unchanged.

Original pinned official sources establish
[eventual projection and annotation refresh](raw/review-15-fixes/configmap-projection-source.txt),
[one volume's atomic generation switch](raw/review-15-fixes/atomic-writer-source.txt)
and [UID immutability](raw/review-15-fixes/uid-validation-source.txt).
Commands and versions are in [the producer record](review-15-producers.json).
Annotation refresh does not guarantee network timing; late release still fails
closed without another repeat.

The namespace finding remains answered `Not valid:`: Preflight awaits BindAsync,
which selects `ensureNamespace=true` and awaits explicit Namespace application.
Existing binding assertions cover that order and failure stop. Hari still
decides that dispute under factory.md "Escapes"; no namespace fix is claimed.
The `IArtifactStore` summary now distinguishes prohibited Runner reinvocation
from permitted pinned CLI SDK retries, addressing issue #116. The exact sweeper
P0 and three dependency P1 groups remain approval-only with severity preserved.

Both Release builds pass with zero warnings/errors. The latest
[build](raw/review-15-fixes/build-attempt-2.log) has a matching
[pre-build inventory](raw/review-15-fixes/repository-inputs-before-build-2.json).
The [first inventory](raw/review-15-fixes/repository-inputs-before-build-1.json)
identifies the earlier build used by the
[92 passing focused Runner cases](raw/review-15-fixes/runner-tests.xml).
Those include transport success/failure, the shared admission deadline and
existing failure-retention, namespace, deployment and budget checks. Only the
new worker test's rename operation changed between those inventories;
production source is identical.

All [23 worker admission cases](raw/review-15-fixes/admission-tests-attempt-2.xml)
pass, including actual polling across a synthetic AtomicWriter-style generation
switch and unchanged identity/hash/deadline rejection assertions. The
[first worker attempt](raw/review-15-fixes/admission-tests-attempt-1.xml)
retains a fixture failure: .NET File.Move rejects directory symlinks. The
corrected fixture uses bounded, shell-free `mv --force --no-target-directory`
for atomic rename, with assertions intact. It does not run a kubelet.

Actual Helm render/image/service-tag/TFLint acceptance remains blocked as
recorded in the README. The deployment assertion script now checks the new
volume and co-delivered bytes; it is not a new successful Helm render. Exact
repaired-head full verification belongs to the existing PR workflow, recorded
in the PR body after push.
