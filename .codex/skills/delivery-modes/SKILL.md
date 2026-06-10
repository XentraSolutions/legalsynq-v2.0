---
name: delivery-modes
description: |
  Use when the user wants planning mode, implementation mode, or review mode for repository work.
  Routes the task into the correct workflow, selects the right local agents, enforces whether code edits are allowed,
  and defines the expected validation and final review behavior for each mode.
---

# Delivery Modes

Use this skill when the user wants one of these operating modes:

- planning mode
- implementation mode
- review mode

If the request is ambiguous, default to planning mode.

## Mode Selection

Use planning mode when the user says:

- plan
- analyze
- assess
- estimate
- break down
- do not change code

Use implementation mode when the user says:

- implement
- fix
- update
- create
- apply
- make the change

Use review mode when the user says:

- review
- audit
- inspect
- validate
- no edits

## Core Rules

- In planning mode, do not edit files.
- In review mode, do not edit files unless the user explicitly asks for fixes.
- In implementation mode, inspect existing patterns before editing.
- Use the minimum required agents.
- For non-trivial implementation, use reviewer before final completion.
- Use security-engineer whenever auth, authorization, session handling, CSRF, CORS, secrets, tenant isolation, or vulnerability remediation is central to the task.

## Agent Routing

Planning mode:
- planner is required
- architect is optional for cross-service, high-risk, or large refactor work
- security-engineer is optional for security-heavy requests

Implementation mode:
- Choose only the required implementation agents:
  - backend-engineer
  - frontend-engineer
  - database-engineer
  - devops-engineer
  - security-engineer
- qa-engineer is optional when test creation or validation depth is part of the task
- reviewer is required before final completion for substantial implementation

Review mode:
- reviewer is required
- qa-engineer is optional for deeper validation or test-gap analysis
- security-engineer is optional for security-focused review
- architect is optional for architecture-focused review

## Mode References

Read only the relevant reference file for the selected mode:

- Planning mode: [references/planning-mode.md](references/planning-mode.md)
- Implementation mode: [references/implementation-mode.md](references/implementation-mode.md)
- Review mode: [references/review-mode.md](references/review-mode.md)
