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
          <span className="text-xs text-gray-400 whitespace-nowrap">{formatDate(comment.createdAt)}</span>
        </div>
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
      </div>
    </div>
  );
}
