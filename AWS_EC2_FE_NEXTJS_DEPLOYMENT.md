# Deploy Frontend Next.js Apps on AWS EC2

This runbook deploys the LegalSynq frontend Next.js applications on AWS EC2, with Nginx/TLS in front of the Node.js processes and the backend gateway reachable through `GATEWAY_URL` / `CONTROL_CENTER_API_BASE`.

Frontend apps:

```text
LegalSynq Web       apps/web             port 3000
Control Center      apps/control-center  port 5004
```

Supported deployment modes in this repo:

- Source checkout deploy: the server has the full monorepo checkout under `/opt/legalsynq/app`.
- Built artifact deploy: the server receives copied frontend build output such as `dist/frontend/web` and `dist/frontend/control-center`.

Recommended public hostnames:

```text
app.yourdomain.com             -> apps/web
controlcenter.yourdomain.com   -> apps/control-center
api.yourdomain.com             -> backend gateway from AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md
```

## 1. Target Architecture

Public traffic should enter only through Nginx. The Next.js app ports should stay private to the EC2 instance.

```text
Internet -> Route 53 -> Nginx :443 -> Next.js apps on localhost
```

For a single-instance first deployment:

```text
app.yourdomain.com           -> Nginx -> 127.0.0.1:3000
controlcenter.yourdomain.com -> Nginx -> 127.0.0.1:5004
api.yourdomain.com           -> Nginx -> 127.0.0.1:5010
```

For a split deployment, place the frontend apps on a separate EC2 instance and point their server-side API env vars at the backend gateway hostname:

```text
GATEWAY_URL=https://api.yourdomain.com
CONTROL_CENTER_API_BASE=https://api.yourdomain.com
```

## 2. AWS Resources

Create or reuse these AWS resources:

- EC2 instance: Ubuntu 24.04 LTS or Amazon Linux 2023.
- Instance size: use at least `t3.medium`; prefer `t3.large` if building on the instance.
- Route 53 records:
  - `app.yourdomain.com`
  - `controlcenter.yourdomain.com`
- Security group inbound:
  - `22` from your IP only, or no SSH if using SSM.
  - `80` from public.
  - `443` from public.
- Security group outbound:
  - Allow HTTPS and backend gateway access.

Do not expose `3000` or `5004` publicly.

## 3. Prepare EC2

Install base packages:

```bash
sudo apt update
sudo apt install -y git curl unzip nginx jq
```

Install Node.js 22:

```bash
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt install -y nodejs
node --version
npm --version
```

Enable pnpm through Corepack:

```bash
sudo corepack enable
corepack prepare pnpm@10.26.1 --activate
pnpm --version
```

Create the deploy user and directories:

```bash
sudo useradd --system --create-home --shell /bin/bash legalsynq || true
sudo mkdir -p /opt/legalsynq /etc/legalsynq /var/log/legalsynq
sudo chown -R legalsynq:legalsynq /opt/legalsynq /var/log/legalsynq
```

Verify that the application checkout path itself is writable by the `legalsynq` user before any `pnpm install` or service restart:

```bash
sudo install -d -o legalsynq -g legalsynq /opt/legalsynq/app
sudo chown -R legalsynq:legalsynq /opt/legalsynq/app
sudo -u legalsynq test -w /opt/legalsynq/app
```

Important:

- `/opt/legalsynq/app` is the monorepo root only. It is the correct place for `git clone` and root-level `pnpm install`, but it is not the correct `systemd` working directory for the frontend apps.
- The frontend repo path in this monorepo is `/opt/legalsynq/app/apps/web`, not `/opt/legalsynq/app/web`.
- If your service, deploy hook, or CI job runs from `/opt/legalsynq/app/web`, fix the working directory first; otherwise `pnpm` may fail with `EACCES` while creating temporary files such as `/opt/legalsynq/app/web/_tmp_*`.
- Exception: if you intentionally deploy a copied artifact into `/opt/legalsynq/app/web`, treat it as an artifact directory, not as the monorepo app source directory. In that mode, do not expect `pnpm start` to work unless you also deploy the app manifest and runtime dependencies needed by Next.js.

