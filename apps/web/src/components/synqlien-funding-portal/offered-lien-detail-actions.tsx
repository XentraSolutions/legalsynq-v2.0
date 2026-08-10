"use client";

import { useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  submitFundingOfferedLienResponse,
  type FundingOfferedLienResponseAction,
} from "@/lib/synqlien-funding-portal/client-actions";
import { formatFundingCurrency } from "@/lib/synqlien-funding-portal/format";
import type { OfferedLienAction } from "@/lib/synqlien-funding-portal/types";
import { OfferedLienResponseAlert, OfferedLienResponseDialog } from "./offered-lien-response-dialog";
import { notifyFundingNotificationsChanged } from "./funding-notifications";

interface OfferedLienDetailActionsProps {
  id: string;
  status: string;
  allowedActions?: OfferedLienAction[];
  lienNumber: string;
  sellerName: string;
  sellerCompany?: string | null;
  askAmount?: number | null;
}

export function OfferedLienDetailActions({
  id,
  status,
  allowedActions = [],
  lienNumber,
  sellerName,
  sellerCompany,
  askAmount,
}: OfferedLienDetailActionsProps) {
  const router = useRouter();
  const closeTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState<FundingOfferedLienResponseAction | null>(null);
  const [pendingAction, setPendingAction] = useState<FundingOfferedLienResponseAction | null>(null);
  const [completedAction, setCompletedAction] = useState<FundingOfferedLienResponseAction | null>(null);
  const [error, setError] = useState<string | null>(null);
  const normalizedStatus = status.trim().toLowerCase();
  const canRespond = normalizedStatus === "pending" || normalizedStatus === "offered";
  const actions: FundingOfferedLienResponseAction[] = canRespond
    ? allowedActions.filter((action): action is FundingOfferedLienResponseAction => action === "accept" || action === "decline")
    : [];
  const hasActions = actions.length > 0;
  const disabled = Boolean(submitting) || !hasActions;

  async function handleAction(action: FundingOfferedLienResponseAction) {
    if (disabled) return;

    setSubmitting(action);
    setError(null);
    const result = await submitFundingOfferedLienResponse(id, action);
    setSubmitting(null);

    if (result.ok) {
      setOpen(false);
      setPendingAction(null);
      setCompletedAction(action);
      notifyFundingNotificationsChanged();
      router.refresh();
      return;
    }

    setError(result.error?.message ?? "The lien offer response could not be recorded.");
  }

  function handleBlur() {
    closeTimerRef.current = setTimeout(() => setOpen(false), 100);
  }

  function handleFocus() {
    if (closeTimerRef.current) {
      clearTimeout(closeTimerRef.current);
      closeTimerRef.current = null;
    }
  }

  return (
    <div className="relative shrink-0" onBlur={handleBlur} onFocus={handleFocus}>
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={hasActions ? open : false}
        disabled={disabled}
        title={hasActions ? "Actions" : "This lien is not accepting responses."}
        onClick={() => {
          if (!hasActions) return;
          setOpen(value => !value);
        }}
        className="inline-flex h-[38px] shrink-0 items-center overflow-hidden rounded-[10px] bg-[#ee7132] text-[14px] font-medium leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-[#d85f25] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] disabled:cursor-not-allowed disabled:bg-[#e5e5e5] disabled:text-[#737373]"
      >
        <span className="px-4 py-2">Actions</span>
        <span
          className={`flex h-full w-9 items-center justify-center border-l ${
            disabled ? "border-[#d4d4d4]" : "border-[#f4a076]"
          }`}
        >
          <i className="ri-arrow-down-s-line text-[16px]" />
        </span>
      </button>

      {open && hasActions ? (
        <div
          role="menu"
          className="absolute right-0 z-20 mt-2 w-44 overflow-hidden rounded-[10px] border border-[#e5e5e5] bg-white py-1 text-[14px] shadow-[0_10px_24px_rgba(0,0,0,0.12)]"
        >
          {actions.map(action => (
            <button
              key={action}
              type="button"
              role="menuitem"
              disabled={disabled}
              onClick={() => {
                setOpen(false);
                setError(null);
                setPendingAction(action);
              }}
              className={`flex w-full items-center gap-2 px-3 py-2 text-left font-medium transition-colors hover:bg-[#fafafa] disabled:cursor-not-allowed disabled:opacity-60 ${
                action === "decline" ? "text-[#e11d48]" : "text-[#0a0a0a]"
              }`}
            >
              <i className={`${action === "accept" ? "ri-check-line" : "ri-close-line"} text-[16px]`} />
              <span>
                {submitting === action ? loadingLabel(action) : labelAction(action)}
              </span>
            </button>
          ))}
        </div>
      ) : null}

      {error && !pendingAction ? (
        <p role="alert" className="absolute right-0 mt-2 w-72 rounded-[10px] border border-red-200 bg-red-50 px-3 py-2 text-[13px] font-medium leading-[1.5] text-red-700 shadow-[0_8px_20px_rgba(0,0,0,0.08)]">
          {error}
        </p>
      ) : null}
      {pendingAction ? (
        <OfferedLienResponseDialog
          action={pendingAction}
          lienNumber={lienNumber}
          sellerName={sellerName}
          sellerCompany={sellerCompany}
          askAmount={askAmount == null ? null : formatFundingCurrency(askAmount)}
          submitting={submitting === pendingAction}
          error={error}
          onCancel={() => {
            if (!submitting) {
              setPendingAction(null);
              setError(null);
            }
          }}
          onConfirm={() => void handleAction(pendingAction)}
        />
      ) : null}
      {completedAction ? <OfferedLienResponseAlert action={completedAction} onDismiss={() => setCompletedAction(null)} /> : null}
    </div>
  );
}

function labelAction(action: FundingOfferedLienResponseAction): string {
  return action === "accept" ? "Accept" : "Decline";
}

function loadingLabel(action: FundingOfferedLienResponseAction): string {
  return action === "accept" ? "Accepting..." : "Declining...";
}
