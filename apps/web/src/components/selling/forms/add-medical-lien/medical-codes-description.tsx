import React, { useEffect, useMemo, useState } from "react";
import { Info } from "lucide-react";
import { BaseSelect } from "@/components/ui/base-select";
import { CreateMedicalCode } from "../add-medical-code";
import {
  useMedicareCosts,
  useMedicareProcedureCodes,
} from "@/hooks/use-case-liens";
import { Input } from "@legalsynq/design-system";
import Field from "@/components/lien/field";
import { Button } from "@/components/selling/button";

export interface MedicalCodesDescriptionProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  // Set when rendered inside a modal that already shows its own title (e.g.
  // EditMedicalPricingModal) so this doesn't duplicate the heading.
  embedded?: boolean;
}

interface PricingRow {
  id: string;
  code: string;
  description: string;
  billingAmount: number;
  medicareCost: number;
  targetSaleAmount: number;
}

const INITIAL_ENTRY = {
  procedureCode: "",
  billingAmount: "",
  targetAmount: "",
  targetPercent: "",
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
  const parsed = Number(value.replace(/,/g, ""));
  return Number.isFinite(parsed) ? parsed : 0;
}

export default function MedicalCodesDescription(
  props: MedicalCodesDescriptionProps,
) {
  const { data = {}, onFormValid, embedded = false } = props;
  const { data: medicalCodes, isLoading: isLoadingMedicalCodes } =
    useMedicareProcedureCodes();

  const [entry, setEntry] = useState({ ...INITIAL_ENTRY });
  const { data: medicareCost } = useMedicareCosts(entry.procedureCode);

  const [rows, setRows] = useState<PricingRow[]>(data?.codeRows ?? []);
  const [showCreate, setShowCreate] = useState(false);

  const totals = useMemo(
    () =>
      rows.reduce(
        (tot, row) => ({
          billing: tot.billing + row.billingAmount,
          target: tot.target + row.targetSaleAmount,
        }),
        { billing: 0, target: 0 },
      ),
    [rows],
  );

  useEffect(() => {
    onFormValid?.(rows.length > 0, { codeRows: rows });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [rows]);

  const billingAmount = parseNumber(entry.billingAmount);

  function handleBillingAmountChange(raw: string) {
    const newBilling = parseNumber(raw);
    setEntry((prev) => {
      if (prev.targetPercent) {
        return {
          ...prev,
          billingAmount: raw,
          targetAmount: newBilling
            ? roundToTwo((parseNumber(prev.targetPercent) / 100) * newBilling).toString()
            : prev.targetAmount,
        };
      }
      if (prev.targetAmount) {
        return {
          ...prev,
          billingAmount: raw,
          targetPercent: newBilling
            ? roundToTwo((parseNumber(prev.targetAmount) / newBilling) * 100).toString()
            : prev.targetPercent,
        };
      }
      return { ...prev, billingAmount: raw };
    });
  }

  function handleTargetAmountChange(raw: string) {
    setEntry((prev) => ({
      ...prev,
      targetAmount: raw,
      targetPercent: billingAmount
        ? roundToTwo((parseNumber(raw) / billingAmount) * 100).toString()
        : prev.targetPercent,
    }));
  }

  function handleTargetPercentChange(raw: string) {
    setEntry((prev) => ({
      ...prev,
      targetPercent: raw,
      targetAmount: billingAmount
        ? roundToTwo((parseNumber(raw) / 100) * billingAmount).toString()
        : prev.targetAmount,
    }));
  }

  const isEntryValid =
    !!entry.procedureCode && billingAmount > 0 && parseNumber(entry.targetAmount) > 0;

  function handleAddRow() {
    const selectedOption = medicalCodes?.find(
      (option) => option.value === entry.procedureCode,
    );
    const nextRow: PricingRow = {
      id: crypto.randomUUID(),
      code: entry.procedureCode,
      description: selectedOption?.label ?? "",
      billingAmount,
      medicareCost: parseNumber(medicareCost ?? ""),
      targetSaleAmount: parseNumber(entry.targetAmount),
    };
    setRows((current) => [...current, nextRow]);
    setEntry({ ...INITIAL_ENTRY });
  }

  function handleDeleteRow(id: string) {
    setRows((current) => current.filter((row) => row.id !== id));
  }

  return (
    <div className="container-fluid">
      <div className="col-12 mb-2">
        {!embedded && (
          <>
            <span className="font-semibold mb-2 text-2xl mt-1">
              Medical Code & Marketplace Pricing
            </span>
            <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
              Provide the necessary medical code and marketplace pricing
              information to support lien valuation and processing.
            </p>
          </>
        )}

        <div className="grid grid-cols-1 mt-4">
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Medical Code & Description
            <span className="text-red-500 ml-0.5">*</span>
          </label>
          <BaseSelect
            value={entry.procedureCode}
            onChange={(v) => setEntry({ ...entry, procedureCode: v })}
            options={medicalCodes ?? []}
            isLoading={isLoadingMedicalCodes}
            placeholder="Select medical code & description"
            searchPlaceholder="Search codes..."
            createAction={{
              label: "Add New Medical Code",
              onSelect: () => setShowCreate(true),
            }}
          />
        </div>

        <div className="grid grid-cols-1 mt-4">
          <Field
            type="number"
            label="Billing Amount (Face Value)"
            required
            prefix="$"
            maxDecimals={2}
            value={entry.billingAmount}
            onChange={handleBillingAmountChange}
          />
        </div>

        <div className="grid grid-cols-1 mt-4">
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Target Ask Amount/Percentage
            <span className="text-red-500 ml-0.5">*</span>
          </label>
          <div className="flex gap-3 items-start">
            <div className="flex-1">
              <Field
                type="number"
                label=""
                prefix="$"
                maxDecimals={2}
                value={entry.targetAmount}
                onChange={handleTargetAmountChange}
              />
            </div>
            <div className="flex-1">
              <Field
                type="number"
                label=""
                prefix="%"
                value={entry.targetPercent}
                onChange={handleTargetPercentChange}
              />
            </div>
            <Button
              type="button"
              variant="secondary"
              className="shrink-0"
              disabled={!isEntryValid}
              rightIcon="plus"
              onClick={handleAddRow}
            >
              Add
            </Button>
          </div>
        </div>

        <div className="col-12 mt-5">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Code/Description
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Billing Amount
                  </th>
                  <th className="px-4 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Target Ask Amount
                  </th>
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
                      {formatCurrency(row.billingAmount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(row.targetSaleAmount)}
                    </td>
                    <td className="px-4 py-3 text-sm text-center">
                      <Button
                        type="button"
                        variant="icon-square"
                        icon="trash2"
                        onClick={() => handleDeleteRow(row.id)}
                      />
                    </td>
                  </tr>
                ))}
                {rows.length === 0 && (
                  <tr>
                    <td
                      colSpan={4}
                      className="px-4 py-6 text-center text-sm text-gray-500"
                    >
                      <Info className="inline h-4 w-4 mr-1.5" />
                      No record added yet
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
                      {formatCurrency(totals.billing)}
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-700 text-right">
                      {formatCurrency(totals.target)}
                    </td>
                    <td className="px-4 py-3" />
                  </tr>
                </tfoot>
              )}
            </table>
          </div>
        </div>
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
    </div>
  );
}
