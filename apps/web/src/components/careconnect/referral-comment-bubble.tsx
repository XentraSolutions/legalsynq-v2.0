'use client';

import type { ReferralComment } from '@/types/careconnect';
import { formatCareConnectAttachmentBytes } from '@/lib/careconnect-message-attachments';
import { useBrowserTimezone } from '@/lib/use-timezone';

interface ReferralCommentBubbleProps {
  comment: ReferralComment;
  onOpenAttachment?: (attachmentId: string, download?: boolean) => void;
  attachmentState?: Record<string, { loading: boolean; error: string | null }>;
}

function formatDate(iso: string, timezone: string) {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month:    'short',
      day:      'numeric',
      year:     'numeric',
      hour:     'numeric',
      minute:   '2-digit',
      hour12:   true,
      timeZone: timezone,
    });
  } catch {
    return iso;
  }
}

export function ReferralCommentBubble({
  comment,
  onOpenAttachment,
  attachmentState = {},
}: ReferralCommentBubbleProps) {
  const timezone = useBrowserTimezone();
  const isProvider = comment.senderType === 'provider';
  const attachments = comment.attachments ?? [];
  const hasMessage = comment.message.trim().length > 0;

  return (
    <div className={`flex items-start gap-2.5 ${isProvider ? 'flex-row-reverse' : 'flex-row'}`}>
      {/* Avatar */}
      <div className={`w-8 h-8 rounded-full flex-shrink-0 flex items-center justify-center text-sm font-bold ${
        isProvider ? 'bg-blue-100 text-blue-700' : 'bg-amber-100 text-amber-700'
      }`}>
        {comment.senderName.charAt(0).toUpperCase()}
      </div>

      {/* Bubble */}
      <div className="max-w-[80%]">
        <div className={`flex items-baseline gap-2 mb-1 ${isProvider ? 'flex-row-reverse' : 'flex-row'}`}>
          <span className="text-xs font-semibold text-gray-700">{comment.senderName}</span>
          <span className="text-xs text-gray-400 whitespace-nowrap">{formatDate(comment.createdAtUtc, timezone)}</span>
        </div>
        {hasMessage && (
          <div
            className={`px-3 py-2.5 text-sm leading-relaxed text-gray-900 whitespace-pre-wrap ${
              isProvider
                ? 'bg-blue-50 border border-blue-200'
                : 'bg-amber-50 border border-amber-200'
            }`}
            style={{ borderRadius: isProvider ? '12px 4px 12px 12px' : '4px 12px 12px 12px' }}
          >
            {comment.message}
          </div>
        )}
        {attachments.length > 0 && (
          <ul className={`mt-2 space-y-1 ${isProvider ? 'items-end' : 'items-start'}`} aria-label="Message attachments">
            {attachments.map((attachment) => {
              const state = attachmentState[attachment.id] ?? { loading: false, error: null };
              return (
                <li key={attachment.id} className={isProvider ? 'text-right' : 'text-left'}>
                  <button
                    type="button"
                    onClick={() => onOpenAttachment?.(attachment.id, false)}
                    disabled={!onOpenAttachment || state.loading}
                    className="inline-flex max-w-full items-center gap-1.5 rounded-md border border-gray-200 bg-white px-2 py-1 text-xs text-gray-700 shadow-sm transition-colors hover:border-blue-200 hover:text-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                    title={`View ${attachment.fileName}`}
                  >
                    <i className="ri-attachment-2 shrink-0 text-gray-400" aria-hidden="true" />
                    <span className="truncate">{attachment.fileName}</span>
                    <span className="shrink-0 text-gray-400">
                      {state.loading ? 'Opening...' : formatCareConnectAttachmentBytes(attachment.fileSizeBytes)}
                    </span>
                  </button>
                  {state.error && (
                    <p className="mt-1 text-xs text-red-600">{state.error}</p>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
