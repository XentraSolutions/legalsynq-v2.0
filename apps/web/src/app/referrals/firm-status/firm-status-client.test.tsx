import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
  createdAtUtc: '2026-06-11T09:57:00Z',
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

  test('sends selected files through the public comment endpoint as referrer multipart data', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id: 'comment-1',
        senderType: 'referrer',
        senderName: 'Jane Intake',
        message: 'Please review the attachment.',
        createdAtUtc: '2026-06-11T10:00:00Z',
        attachments: [],
      }),
    }));

    const user = userEvent.setup();
    const { container } = render(
      <FirmStatusClient
        token="token-1"
        data={baseData}
        portalAccessStatus={ReferrerPortalAccessStatuses.NoAccount}
        loginUrl="/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1"
        enrollToken="enroll-1"
      />,
    );

    await user.type(screen.getByPlaceholderText(/Type your message here/), 'Please review the attachment.');
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, new File(['abc'], 'scan.png', { type: 'image/png' }));
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe('/api/public/careconnect/api/public/referrals/thread/comments?token=token-1');
    expect(init?.body).toBeInstanceOf(FormData);
    const form = init!.body as FormData;
    expect(form.get('senderType')).toBe('referrer');
    expect(form.get('message')).toBe('Please review the attachment.');
    expect(form.getAll('files')).toHaveLength(1);
  });

  test('sends selected files without message text as referrer multipart data', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id: 'comment-2',
        senderType: 'referrer',
        senderName: 'Jane Intake',
        message: '',
        createdAtUtc: '2026-06-11T10:05:00Z',
        attachments: [],
      }),
    }));

    const user = userEvent.setup();
    const { container } = render(
      <FirmStatusClient
        token="token-1"
        data={baseData}
        portalAccessStatus={ReferrerPortalAccessStatuses.NoAccount}
        loginUrl="/login?returnTo=%2Fcareconnect%2Freferrals%2Fref-1"
        enrollToken="enroll-1"
      />,
    );

    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, new File(['abc'], 'scan.png', { type: 'image/png' }));
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.body).toBeInstanceOf(FormData);
    const form = init!.body as FormData;
    expect(form.get('senderType')).toBe('referrer');
    expect(form.get('message')).toBe('');
    expect(form.getAll('files')).toHaveLength(1);
  });
});