## 4. Deploy Code

Clone the repository:

```bash
sudo -iu legalsynq
cd /opt/legalsynq
git clone <your-repo-url> app
cd app
git checkout <release-branch-or-tag>
```

Install dependencies from the monorepo root:

```bash
pnpm install --frozen-lockfile
```

Important:

- Run `pnpm install` from `/opt/legalsynq/app`.
- Run `pnpm build` and `pnpm start` from each app directory such as `/opt/legalsynq/app/apps/web`.
- If you run `pnpm start` from `/opt/legalsynq/app`, `pnpm` will fail because the root [package.json](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/package.json) does not define a `start` script.

If the lockfile is not current yet, use this only for the first manual deploy and commit the resulting lockfile afterward:

```bash
pnpm install
```

## 5. Configure Environment

Create `/etc/legalsynq/web.env`:

```bash
NODE_ENV=production
NEXT_PUBLIC_ENV=production
PORT=3000
GATEWAY_URL=https://api.yourdomain.com
PublicTrustBoundary__InternalRequestSecret=<same-value-as-backend>
CC_COMMON_PORTAL_HOSTNAME=app.yourdomain.com
NEXT_PUBLIC_CC_STANDALONE=true
NEXT_PUBLIC_CC_ORIGIN=https://controlcenter.yourdomain.com
NEXT_PUBLIC_CC_LOGIN_URL=https://controlcenter.yourdomain.com/login
NEXT_PUBLIC_GOOGLE_MAPS_KEY=<browser-restricted-google-maps-key>
TENANT_BRANDING_READ_SOURCE=Tenant
```

Create `/etc/legalsynq/control-center.env`:

```bash
NODE_ENV=production
PORT=5004
CONTROL_CENTER_API_BASE=https://api.yourdomain.com
GATEWAY_URL=https://api.yourdomain.com
NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=https://controlcenter.yourdomain.com
CONTROL_CENTER_SELF_URL=https://controlcenter.yourdomain.com
REPORTS_SERVICE_URL=https://api.yourdomain.com/reports
COMMERCE_SERVICE_URL=https://api.yourdomain.com/commerce
BILLING_SERVICE_URL=https://api.yourdomain.com/billing
BILLING_INTERNAL_TOKEN=<billing-internal-token-if-required>
```

Secure the files:

```bash
sudo chown root:legalsynq /etc/legalsynq/web.env /etc/legalsynq/control-center.env
sudo chmod 640 /etc/legalsynq/web.env /etc/legalsynq/control-center.env
```

Important notes:

- `NEXT_PUBLIC_*` values are baked into the browser bundle by `next build`. Rebuild after changing them.
- `GATEWAY_URL` and `CONTROL_CENTER_API_BASE` are server-side values used by Next.js route handlers and rewrites.
- Keep `PublicTrustBoundary__InternalRequestSecret` aligned with the backend value because public tenant/network routes sign internal tenant headers with it.
- Do not use the checked-in `apps/web/.env.local` values for production.

## 6. Build the Apps

Build Web:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app/apps/web
set -a
. /etc/legalsynq/web.env
set +a
pnpm build
```

Build Control Center:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app/apps/control-center
set -a
. /etc/legalsynq/control-center.env
set +a
pnpm build
```

If the EC2 instance runs out of memory during build, either:

- Build on a larger temporary instance and deploy the built release artifact.
- Add swap before building.
- Use at least `t3.large` for build-on-instance deployments.

## 6A. Understand the `dist/` Artifact Layout

The local frontend build script [scripts/build-fe-local.sh](/Users/ralphlopez/Documents/GitHub/legalsynq/legalsynq-v2.0/scripts/build-fe-local.sh) packages each app into:

