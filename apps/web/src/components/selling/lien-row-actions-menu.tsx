"use client";

import { useEffect, useState, type ReactNode } from "react";
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
import { ConfirmDialog } from "@/components/selling/modal";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";

interface LienRowActionsMenuProps {
  lienId: string;
  availableActions: string[];
  onActionComplete: () => void;
  align?: "left" | "right";
  /** Custom trigger element; defaults to a bare ellipsis icon button. */
  trigger?: ReactNode;
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

// The full set of lien actions this UI knows how to render. Exported so
// other lien-action surfaces (e.g. the portfolio table's row menu) can
// detect an action the frontend doesn't recognize yet, instead of silently
// dropping it.
export const KNOWN_LIEN_ACTIONS = Object.keys(ACTION_LABELS);

export function LienRowActionsMenu({
  lienId,
  availableActions,
  onActionComplete,
  align = "right",
  trigger,
}: LienRowActionsMenuProps) {
  const router = useRouter();

  const [confirmAction, setConfirmAction] = useState<
    "withdraw-sale" | "archive" | "restore" | "keep" | null
  >(null);
  const [actionLoading, setActionLoading] = useState(false);

  const unsupportedActions = availableActions.filter(
    (action) => !ACTION_LABELS[action],
  );
  useEffect(() => {
    if (unsupportedActions.length > 0) {
      console.warn(
        `LienRowActionsMenu: lien ${lienId} has unsupported action(s): ${unsupportedActions.join(", ")}`,
      );
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [unsupportedActions.join(",")]);

  if (availableActions.length === 0) return null;

  const handleAction = (action: string) => {
    if (action === "prepare-sale" || action === "confirm-sale") {
      router.push(`/selling/portfolio/lien/${lienId}/sell`);
      return;
    }
    if (action === "keep" || action === "withdraw-sale" || action === "archive" || action === "restore") {
      setConfirmAction(action);
      return;
    }
  };

  const items: ActionMenuItem[] = availableActions
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
      } else {
        await liensService.moveToManagement(lienId, {
          reason: "Retained internally",
        });
        toast.success("Lien kept as internal asset.");
      }
      setConfirmAction(null);
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
              : "Keep as Internal Asset?"
        }
        description={
          confirmAction === "withdraw-sale"
            ? "This lien will no longer be visible to the buyer and will need to be re-submitted for sale."
            : confirmAction === "archive"
              ? "This lien will be hidden from active portfolio lists, but its record and history will be retained."
              : confirmAction === "restore"
                ? "This lien will be restored to the Pending list for active portfolio tracking."
              : "This lien will be kept as a private internal asset instead of being offered for sale."
        }
        confirmLabel={
          confirmAction === "withdraw-sale"
            ? "Withdraw"
            : confirmAction === "archive"
              ? "Archive"
              : confirmAction === "restore"
                ? "Restore"
              : "Keep"
        }
        confirmVariant={confirmAction === "keep" || confirmAction === "restore" ? "primary" : "danger"}
      />
    </div>
  );
}
