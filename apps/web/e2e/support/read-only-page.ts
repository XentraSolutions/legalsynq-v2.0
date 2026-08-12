import type { Page, Locator } from '@playwright/test';

/**
 * Type-level read-only surface over Playwright's Page/Locator, used to gate
 * e2e specs that run against live environments (including production) away
 * from accidentally performing a data mutation.
 *
 * This is enforced by `tsc`, not a lint rule or a code-review habit: a spec
 * built on readonly-test.ts receives `page: ReadOnlyPage`, not the real
 * `Page`. Calling a method that isn't listed here — `.fill()`, `.type()`,
 * `.press()`, `.check()`, `.selectOption()`, `.setInputFiles()`, `.dragTo()`,
 * `.evaluate()` — is a compile error and won't even show up in autocomplete.
 * Working around it needs a visible, deliberate `as unknown as Page` cast,
 * which is easy to flag in review — unlike an accidental mutating call that
 * happens to type-check.
 *
 * `.click()` is intentionally the one interactive action allowed, and only
 * on locators obtained via `getByRole('link', ...)` (see ReadOnlyLinkLocator
 * below) — a link click is a same-origin GET navigation, not a form
 * submission or a state change. Every other role (button, checkbox, ...)
 * only ever yields a plain ReadOnlyLocator with no click(). This still can't
 * fully prove a given link never triggers a destructive action server-side —
 * types can't reason about what a route handler does — so specs written
 * against this module should still stick to the documented policy: read-only
 * assertions and navigation only, never a page whose interaction submits
 * user-entered data.
 *
 * Deliberately omitted (in addition to input/click methods above), because
 * each is its own escape hatch back to an unrestricted object:
 *   - locator.page()        → returns the real, unrestricted Page
 *   - page.context()        → BrowserContext: new pages, cookies, permissions
 *   - page.request          → APIRequestContext: raw POST/PUT/DELETE calls
 *   - page.keyboard/mouse    → low-level input, bypasses locator restrictions
 *   - evaluate/evaluateHandle → arbitrary JS in the page context
 *   - elementHandle()        → ElementHandle has its own click()/fill()
 */

type GetByRoleArgs = Parameters<Locator['getByRole']>;
type AriaRole = GetByRoleArgs[0];
type GetByRoleOptions = GetByRoleArgs[1];

export interface ReadOnlyLocator {
  getByText(...args: Parameters<Locator['getByText']>): ReadOnlyLocator;
  getByLabel(...args: Parameters<Locator['getByLabel']>): ReadOnlyLocator;
  getByPlaceholder(...args: Parameters<Locator['getByPlaceholder']>): ReadOnlyLocator;
  getByTestId(...args: Parameters<Locator['getByTestId']>): ReadOnlyLocator;
  locator(...args: Parameters<Locator['locator']>): ReadOnlyLocator;
  filter(...args: Parameters<Locator['filter']>): ReadOnlyLocator;

  getByRole(role: 'link', options?: GetByRoleOptions): ReadOnlyLinkLocator;
  getByRole(role: Exclude<AriaRole, 'link'>, options?: GetByRoleOptions): ReadOnlyLocator;

  first(): ReadOnlyLocator;
  last(): ReadOnlyLocator;
  nth(index: number): ReadOnlyLocator;

  isVisible(): Promise<boolean>;
  isHidden(): Promise<boolean>;
  isEnabled(): Promise<boolean>;
  isDisabled(): Promise<boolean>;
  isChecked(): Promise<boolean>;
  count(): Promise<number>;
  textContent(): Promise<string | null>;
  innerText(): Promise<string>;
  innerHTML(): Promise<string>;
  getAttribute(name: string): Promise<string | null>;
  boundingBox(): ReturnType<Locator['boundingBox']>;
  waitFor(options?: Parameters<Locator['waitFor']>[0]): Promise<void>;
  allTextContents(): Promise<string[]>;
}

/** Only obtainable via getByRole('link', ...) — see the module doc above for why. */
export interface ReadOnlyLinkLocator extends ReadOnlyLocator {
  click(options?: Parameters<Locator['click']>[0]): Promise<void>;
}

export interface ReadOnlyPage {
  goto(...args: Parameters<Page['goto']>): ReturnType<Page['goto']>;
  url(): string;
  waitForURL(...args: Parameters<Page['waitForURL']>): Promise<void>;
  waitForLoadState(...args: Parameters<Page['waitForLoadState']>): Promise<void>;
  reload(...args: Parameters<Page['reload']>): ReturnType<Page['reload']>;
  title(): Promise<string>;
  setViewportSize(size: { width: number; height: number }): Promise<void>;

  getByText(...args: Parameters<Page['getByText']>): ReadOnlyLocator;
  getByLabel(...args: Parameters<Page['getByLabel']>): ReadOnlyLocator;
  getByPlaceholder(...args: Parameters<Page['getByPlaceholder']>): ReadOnlyLocator;
  getByTestId(...args: Parameters<Page['getByTestId']>): ReadOnlyLocator;
  locator(...args: Parameters<Page['locator']>): ReadOnlyLocator;

  getByRole(role: 'link', options?: GetByRoleOptions): ReadOnlyLinkLocator;
  getByRole(role: Exclude<AriaRole, 'link'>, options?: GetByRoleOptions): ReadOnlyLocator;
}