- `dist/frontend/web`
- `dist/frontend/control-center`

Important:

- These directories are app-root runtime artifacts.
- Each artifact now contains:
  - `.next/`
  - `package.json`
  - `next.config.mjs`
  - `public/` when present
- The artifact is still not a fully standalone Next.js server bundle.
- You must still install runtime dependencies in the artifact directory on the server before running `pnpm start`.
- The `dist/` output currently does not include a standalone `server.js` launcher and is not built with Next.js standalone output mode.

If you deploy the copied `dist/frontend/web` folder directly to the server, use this flow:

1. Copy `dist/frontend/web` to `/opt/legalsynq/app/web`.
2. Run `pnpm install --prod` inside `/opt/legalsynq/app/web`.
3. Start the app with `pnpm start` from `/opt/legalsynq/app/web`.

Example:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app/web
pnpm install --prod
set -a
. /etc/legalsynq/web.env
set +a
pnpm start
```

If `pnpm install --prod` fails with:

```text
[EACCES] EACCES: permission denied, open '/opt/legalsynq/app/web/_tmp_*'
```

repair the artifact directory ownership and permissions first:

```bash
sudo chown -R legalsynq:legalsynq /opt/legalsynq/app
sudo find /opt/legalsynq/app -type d -exec chmod 755 {} \;
sudo find /opt/legalsynq/app -type f -exec chmod 644 {} \;
sudo chmod 755 /opt/legalsynq/app/web
```

Then verify write access explicitly:

```bash
sudo -u legalsynq test -w /opt/legalsynq/app/web && echo writable
sudo -u legalsynq touch /opt/legalsynq/app/web/.permcheck
sudo -u legalsynq rm /opt/legalsynq/app/web/.permcheck
```

Only after those checks pass should you retry:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app/web
pnpm install --prod
```

Note about this warning:

```text
[WARN] The "pnpm" field in package.json is no longer read by pnpm. The following keys were ignored: "pnpm.overrides".
```

- This warning is separate from the `EACCES` failure.
- It does not prevent `pnpm` from running.
- The immediate install blocker is directory write permission under `/opt/legalsynq/app/web`.
- Clean this up later by moving overrides to the current pnpm-supported config location.

Future improvement:

- Switch the app to Next.js standalone output and deploy the standalone server bundle instead of relying on `pnpm start`.

## 7. Create systemd Services

Choose the unit template that matches your deployment mode.

### Option A — Source Checkout Deploy

Use this when the server has the full monorepo under `/opt/legalsynq/app`.

Create `/etc/systemd/system/legalsynq-web.service`:

```ini
[Unit]
Description=LegalSynq Web Next.js App
After=network-online.target
Wants=network-online.target

[Service]
User=legalsynq
WorkingDirectory=/opt/legalsynq/app/apps/web
EnvironmentFile=/etc/legalsynq/web.env
Environment=NEXT_TELEMETRY_DISABLED=1
ExecStart=/usr/bin/pnpm start
Restart=always
RestartSec=5
SyslogIdentifier=legalsynq-web

[Install]
WantedBy=multi-user.target
```

### Option B — Copied Artifact Deploy

Use this only if `/opt/legalsynq/app/web` is a runtime artifact directory that you assembled intentionally.

Important:

- `ExecStart=/usr/bin/pnpm start` in `/opt/legalsynq/app/web` is valid only after you copy the packaged artifact and run `pnpm install --prod` in that directory.
- If runtime dependencies are missing, `pnpm start` may resolve but `next start` will fail at runtime.
- If you copied only old `.next` output into `/opt/legalsynq/app/web`, `systemd` will still fail even though the build succeeded on your local machine.
- If the artifact was copied by `root` or another user, repair ownership before running any `pnpm` command as `legalsynq`.

Example artifact-based web service:

