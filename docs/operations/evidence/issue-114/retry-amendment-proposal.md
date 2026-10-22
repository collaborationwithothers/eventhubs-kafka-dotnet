# Proposed clarification of the Blob retry contract

The required Azure CLI interface inherits SDK write retries and exposes no
verified retry-disable control at the pinned source. This proposal resolves
that incompatibility while retaining the existing conditional-write protocol.
It is a proposal, not implementation authority.

The [independent investigation](blob-retry-investigation.md) traces the CLI and
SDK sources. A lost success response can be retried with the old ETag and become
a 412 conflict. That stops advancement; it does not commit the transition twice.
The current fake-process tests establish application invocation counts, not
the absence of SDK HTTP retries.

## Recommended narrow amendment

Replace the selected plan's durable-store phrase `no write retry` with:

> No Runner-initiated write retry. The required Azure CLI may retain its pinned
> SDK's internal conditional-request retries inside the existing 60-second CLI
> and 180-second store-operation deadlines. A CLI 412/conflict always stops
> advancement, including when an earlier request may have committed. Other
> uncertain CLI outcomes retain the existing three bounded readbacks with
> delays of 1 and 2 seconds. No conflicting write is merged or reinvoked.

The consequence is deliberate: a lost response can leave a committed transition
reported as a conflict, halting the phase rather than advancing. Reservations
and available evidence remain preserved. This amendment adds no identity,
credential provider, package, lease, repeat or live operation. Deployment still
requires the actual CLI version/help receipts and all existing acceptance.

After a recognized decision adopts this text, update the class summary and
operational contract to distinguish Runner calls from SDK requests. Preserve
the conflict-stop and exact-byte readback assertions and obtain the next cold
review before an ordinary push. The existing PR workflow must still supply
exact-head full verification.

## Alternative if single-attempt HTTP writes remain required

Authorize an independently checked amendment for a supported retry-configurable
Blob write transport. That changes the current explicit CLI-interface and
credential/dependency constraints and needs a concrete revised plan before
implementation. No unsupported environment variable or CLI flag is proposed.
