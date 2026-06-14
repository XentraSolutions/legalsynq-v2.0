import { render, screen } from '@testing-library/react';
import { beforeAll, beforeEach, describe, expect, test, vi } from 'vitest';
import { ThreadClient } from './thread-client';

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
      json: async () => [],
    }));
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
          createdAt: '2026-06-14T17:15:00Z',
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
});
