# Control Center — CI/CD Pipeline Template

This document is a generic CI/CD template that can be adapted for any pipeline
runner (GitHub Actions, GitLab CI, AWS CodeBuild, CircleCI, Buildkite, etc.).
The logical stages are listed below; expand each placeholder section with your
platform's syntax.

---

## Pipeline Trigger

```yaml
# Trigger on:
#   - push to main (deploy)
#   - pull-request to main (validate only — no deploy)
#   - manual dispatch with optional environment override

trigger:
  - push:  [ main ]
  - pull_request: [ main ]
  - workflow_dispatch:
      inputs:
        environment: { default: staging }
```

---

## Environment Variables (CI secrets)

The following secrets must be configured in the CI secret store before running
the pipeline. **Never hard-code these in pipeline YAML files.**

| Secret name | Stage | Description |
|-------------|-------|-------------|
| `CONTROL_CENTER_API_BASE` | build + runtime | API gateway base URL |
| `NEXT_PUBLIC_CONTROL_CENTER_ORIGIN` | build | Public CC origin (baked into client bundle) |
| `AWS_ACCESS_KEY_ID` | push | ECR / S3 access (or equivalent for your registry) |
| `AWS_SECRET_ACCESS_KEY` | push | ECR / S3 secret |
| `REGISTRY_URL` | push | Container registry URL (e.g. `123456789.dkr.ecr.eu-west-1.amazonaws.com`) |
| `DEPLOY_TOKEN` | deploy | Platform API token (Railway / ECS / Vercel / Fly.io) |

---

## Stage 1 — Install

```bash
# Working directory: apps/control-center

# Restore package-lock.json for deterministic installs.
# Use --legacy-peer-deps to match local dev setup (Tailwind v4 peer deps).
npm ci --legacy-peer-deps

# Verify the lock file is not dirty after install.
# git diff --exit-code package-lock.json
```

**Cache:** Cache `node_modules/` keyed on `package-lock.json` hash to skip
re-download on subsequent runs when dependencies have not changed.

---

## Stage 2 — Type Check

```bash
# Working directory: apps/control-center
# Runs BEFORE build — catches type errors without spending time on next build.

npm run type-check
# Equivalent: npx tsc --noEmit
```

**Failure policy:** Block merge. Zero TypeScript errors required.

---

## Stage 3 — Lint (optional but recommended)

```bash
# Requires: eslint configured in package.json
# TODO: add ESLint config to apps/control-center

npm run lint
# Equivalent: next lint
```

**Failure policy:** Block merge in strict mode; warn-only in permissive mode.

---

## Stage 4 — Production Build

```bash
# Working directory: apps/control-center
# NEXT_PUBLIC_* vars must be available at BUILD TIME — they are inlined into
# the client-side JS bundle.

export NEXT_PUBLIC_CONTROL_CENTER_ORIGIN=<from-ci-secret>
export NEXT_PUBLIC_BASE_PATH=""
export NODE_ENV=production
export NEXT_TELEMETRY_DISABLED=1

npm run build
# Produces: .next/
```

**Failure policy:** Block merge on any non-zero exit.

**Output artefact:** Upload `.next/` as a CI artefact so the Docker build stage
can reuse it without a second compile.

---

## Stage 5 — Docker Build

```bash
# Working directory: apps/control-center
# Requires: Docker daemon (or Buildx for multi-platform builds)

IMAGE_TAG="${REGISTRY_URL}/control-center:${GIT_SHA}"
IMAGE_LATEST="${REGISTRY_URL}/control-center:latest"

docker build \
  --build-arg NEXT_PUBLIC_CONTROL_CENTER_ORIGIN="${NEXT_PUBLIC_CONTROL_CENTER_ORIGIN}" \
  --build-arg NEXT_PUBLIC_BASE_PATH="" \
  --tag "${IMAGE_TAG}" \
  --tag "${IMAGE_LATEST}" \
  --file Dockerfile \
  .

# Verify the image starts and the health endpoint responds 200.
docker run --rm -d \
  --name cc-smoke \
  -p 5004:5004 \
  -e CONTROL_CENTER_API_BASE=http://host.docker.internal:5010 \
  -e NODE_ENV=production \
  "${IMAGE_TAG}"

# Wait for startup (max 30 s)
for i in $(seq 1 10); do
  sleep 3
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5004/api/health) && break
done
echo "Health status: ${STATUS}"
docker stop cc-smoke

[ "${STATUS}" = "200" ] || exit 1
```

**Failure policy:** Block push to registry if smoke test fails.

