# Terraform mock fixtures

These native Terraform tests use synthetic identifiers, addresses, hostnames, timestamps and version strings. They keep the actual pinned Azure Verified Modules and replace computed provider responses in plan mode. No Azure identity or backend connectivity is used. Root-local `tests` symlinks expose these canonical files through Terraform's required local testing-directory argument.

The [approved plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318201099) owns the exact fixture expansion, positive cases, negative input table and assertions. The test files are [persistent](../persistent/main.tftest.hcl) and [ephemeral](../ephemeral/main.tftest.hcl). Run them with the [offline commands](../../../docs/operations/terraform.md#offline-checks).

Mocks supply resource IDs, identity client/principal IDs, private IPs and ARM response objects. Configured access flags, resource bodies, role scopes, tags, retention and pool definitions remain those of the actual modules. Where a module exposes only response fields, tests pair its supported outputs with the exact named configuration local passed into the module; this is input/output contract coverage rather than inspection of hidden provider resources.

The AKS fixture supplies the actual OIDC issuer and kubelet-identity response shape and a synthetic user kubeconfig containing a cluster certificate field. It is not a usable kubeconfig. The storage endpoint read returns a synthetic private-service connection; its approved status is fixture data, not observed endpoint readiness. Remote persistent state is overridden with the actual declared root output shape; the test backend name is never contacted.

Persistent positive cases cover existing external DNS zones and optional standalone zone ownership. Ephemeral positive coverage uses the fixed A01 resource and identity contract. Negative cases change the exact variable named by each test and expect its validation failure; an unrelated provider or expression error does not count as successful rejection.

The tag map and existing external backend follow [Hari's decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/112#issuecomment-6318488448). Neither root provisions or migrates the existing state account. Live backend access, network overlap, Kubernetes GA availability, Azure authorization and service behavior remain later deployment/run evidence.
