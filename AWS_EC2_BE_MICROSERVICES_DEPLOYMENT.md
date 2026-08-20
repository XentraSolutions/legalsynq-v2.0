# Deploy Backend Microservices on AWS EC2

This runbook deploys the LegalSynq backend .NET microservices on a single AWS EC2 instance, with AWS RDS MySQL 8 for databases, S3 for document storage, and Nginx/TLS in front of the YARP gateway.

## 1. Target Architecture

Public traffic should enter only through Nginx and the Gateway. Downstream service ports should stay private to the EC2 instance.

```text
Internet -> Route 53 -> Nginx :443 -> Gateway :5010 -> internal services
```

Backend service ports:

```text
Gateway        5010
Identity       5001
Fund           5002
CareConnect    5003
Tenant         5005
Documents      5006
Audit          5007
Notifications  5008
Liens          5009
Comms          5011
Flow           5012
Monitoring     5015
Task           5016
Support        5017
Reports        5029
Commerce       5030
Billing        5031
Xenia          5035
```

Do not expose service ports publicly. Only expose `80`, `443`, and restricted SSH or AWS SSM access.

## 2. AWS Resources

Create these AWS resources:

- EC2 instance: Ubuntu 24.04 LTS or Amazon Linux 2023.
- Instance size: use at least `t3.large`; prefer `t3.xlarge` if all backend services and frontends run on one instance.
- RDS MySQL 8 in the same VPC.
- S3 bucket for Documents service storage.
- IAM role attached to EC2 with access to the document S3 bucket.
- Route 53 record such as `api.yourdomain.com`.

Security groups:

- EC2 inbound:
  - `22` from your IP only, or no SSH if using SSM.
  - `80` from public.
  - `443` from public.
- RDS inbound:
  - `3306` from the EC2 security group only.
- EC2 outbound:
  - Allow RDS, S3, SendGrid, Twilio, and other required external services.

## 3. Prepare EC2

Install base packages:

```bash
sudo apt update
sudo apt install -y git curl unzip nginx mysql-client jq
```

Install .NET 10:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
sudo mkdir -p /opt/dotnet
sudo ./dotnet-install.sh --channel 10.0 --install-dir /opt/dotnet
sudo ln -sf /opt/dotnet/dotnet /usr/bin/dotnet
dotnet --info
```

Create the deploy user and directories:

```bash
sudo useradd --system --create-home --shell /bin/bash legalsynq || true
sudo mkdir -p /opt/legalsynq /etc/legalsynq /var/log/legalsynq
sudo chown -R legalsynq:legalsynq /opt/legalsynq /var/log/legalsynq
```

## 4. Create RDS Databases

Connect to RDS from the EC2 instance:

```bash
mysql -h <rds-endpoint> -u <admin-user> -p
```

Create the service databases:

```sql
CREATE DATABASE identity_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE tenant_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE careconnect_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE fund_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE liens_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE docs_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE audit_event_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE notifications_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE flow_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE tasks_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE monitoring_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE reports_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE support CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE commerce_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE billing_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE synqcomm_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE xenia_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

## 5. Deploy Code

Clone the repository:

```bash
sudo -iu legalsynq
cd /opt/legalsynq
git clone <your-repo-url> app
cd app
git checkout <release-branch-or-tag>
```

Publish backend services:

```bash
dotnet publish apps/gateway/Gateway.Api/Gateway.Api.csproj -c Release -o /opt/legalsynq/publish/gateway
dotnet publish apps/services/identity/Identity.Api/Identity.Api.csproj -c Release -o /opt/legalsynq/publish/identity
dotnet publish apps/services/tenant/Tenant.Api/Tenant.Api.csproj -c Release -o /opt/legalsynq/publish/tenant
dotnet publish apps/services/careconnect/CareConnect.Api/CareConnect.Api.csproj -c Release -o /opt/legalsynq/publish/careconnect
dotnet publish apps/services/fund/Fund.Api/Fund.Api.csproj -c Release -o /opt/legalsynq/publish/fund
dotnet publish apps/services/liens/Liens.Api/Liens.Api.csproj -c Release -o /opt/legalsynq/publish/liens
dotnet publish apps/services/documents/Documents.Api/Documents.Api.csproj -c Release -o /opt/legalsynq/publish/documents
dotnet publish apps/services/audit/PlatformAuditEventService.csproj -c Release -o /opt/legalsynq/publish/audit
dotnet publish apps/services/notifications/Notifications.Api/Notifications.Api.csproj -c Release -o /opt/legalsynq/publish/notifications
dotnet publish apps/services/flow/backend/src/Flow.Api/Flow.Api.csproj -c Release -o /opt/legalsynq/publish/flow
dotnet publish apps/services/task/Task.Api/Task.Api.csproj -c Release -o /opt/legalsynq/publish/task
dotnet publish apps/services/monitoring/Monitoring.Api/Monitoring.Api.csproj -c Release -o /opt/legalsynq/publish/monitoring
dotnet publish apps/services/reports/src/Reports.Api/Reports.Api.csproj -c Release -o /opt/legalsynq/publish/reports
dotnet publish apps/services/support/Support.Api/Support.Api.csproj -c Release -o /opt/legalsynq/publish/support
dotnet publish apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj -c Release -o /opt/legalsynq/publish/commerce
dotnet publish apps/services/tenant-billing/src/Billing.Api/Billing.Api.csproj -c Release -o /opt/legalsynq/publish/billing
dotnet publish apps/services/comms/Comms.Api/Comms.Api.csproj -c Release -o /opt/legalsynq/publish/comms
dotnet publish apps/services/xenia/Xenia.Api/Xenia.Api.csproj -c Release -o /opt/legalsynq/publish/xenia
```

## 6. Create Environment Files

Use one env file per deployable service, plus one shared env file for values that are truly common across backend services.

Keep service-specific secrets out of unrelated service processes. For example, the Fund service should receive `ConnectionStrings__FundDb`, but it should not receive the Documents S3 config or Notifications SendGrid keys.

### Config coverage rules

These examples intentionally include both required production settings and option-bound defaults/feature toggles. The services may boot without every toggle listed here because `appsettings.json`, `appsettings.Production.json`, option class defaults, and direct `IConfiguration[...]` reads provide fallback values. Keep this runbook reconciled against those source files whenever service config changes.

Use ASP.NET Core environment variable syntax for nested config, for example `GovernanceFederation__Enabled=true`.

Generated from: source `appsettings.json`, `appsettings.Production.json`, option `SectionName`/`SectionKey` constants, and direct `IConfiguration[...]` reads. Do not use generated `bin/` config copies as the source of truth.

If you want to generate these files from the repository helper instead of copy/pasting each block, run `scripts/create-ec2-step6-env-files.sh` on the EC2 host after reviewing its `CHANGE_ME_...` placeholders and QA database defaults.

Create `/etc/legalsynq/shared.env`:

```bash
# required
ASPNETCORE_ENVIRONMENT=Production
Jwt__Issuer=legalsynq-identity
Jwt__Audience=legalsynq-platform
Jwt__SigningKey=<minimum-32-char-production-secret>

# shared internal trust
FLOW_SERVICE_TOKEN_SECRET=<minimum-32-char-service-token-secret>
PublicTrustBoundary__InternalRequestSecret=<secret>
```

Create service-specific files under `/etc/legalsynq`.

`/etc/legalsynq/gateway.env`:

```bash
# required shared boundary
PublicTrustBoundary__InternalRequestSecret=<secret>

# verified deep-link association artifacts
# Point this at the generated directory for the deployed environment.
# It must contain apple-app-site-association and assetlinks.json.
DeepLinks__AssociationDirectory=/opt/legalsynq/app/apps/gateway/Gateway.Api/DeepLinks/Associations/production

# reverse proxy destination overrides
ReverseProxy__Clusters__identity_cluster__Destinations__identity_primary__Address=http://127.0.0.1:5001
ReverseProxy__Clusters__fund_cluster__Destinations__fund_primary__Address=http://127.0.0.1:5002
ReverseProxy__Clusters__careconnect_cluster__Destinations__careconnect_primary__Address=http://127.0.0.1:5003
ReverseProxy__Clusters__tenant_cluster__Destinations__tenant_primary__Address=http://127.0.0.1:5005
ReverseProxy__Clusters__documents_cluster__Destinations__documents_primary__Address=http://127.0.0.1:5006
ReverseProxy__Clusters__audit_cluster__Destinations__audit_primary__Address=http://127.0.0.1:5007
ReverseProxy__Clusters__notifications_cluster__Destinations__notifications_primary__Address=http://127.0.0.1:5008
ReverseProxy__Clusters__liens_cluster__Destinations__liens_primary__Address=http://127.0.0.1:5009
ReverseProxy__Clusters__comms_cluster__Destinations__comms_primary__Address=http://127.0.0.1:5011
ReverseProxy__Clusters__flow_cluster__Destinations__flow_primary__Address=http://127.0.0.1:5012
ReverseProxy__Clusters__monitoring_cluster__Destinations__monitoring_primary__Address=http://127.0.0.1:5015
ReverseProxy__Clusters__task_cluster__Destinations__task_primary__Address=http://127.0.0.1:5016
ReverseProxy__Clusters__support_cluster__Destinations__support_primary__Address=http://127.0.0.1:5017
ReverseProxy__Clusters__reports_cluster__Destinations__reports_primary__Address=http://127.0.0.1:5029
ReverseProxy__Clusters__commerce_cluster__Destinations__commerce_primary__Address=http://127.0.0.1:5030
ReverseProxy__Clusters__billing_cluster__Destinations__billing_primary__Address=http://127.0.0.1:5031
ReverseProxy__Clusters__xenia_cluster__Destinations__xenia_primary__Address=http://127.0.0.1:5035

# legacy service clients retained for code paths that read direct clients
IdentityService__BaseUrl=http://localhost:5001
TenantService__BaseUrl=http://localhost:5005
NotificationsService__BaseUrl=http://localhost:5008
AuditClient__BaseUrl=http://localhost:5007
```

`/etc/legalsynq/identity.env`:

```bash
# required
ConnectionStrings__IdentityDb=Server=<rds>;Port=3306;Database=identity_db;User=<user>;Password=<pass>;
Features__TenantDualWriteEnabled=false

# tenant integration
TenantService__BaseUrl=http://localhost:5005
TenantService__InternalUrl=http://localhost:5005
TenantService__ProvisioningToken=<secret>
TenantService__ProvisioningSecret=<secret>
TenantService__SyncSecret=<secret>

# notifications integration
NotificationsService__BaseUrl=http://localhost:5008
NotificationsService__PortalBaseUrl=https://app.yourdomain.com
NotificationsService__PortalBaseDomain=yourdomain.com

# documents integration
DocumentsService__InternalUrl=http://localhost:5006

# commerce integration
CommerceIntegration__BaseUrl=http://localhost:5030
CommerceIntegration__HostPlatformKey=legalsynq
CommerceIntegration__InternalServiceToken=<secret>
CommerceIntegration__TimeoutSeconds=10

# audit integration
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=identity
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10

# tenant domain verification
Route53__Region=<aws-region>
Route53__HostedZoneId=<hosted-zone-id>
Route53__BaseDomain=yourdomain.com
Route53__RecordType=CNAME
Route53__RecordValue=app.yourdomain.com
Route53__Ttl=300
TenantVerification__Enabled=true
TenantVerification__ExpectedCnameTarget=app.yourdomain.com
TenantVerification__VerificationEndpointPath=/.well-known/legalsynq-tenant-verification
TenantVerification__DnsTimeoutSeconds=5
TenantVerification__HttpTimeoutSeconds=5
VerificationRetry__MaxAttempts=5
VerificationRetry__InitialDelaySeconds=30
VerificationRetry__BackoffMultiplier=2
VerificationRetry__MaxDelaySeconds=900
VerificationRetry__MaxRetryWindowMinutes=1440
```

`/etc/legalsynq/tenant.env`:

```bash
# required
ConnectionStrings__TenantDb=Server=<rds>;Port=3306;Database=tenant_db;User=<user>;Password=<pass>;

# identity integration
IdentityService__BaseUrl=http://localhost:5001
IdentityService__InternalUrl=http://localhost:5001
IdentityService__ProvisioningToken=<secret>
IdentityService__ProvisioningSecret=<secret>

# inbound provisioning auth for internal callers
TenantService__ProvisioningSecret=<secret>

# documents integration
DocumentsService__InternalUrl=http://localhost:5006

# commerce integration
CommerceIntegration__BaseUrl=http://localhost:5030
CommerceIntegration__HostPlatformKey=legalsynq
CommerceIntegration__InternalServiceToken=<secret>
CommerceIntegration__TimeoutSeconds=10

# feature toggles/read behavior
Features__TenantDualWriteEnabled=false
Features__TenantReadSource=tenant
Features__TenantBrandingReadSource=tenant
Features__TenantResolutionReadSource=tenant
Features__TenantReadCachingEnabled=true
Features__TenantReadCacheTtlSeconds=60
```

`/etc/legalsynq/careconnect.env`:

```bash
# required
ConnectionStrings__CareConnectDb=Server=<rds>;Port=3306;Database=careconnect_db;User=<user>;Password=<pass>;
AppBaseUrl=https://app.yourdomain.com
AppBaseDomain=yourdomain.com
PublicTrustBoundary__InternalRequestSecret=<secret>

# service-token validation
ServiceTokens__Issuer=legalsynq-identity
ServiceTokens__Audience=legalsynq-platform
ServiceTokens__SigningKey=<minimum-32-char-production-secret>
ServiceTokens__ServiceName=careconnect
ServiceTokens__LifetimeMinutes=10

# identity integration
IdentityService__BaseUrl=http://localhost:5001
IdentityService__ProvisioningToken=<secret>
IdentityService__TimeoutSeconds=10

# tenant integration
TenantService__BaseUrl=http://localhost:5005
TenantService__ProvisioningToken=<secret>
TenantService__TimeoutSeconds=10

# notifications/documents/audit/flow integration
NotificationsService__BaseUrl=http://localhost:5008
DocumentsService__BaseUrl=http://localhost:5006
DocumentsService__DocumentTypeId=<valid-document-type-uuid>
DocumentsService__ProductId=CareConnect
DocumentsService__ServiceToken=<secret>
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=careconnect
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10
Flow__BaseUrl=http://localhost:5012
Flow__TimeoutSeconds=10

# upload policy
AttachmentUpload__MaxFileSizeBytes=26214400
AttachmentUpload__AllowedContentTypes__0=application/pdf
AttachmentUpload__AllowedContentTypes__1=image/jpeg
AttachmentUpload__AllowedContentTypes__2=image/png
AttachmentUpload__AllowedContentTypes__3=image/gif
AttachmentUpload__AllowedContentTypes__4=image/webp
AttachmentUpload__AllowedContentTypes__5=text/plain
AttachmentUpload__AllowedContentTypes__6=text/csv
AttachmentUpload__AllowedContentTypes__7=application/msword
AttachmentUpload__AllowedContentTypes__8=application/vnd.openxmlformats-officedocument.wordprocessingml.document
AttachmentUpload__AllowedContentTypes__9=application/vnd.ms-excel
AttachmentUpload__AllowedContentTypes__10=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
AttachmentUpload__AllowedContentTypes__11=application/zip
AttachmentUpload__AllowedContentTypes__12=application/x-zip-compressed
AttachmentUpload__AllowedContentTypes__13=application/octet-stream
```

`/etc/legalsynq/fund.env`:

```bash
# required
ConnectionStrings__FundDb=Server=<rds>;Port=3306;Database=fund_db;User=<user>;Password=<pass>;

# flow integration
Flow__BaseUrl=http://localhost:5012
Flow__TimeoutSeconds=10
```

`/etc/legalsynq/liens.env`:

```bash
# required
ConnectionStrings__LiensDb=Server=<rds>;Port=3306;Database=liens_db;User=<user>;Password=<pass>;

# audit integration
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=liens
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10

# commerce integration
CommerceIntegration__BaseUrl=http://localhost:5030
CommerceIntegration__HostPlatformKey=legalsynq
CommerceIntegration__TimeoutSeconds=10

# service clients
ExternalServices__Identity__BaseUrl=http://localhost:5001
ExternalServices__Documents__BaseUrl=http://localhost:5006
ExternalServices__Audit__BaseUrl=http://localhost:5007
ExternalServices__Notifications__BaseUrl=http://localhost:5008
ExternalServices__Task__BaseUrl=http://localhost:5016
Flow__BaseUrl=http://localhost:5012
Flow__TimeoutSeconds=10
```

`/etc/legalsynq/documents.env`:

```bash
# required
ConnectionStrings__DocsDb=Server=<rds>;Port=3306;Database=docs_db;User=<user>;Password=<pass>;
Documents__MaxUploadSizeMb=100
Documents__MaxScannableFileSizeMb=100
Documents__RequireCleanScanForAccess=true
Documents__SignedUrlTtlSeconds=300

# storage
Storage__Provider=S3
Storage__Local__BasePath=/var/lib/legalsynq/documents
Storage__S3__BucketName=<bucket>
Storage__S3__Region=<region>
AWS_S3_BUCKET_NAME=<bucket>
AWS_S3_REGION=<region>
AWS_S3_ACCESS_KEY_ID=<access-key-if-not-using-instance-role>
AWS_S3_SECRET_ACCESS_KEY=<secret-key-if-not-using-instance-role>

# scanner
Scanner__Provider=ClamAv
Scanner__Mock__MockResult=Clean
Scanner__ClamAv__Host=127.0.0.1
Scanner__ClamAv__Port=3310
Scanner__ClamAv__TimeoutMs=30000
Scanner__ClamAv__ChunkSizeBytes=1048576
Scanner__ClamAv__MaxScannableFileSizeMb=100
Scanner__ClamAv__SignatureMaxAgeHours=48
Scanner__ClamAv__CircuitBreaker__FailureThreshold=5
Scanner__ClamAv__CircuitBreaker__MinimumThroughput=10
Scanner__ClamAv__CircuitBreaker__SamplingDurationSeconds=60
Scanner__ClamAv__CircuitBreaker__BreakDurationSeconds=30

# scan worker and Redis queue
ScanWorker__QueueProvider=Redis
ScanWorker__QueueCapacity=1000
ScanWorker__WorkerCount=2
ScanWorker__MaxRetryAttempts=5
ScanWorker__InitialRetryDelaySeconds=10
ScanWorker__MaxRetryDelaySeconds=300
ScanWorker__ClaimStaleJobsAfterSeconds=900
ScanWorker__ConsumerGroup=documents-scan-workers
ScanWorker__StreamKey=documents:scan-jobs
ScanWorker__StreamMaxLength=10000
Redis__Url=redis://localhost:6379
Redis__CircuitBreaker__FailureThreshold=5
Redis__CircuitBreaker__MinimumThroughput=10
Redis__CircuitBreaker__SamplingDurationSeconds=60
Redis__CircuitBreaker__BreakDurationSeconds=30

# access tokens and scan-completion notifications
AccessToken__Store=Database
AccessToken__TtlSeconds=300
AccessToken__RedirectTtlSeconds=60
AccessToken__OneTimeUse=true
Notifications__ScanCompletion__Provider=RedisStream
Notifications__ScanCompletion__Redis__Channel=documents.scan.completed
Notifications__ScanCompletion__Redis__StreamKey=documents:scan-completed
Notifications__ScanCompletion__Redis__StreamMaxLength=10000

# CORS
Cors__Origins=https://app.yourdomain.com
```

Documents malware scanning requires a reachable local `clamd` listener when `Scanner__Provider=ClamAv`. With the env block above, the Documents service will connect to `127.0.0.1:3310` for each scan job. If nothing is listening there, uploads will persist metadata and storage successfully but the scan step will fail with `Connection refused`, and access remains blocked when `Documents__RequireCleanScanForAccess=true`.

Install and configure ClamAV on the Documents host:

```bash
sudo apt-get update
sudo apt-get install -y clamav clamav-daemon
sudo systemctl stop clamav-daemon clamav-freshclam
sudo sed -i 's/^Example/#Example/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?TCPSocket.*/TCPSocket 3310/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?TCPAddr.*/TCPAddr 127.0.0.1/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?MaxScanSize.*/MaxScanSize 100M/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?MaxFileSize.*/MaxFileSize 100M/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?PCREMaxFileSize.*/PCREMaxFileSize 100M/' /etc/clamav/clamd.conf
sudo sed -i 's/^#\?StreamMaxLength.*/StreamMaxLength 100M/' /etc/clamav/clamd.conf
sudo systemctl start clamav-freshclam clamav-daemon
```

The size limits above must stay aligned with the Documents application policy. This runbook configures Documents to scan up to `100 MB`, so `clamd.conf` must not keep smaller values such as `25M` for `MaxFileSize`, `PCREMaxFileSize`, or `StreamMaxLength`, otherwise the app will accept the upload and ClamAV will reject the scan.

Validate before starting or restarting `legalsynq-documents`:

```bash
systemctl status clamav-daemon
ss -ltnp | rg 3310
nc -vz 127.0.0.1 3310
journalctl -u clamav-daemon --since today
rg -n "^(MaxScanSize|MaxFileSize|PCREMaxFileSize|StreamMaxLength)" /etc/clamav/clamd.conf
```

Expected result:

- `ss` shows `127.0.0.1:3310` listening
- `nc` reports a successful connection
- `journalctl` shows `clamd` started cleanly and loaded signatures

Temporary QA-only fallback when real ClamAV is not yet available:

```bash
Scanner__Provider=Mock
Scanner__Mock__MockResult=Clean
```

Do not use the mock scanner in environments that require real malware scanning.

`/etc/legalsynq/audit.env`:

```bash
# required
ConnectionStrings__AuditEventDb=Server=<rds>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;
Database__Provider=MySql
Database__ServerVersion=8.0.0
Database__ConnectionTimeoutSeconds=30
Database__CommandTimeoutSeconds=30
Database__MinPoolSize=0
Database__MaxPoolSize=100
Database__VerifyConnectionOnStartup=true
Database__StartupProbeTimeoutSeconds=30
AuditService__ServiceName=audit
AuditService__Version=1.0.0

# ingest/query auth
IngestAuth__Mode=ServiceToken
IngestAuth__RequireSourceSystemHeader=false
IngestAuth__ServiceTokens__0__Token=<secret>
IngestAuth__ServiceTokens__0__ServiceName=identity
IngestAuth__ServiceTokens__0__Enabled=true
IngestAuth__ServiceTokens__1__Token=<secret>
IngestAuth__ServiceTokens__1__ServiceName=careconnect
IngestAuth__ServiceTokens__1__Enabled=true
IngestAuth__ServiceTokens__2__Token=<secret>
IngestAuth__ServiceTokens__2__ServiceName=liens
IngestAuth__ServiceTokens__2__Enabled=true
IngestAuth__ServiceTokens__3__Token=<secret>
IngestAuth__ServiceTokens__3__ServiceName=notifications
IngestAuth__ServiceTokens__3__Enabled=true
IngestAuth__ServiceTokens__4__Token=<secret>
IngestAuth__ServiceTokens__4__ServiceName=task
IngestAuth__ServiceTokens__4__Enabled=true
IngestAuth__ServiceTokens__5__Token=<secret>
IngestAuth__ServiceTokens__5__ServiceName=reports
IngestAuth__ServiceTokens__5__Enabled=true
IngestAuth__ServiceTokens__6__Token=<secret>
IngestAuth__ServiceTokens__6__ServiceName=support
IngestAuth__ServiceTokens__6__Enabled=true
IngestAuth__ServiceTokens__7__Token=<secret>
IngestAuth__ServiceTokens__7__ServiceName=comms
IngestAuth__ServiceTokens__7__Enabled=true
QueryAuth__Mode=Jwt
QueryAuth__EnforceTenantScope=true
QueryAuth__MaxPageSize=500
QueryAuth__TenantIdClaimType=tenant_id
QueryAuth__OrganizationIdClaimType=organization_id
QueryAuth__UserIdClaimType=sub
QueryAuth__RoleClaimType=role
QueryAuth__PlatformAdminRoles__0=platform_admin
QueryAuth__PlatformAdminRoles__1=super_admin
QueryAuth__OrganizationAdminRoles__0=org_admin
QueryAuth__OrganizationAdminRoles__1=organization_admin
QueryAuth__TenantAdminRoles__0=tenant_admin
QueryAuth__TenantAdminRoles__1=admin
QueryAuth__TenantAdminRoles__2=owner
QueryAuth__TenantUserRoles__0=tenant_user
QueryAuth__TenantUserRoles__1=user
QueryAuth__TenantUserRoles__2=member
QueryAuth__UserSelfRoles__0=user
QueryAuth__RestrictedRoles__0=viewer
QueryAuth__RestrictedRoles__1=readonly

# integrity and retention
Integrity__Algorithm=HMAC-SHA256
Integrity__HmacKeyBase64=<base64-hmac-key>
Integrity__FlagTamperedRecords=true
Retention__DefaultRetentionDays=2555
Retention__HotRetentionDays=90
Retention__JobCronUtc="0 3 * * *"
Retention__MaxDeletesPerRun=1000
Retention__DryRun=true

# export, archival, forwarding
Export__Provider=None
Export__SupportedFormats__0=Json
Export__SupportedFormats__1=Csv
Export__SupportedFormats__2=Ndjson
Export__MaxRecordsPerFile=100000
Export__FileNamePrefix=audit-export
Archival__Strategy=None
Archival__BatchSize=1000
Archival__LocalOutputPath=/var/lib/legalsynq/audit/archive
Archival__FileNamePrefix=audit-archive
EventForwarding__BrokerType=None
EventForwarding__MinSeverity=Information
EventForwarding__SubjectPrefix=legalsynq.audit
```

`/etc/legalsynq/notifications.env`:

```bash
# required database
ConnectionStrings__NotificationsDb=Server=<rds>;Port=3306;Database=notifications_db;User=<user>;Password=<pass>;
NOTIF_DB_HOST=<rds>
NOTIF_DB_PORT=3306
NOTIF_DB_NAME=notifications_db
NOTIF_DB_USER=<user>
NOTIF_DB_PASSWORD=<pass>

# portal and providers
NotificationsService__PortalBaseUrl=https://app.yourdomain.com
NotificationsService__PortalBaseDomain=yourdomain.com
SENDGRID_API_KEY=<key>
SENDGRID_FROM_EMAIL=<email>
SENDGRID_FROM_NAME=LegalSynq
SENDGRID_WEBHOOK_VERIFICATION_ENABLED=true
SENDGRID_WEBHOOK_PUBLIC_KEY=<sendgrid-webhook-public-key>
TWILIO_ACCOUNT_SID=<twilio-account-sid>
TWILIO_AUTH_TOKEN=<twilio-auth-token>
TWILIO_FROM_NUMBER=<twilio-from-number>
TWILIO_WEBHOOK_VERIFICATION_ENABLED=true

# audit and identity integration
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=notifications
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10
IdentityService__BaseUrl=http://localhost:5001
IdentityService__TimeoutSeconds=10

# SMS cost/routing/provider quality
SmsRouting__Enabled=true
SmsCostAnalytics__Enabled=true
SmsCostAnalytics__DefaultCurrency=USD
SmsCostAnalytics__TwilioEstimatedOutboundSmsCost=0.0075
SmsCostAnalytics__RetryCostPolicy=count_retry_attempts
SmsCostAnalytics__FailedMessageCostPolicy=count_provider_accepted
SmsCostAnalytics__ProviderEstimates__twilio=0.0075
SmsCostAnalytics__ProviderEstimates__telnyx=0.004
SmsCostAnalytics__ProviderEstimates__vonage=0.006
SmsProviderQuality__Enabled=false
SmsProviderQuality__SnapshotWindowMinutes=1440
SmsProviderQuality__CalculationIntervalMinutes=60
SmsProviderQuality__MinimumAttemptCount=20
SmsProviderQuality__DeliverySuccessWeight=0.45
SmsProviderQuality__FailurePenaltyWeight=0.25
SmsProviderQuality__RetryPenaltyWeight=0.10
SmsProviderQuality__ReconciliationPenaltyWeight=0.10
SmsProviderQuality__HealthPenaltyWeight=0.10
SmsProviderQuality__DefaultQualityScore=50
SmsProviderQuality__InsufficientDataScore=50

# recipient intelligence
SmsRecipientIntelligence__RecipientHashSalt=<production-salt>
SmsRecipientIntelligence__ReputationWindowDays=30
SmsRecipientIntelligence__CalculationIntervalMinutes=60
SmsRecipientIntelligence__MaxSnapshotsPerCycle=1000
SmsRecipientIntelligence__MinimumAttemptCount=3
SmsRecipientIntelligence__MaxAttemptsPerWindow=100
SmsRecipientIntelligence__WarnSuppressionThreshold=0.15
SmsRecipientIntelligence__SoftSuppressionThreshold=0.30
SmsRecipientIntelligence__HardSuppressionThreshold=0.50
SmsRecipientIntelligence__InvalidNumberReviewThreshold=0.20

# governance core
SmsGovernance__Enabled=true
SmsGovernance__DecisionAuditEnabled=true
SmsGovernance__FailOpenOnEvaluationError=true
SmsGovernance__MaxPolicyEvaluationMs=200
SmsGovernance__RateLimitWindowMinutes=60
SmsGovernance__DefaultTimezone=UTC
SmsTemplateGovernance__Enabled=true
SmsTemplateGovernance__RequireApprovedTemplates=true
SmsTemplateGovernance__FailOpenOnEvaluationError=true
SmsTemplateGovernance__MaxTemplateLength=1600
SmsTemplateGovernance__MaxVariableCount=50
SmsTemplateGovernance__AllowInlineUntemplatedMessages=false
SmsTemplateGovernance__RestrictedCategories__0=legal_advice
SmsTemplateGovernance__RestrictedCategories__1=regulated_content
SmsGovernanceDynamic__Enabled=true
SmsGovernanceDynamic__AllowRegexRules=true
SmsGovernanceDynamic__FailOpenOnEvaluationError=true
SmsGovernanceDynamic__MaxPatternLength=1000
SmsGovernanceDynamic__MaxRulesPerEvaluation=100
SmsGovernanceDynamic__RegexTimeoutMs=200
SmsGovernanceVersioning__Enabled=true
SmsGovernanceVersioning__IncludeRulesInPackSnapshot=true
SmsGovernanceVersioning__MaxSnapshotJsonBytes=65536
SmsGovernanceAnalytics__Enabled=true
SmsGovernanceAnalytics__WindowDays=30
SmsGovernanceAnalytics__MaxResultRows=500
# Values < 1 are treated as ratios; values >= 1 are treated as absolute counts.
SmsGovernanceAnalytics__FalsePositiveWarnThreshold=0.05
# Values > 1 are treated as sim/live ratio thresholds; values <= 1 are treated as live-share thresholds.
SmsGovernanceAnalytics__FalsePositiveLiveToSimRatio=2

# release, rollout, tenant scoping, federation, runtime
SmsGovernanceReleaseManagement__Enabled=true
SmsGovernanceReleaseManagement__RequireApprovalForActivation=true
SmsGovernanceReleaseManagement__EnforceApprovalRoles=true
SmsGovernanceReleaseManagement__AllowPlatformAdminApprovalFallback=false
SmsGovernanceReleaseManagement__AllowImmediateActivation=true
SmsGovernanceReleaseManagement__AllowScheduledActivation=true
SmsGovernanceReleaseManagement__FailOpenOnReleaseEvaluationError=true
SmsGovernanceReleaseManagement__MaxReleaseItems=100
SmsGovernanceReleaseManagement__MaxScheduledReleasesPerCycle=25
SmsGovernanceReleaseManagement__ScheduledActivationPollMinutes=5
SmsGovernanceReleaseManagement__ActivationRetryLimit=3
SmsGovernanceReleaseManagement__ActivationRetryBackoffMinutes=5
SmsGovernanceReleaseManagement__ActivationLockTimeoutMinutes=15
SmsGovernanceReleaseManagement__DefaultApprovalStagesJson=[]
SmsGovernanceRollouts__Enabled=true
SmsGovernanceRollouts__DefaultCanaryPercentage=10
SmsGovernanceRollouts__DefaultStageDurationMinutes=60
SmsGovernanceRollouts__RolloutPollMinutes=5
SmsGovernanceRollouts__MaxRolloutsPerCycle=25
SmsGovernanceRollouts__AutoPauseOnThresholdBreach=true
SmsGovernanceRollouts__FailOpenOnRolloutEvaluationError=true
SmsGovernanceTenantScoping__Enabled=true
SmsGovernanceTenantScoping__ResolutionMode=tenant_inherited
SmsGovernanceTenantScoping__FailOpenOnResolutionError=true
SmsGovernanceTenantScoping__EnableTenantOverlays=true
SmsGovernanceTenantScoping__EnableRolloutAssignments=true
SmsGovernanceTenantScoping__MaxAssignmentsPerTenant=20
SmsGovernanceTenantScoping__MaxOverlaysPerTenant=50
SmsGovernanceTenantScoping__CacheResolvedRules=false
SmsGovernanceTenantScoping__ResolutionCacheSeconds=60
GovernanceFederation__Enabled=true
GovernanceFederation__DefaultScopeMode=isolated_channel
GovernanceFederation__FailOpenOnFederationError=true
GovernanceFederation__EnableCrossChannelOverlays=true
GovernanceFederation__EnableFederatedRollouts=true
GovernanceFederation__MaxFederatedPacksPerChannel=100
GovernanceFederation__MaxFederationOverlaysPerChannel=100
GovernanceFederation__CacheTopology=false
GovernanceFederation__TopologyCacheSeconds=60
GovernanceExecutionRuntime__Enabled=true
GovernanceExecutionRuntime__FailOpenOnRuntimeError=true
GovernanceExecutionRuntime__EnableEmailEnforcement=true
GovernanceExecutionRuntime__EnablePushEnforcement=true
GovernanceExecutionRuntime__EnableWebhookEnforcement=true
GovernanceExecutionRuntime__EnableSmsCompatibilityRuntime=false
GovernanceExecutionRuntime__PersistAllowDecisions=false
GovernanceExecutionRuntime__MaxEvaluationTextLength=5000
GovernanceExecutionRuntime__RegexTimeoutMs=200
```