```ini
[Unit]
Description=LegalSynq Web Next.js App
After=network-online.target
Wants=network-online.target

[Service]
User=legalsynq
WorkingDirectory=/opt/legalsynq/app/web
EnvironmentFile=/etc/legalsynq/web.env
Environment=NEXT_TELEMETRY_DISABLED=1
ExecStart=/usr/bin/pnpm start
Restart=always
RestartSec=5
SyslogIdentifier=legalsynq-web

[Install]
WantedBy=multi-user.target
```

Create `/etc/systemd/system/legalsynq-control-center.service`:

```ini
[Unit]
Description=LegalSynq Control Center Next.js App
After=network-online.target
Wants=network-online.target

[Service]
User=legalsynq
WorkingDirectory=/opt/legalsynq/app/apps/control-center
EnvironmentFile=/etc/legalsynq/control-center.env
Environment=NEXT_TELEMETRY_DISABLED=1
ExecStart=/usr/bin/pnpm start
Restart=always
RestartSec=5
SyslogIdentifier=legalsynq-control-center

[Install]
WantedBy=multi-user.target
```

Reload and start:

```bash
sudo systemctl daemon-reload
sudo systemctl enable legalsynq-web legalsynq-control-center
sudo systemctl start legalsynq-web legalsynq-control-center
```

Check logs:

```bash
sudo journalctl -u legalsynq-web -n 100 --no-pager
sudo journalctl -u legalsynq-control-center -n 100 --no-pager
```

Quick validation for Ubuntu/systemd deployments:

```bash
sudo systemctl cat legalsynq-web
sudo -u legalsynq test -w /opt/legalsynq/app/apps/web && echo writable
sudo ls -ld /opt/legalsynq/app /opt/legalsynq/app/apps /opt/legalsynq/app/apps/web
sudo -u legalsynq sh -lc 'cd /opt/legalsynq/app/apps/web && pnpm run start --help >/dev/null && echo web-start-script-found'
```

Expected result:

- `WorkingDirectory=/opt/legalsynq/app/apps/web`
- the checkout directories are owned by `legalsynq:legalsynq` or are otherwise writable by that user
- the app-level `package.json` is the one being resolved for `pnpm start`

Common failure patterns:

- `ERR_PNPM_NO_SCRIPT_OR_SERVER Missing script start or file server.js`
  - Cause: `systemd` is starting from `/opt/legalsynq/app` or another directory that does not contain the web app `package.json`.
  - Fix: set `WorkingDirectory=/opt/legalsynq/app/apps/web`.
- `ERR_PNPM_NO_SCRIPT_OR_SERVER Missing script start or file server.js` in `/opt/legalsynq/app/web`
  - Cause: `/opt/legalsynq/app/web` contains old copied build output from `dist/frontend/web` without the app manifest.
  - Fix: rebuild with the current packaging script and redeploy the full `dist/frontend/web` artifact.
- `EACCES permission denied, open '/opt/legalsynq/app/web/_tmp_*'`
  - Cause: a deploy hook or service is using the wrong path `/opt/legalsynq/app/web`, or the checkout tree is not writable by `legalsynq`.
  - Fix: use `/opt/legalsynq/app/apps/web` and restore ownership with `sudo chown -R legalsynq:legalsynq /opt/legalsynq/app`.
- `EACCES permission denied, open '/opt/legalsynq/app/web/_tmp_*'` during artifact deploy
  - Cause: `/opt/legalsynq/app/web` exists, but the deployed artifact tree is not writable by `legalsynq`.
  - Fix: run `sudo chown -R legalsynq:legalsynq /opt/legalsynq/app`, normalize permissions, verify `test -w`, then retry `pnpm install --prod`.
- `pnpm start` works manually but fails in `systemd`
  - Cause: the shell session is in the app directory, but the unit file is not.
  - Fix: trust `systemctl cat legalsynq-web`, not the current interactive shell location.
