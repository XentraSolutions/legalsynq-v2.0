"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { formatFundingCurrency, formatFundingDateTime } from "@/lib/synqlien-funding-portal/format";
import type { OfferedLienRow, OfferedLiensResult } from "@/lib/synqlien-funding-portal/types";

const READ_STORAGE_KEY = "ls_synqlien_funding_read_notifications";
const READ_EVENT = "synqlien-funding-notifications-read";
const DATA_EVENT = "synqlien-funding-notifications-changed";

export function notifyFundingNotificationsChanged() {
  if (typeof window !== "undefined") window.dispatchEvent(new Event(DATA_EVENT));
}

export function FundingNotificationBell() {
  const [open, setOpen] = useState(false);
  const [rows, setRows] = useState<OfferedLienRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [loaded, setLoaded] = useState(false);
  const [readIds, setReadIds] = useState<Set<string>>(new Set());
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setReadIds(readNotificationIds());
    const syncReadState = () => setReadIds(readNotificationIds());
    window.addEventListener(READ_EVENT, syncReadState);
    window.addEventListener("storage", syncReadState);
    return () => {
      window.removeEventListener(READ_EVENT, syncReadState);
      window.removeEventListener("storage", syncReadState);
    };
  }, []);

  useEffect(() => {
    const reload = () => {
      setRows([]);
      setLoaded(false);
    };
    window.addEventListener(DATA_EVENT, reload);
    return () => window.removeEventListener(DATA_EVENT, reload);
  }, []);

  useEffect(() => {
    if (loaded || loading) return;
    setLoading(true);
    void fetch("/api/lien/api/liens/selling/buyer/liens?page=1&pageSize=8&sort=initialServiceDate&direction=desc", {
      credentials: "include",
      cache: "no-store",
    })
      .then(async response => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json() as Promise<OfferedLiensResult | { data: OfferedLiensResult }>;
      })
      .then(body => setRows(unwrapRows(body).slice(0, 3)))
      .catch(() => setRows([]))
      .finally(() => {
        setLoaded(true);
        setLoading(false);
      });
  }, [loaded, loading]);

  useEffect(() => {
    if (!open) return;
    const close = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener("pointerdown", close);
    return () => document.removeEventListener("pointerdown", close);
  }, [open]);

  const unreadCount = rows.filter(row => !readIds.has(row.id)).length;

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        aria-label={unreadCount ? `Notifications, ${unreadCount} unread` : "Notifications"}
        aria-haspopup="dialog"
        aria-expanded={open}
        onClick={() => setOpen(value => !value)}
        className="relative flex h-7 w-7 items-center justify-center rounded-[8px] text-[#0a0a0a] transition-colors hover:bg-[#f5f5f5]"
      >
        <i className="ri-notification-3-line text-[16px]" />
        {unreadCount ? <span className="absolute right-1 top-1 h-1.5 w-1.5 rounded-full bg-[#ee7132] ring-2 ring-white" /> : null}
      </button>

      {open ? (
        <section
          role="dialog"
          aria-label="Notifications"
          className="absolute right-0 top-10 z-50 w-[min(450px,calc(100vw-2rem))] overflow-hidden rounded-[16px] border border-[#e5e5e5] bg-white shadow-[0_20px_32px_rgba(0,0,0,0.16)]"
        >
          <header className="flex items-center justify-between border-b border-[#e5e5e5] px-4 py-3">
            <h2 className="text-[16px] font-semibold leading-[1.6]">Notifications</h2>
            {unreadCount ? (
              <button type="button" onClick={() => setReadIds(markNotificationsRead(rows.map(row => row.id)))} className="rounded-[6px] p-1 text-[14px] font-medium text-[#ee7132] hover:bg-[#fdf1eb]">
                Mark all as read
              </button>
            ) : null}
          </header>

          {loading ? (
            <div className="flex h-48 items-center justify-center text-[#737373]"><i className="ri-loader-4-line animate-spin text-[22px]" /></div>
          ) : rows.length === 0 ? (
            <NotificationEmptyState compact />
          ) : (
            <div>
              {rows.map(row => (
                <NotificationPreview key={row.id} row={row} unread={!readIds.has(row.id)} onRead={() => setReadIds(markNotificationsRead([row.id]))} />
              ))}
            </div>
          )}

          <Link href="/funding/notifications" onClick={() => setOpen(false)} className="flex h-[54px] items-center justify-between bg-[#f5f5f5] px-4 text-[14px] font-medium text-[#0a0a0a] hover:text-[#ee7132]">
            View All Notifications <i className="ri-arrow-right-line text-[16px]" />
          </Link>
        </section>
      ) : null}
    </div>
  );
}

