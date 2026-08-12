import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { ThreadClient, formatDate } from './thread-client';

describe('ThreadClient', () => {
  beforeAll(() => {
    Object.defineProperty(window.HTMLElement.prototype, 'scrollIntoView', {
      configurable: true,
      value: vi.fn(),
    });
  });

  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        id: 'comment-1',
        senderType: 'provider',
        senderName: 'Demo Provider',
        message: 'See attached.',
        createdAtUtc: '2026-06-14T17:16:00Z',
        attachments: [],
      }),
    }));
  });

  test('formats thread timestamps without locale-dependent literals', () => {
    expect(formatDate('2026-08-03T08:20:00Z', 'UTC')).toBe('Aug 3, 2026, 8:20 AM');
  });

  test('passes the provider org name through the create-account CTA', () => {
    render(
      <ThreadClient
        token="abc123"
        loginUrl="/login"
        data={{
          referralId: 'ref-123',
          status: 'New',
          clientName: 'Jane Doe',
          clientPhone: null,
          clientEmail: null,
          clientDob: null,
          caseNumber: null,
          service: 'Physical Therapy',
          urgency: null,
          notes: null,
          providerName: 'Demo Provider Group',
          referrerName: 'Demo Firm',
          referrerEmail: 'firm@example.com',
          createdAtUtc: '2026-06-14T17:15:00Z',
          comments: [],
          attachments: [],
          providerHasAccount: false,
        }}
      />,
    );

    expect(screen.getByRole('link', { name: 'Activate free account' })).toHaveAttribute(
      'href',
      '/referrals/activate?referralId=ref-123&token=abc123&companyName=Demo%20Provider%20Group',
    );
  });

  test('sends selected files through the public comment endpoint as multipart form data', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <ThreadClient
        token="abc123"
        loginUrl="/login"
        data={{
          referralId: 'ref-123',
          status: 'New',
          clientName: 'Jane Doe',
          clientPhone: null,
          clientEmail: null,
          clientDob: null,
          caseNumber: null,
          service: 'Physical Therapy',
          urgency: null,
          notes: null,
          providerName: 'Demo Provider Group',
          referrerName: 'Demo Firm',
          referrerEmail: 'firm@example.com',
          createdAtUtc: '2026-06-14T17:15:00Z',
          comments: [],
          attachments: [],
          providerHasAccount: false,
        }}
      />,
    );

    await user.type(screen.getByPlaceholderText(/Type your message here/), 'See attached.');
    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, new File(['abc'], 'scan.png', { type: 'image/png' }));
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const [url, init] = vi.mocked(fetch).mock.calls[0];
    expect(url).toBe('/api/public/careconnect/api/public/referrals/thread/comments?token=abc123');
    expect(init?.method).toBe('POST');
    expect(init?.body).toBeInstanceOf(FormData);
    const form = init!.body as FormData;
    expect(form.get('senderType')).toBe('provider');
    expect(form.get('message')).toBe('See attached.');
    expect(form.getAll('files')).toHaveLength(1);
  });

  test('sends selected files without message text through the public comment endpoint', async () => {
    const user = userEvent.setup();
    const { container } = render(
      <ThreadClient
        token="abc123"
        loginUrl="/login"
        data={{
          referralId: 'ref-123',
          status: 'New',
          clientName: 'Jane Doe',
          clientPhone: null,
          clientEmail: null,
          clientDob: null,
          caseNumber: null,
          service: 'Physical Therapy',
          urgency: null,
          notes: null,
          providerName: 'Demo Provider Group',
          referrerName: 'Demo Firm',
          referrerEmail: 'firm@example.com',
          createdAtUtc: '2026-06-14T17:15:00Z',
          comments: [],
          attachments: [],
          providerHasAccount: false,
        }}
      />,
    );

    const input = container.querySelector('input[type="file"]') as HTMLInputElement;
    await user.upload(input, new File(['abc'], 'scan.png', { type: 'image/png' }));
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.body).toBeInstanceOf(FormData);
    const form = init!.body as FormData;
    expect(form.get('senderType')).toBe('provider');
    expect(form.get('message')).toBe('');
    expect(form.getAll('files')).toHaveLength(1);
  });
});
