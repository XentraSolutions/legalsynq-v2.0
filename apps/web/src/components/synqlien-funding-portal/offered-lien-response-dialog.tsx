"use client";

import { useEffect, useRef } from "react";
import type { FundingOfferedLienResponseAction } from "@/lib/synqlien-funding-portal/client-actions";

interface OfferedLienResponseDialogProps {
  action: FundingOfferedLienResponseAction;
  lienNumber: string;
  sellerName: string;
  sellerCompany?: string | null;
  askAmount?: string | null;
  submitting: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}

export function OfferedLienResponseDialog({
  action,
  lienNumber,
  sellerName,
  sellerCompany,
  askAmount,
  submitting,
  error,
  onCancel,
  onConfirm,
}: OfferedLienResponseDialogProps) {
  const confirmRef = useRef<HTMLButtonElement>(null);
  const accepting = action === "accept";
  const company = sellerCompany?.trim();
  const party = company ? `${sellerName} from ${company}` : sellerName;

  useEffect(() => {
    confirmRef.current?.focus();

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape" && !submitting) onCancel();
    }

    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [onCancel, submitting]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/45 px-4 backdrop-blur-[2px]"
      role="presentation"
      onMouseDown={event => {
        if (event.target === event.currentTarget && !submitting) onCancel();
      }}
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="offered-lien-response-title"
        className="w-full max-w-[512px] overflow-hidden rounded-[16px] border border-[#e5e5e5] bg-white shadow-[0_24px_60px_rgba(0,0,0,0.24)]"
      >
        <div className="flex items-start gap-3 px-6 py-5">
          <span
            className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] ${
              accepting ? "bg-[#f5f5f5] text-[#0a0a0a]" : "bg-[#fee2e2] text-[#ef4444]"
            }`}
          >
            <i className={`${accepting ? "ri-file-check-line" : "ri-file-close-line"} text-[18px]`} />
          </span>

          <div className="min-w-0 flex-1">
            <h2 id="offered-lien-response-title" className="text-[18px] font-semibold leading-7 text-[#0a0a0a]">
              {accepting ? "Accept This Lien?" : "Decline This Lien?"}
            </h2>
            <p className="mt-1 text-[14px] font-normal leading-[1.6] text-[#737373]">
              You&apos;re about to {action} lien <span className="font-medium text-[#0a0a0a]">{lienNumber}</span>, submitted by{" "}
              <span className="font-medium text-[#0a0a0a]">{party}</span>
              {askAmount ? <> worth <span className="font-medium text-[#0a0a0a]">{askAmount}</span></> : null}.
            </p>
            {error ? (
              <p role="alert" className="mt-3 rounded-[8px] bg-red-50 px-3 py-2 text-[13px] font-medium leading-5 text-red-700">
                {error}
              </p>
            ) : null}
          </div>

          <button
            type="button"
            aria-label="Close confirmation"
            disabled={submitting}
            onClick={onCancel}
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] text-[#525252] transition-colors hover:bg-[#f5f5f5] disabled:opacity-50"
          >
            <i className="ri-close-line text-[18px]" />
          </button>
        </div>

        <div className="flex justify-end gap-2 border-t border-[#e5e5e5] bg-[#fafafa] px-6 py-4">
          <button
            type="button"
            disabled={submitting}
            onClick={onCancel}
            className="inline-flex h-9 items-center justify-center rounded-[8px] border border-[#e5e5e5] bg-white px-4 text-[14px] font-medium text-[#0a0a0a] shadow-[0_1px_2px_rgba(0,0,0,0.08)] transition-colors hover:bg-[#f5f5f5] disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            ref={confirmRef}
            type="button"
            disabled={submitting}
            onClick={onConfirm}
            className={`inline-flex h-9 min-w-[96px] items-center justify-center gap-2 rounded-[8px] px-4 text-[14px] font-medium text-white shadow-[0_1px_2px_rgba(0,0,0,0.08)] transition-colors disabled:cursor-wait disabled:opacity-70 ${
              accepting ? "bg-[#ee7132] hover:bg-[#d85f25]" : "bg-[#ef7f86] hover:bg-[#dc626b]"
            }`}
          >
            {submitting ? <i className="ri-loader-4-line animate-spin text-[16px]" /> : null}
            {submitting ? (accepting ? "Accepting..." : "Declining...") : accepting ? "Yes, Accept" : "Yes, Decline"}
          </button>
        </div>
      </section>
    </div>
  );
}

export function OfferedLienResponseAlert({
  action,
  onDismiss,
}: {
  action: FundingOfferedLienResponseAction;
  onDismiss: () => void;
}) {
  const accepted = action === "accept";
  return (
    <div
      role="status"
      className="fixed right-6 top-6 z-40 flex w-[min(512px,calc(100vw-3rem))] items-start gap-3 rounded-[10px] border border-[#e5e5e5] bg-white px-4 py-3 shadow-[0_10px_24px_rgba(0,0,0,0.14)]"
    >
      <i className={`${accepted ? "ri-checkbox-circle-line text-[#22c55e]" : "ri-close-circle-line text-[#ef4444]"} mt-0.5 text-[18px]`} />
      <div className="min-w-0 flex-1">
        <p className={`text-[14px] font-medium leading-[1.6] ${accepted ? "text-[#15803d]" : "text-[#dc2626]"}`}>
          Lien {accepted ? "Accepted" : "Declined"}
        </p>
        <p className="text-[14px] font-normal leading-[1.6] text-[#0a0a0a]">
          The lien offer was successfully {accepted ? "accepted" : "declined"}.
        </p>
      </div>
      <button type="button" aria-label="Dismiss notification" onClick={onDismiss} className="text-[#525252] hover:text-[#0a0a0a]">
        <i className="ri-close-line text-[16px]" />
      </button>
    </div>
  );
}
