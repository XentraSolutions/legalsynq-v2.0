# E2E Tests (apps/web)

Two separate Playwright suites live here — don't confuse them:

| | Config | Command | What it hits |
|---|---|---|---|
| **e2e** (real) | `playwright.config.ts` | `pnpm test:e2e[:qa\|:prod]` | A live environment — no mocks |
| **mocked** | `playwright.mocked.config.ts` | `pnpm test:e2e:mocked` | Local dev server + an in-process mock identity API |

"e2e" in this repo means the first one. The mocked suite (`e2e/mocked/`) is a separate, hermetic
set of component/rendering checks CI runs on every PR — it's not part of this suite and shouldn't
be extended for new product coverage.

Adding a new test? Use the `create-e2e-test` skill (`.claude/skills/create-e2e-test/`) — it walks
through the readonly/mutations decision, file placement, and templates. This file is about
*running* what's already there.

## First-time setup

```bash
cp apps/web/e2e/data/credentials.example.json apps/web/e2e/data/credentials.json
```

Fill in real values. That file is gitignored — it won't exist on a fresh clone, and `getCredentials()`
throws a clear error pointing back here if it's missing.

## Environments

Selected via `E2E_ENV` (`e2e/config/environments.ts`), default `local`:

```bash
pnpm test:e2e              # E2E_ENV=local  — your machine's dev server + the QA backend
pnpm test:e2e:qa           # E2E_ENV=qa     — the deployed QA frontend directly, no local server
pnpm test:e2e:prod         # E2E_ENV=production — readonly/ specs only (see below); not configured yet
```

`local` and `qa` share one backend and tenant, so they share the `default` credentials entry per
platform in `credentials.json`. `production` reuses `default` too unless you add a `production`
key of its own.

**Port 3000**: if you already have `pnpm dev` running, `test:e2e`/`test:e2e:qa` reuse it — you
don't need to stop it first. If nothing's running, Playwright starts its own and tears it down
when the run finishes.

## readonly/ vs mutations/

Every product directory (`e2e/(platform)/<product>/`) splits into `readonly/` and `mutations/`.
`production` only ever discovers and runs `readonly/` — this is structural (`testMatch` in
`playwright.config.ts`), not just convention, and the `mutations/` fixture itself refuses to run
under `E2E_ENV=production` as a second, independent check. See `AGENTS.md` → "E2E Test Rules" for
the full guardrail.

## Running a single test / narrowing scope

```bash
npx playwright test e2e/\(platform\)/lien/readonly/login.spec.ts   # one file — escape/quote the parens
npx playwright test -g "SynqLien login"                            # by test/describe name
```

`E2E_ENV=qa` (or `production`) works as a prefix on any of these the same way.

## `--ui` — interactive runner

```bash
npx playwright test --ui
```

Browser-based, not an Electron window — works fine over SSH/remote/headless too. Prints a local
URL to open:

```
Listening on http://localhost:xxxxx
```

Gives you: every discovered test grouped by file (so you can see at a glance what's under
`readonly/` vs `mutations/`), a time-travel trace after each run (every action, DOM snapshot,
network call, console log — replayable step by step), and a watch mode that reruns a test
automatically on save. Keeping this open in a tab while editing a spec is the fastest feedback
loop day to day.

On a remote box, pin a known port instead of a random one:

```bash
npx playwright test --ui-port=9323 --ui-host=localhost
```

`--ui` respects `E2E_ENV` like any other run: `E2E_ENV=qa npx playwright test --ui`.

## `--debug` — step through one test

```bash
npx playwright test e2e/\(platform\)/lien/readonly/login.spec.ts --debug
```

Opens the Playwright Inspector: runs headed, pauses before each action, steps forward one action
at a time against the live page. Better than `--ui`'s trace viewer when you need to inspect state
*as it happens* rather than after the fact.

## `--headed` — just watch it run

```bash
npx playwright test --headed
```

No pausing, no inspector — a visible browser window instead of headless. Cheapest way to eyeball
a flaky test.

## Sanity-check a new readonly/ spec

```bash
E2E_ENV=production npx playwright test --list
```

Your new spec should appear here if and only if it's under `readonly/`. Worth running once per
new test, before it ever reaches a PR.
