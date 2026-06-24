import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';
import ProviderReferralDetailPage from './page';

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
  requireExternalPortal: vi.fn(),
}));

vi.mock('@/lib/careconnect-server-api', () => ({
  careConnectServerApi: {
    referrals: {
      getById: vi.fn(),
      getComments: vi.fn(),
    },
  },
}));

vi.mock('@/components/careconnect/referral-detail-panel', () => ({
  ReferralDetailPanel: () => <div>Referral detail panel</div>,
}));

vi.mock('@/components/careconnect/referral-status-actions', () => ({
  ReferralStatusActions: () => <div>Status actions</div>,
}));

vi.mock('@/components/careconnect/referral-timeline', () => ({
  ReferralTimeline: () => <div>Timeline</div>,
}));

vi.mock('@/components/careconnect/attachment-panel', () => ({
  AttachmentPanel: ({ readOnly }: { readOnly?: boolean }) => (
    <div>Attachment panel:{String(!!readOnly)}</div>
  ),
}));

vi.mock('@/components/careconnect/referral-message-thread', () => ({
  ReferralMessageThread: () => <div>Message thread</div>,
}));

import { requireExternalPortal } from '@/lib/auth-guards';
import { careConnectServerApi } from '@/lib/careconnect-server-api';

const REFERRAL = {
  id: 'ref-123',
  clientFirstName: 'Jane',
  clientLastName: 'Doe',
  caseNumber: 'CASE-001',
  status: 'New',
};

describe('Provider referral detail page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(requireExternalPortal).mockResolvedValue({} as Awaited<ReturnType<typeof requireExternalPortal>>);
    vi.mocked(careConnectServerApi.referrals.getById).mockResolvedValue(
      REFERRAL as Awaited<ReturnType<typeof careConnectServerApi.referrals.getById>>,
    );
  });

  test('renders provider referral detail without owning the message thread', async () => {
    const page = await ProviderReferralDetailPage({
      params: Promise.resolve({ id: 'ref-123' }),
    });

    render(page);

    expect(screen.getByRole('heading', { name: 'Jane Doe' })).toBeInTheDocument();
    expect(screen.getByText('Referral detail panel')).toBeInTheDocument();
    expect(screen.getByText('Status actions')).toBeInTheDocument();
    expect(screen.getByText('Attachment panel:true')).toBeInTheDocument();
    expect(screen.getByText('Timeline')).toBeInTheDocument();
    expect(screen.queryByText('Message thread')).not.toBeInTheDocument();
    expect(careConnectServerApi.referrals.getComments).not.toHaveBeenCalled();
  });
});
