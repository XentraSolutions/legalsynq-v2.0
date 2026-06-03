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
```

## 6. Create Environment Files

Use one env file per deployable service, plus one shared env file for values that are truly common across backend services.

Keep service-specific secrets out of unrelated service processes. For example, the Fund service should receive `ConnectionStrings__FundDb`, but it should not receive the Documents S3 config or Notifications SendGrid keys.

Create `/etc/legalsynq/shared.env`:

```bash
ASPNETCORE_ENVIRONMENT=Production
Jwt__Issuer=legalsynq-identity
Jwt__Audience=legalsynq-platform
Jwt__SigningKey=<minimum-32-char-production-secret>
FLOW_SERVICE_TOKEN_SECRET=<minimum-32-char-service-token-secret>
PublicTrustBoundary__InternalRequestSecret=<secret>
```

Create service-specific files under `/etc/legalsynq`.

`/etc/legalsynq/gateway.env`:

```bash
IdentityService__BaseUrl=http://localhost:5001
TenantService__BaseUrl=http://localhost:5005
NotificationsService__BaseUrl=http://localhost:5008
AuditClient__BaseUrl=http://localhost:5007
```

`/etc/legalsynq/identity.env`:

```bash
ConnectionStrings__IdentityDb=Server=<rds>;Port=3306;Database=identity_db;User=<user>;Password=<pass>;
TenantService__BaseUrl=http://localhost:5005
TenantService__ProvisioningToken=<secret>
TenantService__ProvisioningSecret=<secret>
NotificationsService__BaseUrl=http://localhost:5008
NotificationsService__PortalBaseUrl=https://app.yourdomain.com
NotificationsService__PortalBaseDomain=yourdomain.com
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
```

`/etc/legalsynq/tenant.env`:

```bash
ConnectionStrings__TenantDb=Server=<rds>;Port=3306;Database=tenant_db;User=<user>;Password=<pass>;
IdentityService__BaseUrl=http://localhost:5001
IdentityService__ProvisioningToken=<secret>
IdentityService__ProvisioningSecret=<secret>
```

`/etc/legalsynq/careconnect.env`:

```bash
ConnectionStrings__CareConnectDb=Server=<rds>;Port=3306;Database=careconnect_db;User=<user>;Password=<pass>;
IdentityService__BaseUrl=http://localhost:5001
IdentityService__ProvisioningToken=<secret>
TenantService__BaseUrl=http://localhost:5005
TenantService__ProvisioningToken=<secret>
NotificationsService__BaseUrl=http://localhost:5008
DocumentsService__BaseUrl=http://localhost:5006
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
Flow__BaseUrl=http://localhost:5012
```

`/etc/legalsynq/fund.env`:

```bash
ConnectionStrings__FundDb=Server=<rds>;Port=3306;Database=fund_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/liens.env`:

```bash
ConnectionStrings__LiensDb=Server=<rds>;Port=3306;Database=liens_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/documents.env`:

```bash
ConnectionStrings__DocsDb=Server=<rds>;Port=3306;Database=docs_db;User=<user>;Password=<pass>;
AWS_S3_BUCKET_NAME=<bucket>
AWS_S3_REGION=<region>
AWS_S3_ACCESS_KEY_ID=<access-key-if-not-using-instance-role>
AWS_S3_SECRET_ACCESS_KEY=<secret-key-if-not-using-instance-role>
```

`/etc/legalsynq/audit.env`:

```bash
ConnectionStrings__AuditEventDb=Server=<rds>;Port=3306;Database=audit_event_db;User=<user>;Password=<pass>;
AuditClient__ServiceToken=<secret>
```

`/etc/legalsynq/notifications.env`:

```bash
NOTIF_DB_HOST=<rds>
NOTIF_DB_PORT=3306
NOTIF_DB_NAME=notifications_db
NOTIF_DB_USER=<user>
NOTIF_DB_PASSWORD=<pass>
NotificationsService__PortalBaseUrl=https://app.yourdomain.com
NotificationsService__PortalBaseDomain=yourdomain.com
SENDGRID_API_KEY=<key>
SENDGRID_FROM_EMAIL=<email>
SENDGRID_FROM_NAME=LegalSynq
```

`/etc/legalsynq/flow.env`:

```bash
ConnectionStrings__FlowDb=Server=<rds>;Port=3306;Database=flow_db;User=<user>;Password=<pass>;
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
NotificationsService__BaseUrl=http://localhost:5008
```

`/etc/legalsynq/task.env`:

```bash
ConnectionStrings__TasksDb=Server=<rds>;Port=3306;Database=tasks_db;User=<user>;Password=<pass>;
NotificationsService__BaseUrl=http://localhost:5008
MonitoringService__BaseUrl=http://localhost:5015
TASK_SERVICE_URL=http://localhost:5016
```

`/etc/legalsynq/monitoring.env`:

```bash
ConnectionStrings__MonitoringDb=Server=<rds>;Port=3306;Database=monitoring_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/reports.env`:

```bash
ConnectionStrings__ReportsDb=Server=<rds>;Port=3306;Database=reports_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/support.env`:

```bash
ConnectionStrings__Support=Server=<rds>;Port=3306;Database=support;User=<user>;Password=<pass>;
```

`/etc/legalsynq/commerce.env`:

```bash
Database__ConnectionString=Server=<rds>;Port=3306;Database=commerce_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/billing.env`:

```bash
BILLING_DB_CONNECTION=Server=<rds>;Port=3306;Database=billing_db;User=<user>;Password=<pass>;
```

`/etc/legalsynq/comms.env`:

```bash
ConnectionStrings__SynqCommDb=Server=<rds>;Port=3306;Database=synqcomm_db;User=<user>;Password=<pass>;
Services__NotificationsUrl=http://localhost:5008
Services__DocumentsUrl=http://localhost:5006
AuditClient__BaseUrl=http://localhost:5007
AuditClient__ServiceToken=<secret>
```

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
  legalsynq-gateway
```

Start services in dependency-friendly order:

```bash
sudo systemctl start legalsynq-audit legalsynq-notifications legalsynq-monitoring
sudo systemctl start legalsynq-identity legalsynq-tenant
sudo systemctl start legalsynq-documents legalsynq-task legalsynq-flow
sudo systemctl start legalsynq-fund legalsynq-careconnect legalsynq-liens
sudo systemctl start legalsynq-reports legalsynq-support legalsynq-commerce legalsynq-billing legalsynq-comms
sudo systemctl start legalsynq-gateway
```

## 8. Configure Nginx

Create `/etc/nginx/sites-available/legalsynq-api`:

```nginx
server {
    listen 80;
    server_name api.yourdomain.com;

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

## 9. Verify Health

Check local services:

```bash
curl -i http://127.0.0.1:5010/health
curl -i http://127.0.0.1:5001/health
curl -i http://127.0.0.1:5003/health
```

Check through Gateway/Nginx:

```bash
curl -i https://api.yourdomain.com/identity/health
curl -i https://api.yourdomain.com/careconnect/health
```

Check logs:

```bash
sudo journalctl -u legalsynq-identity -n 100 --no-pager
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
monitoring/reports/support/commerce/billing/comms
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