- `next: command not found` or `Cannot find module 'next'`
  - Cause: the artifact was copied, but runtime dependencies were never installed in `/opt/legalsynq/app/web`.
  - Fix: run `pnpm install --prod` in the deployed artifact directory before starting the service.
- `The "pnpm" field in package.json is no longer read by pnpm`
  - Cause: the deployed app uses a newer pnpm version that ignores `package.json -> pnpm.overrides`.
  - Fix: treat it as a config warning, not as the root cause of install/start failure. Migrate the overrides later.

## 8. Configure Nginx

Create `/etc/nginx/sites-available/legalsynq-frontend`:

```nginx
server {
    listen 80;
    server_name app.yourdomain.com;

    location / {
        proxy_pass http://127.0.0.1:3000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}

server {
    listen 80;
    server_name controlcenter.yourdomain.com;

    location / {
        proxy_pass http://127.0.0.1:5004;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

Enable the site:

```bash
sudo ln -sf /etc/nginx/sites-available/legalsynq-frontend /etc/nginx/sites-enabled/legalsynq-frontend
sudo nginx -t
sudo systemctl reload nginx
```

Add TLS with Certbot:

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d app.yourdomain.com -d controlcenter.yourdomain.com
```

If `api.yourdomain.com` is served from the same Nginx instance, include it in the backend Nginx site from `AWS_EC2_BE_MICROSERVICES_DEPLOYMENT.md` or add it to the same Certbot run.

## 9. Verify Deployment

Check local app processes:

```bash
curl -i http://127.0.0.1:3000
curl -i http://127.0.0.1:5004
```

Check public hostnames:

```bash
curl -I https://app.yourdomain.com
curl -I https://controlcenter.yourdomain.com
```

Check backend connectivity from the EC2 instance:

```bash
curl -i https://api.yourdomain.com/identity/health
curl -i https://api.yourdomain.com/tenant/health
```

Browser smoke test:

- Open `https://app.yourdomain.com/login`.
- Sign in and confirm `/api/auth/me` succeeds.
- Open `https://controlcenter.yourdomain.com/login`.
- Sign in with a `PlatformAdmin` user and confirm the dashboard loads.
- Confirm cookies are secure and HttpOnly in production.

## 10. Release Update Flow

Pull the new release:

```bash
sudo -iu legalsynq
cd /opt/legalsynq/app
git fetch
git checkout <new-release-tag>
pnpm install --frozen-lockfile
```

Rebuild changed apps:

```bash
cd /opt/legalsynq/app/apps/web
set -a
. /etc/legalsynq/web.env
set +a
pnpm build

cd /opt/legalsynq/app/apps/control-center
set -a
. /etc/legalsynq/control-center.env
set +a
pnpm build
```

Restart and verify:

```bash
exit
sudo systemctl restart legalsynq-web legalsynq-control-center
curl -I http://127.0.0.1:3000
curl -I http://127.0.0.1:5004
```

## 11. Operational Notes

- Prefer a CI/CD pipeline that builds once, stores an immutable artifact, and deploys that artifact to EC2.
- Prefer AWS Secrets Manager or SSM Parameter Store over plain env files after the first stable deployment.
- Add CloudWatch Agent for application logs and host metrics.
- Put frontend and backend behind an ALB when moving beyond a single-instance deployment.
- Keep `NEXT_PUBLIC_GOOGLE_MAPS_KEY` browser-restricted by domain in Google Cloud.
- Restrict Control Center access further with VPN, IP allowlists, AWS WAF, or an internal ALB if it is for operators only.
- Add explicit production domains to `serverActions.allowedOrigins` in both Next.js configs before hardening production CSRF posture.

## References

- Next.js production deployment: https://nextjs.org/docs/app/guides/deploying
- NodeSource Node.js packages: https://github.com/nodesource/distributions
- pnpm installation: https://pnpm.io/installation
- Nginx reverse proxy: https://docs.nginx.com/nginx/admin-guide/web-server/reverse-proxy/
