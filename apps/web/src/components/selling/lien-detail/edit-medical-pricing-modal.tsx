"use client";

import { useMemo, useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";
import type { MedicalPricingRowDetail } from "@/types/lien-selling";
import MedicalCodesDescription from "@/components/selling/forms/add-medical-lien/medical-codes-description";

interface EditMedicalPricingModalProps {
  lienId: string;
  rows: MedicalPricingRowDetail[];
  onClose: () => void;
  onSaved: () => void;
}

// Mirrors MedicalCodesStep (edit/step-3): same PricingRow shape (id, code,
// description, billingAmount, medicareCost, targetSaleAmount), same
// MedicalCodesDescription table/entry-form, same totals-from-rows submit
// logic. Kept as a modal instead of a wizard step for the lien detail page's
// "Edit Medical Code & Marketplace Pricing" action.
export function EditMedicalPricingModal({
  lienId,
  rows: initialRows,
  onClose,
  onSaved,
}: EditMedicalPricingModalProps) {
  const { show: showToast } = useToast();
  const initialCodeRows = useMemo(
    () =>
      initialRows.map((row) => {
        const parsed = parsePricingRow(row);
        return {
          id: row.id,
          code: parsed.medicalCode,
          description: parsed.description ?? "",
          billingAmount: parsed.billingAmount,
          medicareCost: parsed.medicareCost,
          targetSaleAmount: parsed.targetSaleAmount,
        };
      }),
    [initialRows],
  );
  const [formData, setFormData] = useState<any>({ codeRows: initialCodeRows });
  const [formValid, setFormValid] = useState(initialCodeRows.length > 0);
  const [saving, setSaving] = useState(false);

  function onFormValid(isValid: boolean, data?: any) {
    setFormValid(!!isValid);
    if (data !== undefined) setFormData(data);
  }

  const handleSubmit = async () => {
    setSaving(true);
    try {
      const rows = formData?.codeRows ?? [];
      const askAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.targetSaleAmount || 0),
        0,
      );
      const totalBillingAmount = rows.reduce(
        (sum: number, row: any) => sum + (row.billingAmount || 0),
        0,
      );

      await liensService.saveMedicalPricing(lienId, {
        askAmount,
        billingAmount: totalBillingAmount,
        rows: rows.map((row: any) => ({
          medicalCode: row.code,
          description: row.description || undefined,
          billingAmount: row.billingAmount,
          medicareCost: row.medicareCost,
          targetSaleAmount: row.targetSaleAmount,
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
      submitDisabled={!formValid}
      loading={saving}
      size="lg"
    >
      <MedicalCodesDescription
        embedded
        lienId={lienId}
        data={formData}
        onFormValid={onFormValid}
      />
    </FormModal>
  );
}