function NotificationPreview({ row, unread, onRead }: { row: OfferedLienRow; unread: boolean; onRead: () => void }) {
  const status = row.status.trim().toLowerCase();
  const title = status === "accepted" ? "Lien Offer Accepted" : status === "declined" ? "Lien Offer Declined" : "New Lien Offer Received";
  const message = status === "pending" || status === "offered"
    ? `${row.sellerName} submitted a lien offer for ${formatFundingCurrency(row.askAmount ?? row.offeredAmount)}.`
    : `The ${formatFundingCurrency(row.askAmount ?? row.offeredAmount)} offer submitted by ${row.sellerName} was ${status}.`;
  return (
    <Link
      href={row.detailHref || `/funding/offered-liens/${row.id}`}
      onClick={onRead}
      className={`flex gap-4 border-b border-[#e5e5e5] p-4 transition-colors hover:bg-[#fafafa] ${unread ? "border-l-[3px] border-l-[#ee7132] bg-[rgba(238,113,50,0.03)]" : ""}`}
    >
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#fdf1eb] text-[16px] font-medium text-[#a95024]">{initials(row.sellerName)}</span>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2"><p className="truncate text-[14px] font-bold text-[#0a0a0a]">{title}</p>{unread ? <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-[#ee7132]" /> : null}</div>
        <p className="mt-1 text-[14px] leading-[1.6] text-[#404040]">{message}</p>
        <p className="mt-3 text-[12px] text-[#737373]">{formatFundingDateTime(row.receivedAtUtc)}</p>
      </div>
    </Link>
  );
}

