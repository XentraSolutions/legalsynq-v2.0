---
name: refactor-feature
description: Safely refactor a specific feature, screen, module, service, or directory without changing its behavior. Use when Codex is asked to restructure feature code, extract components or helpers into files, split large files, improve module boundaries, remove duplication, rename or reorganize feature internals, or perform another targeted architecture cleanup while preserving public contracts and running focused validation.
---

# Refactor Feature

Restructure only the requested feature while preserving observable behavior, public contracts, and repository conventions.

## Workflow

1. Resolve the exact target from the request and repository structure.
2. Read every applicable `AGENTS.md`, package manifest, and nearby barrel/export file before editing.
3. Check `git status --short`. Preserve unrelated user changes and identify overlapping edits.
4. Inspect the target completely before choosing boundaries:
   - public exports and consumers;
   - local components, hooks, helpers, types, constants, and fixtures;
   - tests and mocks;
   - circular-dependency risks;
   - framework-specific client/server or service boundaries.
5. State the intended file layout briefly when the refactor creates or moves multiple files.
6. Make the smallest behavior-preserving change that satisfies the request.
7. Update imports, exports, tests, mocks, and type references together.
8. Run formatting when needed, then the narrowest meaningful type-check, lint, and tests.
9. Run `git diff --check` and inspect the final diff/status for omissions or unrelated churn.

## Refactor Rules

- Do not change user-visible behavior, API payloads, persistence, authorization, navigation, or analytics unless explicitly requested.
- Keep public imports stable when practical. Use a barrel or compatibility re-export only when existing consumers require it.
- Put each extracted React component in a file named exactly after the component when requested.
- Move shared types, constants, and pure helpers to neutral modules when multiple extracted files need them. Avoid importing shared runtime values back from a barrel that imports those consumers; this creates circular dependencies.
- Keep hooks at component top level and preserve hook call order.
- Preserve component props and type them explicitly. Prefer `import type` for type-only dependencies.
- Keep tests near the established feature location. Add or update regression coverage only where the structural change affects import boundaries, mocks, or explicitly requested behavior.
- Do not introduce a new dependency for a structural refactor unless the user authorizes it.
- Do not combine cleanup outside the target feature with the requested refactor.
- Do not use generated scripts to rewrite source unless the transformation is deterministic, reviewed afterward, and safer than focused patches.

## Common Layout

For a large screen or feature file, prefer this shape when it matches local conventions:

```text
FeatureName/
├── index.tsx                 # public screen/component orchestration
├── ComponentName.tsx         # one extracted component
├── AnotherComponent.tsx
├── types.ts                  # shared feature-local types
├── constants.ts              # shared static data
├── helpers.ts                # pure transformations
└── index.test.tsx            # existing test convention, if applicable
```

Do not create every optional file automatically. Create only boundaries supported by actual reuse or complexity.

## Validation

Derive commands from the affected workspace and repository instructions. At minimum:

- run the workspace type-check for TypeScript refactors;
- run lint for the affected workspace;
- run the closest feature or screen tests;
- run build/tests appropriate to the affected C# project for backend refactors;
- run `git diff --check`.

If validation cannot run, report the exact command and blocker. Never claim behavior preservation solely because compilation succeeds.

## Completion Report

Report:

- the resulting structure and important boundaries;
- confirmation that behavior/contracts were preserved, with any exceptions;
- validation commands and results;
- any remaining risk, skipped validation, or required follow-up.
