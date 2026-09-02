'use client';

import { useState, useRef, useEffect } from 'react';
import Link from 'next/link';
import { MOCK_NOTIFICATIONS, type MockNotification } from '@/lib/mock-notifications';

// The personal notification feed isn't backed by a real API yet — the
// backend endpoint returns errors (see the removed notificationsService
// calls this replaced). Rendering mock data keeps the header in a safe,
// designed shape instead of surfacing "Unable to load notifications" to
// every user until that endpoint ships.

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  if (days < 30) return `${days}d ago`;
  return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<MockNotification[]>(MOCK_NOTIFICATIONS);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function handler(e: KeyboardEvent) { if (e.key === 'Escape') setOpen(false); }
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [open]);

  const unreadCount = items.filter((n) => !n.read).length;
  const preview = items.slice(0, 6);

  function markAllRead() {
    setItems((prev) => prev.map((n) => ({ ...n, read: true })));
  }

  return (
    <div ref={ref} className="relative flex items-center shrink-0">
      <button
        onClick={() => setOpen((p) => !p)}
        title="Notifications"
        aria-label={unreadCount > 0 ? `Notifications, ${unreadCount} unread` : 'Notifications'}
        aria-haspopup="true"
        aria-expanded={open}
        className={[
          'w-8 h-8 flex items-center justify-center rounded-lg transition-colors relative',
          open
            ? 'bg-gray-100 text-gray-900'
            : 'text-gray-400 hover:bg-gray-100 hover:text-gray-700',
        ].join(' ')}
      >
        <i className="ri-notification-3-line text-[18px] leading-none" />
        {unreadCount > 0 && (
          <span className="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-red-500 ring-2 ring-white" />
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-[calc(100%+10px)] w-96 rounded-xl bg-white shadow-2xl border border-gray-200 overflow-hidden z-50">
          <div className="flex items-center justify-between px-4 py-3.5 border-b border-gray-100">
            <p className="text-base font-bold text-gray-900">Notifications</p>
            {unreadCount > 0 && (
              <button
                onClick={markAllRead}
                className="text-sm font-medium text-primary hover:underline"
              >
                Mark all as read
              </button>
            )}
          </div>

          <div className="max-h-[380px] overflow-y-auto">
            {preview.length === 0 && (
              <div className="px-4 py-8 text-center">
                <i className="ri-mail-check-line text-2xl text-gray-300" />
                <p className="text-xs text-gray-400 mt-2">No notifications yet</p>
              </div>
            )}

            {preview.map((item) => (
              <div
                key={item.id}
                className={[
                  'flex items-start gap-3 px-4 py-3 border-b border-gray-50 last:border-b-0 border-l-2',
                  item.read ? 'border-l-transparent' : 'bg-primary/5 border-l-primary',
                ].join(' ')}
              >
                <span
                  className="w-9 h-9 rounded-full flex items-center justify-center text-xs font-semibold shrink-0"
                  style={{ backgroundColor: item.avatar.bg, color: item.avatar.color }}
                >
                  {item.avatar.initials}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5">
                    <p className="text-sm font-semibold text-gray-900 truncate">{item.title}</p>
                    {!item.read && (
                      <span className="w-1.5 h-1.5 rounded-full bg-primary shrink-0" />
                    )}
                  </div>
                  <p className="text-sm text-gray-600 mt-0.5 line-clamp-2">{item.description}</p>
                  <p className="text-xs text-gray-400 mt-1">{timeAgo(item.timestamp)}</p>
                </div>
              </div>
            ))}
          </div>

          <div className="px-4 py-2.5 border-t border-gray-100 bg-gray-50">
            <Link
              href="/notifications/inbox"
              onClick={() => setOpen(false)}
              className="flex items-center justify-between text-sm text-gray-700 hover:text-gray-900 font-medium"
            >
              View all notifications <i className="ri-arrow-right-s-line" />
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
