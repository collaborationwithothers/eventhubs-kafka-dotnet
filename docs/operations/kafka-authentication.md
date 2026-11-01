# Load generator Kafka authentication

The load generator uses the repository's OAUTHBEARER refresh handler with an explicit workload identity credential. This is the production authentication path for the producer described by [ADR-003](../blueprint.md#adr-003-entra-only-authentication-with-aks-workload-identity-and-a-custom-oauthbearer-refresh-handler). The approved [issue #74 plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/74#issuecomment-6230988038) defines the harness decisions and rejection checks.

## Inputs and startup

Provide these nonempty environment values to the load generator:

| Environment variable | Meaning |
| --- | --- |
| `AZURE_TENANT_ID` | Entra tenant ID for the workload identity credential. |
| `AZURE_CLIENT_ID` | Application client ID for the workload identity credential. |
| `AZURE_FEDERATED_TOKEN_FILE` | Path to the projected workload identity token file. |

These names match the defaults documented in the pinned [Azure.Core 1.60.0 `WorkloadIdentityCredentialOptions`](https://github.com/Azure/azure-sdk-for-net/blob/84e76f0ebc1bb8ce92b5c28b2eddfa6016cfdc4a/sdk/core/Azure.Core/src/Identity/Credentials/WorkloadIdentityCredentialOptions.cs#L21-L43). The application client ID identifies the credential and is supplied as the Kafka OAuth principal name. It is distinct from Kafka `client.id`, which remains role-specific and identifies the native client in evidence and metric tags.

At host startup, missing or whitespace-only identity inputs fail options validation. Startup does not read the projected token file or acquire a token. Before output artifacts or a native producer are created, admission validates the resolved producer settings using the shared authentication baseline validator. It checks configuration and wiring only. The namespace is validated through the existing namespace validation, and the token scope is derived from it as `https://<namespace>.servicebus.windows.net/.default`; there is no separate scope input. Token acquisition and token validation occur only when the native client invokes the attached refresh callback.

Each native Kafka client has one `KafkaOAuthBearerSession`, created for its run and attached to its builder. The same session serves the callback and that run's executor; it cannot be reused for another native client. The SDK owns token acquisition and caching, while librdkafka schedules refresh callbacks. A successful credential call can return a cached token, so it does not prove a new Entra exchange. The pinned [Azure.Core credential implementation](https://github.com/Azure/azure-sdk-for-net/blob/84e76f0ebc1bb8ce92b5c28b2eddfa6016cfdc4a/sdk/core/Azure.Core/src/Identity/Credentials/WorkloadIdentityCredential.cs#L97-L109) delegates through [ClientAssertionCredential](https://github.com/Azure/azure-sdk-for-net/blob/84e76f0ebc1bb8ce92b5c28b2eddfa6016cfdc4a/sdk/core/Azure.Core/src/Identity/Credentials/ClientAssertionCredential.cs#L80-L122) to [MSAL acquisition](https://github.com/Azure/azure-sdk-for-net/blob/84e76f0ebc1bb8ce92b5c28b2eddfa6016cfdc4a/sdk/core/Azure.Core/src/Identity/MsalConfidentialClient.cs#L167-L192). The pinned [MSAL confidential-client contract](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/9a8a703cbf1c9ca15f20dffe6c6f3619851e8e20/src/client/Microsoft.Identity.Client/IConfidentialClientApplication.cs#L51-L61) documents token caching.

Acquisition is synchronous inside the native callback. The load generator passes the owning run's cancellation token to acquisition and uses its existing bounded cleanup and process-watchdog controls. It adds no separate acquisition timeout or retry policy. The pinned [Confluent.Kafka 2.16.0 callback contract](https://github.com/confluentinc/confluent-kafka-dotnet/blob/56ef87df3402861f0dc72ef921fcfac702ae1f16/src/Confluent.Kafka/ProducerBuilder.cs#L290-L318) and [librdkafka 2.16.0 refresh path](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_sasl_oauthbearer.c#L466-L497) are the pinned implementation references for callback and refresh behavior.

## Failure and completion behavior

The handler validates that an acquired token is nonempty and its actual expiry is later than the current time. The expiry advances only after Kafka accepts the token installation. Failures use one of three sanitized reasons: `acquisition`, `token_validation`, or `installation`. Credential exception text, token contents, credential identifiers and token-file paths are not included in failure details.

Before the first successful token installation, a failed acquisition, validation or installation is an initial failure. The failure is sticky for that session: a later successful callback cannot clear it. The executor observes it during admission and again during bounded cleanup, stops new admissions, and leaves any opportunity that did not reach `Produce` uncommitted. If cleanup and read-back complete, the worker saves an unsuccessful `failed` manifest with `bookkeeping_complete: false` and exits unsuccessfully. This is an incomplete run and cannot be published as a successful run or used for authoritative oracle totals. If cleanup itself fails or the process watchdog fires, only partial local evidence may remain. Existing delivery, capture, cancellation, reconciliation and completion failures continue to apply independently. These controls preserve [ADR-012](../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes) and [ADR-013](../blueprint.md#adr-013-a-run-is-the-window-between-high-watermarks-recorded-before-and-after-the-load-generator).

After a token has been installed successfully, a refresh acquisition, validation or installation failure is recorded and reported to Kafka but does not by itself fail an otherwise valid run while the installed token remains valid. The recorded installed expiry stays unchanged until a replacement is successfully installed. A failed refresh does not extend the expiry or guarantee authentication after expiry. librdkafka retains the current token on refresh failure and schedules another refresh according to its native behavior; see its pinned [OAuth bearer implementation](https://github.com/confluentinc/librdkafka/blob/c6c9973cf1b25394c58e0cade13a36f386856f77/src/rdkafka_sasl_oauthbearer.c#L483-L497). Existing delivery and run-completion gates remain active. Native authentication error evidence and its guard observation are retained; that observation is not itself a blanket worker-failure rule.

## Failure metric

The handler emits one counter measurement for each failed invocation, including repeated failures with the same reason. It emits no measurement on success. Emission occurs in the handler, before native failure reporting, so native error callbacks do not count the same attempt a second time.

| Instrument property | Value |
| --- | --- |
| Meter | `Parchwell.Kafka.Authentication` |
| Counter | `parchwell.kafka.oauthbearer.refresh_handler.errors` |
| Unit | `count` |
| Tags | `scenario`, `run`, `role`, `client_id`, `phase`, `reason` |

The `phase` tag is `initial` until the first successful installation and `refresh` afterward. `reason` is one of `acquisition`, `token_validation`, or `installation`. The Kafka client ID in the tag is the actual role-specific `client.id`, not the workload credential application ID. This instrument provides the FM-06 handler-failure signal; documenting the signal does not mean an alert is deployed or that Azure Monitor ingestion has been verified. See [FM-06 in the failure modes](../blueprint.md#5-failure-modes) and the approved [metric decision](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/74#issuecomment-6230988038).

## Evidence limits

Local tests with synthetic token providers establish local callback, session, metric and run-bookkeeping behavior. They do not establish Entra federation, Event Hubs token acceptance, projected-token rotation, or authoritative message outcome totals. A successful credential call may use the SDK cache and is not evidence of a fresh exchange. Oracle eligibility and outcome totals remain subject to the independent evidence and late-write requirements in [ADR-012 and ADR-013](../blueprint.md#adr-012-the-oracle-is-a-per-message-ledger-joined-into-four-outcome-classes).

## Lifecycle observation capability

Opt-in lifecycle observations separate a native refresh callback, a credential return and a successful native token installation. The independent projected-file observer records filesystem generations without reading token contents. These are diagnostic capabilities; the [lifecycle observation contract](authentication-lifecycle-observation.md) defines their bounds and the live evidence still required.

The explicitly selected handler refresh-path diagnostic adds separate synthetic service operations within the existing statistics window; its contract is in [Authentication lifecycle observations](authentication-lifecycle-observation.md). Scientific capture and judgement are unchanged.

The native client compatibility diagnostic builds unmodified native OIDC producers outside the authentication factory; its contract is in [Authentication lifecycle observations](authentication-lifecycle-observation.md).
