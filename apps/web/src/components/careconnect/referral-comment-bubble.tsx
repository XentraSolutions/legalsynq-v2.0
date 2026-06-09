import type { ReferralComment } from '@/types/careconnect';

interface ReferralCommentBubbleProps {
  comment: ReferralComment;
}

function formatDate(iso: string) {
  try {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
  } catch {
    return iso;
  }
}

export function ReferralCommentBubble({ comment }: ReferralCommentBubbleProps) {
  const isProvider = comment.senderType === 'provider';

  return (
    <div
      className={`rounded-lg border px-4 py-3 ${
        isProvider
          ? 'border-blue-200 bg-blue-50'
          : 'border-gray-200 bg-gray-50'
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold text-gray-900">{comment.senderName}</p>
          <p className="text-xs uppercase tracking-wide text-gray-500">
            {isProvider ? 'Provider' : 'Referrer'}
          </p>
        </div>
        <p className="text-xs text-gray-500 whitespace-nowrap">{formatDate(comment.createdAt)}</p>
      </div>
      <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-gray-700">{comment.message}</p>
    </div>
  );
}
