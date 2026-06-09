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
    },
  },
}));

const EXISTING_COMMENT = {
  id: 'c-1',
  senderType: 'referrer',
  senderName: 'Sarah Johnson',
  message: 'Can you see this patient this week?',
  createdAt: '2026-06-09T10:00:00Z',
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

  test('shows validation for blank message', async () => {
    const user = userEvent.setup();
    render(<ReferralMessageThread referralId="ref-1" initialComments={[]} />);

    await user.click(screen.getByRole('button', { name: 'Send Message' }));

    expect(screen.getByText('Message is required.')).toBeInTheDocument();
  });

  test('appends a successful message post to the thread', async () => {
    vi.mocked(careConnectApi.referrals.postComment).mockResolvedValue(ok({
      id: 'c-2',
      senderType: 'provider',
      senderName: 'Dr. Gray',
      message: 'Yes, we can see them Thursday.',
      createdAt: '2026-06-09T11:00:00Z',
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
});
