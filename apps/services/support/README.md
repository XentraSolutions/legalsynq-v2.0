# Support Service

Support case management for platform operators and tenants.

## Responsibilities

- Support case creation and tracking
- Case status lifecycle management
- Case assignment to support agents
- Audit event publication for all case lifecycle events

## Structure

```
Support.Api/     Endpoints, Program.cs
Support.Tests/   Test suite
```

## Database

`ConnectionStrings__Support` (MySQL).

## Notes

Support cases are surfaced in the Control Center dashboard (recent cases widget) and the Support section in the Control Center navigation. Counts and recent case lists are fetched via the gateway by the Control Center.
