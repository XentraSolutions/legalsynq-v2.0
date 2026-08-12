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

test('buildXeniaContext extracts SynqLien case and lien context', () => {
  const lien = buildXeniaContext(
    '/lien/cases/22222222-2222-2222-2222-222222222222/liens/33333333-3333-3333-3333-333333333333',
    new URLSearchParams('status=Active&caseNumber=CASE-1'),
    'drawer',
  );

  assert.deepEqual(lien.entity, {
    kind: 'lien',
    id: '33333333-3333-3333-3333-333333333333',
  });
  assert.equal(lien.route.product, 'lien');
  assert.equal(lien.route.entityType, 'lien');
  assert.deepEqual(lien.filters, {
    status: 'Active',
    caseNumber: 'CASE-1',
  });

  const caseContext = buildXeniaContext(
    '/lien/cases/22222222-2222-2222-2222-222222222222',
    new URLSearchParams(),
    'drawer',
  );

  assert.deepEqual(caseContext.entity, {
    kind: 'case',
    id: '22222222-2222-2222-2222-222222222222',
  });
});

test('buildStarterPrompts favors current SynqLien context', () => {
  const context = buildXeniaContext(
    '/lien/liens',
    new URLSearchParams('status=Draft'),
    'page',
  );

  assert.deepEqual(buildStarterPrompts('synqlien', context), [
    'Summarize my lien queue',
    'Find liens by client, case, or status',
    'Which liens need attention first?',
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
