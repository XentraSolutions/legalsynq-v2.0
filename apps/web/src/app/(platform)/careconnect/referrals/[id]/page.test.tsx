import { render, screen } from '@testing-library/react';
import { describe, expect, test, vi, beforeEach } from 'vitest';
import ReferralDetailPage from './page';
import { OrgType, ProductRole, type PlatformSession } from '@/types';

vi.mock('next/navigation', () => ({
  notFound: vi.fn(() => {
    throw new Error('NOT_FOUND');
  }),
}));

vi.mock('next/link', () => ({
  default: ({ href, children, className }: { href: string; children: React.ReactNode; className?: string }) => (
    <a href={href} className={className}>{children}</a>
  ),
}));

vi.mock('@/lib/auth-guards', () => ({
  requireOrg: vi.fn(),
}));

vi.mock('@/lib/careconnect-server-api', () => ({
  careConnectServerApi: {
    referrals: {
      getById: vi.fn(),
      getComments: vi.fn(),
    },
    adminDashboard: {
      getReferralById: vi.fn(),
    },
  },
}));

vi.mock('@/lib/careconnect-access', () => ({
  checkCareConnectReceiverAccess: vi.fn(() => ({ reason: 'missing-access' })),
}));

vi.mock('@/lib/referral-nav', () => ({
  resolveReferralDetailBack: vi.fn(() => ({
    href: '/careconnect/referrals',
    label: 'Back to Referrals',
  })),
}));

vi.mock('@/components/careconnect/referral-page-header', () => ({
  ReferralPageHeader: ({ referral }: { referral: { clientFirstName: string; clientLastName: string } }) => (
    <div>Header:{referral.clientFirstName} {referral.clientLastName}</div>
  ),
}));

vi.mock('@/components/careconnect/referral-detail-panel', () => ({
  ReferralDetailPanel: () => <div>Referral detail panel</div>,
}));

vi.mock('@/components/careconnect/referral-delivery-card', () => ({
  ReferralDeliveryCard: () => <div>Delivery card</div>,
}));

vi.mock('@/components/careconnect/referral-status-actions', () => ({
  ReferralStatusActions: () => <div>Status actions</div>,
}));

vi.mock('@/components/careconnect/referral-timeline', () => ({
  ReferralTimeline: ({ adminView }: { adminView?: boolean }) => <div>Timeline:{String(!!adminView)}</div>,
}));

vi.mock('@/components/careconnect/referral-audit-timeline', () => ({
  ReferralAuditTimeline: () => <div>Audit timeline</div>,
}));

vi.mock('@/components/careconnect/referral-access-blocked', () => ({
  ReferralAccessBlocked: ({ reason }: { reason?: string }) => <div>Blocked:{reason}</div>,
}));

vi.mock('@/components/careconnect/attachment-panel', () => ({
  AttachmentPanel: ({
    readOnly,
    adminReferralView,
  }: {
    readOnly?: boolean;
    adminReferralView?: boolean;
  }) => <div>Attachment panel:{String(!!readOnly)}:{String(!!adminReferralView)}</div>,
}));

vi.mock('@/components/careconnect/referral-message-thread', () => ({
  ReferralMessageThread: ({
    referralId,
    initialComments,
    initialError,
    readOnly,
  }: {
    referralId: string;
    initialComments: Array<{ message: string }>;
    initialError?: string | null;
    readOnly?: boolean;
  }) => (
    <div>
      <div>MessageThread:{referralId}</div>
      <div>MessageCount:{initialComments.length}</div>
      <div>MessageReadOnly:{String(!!readOnly)}</div>
      {initialComments.map((comment) => (
        <div key={comment.message}>{comment.message}</div>
      ))}
      {initialError ? <div>MessageError:{initialError}</div> : null}
    </div>
  ),
}));

import { requireOrg } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';
import { ServerApiError } from '@/lib/server-api-client';

const SESSION: PlatformSession & { orgId: string } = {
  userId: 'user-1',
  tenantId: 'tenant-1',
  tenantCode: 'tenant',
  orgId: 'org-1',
  hasOrg: true,
  orgType: OrgType.Provider,
  email: 'ralph.lopez+13@xentragroup.com',
  isPlatformAdmin: false,
  isTenantAdmin: false,
  systemRoles: [],
  expiresAt: new Date('2026-06-10T00:00:00Z'),
  sessionTimeoutMinutes: 60,
  enabledProducts: ['CareConnect'],
  userProducts: ['CareConnect'],
  productRoles: [ProductRole.CareConnectReceiver],
};

