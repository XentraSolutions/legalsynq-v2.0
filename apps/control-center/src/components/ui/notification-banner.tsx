'use client';

import { useEffect } from 'react';

export interface NotificationBannerProps {
  title: string;
  description: string;
  onDismiss: () => void;
  duration?: number;
}

export function NotificationBanner({ title, description, onDismiss, duration = 5000 }: NotificationBannerProps) {
  useEffect(() => {
    const timer = window.setTimeout(onDismiss, duration);
    return () => window.clearTimeout(timer);
  }, [duration, onDismiss]);

  return (
    <div role="status" className="fixed right-6 top-[84px] z-40 flex w-[calc(100%-3rem)] max-w-[512px] items-start gap-1 rounded-xl border border-[#e5e5e5] bg-white px-4 py-3 shadow-[0_4px_3px_rgba(0,0,0,0.1),0_2px_2px_rgba(0,0,0,0.1)]">
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-3 text-sm font-medium leading-[1.6] text-[#22c55e]">
          <i className="ri-checkbox-circle-line text-base" aria-hidden="true" />
          <span>{title}</span>
        </div>
        <p className="ml-7 text-sm leading-[1.6] text-[#0a0a0a]">{description}</p>
      </div>
      <button type="button" aria-label="Dismiss notification" onClick={onDismiss} className="-mr-1 -mt-1 flex h-6 w-6 shrink-0 items-center justify-center text-base text-[#737373] hover:text-[#0a0a0a]">
        <i className="ri-close-line" aria-hidden="true" />
      </button>
    </div>
  );
}
