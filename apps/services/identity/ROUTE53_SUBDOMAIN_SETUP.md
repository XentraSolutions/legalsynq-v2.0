# Identity Route53 Subdomain Setup

This document explains how tenant subdomain provisioning works in the Identity service and how to configure AWS Route53, application settings, and verification so tenant creation succeeds.

## Scope

This setup applies to the Identity service subdomain provisioning flow used when Control Center creates a tenant.

Relevant code:

- [apps/services/identity/Identity.Infrastructure/Services/Route53DnsService.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/Services/Route53DnsService.cs:10)
- [apps/services/identity/Identity.Infrastructure/Services/TenantProvisioningService.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/Services/TenantProvisioningService.cs:37)
- [apps/services/identity/Identity.Infrastructure/Services/TenantVerificationService.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/Services/TenantVerificationService.cs:22)
- [apps/services/identity/Identity.Api/appsettings.json](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Api/appsettings.json:26)
- [apps/services/identity/Identity.Infrastructure/DependencyInjection.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/DependencyInjection.cs:57)

## How Provisioning Works

When a tenant is created:

1. The Tenant service creates the canonical tenant record.
2. The Tenant service calls the Identity internal provisioning endpoint.
3. Identity creates the tenant, org, and admin user.
4. Identity calls `Route53DnsService.CreateSubdomainAsync(subdomain)`.
5. The DNS service upserts a Route53 record for `<slug>.<base-domain>`.
6. Identity verifies the hostname:
   - DNS resolution must succeed.
   - HTTPS `GET https://<hostname>/.well-known/tenant-verify` must succeed.
   - The response body must contain `tenant-verify-ok`.
7. If verification passes, the tenant becomes `Active`.
8. If verification fails, the tenant may remain in `Verifying` and retry automatically, or end in `Failed`.

## Required Configuration

The Identity service binds these settings from the `Route53` section:

- `Route53__HostedZoneId`
- `Route53__BaseDomain`
- `Route53__RecordType`
- `Route53__RecordValue`
- `Route53__Ttl`
- `Route53__Region`
- `Route53__AccessKeyId` optional
- `Route53__SecretAccessKey` optional

The defaults in source are not production-ready:

- `HostedZoneId` is empty by default
- `RecordValue` is empty by default

See:

- [apps/services/identity/Identity.Api/appsettings.json](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Api/appsettings.json:26)

## Route53 Settings Reference

### `Route53__HostedZoneId`

The Route53 hosted zone ID for the base domain that will contain tenant subdomains.

Example:

```env
Route53__HostedZoneId=Z0123456789ABCDEFG
```

If `BaseDomain=demo.legalsynq.com`, this hosted zone must be the zone that authoritatively manages `demo.legalsynq.com`.

### `Route53__BaseDomain`

The shared parent domain for tenant hostnames.

Example:

```env
Route53__BaseDomain=demo.legalsynq.com
```

If the tenant slug is `acme`, Identity will provision:

```text
acme.demo.legalsynq.com
```

### `Route53__RecordType`

The DNS record type Identity should upsert.

Supported in practice by the current implementation:

- `A`
- `AAAA`
- `CNAME`

Example:

```env
Route53__RecordType=CNAME
```

Choose this carefully:

- Use `CNAME` if tenant subdomains should point at a shared ingress hostname such as an ALB or platform edge host.
- Use `A` only if you are pointing directly at an IP address.
- Use `AAAA` only if you are pointing directly at an IPv6 address.

### `Route53__RecordValue`

The target value written into the DNS record.

Examples:

For `CNAME`:

```env
Route53__RecordType=CNAME
Route53__RecordValue=ingress.demo.legalsynq.com
```

For `A`:

```env
Route53__RecordType=A
Route53__RecordValue=203.0.113.10
```

This field must not be empty.

### `Route53__Ttl`

DNS TTL in seconds.

Example:

```env
Route53__Ttl=300
```

### `Route53__Region`

AWS region used by the Route53 SDK client configuration.

Example:

```env
Route53__Region=us-east-2
```

Note: Route53 is a global service, but the SDK client still takes a region setting.

### `Route53__AccessKeyId` and `Route53__SecretAccessKey`

Optional explicit AWS credentials.

If these are not set, the AWS SDK falls back to the standard credential chain:

- environment variables
- EC2 instance profile
- ECS task role
- other configured AWS credential sources

Recommended for production:

- do not hardcode static access keys
- use an instance profile, task role, or workload identity with Route53 permissions

## Required AWS Permissions

The runtime identity used by the Identity service must be able to:

- list records in the target hosted zone
- change records in the target hosted zone

Minimum actions:

- `route53:ListResourceRecordSets`
- `route53:ChangeResourceRecordSets`

Recommended to scope to the specific hosted zone ARN where possible.

