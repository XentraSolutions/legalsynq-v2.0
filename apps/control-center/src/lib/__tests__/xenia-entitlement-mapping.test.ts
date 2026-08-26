import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mapTenantDetail, mapEntitlementResponse } from '../api-mappers';
import { mergeTenantEntitlements } from '../product-catalog';

test('mapEntitlementResponse keeps Xenia enabled when tenant service returns canonical Xenia code', () => {
  const mapped = mapEntitlementResponse({
    productCode: 'Xenia',
    productName: 'Xenia',
    enabled: true,
    status: 'Active',
    enabledAtUtc: '2026-07-13T08:00:00.000Z',
  });

  assert.equal(mapped.productCode, 'Xenia');
  assert.equal(mapped.enabled, true);
  assert.equal(mapped.status, 'Active');
});

test('mapTenantDetail normalizes legacy SYNQ_AI entitlements to a single Xenia card after merge', () => {
  const detail = mapTenantDetail({
    id: '019ea7f6-21e9-7421-ab54-7846cdc6bc76',
    code: 'qa-lsv2',
    displayName: 'QA-LSV2',
    status: 'Active',
    isActive: true,
    primaryContactName: '',
    userCount: 0,
    orgCount: 0,
    createdAtUtc: '2026-07-13T08:00:00.000Z',
    updatedAtUtc: '2026-07-13T08:05:00.000Z',
    activeUserCount: 0,
    productEntitlements: [
      {
        productCode: 'SYNQ_AI',
        productName: 'Xenia',
        enabled: true,
        status: 'Active',
      },
    ],
  });

  const merged = mergeTenantEntitlements(detail.productEntitlements);
  const xeniaItems = merged.filter(item => item.productCode === 'Xenia');
  const synqFundNamedXenia = merged.find(
    item => item.productCode === 'SynqFund' && item.productName === 'Xenia',
  );

  assert.equal(xeniaItems.length, 1);
  assert.equal(xeniaItems[0].enabled, true);
  assert.equal(synqFundNamedXenia, undefined);
});

test('maps and displays the SYNQ_SELLING tenant entitlement as Synq Selling', () => {
  const mapped = mapEntitlementResponse({
    productCode: 'SYNQ_SELLING',
    productName: 'SynqSelling',
    enabled: true,
    status: 'Active',
  });

  const selling = mergeTenantEntitlements([mapped]).find(
    item => item.productCode === 'SynqSelling',
  );

  assert.equal(selling?.productName, 'Synq Selling');
  assert.equal(selling?.enabled, true);
});