const REFERRAL = {
  id: 'ref-123',
  clientFirstName: 'Jane',
  clientLastName: 'Doe',
  referringOrganizationId: 'firm-1',
  receivingOrganizationId: 'org-1',
  referrerEmail: 'firm@example.com',
} as const;

const COMMENTS = [
  {
    id: 'comment-1',
    senderType: 'referrer',
    senderName: 'Sarah Johnson',
    message: 'Can you see this patient this week?',
    createdAtUtc: '2026-06-09T10:00:00Z',
  },
];

describe('CareConnect referral detail page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(requireOrg).mockResolvedValue(SESSION);
    vi.mocked(careConnectServerApi.referrals.getById).mockResolvedValue(REFERRAL as Awaited<ReturnType<typeof careConnectServerApi.referrals.getById>>);
    vi.mocked(careConnectServerApi.adminDashboard.getReferralById).mockResolvedValue(REFERRAL as Awaited<ReturnType<typeof careConnectServerApi.adminDashboard.getReferralById>>);
    vi.mocked(careConnectServerApi.referrals.getComments).mockResolvedValue(COMMENTS);
  });

  test('renders the shared message thread with server-fetched comments on /careconnect/referrals/[id]', async () => {
    const page = await ReferralDetailPage({
      params: Promise.resolve({ id: 'ref-123' }),
      searchParams: Promise.resolve({}),
    });

    render(page);

    expect(screen.getByText('MessageThread:ref-123')).toBeInTheDocument();
    expect(screen.getByText('MessageCount:1')).toBeInTheDocument();
    expect(screen.getByText('Can you see this patient this week?')).toBeInTheDocument();
    expect(screen.getByText('Attachment panel:true:false')).toBeInTheDocument();
  });

  test('passes comment load failures into the message thread fallback state', async () => {
    vi.mocked(careConnectServerApi.referrals.getComments).mockRejectedValue(new Error('Failed to load messages.'));

    const page = await ReferralDetailPage({
      params: Promise.resolve({ id: 'ref-123' }),
      searchParams: Promise.resolve({}),
    });

    render(page);

    expect(screen.getByText('MessageThread:ref-123')).toBeInTheDocument();
    expect(screen.getByText('MessageCount:0')).toBeInTheDocument();
    expect(screen.getByText('MessageError:Failed to load messages.')).toBeInTheDocument();
  });

  test('does not expose a raw HTTP 403 when referral comments are forbidden', async () => {
    vi.mocked(careConnectServerApi.referrals.getComments)
      .mockRejectedValue(new ServerApiError(403, 'HTTP 403'));

    const page = await ReferralDetailPage({
      params: Promise.resolve({ id: 'ref-123' }),
      searchParams: Promise.resolve({}),
    });

    render(page);

    expect(screen.getByText('MessageThread:ref-123')).toBeInTheDocument();
    expect(screen.getByText('MessageCount:0')).toBeInTheDocument();
    expect(screen.queryByText('MessageError:HTTP 403')).not.toBeInTheDocument();
  });

  test('renders tenant-admin-only access in read-only mode', async () => {
    vi.mocked(requireOrg).mockResolvedValue({
      ...SESSION,
      isTenantAdmin: true,
      systemRoles: ['TenantAdmin'],
      productRoles: [],
    });

    const page = await ReferralDetailPage({
      params: Promise.resolve({ id: 'ref-123' }),
      searchParams: Promise.resolve({}),
    });

    render(page);

    expect(screen.queryByText('Status actions')).not.toBeInTheDocument();
    expect(screen.queryByText('Delivery card')).not.toBeInTheDocument();
    expect(screen.getByText('Attachment panel:true:true')).toBeInTheDocument();
    expect(screen.queryByText('MessageThread:ref-123')).not.toBeInTheDocument();
    expect(screen.getByText('Timeline:true')).toBeInTheDocument();
    expect(careConnectServerApi.referrals.getComments).not.toHaveBeenCalled();
    expect(careConnectServerApi.adminDashboard.getReferralById).toHaveBeenCalledWith('ref-123');
  });
});
