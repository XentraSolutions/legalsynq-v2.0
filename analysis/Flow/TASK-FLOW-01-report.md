# TASK-FLOW-01 — Flow → Task Execution Delegation

**Date:** 2026-04-21  
**Scope:** Replace Flow's direct DB writes to `flow_workflow_tasks` with calls to the external Task microservice. Flow's orchestration layer (WorkflowEngine, step sequencing, SLA evaluation) is unchanged; only task creation and lifecycle mutation ownership moves to Task service.

---

## 1. Inventory of flow_workflow_tasks touch-points

| Component | Layer | Current behaviour | Mutation type |
|---|---|---|---|
| `WorkflowTaskFromWorkflowFactory` | Application | Stages `WorkflowTask` EF entity; committed with workflow advance in same SaveChanges | CREATE |
| `WorkflowTaskLifecycleService` | Application | `ExecuteUpdateAsync` CAS: Open→InProgress, InProgress→Completed, Open|InProgress→Cancelled | STATUS UPDATE |
| `WorkflowTaskCompletionService` | Application | Orchestrates lifecycle CAS + `WorkflowEngine.AdvanceAsync` inside a DB transaction | COMPOSITE |
| `WorkflowTaskAssignmentService` | Application | `ExecuteUpdateAsync` CAS on assignment columns (Claim / Reassign) | ASSIGNMENT UPDATE |
| `MyTasksService` | Application | `AsNoTracking` queries: direct tasks, role queue, org queue, task detail | READ |
| `WorkflowTaskSlaEvaluator` | Infrastructure | Background worker: reads active tasks, updates SlaStatus / SlaBreachedAt / LastSlaEvaluatedAt | SLA UPDATE |
| `WorkflowTasksController` | API | HTTP surface for all of the above | — |

---

## 2. Key architectural constraints discovered

### 2a. Task service auth model
All Task service user-facing endpoints (`POST /api/tasks`, `POST /api/tasks/{id}/status`, `POST /api/tasks/{id}/assign`, `GET /api/tasks/*`) are protected by `AuthenticatedUser` policy — they require a valid user JWT, **not** a service token. The only service-token endpoint is `POST /api/tasks/internal/flow-callback`.

**Impact:** Flow cannot call Task service write or read endpoints using a service token alone (as Liens does for its Task service writes). Flow must forward the caller's bearer token to the Task service.

**Resolution:** `FlowTaskServiceAuthDelegatingHandler` reads the bearer token from the active `HttpContext.Request.Headers["Authorization"]` (via `IHttpContextAccessor`) and forwards it. For background-service calls (no HTTP context) it falls back to a minted service token — applicable only when Task service exposes an internal endpoint for that path (e.g., SLA). Since Flow's background SLA evaluator will be retired in this task, the fallback is defensive-only.

### 2b. Assignment model mismatch
Flow's `WorkflowTask` has a three-target assignment model:

| Flow concept | Columns | Task service equivalent |
|---|---|---|
| `DirectUser` | `AssignedUserId (string)` | `AssignedUserId (Guid?)` — parseable if userId is a UUID |
| `RoleQueue` | `AssignedRole (string)` | **No direct equivalent** |
| `OrgQueue` | `AssignedOrgId (string)` | **No direct equivalent** |

Flow's user IDs are strings (JWT `sub` claim, typically UUID-shaped). Task service expects `Guid?`. The client will `Guid.TryParse` and log a warning on failure.

For `RoleQueue` and `OrgQueue`, the Task service has no native support. In this Phase 1, the task is created with `AssignedUserId = null` (unassigned in Task service terms) and the queue metadata (`AssignedRole`, `AssignedOrgId`, `AssignmentMode`) is preserved in `flow_workflow_tasks` via the dual-write shadow (see §3).

A subsequent task (TASK-FLOW-02) will extend the Task service to carry Flow assignment metadata natively before the shadow table is dropped.

### 2c. Atomicity relaxation at WorkflowTaskCompletionService
Current shape: lifecycle CAS and engine advance share one DB transaction — either both commit or neither does.

Post-delegation shape: Task service `POST /api/tasks/{id}/status` call is outside the DB transaction. The execution strategy now wraps only the engine advance.

**Agreed sequencing:**
1. Call Task service to complete the task (**outside** any transaction).
2. Open DB transaction; run `WorkflowEngine.AdvanceAsync`; commit.
3. If step 2 throws: log the inconsistency (task is `Completed` in Task service but workflow has not advanced); surface the error to the caller. The caller may safely retry — Task service's `POST /api/tasks/{id}/status` is idempotent for terminal statuses. The engine's step-match check (`expectedCurrentStepKey`) prevents a double-advance.

The window of inconsistency (task Completed, workflow not yet advanced) is sub-millisecond in the success path and only materialises on DB failure during step 2. This is accepted as eventual-consistency within a single request.

---

## 3. Dual-write shadow strategy (Phase 1)

Because the Task service's query API requires bearer-token propagation and lacks eligibility-filtered queue reads for role/org queues, **reads remain on `flow_workflow_tasks` for Phase 1**. All mutations go to the Task service first (making it the write authority) and then mirror to `flow_workflow_tasks` (the shadow).

```
Request
  │
  ├── WRITE: Task service (primary authority)
  └── WRITE: flow_workflow_tasks (shadow / read replica)

Query surface (MyTasksService, SlaEvaluator)
  └── READ: flow_workflow_tasks (unchanged in Phase 1)
```

