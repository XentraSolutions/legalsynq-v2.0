# Implementation Mode

Use implementation mode when the user wants changes made.

If the prompt includes `delivery-modes auto`, spawn only the minimum required implementation agents and keep file ownership disjoint whenever possible.

## Goals

- Discover existing patterns before editing
- Make the smallest effective change
- Select only the agents needed for the affected layers
- Run the narrowest meaningful validation
- Prepare the work for final review

## Required Behavior

- Inspect relevant code paths before editing
- Preserve service boundaries and repo conventions
- Avoid unrelated cleanup
- Keep one owning agent per file whenever possible
- Do not use extra agents unless the task truly crosses boundaries
- For substantial work, use reviewer before final completion

## Agent Selection Matrix

Use backend-engineer for:

- ASP.NET APIs
- application or domain logic
- shared C# libraries
- auth logic in backend services
- service-to-service integrations

Use frontend-engineer for:

- Next.js routes
- UI components
- forms and client state
- BFF route handlers
- frontend API integration

Use database-engineer for:

- migrations
- DbContext changes
- schema mapping
- indexes
- SQL scripts

Use devops-engineer for:

- scripts
- CI/CD
- environment variables
- Docker
- runtime config
- gateway routing and ports

Use security-engineer for:

- auth and authorization hardening
- session and cookie security
- CSRF or CORS changes
- secret handling
- dependency vulnerability remediation
- tenant-isolation controls
- targeted security remediation

Use qa-engineer when:

- tests need to be added or updated as a distinct task
- acceptance criteria need explicit coverage analysis
- a deeper validation pass is needed beyond basic targeted checks

Always use reviewer before final completion when:

- multiple files or modules changed
- auth, security, billing, identity, migrations, or deployment were affected
- the change is non-trivial

## Validation Expectations

Run the narrowest meaningful checks for the changed area:

- `pnpm --dir apps/web type-check`
- `pnpm --dir apps/control-center type-check`
- targeted `pnpm test`
- targeted `dotnet build`
- targeted `dotnet test`
- relevant script tests under `scripts/tests`

If full validation is impractical, state exactly what was not run and why.

## Recommended Flow

1. Inspect the relevant code and current patterns.
2. Select the minimum required agents.
3. Make scoped edits.
4. Run targeted validation.
5. Run reviewer for substantial work.
6. Report changed areas, checks run, and residual risks.
