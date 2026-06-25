import React, { useEffect, useMemo, useState } from "react";
import Field from "../../field";
import { lookupService } from "@/lib/lookup";
import { CreateCaseForm } from "../create-case-form";
import { CreateMedicalCode } from "../add-medical-code";
import { casesService } from "@/lib/cases";

export interface MedicalCodesDescriptionProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
}

const INITIAL_FORM = {
  procedureCode: "",
  medicareCost: "",
  billingAmount: "",
  purchaseAmount: "",
  purchaseAmountType: "amount",
  payee: "",
  outboundCheckNumber: "",
};

const INITIAL_ROW = {
  id: "",
  code: "",
  description: "",
  medicareCost: 0,
  billingAmount: 0,
  purchaseAmount: 0,
};

function formatCurrency(value: number) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    maximumFractionDigits: 2,
  }).format(value);
}

function formatPercent(value: number) {
  return `${value.toFixed(2)}%`;
}

function parseNumber(value: string) {
  const parsed = Number(value.replace(/,/g, ""));
  return Number.isFinite(parsed) ? parsed : 0;
}

export default function MedicalCodesDescription(
  props: MedicalCodesDescriptionProps,
) {
  const { data = {}, onFormValid } = props;

  const [form, setForm] = useState({ ...INITIAL_FORM, ...data });
  const [procedureOptions, setProcedureOptions] = useState(
    [] as Array<{ key: string; value: string; label: string }>,
  );
  const [rows, setRows] = useState<Array<typeof INITIAL_ROW>>(
    data?.codeRows ?? [],
  );
  const [editingId, setEditingId] = useState<string>("");
  const [showCreate, setShowCreate] = useState<boolean>(false);

  useEffect(() => {
    loadProcedureCodes();
  }, []);

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form, rows]);

  async function loadProcedureCodes() {
    try {
      const procedureRes = await casesService.getMedicalCodes(
        props?.caseId ?? "",
      );
      const codes = await lookupService.getMedicalProcedureCodes();
      console.log(codes, "wenks");
      const list = codes.map((item, index) => ({
        key: item.code + index,
        value: item.code,
        label: item.description,
      }));
      setProcedureOptions(list ?? []);
    } catch (e) {
      setProcedureOptions([]);
    }
  }

  function validateForm() {
    const valid = rows.length > 0;
    onFormValid?.(valid, { ...form, codeRows: rows });
  }

  const currentBilling = parseNumber(form.billingAmount);
  const currentPurchase = form.purchaseAmount;

  const calculatedValue = useMemo(() => {
    if (form.purchaseAmountType === "amount") {
      return currentBilling > 0 ? (currentPurchase / currentBilling) * 100 : 0;
    }
    return currentBilling * (currentPurchase / 100);
  }, [form.purchaseAmountType, form.purchaseAmount, form.billingAmount]);

  const totals = useMemo(
    () =>
      rows.reduce(
        (tot, row) => ({
          medicare: tot.medicare + row.medicareCost,
          billing: tot.billing + row.billingAmount,
          purchase: tot.purchase + row.purchaseAmount,
        }),
        { medicare: 0, billing: 0, purchase: 0 },
      ),
    [rows],
  );

  function resetLine() {
    setForm({
      ...form,
      procedureCode: "",
      medicareCost: "",
      billingAmount: "",
      purchaseAmount: "",
    });
    setEditingId("");
  }

  function handleAddOrUpdateLine() {
    const selectedOption = procedureOptions.find(
      (option) => option.value === form.procedureCode,
    );
    const nextRow = {
      id: editingId || `${Date.now()}`,
      code: form.procedureCode,
      description: selectedOption?.label ?? "",
      medicareCost: parseNumber(form.medicareCost),
      billingAmount: currentBilling,
      purchaseAmount:
        form.purchaseAmountType === "amount"
          ? currentPurchase
          : currentBilling * (currentPurchase / 100),
    };

    setRows((current) => {
      if (editingId) {
        return current.map((row) => (row.id === editingId ? nextRow : row));
      }
      return [...current, nextRow];
    });
    resetLine();
  }

  function handleEditRow(row: typeof INITIAL_ROW) {
    setEditingId(row.id);
    setForm({
      ...form,
      procedureCode: row.code,
      medicareCost: String(row.medicareCost),
      billingAmount: String(row.billingAmount),
      purchaseAmount:
        form.purchaseAmountType === "amount"
          ? String(row.purchaseAmount)
          : String(
              row.billingAmount > 0
                ? (row.purchaseAmount / row.billingAmount) * 100
                : 0,
            ),
    });
  }

  function handleDeleteRow(id: string) {
    setRows((current) => current.filter((row) => row.id !== id));
    if (editingId === id) {
      resetLine();
    }
  }

  const isLineValid = currentBilling > 0 && currentPurchase > 0;

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        <div className="col-12 mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-primary">
            <i className="ri-capsule-line text-light" />
          </span>
          <span className="font-semibold mb-2 text-blue-700 mt-1">
            Medical Code Information
          </span>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <Field
              label="Medical Code & Description"
              value={form.procedureCode}
              options={procedureOptions}
              onChange={(v) =>
                setForm({ ...form, procedureCode: v.toString() })
              }
              placeholder="Select a code"
              type="select"
            >
              <button
                type="button"
                onClick={() => {
                  setShowCreate(!showCreate);
                }}
                className="inline-flex items-center justify-center rounded-lg px-2 py-2 text-sm font-semibold text-primary disabled:cursor-not-allowed disabled:bg-gray-300"
              >
                Add New Medical Code
              </button>
            </Field>
          </div>

          <Field
            label="Medicare Cost"
            value={form.medicareCost}
            onChange={(v) => setForm({ ...form, medicareCost: v.toString() })}
            placeholder="Medicare Cost"
          />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
          <Field
            label="Billing Amount"
            value={form.billingAmount}
            onChange={(v) => setForm({ ...form, billingAmount: v.toString() })}
            placeholder="Billing Amount"
          />

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Purchase Amount
              <span className="text-red-500 ml-0.5">*</span>
            </label>
            <div className="flex flex-col gap-2">
              <input
                type="text"
                value={form.purchaseAmount}
                onChange={(e) => {
                  setForm({ ...form, purchaseAmount: e.target.value });
                }}
                placeholder="Purchase Amount"
                className="w-full border rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
              <div className="flex flex-wrap items-center justify-end gap-2 mb-4">
                <span className="text-sm text-gray-500">
                  {form.purchaseAmountType === "amount"
                    ? formatPercent(calculatedValue)
                    : formatCurrency(calculatedValue)}
                </span>
                <label
                  className={`inline-flex w-10 items-center justify-center p-1 rounded text-sm text-gray-600 ${form.purchaseAmountType === "amount" ? "border-2 border-gray-300 bg-gray-100" : ""}`}
                >
                  <input
                    type="radio"
                    checked={form.purchaseAmountType === "amount"}
                    onChange={() =>
                      setForm({ ...form, purchaseAmountType: "amount" })
                    }
                    className="appearance-none peer"
                  />
                  <i className="ri-exchange-dollar-line radio-icon"></i>
                </label>
                <label
                  className={`inline-flex w-10 items-center justify-center p-1 rounded text-sm text-gray-600 ${form.purchaseAmountType === "percent" ? "border-2 border-gray-300 bg-gray-100" : ""}`}
                >
                  <input
                    type="radio"
                    checked={form.purchaseAmountType === "percent"}
                    onChange={() =>
                      setForm({ ...form, purchaseAmountType: "percent" })
                    }
                    className="appearance-none peer"
                  />

                  <i className="ri-percent-line radio-icon"></i>
                </label>
              </div>
            </div>
          </div>
        </div>

        <div className="justify-self-end">
          <button
            type="button"
            disabled={!isLineValid}
            onClick={handleAddOrUpdateLine}
            className="inline-flex items-center justify-center rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary/90 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {editingId ? "Update" : "Add"}
          </button>
        </div>

        <div className="col-12 mt-5">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Code / Description
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Medical Care Cost
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Billing Amount
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Purchase Amount
                  </th>
                  <th className="px-4 py-3" />
                  <th className="px-4 py-3" />
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {rows.map((row) => (
                  <tr key={row.id}>
                    <td className="px-4 py-3 text-sm text-gray-700">
                      <div className="font-medium">{row.code}</div>
                      <div className="text-gray-500 text-xs truncate">
                        {row.description}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(row.medicareCost)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(row.billingAmount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(row.purchaseAmount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-center">
                      <button
                        type="button"
                        onClick={() => handleEditRow(row)}
                        className="text-primary hover:text-primary/80"
                      >
                        Edit
                      </button>
                    </td>
                    <td className="px-4 py-3 text-sm text-center">
                      <button
                        type="button"
                        onClick={() => handleDeleteRow(row.id)}
                        className="text-red-600 hover:text-red-700"
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="px-4 py-6 text-center text-sm text-gray-500"
                    >
                      No record
                    </td>
                  </tr>
                )}
              </tbody>
              {rows.length > 0 && (
                <tfoot className="bg-gray-50">
                  <tr>
                    <td className="px-4 py-3 text-sm font-semibold text-gray-700">
                      Total
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(totals.medicare)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(totals.billing)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(totals.purchase)}
                    </td>
                    <td className="px-4 py-3" />
                    <td className="px-4 py-3" />
                  </tr>
                </tfoot>
              )}
            </table>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-6">
        <Field
          label="Payee"
          value={form.payee}
          onChange={(v) => setForm({ ...form, payee: v.toString() })}
          placeholder="Payee"
        />
        <Field
          label="Outbound Check Number"
          value={form.outboundCheckNumber}
          onChange={(v) =>
            setForm({ ...form, outboundCheckNumber: v.toString() })
          }
          placeholder="Outbound Check Number"
        />
      </div>

      {showCreate && (
        <CreateMedicalCode
          open={showCreate}
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            loadProcedureCodes();
          }}
        />
      )}
    </div>
  );
}
