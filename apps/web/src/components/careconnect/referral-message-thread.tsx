'use client';

import { useEffect, useRef, useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { ReferralComment } from '@/types/careconnect';
import { ReferralCommentBubble } from './referral-comment-bubble';
import { ReferralMessageComposer } from './referral-message-composer';

interface ReferralMessageThreadProps {
  referralId: string;
  initialComments: ReferralComment[];
  initialError?: string | null;
  readOnly?: boolean;
}

export function ReferralMessageThread({
  referralId,
  initialComments,
  initialError = null,
  readOnly = false,
}: ReferralMessageThreadProps) {
  const historyRef = useRef<HTMLDivElement | null>(null);
  const [comments, setComments] = useState(initialComments);
  const [message, setMessage] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(initialError);

  useEffect(() => {
    if (!historyRef.current || comments.length === 0) return;

    const history = historyRef.current;
    history.scrollTo?.({
      top: history.scrollHeight,
      behavior: 'auto',
    });
    history.scrollTop = history.scrollHeight;
  }, [comments.length]);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const trimmed = message.trim();
    if (!trimmed) {
      setError('Message is required.');
      return;
    }
    if (trimmed.length > 4000) {
      setError('Message must be 4000 characters or fewer.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const { data } = await careConnectApi.referrals.postComment(referralId, { message: trimmed });
      setComments((prev) => [...prev, data]);
      setMessage('');
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'Failed to send message. Please try again.',
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg px-5 py-4 space-y-4">
      <div>
        <h3 className="text-xs font-semibold text-gray-400 uppercase tracking-wider">Messages</h3>
        <p className="mt-1 text-sm text-gray-500">
          Messages sent here are added to the same referral thread used in the email-link flow.
        </p>
      </div>

      {error && (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {error}
        </div>
      )}

      <div
        ref={historyRef}
        data-testid="referral-message-history"
        className="h-[26rem] overflow-y-auto flex flex-col gap-3"
      >
        {comments.length === 0 ? (
          <p className="text-sm text-gray-500 italic">
            {readOnly
              ? 'No messages yet.'
              : 'No messages yet. Start the conversation with the referring party below.'}
          </p>
        ) : (
          comments.map((comment) => (
            <ReferralCommentBubble key={comment.id} comment={comment} />
          ))
        )}
      </div>

      {readOnly ? (
        <div className="rounded-md border border-gray-200 bg-gray-50 px-4 py-3 text-sm text-gray-500">
          Tenant Admin view only. Messaging is disabled on this referral.
        </div>
      ) : (
        <ReferralMessageComposer
          message={message}
          onChange={setMessage}
          onSubmit={handleSubmit}
          isSubmitting={isSubmitting}
        />
      )}
    </div>
  );
}
