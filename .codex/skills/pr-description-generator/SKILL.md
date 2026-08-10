---
name: pr-description-generator
description: Generate a pull request summary and full PR description from local Git branch changes, including untracked files in the current working tree. Use when the user wants a PR description, pull request body, PR summary, or release-ready change summary for a source branch compared with a target/base branch. Inspect only local Git history, diffs, and untracked files, ask for the ticket/JIRA link when missing, infer change and test categories conservatively, and output a commit-style PR title/message followed by the required Markdown PR template. Do not require GitHub, GitLab, or other repository-service APIs.
---

# PR Description Generator

Generate an evidence-based PR description from changes committed on a local source branch relative to a local target branch.

## Workflow

1. Collect required inputs.
   - Determine the repository from the current working directory unless the user specifies a path.
   - Determine the source branch from the user's request. If omitted, use the current branch from `git branch --show-current` when it is non-empty; otherwise ask for the source branch.
   - Ask for the target/base branch if it is missing.
   - Always request the JIRA/ticket link if it has not already been provided. Do not invent or infer a ticket URL.

2. Validate the repository and refs using read-only commands.

```bash
git rev-parse --show-toplevel
git status --short
git rev-parse --verify <source>
git rev-parse --verify <target>
```

   - If either ref is unavailable locally, report which ref is missing and ask the user to make it available locally.
   - Do not fetch from a remote unless the user explicitly asks.
   - Do not checkout, reset, clean, stash, commit, merge, rebase, or otherwise modify the repository.
   - Record whether the source branch is currently checked out with `git branch --show-current`.
   - Include untracked files from the current working tree in the analysis, but distinguish them from committed branch changes because they are not part of any branch until added and committed.
   - Do not include modified/staged tracked working-tree changes in the branch comparison unless the user explicitly asks; the required extra working-tree scope is untracked files only.

3. Inspect the PR-shaped change set using the merge base. Prefer three-dot diff semantics because the goal is to describe what the source branch introduces relative to the target branch.

```bash
git merge-base <target> <source>
git log --oneline --no-merges <target>..<source>
git diff --stat <target>...<source>
git diff --name-status --find-renames <target>...<source>
git diff --find-renames <target>...<source>
```

   - Use `git log <target>..<source>` for source-only commits.
   - Use `git diff <target>...<source>` for content introduced since the common ancestor.
   - Inspect enough diff context to understand behavior, architecture, tests, migrations, configuration, and documentation changes.
   - For very large diffs, inspect the summary and changed-file list first, then inspect the most relevant files individually rather than dumping the entire diff.

4. Inspect untracked files in the current working tree and include them as intended PR changes.

```bash
git ls-files --others --exclude-standard
```

   - For each relevant untracked text file, inspect its contents directly or render a read-only synthetic diff against `/dev/null`:

```bash
git diff --no-index /dev/null "<untracked-file>" || true
```

   - `git diff --no-index` returns exit code `1` when differences are found; treat that as expected, not as a command failure.
   - Do not inspect ignored files unless the user explicitly asks.
   - For binary, generated, dependency, lock, vendor, build-output, or very large untracked files, avoid dumping full contents. Use the filename, type, size, and other safe metadata to determine relevance, and inspect only when useful.
   - Treat relevant untracked files as changes the user intends to include in the PR description.
   - Always disclose in `Notes` when untracked files contributed to the generated PR description, because they are not yet committed and therefore are not actually part of the branch or PR.
   - If the requested source branch is not currently checked out, state that the untracked files belong to the current working tree rather than to the named source ref. Still include them when the user asked for untracked files, but make this distinction explicit in `Notes`.

5. Derive the description only from observed changes.
   - Explain the user-visible or technical outcome, not just filenames.
   - Group related changes into concise bullets.
   - Mention meaningful implementation details when they help reviewers understand scope.
   - Do not claim a bug is fixed, a feature works, or tests pass unless the diff/history or user-provided information supports that claim.

6. Infer the change type conservatively.
   - Check **Bug fix** only when the changes clearly correct broken or incorrect behavior.
   - Check **New feature** only when the changes clearly add new behavior or capability.
   - Check **Breaking change** only when the diff clearly introduces an incompatible behavior, contract, schema, API, or workflow change.
   - Multiple boxes may be checked when genuinely applicable.
   - If none can be established confidently, leave all unchecked and explain the uncertainty in Notes.

7. Infer tests added from repository changes, not assumptions.
   - Check **unit** when new or materially updated unit tests are visible in the branch diff or relevant untracked files.
   - Check **integration** when new or materially updated integration/service/component tests are visible in the branch diff or relevant untracked files.
   - Check **e2e** when new or materially updated end-to-end tests are visible in the branch diff or relevant untracked files.
   - Leave a test type unchecked when there is no diff evidence that tests of that type were added or materially updated.
   - Do not interpret an existing test command, CI config, or test directory by itself as evidence that tests were added.
   - Do not claim tests were executed unless the user provides execution results or the current task explicitly runs them and observes the result.

8. Handle Evidence honestly.
   - Never invent screenshots, videos, URLs, or test artifacts.
   - If the user supplied evidence, include it.
   - Otherwise write `- N/A` or a concise placeholder such as `- Add screenshot/video if applicable` based on the nature of the changes.

