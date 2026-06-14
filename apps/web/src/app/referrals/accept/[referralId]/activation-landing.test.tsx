import { render, screen } from '@testing-library/react';
import { describe, expect, test, vi } from 'vitest';
import { ActivationLanding } from './activation-landing';

vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href?: string; children?: any } & Record<string, unknown>) => (
    <a href={typeof href === 'string' ? href : ''} {...props}>{children}</a>
  ),
}));

vi.mock('./public-attachment-link', () => ({
  PublicAttachmentLink: () => null,
}));

describe('ActivationLanding', () => {
  test('passes the provider org name through the create-account CTA', () => {
    render(
      <ActivationLanding
        referralId="ref-123"
        token="abc123"
        summary={{
          referralId: 'ref-123',
          clientFirstName: 'Jane',
          clientLastName: 'Doe',
          referrerName: 'Demo Firm',
          providerName: 'Demo Provider Group',
          providerPhone: '555-0101',
          providerEmail: 'provider@example.com',
          providerAddressLine1: '123 Main',
          providerCity: 'Las Vegas',
          providerState: 'NV',
          providerPostalCode: '89101',
          requestedService: 'Physical Therapy',
          status: 'New',
          isAlreadyAccepted: false,
          providerHasAccount: false,
          attachments: [],
        }}
      />,
    );

    expect(screen.getByRole('link', { name: 'Activate & Accept Referral' })).toHaveAttribute(
      'href',
      '/referrals/activate?referralId=ref-123&token=abc123&companyName=Demo%20Provider%20Group',
    );
  });
});
