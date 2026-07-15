"use client";

import { useState, useEffect } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { settlementService } from "@/lib/settlement";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { DatePicker } from "@/components/ui/date-picker";
import { LienTable } from "@/components/lien/lien-table";
import type { LienColumnDef, LienFooterCell } from "@/components/lien/lien-table";

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "---";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

function cleanNumericInput(raw: string): string {
  const cleaned = raw.replace(/[^\d.]/g, "");
  const parts = cleaned.split(".");
  return parts.length > 2 ? parts[0] + "." + parts[1] : cleaned;
}

interface LienSettlementFormProps {
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
  settlementDate: new Date().toISOString().slice(0, 10),
  notes: "",
};

export function LienSettlementForm({
  open,
  onClose,
  caseId,
  liens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  onSaved,
}: LienSettlementFormProps) {
  const addToast = useLienStore((s) => s.addToast);

  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set());
  const [lienAmounts, setLienAmounts] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setForm({ settlementDate: new Date().toISOString().slice(0, 10), notes: "" });
      setCheckedIds(new Set());
      setLienAmounts({});
    }
  }, [open]);

  const openLiens = liens.filter(
    (l) => l.status !== "Closed" && l.status !== "Withdrawn" && l.status !== "Sold",
  );

  const allChecked = openLiens.length > 0 && checkedIds.size === openLiens.length;

  const toggleCheck = (id: string) => {
    const next = new Set(checkedIds);
    if (next.has(id)) {
      next.delete(id);
      setLienAmounts((prev) => {
        const updated = { ...prev };
        delete updated[id];
        return updated;
      });
    } else {
      next.add(id);
    }
    setCheckedIds(next);
  };

  const toggleAll = () => {
    if (allChecked) {
      setCheckedIds(new Set());
      setLienAmounts({});
    } else {
      setCheckedIds(new Set(openLiens.map((l) => l.id)));
    }
  };

  const handleResetClose = () => {
    setForm({ ...INITIAL_FORM });
    setCheckedIds(new Set());
    setLienAmounts({});
    onClose();
  };

  const handleSave = async () => {
    const liensToSave = openLiens.filter(
      (l) => checkedIds.has(l.id) && parseFloat(lienAmounts[l.id] || "0") > 0,
    );
    if (liensToSave.length === 0) {
      addToast({
        type: "error",
        title: "No Settlements",
        description: "Select liens and enter a settlement amount before saving.",
      });
      return;
    }
    setSaving(true);
    try {
      await Promise.all(
        liensToSave.map((l) =>
          settlementService.createLienSettlement({
            lienId: l.id,
            caseId,
            settlementAmount: parseFloat(lienAmounts[l.id]),
            settlementDate: form.settlementDate,
            notes: form.notes,
          }),
        ),
      );
      addToast({
        type: "success",
        title: "Settlement Saved",
        description: `Settlement recorded for ${liensToSave.length} lien${liensToSave.length !== 1 ? "s" : ""}.`,
      });
      handleResetClose();
      onSaved();
    } catch (err) {
      addToast({
        type: "error",
        title: "Save Failed",
        description:
          err instanceof ApiError ? err.message : "Failed to save settlement.",
      });
    } finally {
      setSaving(false);
    }
  };

  const totalCheckedBilling = openLiens
    .filter((l) => checkedIds.has(l.id))
    .reduce((s, l) => s + (l.originalAmount ?? 0), 0);

  const totalCheckedPurchase = openLiens
    .filter((l) => checkedIds.has(l.id))
    .reduce((s, l) => s + (l.purchaseAmount ?? 0), 0);

  const totalSettlement = Array.from(checkedIds).reduce(
    (s, id) => s + (parseFloat(lienAmounts[id] || "0") || 0),
    0,
  );

  const isFormInvalid = !form.settlementDate || checkedIds.size === 0;

  const settlementColumns: LienColumnDef[] = [
    {
      id: "lienId",
      header: "Lien ID",
      cell: (l) => (
        <span className="text-xs font-mono text-primary">{l.lienNumber}</span>
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
      id: "purchase",
      header: "Purchase Amount",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.purchaseAmount ?? 0)}
        </span>
      ),
    },
    {
      id: "settlement",
      header: "Settlement Amount",
      align: "right",
      cell: (l, isChecked) => {
        if (!isChecked)
          return <span className="text-sm text-gray-300">---</span>;
        const inputVal = lienAmounts[l.id] ?? "";
        const inputNumeric = parseFloat(inputVal) || 0;
        const rowExceedsBilling =
          inputNumeric > (l.originalAmount ?? 0) && inputNumeric > 0;
        return (
          <div className="flex flex-col items-end gap-0.5">
            <div className="relative">
              <span className="absolute left-2 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                $
              </span>
              <Input
                type="text"
                inputMode="decimal"
                value={inputVal}
                onChange={(e) => {
                  const sanitized = cleanNumericInput(e.target.value);
                  setLienAmounts((prev) => ({ ...prev, [l.id]: sanitized }));
                }}
                onBlur={() => {
                  const n = parseFloat(inputVal);
                  if (!isNaN(n))
                    setLienAmounts((prev) => ({
                      ...prev,
                      [l.id]: n.toFixed(2),
                    }));
                }}
                placeholder="0.00"
                className={`w-28 pl-5 pr-2 py-1 text-right ${
                  rowExceedsBilling
                    ? "border-red-300 focus:border-red-400 focus:ring-red-100"
                    : ""
                }`}
              />
            </div>
            {rowExceedsBilling && (
              <span className="text-[10px] text-red-500 whitespace-nowrap">
                Exceeds billing
              </span>
            )}
          </div>
        );
      },
    },
  ];

  const settlementFooter: LienFooterCell[] = [
    {
      colSpan: 3,
      content: (
        <span className="text-xs font-semibold text-gray-600 uppercase tracking-wide">
          Total
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalCheckedBilling)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalCheckedPurchase)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-blue-600 tabular-nums">
          {formatCurrency(totalSettlement)}
        </span>
      ),
    },
  ];

  return (
    <FormModal
      open={open}
      onClose={handleResetClose}
      onSubmit={handleSave}
      title="Lien Settlement"
      submitLabel={saving ? "Saving..." : "Save Settlement"}
      submitDisabled={saving || isFormInvalid}
      size="lg"
    >
      <div className="space-y-5">
        <div>
          <div className="flex items-center gap-2 mb-3">
            <div className="w-7 h-7 rounded-md bg-blue-100 flex items-center justify-center shrink-0">
              <i className="ri-handshake-line text-sm text-blue-500" />
            </div>
            <h3 className="text-sm font-semibold text-primary">
              Settlement Information
            </h3>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Settlement Date <span className="text-red-500">*</span>
              </label>
              <DatePicker
                value={form.settlementDate}
                onChange={(v) => setForm({ ...form, settlementDate: v })}
                disableFutureDates
              />
            </div>

            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Notes
              </label>
              <Textarea
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                placeholder="e.g. Settlement agreed per negotiation"
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
          columns={settlementColumns}
          footer={settlementFooter}
          loadedAt={liensLoadedAt}
          onRefresh={onRefreshLiens}
          isRefreshing={isLiensFetching}
        />
      </div>
    </FormModal>
  );
}
