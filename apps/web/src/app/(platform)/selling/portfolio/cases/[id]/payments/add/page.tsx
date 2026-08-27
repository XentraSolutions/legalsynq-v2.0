"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, ArrowDownUp, Split } from "lucide-react";
import { toast } from "sonner";
import Field from "@/components/lien/field";
import { Button } from "@/components/selling/button";
import {
  PAYMENT_METHOD_OPTIONS,
  formatCurrency,
} from "@/components/selling/lien-detail/payment-tab";
import { useCaseLiens } from "@/lib/selling/use-case-liens";
import { useLienPayments } from "@/lib/selling/use-lien-payments";
import { lienPaymentsService } from "@/lib/selling/lien-payments.service";
import { ApiError } from "@/lib/api-client";

interface AllocationRow {
  lienId: string;
  lienNumber: string;
  fundingCompany: string;
  billingAmount: number;
  askAmount: number;
  remainingBalance: number;
}

export default function AddCasePaymentPage() {
  const params = useParams<{ id: string }>();
  const caseId = params?.id ?? "";
  const router = useRouter();
  const backHref = `/selling/portfolio/cases/${caseId}?tab=payments`;

  const [amount, setAmount] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("");
  const [paymentDate, setPaymentDate] = useState("");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [notes, setNotes] = useState("");
  const [selected, setSelected] = useState<Record<string, boolean>>({});
  const [allocationAmounts, setAllocationAmounts] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const liensQuery = useCaseLiens(caseId, { pageSize: 100 });
  // Existing posted payments across the case, used to compute each lien's
  // remaining balance (askAmount minus what's already been received).
  const paymentsQuery = useLienPayments(caseId, {
    postingStatus: "Posted",
    pageSize: 200,
  });

  const receivedByLien = useMemo(() => {
    const map = new Map<string, number>();
    for (const payment of paymentsQuery.data?.items ?? []) {
      map.set(payment.lienId, (map.get(payment.lienId) ?? 0) + payment.amount);
    }
    return map;
  }, [paymentsQuery.data]);

  const rows = useMemo<AllocationRow[]>(
    () =>
      (liensQuery.data?.items ?? []).map((lien) => {
        const received = receivedByLien.get(lien.lienId) ?? 0;
        return {
          lienId: lien.lienId,
          lienNumber: lien.lienNumber,
          fundingCompany: lien.fundingCompany,
          billingAmount: lien.billingAmount,
          askAmount: lien.askAmount,
          remainingBalance: Math.max((lien.askAmount ?? 0) - received, 0),
        };
      }),
    [liensQuery.data, receivedByLien],
  );

  const totals = useMemo(
    () => ({
      billingAmount: rows.reduce((sum, r) => sum + r.billingAmount, 0),
      askAmount: rows.reduce((sum, r) => sum + r.askAmount, 0),
      remainingBalance: rows.reduce((sum, r) => sum + r.remainingBalance, 0),
    }),
    [rows],
  );

  const selectedRows = rows.filter((r) => selected[r.lienId]);
  const allocatedTotal = selectedRows.reduce(
    (sum, r) => sum + (Number(allocationAmounts[r.lienId]) || 0),
    0,
  );

  const paymentAmount = Number(amount) || 0;

  const applyAllocation = (compute: (row: AllocationRow, index: number, list: AllocationRow[]) => number) => {
    if (paymentAmount <= 0) {
      toast.info("Enter a payment amount before allocating.");
      return;
    }
    const targets = selectedRows.length > 0
      ? selectedRows
      : rows.filter((r) => r.remainingBalance > 0);
    if (targets.length === 0) return;

    const nextSelected = { ...selected };
    const nextAmounts = { ...allocationAmounts };
    let allocated = 0;
    targets.forEach((row, index) => {
      nextSelected[row.lienId] = true;
      const isLast = index === targets.length - 1;
      const raw = isLast
        ? paymentAmount - allocated
        : compute(row, index, targets);
      const value = Math.round(Math.min(raw, row.remainingBalance) * 100) / 100;
      allocated += value;
      nextAmounts[row.lienId] = value > 0 ? value.toFixed(2) : "";
    });
    setSelected(nextSelected);
    setAllocationAmounts(nextAmounts);
  };

  const handleAllocateProportionally = () => {
    const targets = selectedRows.length > 0
      ? selectedRows
      : rows.filter((r) => r.remainingBalance > 0);
    const totalRemaining = targets.reduce((sum, r) => sum + r.remainingBalance, 0);
    applyAllocation((row) =>
      totalRemaining > 0
        ? (row.remainingBalance / totalRemaining) * paymentAmount
        : paymentAmount / targets.length,
    );
  };

  const handleDistributeEvenly = () => {
    const targets = selectedRows.length > 0
      ? selectedRows
      : rows.filter((r) => r.remainingBalance > 0);
    applyAllocation(() => paymentAmount / targets.length);
  };

  const handleAllocationInput = (lienId: string, raw: string) => {
    if (raw === "" || paymentAmount <= 0) {
      setAllocationAmounts((current) => ({ ...current, [lienId]: raw }));
      return;
    }
    const entered = Number(raw);
    if (Number.isNaN(entered)) return;
    const otherAllocated = selectedRows
      .filter((r) => r.lienId !== lienId)
      .reduce((sum, r) => sum + (Number(allocationAmounts[r.lienId]) || 0), 0);
    const row = rows.find((r) => r.lienId === lienId);
    const maxAllowed = Math.max(
      Math.min(paymentAmount - otherAllocated, row?.remainingBalance ?? Infinity),
      0,
    );
    const clamped = Math.min(entered, maxAllowed);
    setAllocationAmounts((current) => ({
      ...current,
      [lienId]: clamped.toString(),
    }));
  };

  const toggleRow = (lienId: string) => {
    setSelected((current) => {
      const next = { ...current, [lienId]: !current[lienId] };
      if (!next[lienId]) {
        setAllocationAmounts((amounts) => ({ ...amounts, [lienId]: "" }));
      }
      return next;
    });
  };

  const canSubmit =
    paymentAmount > 0 &&
    paymentMethod !== "" &&
    paymentDate !== "" &&
    referenceNumber.trim() !== "" &&
    selectedRows.length > 0;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSaving(true);
    try {
      await lienPaymentsService.recordLienPayment(caseId, {
        amount: paymentAmount,
        paymentDate,
        paymentMethod,
        referenceNumber: referenceNumber.trim(),
        notes: notes.trim() || undefined,
        allocations: selectedRows
          .map((row) => ({
            lienId: row.lienId,
            amount: Number(allocationAmounts[row.lienId]) || 0,
          }))
          .filter((a) => a.amount > 0),
      });
      toast.success("Payment recorded.");
      router.push(backHref);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Failed to record payment");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-6">
      <Link
        href={backHref}
        aria-label="Back to Case"
        className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors"
      >
        <ArrowLeft className="h-5 w-5" />
      </Link>

      <div>
        <h1 className="text-2xl font-bold text-gray-900">Add Lien Payment</h1>
        <p className="text-sm text-gray-400 mt-1">
          Provide the payment information below to keep your payment details accurate and up to date.
        </p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field
          type="number"
          label="Payment Amount"
          required
          maxDecimals={2}
          prefix="$"
          value={amount}
          onChange={setAmount}
        />
        <Field
          type="select"
          label="Payment Method"
          required
          multiple={false}
          placeholder="Select payment method"
          options={PAYMENT_METHOD_OPTIONS}
          value={paymentMethod || null}
          onChange={(v: string) => setPaymentMethod(v)}
        />
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Field
          type="date"
          label="Payment Date"
          required
          value={paymentDate}
          onChange={setPaymentDate}
        />
        <Field
          type="text"
          label="Reference / ID #"
          required
          value={referenceNumber}
          onChange={setReferenceNumber}
        />
      </div>

      <Field
        type="textarea"
        label="Notes"
        placeholder="Leave payment note here..."
        value={notes}
        onChange={setNotes}
      />

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="flex items-start justify-between gap-4 px-6 pt-5">
          <div>
            <h2 className="text-base font-semibold text-gray-900">
              Lien Payment Allocation <span className="text-red-500">*</span>
            </h2>
            <p className="text-sm text-gray-400 mt-0.5">
              Select liens and auto-distribute funds, or manually enter amounts for each selected lien.
            </p>
          </div>
          <div className="flex items-center gap-2 shrink-0">
            <Button
              variant="secondary"
              className="border-gray-300"
              onClick={handleAllocateProportionally}
            >
              Allocate Proportionally
              <Split className="h-4 w-4" />
            </Button>
            <Button
              variant="secondary"
              className="border-gray-300"
              onClick={handleDistributeEvenly}
            >
              Distribute
              <ArrowDownUp className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <div className="overflow-x-auto mt-4">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-left text-xs font-medium text-gray-500">
                <th className="px-6 py-3 w-10">
                  <input
                    type="checkbox"
                    checked={rows.length > 0 && rows.every((r) => selected[r.lienId])}
                    onChange={(e) => {
                      const next: Record<string, boolean> = {};
                      rows.forEach((r) => {
                        next[r.lienId] = e.target.checked;
                      });
                      setSelected(next);
                      if (!e.target.checked) setAllocationAmounts({});
                    }}
                    className="rounded border-gray-300"
                  />
                </th>
                <th className="px-3 py-3">Lien ID</th>
                <th className="px-3 py-3">Funding Company</th>
                <th className="px-3 py-3">Billing Amount</th>
                <th className="px-3 py-3">Ask Amount</th>
                <th className="px-3 py-3">Remaining Balance</th>
                <th className="px-3 py-3">Amount Received</th>
              </tr>
            </thead>
            <tbody>
              {liensQuery.isLoading ? (
                <tr>
                  <td colSpan={7} className="px-6 py-8 text-center text-gray-400">
                    Loading liens…
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-6 py-8 text-center text-gray-400">
                    No liens on this case.
                  </td>
                </tr>
              ) : (
                rows.map((row) => (
                  <tr key={row.lienId} className="border-t border-gray-100">
                    <td className="px-6 py-3">
                      <input
                        type="checkbox"
                        checked={!!selected[row.lienId]}
                        onChange={() => toggleRow(row.lienId)}
                        className="rounded border-gray-300"
                      />
                    </td>
                    <td className="px-3 py-3 text-gray-900 font-medium whitespace-nowrap">
                      {row.lienNumber}
                    </td>
                    <td className="px-3 py-3 text-gray-600 whitespace-nowrap">
                      {row.fundingCompany || "—"}
                    </td>
                    <td className="px-3 py-3 text-gray-600 whitespace-nowrap">
                      {formatCurrency(row.billingAmount)}
                    </td>
                    <td className="px-3 py-3 text-gray-600 whitespace-nowrap">
                      {formatCurrency(row.askAmount)}
                    </td>
                    <td className="px-3 py-3 text-gray-600 whitespace-nowrap">
                      {formatCurrency(row.remainingBalance)}
                    </td>
                    <td className="px-3 py-3 whitespace-nowrap">
                      {selected[row.lienId] ? (
                        <div className="relative w-32">
                          <span className="absolute left-2 top-1/2 -translate-y-1/2 text-gray-400 text-xs">
                            $
                          </span>
                          <input
                            type="number"
                            min={0}
                            step="0.01"
                            value={allocationAmounts[row.lienId] ?? ""}
                            onChange={(e) => handleAllocationInput(row.lienId, e.target.value)}
                            className="w-full pl-5 pr-2 py-1.5 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#EE7132]/20 focus:border-[#EE7132]"
                          />
                        </div>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
            {rows.length > 0 && (
              <tfoot>
                <tr className="border-t border-gray-200 bg-gray-50 font-semibold text-gray-900">
                  <td className="px-6 py-3" />
                  <td className="px-3 py-3">Total</td>
                  <td className="px-3 py-3" />
                  <td className="px-3 py-3 whitespace-nowrap">
                    {formatCurrency(totals.billingAmount)}
                  </td>
                  <td className="px-3 py-3 whitespace-nowrap">
                    {formatCurrency(totals.askAmount)}
                  </td>
                  <td className="px-3 py-3 whitespace-nowrap">
                    {formatCurrency(totals.remainingBalance)}
                  </td>
                  <td className="px-3 py-3 whitespace-nowrap">
                    {allocatedTotal > 0 ? formatCurrency(allocatedTotal) : "-"}
                  </td>
                </tr>
              </tfoot>
            )}
          </table>
        </div>

        <div className="h-5" />
      </div>

      <div className="flex justify-end gap-3">
        <Button variant="secondary" onClick={() => router.push(backHref)}>
          Cancel
        </Button>
        <Button
          variant="primary"
          disabled={!canSubmit}
          loading={saving}
          onClick={handleSubmit}
        >
          Save Payment
        </Button>
      </div>
    </div>
  );
}