`/etc/legalsynq/flow.env`:

```bash
# required
ConnectionStrings__FlowDb=Server=<rds>;Port=3306;Database=flow_db;User=<user>;Password=<pass>;

# integration
Audit__BaseUrl=http://localhost:5007
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
Notifications__BaseUrl=http://localhost:5008
NotificationsService__BaseUrl=http://localhost:5008

# workers/runtime
Outbox__Enabled=true
Outbox__PollingIntervalSeconds=5
Outbox__BatchSize=50
Outbox__MaxAttempts=5
Outbox__BaseBackoffSeconds=5
Outbox__BackoffMultiplier=2
WorkflowSla__Enabled=true
WorkflowSla__PollingIntervalSeconds=60
WorkflowSla__BatchSize=100
WorkflowSla__DueSoonThresholdMinutes=60
WorkflowSla__EscalationThresholdMinutes=0
WorkDistribution__EnableRecommendation=true
WorkDistribution__EnableAutoAssignment=true
WorkDistribution__SoftCapacityThreshold=15
WorkDistribution__MaxActiveTasksPerUser=20
WorkDistribution__MaxDerivedCandidates=50

# CORS
Cors__AllowedOrigins__0=https://app.yourdomain.com
```

`/etc/legalsynq/task.env`:

```bash
# required
ConnectionStrings__TasksDb=Server=<rds>;Port=3306;Database=tasks_db;User=<user>;Password=<pass>;
Service__Name=task

# integrations
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__TimeoutSeconds=10
NotificationsService__BaseUrl=http://localhost:5008
NotificationsService__TimeoutSeconds=10
MonitoringService__BaseUrl=http://localhost:5015
MonitoringService__TimeoutSeconds=10
TASK_SERVICE_URL=http://localhost:5016
```

`/etc/legalsynq/monitoring.env`:

```bash
# required
ConnectionStrings__MonitoringDb=Server=<rds>;Port=3306;Database=monitoring_db;User=<user>;Password=<pass>;
Service__Name=monitoring

# scheduler/runtime
Monitoring__Scheduler__Enabled=true
Monitoring__Scheduler__IntervalSeconds=30
Monitoring__UptimeAggregation__Enabled=true
Monitoring__UptimeAggregation__IntervalSeconds=300
Monitoring__UptimeAggregation__LookbackDays=30
Monitoring__HttpCheck__TimeoutSeconds=10
Monitoring__HttpCheck__AllowInternalTargets=false
```

`/etc/legalsynq/reports.env`:

```bash
# required
ConnectionStrings__ReportsDb=Server=<rds>;Port=3306;Database=reports_db;User=<user>;Password=<pass>;
ConnectionStrings__LiensDb=Server=<rds>;Port=3306;Database=liens_db;User=<user>;Password=<pass>;
ReportsService__ServiceName=reports
ReportsService__LogLevel=Information

# adapters and audit
Adapters__IdentityBaseUrl=http://localhost:5001
Adapters__TenantBaseUrl=http://localhost:5005
Adapters__DocumentBaseUrl=http://localhost:5006
Adapters__AuditBaseUrl=http://localhost:5007
Adapters__NotificationBaseUrl=http://localhost:5008
Adapters__ProductDataBaseUrl=http://localhost:5009
Adapters__EntitlementBaseUrl=http://localhost:5030
AuditService__BaseUrl=http://localhost:5007
AuditService__ServiceToken=<secret>
AuditService__TimeoutSeconds=10

# email/SFTP delivery
EmailDelivery__NotificationsBaseUrl=http://localhost:5008
EmailDelivery__ServiceToken=<secret>
EmailDelivery__TimeoutSeconds=10
EmailDelivery__MaxRetries=3
SftpDelivery__Host=<sftp-host>
SftpDelivery__Port=22
SftpDelivery__Username=<sftp-user>
SftpDelivery__Password=<sftp-password-if-used>
SftpDelivery__PrivateKeyPath=<private-key-path-if-used>
SftpDelivery__PrivateKeyPassphrase=<private-key-passphrase-if-used>
SftpDelivery__RemotePath=/incoming
SftpDelivery__TimeoutSeconds=30
SftpDelivery__MaxRetries=3

# storage/data tuning
Storage__Provider=S3
Storage__BucketName=<bucket>
Storage__Region=<region>
Storage__AccessKeyId=<access-key-if-not-using-instance-role>
Storage__SecretAccessKey=<secret-key-if-not-using-instance-role>
Storage__BasePath=reports
LiensData__MaxRows=50000
LiensData__QueryTimeoutSeconds=30
MySql__ConnectionString=Server=<rds>;Port=3306;Database=reports_db;User=<user>;Password=<pass>;
MySql__CommandTimeout=30
MySql__MaxRetryCount=3
```

`/etc/legalsynq/support.env`:

