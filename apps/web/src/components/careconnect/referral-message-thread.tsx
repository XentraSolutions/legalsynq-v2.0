'use client';

import { useEffect, useRef, useState } from 'react';
import { careConnectApi } from '@/lib/careconnect-api';
import { ApiError } from '@/lib/api-client';
import type { ReferralComment } from '@/types/careconnect';
import type { SelectedCareConnectMessageFile } from '@/lib/careconnect-message-attachments';
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
  const [files, setFiles] = useState<SelectedCareConnectMessageFile[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(initialError);
  const [attachmentState, setAttachmentState] = useState<
    Record<string, { loading: boolean; error: string | null }>
  >({});

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
    if (!trimmed && files.length === 0) {
      setError('Enter a message or attach at least one file.');
      return;
    }
    if (trimmed.length > 4000) {
      setError('Message must be 4000 characters or fewer.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const { data } = files.length > 0
        ? await careConnectApi.referrals.postCommentWithAttachments(referralId, trimmed, files)
        : await careConnectApi.referrals.postComment(referralId, { message: trimmed });
      setComments((prev) => [...prev, data]);
      setMessage('');
      setFiles([]);
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

  async function handleOpenAttachment(attachmentId: string, download = false) {
    setAttachmentState((prev) => ({
      ...prev,
      [attachmentId]: { loading: true, error: null },
    }));

    try {
      const { data } = await careConnectApi.referralAttachments.getSignedUrl(
        referralId,
        attachmentId,
        download,
      );
      window.open(data.url, '_blank', 'noopener,noreferrer');
      setAttachmentState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: null },
      }));
    } catch (err) {
      const message =
        err instanceof ApiError && err.isForbidden
          ? 'You do not have permission to view this attachment.'
          : err instanceof ApiError && err.isServerError
          ? 'This attachment is temporarily unavailable.'
          : err instanceof ApiError
          ? err.message
          : 'Unable to open this attachment.';

      setAttachmentState((prev) => ({
        ...prev,
        [attachmentId]: { loading: false, error: message },
      }));
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
        className="h-[26rem] overflow-y-auto flex flex-col gap-3 md:max-h-[28rem] md:overflow-y-auto md:pr-2"
      >
        {comments.length === 0 ? (
          <p className="text-sm text-gray-500 italic">
            {readOnly
              ? 'No messages yet.'
              : 'No messages yet. Start the conversation with the referring party below.'}
          </p>
        ) : (
          comments.map((comment) => (
            <ReferralCommentBubble
              key={comment.id}
              comment={comment}
              onOpenAttachment={handleOpenAttachment}
              attachmentState={attachmentState}
            />
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
          files={files}
          onFilesChange={setFiles}
        />
      )}
    </div>
  );
}
