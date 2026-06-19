import { render } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';

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
      contactFirstName: 'Diane',
      contactLastName: 'Galano',
      exp: 9999999999,
    });

    const result = await EnrollPage({ searchParams: Promise.resolve({ token: 'signed-token' }) });
    render(result);

    expect(enrollmentFormMock).toHaveBeenCalledWith(expect.objectContaining({
        referralPrefill: expect.objectContaining({
          companyName: 'Fight For You Company',
          firstName: 'Diane',
          lastName: 'Galano',
        }),
        isFirmEnrollment: true,
      }), expect.anything());
  });
});
