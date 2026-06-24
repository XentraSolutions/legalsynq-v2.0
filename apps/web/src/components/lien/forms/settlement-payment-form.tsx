"use client";

import { useState, useEffect } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { settlementService } from "@/lib/settlement";
import { liensService } from "@/lib/liens";
import { ApiError } from "@/lib/api-client";
import { CreateLienSettlementRequest, UpdateSettlementRequest } from "@/lib/settlement/settlement.types";

interface CreateSettlementFormProps {
  caseId: string;
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
  noRecovery?: boolean;
  lookups?: {
    settlementType: { id: string; description: string }[];
    settlementStatus: { id: string; description: string }[];
  };
}

const INITIAL_FORM = {
  lienStatus: "1",
  closedDate: "",
  amount: "",
  checkAmount: "",
  checkDate: "",
  checkNumber: "",
  type: "",
  status: "",
  note: "",
};

export function CreateSettlementForm({
  caseId,
  open,
  onClose,
  onCreated,
  noRecovery = false,
  lookups = { settlementType: [], settlementStatus: [] },
}: CreateSettlementFormProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const [liens, setLiens] = useState<any[]>([]);
  const [loadingLiens, setLoadingLiens] = useState(false);
  const [checkedLienIds, setCheckedLienIds] = useState<string[]>([]);
  const [lienAmounts, setLienAmounts] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!open || !caseId) return;

    const fetchLiens = async () => {
      setLoadingLiens(true);
      try {
        const result = await liensService.getLiens({caseId});
        setLiens(result?.items || (Array.isArray(result) ? result : []));
      } catch (err) {
      } finally {
        setLoadingLiens(false);
      }
    };

    fetchLiens();
  }, [open, caseId, addToast]);

  const formatDate = (dateString: string) => {
    if (!dateString) return "";
    const date = new Date(dateString);
    const formatter = new Intl.DateTimeFormat("en-CA");
    return formatter.format(date);
  };

  const handleToggleLien = (id: string) => {
    setCheckedLienIds((prev) => {
      const isChecked = prev.includes(id);
      if (isChecked) {
        const next = prev.filter((item) => item !== id);
        setLienAmounts((amounts) => {
          const updated = { ...amounts };
          delete updated[id];
          return updated;
        });
        return next;
      } else {
        return [...prev, id];
      }
    });
  };

  const handleAmountChange = (id: string, value: string) => {
    setLienAmounts((prev) => ({
      ...prev,
      [id]: value,
    }));
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      const requests: Promise<any>[] = [];

      const paymentPayload: CreateLienSettlementRequest = {
        amount: noRecovery ? "" : form.amount,
        amountToSettle: noRecovery ? "" : form.amount,
        checkAmount: noRecovery ? "" : form.checkAmount,
        checkDate: !noRecovery && form.checkDate ? formatDate(form.checkDate) : "",
        checkNumber: noRecovery ? "" : form.checkNumber,
        closedDate: noRecovery && form.closedDate ? formatDate(form.closedDate) : "",
        lienId: noRecovery ? "" : checkedLienIds.join(","),
        lienStatus: form.lienStatus,
        netProfit: "",
        note: form.note,
        paymentNumber: "",
        payor: "",
        status: noRecovery ? "" : form.status,
        type: noRecovery ? "" : form.type,
      };

      if (!noRecovery) {
        requests.push(settlementService.createPayment(paymentPayload));
        const allocationPayload: UpdateSettlementRequest = {
          caseId,
          payments: checkedLienIds.map((id) => String(lienAmounts[id] || "0"))
        };
        requests.push(settlementService.updateSettlement(allocationPayload));
      }

      await Promise.all(requests);

      addToast({
        type: "success",
        title: "Success",
        description: noRecovery
          ? "Status updated successfully."
          : "Payment and allocations saved successfully.",
      });

      handleResetClose();
      onCreated?.();
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({ type: "error", title: "Error", description: err.message });
      } else {
        addToast({ type: "error", title: "Error", description: "An unexpected error occurred" });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleResetClose = () => {
    setForm({ ...INITIAL_FORM });
    setCheckedLienIds([]);
    setLienAmounts({});
    setLiens([]);
    setErrors({});
    onClose();
  };

  const isFormInvalid = () => {
    if (noRecovery) {
      return !form.lienStatus || !form.closedDate;
    }
    return (
      !form.lienStatus ||
      !form.checkAmount ||
      !form.checkDate ||
      !form.checkNumber ||
      !form.type ||
      checkedLienIds.length === 0
    );
  };

  return (
    <FormModal
      open={open}
      onClose={handleResetClose}
      onSubmit={handleSubmit}
      title={noRecovery ? "No Recovery Setup" : "Payment"}
      submitLabel={submitting ? "Saving..." : noRecovery ? "Save" : "Save Payment"}
      submitDisabled={submitting || isFormInvalid()}
    >
      <div className="space-y-5">
        {noRecovery && (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                <span className="text-red-500">*</span> Lien Status
              </label>
              <select
                value={form.lienStatus}
                onChange={(e) => setForm({ ...form, lienStatus: e.target.value })}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              >
                <option value="1">Open</option>
                <option value="2">Close</option>
              </select>
            </div>
            <Field
              label="Date Closed"
              required
              type="date"
              value={form.closedDate}
              onChange={(v) => setForm({ ...form, closedDate: v })}
            />
          </div>
        )}

        {!noRecovery && (
          <>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                <span className="text-red-500">*</span> Lien Status
              </label>
              <select
                value={form.lienStatus}
                onChange={(e) => setForm({ ...form, lienStatus: e.target.value })}
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary mb-1"
              >
                <option value="1">Open</option>
                <option value="2">Close</option>
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <Field
                label="Amount to Settle"
                type="number"
                placeholder="0.00"
                value={form.amount}
                onChange={(v) => setForm({ ...form, amount: v })}
              />
              <Field
                label="Check Amount"
                required
                type="number"
                placeholder="0.00"
                value={form.checkAmount}
                onChange={(v) => setForm({ ...form, checkAmount: v })}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
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
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  <span className="text-red-500">*</span> Settlement Type
                </label>
                <select
                  value={form.type}
                  onChange={(e) => setForm({ ...form, type: e.target.value })}
                  className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                >
                  <option value="" disabled>Please select</option>
                  {lookups.settlementType.map((s) => (
                    <option key={s.id} value={s.id}>{s.description}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Settlement Status
                </label>
                <select
                  value={form.status}
                  onChange={(e) => setForm({ ...form, status: e.target.value })}
                  className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                >
                  <option value="" disabled>Please select</option>
                  {lookups.settlementStatus.map((s) => (
                    <option key={s.id} value={s.id}>{s.description}</option>
                  ))}
                </select>
              </div>
            </div>
          </>
        )}

        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Notes
          </label>
          <textarea
            value={form.note}
            onChange={(e) => setForm({ ...form, note: e.target.value })}
            placeholder="Leave some notes here..."
            rows={4}
            className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
          />
        </div>

        <div className="border border-gray-200 rounded-lg overflow-hidden bg-white">
          {loadingLiens ? (
            <div className="p-6 text-center text-sm text-gray-400">
              <div className="inline-block h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent mr-2" />
              Loading case liens...
            </div>
          ) : liens.length === 0 ? (
            <div className="p-6 text-center text-sm text-gray-400 italic">
              No active liens associated with this case.
            </div>
          ) : (
            <div className="overflow-x-auto max-h-[220px]">
              <table className="w-full text-left text-sm border-collapse">
                <thead>
                  <tr className="bg-gray-50/70 border-b border-gray-200 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                    <th className="px-4 py-2 w-10 text-center">Select</th>
                    <th className="px-4 py-2">Provider / Category</th>
                    <th className="px-4 py-2">Lien Amount</th>
                    <th className="px-4 py-2 w-36">Settlement Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {liens.map((lien) => {
                    const isChecked = checkedLienIds.includes(lien.id);
                    return (
                      <tr key={lien.id} className={`hover:bg-gray-50/50 transition-colors ${isChecked ? "bg-primary/5" : ""}`}>
                        <td className="px-4 py-2 text-center">
                          <input
                            type="checkbox"
                            checked={isChecked}
                            onChange={() => handleToggleLien(lien.id)}
                            className="rounded border-gray-300 text-primary focus:ring-primary h-4 w-4 cursor-pointer"
                          />
                        </td>
                        <td className="px-4 py-2 font-medium text-gray-700">
                          <div>{lien.providerName || lien.provider || "Unknown Provider"}</div>
                          <div className="text-[11px] text-gray-400 font-normal">{lien.categoryName || lien.category || "Uncategorized"}</div>
                        </td>
                        <td className="px-4 py-2 text-gray-600 font-medium">
                          {lien.lienAmount !== undefined && lien.lienAmount !== null ? `$${Number(lien.lienAmount).toLocaleString()}` : "—"}
                        </td>
                        <td className="px-4 py-2">
                          <input
                            type="number"
                            disabled={!isChecked}
                            placeholder="0.00"
                            value={lienAmounts[lien.id] || ""}
                            onChange={(e) => handleAmountChange(lien.id, e.target.value)}
                            className={`w-full px-2 py-1 text-xs border border-gray-200 rounded focus:outline-none focus:ring-1 focus:ring-primary/30 focus:border-primary ${!isChecked ? "bg-gray-50 opacity-60 cursor-not-allowed" : "bg-white"}`}
                          />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
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
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className={`w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary ${error ? "border-red-300" : "border-gray-200"}`}
      />
      {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
    </div>
  );
}