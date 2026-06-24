import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { EnrollmentForm } from './enrollment-form';

const { registerEnrollmentMock, registerFirmEnrollmentMock, sendOtpMock, pushMock } = vi.hoisted(() => ({
  registerEnrollmentMock: vi.fn(),
  registerFirmEnrollmentMock: vi.fn(),
  sendOtpMock: vi.fn(),
  pushMock: vi.fn(),
}));

const addressSuggestion = {
  displayName: '885 Sample Rd, Atlanta, GA 30316',
  addressLine1: '885 Sample Rd',
  city: 'Atlanta',
  state: 'GA',
  postalCode: '30316',
  addressSelectionToken: 'signed-address-token',
};

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

vi.mock('./actions', () => ({
  sendOtp: sendOtpMock,
  registerEnrollment: registerEnrollmentMock,
  registerFirmEnrollment: registerFirmEnrollmentMock,
}));

describe('EnrollmentForm', () => {
  test.beforeEach(() => {
    vi.useRealTimers();
    vi.stubGlobal('fetch', vi.fn());
    registerEnrollmentMock.mockReset();
    registerFirmEnrollmentMock.mockReset();
    sendOtpMock.mockReset();
    pushMock.mockReset();
  });

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

  test('prefills first and last name from enrollment prefill data', () => {
    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '555-0101',
          firstName: 'Ralph',
          lastName: 'Lopez',
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

    expect(screen.getByDisplayValue('Ralph')).toBeDisabled();
    expect(screen.getByDisplayValue('Lopez')).toBeDisabled();
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

    await user.type(screen.getByPlaceholderText('Enter 10-digit phone number'), '123');
    await user.type(screen.getByPlaceholderText('Enter first name'), 'Taylor');
    await user.type(screen.getByPlaceholderText('Create password'), 'password123');
    await user.type(screen.getByPlaceholderText('Re-enter password'), 'password123');
    await user.click(screen.getByLabelText(/i agree to the/i));
    await user.click(screen.getByRole('button', { name: /activate my portal access/i }));

    expect(screen.getAllByText('Phone number must be 10 digits.').length).toBeGreaterThan(0);
    expect(registerEnrollmentMock).not.toHaveBeenCalled();
  });

  test('shows inline error and blocks submit when auto-populated ZIP is changed', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [addressSuggestion],
    } as Response);

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
          addressLine1: '',
          city: '',
          state: '',
          postalCode: '',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    await user.type(screen.getByPlaceholderText('Enter street address'), '885 Sample');
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await screen.findByText(addressSuggestion.displayName);
    await user.click(screen.getByText(addressSuggestion.displayName));
    await user.clear(screen.getByPlaceholderText('Enter ZIP code'));
    await user.type(screen.getByPlaceholderText('Enter ZIP code'), '30317');
    await user.type(screen.getByPlaceholderText('Enter first name'), 'Taylor');
    await user.type(screen.getByPlaceholderText('Create password'), 'password123');
    await user.type(screen.getByPlaceholderText('Re-enter password'), 'password123');
    await user.click(screen.getByLabelText(/i agree to the/i));
    await user.click(screen.getByRole('button', { name: /activate my portal access/i }));

    expect(screen.getAllByText('ZIP code must match the selected address.').length).toBeGreaterThan(0);
    expect(registerEnrollmentMock).not.toHaveBeenCalled();
  });

  test('submits when auto-populated ZIP is unchanged', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [addressSuggestion],
    } as Response);
    registerEnrollmentMock.mockResolvedValue({ ok: true });

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
          addressLine1: '',
          city: '',
          state: '',
          postalCode: '',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    await user.type(screen.getByPlaceholderText('Enter street address'), '885 Sample');
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await screen.findByText(addressSuggestion.displayName);
    await user.click(screen.getByText(addressSuggestion.displayName));
    await user.type(screen.getByPlaceholderText('Enter first name'), 'Taylor');
    await user.type(screen.getByPlaceholderText('Create password'), 'password123');
    await user.type(screen.getByPlaceholderText('Re-enter password'), 'password123');
    await user.click(screen.getByLabelText(/i agree to the/i));
    await user.click(screen.getByRole('button', { name: /activate my portal access/i }));

    expect(registerEnrollmentMock).toHaveBeenCalledWith(expect.objectContaining({
      postalCode: '30316',
      addressSelectionToken: 'signed-address-token',
    }));
    expect(pushMock).toHaveBeenCalledWith('/enroll/welcome');
  });

  test('clears ZIP mismatch when address fields are changed after autocomplete', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [addressSuggestion],
    } as Response);

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
          addressLine1: '',
          city: '',
          state: '',
          postalCode: '',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    await user.type(screen.getByPlaceholderText('Enter street address'), '885 Sample');
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await screen.findByText(addressSuggestion.displayName);
    await user.click(screen.getByText(addressSuggestion.displayName));
    await user.clear(screen.getByPlaceholderText('Enter ZIP code'));
    await user.type(screen.getByPlaceholderText('Enter ZIP code'), '30317');

    expect(screen.getByText('ZIP code must match the selected address.')).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText('Enter city'), ' East');

    expect(screen.queryByText('ZIP code must match the selected address.')).not.toBeInTheDocument();
  });

  test('restoring the exact selected ZIP allows submit again', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [addressSuggestion],
    } as Response);
    registerEnrollmentMock.mockResolvedValue({ ok: true });

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
          addressLine1: '',
          city: '',
          state: '',
          postalCode: '',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    await user.type(screen.getByPlaceholderText('Enter street address'), '885 Sample');
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await screen.findByText(addressSuggestion.displayName);
    await user.click(screen.getByText(addressSuggestion.displayName));
    await user.clear(screen.getByPlaceholderText('Enter ZIP code'));
    await user.type(screen.getByPlaceholderText('Enter ZIP code'), '30317');

    expect(screen.getByText('ZIP code must match the selected address.')).toBeInTheDocument();

    await user.clear(screen.getByPlaceholderText('Enter ZIP code'));
    await user.type(screen.getByPlaceholderText('Enter ZIP code'), addressSuggestion.postalCode);
    await user.type(screen.getByPlaceholderText('Enter first name'), 'Taylor');
    await user.type(screen.getByPlaceholderText('Create password'), 'password123');
    await user.type(screen.getByPlaceholderText('Re-enter password'), 'password123');
    await user.click(screen.getByLabelText(/i agree to the/i));
    await user.click(screen.getByRole('button', { name: /activate my portal access/i }));

    expect(screen.queryByText('ZIP code must match the selected address.')).not.toBeInTheDocument();
    expect(registerEnrollmentMock).toHaveBeenCalledWith(expect.objectContaining({
      postalCode: '30316',
      addressSelectionToken: 'signed-address-token',
    }));
    expect(pushMock).toHaveBeenCalledWith('/enroll/welcome');
  });

  test('keeps ZIP mismatch when the entered ZIP only shares the same first five digits', async () => {
    const user = userEvent.setup();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => [addressSuggestion],
    } as Response);

    render(
      <EnrollmentForm
        prefill={{
          providerId: 'provider-123',
          companyName: 'Demo Provider',
          companyType: 'Provider',
          email: 'provider@example.com',
          phone: '',
          addressLine1: '',
          city: '',
          state: '',
          postalCode: '',
        }}
        providerId="provider-123"
        tenantId="tenant-123"
        referralPrefill={null}
        isFirmEnrollment={false}
      />,
    );

    await user.type(screen.getByPlaceholderText('Enter street address'), '885 Sample');
    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    await screen.findByText(addressSuggestion.displayName);
    await user.click(screen.getByText(addressSuggestion.displayName));
    await user.clear(screen.getByPlaceholderText('Enter ZIP code'));
    await user.type(screen.getByPlaceholderText('Enter ZIP code'), '30316-1234');

    expect(screen.getByText('ZIP code must match the selected address.')).toBeInTheDocument();
  });
});
