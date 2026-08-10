"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { submitFundingOfferedLienResponse, type FundingOfferedLienResponseAction } from "@/lib/synqlien-funding-portal/client-actions";
import { formatFundingCurrency } from "@/lib/synqlien-funding-portal/format";
import type { OfferedLienAction } from "@/lib/synqlien-funding-portal/types";
import { OfferedLienResponseAlert, OfferedLienResponseDialog } from "./offered-lien-response-dialog";
import { notifyFundingNotificationsChanged } from "./funding-notifications";

export function OfferedLienRowActions({
  lienNumber,
  detailHref,
  id,
  sellerName,
  sellerCompany,
  askAmount,
  allowedActions = [],
}: {
  id: string;
  lienNumber: string;
  detailHref: string | null;
  sellerName: string;
  sellerCompany?: string | null;
  askAmount?: number | null;
  allowedActions?: OfferedLienAction[];
}) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [pendingAction, setPendingAction] = useState<FundingOfferedLienResponseAction | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [completedAction, setCompletedAction] = useState<FundingOfferedLienResponseAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;

    function handlePointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [open]);

  async function confirmResponse() {
    if (!pendingAction || submitting) return;
    setSubmitting(true);
    setError(null);
    const result = await submitFundingOfferedLienResponse(id, pendingAction);
    setSubmitting(false);
    if (!result.ok) {
      setError(result.error?.message ?? "The lien offer response could not be recorded.");
      return;
    }
    setCompletedAction(pendingAction);
    setPendingAction(null);
    notifyFundingNotificationsChanged();
    router.refresh();
  }

  const responseActions = allowedActions.filter(
    (action): action is FundingOfferedLienResponseAction => action === "accept" || action === "decline",
  );

  return (
    <div ref={rootRef} className="relative inline-flex">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Open actions for ${lienNumber}`}
        onClick={() => setOpen(value => !value)}
        className="inline-flex h-8 w-8 items-center justify-center rounded-[8px] text-[#525252] transition-colors hover:bg-[#f5f5f5] hover:text-[#0a0a0a] focus:outline-none focus:ring-2 focus:ring-[#f4a076]"
      >
        <i className="ri-more-2-fill text-[20px]" />
      </button>

      {open ? (
        <div
          role="menu"
          className="absolute right-0 top-9 z-20 min-w-[128px] rounded-[10px] border border-[#e5e5e5] bg-white p-1 text-left shadow-[0_8px_24px_rgba(0,0,0,0.12)]"
        >
          {detailHref ? (
            <Link
              href={detailHref}
              role="menuitem"
              className="flex h-9 items-center gap-2 rounded-[8px] px-3 text-[14px] font-medium leading-[1.6] text-[#0a0a0a] transition-colors hover:bg-[#f5f5f5] focus:bg-[#f5f5f5] focus:outline-none"
            >
              <i className="ri-eye-line text-[16px] text-[#525252]" />
              View
            </Link>
          ) : (
            <span
              role="menuitem"
              aria-disabled="true"
              className="flex h-9 cursor-not-allowed items-center gap-2 rounded-[8px] px-3 text-[14px] font-medium leading-[1.6] text-[#a3a3a3]"
            >
              <i className="ri-eye-off-line text-[16px]" />
              View
            </span>
          )}
          {responseActions.map(action => (
            <button
              key={action}
              type="button"
              role="menuitem"
              onClick={() => {
                setOpen(false);
                setError(null);
                setPendingAction(action);
              }}
              className={`flex h-9 w-full items-center gap-2 rounded-[8px] px-3 text-left text-[14px] font-medium leading-[1.6] transition-colors hover:bg-[#f5f5f5] focus:bg-[#f5f5f5] focus:outline-none ${action === "decline" ? "text-[#ef4444]" : "text-[#0a0a0a]"}`}
            >
              <i className={`${action === "accept" ? "ri-file-check-line" : "ri-file-close-line"} text-[16px]`} />
              {action === "accept" ? "Accept" : "Decline"}
            </button>
          ))}
        </div>
      ) : null}
      {pendingAction ? (
        <OfferedLienResponseDialog
          action={pendingAction}
          lienNumber={lienNumber}
          sellerName={sellerName}
          sellerCompany={sellerCompany}
          askAmount={askAmount == null ? null : formatFundingCurrency(askAmount)}
          submitting={submitting}
          error={error}
          onCancel={() => {
            if (!submitting) {
              setPendingAction(null);
              setError(null);
            }
          }}
          onConfirm={() => void confirmResponse()}
        />
      ) : null}
      {completedAction ? <OfferedLienResponseAlert action={completedAction} onDismiss={() => setCompletedAction(null)} /> : null}
    </div>
  );
}
