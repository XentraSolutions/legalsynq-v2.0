import { test } from 'node:test';
import assert from 'node:assert/strict';

import { buildCareConnectLoginUrl } from '../careconnect-login-url.ts';

test('builds an https CareConnect login URL when a shared portal host is configured', () => {
  const url = buildCareConnectLoginUrl('careconnect-demo.legalsynq.com');

  assert.equal(
    url,
    'https://careconnect-demo.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
  );
});

test('builds an http CareConnect login URL for localhost-style hosts', () => {
  const url = buildCareConnectLoginUrl('portal.localhost');

  assert.equal(
    url,
    'http://portal.localhost/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
  );
});

test('falls back to a same-origin login path when the shared portal host is unset', () => {
  const url = buildCareConnectLoginUrl('');

  assert.equal(
    url,
    '/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
  );
});
