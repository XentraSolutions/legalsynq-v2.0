# Fund Service (SynqFund)

Funding application workflow management.

**Port:** 5008

## Responsibilities

- Funding application lifecycle (Draft → Submitted → InReview → Approved / Rejected)
- Applicant data management
- Reviewer decision recording

## Layer Structure

```
Fund.Api/            Endpoints, middleware, Program.cs (port 5008)
Fund.Application/    Interfaces, DTOs, services
Fund.Domain/         FundingApplication, ApplicantInfo, ApplicationStatus
Fund.Infrastructure/ DbContext (FundDb), repositories, EF migrations
```

## Key Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/fund/applications` | Create application (Draft) |
| `GET` | `/api/fund/applications` | List applications |
| `GET` | `/api/fund/applications/{id}` | Application detail |
| `POST` | `/api/fund/applications/{id}/submit` | Submit for review |
| `POST` | `/api/fund/applications/{id}/begin-review` | Begin review (funder) |
| `POST` | `/api/fund/applications/{id}/approve` | Approve |
| `POST` | `/api/fund/applications/{id}/deny` | Deny |

## Product Roles

| Role | Access |
|---|---|
| `SYNQFUND_REFERRER` | Create and submit applications |
| `SYNQFUND_FUNDER` | Review and decide on applications |

## Database

`FundDb` (MySQL).

## Authorization Policies

- `AuthenticatedUser` — all authenticated requests
- `SynqFundAccess` — requires `SYNQFUND_REFERRER` or `SYNQFUND_FUNDER` product role
- `FunderOnly` — requires `SYNQFUND_FUNDER`
