"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { Modal, ConfirmDialog } from "@/components/lien/modal";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";

interface LienRowActionsMenuProps {
  lienId: string;
  availableActions: string[];
  onActionComplete: () => void;
  align?: "left" | "right";
  /** Custom trigger content; defaults to a bare ellipsis icon button. */
  trigger?: (props: { onClick: () => void }) => ReactNode;
}

const ACTION_LABELS: Record<string, { label: string; icon: string }> = {
  "prepare-sale": { label: "Sell Lien", icon: "ri-hand-coin-line" },
  "confirm-sale": { label: "Continue Sale", icon: "ri-send-plane-line" },
  "withdraw-sale": {
    label: "Withdraw from Sale",
    icon: "ri-arrow-go-back-line",
  },
  archive: { label: "Archive", icon: "ri-archive-line" },
};

export function LienRowActionsMenu({
  lienId,
  availableActions,
  onActionComplete,
  align = "right",
  trigger,
}: LienRowActionsMenuProps) {
  const router = useRouter();
  const { show: showToast } = useToast();
  const [menuOpen, setMenuOpen] = useState(false);
  const [showDecisionModal, setShowDecisionModal] = useState(false);
  const [confirmAction, setConfirmAction] = useState<
    "withdraw-sale" | "archive" | null
  >(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [keepLoading, setKeepLoading] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [menuOpen]);

  if (availableActions.length === 0) return null;

  const handleAction = (action: string) => {
    setMenuOpen(false);
    if (action === "prepare-sale") {
      router.push(`/selling/portfolio/${lienId}/sell`);
      return;
    }
    if (action === "confirm-sale") {
      router.push(`/selling/portfolio/${lienId}/sell`);
      return;
    }
    if (action === "withdraw-sale" || action === "archive") {
      setConfirmAction(action);
      return;
    }
  };

  const keepAsInternalAsset = async () => {
    setKeepLoading(true);
    try {
      await liensService.submitLien(lienId, {
        sellerStatus: "Internal",
        listingVisibility: "Private",
      });
      showToast("Lien kept as internal asset.", "success");
      setShowDecisionModal(false);
      onActionComplete();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Action failed", "error");
    } finally {
      setKeepLoading(false);
    }
  };

  const runConfirmAction = async () => {
    if (!confirmAction) return;
    setActionLoading(true);
    try {
      if (confirmAction === "withdraw-sale") {
        await liensService.withdrawSale(lienId);
        showToast("Lien withdrawn from sale.", "success");
      } else {
        await liensService.archiveLien(lienId);
        showToast("Lien archived.", "success");
      }
      setConfirmAction(null);
      onActionComplete();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Action failed", "error");
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div
      className="relative"
      ref={menuRef}
      onClick={(e) => e.stopPropagation()}
    >
      {trigger ? (
        trigger({ onClick: () => setMenuOpen((v) => !v) })
      ) : (
        <button
          onClick={() => setMenuOpen((v) => !v)}
          className="w-8 h-8 flex items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 transition-colors"
          aria-label="Lien actions"
        >
          <i className="ri-more-2-fill text-lg" />
        </button>
      )}
      {menuOpen && (
        <div
          className={`absolute ${align === "right" ? "right-0" : "left-0"} mt-1 w-56 bg-white border border-gray-200 rounded-lg shadow-lg z-20 overflow-hidden divide-y divide-gray-100`}
        >
          {availableActions.map((action) => {
            const meta = ACTION_LABELS[action];
            if (!meta) return null;
            return (
              <button
                key={action}
                onClick={() => handleAction(action)}
                className="w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50 flex items-center gap-2"
              >
                <i className={`${meta.icon} text-gray-400`} />
                {meta.label}
              </button>
            );
          })}
        </div>
      )}

      <Modal
        open={showDecisionModal}
        onClose={() => setShowDecisionModal(false)}
        title="What Would You Like to Do With This Lien?"
        size="sm"
        footer={
          <>
            <button
              onClick={keepAsInternalAsset}
              disabled={keepLoading}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
            >
              {keepLoading ? "Keeping..." : "Keep"}
            </button>
            <button
              onClick={() => {
                setShowDecisionModal(false);
                router.push(`/selling/portfolio/${lienId}/sell`);
              }}
              disabled={keepLoading}
              className="text-sm px-4 py-2 bg-[#EE7132] hover:bg-[#EE7132]/90 text-white rounded-lg disabled:opacity-50"
            >
              Sell
            </button>
          </>
        }
      >
        <p className="text-sm text-gray-600">
          Choose whether to keep this lien in your portfolio or proceed with
          selling it to a funding company.
        </p>
      </Modal>

      <ConfirmDialog
        open={confirmAction !== null}
        onClose={() => setConfirmAction(null)}
        onConfirm={runConfirmAction}
        loading={actionLoading}
        title={
          confirmAction === "withdraw-sale"
            ? "Withdraw From Sale?"
            : "Archive This Lien?"
        }
        description={
          confirmAction === "withdraw-sale"
            ? "This lien will no longer be visible to the buyer and will need to be re-submitted for sale."
            : "Archived liens are hidden from the active portfolio. This can't be undone through this workflow."
        }
        confirmLabel={
          confirmAction === "withdraw-sale" ? "Withdraw" : "Archive"
        }
        confirmVariant="danger"
      />
    </div>
  );
}
