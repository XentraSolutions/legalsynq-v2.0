"use client";

import { useMemo, useState } from "react";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  MOCK_NOTIFICATIONS,
  type MockNotification,
  type MockNotificationCategory,
} from "@/lib/mock-notifications";

type TabKey = "all" | "unread" | "lien" | "message";

const TABS: { key: TabKey; label: string }[] = [
  { key: "all", label: "All Notifications" },
  { key: "unread", label: "Unread" },
  { key: "lien", label: "Liens" },
  { key: "message", label: "Messages" },
];

const ROWS_PER_PAGE_OPTIONS = [10, 25, 50];

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleString("en-US", {
    month: "2-digit",
    day: "2-digit",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

function countByTab(items: MockNotification[], key: TabKey): number {
  if (key === "all") return items.length;
  if (key === "unread") return items.filter((n) => !n.read).length;
  return items.filter((n) => n.category === (key as MockNotificationCategory)).length;
}

export function NotificationInboxClient() {
  const [items, setItems] = useState<MockNotification[]>(MOCK_NOTIFICATIONS);
  const [tab, setTab] = useState<TabKey>("all");
  const [rowsPerPage, setRowsPerPage] = useState(ROWS_PER_PAGE_OPTIONS[0]);
  const [page, setPage] = useState(1);

  const filtered = useMemo(() => {
    if (tab === "all") return items;
    if (tab === "unread") return items.filter((n) => !n.read);
    return items.filter((n) => n.category === tab);
  }, [items, tab]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / rowsPerPage));
  const currentPage = Math.min(page, totalPages);
  const start = (currentPage - 1) * rowsPerPage;
  const pageItems = filtered.slice(start, start + rowsPerPage);

  function selectTab(next: TabKey) {
    setTab(next);
    setPage(1);
  }

  function markAllAsRead() {
    setItems((prev) => prev.map((n) => ({ ...n, read: true })));
  }

  function dismiss(id: string) {
    setItems((prev) => prev.filter((n) => n.id !== id));
  }

  const unreadCount = countByTab(items, "unread");

  return (
    <div className="max-w-5xl mx-auto space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Notifications</h1>
          <p className="mt-1 text-sm text-gray-500">
            Manage your system updates, offers, and platform activity alerts.
          </p>
        </div>
        <Button
          variant="primary"
          onClick={markAllAsRead}
          disabled={unreadCount === 0}
        >
          Mark all as read
        </Button>
      </div>

      <div className="flex items-center gap-2 border-b border-gray-200 pb-3 overflow-x-auto">
        {TABS.map((t) => {
          const isActive = tab === t.key;
          return (
            <button
              key={t.key}
              onClick={() => selectTab(t.key)}
              className={[
                "shrink-0 rounded-full px-4 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-primary text-white"
                  : "text-gray-500 hover:bg-gray-100 hover:text-gray-700",
              ].join(" ")}
            >
              {t.label} ({countByTab(items, t.key)})
            </button>
          );
        })}
      </div>

      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        {pageItems.length === 0 ? (
          <div className="px-5 py-16 text-center">
            <i className="ri-mail-check-line text-3xl text-gray-300" />
            <p className="mt-2 text-sm text-gray-400">No notifications here.</p>
          </div>
        ) : (
          <ul className="divide-y divide-gray-100">
            {pageItems.map((item) => (
              <li
                key={item.id}
                className={[
                  "flex items-start gap-4 px-5 py-4 border-l-2",
                  item.read ? "border-l-transparent" : "bg-orange-50/50 border-l-primary",
                ].join(" ")}
              >
                <span
                  className="w-10 h-10 rounded-full flex items-center justify-center text-sm font-semibold shrink-0"
                  style={{ backgroundColor: item.avatar.bg, color: item.avatar.color }}
                >
                  {item.avatar.initials}
                </span>
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-1.5">
                    <p className="text-sm font-semibold text-gray-900">{item.title}</p>
                    {!item.read && (
                      <span className="w-1.5 h-1.5 rounded-full bg-primary shrink-0" />
                    )}
                  </div>
                  <p className="text-sm text-gray-600 mt-1">{item.description}</p>
                  <p className="text-xs text-gray-400 mt-1.5">{fmtDate(item.timestamp)}</p>
                </div>
                <button
                  type="button"
                  onClick={() => dismiss(item.id)}
                  title="Dismiss"
                  className="shrink-0 text-gray-300 hover:text-gray-500 transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className="flex items-center justify-between gap-4 px-5 py-3 border-t border-gray-100 bg-gray-50 text-sm text-gray-500">
          <div className="flex items-center gap-2">
            <span>Rows per page:</span>
            <select
              value={rowsPerPage}
              onChange={(e) => {
                setRowsPerPage(Number(e.target.value));
                setPage(1);
              }}
              className="rounded-md border border-gray-200 bg-white px-2 py-1 text-sm text-gray-700"
            >
              {ROWS_PER_PAGE_OPTIONS.map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
            <span className="ml-2">
              {filtered.length === 0
                ? "0 of 0"
                : `${start + 1}-${Math.min(start + rowsPerPage, filtered.length)} of ${filtered.length}`}
            </span>
          </div>

          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(1)}
              disabled={currentPage === 1}
              className="w-7 h-7 flex items-center justify-center rounded-md hover:bg-gray-200 disabled:opacity-30 disabled:hover:bg-transparent"
              title="First page"
            >
              «
            </button>
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={currentPage === 1}
              className="w-7 h-7 flex items-center justify-center rounded-md hover:bg-gray-200 disabled:opacity-30 disabled:hover:bg-transparent"
              title="Previous page"
            >
              ‹
            </button>
            <span className="px-2 text-gray-700 font-medium">
              {currentPage} / {totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
              className="w-7 h-7 flex items-center justify-center rounded-md hover:bg-gray-200 disabled:opacity-30 disabled:hover:bg-transparent"
              title="Next page"
            >
              ›
            </button>
            <button
              onClick={() => setPage(totalPages)}
              disabled={currentPage === totalPages}
              className="w-7 h-7 flex items-center justify-center rounded-md hover:bg-gray-200 disabled:opacity-30 disabled:hover:bg-transparent"
              title="Last page"
            >
              »
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