9. Produce exactly two deliverables in this order. Do not omit the first deliverable unless the user explicitly asks for only the full PR body.

   **Deliverable 1 — PR commit message / title**
   - Always print the literal label `PR commit message` first.
   - On the next line, provide exactly one copyable Conventional Commit-style line using this format:

```text
<type>(<scope>): <ticket-key> <title>
```

   - Derive `<type>` primarily from the source branch prefix, case-insensitively:
     - `chore`, `maintenance`, `deps`, `dependency` -> `chore`
     - `feature`, `feat` -> `feat`
     - `bugfix`, `bug`, `fix`, `hotfix` -> `fix`
     - `refactor` -> `refactor`
     - `test`, `tests` -> `test`
     - `docs`, `doc` -> `docs`
     - `ci` -> `ci`
     - `build` -> `build`
     - `perf`, `performance` -> `perf`
   - If the branch prefix is not recognizable, infer the narrowest supported type from the observed diff. Prefer `feat` for new behavior, `fix` for corrective behavior, and `chore` for maintenance/tooling/configuration changes.
   - Derive `<scope>` from the actual affected platform/workspace/module, especially in monorepos. Treat examples such as `app/portal/api` as illustrative only; never hardcode that scope.
   - Prefer changed-path evidence over branch wording when the repository is a monorepo. Inspect the changed files and identify the narrowest meaningful workspace or platform boundary, such as `apps/<app>`, `packages/<package>`, `services/<service>`, `libs/<library>`, or another repository-specific top-level workspace convention.
   - Normalize common monorepo path prefixes into concise scopes while preserving the platform identity. Examples: `apps/portal/api/...` -> `app/portal/api`; `apps/admin/web/...` -> `app/admin/web`; `services/auth/...` -> `auth`; `packages/ui/...` -> `ui`.
   - If all meaningful changes are within one platform/workspace, use that platform/workspace as the scope even when the branch name is generic.
   - If changes span multiple submodules inside one platform, use their nearest meaningful shared platform scope. Example: changes in both `apps/portal/api/...` and `apps/portal/web/...` should usually use `app/portal` rather than arbitrarily choosing `api` or `web`.
   - If changes span multiple independent platforms/workspaces, do not fabricate a combined slash path. Use the smallest defensible shared domain when one exists; otherwise omit the scope parentheses and use `<type>: <ticket-key> <title>`.
   - Use the branch path as a secondary hint when it clearly names the affected workspace and agrees with the changed files. Remove branch-control segments from scope candidates, including the type prefix, ticket key, and obvious title words. Normalize separators to `/` and keep the scope concise.
   - For untracked files, include their paths when determining the intended affected platform/workspace, while remembering they are not part of the branch until committed.
   - Derive `<ticket-key>` from the supplied ticket/JIRA link first. Accept common keys such as `ABC-123`. If the link does not expose a recognizable key, look for one in the source branch name. Do not invent a ticket key.
   - Derive `<title>` in this order: (1) a clear human-readable title encoded in the source branch slug after type/scope/ticket segments, (2) a concise title inferred from the branch diff and commits. Do not copy noisy branch separators verbatim; convert slugs such as `add-user-filter` into `add user filter`.
   - Keep the title concise and sentence-cased after the ticket key: capitalize the first word of the title while preserving intentional acronyms and proper nouns. Do not end it with a period.
   - Example outputs:

```text
chore(app/portal/api): ABC-123 Update portal API dependencies
feat(app/portal): ABC-456 Add account filters
fix(auth): ABC-789 Prevent expired token reuse
```

   **Deliverable 2 — Full PR description (Markdown)**
   - Always print the literal label `Full PR description` before the Markdown body.
   - Use the template below and preserve its section order and checkbox syntax.

   Use this exact outer structure:

```text
PR commit message
<type>(<scope>): <ticket-key> <title>

Full PR description
<full Markdown template>
```

## Full PR Markdown Template

```markdown
## What does this PR do?
<!-- Tell us a bit about what this PR is supposed to do, what does it solve etc. -->
- <concise change summary>

## Evidence
<!-- Insert screenshots or videos to demonstrate change/fixes. -->
- <evidence or N/A>

## Type of change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)

## Tests Added
- [ ] unit
- [ ] integration
- [ ] e2e

## JIRA Task 
<ticket link>

## Notes
<!-- Additional notes for developers or testers. -->
- <reviewer/tester notes or N/A>
```

Replace checkbox markers with `[x]` only when supported by the observed diff. Populate `## JIRA Task` with the exact ticket link supplied by the user.

## Output Quality Rules

- Keep the PR commit message to one concise line.
- Never return only the full PR template when the normal workflow is requested; the standalone `PR commit message` must appear first.
- Do not use a prose summary as the first deliverable; use the required Conventional Commit-style title/message format.
- Make `What does this PR do?` specific enough for a reviewer to understand the purpose without reading every changed file.
- Prefer 1-5 concise bullets in `What does this PR do?` depending on scope.
- Put migration steps, config requirements, follow-up work, known limitations, or validation caveats in `Notes` when relevant.
- If there are no meaningful notes, use `- N/A`.
- Mention the compared branches outside the Markdown template only when useful for traceability; do not add extra sections to the required template.
- Preserve the user's ticket link exactly.
- Never fabricate evidence, testing, ticket information, validation results, branch changes, or untracked-file contents.
- When untracked files are included, add a concise Notes bullet such as `- Includes untracked working-tree files that must be added and committed before they can appear in the PR.`
