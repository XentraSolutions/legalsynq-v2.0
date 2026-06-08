'use client';

import { useState } from 'react';

export function CopyableValue({
  value,
  mono = false,
  truncate = false,
}: {
  value: string;
  mono?: boolean;
  truncate?: boolean;
}) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  }

  return (
    <div className="flex items-center gap-2 min-w-0">
      <span
        className={[
          'text-gray-800',
          'min-w-0',
          mono ? 'font-mono text-xs' : '',
          truncate ? 'truncate max-w-[220px]' : '',
        ].filter(Boolean).join(' ')}
        title={value}
      >
        {value}
      </span>
      <button
        type="button"
        onClick={handleCopy}
        className="inline-flex shrink-0 items-center gap-1 rounded-md border border-gray-200 bg-gray-50 px-2 py-1 text-[11px] font-medium text-gray-600 hover:bg-gray-100 hover:text-gray-900 transition-colors"
        title={`Copy ${value}`}
        aria-label={`Copy ${value}`}
      >
        <i className={copied ? 'ri-check-line text-green-600' : 'ri-file-copy-line'} />
        {copied ? 'Copied' : 'Copy'}
      </button>
    </div>
  );
}