```bash
# required
ConnectionStrings__Support=Server=<rds>;Port=3306;Database=support;User=<user>;Password=<pass>;
ConnectionStrings__IdentityDb=Server=<rds>;Port=3306;Database=identity_db;User=<user>;Password=<pass>;
ConnectionStrings__TenantDb=Server=<rds>;Port=3306;Database=tenant_db;User=<user>;Password=<pass>;

# service-token validation
ServiceTokens__Issuer=legalsynq-identity
ServiceTokens__Audience=legalsynq-platform
ServiceTokens__SigningKey=<minimum-32-char-production-secret>
ServiceTokens__ServiceName=support
ServiceTokens__LifetimeMinutes=10

# notifications/audit/file storage
Support__Notifications__Mode=Http
Support__Notifications__TimeoutSeconds=10
Support__Audit__Mode=Http
Support__Audit__TimeoutSeconds=10
Support__FileStorage__Mode=DocumentsService
Support__FileStorage__LocalRootPath=/var/lib/legalsynq/support
Support__FileStorage__MaxFileSizeMb=25
Support__FileStorage__DocumentsService__UploadPath=/v1/documents
Support__FileStorage__DocumentsService__TimeoutSeconds=30
Support__FileStorage__AllowedContentTypes__0=application/pdf
Support__FileStorage__AllowedContentTypes__1=image/jpeg
Support__FileStorage__AllowedContentTypes__2=image/png
Support__FileStorage__AllowedContentTypes__3=text/plain
Support__FileStorage__AllowedContentTypes__4=text/csv
Support__FileStorage__AllowedContentTypes__5=application/msword
Support__FileStorage__AllowedContentTypes__6=application/vnd.openxmlformats-officedocument.wordprocessingml.document
Support__FileStorage__AllowedContentTypes__7=application/vnd.ms-excel
Support__FileStorage__AllowedContentTypes__8=application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Support__FileStorage__AllowedContentTypes__9=application/zip
Support__FileStorage__AllowedContentTypes__10=application/x-zip-compressed
Support__FileStorage__AllowedContentTypes__11=application/octet-stream

# rate limits and outbound clients
Support__RateLimit__CustomerPermitLimit=60
Support__RateLimit__CustomerWindowSeconds=60
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=support
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10
```

`/etc/legalsynq/commerce.env`:

```bash
# required
Database__ConnectionString=Server=<rds>;Port=3306;Database=commerce_db;User=<user>;Password=<pass>;
Database__Provider=MySql
COMMERCE_RUN_MIGRATIONS=false
Commerce__ServiceName=commerce
Commerce__Version=1.0.0

# LegalSynq host identity
LegalSynq__Identity__Enabled=true
LegalSynq__Identity__Issuer=legalsynq-identity
LegalSynq__Identity__Audience=legalsynq-platform
LegalSynq__Identity__SigningKey=<minimum-32-char-production-secret>
LegalSynq__Identity__HostPlatformKey=legalsynq

# payment providers
PaymentProviders__Stripe__PublishableKey=<stripe-publishable-key>
PaymentProviders__Stripe__Enabled=false
PaymentProviders__Stripe__SecretKey=<stripe-secret-key>
PaymentProviders__Stripe__WebhookSecret=<stripe-webhook-secret>
PaymentProviders__Stripe__ApiBaseUrl=https://api.stripe.com
PaymentProviders__Stripe__DefaultSuccessUrl=https://app.yourdomain.com/billing/success
PaymentProviders__Stripe__DefaultCancelUrl=https://app.yourdomain.com/billing/cancel
PaymentProviders__Stripe__SignatureToleranceSeconds=300

# tenant billing integration
Commerce__TenantBilling__BaseUrl=http://localhost:5031
Commerce__TenantBilling__InternalToken=<secret>
Commerce__TenantBilling__Enabled=false
Commerce__TenantBilling__TimeoutSeconds=10
Commerce__TenantBilling__RetryAttempts=3
Commerce__TenantBilling__RetryDelayMilliseconds=250
Commerce__TenantBilling__CircuitBreakerEnabled=false
Commerce__TenantBilling__CircuitBreakerFailures=5
Commerce__TenantBilling__CircuitBreakerDurationSeconds=30
Commerce__TenantBilling__AutoPublishEnabled=false
Commerce__TenantBilling__AutoPublishQueueCapacity=1000
Commerce__TenantBilling__OutboxEnabled=false
Commerce__TenantBilling__OutboxBatchSize=50
Commerce__TenantBilling__OutboxPollSeconds=10
Commerce__TenantBilling__OutboxMaxAttempts=5
Commerce__TenantBilling__OutboxRetryBaseDelaySeconds=30

# observability/resilience
Observability__ServiceName=commerce
Observability__Otlp__Enabled=false
Observability__Otlp__Endpoint=http://localhost:4317
Resilience__Http__RetryCount=3
Resilience__Http__CircuitBreaker__BreakDurationSeconds=30
Resilience__Http__CircuitBreaker__FailureRatio=0.5
Resilience__Http__CircuitBreaker__MinimumThroughput=10
```

Commerce migration behavior:

- In `Development`, Commerce auto-runs EF migrations on startup.
- In `Production`, Commerce skips migrations unless `COMMERCE_RUN_MIGRATIONS=true`.
- For first deploy or any schema-changing release, either:
  - run `dotnet ef database update --project apps/services/commerce/src/Commerce.Infrastructure/Commerce.Infrastructure.csproj --startup-project apps/services/commerce/src/Commerce.Api/Commerce.Api.csproj` from the checked-out repo on the EC2 host with `Database__ConnectionString` exported, or
  - set `COMMERCE_RUN_MIGRATIONS=true` in `/etc/legalsynq/commerce.env`, restart `legalsynq-commerce` once, confirm the migration log lines, then set it back to `false`.
- Expected boot logs are explicit: Commerce logs either `running EF Core migrations` or `skipping EF Core migrations`. If neither appears, the deployed binary is not current.

`/etc/legalsynq/billing.env`:

```bash
# required
BILLING_DB_CONNECTION=Server=<rds>;Port=3306;Database=billing_db;User=<user>;Password=<pass>;
ConnectionStrings__DefaultConnection=Server=<rds>;Port=3306;Database=billing_db;User=<user>;Password=<pass>;

# LegalSynq host identity and tenant context
LegalSynq__Identity__Enabled=true
LegalSynq__Identity__Issuer=legalsynq-identity
LegalSynq__Identity__Audience=legalsynq-platform
LegalSynq__Identity__SigningKey=<minimum-32-char-production-secret>
LegalSynq__TenantContext__PreferJwtTenant=true
LegalSynq__TenantContext__AllowHeaderFallback=true
LegalSynq__TenantContext__AllowInternalTokenFallback=true

# invoice lifecycle
InvoiceLifecycle__OverdueJobIntervalMinutes=60
InvoiceLifecycle__OverdueBatchSize=100

# delivery and retries
Billing__Delivery__Provider=Ncm
Billing__Delivery__Ncm__BaseUrl=<ncm-base-url>
Billing__Delivery__Ncm__ApiKey=<ncm-api-key>
Billing__Delivery__Ncm__FromEmail=billing@yourdomain.com
Billing__Delivery__Ncm__FromName="LegalSynq Billing"
Billing__Delivery__Ncm__TemplateCode=<template-code>
Billing__Delivery__Ncm__TimeoutSeconds=30
Billing__Delivery__Retry__MaxAttempts=3
Billing__Delivery__Retry__CooldownSeconds=300
Billing__Delivery__Retry__ProviderHealth__WindowSeconds=900
Billing__Delivery__Retry__ProviderHealth__DegradedAfterFailures=3
Billing__Delivery__Retry__ProviderHealth__UnavailableAfterFailures=5

# entitlement enforcement
Billing__EntitlementEnforcement__UnknownMode=ReadOnly
Billing__EntitlementEnforcement__GraceLimitedMode=Limited
Billing__EntitlementEnforcement__AllowPaymentsInReadOnly=false
Billing__EntitlementEnforcement__AllowStatementsInReadOnly=true

# QuickBooks ERP integration
Billing__Erp__QuickBooks__Environment=Production
Billing__Erp__QuickBooks__ClientId=<quickbooks-client-id>
Billing__Erp__QuickBooks__ClientSecret=<quickbooks-client-secret>
Billing__Erp__QuickBooks__RefreshToken=<quickbooks-refresh-token>
Billing__Erp__QuickBooks__RealmId=<quickbooks-realm-id>
Billing__Erp__QuickBooks__MinorVersion=75
Billing__Erp__QuickBooks__TimeoutSeconds=30
Billing__Erp__QuickBooks__ExportMode=Disabled
Billing__Erp__QuickBooks__FallbackCustomerRef=<fallback-customer-ref>
Billing__Erp__QuickBooks__AccountsReceivableRef=<accounts-receivable-ref>
Billing__Erp__QuickBooks__IncomeAccountRef=<income-account-ref>
Billing__Erp__QuickBooks__AdjustmentAccountRef=<adjustment-account-ref>
Billing__Erp__QuickBooks__UndepositedFundsRef=<undeposited-funds-ref>
```

