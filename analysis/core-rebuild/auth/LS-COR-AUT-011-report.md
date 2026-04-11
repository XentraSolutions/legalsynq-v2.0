# LS-COR-AUT-011 — Advanced Authorization (ABAC + Context-Aware Policies) Report

## Summary

Implemented Attribute-Based Access Control (ABAC) on top of the existing PBAC permission system. Policies contain rules evaluated against user, resource, and context attributes. Policies are linked to permissions via `PermissionPolicy` mappings, and enforcement is additive — users must first have the permission claim, then all linked active policies must pass.

## Components Delivered

### Domain Layer (Identity.Domain)

| File | Description |
|------|-------------|
| `Policy.cs` | Policy aggregate with factory method, code validation (`PRODUCT.domain.qualifier`), lifecycle methods |
| `PolicyRule.cs` | Rule entity with 11 supported fields, operator/field validation, condition type/logical group |
| `PermissionPolicy.cs` | Junction entity linking permission codes to policies |
| Enums | `PolicyConditionType`, `RuleOperator`, `LogicalGroupType` |

### Infrastructure Layer (Identity.Infrastructure)

| File | Description |
|------|-------------|
| `PolicyConfiguration.cs` | EF configuration with unique index on PolicyCode |
| `PolicyRuleConfiguration.cs` | EF configuration with FK to Policy |
| `PermissionPolicyConfiguration.cs` | EF configuration with unique index on (PermissionCode, PolicyId) |
| `IdentityDbContext.cs` | Added DbSets for Policies, PolicyRules, PermissionPolicies |
| `PolicyEvaluationService.cs` | Full rule evaluation engine with AND/OR grouping, numeric comparison, In/NotIn/Contains/StartsWith operators |
| `DefaultAttributeProvider.cs` | Extracts user/resource/request attributes from claims, context, and HttpContext |

### Shared BuildingBlocks

| File | Description |
|------|-------------|
| `IPolicyEvaluationService.cs` | Abstraction for policy evaluation |
| `IAttributeProvider.cs` | Abstraction for attribute extraction |
| `PolicyEvaluationResult.cs` | Result model with MatchedPolicy and RuleResult for explainability |
| `RequireProductAccessFilter.cs` | Enhanced to call policy evaluation when `Authorization:EnablePolicyEvaluation=true` |

### Admin API Endpoints (Identity.Api)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/admin/policies` | GET | List policies with optional product/search filter |
| `/api/admin/policies` | POST | Create new policy |
| `/api/admin/policies/{id}` | GET | Get policy with rules and permission mappings |
| `/api/admin/policies/{id}` | PATCH | Update policy name/description/priority |
| `/api/admin/policies/{id}` | DELETE | Deactivate policy |
| `/api/admin/policies/{id}/rules` | POST | Add rule to policy |
| `/api/admin/policies/{id}/rules/{ruleId}` | DELETE | Remove rule from policy |
| `/api/admin/policies/supported-fields` | GET | List supported fields, operators, condition types |
| `/api/admin/permission-policies` | GET | List permission-policy mappings |
| `/api/admin/permission-policies` | POST | Create permission-policy mapping |
| `/api/admin/permission-policies/{id}` | DELETE | Deactivate mapping |

### Frontend (Control Center)

| File | Description |
|------|-------------|
| `types/control-center.ts` | TypeScript types: PolicySummary, PolicyDetail, PolicyRule, etc. |
| `lib/api-mappers.ts` | Mapper functions for all policy response types |
| `lib/api-client.ts` | Added `policies` cache tag |
| `lib/control-center-api.ts` | Full policy + permission-policy API client methods |
| `lib/nav.ts` | Added Policies navigation entry under IDENTITY section |
| `app/policies/page.tsx` | Policy list page with product filter chips |
| `app/policies/[id]/page.tsx` | Policy detail page with breadcrumb navigation |
| `components/policies/policy-list-table.tsx` | Sortable policy list table |
| `components/policies/policy-create-dialog.tsx` | Modal dialog for creating policies |
| `components/policies/policy-detail-panel.tsx` | Tabbed detail view (Rules/Permissions/Info) |
| `components/policies/policy-rules-editor.tsx` | Visual rule builder with field/operator dropdowns |
| `components/policies/policy-permission-mappings.tsx` | Permission linking UI |
| `app/api/policies/route.ts` | Next.js API route for policy creation |
| `app/api/policies/[id]/rules/route.ts` | Next.js API route for rule creation |
| `app/api/policies/[id]/rules/[ruleId]/route.ts` | Next.js API route for rule deletion |
| `app/api/permission-policies/route.ts` | Next.js API route for mapping creation |
| `app/api/permission-policies/[id]/route.ts` | Next.js API route for mapping deactivation |

### Tests

| File | Tests | Description |
|------|-------|-------------|
| `PolicyEvaluationTests.cs` | 47 tests | Policy code validation, domain creation/update/deactivation, rule field validation, operator constraints, PermissionPolicy lifecycle |

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `Authorization:EnablePolicyEvaluation` | `false` | Enable ABAC policy evaluation in RequirePermissionFilter |
| `Authorization:EnableRoleFallback` | `true` | Existing PBAC role fallback toggle |

## Policy Enforcement Flow

1. User request hits endpoint with `[RequirePermission("CODE")]`
2. Filter checks permission claim exists (PBAC — existing)
3. If `EnablePolicyEvaluation=true`, filter calls `IPolicyEvaluationService.EvaluateAsync()`
4. Service loads active policies linked to the permission via PermissionPolicies
5. If no policies linked → allow (backward compatible)
6. Each policy's rules evaluated with AND/OR grouping against merged attributes
7. All policies must pass → allow; any failure → deny with explainable result
8. Resource context injectable via `httpContext.Items["PolicyResourceContext"]`

## Build & Test Status

- **dotnet build**: PASS (0 errors, 0 warnings)
- **dotnet test**: PASS (153 total, 0 failures)
- **TypeScript tsc --noEmit**: PASS (0 errors)
