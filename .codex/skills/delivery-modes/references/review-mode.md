# Review Mode

Use review mode when the user wants assessment without implementation.

If the prompt includes `delivery-modes auto`, spawn `reviewer` first and add `security-engineer`, `qa-engineer`, or `architect` only when the review scope calls for them.

## Goals

- Identify bugs, regressions, and security risks
- Assess architecture fit and maintainability
- Evaluate validation depth and missing tests
- Determine whether the change is ready

## Required Behavior

- Do not edit files unless the user explicitly asks for fixes
- Findings come before summary
- Order findings by severity
- Include file references where possible
- Distinguish observed issues from inferred risks
- State skipped validation or unverified assumptions

## Agent Selection

Required:
- reviewer

Optional:
- qa-engineer for test-gap analysis, regression thinking, or validation depth
- security-engineer for security-focused review
- architect for architecture-focused review

Usually unnecessary:
- backend-engineer
- frontend-engineer
- database-engineer
- devops-engineer

## Review Focus Areas

- correctness
- regressions
- input validation
- auth and authorization
- tenant isolation
- secret handling
- logging of sensitive data
- performance and maintainability
- missing tests
- deployment or migration risk when applicable

## Recommended Output Shape

Use a structure like:

```md
1. [Severity] Finding with file reference and concise explanation.
2. [Severity] Finding with file reference and concise explanation.

Open questions:
- ...

Summary:
- ...
```

If no findings are discovered, say that explicitly and mention residual risks or validation gaps.
