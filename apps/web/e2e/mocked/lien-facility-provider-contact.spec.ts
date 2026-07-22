import { test, expect } from '@playwright/test';
import { baseSelectTrigger } from '../support/base-select';
import {
  MOCK_CASE_ID,
  MOCK_LIEN_ID,
  FACILITY_DISPLAY_NAME,
  CONTACT_PERSON_DISPLAY_NAME,
  PROVIDER_DISPLAY_NAME,
} from '../../src/mocks/handlers/lien';

/**
 * Proves the ContactEntitySelect resolution fix (src/components/lien/
 * contact-entity-select.tsx) on the real lien case detail page, using a
 * backend response the real one can't currently produce.
 *
 * KNOWN BACKEND GAP this fixture works around: get-facility/update-facility
 * persists `facilityId` correctly but silently drops `facilityContactId`
 * and `medicalProviderId` — confirmed manually and documented in
 * e2e/(platform)/lien/mutations/facility-provider-contact.spec.ts, which
 * deliberately does NOT assert those two fields persist for that reason.
 * Here, src/mocks/handlers/lien.ts's get-facility handler returns all three
 * populated, so this spec verifies the frontend correctly displays them
 * once the backend does — independent of when that backend defect gets
 * fixed. Also exercises the harder of the two resolution paths: the
 * facility's own id (FACILITY_LEGACY_ID) is deliberately different from its
 * contact id, forcing the facilityId-scoped fallback rather than a direct
 * by-id lookup — see the fixture comment in src/mocks/handlers/lien.ts.
 *
 * No real backend, no real session — see playwright.mocked.config.ts's doc
 * comment for how MSW intercepts at the gateway boundary so the real BFF
 * route/layout guard code still runs.
 */

test.describe('Lien case detail — Medical Facility and Provider Information (mocked backend)', () => {
  test('displays the saved Facility, Contact Person, and Provider once the backend returns them', async ({
    page,
  }) => {
    await page.context().addCookies([
      {
        name: 'platform_session',
        value: 'mock-session-token',
        url: 'http://localhost:3001',
      },
    ]);

    await page.goto(`/lien/cases/${MOCK_CASE_ID}/liens/${MOCK_LIEN_ID}`);

    await expect(
      page.getByText('Medical Facility and Provider Information'),
    ).toBeVisible();

    await expect(baseSelectTrigger(page, 'Facility Name')).toHaveText(FACILITY_DISPLAY_NAME);
    await expect(baseSelectTrigger(page, 'Select Contact Person')).toHaveText(
      CONTACT_PERSON_DISPLAY_NAME,
    );
    await expect(baseSelectTrigger(page, 'Provider Name')).toHaveText(PROVIDER_DISPLAY_NAME);
  });
});
