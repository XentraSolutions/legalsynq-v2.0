"use client";

import { useState, useEffect } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { settlementService } from "@/lib/settlement";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";
import { lookupService } from "@/lib/lookup";
import type {
  LiensStatusResponse,
  LookupData,
} from "@/lib/lookup/lookup.types";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { NumberInput } from "@/components/ui/number-input";
import { Textarea } from "@/components/ui/textarea";
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

interface AddPaymentFormProps {
  open: boolean;
  onClose: () => void;
  caseId: string;
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens?: () => void;
  isLiensFetching?: boolean;
  onSaved: () => void;
  selectedPayment?: any;
  isEditing?: boolean;
}

const INITIAL_FORM = {
  lienStatus: "",
  checkAmount: "",
  checkDate: "",
  checkNumber: "",
  type: "",
  status: "",
  note: "",
};

export function AddPaymentForm({
  open,
  onClose,
  caseId,
  liens,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  onSaved,
  selectedPayment,
  isEditing = false,
}: AddPaymentFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM, ...selectedPayment });

  const [checkedIds, setCheckedIds] = useState<Set<string>>(new Set());
  const [lienPayments, setLienPayments] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const [settlementTypes, setSettlementTypes] = useState<LookupData[]>([]);
  const [settlementStatuses, setSettlementStatuses] = useState<LookupData[]>(
    [],
  );
  const [lienStatuses, setLienStatuses] = useState<LiensStatusResponse[]>([]);
  const [lookupsLoading, setLookupsLoading] = useState(true);
  const [typeError, setTypeError] = useState(false);
  const [statusError, setStatusError] = useState(false);
  const PAYMENT_METHOD_CHECK = "Check";

  // TEMP: hardcoded until API endpoint is ready
  const TEMP_SETTLEMENT_STATUSES: LookupData[] = [
    {
      id: "full_payment",
      name: "Full Payment",
      code: "full_payment",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 1,
    },
    {
      id: "reduced_payment",
      name: "Reduced Payment",
      code: "reduced_payment",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 2,
    },
    {
      id: "partial_loss",
      name: "Partial Loss",
      code: "partial_loss",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 3,
    },
    {
      id: "no_recovery",
      name: "No Recovery",
      code: "no_recovery",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 4,
    },
  ];

  // TEMP: hardcoded until API endpoint is ready
  const TEMP_SETTLEMENT_TYPES: LookupData[] = [
    {
      id: "by_attorney",
      name: "By Attorney",
      code: "by_attorney",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 1,
    },
    {
      id: "by_medical_provider",
      name: "By Medical Provider",
      code: "by_medical_provider",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 2,
    },
    {
      id: "by_funding_company",
      name: "By Funding Company",
      code: "by_funding_company",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 3,
    },
    {
      id: "other",
      name: "Other",
      code: "other",
      category: "",
      description: null,
      isActive: true,
      isSystem: false,
      sortOrder: 4,
    },
  ];

  function isLienPayable(l: CaseLienItem & CaseLienItemMetadata): boolean {
    return (
      l.status !== "Closed" &&
      l.status !== "Withdrawn" &&
      l.status !== "Sold" &&
      l.balance > 0
    );
  }

  const activeLiens = liens.filter(isLienPayable);

  useEffect(() => {
    if (!open) return;
    setLookupsLoading(true);
    setTypeError(false);
    setStatusError(false);
    Promise.allSettled([
      lookupService.getSettlementType(),
      lookupService.getSettlementStatus(),
      lookupService.getLiensStatus(),
    ]).then(([typeRes, statusRes, lienStatusRes]) => {
      if (typeRes.status === "fulfilled" && typeRes.value.items.length > 0) {
        setSettlementTypes(typeRes.value.items);
      } else {
        // setTypeError(true);
        // TEMP: fall back to hardcoded options until API endpoint is ready
        setSettlementTypes(TEMP_SETTLEMENT_TYPES);
      }
      if (
        statusRes.status === "fulfilled" &&
        statusRes.value.items.length > 0
      ) {
        setSettlementStatuses(statusRes.value.items);
      } else {
        // setStatusError(true);
        // TEMP: fall back to hardcoded options until API endpoint is ready
        setSettlementStatuses(TEMP_SETTLEMENT_STATUSES);
      }
      const lienStatusOptions =
        lienStatusRes.status === "fulfilled"
          ? pickLienStatusOptions(lienStatusRes.value.items)
          : [];
      setLienStatuses(lienStatusOptions);
      const active = lienStatusOptions.find(
        (s) => (s.code || "").toLowerCase() === "active",
      );
      setForm((prev: any) => ({
        ...prev,
        ...selectedPayment,
        lienStatus: isEditing
          ? selectedPayment.lienStatus
          : (active ?? lienStatusOptions[0]?.code ?? ""),
      }));
      setLookupsLoading(false);
    });
  }, [open]);

  const openLiens = liens.filter(
    (l) =>
      l.status !== "Closed" &&
      l.status !== "Withdrawn" &&
      l.status !== "Sold" &&
      l.balance > 0,
  );

  const allChecked =
    openLiens.length > 0 && checkedIds.size === openLiens.length;

  const toggleCheck = (id: string) => {
    const next = new Set(checkedIds);
    if (next.has(id)) {
      next.delete(id);
      setLienPayments((prev) => {
        const updated = { ...prev };
        delete updated[id];
        return updated;
      });
    } else {
      next.add(id);
      const lien = openLiens.find((l) => l.id === id);
      if (lien?.paymentAmount != null) {
        setLienPayments((prev) => ({
          ...prev,
          [id]: lien.paymentAmount!.toFixed(2),
        }));
      }
    }
    setCheckedIds(next);
  };

  const toggleAll = () => {
    if (allChecked) {
      setCheckedIds(new Set());
      setLienPayments({});
    } else {
      setCheckedIds(new Set(openLiens.map((l) => l.id)));
      const initialPayments: Record<string, string> = {};
      for (const l of openLiens) {
        if (l.paymentAmount != null) {
          initialPayments[l.id] = l.paymentAmount.toFixed(2);
        }
      }
      setLienPayments(initialPayments);
    }
  };
  const computeTotal = () => {
    let balances = 0;
    const initialPayments: Record<string, string> = {};

    // Create a Set for fast lookups
    const checkedSet = new Set(checkedIds);

    for (const l of activeLiens) {
      // Check if current lien ID is in the checked list
      if (checkedSet.has(l.id)) {
        if (l.balance != null) {
          balances += l.balance;
        }

        // If you also need to populate initialPayments for checked items:
        // initialPayments[l.id] = ...;
      }
    }
    return balances;
  };

  const handleAllocateProportionally = () => {
    const val = parseFloat(form.checkAmount);
    if (isNaN(val) || val <= 0 || checkedIds.size === 0) return;
    const totalBalance = selectedLiens.reduce(
      (s, l) => s + (l.balance ?? 0),
      0,
    );
    if (totalBalance === 0) return;
    const ratio = Math.min(val, totalBalance) / totalBalance;
    const updates: Record<string, string> = { ...lienPayments };
    for (const l of selectedLiens) {
      updates[l.id] = ((l.balance ?? 0) * ratio).toFixed(2);
    }
    setLienPayments(updates);
  };

  const handleDistributePayment = () => {
    const val = parseFloat(form.checkAmount);
    if (isNaN(val) || val <= 0 || checkedIds.size === 0) return;
    // const selectedLiens = openLiens.filter((l) => checkedIds.has(l.id));
    const perLien = val / selectedLiens.length;
    const updates: Record<string, string> = { ...lienPayments };
    for (const l of selectedLiens) {
      updates[l.id] = Math.min(perLien, l.balance ?? 0).toFixed(2);
    }
    setLienPayments(updates);
  };

  const handleResetClose = () => {
    setForm({ ...INITIAL_FORM });
    setCheckedIds(new Set());
    setLienPayments({});
    onClose();
  };

  const formatDate = (dateString: string) => {
    if (!dateString) return "";
    const date = new Date(dateString);
    return new Intl.DateTimeFormat("en-CA").format(date);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      const lienIds = Array.from(checkedIds);
      const paymentDate = form.checkDate ? formatDate(form.checkDate) : "";

      if (isEditing) {
        settlementService.updateSettlement({
          lienId: form.lienId,
          lienStatus: form.lienStatus,
          caseId,
          amount: parseFloat(form.checkAmount || "0"),
          paymentDate,
          paymentMethod: PAYMENT_METHOD_CHECK,
          referenceNumber: form.checkNumber,
          notes: form.note,
          settlementType: form.type,
          settlementStatus: form.status,
        });
      } else {
        await Promise.all(
          lienIds.flatMap((id) => [
            settlementService.createSettlementPayment({
              lienId: id,
              lienStatus: form.lienStatus,
              caseId,
              amount: parseFloat(lienPayments[id] || "0"),
              paymentDate,
              paymentMethod: PAYMENT_METHOD_CHECK,
              referenceNumber: form.checkNumber,
              notes: form.note,
              settlementType: form.type,
              settlementStatus: form.status,
            }),
            settlementService.createLienSettlement({
              lienId: id,
              caseId,
              settlementAmount: parseFloat(lienPayments[id] || "0"),
              settlementDate: paymentDate,
              notes: form.note,
            }),
          ]),
        );
      }

      addToast({
        type: "success",
        title: "Payment Saved",
        description: "Payment and allocations saved successfully.",
      });
      handleResetClose();
      onSaved();
    } catch (err) {
      addToast({
        type: "error",
        title: "Save Failed",
        description:
          err instanceof ApiError ? err.message : "Failed to save payment.",
      });
    } finally {
      setSaving(false);
    }
  };

  const isFormInvalid =
    !form.lienStatus ||
    !form.checkAmount ||
    !form.checkDate ||
    !form.checkNumber ||
    !form.type ||
    isEditing
      ? false
      : checkedIds.size === 0;

  const selectedLiens = openLiens.filter((l) => checkedIds.has(l.id));

  const totalAmountToSettle = openLiens.reduce(
    (s, l) => s + (l.balance ?? 0),
    0,
  );
  const totalBilling = openLiens.reduce(
    (s, l) => s + (l.originalAmount ?? 0),
    0,
  );
  const totalPurchase = openLiens.reduce(
    (s, l) => s + (l.purchaseAmount ?? 0),
    0,
  );
  const totalReceivedPayment = openLiens.reduce((s, l) => {
    const val = checkedIds.has(l.id)
      ? parseFloat(lienPayments[l.id] || "0") || 0
      : (l.paymentAmount ?? 0);
    return s + val;
  }, 0);

  const checkAmountNum = parseFloat(form.checkAmount) || 0;
  const receivedExceedsCheck =
    totalReceivedPayment > checkAmountNum &&
    checkAmountNum > 0 &&
    totalReceivedPayment > 0 &&
    !isEditing;

  const paymentColumns: LienColumnDef[] = [
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
      id: "toSettle",
      header: "Amount to Settle",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.balance ?? 0)}
        </span>
      ),
    },
    {
      id: "received",
      header: "Amount Received",
      align: "right",
      cell: (l, isChecked) => {
        if (!isChecked)
          return (
            <span className="text-sm text-gray-700 tabular-nums">
              {formatCurrency(l.paymentAmount ?? 0)}
            </span>
          );
        const inputVal = lienPayments[l.id] ?? "";
        const inputNumeric = parseFloat(inputVal) || 0;
        const rowExceedsBilling =
          inputNumeric > (l.balance ?? 0) && inputNumeric > 0;
        return (
          <div className="flex flex-col items-end gap-0.5">
            <NumberInput
              value={inputVal}
              onValueChange={(v) =>
                setLienPayments((prev) => ({ ...prev, [l.id]: v }))
              }
              onBlur={() => {
                const n = parseFloat(inputVal);
                if (!isNaN(n))
                  setLienPayments((prev) => ({
                    ...prev,
                    [l.id]: n.toFixed(2),
                  }));
              }}
              placeholder="0.00"
              prefix="$"
              className={`w-28 text-right ${
                rowExceedsBilling
                  ? "border-red-300 focus:border-red-400 focus:ring-red-100"
                  : ""
              }`}
            />
            {rowExceedsBilling && (
              <span className="text-[10px] text-red-500 whitespace-nowrap">
                Exceeds balance
              </span>
            )}
          </div>
        );
      },
    },
  ];

  const paymentFooter: LienFooterCell[] = [
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
        <span className="text-sm font-semibold text-gray-900 tabular-nums">
          {formatCurrency(totalAmountToSettle)}
        </span>
      ),
    },
    {
      align: "right",
      content: (
        <div>
          <span
            className={`text-sm font-semibold ${receivedExceedsCheck ? "text-red-600" : "text-green-600"}`}
          >
            {formatCurrency(totalReceivedPayment)}
          </span>
          {receivedExceedsCheck && (
            <p className="text-[10px] text-red-500 whitespace-nowrap">
              Exceeds check ({formatCurrency(checkAmountNum)})
            </p>
          )}
        </div>
      ),
    },
  ];

  return (
    <FormModal
      open={open}
      onClose={handleResetClose}
      onSubmit={handleSave}
      title="Add Payment"
      submitLabel={saving ? "Saving..." : "Save Payment"}
      submitDisabled={saving || isFormInvalid || receivedExceedsCheck}
      size="lg"
    >
      <div className="space-y-5">
        <div>
          <div className="flex items-center gap-2 mb-3">
            <div className="w-7 h-7 rounded-md bg-green-100 flex items-center justify-center shrink-0">
              <i className="ri-money-dollar-circle-line text-sm text-green-600" />
            </div>
            <h3 className="text-sm font-semibold text-primary">
              Payment Details
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
                Amount to Settle
              </label>
              <div className="h-9 flex items-center px-3 rounded-lg border border-gray-200 bg-gray-50 text-sm text-gray-700 tabular-nums">
                {checkedIds.size > 0 ? (
                  computeTotal()
                ) : (
                  <span className="text-gray-400">Select liens below</span>
                )}
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Check Amount <span className="text-red-500">*</span>
              </label>
              <NumberInput
                value={form.checkAmount}
                onValueChange={(v) => setForm({ ...form, checkAmount: v })}
                onBlur={() => {
                  const n = parseFloat(form.checkAmount);
                  if (!isNaN(n) && n > 0)
                    setForm({ ...form, checkAmount: n.toFixed(2) });
                }}
                placeholder="0.00"
                prefix="$"
              />
            </div>

            <Field
              label="Check Received"
              required
              type="date"
              value={form.checkDate}
              onChange={(v) => setForm({ ...form, checkDate: v })}
            />

            <Field
              label="Check Number"
              required
              placeholder="Enter check number"
              value={form.checkNumber}
              onChange={(v) => setForm({ ...form, checkNumber: v })}
            />

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Settlement Type <span className="text-red-500">*</span>
              </label>
              <Select
                value={form.type}
                onValueChange={(v) => setForm({ ...form, type: v })}
                disabled={lookupsLoading}
              >
                <SelectTrigger
                  className={typeError ? "border-red-300" : undefined}
                >
                  <SelectValue
                    placeholder={
                      lookupsLoading
                        ? "Loading..."
                        : typeError
                          ? "Failed to load"
                          : settlementTypes.length === 0
                            ? "No options available"
                            : "Please select"
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {settlementTypes.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name || s.description}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {typeError && (
                <p className="text-xs text-red-500 mt-1">
                  Could not load settlement types.
                </p>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Settlement Status
              </label>
              <Select
                value={form.status}
                onValueChange={(v) => setForm({ ...form, status: v })}
                disabled={lookupsLoading}
              >
                <SelectTrigger
                  className={statusError ? "border-red-300" : undefined}
                >
                  <SelectValue
                    placeholder={
                      lookupsLoading
                        ? "Loading..."
                        : statusError
                          ? "Failed to load"
                          : settlementStatuses.length === 0
                            ? "No options available"
                            : "Please select"
                    }
                  />
                </SelectTrigger>
                <SelectContent>
                  {settlementStatuses.map((s) => (
                    <SelectItem key={s.id} value={s.id}>
                      {s.name || s.description}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {statusError && (
                <p className="text-xs text-red-500 mt-1">
                  Could not load settlement statuses.
                </p>
              )}
            </div>

            <div className="col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Notes
              </label>
              <Textarea
                value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
                placeholder="Leave some notes here..."
                rows={3}
              />
            </div>
          </div>
        </div>

        <div className="flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={handleAllocateProportionally}
            disabled={!form.checkAmount || checkedIds.size === 0}
            className="px-3.5 py-2 text-sm font-medium text-primary bg-white border border-primary/30 rounded-lg hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Allocate Proportionally
          </button>
          <button
            type="button"
            onClick={handleDistributePayment}
            disabled={!form.checkAmount || checkedIds.size === 0}
            className="px-3.5 py-2 text-sm font-medium text-primary bg-white border border-primary/30 rounded-lg hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Distribute Payment
          </button>
        </div>

        <LienTable
          liens={liens}
          checkedIds={checkedIds}
          onToggleCheck={toggleCheck}
          onToggleAll={toggleAll}
          isRowSelectable={isLienPayable}
          columns={paymentColumns}
          footer={paymentFooter}
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