`/etc/legalsynq/comms.env`:

```bash
# required
ConnectionStrings__SynqCommDb=Server=<rds>;Port=3306;Database=synqcomm_db;User=<user>;Password=<pass>;

# service clients
Services__NotificationsUrl=http://localhost:5008
Services__DocumentsUrl=http://localhost:5006

# audit integration
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
AuditClient__SourceService=comms
AuditClient__SourceSystem=legalsynq
AuditClient__TimeoutSeconds=10
```

`/etc/legalsynq/xenia.env`:

```bash
# required
ConnectionStrings__XeniaDb=Server=<rds>;Port=3306;Database=xenia_db;User=<user>;Password=<pass>;
Xenia__SkipDatabaseStartup=false

# cursor protection
XeniaCursorProtection__Key=<64-hex-char-key>

# assistant runtime
XeniaAssistant__Provider=OpenAI
XeniaAssistant__ModelKey=<openai-model-key>
XeniaAssistant__OpenAI__BaseUrl=https://api.openai.com
XeniaAssistant__OpenAI__ApiKey=<openai-api-key>
XeniaAssistant__OpenAI__TimeoutSeconds=60
XeniaAssistant__OpenAI__ReasoningEffort=medium
XeniaAssistant__OpenAI__TextVerbosity=medium
XeniaAssistant__OpenAI__MaxOutputTokens=4096
XeniaAssistant__CareConnect__BaseUrl=http://localhost:5003
XeniaAssistant__CareConnect__TimeoutSeconds=20
XeniaAssistant__CareConnect__MaxHistoryItems=5

# email ingestion and automation workers
XeniaIngestion__IngestionEnabled=true
XeniaIngestion__WorkerEnabled=true
XeniaAutomation__SchedulingEnabled=false
```

Xenia can boot in a degraded no-database mode when `ConnectionStrings__XeniaDb` is missing or still uses the placeholder value from `appsettings`, but production deploys should point it at a real MySQL 8 database and keep `Xenia__SkipDatabaseStartup=false` so migrations, seeders, and workers run normally.

Only include a key in `shared.env` when every service that loads it legitimately needs the value. Prefer moving ambiguous values into the specific service env file.

For provisioning keys, `ProvisioningToken` is the outbound value sent in the `X-Provisioning-Token` header and `ProvisioningSecret` is the receiver-side expected value. The paired values must match for the relevant service call path. Keep both names where this runbook lists both because the current services read both naming conventions.

Secure the files:

```bash
sudo chown root:legalsynq /etc/legalsynq/*.env
sudo chmod 640 /etc/legalsynq/*.env
```

Avoid the old single-file layout:

```text
/etc/legalsynq/backend.env
```

That file gives every backend process every secret. The per-service layout keeps runtime config isolated and makes rotation safer.

## 7. Create systemd Services

Create every backend service unit from the same hardened template. Each unit:

- runs as the unprivileged `legalsynq` user
- loads `shared.env` plus exactly one service-specific env file
- binds only to loopback through `ASPNETCORE_URLS`
- restarts automatically after process failure
- keeps logs in journald under the service-specific `SyslogIdentifier`

Create a small helper while logged in as an admin user:

```bash
create_legalsynq_service() {
  local name="$1"
  local description="$2"
  local publish_dir="$3"
  local dll="$4"
  local port="$5"
  local env_file="$6"
  local after_units="${7:-network-online.target}"
  local wants_units="${8:-network-online.target}"

  sudo tee "/etc/systemd/system/legalsynq-${name}.service" >/dev/null <<EOF
[Unit]
Description=${description}
After=${after_units}
Wants=${wants_units}

[Service]
Type=simple
User=legalsynq
Group=legalsynq
WorkingDirectory=/opt/legalsynq/publish/${publish_dir}
EnvironmentFile=/etc/legalsynq/shared.env
EnvironmentFile=/etc/legalsynq/${env_file}
Environment=ASPNETCORE_URLS=http://127.0.0.1:${port}
ExecStart=/usr/bin/dotnet /opt/legalsynq/publish/${publish_dir}/${dll}
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=legalsynq-${name}
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full
ProtectHome=true
ReadWritePaths=/var/log/legalsynq /tmp

[Install]
WantedBy=multi-user.target
EOF
}
```

Create the service files:

```bash
create_legalsynq_service gateway \
  "LegalSynq Gateway API" \
  gateway Gateway.Api.dll 5010 gateway.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service" \
  "network-online.target"

create_legalsynq_service identity \
  "LegalSynq Identity API" \
  identity Identity.Api.dll 5001 identity.env \
  "network-online.target legalsynq-notifications.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service tenant \
  "LegalSynq Tenant API" \
  tenant Tenant.Api.dll 5005 tenant.env \
  "network-online.target legalsynq-identity.service" \
  "network-online.target"

create_legalsynq_service careconnect \
  "LegalSynq CareConnect API" \
  careconnect CareConnect.Api.dll 5003 careconnect.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service legalsynq-flow.service" \
  "network-online.target"

create_legalsynq_service fund \
  "LegalSynq Fund API" \
  fund Fund.Api.dll 5002 fund.env \
  "network-online.target legalsynq-flow.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service liens \
  "LegalSynq Liens API" \
  liens Liens.Api.dll 5009 liens.env \
  "network-online.target legalsynq-identity.service legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service legalsynq-flow.service legalsynq-task.service" \
  "network-online.target"

create_legalsynq_service documents \
  "LegalSynq Documents API" \
  documents Documents.Api.dll 5006 documents.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service audit \
  "LegalSynq Audit Event API" \
  audit PlatformAuditEventService.dll 5007 audit.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service notifications \
  "LegalSynq Notifications API" \
  notifications Notifications.Api.dll 5008 notifications.env \
  "network-online.target legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service flow \
  "LegalSynq Flow API" \
  flow Flow.Api.dll 5012 flow.env \
  "network-online.target legalsynq-audit.service legalsynq-notifications.service legalsynq-task.service" \
  "network-online.target"

create_legalsynq_service task \
  "LegalSynq Task API" \
  task Task.Api.dll 5016 task.env \
  "network-online.target legalsynq-notifications.service legalsynq-monitoring.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service monitoring \
  "LegalSynq Monitoring API" \
  monitoring Monitoring.Api.dll 5015 monitoring.env \
  "network-online.target" \
  "network-online.target"

create_legalsynq_service reports \
  "LegalSynq Reports API" \
  reports Reports.Api.dll 5029 reports.env \
  "network-online.target legalsynq-audit.service legalsynq-documents.service legalsynq-notifications.service" \
  "network-online.target"

create_legalsynq_service support \
  "LegalSynq Support API" \
  support Support.Api.dll 5017 support.env \
  "network-online.target legalsynq-identity.service legalsynq-tenant.service legalsynq-notifications.service legalsynq-audit.service legalsynq-documents.service" \
  "network-online.target"

create_legalsynq_service commerce \
  "LegalSynq Commerce API" \
  commerce Commerce.Api.dll 5030 commerce.env \
  "network-online.target legalsynq-tenant.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service billing \
  "LegalSynq Billing API" \
  billing Billing.Api.dll 5031 billing.env \
  "network-online.target legalsynq-commerce.service legalsynq-tenant.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service comms \
  "LegalSynq Comms API" \
  comms Comms.Api.dll 5011 comms.env \
  "network-online.target legalsynq-documents.service legalsynq-notifications.service legalsynq-audit.service" \
  "network-online.target"

create_legalsynq_service xenia \
  "LegalSynq Xenia API" \
  xenia Xenia.Api.dll 5035 xenia.env \
  "network-online.target legalsynq-careconnect.service" \
  "network-online.target"
```

