import { render, screen } from '@testing-library/react';
import { afterAll, beforeEach, describe, expect, test, vi } from 'vitest';

const {
  fetchEnrollmentPrefillMock,
  decodeEnrollmentTokenMock,
  fetchExistingEnrollmentPrefillMock,
  checkPortalAccessStatusMock,
  getServerSessionMock,
  enrollmentFormMock,
} = vi.hoisted(() => ({
  fetchEnrollmentPrefillMock: vi.fn(),
  decodeEnrollmentTokenMock: vi.fn(),
  fetchExistingEnrollmentPrefillMock: vi.fn(),
  checkPortalAccessStatusMock: vi.fn(),
  getServerSessionMock: vi.fn(),
  enrollmentFormMock: vi.fn(() => null),
}));

vi.mock('./actions', () => ({
  fetchEnrollmentPrefill: fetchEnrollmentPrefillMock,
  decodeEnrollmentToken: decodeEnrollmentTokenMock,
  fetchExistingEnrollmentPrefill: fetchExistingEnrollmentPrefillMock,
  checkPortalAccessStatus: checkPortalAccessStatusMock,
}));

vi.mock('./enrollment-form', () => ({
  EnrollmentForm: enrollmentFormMock,
}));

vi.mock('@/lib/session', () => ({
  getServerSession: getServerSessionMock,
}));

import EnrollPage from './page';

describe('EnrollPage', () => {
  const originalCommonPortalHostname = process.env.CC_COMMON_PORTAL_HOSTNAME;

  beforeEach(() => {
    fetchEnrollmentPrefillMock.mockReset();
    decodeEnrollmentTokenMock.mockReset();
    fetchExistingEnrollmentPrefillMock.mockReset();
    checkPortalAccessStatusMock.mockReset();
    getServerSessionMock.mockReset();
    enrollmentFormMock.mockClear();

    getServerSessionMock.mockResolvedValue(null);
    fetchExistingEnrollmentPrefillMock.mockResolvedValue(null);
    checkPortalAccessStatusMock.mockResolvedValue('no_account');
    process.env.CC_COMMON_PORTAL_HOSTNAME = 'careconnect-demo.legalsynq.com';
  });

  afterAll(() => {
    process.env.CC_COMMON_PORTAL_HOSTNAME = originalCommonPortalHostname;
  });

  test('does not prefill first name from the legacy contact claim when it matches the firm name', async () => {
    decodeEnrollmentTokenMock.mockResolvedValue({
      tenantId: 'tenant-1',
      email: 'diane@example.com',
      firm: 'Fight For You Company',
      contact: 'Fight For You Company',
      phone: '5550102',
      exp: 9999999999,
    });

    const result = await EnrollPage({ searchParams: Promise.resolve({ token: 'signed-token' }) });
    render(result);

    expect(enrollmentFormMock).toHaveBeenCalledWith(expect.objectContaining({
        referralPrefill: expect.objectContaining({
          companyName: 'Fight For You Company',
          firstName: '',
          lastName: '',
        }),
        isFirmEnrollment: true,
      }), expect.anything());
  });

  test('still prefills split contact names when provided', async () => {
    decodeEnrollmentTokenMock.mockResolvedValue({
      tenantId: 'tenant-1',
      email: 'diane@example.com',
      firm: 'Fight For You Company',
      title: 'Dr.',
      contactFirstName: 'Diane',
      contactLastName: 'Galano',
      exp: 9999999999,
    });

    const result = await EnrollPage({ searchParams: Promise.resolve({ token: 'signed-token' }) });
    render(result);

    expect(enrollmentFormMock).toHaveBeenCalledWith(expect.objectContaining({
        referralPrefill: expect.objectContaining({
          companyName: 'Fight For You Company',
          title: 'Dr.',
          firstName: 'Diane',
          lastName: 'Galano',
        }),
        isFirmEnrollment: true,
      }), expect.anything());
  });

  test('prefers existing Identity title over token title when both are available', async () => {
    decodeEnrollmentTokenMock.mockResolvedValue({
      tenantId: 'tenant-1',
      email: 'diane@example.com',
      firm: 'Fight For You Company',
      title: 'Ms.',
      contactFirstName: 'Diane',
      contactLastName: 'Galano',
      exp: 9999999999,
    });
    fetchExistingEnrollmentPrefillMock.mockResolvedValue({
      found: true,
      companyName: 'Fight For You Company',
      email: 'diane@example.com',
      phone: '5550102',
      title: 'Dr.',
      firstName: 'Diane',
      lastName: 'Galano',
      addressLine1: '123 Main',
      city: 'Las Vegas',
      state: 'NV',
      postalCode: '89101',
    });

    const result = await EnrollPage({ searchParams: Promise.resolve({ token: 'signed-token' }) });
    render(result);

    expect(enrollmentFormMock).toHaveBeenCalledWith(expect.objectContaining({
        referralPrefill: expect.objectContaining({
          title: 'Dr.',
          firstName: 'Diane',
          lastName: 'Galano',
        }),
        isFirmEnrollment: true,
      }), expect.anything());
  });

  test('uses the CareConnect portal login URL for the enrollment footer sign-in link', async () => {
    decodeEnrollmentTokenMock.mockResolvedValue({
      tenantId: 'tenant-1',
      email: 'diane@example.com',
      firm: 'Fight For You Company',
      contactFirstName: 'Diane',
      contactLastName: 'Galano',
      exp: 9999999999,
    });

    const result = await EnrollPage({ searchParams: Promise.resolve({ token: 'signed-token' }) });
    render(result);

    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute(
      'href',
      'https://careconnect-demo.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
    );
  });

  test('uses the CareConnect portal login URL for the invalid-link sign-in link', async () => {
    const result = await EnrollPage({ searchParams: Promise.resolve({}) });
    render(result);

    expect(screen.getByRole('link', { name: 'Sign in' })).toHaveAttribute(
      'href',
      'https://careconnect-demo.legalsynq.com/login?returnTo=%2Fcareconnect%2Fdashboard&reason=referral-portal',
    );
  });
});
