"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
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
    <div className="inline-flex">
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            aria-label={`Open actions for ${lienNumber}`}
            className="inline-flex h-8 w-8 items-center justify-center rounded-[8px] text-[#525252] transition-colors hover:bg-[#f5f5f5] hover:text-[#0a0a0a] focus:outline-none focus:ring-2 focus:ring-[#f4a076]"
          >
            <i className="ri-more-2-fill text-[20px]" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          sideOffset={4}
          collisionPadding={16}
          className="w-[224px] rounded-[8px] p-1 text-left shadow-[0_4px_6px_-1px_rgba(0,0,0,0.1),0_2px_4px_-2px_rgba(0,0,0,0.1)]"
        >
          {detailHref ? (
            <DropdownMenuItem asChild className="rounded-[4px] px-2 py-1.5 text-[14px] font-normal leading-[1.6] text-[#0a0a0a]">
              <Link href={detailHref}>
                <i className="ri-eye-line text-[16px] text-[#525252]" />
                View
              </Link>
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem disabled className="rounded-[4px] px-2 py-1.5 text-[14px] font-normal leading-[1.6]">
              <i className="ri-eye-off-line text-[16px]" />
              View
            </DropdownMenuItem>
          )}
          {responseActions.map(action => (
            <DropdownMenuItem
              key={action}
              onSelect={() => {
                setError(null);
                setPendingAction(action);
              }}
              className={`rounded-[4px] px-2 py-1.5 text-[14px] font-normal leading-[1.6] ${action === "decline" ? "text-[#ef4444] focus:text-[#ef4444]" : "text-[#0a0a0a]"}`}
            >
              <i className={`${action === "accept" ? "ri-file-check-line" : "ri-file-close-line"} text-[16px]`} />
              {action === "accept" ? "Accept" : "Decline"}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
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