The generated units correspond to this service map:

```text
Service                 WorkingDirectory                       DLL                              Port  Service env file
legalsynq-gateway       /opt/legalsynq/publish/gateway         Gateway.Api.dll                  5010  gateway.env
legalsynq-identity      /opt/legalsynq/publish/identity        Identity.Api.dll                 5001  identity.env
legalsynq-tenant        /opt/legalsynq/publish/tenant          Tenant.Api.dll                   5005  tenant.env
legalsynq-careconnect   /opt/legalsynq/publish/careconnect     CareConnect.Api.dll              5003  careconnect.env
legalsynq-fund          /opt/legalsynq/publish/fund            Fund.Api.dll                     5002  fund.env
legalsynq-liens         /opt/legalsynq/publish/liens           Liens.Api.dll                    5009  liens.env
legalsynq-documents     /opt/legalsynq/publish/documents       Documents.Api.dll                5006  documents.env
legalsynq-audit         /opt/legalsynq/publish/audit           PlatformAuditEventService.dll    5007  audit.env
legalsynq-notifications /opt/legalsynq/publish/notifications   Notifications.Api.dll            5008  notifications.env
legalsynq-flow          /opt/legalsynq/publish/flow            Flow.Api.dll                     5012  flow.env
legalsynq-task          /opt/legalsynq/publish/task            Task.Api.dll                     5016  task.env
legalsynq-monitoring    /opt/legalsynq/publish/monitoring      Monitoring.Api.dll               5015  monitoring.env
legalsynq-reports       /opt/legalsynq/publish/reports         Reports.Api.dll                  5029  reports.env
legalsynq-support       /opt/legalsynq/publish/support         Support.Api.dll                  5017  support.env
legalsynq-commerce      /opt/legalsynq/publish/commerce        Commerce.Api.dll                 5030  commerce.env
legalsynq-billing       /opt/legalsynq/publish/billing         Billing.Api.dll                  5031  billing.env
legalsynq-comms         /opt/legalsynq/publish/comms           Comms.Api.dll                    5011  comms.env
legalsynq-xenia         /opt/legalsynq/publish/xenia           Xenia.Api.dll                    5035  xenia.env
```

If one service fails because a dependency is still migrating on first boot, restart it after the dependency becomes healthy. The `After=` entries order startup; they do not prove that an HTTP dependency is ready.

Every generated unit loads exactly two env files:

```ini
EnvironmentFile=/etc/legalsynq/shared.env
EnvironmentFile=/etc/legalsynq/<service>.env
```

Reload systemd:

```bash
sudo systemctl daemon-reload
```

Enable services:

```bash
sudo systemctl enable \
  legalsynq-audit \
  legalsynq-notifications \
  legalsynq-tenant \
  legalsynq-identity \
  legalsynq-documents \
  legalsynq-flow \
  legalsynq-task \
  legalsynq-fund \
  legalsynq-careconnect \
  legalsynq-liens \
  legalsynq-monitoring \
  legalsynq-reports \
  legalsynq-support \
  legalsynq-commerce \
  legalsynq-billing \
  legalsynq-comms \
  legalsynq-xenia \
  legalsynq-gateway
```

Start services in dependency-friendly order:

```bash
sudo systemctl start legalsynq-audit legalsynq-notifications legalsynq-monitoring
sudo systemctl start legalsynq-identity legalsynq-tenant
sudo systemctl start legalsynq-documents legalsynq-task legalsynq-flow
sudo systemctl start legalsynq-fund legalsynq-careconnect legalsynq-liens
sudo systemctl start legalsynq-reports legalsynq-support legalsynq-commerce legalsynq-billing legalsynq-comms legalsynq-xenia
sudo systemctl start legalsynq-gateway
```

## 8. Configure Nginx

Create `/etc/nginx/sites-available/legalsynq-api`:

```nginx
server {
    listen 80;
    server_name api.yourdomain.com;
    client_max_body_size 60m;

    location / {
        proxy_pass http://127.0.0.1:5010;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

Enable the site:

```bash
sudo ln -sf /etc/nginx/sites-available/legalsynq-api /etc/nginx/sites-enabled/legalsynq-api
sudo nginx -t
sudo systemctl reload nginx
```

Add TLS with Certbot or use an AWS Application Load Balancer with ACM. For a single EC2 Nginx deployment, Certbot is the simplest.

## 8A. Deep-Link Association Files

The gateway serves Apple and Android association files anonymously at these canonical paths:

```text
/.well-known/apple-app-site-association
/.well-known/assetlinks.json
```

Generate the files only after Platform has approved the host and Mobile release owners have provided the Apple Team ID and public Android SHA-256 app signing certificate fingerprint:

```bash
python3 scripts/deep-links/generate-association-files.py \
  --config <approved-association-config.json> \
  --routes shared/contracts/deep-links/routes.json \
  --output apps/gateway/Gateway.Api/DeepLinks/Associations
```

Validate before restart:

```bash
python3 scripts/deep-links/validate-association-files.py \
  --routes shared/contracts/deep-links/routes.json \
  --directory apps/gateway/Gateway.Api/DeepLinks/Associations/production \
  --apple-app-id <APPLE_TEAM_ID>.com.legalsynq \
  --android-package com.legalsynq
```

After deployment, verify direct public HTTPS delivery without redirects or authentication:

```bash
curl -i https://<approved-deep-link-host>/.well-known/apple-app-site-association
curl -i https://<approved-deep-link-host>/.well-known/assetlinks.json
```

## 9. Verify Health

Check local services:

```bash
curl -i http://127.0.0.1:5010/health
curl -i http://127.0.0.1:5001/health
curl -i http://127.0.0.1:5003/health
curl -i http://127.0.0.1:5035/health
```

Check through Gateway/Nginx:

```bash
curl -i https://api.yourdomain.com/identity/health
curl -i https://api.yourdomain.com/careconnect/health
curl -i https://api.yourdomain.com/xenia/health
```

Check logs:

```bash
sudo journalctl -u legalsynq-identity -n 100 --no-pager
sudo journalctl -u legalsynq-xenia -n 100 --no-pager
sudo journalctl -u legalsynq-gateway -n 100 --no-pager
```

## 10. Release Update Flow

Pull the new release:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app
git fetch
git checkout <new-release-tag>
```

Publish the changed service:

```bash
dotnet publish apps/services/identity/Identity.Api/Identity.Api.csproj -c Release -o /opt/legalsynq/publish/identity
```

Restart and verify:

```bash
exit
sudo systemctl restart legalsynq-identity
curl -f http://127.0.0.1:5001/health
```

For a full backend deploy, publish all services, then restart in this order:

```text
audit/notifications
tenant/identity
documents/flow/task
fund/careconnect/liens
monitoring/reports/support/commerce/billing/comms/xenia
gateway
```

## 11. Operational Notes

- Prefer AWS Secrets Manager or SSM Parameter Store over plain env files after the first stable deployment.
- Use CloudWatch Agent for logs, disk, memory, and process metrics.
- Enable RDS automated backups before first production traffic.
- Keep RDS private; never expose MySQL publicly.
- Use a single EC2 instance only as a first deployment step. The next production-grade step is ALB + Auto Scaling, ECS, or EKS.
- All services should validate JWTs independently; the Gateway is not the only auth boundary.
- Most services run EF migrations on startup, so check logs carefully during first boot.

## References

- AWS EC2 security groups: https://docs.aws.amazon.com/AWSEC2/latest/UserGuide/creating-security-group.html
- AWS RDS MySQL setup: https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/CHAP_GettingStarted.CreatingConnecting.MySQL.html
- Microsoft .NET Linux install: https://learn.microsoft.com/en-us/dotnet/core/install/linux-scripted-manual
