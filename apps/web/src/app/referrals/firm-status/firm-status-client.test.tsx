import { render, screen } from '@testing-library/react';
import { beforeAll, describe, expect, test, vi } from 'vitest';
import { FirmStatusClient } from './firm-status-client';
import { ReferrerPortalAccessStatuses } from '@/types/careconnect';

const baseData = {
  referralId: 'ref-1',
  tenantId: 'tenant-1',
  status: 'New',
  clientName: 'Angelou Brown',
  service: 'General Referral',
  providerName: 'VITALAB HEALTHCARE INC',
  referrerName: 'Jane Intake',
  referrerEmail: 'jane@example.com',
  notes: null,
  createdAt: '2026-06-11T09:57:00Z',
  comments: [],
};

describe('FirmStatusClient portal CTA', () => {
  beforeAll(() => {
    Object.defineProperty(window.HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn(),
    });
  });

  test('renders login CTA for active in-tenant access', () => {
    render(
      <FirmStatusClient
        token="token-1"
        data={baseData}
        portalAccessStatus={ReferrerPortalAccessStatuses.ActiveInTenant}
        loginUrl="/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1"
        enrollToken="enroll-1"
      />,
    );

    expect(screen.getByRole('link', { name: 'Log in to CareConnect' })).toHaveAttribute(
      'href',
      '/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1',
    );
    expect(screen.queryByRole('link', { name: 'Get full portal access' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Link this network' })).not.toBeInTheDocument();
  });

  test('renders link-account CTA for cross-tenant existing users', () => {
    render(
      <FirmStatusClient
        token="token-1"
        data={baseData}
        portalAccessStatus={ReferrerPortalAccessStatuses.ExistingUserOtherTenant}
        loginUrl="/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1"
        enrollToken="enroll-1"
      />,
    );

    expect(screen.getByRole('link', { name: 'Link this network' })).toHaveAttribute(
      'href',
      '/enroll?token=enroll-1',
    );
    expect(screen.getByRole('link', { name: 'Log in to another account' })).toHaveAttribute(
      'href',
      '/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1',
    );
  });

  test('renders create-account CTA when no account exists', () => {
    render(
      <FirmStatusClient
        token="token-1"
        data={baseData}
        portalAccessStatus={ReferrerPortalAccessStatuses.NoAccount}
        loginUrl="/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1"
        enrollToken="enroll-1"
      />,
    );

    expect(screen.getByRole('link', { name: 'Get full portal access' })).toHaveAttribute(
      'href',
      '/enroll?token=enroll-1',
    );
    expect(screen.queryByRole('link', { name: 'Already have access? Log in' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Log in to another account' })).not.toBeInTheDocument();
  });
});
