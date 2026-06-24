# Flow Service

Workflow engine and task management for the SynqLien product (and extensible to other products).

**Port:** 5015 (backend API)

## Responsibilities

- Workflow configuration (stages, transitions, guard conditions)
- Task lifecycle management (create, assign, status, complete, cancel)
- Task templates (reusable task definitions with defaults)
- Event-driven task auto-generation rules (triggered by lien lifecycle events)
- Task notes and collaboration
- Workflow transition engine (stage advancement with guard evaluation)
- Task creation governance (quota, deduplication, email notifications on create)
- Work distribution and assignment recommendation
- SLA tracking and escalation

## Structure

```
flow/
  backend/
    src/
      Flow.Api/             Endpoints, middleware, Program.cs (port 5015)
      Flow.Application/     Workflow and task orchestration services
      Flow.Domain/          WorkflowConfig, WorkflowStage, Task, TaskTemplate,
                             TaskGenerationRule, TaskNote
      Flow.Infrastructure/  DbContext (FlowDb), repositories, EF migrations,
                             email notification adapter
  docs/                     Design documentation
```

## Key Endpoint Groups

| Prefix | Description |
|---|---|
| `/api/liens/tasks` | Task CRUD + assign + status + complete + cancel |
| `/api/liens/tasks/{id}/notes` | Task notes (create, read, update, delete) |
| `/api/liens/workflow` | Tenant workflow config (get, create, update, reorder stages) |
| `/api/admin/workflow` | Admin workflow config (per-tenant with explicit tenantId) |
| `/api/liens/task-templates` | Task template CRUD |
| `/api/liens/task-generation-rules` | Generation rule CRUD + enable/disable |

## Task Auto-Generation

Rules define: trigger event type + lien criteria + template reference. When a matching lien event fires, the engine creates tasks automatically. Tasks carry metadata: `TaskSourceType` (Manual / SystemGenerated), `generationRuleId`, `generatingTemplateId`.

## Workflow Transition Engine

Guards evaluate conditions before allowing stage transitions. Transition attempts that fail guard conditions return 422 with a list of unmet conditions. Guards are evaluated in order — first failing guard short-circuits.

## Database

`FlowDb` (MySQL).

## External Integrations

- **Notifications service** — email notifications on task create (governance-aware)
- **Audit service** — task lifecycle and workflow events published

## Auth

Requests authenticated by JWT (user context) or service token (`FLOW_SERVICE_TOKEN_SECRET`) for machine-to-machine calls from the Liens service.