Example IAM policy shape:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "route53:ListResourceRecordSets",
        "route53:ChangeResourceRecordSets"
      ],
      "Resource": "arn:aws:route53:::hostedzone/Z0123456789ABCDEFG"
    }
  ]
}
```

If you need hosted-zone discovery outside the app, your operators may also need:

- `route53:ListHostedZones`

That permission is not required by the application code itself.

## Recommended Production Topology

For most deployments, use `CNAME` records to a shared ingress hostname.

Recommended:

```env
Route53__BaseDomain=demo.legalsynq.com
Route53__RecordType=CNAME
Route53__RecordValue=app-ingress.demo.legalsynq.com
Route53__HostedZoneId=Z0123456789ABCDEFG
```

This means each tenant record looks like:

```text
acme.demo.legalsynq.com CNAME app-ingress.demo.legalsynq.com
```

This is usually easier to operate than raw `A` records because the ingress target can change without rewriting every tenant record.

## Verification Configuration

Subdomain creation alone is not enough. Identity also verifies DNS and the application endpoint.

Relevant settings from `TenantVerification`:

- `TenantVerification__Enabled`
- `TenantVerification__VerificationEndpointPath`
- `TenantVerification__DnsTimeoutSeconds`
- `TenantVerification__HttpTimeoutSeconds`
- `TenantVerification__DevBypass`

And retry behavior from `VerificationRetry`:

- `VerificationRetry__MaxAttempts`
- `VerificationRetry__InitialDelaySeconds`
- `VerificationRetry__MaxDelaySeconds`
- `VerificationRetry__BackoffMultiplier`
- `VerificationRetry__MaxRetryWindowMinutes`

See:

- [apps/services/identity/Identity.Infrastructure/Services/TenantVerificationOptions.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/Services/TenantVerificationOptions.cs:3)
- [apps/services/identity/Identity.Infrastructure/Services/VerificationRetryOptions.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Infrastructure/Services/VerificationRetryOptions.cs:3)

### Verification Requirements

For a tenant hostname such as `acme.demo.legalsynq.com`, Identity expects:

1. DNS resolution for `acme.demo.legalsynq.com` to return at least one address.
2. HTTPS `GET https://acme.demo.legalsynq.com/.well-known/tenant-verify` to return a successful status.
3. The response body to contain:

```text
tenant-verify-ok
```

If DNS creation works but this endpoint is missing, tenant creation will still end up in verification failure or repeated retries.

### Development Bypass

For local development only, you can bypass verification:

```env
TenantVerification__DevBypass=true
```

Or fully disable verification:

```env
TenantVerification__Enabled=false
```

Do not use either setting in production.

## Portal Base Domain

Identity also uses `NotificationsService__PortalBaseDomain` for tenant-specific links in invitation and password-reset flows.

This does not create DNS records, but it should usually align with the DNS base domain strategy.

Example:

```env
NotificationsService__PortalBaseDomain=demo.legalsynq.com
```

If this is missing in non-development environments, startup validation can fail or email links may not be tenant-aware.

See:

- [apps/services/identity/Identity.Api/Program.cs](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/apps/services/identity/Identity.Api/Program.cs:54)

## Example Environment Configurations

### Local Development

Use bypass if you are not actually provisioning Route53:

```env
Route53__HostedZoneId=Z0123456789ABCDEFG
Route53__BaseDomain=demo.legalsynq.com
Route53__RecordType=CNAME
Route53__RecordValue=localhost
Route53__Ttl=60
Route53__Region=us-east-2

TenantVerification__DevBypass=true
NotificationsService__PortalBaseDomain=demo.legalsynq.com
```

Note: `localhost` is not a valid real production DNS target for Route53-based tenant routing. This example is only for local experimentation if the DNS path is mocked or not exercised end-to-end.

### Staging

```env
Route53__HostedZoneId=Z0STAGING123456789
Route53__BaseDomain=staging-demo.legalsynq.com
Route53__RecordType=CNAME
Route53__RecordValue=staging-ingress.legalsynq.com
Route53__Ttl=300
Route53__Region=us-east-2

TenantVerification__Enabled=true
TenantVerification__VerificationEndpointPath=/.well-known/tenant-verify
TenantVerification__DnsTimeoutSeconds=10
TenantVerification__HttpTimeoutSeconds=10
TenantVerification__DevBypass=false

VerificationRetry__MaxAttempts=5
VerificationRetry__InitialDelaySeconds=30
VerificationRetry__MaxDelaySeconds=300
VerificationRetry__BackoffMultiplier=2
VerificationRetry__MaxRetryWindowMinutes=30

NotificationsService__PortalBaseDomain=staging-demo.legalsynq.com
```

### Production

