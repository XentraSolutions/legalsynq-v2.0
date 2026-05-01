import { test, expect } from '@playwright/test';

/**
 * Visual/layout tests for the login page logo.
 *
 * Both LegalSynq logo files (legalsynq-logo-white.png and legalsynq-logo.png)
 * share the same intrinsic dimensions: 407 × 116 px → aspect ratio ≈ 3.51 : 1.
 *
 * CSS governs the rendered size:
 *   Desktop left-panel logo  [data-testid="ls-desktop-logo"]:  h-12 w-auto → ~48 px tall
 *   Mobile right-panel logo  [data-testid="ls-mobile-logo"]:   h-8  w-auto → ~32 px tall
 *
 * These tests fail if:
 *   - the wrong logo is visible at a given breakpoint
 *   - the rendered height falls outside the expected CSS-driven range
 *   - the aspect ratio deviates significantly (indicating distortion / squashing)
 *   - the logo overflows its container
 */

const VIEWPORTS = [
  { label: 'mobile',  width: 375,  height: 812  },
  { label: 'tablet',  width: 768,  height: 1024 },
  { label: 'desktop', width: 1280, height: 800  },
];

const LG_BREAKPOINT = 1024;

const LOGO_ASPECT_MIN = 3.0;
const LOGO_ASPECT_MAX = 4.1;

test.describe('Login page logo', () => {

  for (const vp of VIEWPORTS) {
    test(`renders correctly at ${vp.label} (${vp.width}px wide)`, async ({ page }) => {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      const isDesktop = vp.width >= LG_BREAKPOINT;

      if (isDesktop) {
        // ── Desktop: left-panel (white) logo must be visible ──────────────────

        // Mobile logo wrapper must be hidden at desktop breakpoint
        const mobileWrap = page.locator('[data-testid="ls-mobile-logo-wrap"]');
        await expect(mobileWrap).toBeHidden();

        const logo = page.locator('[data-testid="ls-desktop-logo"]');
        await expect(logo).toBeVisible();

        const box = await logo.boundingBox();
        expect(box, 'Desktop left-panel logo must have a bounding box').not.toBeNull();

        // CSS class h-12 resolves to 48px; allow ±4px for sub-pixel rounding
        expect(box!.height).toBeGreaterThanOrEqual(44);
        expect(box!.height).toBeLessThanOrEqual(52);

        // Width is auto-calculated from the intrinsic 407:116 ratio at 48px → ≈ 168px
        expect(box!.width).toBeGreaterThan(0);

        const ratio = box!.width / box!.height;
        expect(ratio, `Desktop logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeGreaterThan(LOGO_ASPECT_MIN);
        expect(ratio, `Desktop logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeLessThan(LOGO_ASPECT_MAX);

      } else {
        // ── Mobile / tablet: mobile logo must be visible ───────────────────────

        // Mobile logo wrapper is visible below the lg breakpoint
        const mobileWrap = page.locator('[data-testid="ls-mobile-logo-wrap"]');
        await expect(mobileWrap).toBeVisible();

        const logo = page.locator('[data-testid="ls-mobile-logo"]');
        await expect(logo).toBeVisible();

        const box = await logo.boundingBox();
        expect(box, 'Mobile logo must have a bounding box').not.toBeNull();

        // CSS class h-8 resolves to 32px; allow ±4px for sub-pixel rounding
        expect(box!.height).toBeGreaterThanOrEqual(28);
        expect(box!.height).toBeLessThanOrEqual(36);

        // Width is auto-calculated from the intrinsic 407:116 ratio at 32px → ≈ 112px
        expect(box!.width).toBeGreaterThan(0);

        const ratio = box!.width / box!.height;
        expect(ratio, `Mobile logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeGreaterThan(LOGO_ASPECT_MIN);
        expect(ratio, `Mobile logo aspect ratio should be ~3.51 : 1, got ${ratio.toFixed(2)}`).toBeLessThan(LOGO_ASPECT_MAX);
      }
    });
  }

  test('logo does not overflow its container at any standard width', async ({ page }) => {
    for (const vp of VIEWPORTS) {
      await page.setViewportSize({ width: vp.width, height: vp.height });
      await page.goto('/login');
      await page.waitForLoadState('networkidle');

      const isDesktop = vp.width >= LG_BREAKPOINT;
      const logo = page.locator(isDesktop ? '[data-testid="ls-desktop-logo"]' : '[data-testid="ls-mobile-logo"]');

      await expect(logo).toBeVisible();

      const logoBox      = await logo.boundingBox();
      const containerBox = await logo.locator('..').boundingBox();

      expect(logoBox,      `Logo bounding box must exist at ${vp.label}`).not.toBeNull();
      expect(containerBox, `Logo container bounding box must exist at ${vp.label}`).not.toBeNull();

      expect(logoBox!.width).toBeLessThanOrEqual(containerBox!.width   + 2);
      expect(logoBox!.height).toBeLessThanOrEqual(containerBox!.height + 2);
    }
  });

});
