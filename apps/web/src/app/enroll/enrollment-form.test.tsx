import { render, screen } from '@testing-library/react';
import { describe, expect, test, vi } from 'vitest';
import { EnrollmentForm } from './enrollment-form';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('./actions', () => ({
  sendOtp: vi.fn(),
  registerEnrollment: vi.fn(),
  registerFirmEnrollment: vi.fn(),
}));

describe('EnrollmentForm', () => {
  test('disables prefilled company name and email fields', () => {
    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '555-0101',
          addressLine1: '123 Main',
          city: 'Las Vegas',
          state: 'NV',
          postalCode: '89101',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    expect(screen.getByDisplayValue('Demo Provider')).toBeDisabled();
    expect(screen.getByDisplayValue('provider@example.com')).toBeDisabled();
  });
});
