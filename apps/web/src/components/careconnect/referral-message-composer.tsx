'use client';

interface ReferralMessageComposerProps {
  message: string;
  onChange: (value: string) => void;
  onSubmit: (e: React.FormEvent<HTMLFormElement>) => void;
  isSubmitting: boolean;
}

export function ReferralMessageComposer({
  message,
  onChange,
  onSubmit,
  isSubmitting,
}: ReferralMessageComposerProps) {
  return (
    <form onSubmit={onSubmit} className="space-y-3">
      <div>
        <label
          htmlFor="referral-message"
          className="block text-xs font-semibold uppercase tracking-wider text-gray-500"
        >
          Send a message
        </label>
        <textarea
          id="referral-message"
          value={message}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Type your message here…"
          rows={4}
          maxLength={4000}
          className="mt-2 w-full rounded-md border border-gray-200 px-3 py-2 text-sm text-gray-900 shadow-sm focus:border-blue-400 focus:outline-none focus:ring-2 focus:ring-blue-100"
        />
        <p className="mt-1 text-right text-xs text-gray-400">{message.length}/4000</p>
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
      >
        {isSubmitting ? 'Sending…' : 'Send Message'}
      </button>
    </form>
  );
}
