'use client';

import { useState, useTransition, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { addCommentAction } from '@/app/(platform)/support/actions';

interface TicketReplyFormProps {
  ticketId: string;
  disabled?: boolean;
}

/**
 * TicketReplyForm — reply textarea at the bottom of a ticket conversation.
 *
 * Client component. Calls addCommentAction server action on submit,
 * then refreshes the page to show the new comment.
 */
export function TicketReplyForm({ ticketId, disabled = false }: TicketReplyFormProps) {
  const router             = useRouter();
  const [body, setBody]    = useState('');
  const [error, setError]  = useState<string | null>(null);
  const [sent, setSent]    = useState(false);
  const [isPending, startTx] = useTransition();
  const textareaRef        = useRef<HTMLTextAreaElement>(null);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!body.trim() || isPending || disabled) return;

    setError(null);
    setSent(false);
    startTx(async () => {
      const result = await addCommentAction(ticketId, body.trim());
      if (result.success) {
        setBody('');
        setSent(true);
        setTimeout(() => setSent(false), 3000);
        router.refresh();
      } else {
        setError(result.error ?? 'Failed to send reply.');
      }
    });
  }

  const isDisabled = disabled || isPending;

  return (
    <form onSubmit={handleSubmit} className="space-y-3">

      {error && (
        <div className="px-4 py-2.5 bg-red-50 border border-red-200 rounded-lg">
          <p className="text-sm text-red-700">{error}</p>
        </div>
      )}

      {sent && (
        <div className="px-4 py-2.5 bg-green-50 border border-green-200 rounded-lg">
          <p className="text-sm text-green-700">Reply sent successfully.</p>
        </div>
      )}

      <textarea
        ref={textareaRef}
        value={body}
        onChange={e => setBody(e.target.value)}
        placeholder="Write your reply…"
        rows={4}
        maxLength={4000}
        disabled={isDisabled}
        className="w-full px-3 py-2.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent disabled:bg-gray-50 disabled:text-gray-400 resize-none"
      />

      <div className="flex items-center justify-between">
        <span className="text-xs text-gray-400 tabular-nums">
          {body.length > 0 ? `${body.length} / 4000` : ''}
        </span>
        <button
          type="submit"
          disabled={!body.trim() || isDisabled}
          className="inline-flex items-center gap-2 px-4 py-2 text-sm font-medium rounded-lg bg-indigo-600 text-white hover:bg-indigo-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {isPending ? (
            <>
              <span className="inline-block h-3.5 w-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              Sending…
            </>
          ) : (
            <>
              <i className="ri-send-plane-line text-sm" aria-hidden="true" />
              Send Reply
            </>
          )}
        </button>
      </div>
    </form>
  );
}
