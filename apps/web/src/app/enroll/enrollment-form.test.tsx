import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { EnrollmentForm } from './enrollment-form';

const { registerEnrollmentMock, registerFirmEnrollmentMock, sendOtpMock, pushMock } = vi.hoisted(() => ({
  registerEnrollmentMock: vi.fn(),
  registerFirmEnrollmentMock: vi.fn(),
  sendOtpMock: vi.fn(),
  pushMock: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock('./actions', () => ({
  sendOtp: sendOtpMock,
  registerEnrollment: registerEnrollmentMock,
  registerFirmEnrollment: registerFirmEnrollmentMock,
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

  test('blocks submit when phone number is not 10 digits', async () => {
    const user = userEvent.setup();

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
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

    await user.type(screen.getByPlaceholderText('(555) 000-0000'), '123');
    await user.type(screen.getByPlaceholderText('First'), 'Taylor');
    await user.type(screen.getByPlaceholderText('At least 8 characters'), 'password123');
    await user.type(screen.getByPlaceholderText('Re-enter password'), 'password123');
    await user.click(screen.getByLabelText(/i agree to the/i));
    await user.click(screen.getByRole('button', { name: /activate my portal access/i }));

    expect(screen.getAllByText('Phone number must be 10 digits.').length).toBeGreaterThan(0);
    expect(registerEnrollmentMock).not.toHaveBeenCalled();
  });
});
