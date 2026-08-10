"use client";

import { useEffect, useMemo, useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect } from "@/components/ui/base-select";
import Field from "@/components/lien/field";
import { liensService } from "@/lib/selling";
import { useMedicareProcedureCodes, useMedicareCosts } from "@/hooks/use-case-liens";
import { useToast } from "@/lib/toast-context";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";
import type { MedicalPricingRowDetail } from "@/types/lien-selling";
import type { SellingMedicalPricingRowRequest } from "@/lib/selling/liens.types";
import { Button } from "@/components/ui/button";

type PricingRow = SellingMedicalPricingRowRequest & { key: string };

function emptyRow(): PricingRow {
  return {
    key: crypto.randomUUID(),
    medicalCode: "",
    description: "",
    serviceDate: "",
    billingAmount: 0,
    medicareCost: 0,
    targetSaleAmount: 0,
  };
}

function PricingRowFields({
  row,
  onChange,
  onRemove,
}: {
  row: PricingRow;
  onChange: (patch: Partial<PricingRow>) => void;
  onRemove: () => void;
}) {
  // Same lookup as the add-medical-lien wizard (useMedicareProcedureCodes /
  // useMedicareCosts) — eager-loaded, client-filtered code list plus a
  // reactive cost fetch keyed off the selected code.
  const { data: medicalCodes, isLoading: isLoadingMedicalCodes } =
    useMedicareProcedureCodes();
  const { data: medicareCost } = useMedicareCosts(row.medicalCode);

  useEffect(() => {
    if (row.medicalCode) onChange({ medicareCost: Number(medicareCost) || 0 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [medicareCost]);

  return (
    <tr>
      <td className="py-2 px-3 max-w-64">
        <BaseSelect
          value={row.medicalCode || null}
          onChange={(v, option) => {
            onChange({
              medicalCode: v,
              description: option.label,
            });
          }}
          options={medicalCodes ?? []}
          isLoading={isLoadingMedicalCodes}
          placeholder="Select code"
          searchPlaceholder="Search codes..."
          className="w-full"
        />
      </td>
      <td className="py-2 px-3 w-20">
        <Field
          type="number"
          label=""
          value={row.billingAmount || ""}
          onChange={(v) => onChange({ billingAmount: Number(v) || 0 })}
        />
      </td>
      <td className="py-2 px-3 w-20">
        <Field
          type="number"
          label=""
          value={row.medicareCost || ""}
          onChange={(v) => onChange({ medicareCost: Number(v) || 0 })}
        />
      </td>
      <td className="py-2 px-3 w-20">
        <Field
          type="number"
          label=""
          value={row.targetSaleAmount || ""}
          onChange={(v) => onChange({ targetSaleAmount: Number(v) || 0 })}
        />
      </td>
      <td className="text-center">
        <Button
          type="button"
          variant="ghost"
          className="w-7 h-7 p-0 text-gray-300 hover:text-red-500"
          onClick={onRemove}
          aria-label="Remove row"
        >
          <i className="ri-close-line" />
        </Button>
      </td>
    </tr>
  );
}

interface EditMedicalPricingModalProps {
  lienId: string;
  rows: MedicalPricingRowDetail[];
  onClose: () => void;
  onSaved: () => void;
}

export function EditMedicalPricingModal({
  lienId,
  rows: initialRows,
  onClose,
  onSaved,
}: EditMedicalPricingModalProps) {
  const { show: showToast } = useToast();
  const [rows, setRows] = useState<PricingRow[]>(() =>
    initialRows.length > 0
      ? [
          ...initialRows.map((row) => {
            const data = parsePricingRow(row);
            return {
              key: row.id,
              ...data,
              description: data.description ?? undefined,
              serviceDate: data.serviceDate ?? undefined,
            };
          }),
          emptyRow(),
        ]
      : [emptyRow()],
  );
  const [saving, setSaving] = useState(false);

  const totalBillingAmount = useMemo(
    () => rows.reduce((sum, r) => sum + (Number(r.billingAmount) || 0), 0),
    [rows],
  );
  const askAmount = useMemo(
    () => rows.reduce((sum, r) => sum + (Number(r.targetSaleAmount) || 0), 0),
    [rows],
  );

  const updateRow = (key: string, patch: Partial<PricingRow>) => {
    setRows((prev) => {
      const next = prev.map((r) => (r.key === key ? { ...r, ...patch } : r));
      const isLastRow = prev[prev.length - 1]?.key === key;
      const filledCode = patch.medicalCode?.trim();
      // Filling the code on the last row implies another line item may
      // follow — grow the table instead of requiring an explicit "Add Row".
      if (isLastRow && filledCode) {
        next.push(emptyRow());
      }
      return next;
    });
  };
  const removeRow = (key: string) => {
    setRows((prev) =>
      prev.length > 1 ? prev.filter((r) => r.key !== key) : prev,
    );
  };

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveMedicalPricing(lienId, {
        askAmount: askAmount || undefined,
        billingAmount: totalBillingAmount || undefined,
        rows: rows
          .filter((r) => r.medicalCode.trim())
          .map(({ key, description, serviceDate, ...rest }) => ({
            ...rest,
            medicalCode: rest.medicalCode.trim(),
            description: description?.trim() || undefined,
            serviceDate: serviceDate?.trim() || undefined,
          })),
      });
      showToast("Medical code & marketplace pricing updated.", "success");
      onSaved();
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to save pricing",
        "error",
      );
    } finally {
      setSaving(false);
    }
  };

  return (
    <FormModal
      open
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Edit Medical Code & Marketplace Pricing"
      submitLabel={saving ? "Saving..." : "Save"}
      loading={saving}
      size="lg"
    >
      <div className="space-y-4">
        <div className="overflow-x-auto border border-gray-200 rounded-lg">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-left text-[11px] text-gray-400 uppercase tracking-wide">
                <th className="py-2 px-3 max-w-64">Code / Description</th>
                <th className="py-2 px-3 text-right">Billing</th>
                <th className="py-2 px-3 text-right">Medicare</th>
                <th className="py-2 px-3 text-right">Target Sale</th>
                <th className="py-2 w-8">
                  <span className="sr-only">Remove</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {rows.map((row) => (
                <PricingRowFields
                  key={row.key}
                  row={row}
                  onChange={(patch) => updateRow(row.key, patch)}
                  onRemove={() => removeRow(row.key)}
                />
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex justify-end">
          <div className="w-40">
            <Field
              type="number"
              label="Total Ask Amount"
              required
              disabled
              prefix="$"
              value={askAmount}
              onChange={() => {}}
            />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
