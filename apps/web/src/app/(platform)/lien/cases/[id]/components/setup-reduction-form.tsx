"use client";

import { useState, useEffect } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { settlementService } from "@/lib/settlement";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { Input } from "@/components/ui/input";
import { DatePicker } from "@/components/ui/date-picker";
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

function cleanNumericInput(raw: string): string {
  const cleaned = raw.replace(/[^\d.]/g, "");
  const parts = cleaned.split(".");
  return parts.length > 2 ? parts[0] + "." + parts[1] : cleaned;
}

interface SetupReductionFormProps {
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
  reductionDate: new Date().toISOString().slice(0, 10),
  note: "",
};

function isLienReducible(l: CaseLienItem & CaseLienItemMetadata): boolean {
  return (
    l.status !== "Closed" &&
    l.status !== "Withdrawn" &&
    l.status !== "Sold" &&
    l.balance > 0
  );
}

export function SetupReductionForm({
  open,
  onClose,
  caseId,
  liens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  onSaved,
}: SetupReductionFormProps) {
  const addToast = useLienStore((s) => s.addToast);

  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [reductionInput, setReductionInput] = useState("");
  const [isPercent, setIsPercent] = useState(false);
  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set());
  const [lienReductions, setLienReductions] = useState<Record<string, number>>(
    {},
  );
  const [lienInputs, setLienInputs] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setForm({
        reductionDate: new Date().toISOString().slice(0, 10),
        note: "",
      });
      setIsPercent(false);

      const activeLiens = liens.filter(isLienReducible);

      const preChecked = new Set<string>();
      const preReductions: Record<string, number> = {};
      const preInputs: Record<string, string> = {};
      let totalExisting = 0;

      for (const l of activeLiens) {
        const amt = l.reductionAmount ?? 0;
        if (amt > 0) {
          preChecked.add(l.id);
          preReductions[l.id] = amt;
          preInputs[l.id] = amt.toFixed(2);
          totalExisting += amt;
        }
      }

      setCheckedIds(preChecked);
      setLienReductions(preReductions);
      setLienInputs(preInputs);
      setReductionInput(totalExisting > 0 ? totalExisting.toFixed(2) : "");
    }
  }, [open, liens]);

  const selectableLiens = liens.filter(isLienReducible);

  const allChecked =
    selectableLiens.length > 0 && checkedIds.size === selectableLiens.length;

  const toggleCheck = (id: string) => {
    const next = new Set(checkedIds);
    if (next.has(id)) {
      next.delete(id);
      const newReductions = { ...lienReductions };
      delete newReductions[id];
      setLienReductions(newReductions);
      setLienInputs((prev) => {
        const n = { ...prev };
        delete n[id];
        return n;
      });
      if (Object.values(newReductions).some((v) => v > 0)) {
        updateParentFromRows(newReductions, next);
      }
    } else {
      next.add(id);
    }
    setCheckedIds(next);
  };

  const toggleAll = () => {
    const next = allChecked
      ? new Set<string>()
      : new Set(selectableLiens.map((l) => l.id));
    if (allChecked) {
      setLienReductions({});
      setLienInputs({});
    } else if (Object.values(lienReductions).some((v) => v > 0)) {
      updateParentFromRows(lienReductions, next);
    }
    setCheckedIds(next);
  };

  const checkedLiens = selectableLiens.filter((l) => checkedIds.has(l.id));
  const checkedBilling = checkedLiens.reduce(
    (s, l) => s + (l.originalAmount ?? 0),
    0,
  );

  const numericParent = parseFloat(reductionInput) || 0;
  const parentExceedsChecked =
    !isPercent &&
    numericParent > checkedBilling &&
    checkedBilling > 0 &&
    numericParent > 0;
  const parentExceeds100 = isPercent && numericParent > 100;

  const handleParentInputChange = (raw: string) => {
    const sanitized = cleanNumericInput(raw);
    if (isPercent) {
      const n = parseFloat(sanitized);
      if (!isNaN(n) && n > 100) {
        setReductionInput("100");
        return;
      }
    }
    setReductionInput(sanitized);
  };

  const syncInputsFromReductions = (reductions: Record<string, number>) => {
    const inputs: Record<string, string> = {};
    for (const [id, val] of Object.entries(reductions)) {
      inputs[id] = val > 0 ? val.toFixed(2) : "";
    }
    setLienInputs(inputs);
  };

  const updateParentFromRows = (
    reductions: Record<string, number>,
    checked: Set<string>,
  ) => {
    const total = selectableLiens
      .filter((l) => checked.has(l.id))
      .reduce((s, l) => s + (reductions[l.id] ?? 0), 0);

    if (isPercent) {
      const totalBill = selectableLiens
        .filter((l) => checked.has(l.id))
        .reduce((s, l) => s + (l.originalAmount ?? 0), 0);
      if (totalBill > 0)
        setReductionInput(((total / totalBill) * 100).toFixed(2));
    } else {
      setReductionInput(total > 0 ? total.toFixed(2) : "");
    }
  };

  const handleRowInputChange = (id: string, raw: string) => {
    const lien = selectableLiens.find((l) => l.id === id);
    if (!lien) return;
    const sanitized = cleanNumericInput(raw);
    setLienInputs((prev) => ({ ...prev, [id]: sanitized }));
    const numeric = parseFloat(sanitized) || 0;
    const maxBilling = lien.originalAmount ?? 0;
    const clamped = Math.min(numeric, maxBilling);
    const newReductions = { ...lienReductions, [id]: clamped };
    setLienReductions(newReductions);
    updateParentFromRows(newReductions, checkedIds);
  };

  const handleApplySame = () => {
    const val = parseFloat(reductionInput);
    if (isNaN(val) || val <= 0 || checkedIds.size === 0) return;
    if (isPercent && val > 100) return;
    const updates = { ...lienReductions };
    for (const l of selectableLiens) {
      if (checkedIds.has(l.id)) {
        const computed = isPercent
          ? ((l.originalAmount ?? 0) * val) / 100
          : val;
        updates[l.id] = Math.min(computed, l.originalAmount ?? 0);
      }
    }
    setLienReductions(updates);
    syncInputsFromReductions(updates);
  };

  const handleDistribute = () => {
    const val = parseFloat(reductionInput);
    if (isNaN(val) || val <= 0 || checkedIds.size === 0) return;
    if (isPercent && val > 100) return;
    const selectedLiens = selectableLiens.filter((l) => checkedIds.has(l.id));
    const totalCheckedBilling = selectedLiens.reduce(
      (s, l) => s + (l.originalAmount ?? 0),
      0,
    );
    if (totalCheckedBilling === 0) return;
    const totalDollar = isPercent ? (val / 100) * totalCheckedBilling : val;
    const clampedTotal = Math.min(totalDollar, totalCheckedBilling);
    const updates = { ...lienReductions };
    for (const l of selectedLiens) {
      updates[l.id] =
        ((l.originalAmount ?? 0) / totalCheckedBilling) * clampedTotal;
    }
    setLienReductions(updates);
    syncInputsFromReductions(updates);
  };

  const handleResetClose = () => {
    setForm({ ...INITIAL_FORM });
    setReductionInput("");
    setIsPercent(false);
    setCheckedIds(new Set());
    setLienReductions({});
    setLienInputs({});
    onClose();
  };

  const handleSave = async () => {
    const liensToSave = selectableLiens.filter(
      (l) => checkedIds.has(l.id) && (lienReductions[l.id] ?? 0) > 0,
    );
    if (liensToSave.length === 0) {
      addToast({
        type: "error",
        title: "No Reductions",
        description: "Select liens and apply a reduction amount before saving.",
      });
      return;
    }
    setSaving(true);
    try {
      await settlementService.legacySaveReduction({
        caseId,
        data: liensToSave.map((l) => ({
          liensId: l.id,
          reductionAmount: Math.round((lienReductions[l.id] ?? 0) * 100) / 100,
        })),
      });
      addToast({
        type: "success",
        title: "Reduction Saved",
        description: `Reduction applied to ${liensToSave.length} lien${liensToSave.length !== 1 ? "s" : ""}.`,
      });
      handleResetClose();
      onSaved();
    } catch (err) {
      addToast({
        type: "error",
        title: "Save Failed",
        description:
          err instanceof ApiError ? err.message : "Failed to save reduction.",
      });
    } finally {
      setSaving(false);
    }
  };

  const totalBilling = liens.reduce((s, l) => s + (l.originalAmount ?? 0), 0);
  const totalPurchase = liens.reduce(
    (s, l) => s + (l.purchaseAmount ?? 0),
    0,
  );
  const totalReduction = liens.reduce(
    (s, l) => s + (lienReductions[l.id] ?? 0),
    0,
  );
  const totalSettle = totalBilling - totalReduction;

  const reductionColumns: LienColumnDef[] = [
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
      id: "reduction",
      header: "Reduction Amount",
      align: "right",
      cell: (l, isChecked) => {
        const inputVal = lienInputs[l.id] ?? "";
        const inputNumeric = parseFloat(inputVal) || 0;
        const rowExceedsBilling =
          inputNumeric > (l.originalAmount ?? 0) && inputNumeric > 0;
        if (!isLienReducible(l))
          return (
            <span className="text-sm text-gray-700 tabular-nums">
              {formatCurrency(l.reductionAmount ?? 0)}
            </span>
          );
        if (!isChecked)
          return <span className="text-sm text-gray-300">---</span>;
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
                onChange={(e) => handleRowInputChange(l.id, e.target.value)}
                onBlur={() => {
                  const n = parseFloat(inputVal);
                  if (!isNaN(n))
                    setLienInputs((prev) => ({
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
    {
      id: "toSettle",
      header: "Amount to Settle",
      align: "right",
      cell: (l, isChecked) => {
        if (!isLienReducible(l)) {
          return (
            <span className="text-sm text-gray-700 tabular-nums">
              {formatCurrency(
                (l.originalAmount ?? 0) - (l.reductionAmount ?? 0),
              )}
            </span>
          );
        }
        if (!isChecked)
          return <span className="text-sm text-gray-300">---</span>;
        const reduction = lienReductions[l.id] ?? 0;
        return (
          <span className="text-sm text-gray-700 tabular-nums">
            {formatCurrency((l.originalAmount ?? 0) - reduction)}
          </span>
        );
      },
    },
  ];

  const reductionFooter: LienFooterCell[] = [
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
          {formatCurrency(totalBilling)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalPurchase)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-green-600 tabular-nums">
          {formatCurrency(totalReduction)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalSettle)}
        </span>
      ),
    },
  ];

  const inverseValue = (() => {
    if (numericParent <= 0 || checkedBilling === 0) return null;
    return isPercent
      ? (numericParent / 100) * checkedBilling
      : (numericParent / checkedBilling) * 100;
  })();

  const handleToggleMode = (toPercent: boolean) => {
    if (toPercent === isPercent) return;
    if (inverseValue !== null) {
      const swapped = toPercent ? Math.min(inverseValue, 100) : inverseValue;
      setReductionInput(swapped.toFixed(2));
    } else {
      setReductionInput("");
    }
    setIsPercent(toPercent);
  };

  return (
    <FormModal
      open={open}
      onClose={handleResetClose}
      onSubmit={handleSave}
      title="Reduction Details"
      submitLabel={saving ? "Saving..." : "Save"}
      submitDisabled={saving || checkedIds.size === 0}
      size="lg"
    >
      <div className="space-y-5">
        <div>
          <div className="flex items-center gap-2 mb-3">
            <div className="w-7 h-7 rounded-md bg-orange-100 flex items-center justify-center shrink-0">
              <i className="ri-file-reduce-line text-sm text-orange-500" />
            </div>
            <h3 className="text-sm font-semibold text-primary">
              Reduction Information
            </h3>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="col-span-2">
              <Field
                label="Note"
                value={form.note}
                onChange={(v) => setForm({ ...form, note: v })}
                placeholder="e.g. Negotiated reduction per agreement"
              />
            </div>
            <div>
              <Field
                label="Reduction Date"
                required
                type="date"
                value={form.reductionDate}
                onChange={(v) => setForm({ ...form, reductionDate: v })}
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Reduction Amount
              </label>
              <div
                className={`flex items-stretch h-9 rounded-lg border overflow-hidden transition-colors ${
                  parentExceedsChecked || parentExceeds100
                    ? "border-red-300"
                    : "border-gray-200"
                }`}
              >
                <div
                  className={`relative ${isPercent ? "w-24 shrink-0" : "flex-1"}`}
                >
                  <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                    {isPercent ? "%" : "$"}
                  </span>
                  <Input
                    type="text"
                    inputMode="decimal"
                    value={reductionInput}
                    onChange={(e) => handleParentInputChange(e.target.value)}
                    onBlur={() => {
                      const n = parseFloat(reductionInput);
                      if (!isNaN(n) && n > 0) setReductionInput(n.toFixed(2));
                    }}
                    placeholder="0.00"
                    className="h-full pl-6 pr-3 border-0 rounded-none focus:ring-0"
                  />
                </div>

                <div
                  className={`relative bg-gray-50 ${isPercent ? "flex-1" : "w-24 shrink-0"}`}
                >
                  {isPercent && (
                    <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                      $
                    </span>
                  )}
                  <Input
                    type="text"
                    disabled
                    readOnly
                    value={
                      inverseValue !== null
                        ? isPercent
                          ? inverseValue.toLocaleString("en-US", {
                              minimumFractionDigits: 2,
                              maximumFractionDigits: 2,
                            })
                          : inverseValue.toFixed(2)
                        : ""
                    }
                    placeholder="0.00"
                    className={`h-full border-0 rounded-none focus:ring-0 bg-transparent text-gray-400 ${isPercent ? "pl-6 pr-3" : "pl-3 pr-6"}`}
                  />
                  {!isPercent && (
                    <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                      %
                    </span>
                  )}
                </div>

                <div className="flex items-stretch border-l border-gray-200 shrink-0">
                  <button
                    type="button"
                    onClick={() => handleToggleMode(false)}
                    className={`w-8 text-xs font-semibold transition-colors ${!isPercent ? "bg-primary text-white" : "bg-gray-50 text-gray-400 hover:bg-gray-100"}`}
                  >
                    $
                  </button>
                  <button
                    type="button"
                    onClick={() => handleToggleMode(true)}
                    className={`w-8 text-xs font-semibold transition-colors border-l border-gray-200 ${isPercent ? "bg-primary text-white" : "bg-gray-50 text-gray-400 hover:bg-gray-100"}`}
                  >
                    %
                  </button>
                </div>
              </div>
              {parentExceedsChecked && (
                <p className="mt-1 text-xs text-red-500">
                  Amount exceeds selected liens' total billing (
                  {formatCurrency(checkedBilling)})
                </p>
              )}
              {parentExceeds100 && (
                <p className="mt-1 text-xs text-red-500">
                  Percentage cannot exceed 100%
                </p>
              )}
            </div>
          </div>
        </div>

        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={handleApplySame}
            disabled={
              !reductionInput ||
              checkedIds.size === 0 ||
              parentExceedsChecked ||
              parentExceeds100
            }
            className="px-3.5 py-2 text-sm font-medium text-primary bg-white border border-primary/30 rounded-lg hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Apply Same Reduction
          </button>
          <button
            type="button"
            onClick={handleDistribute}
            disabled={
              !reductionInput ||
              checkedIds.size === 0 ||
              parentExceedsChecked ||
              parentExceeds100
            }
            className="px-3.5 py-2 text-sm font-medium text-primary bg-white border border-primary/30 rounded-lg hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Distribute Reduction
          </button>
        </div>

        <LienTable
          liens={liens}
          checkedIds={checkedIds}
          onToggleCheck={toggleCheck}
          onToggleAll={toggleAll}
          isRowSelectable={isLienReducible}
          columns={reductionColumns}
          footer={reductionFooter}
          loadedAt={liensLoadedAt}
          onRefresh={onRefreshLiens}
          isRefreshing={isLiensFetching}
        />
      </div>
    </FormModal>
  );
}

function Field({
  label,
  value,
  onChange,
  error,
  placeholder,
  type = "text",
  required,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  error?: string;
  placeholder?: string;
  type?: string;
  required?: boolean;
}) {
  return (
    <div>
      <label className="block text-sm font-medium text-gray-700 mb-1">
        {label}
        {required && <span className="text-red-500 ml-0.5">*</span>}
      </label>
      {type === "date" ? (
        <DatePicker
          value={value}
          onChange={onChange}
          className={error ? "border-red-300" : undefined}
          disableFutureDates
        />
      ) : (
        <Input
          type={type}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className={error ? "border-red-300" : undefined}
        />
      )}
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}
