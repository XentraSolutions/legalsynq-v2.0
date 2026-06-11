'use client';

import { useState } from 'react';

interface PublicAttachmentLinkProps {
  attachmentId: string;
  fileName: string;
  fileSizeLabel: string;
  referralId: string;
  token: string;
}

export function PublicAttachmentLink({
  attachmentId,
  fileName,
  fileSizeLabel,
  referralId,
  token,
}: PublicAttachmentLinkProps) {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleOpen = async () => {
    setIsLoading(true);
    setError(null);

    try {
      const res = await fetch(
        `/api/public/careconnect/api/referrals/${referralId}/public-attachments/${attachmentId}/url` +
          `?token=${encodeURIComponent(token)}&download=true`,
      );

      if (!res.ok) {
        setError('Could not load this document. Please try again.');
        return;
      }

      const body = await res.json() as { url?: string };
      if (!body.url) {
        setError('Document URL unavailable.');
        return;
      }

      window.open(body.url, '_blank', 'noopener,noreferrer');
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div>
      <button
        type="button"
        onClick={handleOpen}
        disabled={isLoading}
        className="group flex w-full items-center gap-3 rounded-lg border border-gray-200 px-3 py-2.5 text-left transition-colors hover:border-primary/40 hover:bg-primary/5 disabled:cursor-wait disabled:opacity-70"
      >
        <svg className="h-5 w-5 shrink-0 text-gray-400 transition-colors group-hover:text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
            d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium text-gray-900">{fileName}</p>
          <p className="text-xs text-gray-400">{fileSizeLabel}</p>
        </div>
        <svg className="h-4 w-4 shrink-0 text-gray-400 transition-colors group-hover:text-primary" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
            d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
        </svg>
      </button>
      {error && <p className="mt-2 text-xs text-red-600">{error}</p>}
    </div>
  );
}
