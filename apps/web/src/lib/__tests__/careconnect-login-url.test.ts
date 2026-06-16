import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  buildCareConnectLoginUrl,
  buildCareConnectReferralLoginUrl,
  buildCareConnectPortalLoginUrl,
  normalizeCareConnectPortalHost,
  isCareConnectCommonPortalHost,
} from '../careconnect-login-url';

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

test('accepts a full portal origin without duplicating the scheme', () => {
  const url = buildCareConnectLoginUrl('https://careconnect-qa.legalsynq.com');

  assert.equal(
    url,
    'https://careconnect-qa.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
  );
});

test('builds a CareConnect referral login URL for provider referral deep links', () => {
  const url = buildCareConnectReferralLoginUrl(
    'careconnect-demo.legalsynq.com',
    '/provider/referrals/11111111-1111-1111-1111-111111111111',
  );

  assert.equal(
    url,
    'https://careconnect-demo.legalsynq.com/login?returnTo=%2Fprovider%2Freferrals%2F11111111-1111-1111-1111-111111111111&reason=referral-view',
  );
});

test('builds a plain login URL for welcome screens when the env contains a full origin', () => {
  const url = buildCareConnectPortalLoginUrl('https://careconnect-qa.legalsynq.com');

  assert.equal(url, 'https://careconnect-qa.legalsynq.com/login');
});

test('normalizes a configured full origin to just the host for hostname comparisons', () => {
  const host = normalizeCareConnectPortalHost('https://careconnect-qa.legalsynq.com');

  assert.equal(host, 'careconnect-qa.legalsynq.com');
});

test('isCareConnectCommonPortalHost matches the configured common portal hostname', () => {
  assert.equal(
    isCareConnectCommonPortalHost('careconnect.legalsynq.com', 'careconnect.legalsynq.com'),
    true,
  );
});

test('isCareConnectCommonPortalHost matches case-insensitively and ignores the port', () => {
  assert.equal(
    isCareConnectCommonPortalHost('CareConnect.LegalSynq.com:443', 'careconnect.legalsynq.com'),
    true,
  );
});

test('isCareConnectCommonPortalHost returns false for an unrelated tenant subdomain', () => {
  assert.equal(
    isCareConnectCommonPortalHost('acme-law.legalsynq.com', 'careconnect.legalsynq.com'),
    false,
  );
});

test('isCareConnectCommonPortalHost returns false when no common portal hostname is configured', () => {
  assert.equal(
    isCareConnectCommonPortalHost('careconnect.legalsynq.com', ''),
    false,
  );
});

test('isCareConnectCommonPortalHost uses the first entry of a comma-separated forwarded-host chain', () => {
  assert.equal(
    isCareConnectCommonPortalHost('careconnect.legalsynq.com, evil.example.com', 'careconnect.legalsynq.com'),
    true,
  );
});