**Optimisation:** Use `docker buildx` with inline cache for faster layer pulls on
subsequent builds:

```bash
docker buildx build \
  --cache-from type=registry,ref="${IMAGE_LATEST}" \
  --cache-to   type=inline \
  ...
```

---

## Stage 6 — Push to Registry

```bash
# Authenticate to the container registry.
# AWS ECR example:
aws ecr get-login-password --region eu-west-1 \
  | docker login --username AWS --password-stdin "${REGISTRY_URL}"

# Push both the SHA tag and the rolling :latest tag.
docker push "${IMAGE_TAG}"
docker push "${IMAGE_LATEST}"
```

**Trigger condition:** Only on push to `main` (not on pull requests).

---

## Stage 7 — Deploy

Replace the placeholder block with the deploy command for your platform.

### AWS ECS Fargate

```bash
# Update the task definition to use the new image tag.
aws ecs update-service \
  --cluster  legalsynq-prod \
  --service  control-center \
  --force-new-deployment

# (Optional) Wait for stable deployment.
aws ecs wait services-stable \
  --cluster legalsynq-prod \
  --services control-center
```

### Railway

```bash
railway deploy --service control-center
```

### Vercel

```bash
vercel deploy \
  --prod \
  --env CONTROL_CENTER_API_BASE="${CONTROL_CENTER_API_BASE}"
```

### Fly.io

```bash
fly deploy \
  --app control-center \
  --image "${IMAGE_TAG}"
```

**Trigger condition:** Only on push to `main` after all prior stages pass.

**Rollback:** Tag the previous commit's image as `:rollback` before deploying.
If the health check fails within 5 minutes of deploy, re-deploy the `:rollback` image.

---

## Stage 8 — Post-deploy Smoke Test

```bash
# Poll /api/health on the production URL for up to 60 seconds.
PROD_URL="https://controlcenter.legalsynq.com"

for i in $(seq 1 20); do
  sleep 3
  STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${PROD_URL}/api/health") && break
done

echo "Post-deploy health: ${STATUS}"
[ "${STATUS}" = "200" ] || exit 1
```

**Alert:** If this step fails, trigger a PagerDuty / Opsgenie alert and halt
the pipeline. Manual intervention required.

---

## Stage 9 — Notify

```bash
# Send a Slack / Teams / email notification on success or failure.
# TODO: add Slack webhook integration
# TODO: add email notification for deployment failures
```

---

## Full pipeline summary

```
Install → Type Check → Lint → Build → Docker Build → Smoke Test
                                        ↓ (main only)
                                    Push to Registry
                                        ↓
                                      Deploy
                                        ↓
                                Post-deploy Health Check
                                        ↓
                                      Notify
```

---

## Required pipeline runtime

| Stage | Typical duration | Can be cached? |
|-------|-----------------|---------------|
| Install | ~60 s | Yes — node_modules cache |
| Type Check | ~10 s | No |
| Lint | ~5 s | No |
| Build | ~90 s | Yes — Next.js build cache |
| Docker Build | ~120 s | Yes — layer cache via registry |
| Push | ~30 s | Depends on layer reuse |
| Deploy | ~60–120 s | No |
| Post-deploy | ~30 s | No |
| **Total** | **~6–8 min** | |

---

## TODOs

```
TODO: add autoscaling config
  — ECS: target-tracking policy on CPU at 60% with min 2 / max 10 tasks.
  — Kubernetes: HPA on CPU + RPS metrics.

TODO: add blue/green deployment
  — Route new traffic to the new task set only after health checks pass.
  — ECS: use CodeDeploy with AppSpec blue/green.
  — Kubernetes: use Argo Rollouts or Flagger.

TODO: add alerting rules
  — Alert on: 5xx rate > 1%, p99 latency > 3 s, pod restarts > 2 in 5 min.
  — CloudWatch Alarms → SNS → PagerDuty.
  — Datadog: monitor "control-center.response_time.p99 > 3000ms".

TODO: add SAST scan stage
  — Add npm audit --audit-level=high between install and build.
  — Add Snyk or Trivy container scan after docker build.
  — Block pipeline on critical CVEs in node_modules or base image.

TODO: add CSP header audit stage
  — Automate Content-Security-Policy validation.
  — Fail pipeline if any 'unsafe-inline' or 'unsafe-eval' sources are present.

TODO: add performance budget check
  — Add bundle-size CI check (e.g. bundlesize or next/bundle-analyzer).
  — Fail if JS bundle exceeds budget (e.g. 300 kB gzipped for main chunk).
```
