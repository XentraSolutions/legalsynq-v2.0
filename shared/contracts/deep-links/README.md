# LegalSynq Deep-Link Foundation

## Purpose

This directory defines the shared DL-001 contract for registering, looking up, validating, and generating supported LegalSynq deep-link URLs across TypeScript and .NET runtimes.

`routes.json` is the only authoritative route registry. Do not duplicate its route templates in Web, Mobile, or Backend code.

This foundation does not receive links, navigate mobile screens, authenticate users, authorize resources, look up records, send analytics, or configure native app links.

## Contract Files

| File                         | Purpose                                                                     |
| ---------------------------- | --------------------------------------------------------------------------- |
| `routes.json`                | Authoritative, environment-independent route definitions.                   |
| `route-registry.schema.json` | Language-neutral JSON Schema for the registry shape.                        |
| `route-contract.ts`          | Shared TypeScript contract and error types.                                 |
| `route-registry.ts`          | Immutable TypeScript registry loader and structural validation.             |
| `deep-link-url.ts`           | Environment, base-domain, parameter, encoding, and URL-generation behavior. |

The .NET reader is `Contracts.DeepLinks.DeepLinkRouteRegistry` in `shared/contracts/Contracts`. Its embedded resource is built from this same `routes.json` file.

## Route-Definition Fields

| Field                     | Meaning                                                                            |
| ------------------------- | ---------------------------------------------------------------------------------- |
| `key`                     | Stable camel-case lookup key.                                                      |
| `pathTemplate`            | Absolute path whose required placeholders use `:parameterName`.                    |
| `mobileDestination`       | Symbolic future mobile destination; it is not wired to navigation by DL-001.       |
| `requiresAuthentication`  | Metadata for future authentication consumers; DL-001 does not enforce it.          |
| `requiresAuthorization`   | Metadata for future authorization consumers; DL-001 does not enforce it.           |
| `requiredPathParameters`  | Exact names of all placeholders in `pathTemplate`.                                 |
| `optionalQueryParameters` | Query names the generator may accept. All initial routes intentionally allow none. |
| `fallbackDestination`     | Symbolic future fallback destination; DL-001 does not navigate to it.              |
| `analyticsEvent`          | Future event name metadata; DL-001 does not emit analytics.                        |
| `enabled`                 | Disabled routes cannot be generated.                                               |

## Supported Routes

| Key                  | Pattern                        | Required path parameters | Optional query parameters |
| -------------------- | ------------------------------ | ------------------------ | ------------------------- |
| `dashboard`          | `/dashboard`                   | None                     | None                      |
| `dealDetails`        | `/deals/:dealId`               | `dealId`                 | None                      |
| `contactDetails`     | `/contacts/:contactId`         | `contactId`              | None                      |
| `applicationDetails` | `/applications/:applicationId` | `applicationId`          | None                      |
| `reportDetails`      | `/reports/:reportId`           | `reportId`               | None                      |

All initial routes are enabled. Authentication and authorization metadata are explicitly `true`, with `dashboard` as the future fallback metadata. Mobile destinations and analytics event names are metadata only.

## Environment Configuration

Web URL generation reads:

```text
NEXT_PUBLIC_ENV
NEXT_PUBLIC_DEEP_LINK_BASE_URL
```

Supported `NEXT_PUBLIC_ENV` values are:

- `local`
- `development`
- `qa`
- `uat`
- `production`

Each deployment must supply its approved base domain through `NEXT_PUBLIC_DEEP_LINK_BASE_URL`. Approved QA, UAT, and Production domains were not present in the repository, so no deployment hostname is hard-coded. Production must use HTTPS. HTTP is accepted for non-production local workflows. Only a protocol and host are accepted; credentials, paths, query strings, and fragments are rejected.

Example local configuration:

```bash
NEXT_PUBLIC_ENV=local
NEXT_PUBLIC_DEEP_LINK_BASE_URL=http://localhost:5000
```

Public environment values are not secrets, but route parameters and examples must never contain credentials, tokens, personal information, or confidential record data.

## Web URL-Generator Usage

Use the centralized Web adapter; do not concatenate supported deep-link URLs in components:

```ts
import { generateDeepLinkUrl } from "@/lib/deep-links";

const dashboardUrl = generateDeepLinkUrl("dashboard");
const dealUrl = generateDeepLinkUrl("dealDetails", {
  pathParameters: { dealId: "example-deal-id" },
});
```

The generator:

- Rejects unknown and disabled routes.
- Rejects missing or blank required path values.
- Rejects undeclared path parameters.
- Rejects unsupported query parameters.
- Omits `undefined`, `null`, and blank approved query values.
- Encodes path values and approved query names and values.
- Sorts query parameters by name for deterministic output.
- Normalizes a trailing slash from the configured base domain.
- Does not mutate route definitions or input objects.

Validation failures throw `DeepLinkError` with a stable `code` and a descriptive message.

## Mobile and Backend Consumption

Mobile exposes the shared definitions from `apps/mobile/src/shared/deepLinks`. DL-001 does not connect those definitions to Expo Linking, operating-system events, or React Navigation.

Backend consumers that already reference `shared/contracts/Contracts` can use:

```csharp
using Contracts.DeepLinks;

var route = DeepLinkRouteRegistry.Get("dealDetails");
```

The .NET reader performs contract validation and lookup only. It adds no endpoint, authentication, authorization, resource lookup, or route execution.

## Adding a Future Route

1. Add the route once to `routes.json`.
2. Use a unique camel-case key and an absolute path template.
3. Make `requiredPathParameters` exactly match all `:placeholders`.
4. Explicitly list approved optional query names, or use an empty array.
5. Set every metadata field explicitly, including `enabled`.
6. Add URL-generation and runtime-consumption tests.
7. Update the Supported Routes table in this document.
8. Run Web, Mobile, and Contracts validation.

Do not add a second route map to an application or service.
