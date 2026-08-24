---
name: Xenia adapter criticality
description: AdapterCriticality enum design decisions, seeding, and /ready behavior. Governs XENIA-P1-T2 delivery order.
---

# Xenia Adapter Criticality

## The rule
`AdapterCriticality.Optional = 0` (CLR default) — this is deliberate. If `Mandatory = 0` were the CLR default, EF would generate a sentinel-vs-database-default conflict warning (because it can't distinguish "user set Mandatory" from "no value set, use DB default").

## Values
| Value | Integer | /ready behavior |
|---|---|---|
| Optional | 0 | /ready 200 with degraded note if unavailable |
| Mandatory | 1 | /ready 503 if unavailable |
| Disabled | 2 | Excluded from readiness computation entirely |

## Seeding (EfAdapterRegistry)
| Adapter | Criticality |
|---|---|
| tenant | Mandatory — required for all multi-tenant operations |
| identity | Mandatory — required for all authenticated operations |
| document | Optional |
| audit | Optional |
| notification | Optional |
| storage | Optional |
| workflow | Optional |
| ai | Optional |

## XENIA-P1-T2 implication
P1-T2 MUST implement `TenantAdapter` and `IdentityAdapter` (both Mandatory) before `/ready` will return 200 on a properly configured deployment. Other adapters (Document, Audit, etc.) can be delivered in later tickets.

## /ready response
Includes `criticality` field per adapter entry so the Control Center and monitoring systems can display which adapter failures are blocking vs degraded.

## Where implemented
- `Xenia.Domain/Adapters/AdapterCriticality.cs`
- `Xenia.Domain/Adapters/PlatformAdapter.cs` — `Criticality` property + `SetCriticality()`
- `Xenia.Application/Adapters/AdapterDto.cs` — `criticality` in DTO
- `Xenia.Infrastructure/Registry/EfAdapterRegistry.cs` — seeding
- `Xenia.Api/Endpoints/XeniaHealthEndpoints.cs` — /ready uses criticality
- Migration `20260710000002_AddAdapterCriticality`
