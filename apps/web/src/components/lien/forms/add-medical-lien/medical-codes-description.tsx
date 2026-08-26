import React, { useCallback, useEffect, useMemo, useState } from "react";
import Field from "../../field";
import { BaseSelect } from "@/components/ui/base-select";
import { lookupService } from "@/lib/lookup";
import { CreateCaseForm } from "../create-case-form";
import { CreateMedicalCode } from "../add-medical-code";
import { casesService } from "@/lib/cases";
import { ConfirmDialog } from "../../modal";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { CreateMedicalCodeLiensDto } from "@/lib/cases/cases.types";
import {
  useMedicareCosts,
  useMedicareProcedureCodes,
} from "@/hooks/use-case-liens";
import { Input } from "@/components/ui/input";

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

function roundToTwo(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function parseNumber(value: string) {
  if (!value) return;
  const parsed = Number(value.replace(/,/g, ""));
  return Number.isFinite(parsed) ? parsed : 0;
}

export default function MedicalCodesDescription(
  props: MedicalCodesDescriptionProps,
) {
  const { data = {}, onFormValid } = props;
  const addToast = useLienStore((s) => s.addToast);
  const { data: medicalCodes } = useMedicareProcedureCodes();

  const [form, setForm] = useState<any>({ ...INITIAL_FORM, ...data });
  const { data: medicareCosts } = useMedicareCosts(form.procedureCode);

  const [rows, setRows] = useState<Array<typeof INITIAL_ROW>>(
    data?.codeRows ?? [],
  );
  const [editingId, setEditingId] = useState<string>("");
  const [showCreate, setShowCreate] = useState<boolean>(false);
  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    label: string;
  } | null>(null);

  useEffect(() => {
    validateForm();
  }, [rows, data?.codeRows]);

  useEffect(() => {
    setForm((prev: any) => ({ ...prev, medicareCost: medicareCosts }));
  }, [medicareCosts]);

  function validateForm() {
    const previousRows = Array.isArray(data?.codeRows) ? data.codeRows : [];
    const rowsChanged = JSON.stringify(rows) !== JSON.stringify(previousRows);
    const valid = rowsChanged || rows.length > 0;
    if (valid) onFormValid?.(valid, { ...form, codeRows: rows });
  }
  function cleanNumericInput(raw: string): string {
    // Remove everything except digits and dots
    const cleaned = raw.replace(/[^\d.]/g, "");

    // Split by the decimal point to isolate the whole number and decimals
    const parts = cleaned.split(".");

    if (parts.length > 2) {
      // If there are multiple dots, keep only the first dot and join the rest
      return parts[0] + "." + parts.slice(1).join("");
    }

    return cleaned;
  }

  const currentBilling = form.billingAmount;
  const currentPurchase = form.purchaseAmount;

  const handleParentInputChange = (raw: string) => {
    const sanitized = cleanNumericInput(raw);

    // Allow the user to type a lone decimal point or trailing dot without resetting to NaN immediately
    if (sanitized === "" || sanitized === ".") {
      setForm({ ...form, purchaseAmount: sanitized });
      return;
    }

    const n = parseFloat(sanitized);
    if (!isNaN(n)) {
      // If it ends with a dot (e.g. "12."), keep it as a string temporarily so the user can type decimals,
      // otherwise store the parsed number.
      const valueToStore = sanitized.endsWith(".") ? sanitized : n;
      setForm({ ...form, purchaseAmount: valueToStore });
    } else {
      setForm({ ...form, purchaseAmount: sanitized });
    }
  };

  const inverseValue = (() => {
    if (!form.purchaseAmount) return "";
    return !form.billingAmount
      ? 0
      : form.purchaseAmountType === "percent"
        ? Number(((form.purchaseAmount / 100) * form.billingAmount).toFixed(2))
        : roundToTwo((form.purchaseAmount / form.billingAmount) * 100);
  })();

  const handleToggleMode = (toPercent: boolean) => {
    const computated = !form.billingAmount
      ? 0
      : toPercent
        ? roundToTwo((form.purchaseAmount / form.billingAmount) * 100)
        : Number(((form.purchaseAmount / 100) * form.billingAmount).toFixed(2));
    if (inverseValue !== null) {
      setForm((prev: any) => ({
        ...prev,
        purchaseAmountType: toPercent ? "percent" : "amount",
        purchaseAmount: computated,
      }));
    }
  };

  const totals = useMemo(() => {
    return rows.reduce(
      (tot, row) => ({
        medicare: tot.medicare + row.medicareCost,
        billing: tot.billing + row.billingAmount,
        purchase: tot.purchase + row.purchaseAmount,
      }),
      { medicare: 0, billing: 0, purchase: 0 },
    );
  }, [rows, data]);

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

  const getCurrentValue = () => {
    if (form.purchaseAmountType == "percent") {
      const val =
        typeof inverseValue == "string"
          ? parseNumber(inverseValue)
          : inverseValue;
      return val;
    } else {
      return typeof currentPurchase == "string"
        ? parseNumber(currentPurchase)
        : currentPurchase;
    }
  };
  function handleAddOrUpdateLine() {
    const selectedOption = medicalCodes?.find(
      (option) => option.value === form.procedureCode,
    );

    setTimeout(async () => {
      if (props.lienId) {
        const response = await createMedicalCodeLiens(
          {
            ...form,
            id: form.id,
            code: form.procedureCode,
            description: selectedOption?.label ?? "",
            medicareCost: parseNumber(form.medicareCost),
            billingAmount: parseNumber(currentBilling),
            purchaseAmount: getCurrentValue(),
          },
          editingId != "",
        );

        const nextRow = {
          id: editingId || response.data,
          code: form.procedureCode,
          description: selectedOption?.label ?? "",
          medicareCost: parseNumber(form.medicareCost),
          billingAmount: parseNumber(currentBilling),
          purchaseAmount: getCurrentValue(),
        };

        setRows((current: any) => {
          if (editingId) {
            return current.map((row: any) =>
              row.id === editingId ? nextRow : row,
            );
          }
          return [...current, nextRow];
        });
      } else {
        const nextRow = {
          id: rows.length.toString(),
          code: form.procedureCode,
          description: selectedOption?.label ?? "",
          medicareCost: parseNumber(form.medicareCost),
          billingAmount: parseNumber(currentBilling),
          purchaseAmount: getCurrentValue(),
        };

        setRows((current: any) => {
          if (editingId) {
            return current.map((row: any) =>
              row.id === editingId ? nextRow : row,
            );
          }
          return [...current, nextRow];
        });
      }
      validateForm();
    }, 100);
    resetLine();
  }

  const findCodeByDescription = (description: string) => {
    return medicalCodes?.find((c) => c.value == description)?.key ?? "";
  };

  const createMedicalCodeLiens = async (
    payload: CreateMedicalCodeLiensDto,
    isEditing: boolean,
  ) => {
    try {
      const selectedCode =
        payload.code ||
        (typeof (payload as any).procedureCode === "string"
          ? (payload as any).procedureCode
          : "");

      const request: CreateMedicalCodeLiensDto = {
        id: payload.id,
        liensId: props.lienId ?? "",
        code: findCodeByDescription(selectedCode),
        medicareCost: parseFloat(payload.medicareCost).toFixed(2),
        billingAmount: parseFloat(payload.billingAmount).toFixed(2),
        purchaseAmount: parseFloat(payload.purchaseAmount).toFixed(2),
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      const res = isEditing
        ? await casesService.updateMedicalCodeLiens(request)
        : await casesService.createMedicalCodeLiens(request);
      addToast({
        type: "success",
        title: `Medical Code ${isEditing ? "Updated" : "Created"}`,
        description: `Medical Code has been ${isEditing ? "updated" : "created"}.`,
      });
      return res;
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Create Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    } finally {
    }
  };

  function handleEditRow(row: typeof INITIAL_ROW) {
    setEditingId(row.id);
    setForm((prev: any) => ({
      ...prev,
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
    }));
  }

  function handleDeleteRow(id: string) {
    // setRows((current) => current.filter((row) => row.id !== id));
    if (editingId === id) {
      resetLine();
    }
    setConfirmAction({ id: id, label: "Delete" });
  }

  async function deleteCode(id: string) {
    try {
      await casesService.deleteMedicalCodeLiens(id);
      addToast({
        type: "success",
        title: `Deleted`,
      });
      setTimeout(() => {
        const newList = rows.filter((p) => p.id != id);
        setRows(newList);
      }, 1000);
    } catch (err) {
      const reason = err instanceof Error ? err.message : String(err);

      addToast({
        type: "error",
        title: `Deleted`,
        description: reason,
      });
    }
  }

  const isLineValid =
    currentBilling > 0 &&
    !!form.procedureCode &&
    parseFloat(form.medicareCost ?? 0) > 0;

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
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Medical Code & Description
              <span className="text-red-500 ml-0.5">*</span>
            </label>
            <BaseSelect
              value={form.procedureCode}
              onChange={(v) => {
                setForm({ ...form, procedureCode: v });
                // getMedicalProcedureCosts(v);
              }}
              options={medicalCodes ?? []}
              placeholder="Select a code"
              searchPlaceholder="Search codes..."
              createAction={{
                label: "Add New Medical Code",
                onSelect: () => setShowCreate(true),
              }}
            />
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
            required
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
              <div
                className={`flex h-9.5 rounded-lg border border-gray-200 overflow-hidden transition-colors `}
              >
                <div className={`relative w-50 flex-1`}>
                  <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                    {form.purchaseAmountType == "percent" ? "%" : "$"}
                  </span>
                  <Input
                    type="text"
                    inputMode="decimal"
                    value={form.purchaseAmount}
                    onChange={(e) => handleParentInputChange(e.target.value)}
                    placeholder="0.00"
                    className={`h-full border-0 rounded-none focus:ring-0 bg-transparent text-gray-400 pl-6 pr-3}`}
                  />
                </div>

                <div className={`relative bg-gray-50 w-25`}>
                  {form.purchaseAmountType == "percent" && (
                    <span className="absolute left-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                      $
                    </span>
                  )}
                  <Input
                    type="text"
                    disabled
                    readOnly
                    value={inverseValue}
                    placeholder="0.00"
                    className={`h-full border-0 rounded-none focus:ring-0 bg-transparent text-gray-400 ${form.purchaseAmountType == "amount" ? "pl-6 pr-2" : "pl-5 pr-6"}`}
                  />
                  {form.purchaseAmountType == "amount" && (
                    <span className="absolute right-2.5 top-1/2 -translate-y-1/2 text-xs text-gray-400 pointer-events-none">
                      %
                    </span>
                  )}
                </div>
                <div className="flex items-stretch border-l border-gray-200">
                  <button
                    type="button"
                    onClick={() => handleToggleMode(false)}
                    className={`w-8 text-xs font-semibold transition-colors ${form.purchaseAmountType == "amount" ? "bg-primary text-white" : "bg-gray-50 text-gray-400 hover:bg-gray-100"}`}
                  >
                    $
                  </button>
                  <button
                    type="button"
                    onClick={() => handleToggleMode(true)}
                    className={`w-8 text-xs font-semibold transition-colors border-l border-gray-200 ${form.purchaseAmountType == "percent" ? "bg-primary text-white" : "bg-gray-50 text-gray-400 hover:bg-gray-100"}`}
                  >
                    %
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="justify-self-end">
          <button
            type="button"
            disabled={!isLineValid}
            onClick={handleAddOrUpdateLine}
            className="inline-flex items-center justify-center rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary/90 disabled:cursor-not-allowed disabled:bg-gray-300 mt-2"
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
                      <div className="text-gray-500 text-xs truncate max-w-90 block">
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
          }}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={() => {
            deleteCode(confirmAction.id);
            setConfirmAction(null);
          }}
          title={`${confirmAction.label} Medical Code`}
          description={`Are you sure you want to ${confirmAction.label.toLowerCase()} this medical code?\n\nThis action is permanent and cannot be undone.`}
          confirmLabel={confirmAction.label}
          confirmVariant="danger"
        />
      )}
    </div>
  );
}