`flow_workflow_tasks` is NOT the authority for any mutation in Phase 1. It is a read-replica that stays consistent via the dual-write. The table is annotated `-- DEPRECATED: shadow only, managed by TASK-FLOW-01 dual-write` in a subsequent migration comment.

**Phase 2 cutover (TASK-FLOW-02):**
1. Extend Task service to expose `InternalService`-scoped search endpoints supporting assignment scope / eligibility filtering.
2. Migrate `MyTasksService` reads to Task service.
3. Retire `WorkflowTaskSlaEvaluator` (Task service owns SLA).
4. Stop dual-write to `flow_workflow_tasks`.
5. Drop table.

---

## 4. New infrastructure

### 4a. IFlowTaskServiceClient (Flow.Application/Interfaces/)
Interface wrapping Task service HTTP calls needed by Flow:

```
CreateTaskAsync(tenantId, workflowInstanceId, stepKey, title, priority, dueAt, assignedUserId)
  → returns TaskId (Guid)
TransitionStatusAsync(taskId, newStatus)
StartTaskAsync(taskId)
CompleteTaskAsync(taskId)
CancelTaskAsync(taskId)
AssignUserAsync(taskId, assignedUserId)
```

### 4b. FlowTaskServiceClient (Flow.Infrastructure/TaskService/)
HTTP implementation. Calls:
- `POST /api/tasks` for create (bearer token forwarded)
- `POST /api/tasks/{id}/status` for lifecycle transitions
- `POST /api/tasks/{id}/assign` for assignment

### 4c. FlowTaskServiceAuthDelegatingHandler (Flow.Infrastructure/TaskService/)
- Reads `HttpContext.Request.Headers["Authorization"]` via `IHttpContextAccessor`
- If present: forwards bearer token verbatim
- If absent (background context): falls back to `IServiceTokenIssuer.IssueToken`

---

## 5. Per-service change summary

### WorkflowTaskFromWorkflowFactory
**Before:** `_db.WorkflowTasks.Add(task)` — staged for same-TX commit.  
**After:** `await _taskClient.CreateTaskAsync(...)` then `_db.WorkflowTasks.Add(task)` (shadow).  
**Dedup:** Still performs in-memory (`DbSet.Local`) and DB `AnyAsync` checks. Task service is called only if both dedup checks pass.

### WorkflowTaskLifecycleService
**Before:** `ExecuteUpdateAsync` CAS on FlowDbContext.  
**After:** `await _taskClient.StartTaskAsync/CompleteTaskAsync/CancelTaskAsync(taskId)` then existing CAS (shadow update). Pre-check and error taxonomy preserved.

### WorkflowTaskCompletionService
**Before:** `lifecycle.CompleteTaskAsync` + `engine.AdvanceAsync` inside a single DB transaction.  
**After:**
1. Pre-validate (cheap, no transaction — unchanged).
2. `await _taskClient.CompleteTaskAsync(taskId)` (outside DB tx).
3. DB execution strategy: `lifecycle.CompleteTaskAsync` (shadow CAS) + `engine.AdvanceAsync` in one DB tx; commit.
4. On step 3 failure: log inconsistency, re-throw.

### WorkflowTaskAssignmentService
**Before:** `ExecuteUpdateAsync` CAS on assignment columns.  
**After:** For `DirectUser` target, `await _taskClient.AssignUserAsync(taskId, userId)`. Then existing CAS (shadow). For `RoleQueue`/`OrgQueue`/`Unassigned`, Task service call is skipped (not representable); shadow only. A warning is logged identifying the non-delegable modes.

### MyTasksService
No change in Phase 1. Continues reading from `flow_workflow_tasks` shadow.

### WorkflowTaskSlaEvaluator
No change in Phase 1. Continues updating shadow rows. Will be retired in TASK-FLOW-02.

---

## 6. DI registration changes (Flow.Infrastructure/DependencyInjection.cs)

```
services.AddTransient<FlowTaskServiceAuthDelegatingHandler>();
services.AddHttpClient<IFlowTaskServiceClient, FlowTaskServiceClient>(client => {
    client.BaseAddress = new Uri(configuration["ExternalServices:Task:BaseUrl"] ?? "http://localhost:5016");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<FlowTaskServiceAuthDelegatingHandler>();
```

Config key: `ExternalServices:Task:BaseUrl` (same as Liens).

---

## 7. Files changed

| File | Action |
|---|---|
| `Flow.Application/Interfaces/IFlowTaskServiceClient.cs` | CREATE |
| `Flow.Infrastructure/TaskService/FlowTaskServiceAuthDelegatingHandler.cs` | CREATE |
| `Flow.Infrastructure/TaskService/FlowTaskServiceClient.cs` | CREATE |
| `Flow.Application/Services/WorkflowTaskFromWorkflowFactory.cs` | MODIFY |
| `Flow.Application/Services/WorkflowTaskLifecycleService.cs` | MODIFY |
| `Flow.Application/Services/WorkflowTaskCompletionService.cs` | MODIFY |
| `Flow.Application/Services/WorkflowTaskAssignmentService.cs` | MODIFY |
| `Flow.Infrastructure/DependencyInjection.cs` | MODIFY |

`MyTasksService.cs`, `WorkflowTaskSlaEvaluator.cs` — **unchanged** (Phase 2).

---

## 8. Build verification plan

1. `dotnet build` — 0 errors, 0 warnings (pre-existing MSB3277 only).
2. Confirm all interfaces still compile against updated service implementations.
3. No DB migration required (shadow table schema unchanged).