```env
Route53__HostedZoneId=Z0PROD123456789ABC
Route53__BaseDomain=demo.legalsynq.com
Route53__RecordType=CNAME
Route53__RecordValue=tenant-edge.legalsynq.com
Route53__Ttl=300
Route53__Region=us-east-2

TenantVerification__Enabled=true
TenantVerification__VerificationEndpointPath=/.well-known/tenant-verify
TenantVerification__DnsTimeoutSeconds=10
TenantVerification__HttpTimeoutSeconds=10
TenantVerification__DevBypass=false

VerificationRetry__MaxAttempts=5
VerificationRetry__InitialDelaySeconds=30
VerificationRetry__MaxDelaySeconds=300
VerificationRetry__BackoffMultiplier=2
VerificationRetry__MaxRetryWindowMinutes=30

NotificationsService__PortalBaseDomain=demo.legalsynq.com
```

## Setup Checklist

Use this sequence when setting up a new environment.

### 1. Prepare the hosted zone

- Create or identify the hosted zone for the chosen base domain.
- Confirm the zone is authoritative for the base domain.
- Record the hosted zone ID.

### 2. Choose the ingress target

- Decide whether tenant records point to:
  - a shared DNS hostname via `CNAME`
  - a static IP via `A`
  - a static IPv6 address via `AAAA`
- Set `Route53__RecordType` and `Route53__RecordValue` accordingly.

### 3. Grant AWS permissions

- Attach an IAM role or credentials to the Identity service runtime.
- Ensure the runtime can:
  - list record sets in the hosted zone
  - change record sets in the hosted zone

### 4. Configure the Identity service

- Set all required `Route53__*` values.
- Set `NotificationsService__PortalBaseDomain`.
- Set `TenantVerification__*` and `VerificationRetry__*`.

### 5. Confirm the application endpoint exists

The tenant-facing app behind the ingress must serve:

```text
/.well-known/tenant-verify
```

And the response body must include:

```text
tenant-verify-ok
```

If this path is not implemented, DNS creation may succeed but verification will fail.

### 6. Restart the Identity service

After changing environment variables or appsettings-backed configuration, restart the Identity service so options are reloaded.

### 7. Test with a new tenant

Create a test tenant in Control Center and verify:

- a Route53 record is created
- the tenant hostname resolves
- `https://<tenant-hostname>/.well-known/tenant-verify` returns success
- tenant status becomes `Active`

## Failure Modes and What They Mean

### `DNS record creation returned failure.`

Likely causes:

- `Route53__HostedZoneId` is wrong or empty
- `Route53__RecordValue` is empty or invalid
- the service has no Route53 permissions
- the hosted zone does not match `Route53__BaseDomain`

### `DNS resolution failed`

Likely causes:

- Route53 record was not actually created
- record exists in the wrong zone
- propagation delay
- ingress target is invalid

### `HTTP verification failed`

Likely causes:

- hostname resolves but the app is not serving traffic for that host
- TLS is not ready
- `/.well-known/tenant-verify` route is missing
- response body does not contain `tenant-verify-ok`

## Log Messages to Look For

In Identity logs, search for:

- `Route53 UPSERT failed for`
- `Could not check/delete conflicting records`
- `DNS verification failed for`
- `HTTP verification failed for`
- `Verification retry exhausted`

Typical AWS exception categories you may see:

- `AccessDenied`
- `NoSuchHostedZone`
- `InvalidChangeBatch`
- invalid record value format errors

## Practical Recommendations

- Prefer `CNAME` records to a shared ingress hostname unless you have a strong reason to use `A`.
- Keep `Route53__RecordValue` non-empty and environment-specific.
- Use IAM roles instead of static keys in production.
- Enable verification retries in staging and production.
- Only use `TenantVerification__DevBypass=true` for local development.
- Make sure the app behind the tenant hostname serves the verification endpoint before turning on automatic tenant provisioning.

## Minimal Working Production Example

```env
Route53__HostedZoneId=Z0PROD123456789ABC
Route53__BaseDomain=demo.legalsynq.com
Route53__RecordType=CNAME
Route53__RecordValue=tenant-edge.legalsynq.com
Route53__Ttl=300
Route53__Region=us-east-2

TenantVerification__Enabled=true
TenantVerification__VerificationEndpointPath=/.well-known/tenant-verify
TenantVerification__DnsTimeoutSeconds=10
TenantVerification__HttpTimeoutSeconds=10
TenantVerification__DevBypass=false

VerificationRetry__MaxAttempts=5
VerificationRetry__InitialDelaySeconds=30
VerificationRetry__MaxDelaySeconds=300
VerificationRetry__BackoffMultiplier=2
VerificationRetry__MaxRetryWindowMinutes=30

NotificationsService__PortalBaseDomain=demo.legalsynq.com
```

If tenant creation still fails after this setup, the next step is to inspect the Identity service logs for the exact Route53 or verification exception.
