# Planning Mode

Use planning mode for analysis-only work.

If the prompt includes `delivery-modes auto`, spawn `planner` first and add other planning agents only when the task clearly warrants them.

## Goals

- Classify the request
- Identify affected areas
- Select the minimum required agents
- Define file ownership
- Define sequencing and safe parallel work
- Define targeted validation
- Surface risks and open questions

## Required Behavior

- Do not edit files
- Do not implement code
- Inspect only the code needed to plan correctly
- Prefer existing repository patterns over assumptions
- Treat ambiguous requests as planning-first

## Agent Selection

Required:
- planner

Optional:
- architect for multi-service design, large refactors, system boundaries, or contract design
- security-engineer when the task centers on auth, authorization, secrets, tenant isolation, or vulnerability risk

Usually unnecessary:
- backend-engineer
- frontend-engineer
- database-engineer
- devops-engineer
- qa-engineer
- reviewer

## Output Expectations

Produce:

- request type
- complexity
- affected files or modules
- required agents
- unnecessary agents when useful
- ownership boundaries
- sequential dependencies
- safe parallel groups
- validation plan
- risks
- open questions

## Recommended Output Shape

Use a structure like:

```md
# Feature Analysis

## Goal
## Request Type
## Complexity
## Affected Areas
## Required Agents
## File Ownership
## Parallelization Plan
## Execution Order
## Validation Plan
## Risks
## Open Questions
## Recommendation
```
