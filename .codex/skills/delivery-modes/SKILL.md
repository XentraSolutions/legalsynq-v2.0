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

## Delegation Convention

Interpret these phrases literally:

- `delivery-modes`
  - Apply the selected mode rules only.
  - Do not spawn sub-agents.
  - Work in-process unless the user separately asks for delegation.

- `delivery-modes auto`
  - The user has explicitly authorized delegation.
  - Spawn the minimum required sub-agents for the selected mode.
  - Run agents in parallel only when file ownership does not overlap.
  - Do not spawn unnecessary agents.

If the prompt does not contain `auto`, do not spawn agents.

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

## Auto Delegation Rules

When the prompt includes `delivery-modes auto`:

Planning mode:
- Spawn `planner`
- Spawn `architect` only for cross-service, high-risk, or large refactor work
- Spawn `security-engineer` only for security-heavy requests

Implementation mode:
- Spawn only the required implementation agents:
  - `backend-engineer`
  - `frontend-engineer`
  - `database-engineer`
  - `devops-engineer`
  - `security-engineer`
- Spawn `qa-engineer` only when test authoring or deeper validation is needed
- Spawn `reviewer` before final completion for substantial implementation

Review mode:
- Spawn `reviewer`
- Spawn `security-engineer` only for security-focused review
- Spawn `qa-engineer` only for test-depth or regression-focused review
- Spawn `architect` only for architecture-focused review

## Mode References

Read only the relevant reference file for the selected mode:

- Planning mode: [references/planning-mode.md](references/planning-mode.md)
- Implementation mode: [references/implementation-mode.md](references/implementation-mode.md)
- Review mode: [references/review-mode.md](references/review-mode.md)