export function FundingNotificationsList({ rows }: { rows: OfferedLienRow[] }) {
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");
  const [readIds, setReadIds] = useState<Set<string>>(new Set());
  useEffect(() => setReadIds(readNotificationIds()), []);
  const filtered = rows.filter(row => {
    const matchesStatus = !status || row.status.toLowerCase() === status.toLowerCase();
    const term = query.trim().toLowerCase();
    return matchesStatus && (!term || `${row.lienNumber} ${row.sellerName}`.toLowerCase().includes(term));
  });
  const unreadPending = rows.filter(row => !readIds.has(row.id) && /pending|offered/i.test(row.status)).length;

  return (
    <div className="space-y-4">
      {unreadPending ? (
        <div className="flex items-start gap-3 rounded-[10px] border border-[#e5e5e5] bg-white px-4 py-3 shadow-[0_2px_6px_rgba(0,0,0,0.08)]">
          <i className="ri-information-line mt-0.5 text-[16px] text-[#eab308]" /><div className="flex-1"><p className="text-[14px] font-medium text-[#a16207]">Unread Lien Notifications</p><p className="text-[14px] text-[#0a0a0a]">You have {unreadPending} medical lien {unreadPending === 1 ? "notification" : "notifications"} awaiting your response.</p></div>
          <button aria-label="Mark notification alert as read" onClick={() => setReadIds(markNotificationsRead(rows.map(row => row.id)))}><i className="ri-close-line text-[#737373]" /></button>
        </div>
      ) : null}
      <div><h1 className="text-[28px] font-semibold leading-9">Notifications</h1><p className="mt-1 text-[14px] text-[#737373]">Stay up to date with important alerts, updates, and activities</p></div>
      <label className="relative block"><i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-[#737373]" /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Search..." className="h-9 w-full rounded-[8px] border border-[#e5e5e5] pl-9 pr-3 text-[14px] outline-none focus:border-[#f4a076]" /></label>
      <div className="grid h-9 grid-cols-4 rounded-[8px] bg-[#f5f5f5] p-px">{["", "Pending", "Accepted", "Declined"].map(item => <button key={item || "all"} onClick={() => setStatus(item)} className={`rounded-[7px] text-[12px] font-medium ${status === item ? "border border-[#e5e5e5] bg-white shadow-sm" : "text-[#737373]"}`}>{item || "All"}</button>)}</div>
      {filtered.length === 0 ? <section className="rounded-[16px] border border-[#e5e5e5] bg-white"><NotificationEmptyState /></section> : (
        <section className="overflow-hidden rounded-[16px] border border-[#e5e5e5] bg-white shadow-sm"><div className="overflow-x-auto"><table className="w-full min-w-[850px] border-collapse"><thead className="bg-[#f5f5f5]"><tr>{["Lien ID","Seller Name","Ask Amount","Status","Offered Date",""] .map(label => <th key={label} className="h-10 px-4 text-left text-[14px] font-medium">{label}</th>)}</tr></thead><tbody>{filtered.map(row => <NotificationTableRow key={row.id} row={row} unread={!readIds.has(row.id)} onRead={() => setReadIds(markNotificationsRead([row.id]))} />)}</tbody></table></div></section>
      )}
    </div>
  );
}

function NotificationTableRow({ row, unread, onRead }: { row: OfferedLienRow; unread: boolean; onRead: () => void }) {
  const tone = /accepted/i.test(row.status) ? "bg-green-100 text-green-700" : /declined/i.test(row.status) ? "bg-red-100 text-red-600" : "bg-amber-100 text-amber-700";
  return <tr className={`border-b border-[#e5e5e5] ${unread ? "border-l-2 border-l-[#ee7132] bg-[rgba(238,113,50,0.03)]" : ""}`}><td className="h-[53px] px-4 text-[14px]"><Link href={row.detailHref || `/funding/offered-liens/${row.id}`} onClick={onRead} className="hover:text-[#ee7132]">{row.lienNumber}</Link></td><td className="px-4 text-[14px]">{row.sellerName}</td><td className="px-4 text-[14px]">{formatFundingCurrency(row.askAmount ?? row.offeredAmount)}</td><td className="px-4"><span className={`rounded-full px-3 py-1 text-[12px] font-medium ${tone}`}>{row.status}</span></td><td className="px-4 text-[14px]">{formatFundingDateTime(row.receivedAtUtc)}</td><td className="px-4 text-right"><Link href={row.detailHref || `/funding/offered-liens/${row.id}`} onClick={onRead} aria-label={`View ${row.lienNumber}`}><i className="ri-more-2-fill text-[20px]" /></Link></td></tr>;
}

function NotificationEmptyState({ compact = false }: { compact?: boolean }) { return <div className={`flex flex-col items-center justify-center text-center ${compact ? "h-48" : "min-h-[330px]"}`}><span className="flex h-10 w-10 items-center justify-center rounded-[8px] bg-[#f5f5f5]"><i className="ri-notification-3-line text-[20px]" /></span><p className="mt-4 text-[16px] font-semibold">No Notifications Yet</p><p className="mt-2 max-w-sm text-[14px] text-[#737373]">We&apos;ll notify you when there&apos;s something that requires your attention.</p></div>; }
function unwrapRows(body: OfferedLiensResult | { data: OfferedLiensResult }): OfferedLienRow[] { return "data" in body ? body.data?.rows ?? [] : body.rows ?? []; }
function readNotificationIds(): Set<string> { if (typeof window === "undefined") return new Set(); try { const parsed = JSON.parse(localStorage.getItem(READ_STORAGE_KEY) || "[]"); return new Set(Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string") : []); } catch { return new Set(); } }
function markNotificationsRead(ids: string[]): Set<string> { const next = readNotificationIds(); ids.forEach(id => next.add(id)); localStorage.setItem(READ_STORAGE_KEY, JSON.stringify([...next])); window.dispatchEvent(new Event(READ_EVENT)); return new Set(next); }
function initials(name: string): string { const parts = name.trim().split(/\s+/); return `${parts[0]?.[0] ?? "S"}${parts[1]?.[0] ?? parts[0]?.[1] ?? "L"}`.toUpperCase(); }
