"use client";

import { useState, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import {
  Tag,
  Send,
  Inbox,
  Undo2,
  Archive,
  RotateCcw,
  type LucideIcon,
} from "lucide-react";
import { Modal, ConfirmDialog } from "@/components/selling/modal";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { Button } from "@/components/selling/button";
import { CreateCaseForm } from "@/components/lien/forms/create-case-form";
import {
  type LienListItem,
  type MoveSellingLienToManagementCaseInfoRequest,
  liensService,
} from "@/lib/selling";
import { toast } from "sonner";

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
  archive: { label: "Archive Lien", icon: Archive, danger: true },
  restore: { label: "Restore Lien", icon: RotateCcw },
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
  const [showDecisionModal, setShowDecisionModal] = useState(autoOpenDecision);
  const [showCreateCaseModal, setShowCreateCaseModal] = useState(false);
  const [confirmAction, setConfirmAction] = useState<
    "withdraw-sale" | "archive" | "restore" | null
  >(null);
  const [actionLoading, setActionLoading] = useState(false);

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
    if (action === "keep") {
      setShowCreateCaseModal(true);
      return;
    }
    if (action === "withdraw-sale" || action === "archive" || action === "restore") {
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

  const moveToManagement = (
    caseInfo: MoveSellingLienToManagementCaseInfoRequest,
  ) =>
    liensService.moveToManagementV2(lienId, {
      reason: "Keep internally",
      caseInfo,
    });

  const runConfirmAction = async () => {
    if (!confirmAction) return;
    setActionLoading(true);
    try {
      if (confirmAction === "withdraw-sale") {
        await liensService.withdrawSale(lienId);
        toast.success("Lien withdrawn from sale and returned to Pending.");
      } else if (confirmAction === "archive") {
        await liensService.archiveLien(lienId);
        toast.success("Lien archived.");
      } else if (confirmAction === "restore") {
        await liensService.restoreLien(lienId);
        toast.success("Lien restored.");
      }
      setConfirmAction(null);
      setShowDecisionModal(false);
      onActionComplete();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Action failed");
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
              onClick={() => {
                setShowDecisionModal(false);
                setShowCreateCaseModal(true);
              }}
            >
              Keep
            </Button>
            <Button
              variant="primary"
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

      <CreateCaseForm
        open={showCreateCaseModal}
        onClose={() => setShowCreateCaseModal(false)}
        onMoveToManagement={moveToManagement}
        onCreated={(caseId) => {
          setShowCreateCaseModal(false);
          onActionComplete();
          router.push(`/lien/cases/${caseId}`);
        }}
      />

      <ConfirmDialog
        open={confirmAction !== null}
        onClose={() => setConfirmAction(null)}
        onConfirm={runConfirmAction}
        loading={actionLoading}
        title={
          confirmAction === "withdraw-sale"
            ? "Withdraw From Sale?"
            : confirmAction === "archive"
              ? "Archive This Lien?"
            : confirmAction === "restore"
              ? "Restore This Lien?"
              : "Confirm Action"
        }
        description={
          confirmAction === "withdraw-sale"
            ? "This lien will no longer be visible to the buyer and will need to be re-submitted for sale."
            : confirmAction === "archive"
              ? "This lien will be hidden from active portfolio lists, but its record and history will be retained."
            : confirmAction === "restore"
              ? "This lien will be restored to the Pending list for active portfolio tracking."
              : ""
        }
        confirmLabel={
          confirmAction === "withdraw-sale"
            ? "Withdraw"
            : confirmAction === "archive"
              ? "Archive"
            : confirmAction === "restore"
              ? "Restore"
              : "Confirm"
        }
        confirmVariant={confirmAction === "restore" ? "primary" : "danger"}
      />
    </div>
  );
}
