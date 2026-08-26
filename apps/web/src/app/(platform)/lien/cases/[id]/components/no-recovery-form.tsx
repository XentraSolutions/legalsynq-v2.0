"use client";

import { useState, useEffect } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { settlementService } from "@/lib/settlement";
import { lookupService } from "@/lib/lookup";
import type { LiensStatusResponse } from "@/lib/lookup/lookup.types";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { DatePicker } from "@/components/ui/date-picker";
import { Textarea } from "@/components/ui/textarea";
import { LienTable } from "@/components/lien/lien-table";
import type {
  LienColumnDef,
  LienFooterCell,
} from "@/components/lien/lien-table";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

function pickLienStatusOptions(
  items: LiensStatusResponse[],
): LiensStatusResponse[] {
  const byCode = (codes: string[]) =>
    items.find((i) => codes.includes((i.code || "").toLowerCase()));
  const openOrActive = byCode(["active", "open"]);
  const settledOrClosed = byCode(["settled", "closed"]);
  return [openOrActive, settledOrClosed].filter((i): i is LiensStatusResponse =>
    Boolean(i),
  );
}

interface NoRecoveryFormProps {
  open: boolean;
  onClose: () => void;
  caseId: string;
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens?: () => void;
  isLiensFetching?: boolean;
  onSaved: () => void;
}

const INITIAL_FORM = {
  lienStatus: "",
  closedDate: new Date().toISOString().slice(0, 10),
  note: "",
};

export function NoRecoveryForm({
  open,
  onClose,
  caseId,
  liens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  onSaved,
}: NoRecoveryFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);
  const [lienStatuses, setLienStatuses] = useState<LiensStatusResponse[]>([]);

  const openLiens = liens.filter(
    (l) =>
      l.status !== "Closed" && l.status !== "Withdrawn" && l.status !== "Sold",
  );

  const allChecked =
    openLiens.length > 0 && checkedIds.size === openLiens.length;

  useEffect(() => {
    if (open) {
      setForm({ ...INITIAL_FORM });
      setCheckedIds(new Set(openLiens.map((l) => l.id)));
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    lookupService.getLiensStatus().then((res) => {
      const options = pickLienStatusOptions(res.items);
      setLienStatuses(options);
      const settled = options.find(
        (s) => (s.code || "").toLowerCase() === "settled",
      );
      setForm((prev) => ({
        ...prev,
        lienStatus: (settled ?? options[1])?.code ?? "",
      }));
    });
  }, [open]);

  const toggleCheck = (id: string) => {
    const next = new Set(checkedIds);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setCheckedIds(next);
  };

  const toggleAll = () => {
    setCheckedIds(allChecked ? new Set() : new Set(openLiens.map((l) => l.id)));
  };

  const handleResetClose = () => {
    setForm({ ...INITIAL_FORM });
    setCheckedIds(new Set());
    onClose();
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return "";
    const date = new Date(dateString);
    return new Intl.DateTimeFormat("en-CA").format(date);
  };

  const handleSave = async () => {
    if (checkedIds.size === 0) {
      addToast({
        type: "error",
        title: "No Liens Selected",
        description: "Select at least one lien to mark as no recovery.",
      });
      return;
    }
    if (!form.closedDate) {
      addToast({
        type: "error",
        title: "Date Required",
        description: "Please provide a closed date.",
      });
      return;
    }
    setSaving(true);
    try {
      await settlementService.updateLiensStatus({
        caseId,
        lienIds: Array.from(checkedIds).join(","),
        lienStatus: form.lienStatus,
        closedDate: formatDate(form.closedDate),
        note: form.note,
      });
      addToast({
        type: "success",
        title: "No Recovery Saved",
        description: `Marked ${checkedIds.size} lien${checkedIds.size !== 1 ? "s" : ""} as no recovery.`,
      });
      handleResetClose();
      onSaved();
    } catch (err) {
      addToast({
        type: "error",
        title: "Save Failed",
        description:
          err instanceof ApiError ? err.message : "Failed to save no recovery.",
      });
    } finally {
      setSaving(false);
    }
  };

  const totalBilling = openLiens
    .filter((l) => checkedIds.has(l.id))
    .reduce((s, l) => s + (l.originalAmount ?? 0), 0);

  const noRecoveryColumns: LienColumnDef[] = [
    {
      id: "lienId",
      header: "Lien ID",
      cell: (l) => (
        <span className="text-sm text-primary whitespace-nowrap">
          {l.lienNumber}
        </span>
      ),
    },
    {
      id: "facilityName",
      header: "Medical Facility",
      cell: (l) => (
        <span className="text-sm text-gray-600 whitespace-wrap max-w-40 block">
          {l.facilityName || ""}
        </span>
      ),
    },
    {
      id: "billing",
      header: "Billing Amount",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.originalAmount ?? 0)}
        </span>
      ),
    },
    {
      id: "balance",
      header: "Balance",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.balance ?? 0)}
        </span>
      ),
    },
  ];

  const noRecoveryFooter: LienFooterCell[] = [
    {
      colSpan: 4,
      content: (
        <span className="text-xs font-semibold text-gray-600 uppercase tracking-wide">
          Selected ({checkedIds.size} of {openLiens.length})
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalBilling)}
        </span>
      ),
    },
    { content: null },
  ];

  return (
    <FormModal
      open={open}
      onClose={handleResetClose}
      onSubmit={handleSave}
      title="No Recovery Setup"
      submitLabel={saving ? "Saving..." : "Save"}
      submitDisabled={saving || checkedIds.size === 0}
      size="lg"
    >
      <div className="space-y-5">
        <div>
          <div className="flex items-center gap-2 mb-3">
            <div className="w-7 h-7 rounded-md bg-red-100 flex items-center justify-center shrink-0">
              <i className="ri-close-circle-line text-sm text-red-500" />
            </div>
            <h3 className="text-sm font-semibold text-primary">
              No Recovery Details
            </h3>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Lien Status <span className="text-red-500">*</span>
              </label>
              <Select
                value={form.lienStatus}
                onValueChange={(v) => setForm({ ...form, lienStatus: v })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Select status" />
                </SelectTrigger>
                <SelectContent>
                  {lienStatuses.map((s) => (
                    <SelectItem key={s.id} value={s.code}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Date Closed <span className="text-red-500">*</span>
              </label>
              <DatePicker
                value={form.closedDate}
                onChange={(v) => setForm({ ...form, closedDate: v })}
                disableFutureDates
              />
            </div>
            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Note
              </label>
              <Textarea
                value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
                placeholder="e.g. Case closed with no recovery"
                rows={3}
              />
            </div>
          </div>
        </div>

        <LienTable
          liens={openLiens}
          checkedIds={checkedIds}
          onToggleCheck={toggleCheck}
          onToggleAll={toggleAll}
          columns={noRecoveryColumns}
          footer={noRecoveryFooter}
          loadedAt={liensLoadedAt}
          onRefresh={onRefreshLiens}
          isRefreshing={isLiensFetching}
        />
      </div>
    </FormModal>
  );
}
