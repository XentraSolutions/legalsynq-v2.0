import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  buildStarterPrompts,
  buildXeniaContext,
  parseXeniaMessageMetadata,
} from '../xenia/context';

test('buildXeniaContext extracts referral detail entity and queue filters', () => {
  const detail = buildXeniaContext(
    '/careconnect/referrals/11111111-1111-1111-1111-111111111111',
    new URLSearchParams('status=New&search=Jane'),
    'drawer',
  );

  assert.deepEqual(detail.entity, {
    kind: 'referral',
    id: '11111111-1111-1111-1111-111111111111',
  });
  assert.equal(detail.route.product, 'careconnect');
  assert.equal(detail.route.entityType, 'referral');
  assert.deepEqual(detail.filters, {
    status: 'New',
    search: 'Jane',
  });
});

test('buildStarterPrompts favors current careconnect context', () => {
  const context = buildXeniaContext(
    '/careconnect/referrals',
    new URLSearchParams('status=New'),
    'page',
  );

  assert.deepEqual(buildStarterPrompts('careconnect', context), [
    'Summarize my referral queue',
    'Find referrals by client, provider, or referrer',
    'Which referrals need attention first?',
  ]);
});

test('parseXeniaMessageMetadata normalizes lookup results and prompts', () => {
  const metadata = parseXeniaMessageMetadata(JSON.stringify({
    lookupResults: [
      {
        kind: 'referral',
        id: 'ref-1',
        title: 'Jane Doe',
        subtitle: 'Atlas Medical',
        description: 'Case CASE-1',
        status: 'New',
        url: '/careconnect/referrals/ref-1',
        badges: ['Urgent'],
      },
    ],
    followUpPrompts: ['Show only New referrals'],
  }));

  assert.deepEqual(metadata, {
    lookupResults: [
      {
        kind: 'referral',
        id: 'ref-1',
        title: 'Jane Doe',
        subtitle: 'Atlas Medical',
        description: 'Case CASE-1',
        status: 'New',
        url: '/careconnect/referrals/ref-1',
        badges: ['Urgent'],
      },
    ],
    followUpPrompts: ['Show only New referrals'],
  });
});
