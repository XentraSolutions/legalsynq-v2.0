"use client";

import { useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import {
  Tag,
  Send,
  Inbox,
  Undo2,
  Trash2,
  type LucideIcon,
} from "lucide-react";
import { Modal, ConfirmDialog } from "@/components/selling/modal";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { Button } from "@/components/selling/button";
import { LienDetail, LienListItem, liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";

interface LienRowActionsMenuProps {
  lienId: string;
  lien?: LienListItem;
  availableActions: string[];
  onActionComplete: () => void;
  align?: "left" | "right";
  /** Custom trigger element; defaults to a bare ellipsis icon button. */
  trigger?: ReactNode;
  /** Show the Keep/Sell decision modal automatically when this lien loads. */
  autoOpenDecision?: boolean;
}

const ACTION_LABELS: Record<
  string,
  { label: string; icon: LucideIcon; danger?: boolean }
> = {
  "prepare-sale": { label: "Sell Lien", icon: Tag },
  "confirm-sale": { label: "Continue Sale", icon: Send },
  keep: { label: "Keep", icon: Inbox },
  "withdraw-sale": { label: "Withdraw from Sale", icon: Undo2 },
  archive: { label: "Delete Lien", icon: Trash2, danger: true },
};

// The liens list endpoint doesn't populate `availableActions` (only the
// single-lien detail endpoint does), so pending/internal rows fall back to
// this static Keep/Sell/Archive set to mirror what the details view offers.
const PENDING_FALLBACK_ACTIONS = ["prepare-sale", "keep", "archive"];

export function LienRowActionsMenu({
  lienId,
  lien,
  availableActions,
  onActionComplete,
  align = "right",
  trigger,
  autoOpenDecision = false,
}: LienRowActionsMenuProps) {
  const router = useRouter();
  const { show: showToast } = useToast();
  const [showDecisionModal, setShowDecisionModal] = useState(autoOpenDecision);
  const [confirmAction, setConfirmAction] = useState<
    "withdraw-sale" | "archive" | "keep" | null
  >(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [keepLoading, setKeepLoading] = useState(false);

  const status = lien?.sellerStatus ?? lien?.status;
  const resolvedActions =
    availableActions.length === 0 &&
    (status === "Pending" || status === "Internal")
      ? PENDING_FALLBACK_ACTIONS
      : availableActions;

  if (resolvedActions.length === 0) return null;

  const handleAction = (action: string) => {
    if (action === "prepare-sale" || action === "confirm-sale") {
      router.push(`/selling/portfolio/lien/${lienId}/sell`);
      return;
    }
    if (action === "keep" || action === "withdraw-sale" || action === "archive") {
      setConfirmAction(action);
      return;
    }
  };

  const items: ActionMenuItem[] = resolvedActions
    .filter((action) => ACTION_LABELS[action])
    .map((action) => {
      const meta = ACTION_LABELS[action];
      return {
        label: meta.label,
        icon: meta.icon,
        variant: meta.danger ? ("danger" as const) : ("default" as const),
        onClick: () => handleAction(action),
      };
    });

  const keepAsInternalAsset = async () => {
    setKeepLoading(true);
    try {
      await liensService.submitLien(lienId, {
        ...lien,
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
      } else if (confirmAction === "archive") {
        await liensService.archiveLien(lienId);
        showToast("Lien archived.", "success");
      } else {
        await liensService.submitLien(lienId, {
          ...lien,
          sellerStatus: "Internal",
          listingVisibility: "Private",
        });
        showToast("Lien kept as internal asset.", "success");
      }
      setConfirmAction(null);
      setShowDecisionModal(false);
      onActionComplete();
    } catch (err) {
      showToast(err instanceof Error ? err.message : "Action failed", "error");
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="relative" onClick={(e) => e.stopPropagation()}>
      <ActionMenu
        items={items}
        trigger={trigger}
        align={align === "right" ? "end" : "start"}
      />

      <Modal
        open={showDecisionModal}
        onClose={() => setShowDecisionModal(false)}
        title="What Would You Like to Do With This Lien?"
        size="sm"
        footer={
          <>
            <Button
              variant="secondary"
              loading={keepLoading}
              onClick={keepAsInternalAsset}
            >
              {keepLoading ? "Keeping..." : "Keep"}
            </Button>
            <Button
              variant="primary"
              disabled={keepLoading}
              onClick={() => {
                setShowDecisionModal(false);
                router.push(`/selling/portfolio/lien/${lienId}/sell`);
              }}
            >
              Sell
            </Button>
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
            : confirmAction === "archive"
              ? "Delete This Lien?"
              : "Keep as Internal Asset?"
        }
        description={
          confirmAction === "withdraw-sale"
            ? "This lien will no longer be visible to the buyer and will need to be re-submitted for sale."
            : confirmAction === "archive"
              ? "Deleted liens are hidden from the active portfolio. This can't be undone through this workflow."
              : "This lien will be kept as a private internal asset instead of being listed for sale."
        }
        confirmLabel={
          confirmAction === "withdraw-sale"
            ? "Withdraw"
            : confirmAction === "archive"
              ? "Delete"
              : "Keep"
        }
        confirmVariant={confirmAction === "keep" ? "primary" : "danger"}
      />
    </div>
  );
}
