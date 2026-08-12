import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, test, expect, beforeEach } from 'vitest';
import { ReferralMessageThread } from '../referral-message-thread';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';

vi.mock('@/lib/careconnect-api', () => ({
  careConnectApi: {
    referrals: {
      postComment: vi.fn(),
      postCommentWithAttachments: vi.fn(),
    },
    referralAttachments: {
      getSignedUrl: vi.fn(),
    },
  },
}));

const EXISTING_COMMENT = {
  id: 'c-1',
  senderType: 'referrer',
  senderName: 'Sarah Johnson',
  message: 'Can you see this patient this week?',
  createdAtUtc: '2026-06-09T10:00:00Z',
  attachments: [],
};

function ok<T>(data: T) {
  return { data, correlationId: 'c', status: 200 } as const;
}

describe('ReferralMessageThread', () => {
  const originalScrollTo = HTMLElement.prototype.scrollTo;
  const scrollToMock = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(HTMLElement.prototype, 'scrollHeight', {
      configurable: true,
      get: () => 500,
    });
    HTMLElement.prototype.scrollTo = scrollToMock;
  });

  afterEach(() => {
    HTMLElement.prototype.scrollTo = originalScrollTo;
  });

  test('renders existing comments', () => {
    render(
      <ReferralMessageThread
        referralId="ref-1"
        initialComments={[EXISTING_COMMENT]}
      />,
    );

    expect(screen.getByText('Sarah Johnson')).toBeInTheDocument();
    expect(screen.getByText('Can you see this patient this week?')).toBeInTheDocument();
  });

  test('uses a dedicated desktop scroll region for message history', () => {
    render(
      <ReferralMessageThread
        referralId="ref-1"
        initialComments={[EXISTING_COMMENT]}
      />,
    );

    expect(screen.getByTestId('referral-message-history')).toHaveClass(
      'md:max-h-[28rem]',
      'md:overflow-y-auto',
      'md:pr-2',
    );
  });

  test('scrolls to the latest message on initial render', async () => {
    render(
      <ReferralMessageThread
        referralId="ref-1"
        initialComments={[EXISTING_COMMENT]}
      />,
    );

    await waitFor(() =>
      expect(scrollToMock).toHaveBeenCalledWith({
        top: 500,
        behavior: 'auto',
      }),
    );
  });

  test('renders empty state when there are no comments', () => {
    render(
      <ReferralMessageThread
        referralId="ref-1"
        initialComments={[]}
      />,
    );

    expect(screen.getByText('No messages yet. Start the conversation with the referring party below.')).toBeInTheDocument();
  });

  test('hides the composer in read-only mode', () => {
    render(
      <ReferralMessageThread
        referralId="ref-1"
        initialComments={[]}
        readOnly
      />,
    );

    expect(screen.getByText('No messages yet.')).toBeInTheDocument();
    expect(screen.getByText('Tenant Admin view only. Messaging is disabled on this referral.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Send Message' })).not.toBeInTheDocument();
  });

  test('shows validation for blank message', async () => {
    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    expect(screen.getByText('Enter a message or attach at least one file.')).toBeInTheDocument();
  });

  test('appends a successful message post to the thread', async () => {
    vi.mocked(careConnectApi.referrals.postComment).mockResolvedValue(ok({
      id: 'c-2',
      senderType: 'provider',
      senderName: 'Dr. Gray',
      message: 'Yes, we can see them Thursday.',
      createdAtUtc: '2026-06-09T11:00:00Z',
    }));

    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    await user.type(screen.getByLabelText('Send a message'), 'Yes, we can see them Thursday.');
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() =>
      expect(screen.getByText('Yes, we can see them Thursday.')).toBeInTheDocument(),
    );
    await waitFor(() =>
      expect(scrollToMock).toHaveBeenLastCalledWith({
        top: 500,
        behavior: 'auto',
      }),
    );
  });

  test('shows API error text when posting fails', async () => {
    vi.mocked(careConnectApi.referrals.postComment)
      .mockRejectedValue(new ApiError(500, 'Notification service unavailable', 'corr-1'));

    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    await user.type(screen.getByLabelText('Send a message'), 'Following up on this referral');
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() =>
      expect(screen.getByText('Notification service unavailable')).toBeInTheDocument(),
    );
  });

  test('submits selected files with a message and renders returned attachments', async () => {
    vi.mocked(careConnectApi.referrals.postCommentWithAttachments).mockResolvedValue(ok({
      id: 'c-3',
      senderType: 'provider',
      senderName: 'Dr. Gray',
      message: 'Attached the intake scan.',
      createdAtUtc: '2026-06-09T12:00:00Z',
      attachments: [
        {
          id: 'att-1',
          fileName: 'scan.png',
          contentType: 'image/png',
          fileSizeBytes: 3,
          createdAtUtc: '2026-06-09T12:00:00Z',
        },
      ],
    }));

    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    const file = new File(['abc'], 'scan.png', { type: 'image/png' });
    const input = document.getElementById('referral-message-attachments') as HTMLInputElement;
    await user.type(screen.getByLabelText('Send a message'), 'Attached the intake scan.');
    await user.upload(input, file);
    expect(screen.getByText('scan.png')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() =>
      expect(careConnectApi.referrals.postCommentWithAttachments).toHaveBeenCalledWith(
        'ref-1',
        'Attached the intake scan.',
        [expect.objectContaining({ file })],
      ),
    );
    await waitFor(() =>
      expect(screen.getByTitle('View scan.png')).toBeInTheDocument(),
    );
  });

  test('submits selected files without message text', async () => {
    vi.mocked(careConnectApi.referrals.postCommentWithAttachments).mockResolvedValue(ok({
      id: 'c-4',
      senderType: 'provider',
      senderName: 'Dr. Gray',
      message: '',
      createdAtUtc: '2026-06-09T12:05:00Z',
      attachments: [
        {
          id: 'att-2',
          fileName: 'scan.png',
          contentType: 'image/png',
          fileSizeBytes: 3,
          createdAtUtc: '2026-06-09T12:05:00Z',
        },
      ],
    }));

    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    const file = new File(['abc'], 'scan.png', { type: 'image/png' });
    const input = document.getElementById('referral-message-attachments') as HTMLInputElement;
    await user.upload(input, file);
    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    await waitFor(() =>
      expect(careConnectApi.referrals.postCommentWithAttachments).toHaveBeenCalledWith(
        'ref-1',
        '',
        [expect.objectContaining({ file })],
      ),
    );
    await waitFor(() =>
      expect(screen.getByTitle('View scan.png')).toBeInTheDocument(),
    );
  });

  test('opens a message attachment through the referral signed-url endpoint', async () => {
    vi.mocked(careConnectApi.referralAttachments.getSignedUrl).mockResolvedValue(ok({
      url: 'https://docs.example/scan',
      expiresInSeconds: 300,
    }));
    const openMock = vi.fn();
    vi.stubGlobal('open', openMock);

    const comment = {
      ...EXISTING_COMMENT,
      attachments: [
        {
          id: 'att-1',
          fileName: 'scan.png',
          contentType: 'image/png',
          fileSizeBytes: 3,
          createdAtUtc: '2026-06-09T12:00:00Z',
        },
      ],
    };

    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[comment]} />);

    await user.click(screen.getByTitle('View scan.png'));

    await waitFor(() =>
      expect(careConnectApi.referralAttachments.getSignedUrl).toHaveBeenCalledWith('ref-1', 'att-1', false),
    );
    expect(openMock).toHaveBeenCalledWith('https://docs.example/scan', '_blank', 'noopener,noreferrer');
  });
});
