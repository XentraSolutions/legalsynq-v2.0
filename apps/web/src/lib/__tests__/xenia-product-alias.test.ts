import { test } from 'node:test';
import assert from 'node:assert/strict';
import { resolveEnabledNavKeys } from '../nav';
import { FrontendProductCode, sessionHasProductAccess } from '../auth-guards';

test('resolveEnabledNavKeys maps uppercase XENIA to the xenia nav key', () => {
  const keys = resolveEnabledNavKeys(['CareConnect', 'XENIA']);
  assert.equal(keys.has('careconnect'), true);
  assert.equal(keys.has('xenia'), true);
});

test('sessionHasProductAccess accepts uppercase XENIA from auth/me for the Xenia route', () => {
  const allowed = sessionHasProductAccess(
    {
      isPlatformAdmin: false,
      isTenantAdmin: false,
      userProducts: ['XENIA'],
      enabledProducts: [],
    },
    FrontendProductCode.Xenia,
  );

  assert.equal(allowed, true);
});
