"use client";

import { useEffect, useMemo, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { liensService } from "@/lib/selling";
import { lookupService } from "@/lib/lookup";
import { useToast } from "@/lib/toast-context";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";
import type { MedicalPricingRowDetail } from "@/types/lien-selling";
import type { SellingMedicalPricingRowRequest } from "@/lib/selling/liens.types";

// Mirrors findMedicareCost/useMedicareCosts in use-case-liens.ts (add-medical-lien
// wizard) — same lookup, used here to auto-fill Medicare Cost on code select.
async function findMedicareCost(code: string): Promise<number> {
  if (!code) return 0;
  try {
    const cost = await lookupService.getMedicalProcedureCosts(code);
    return Number(cost?.total) || 0;
  } catch {
    return 0;
  }
}

type MedicalCodeOption = BaseSelectOption & { description: string };

// Server-side search, debounced — mirrors the pattern in
// contact-entity-select.tsx (no shared useDebounce hook in this codebase).
function useMedicalCodeOptions(search: string) {
  const [debouncedSearch, setDebouncedSearch] = useState(search);
  const [options, setOptions] = useState<MedicalCodeOption[]>([]);
  const [isSearching, setIsSearching] = useState(false);

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setIsSearching(true);
    liensService
      .getMedicalCodes(debouncedSearch)
      .then((items) => {
        if (cancelled) return;
        setOptions(
          items.map((item) => ({
            value: item.code,
            // Matches the lien product's "Medical Code & Description" combined
            // display (medical-codes-information-panel.tsx / add-medical-lien).
            label: item.description
              ? `${item.code} — ${item.description}`
              : item.code,
            description: item.description,
          })),
        );
      })
      .finally(() => {
        if (!cancelled) setIsSearching(false);
      });
    return () => {
      cancelled = true;
    };
  }, [debouncedSearch]);

  return { options, isSearching };
}

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
  const [search, setSearch] = useState(row.medicalCode);
  const { options, isSearching } = useMedicalCodeOptions(search);

  return (
    <tr>
      <td className="py-2 px-3 max-w-64">
        <BaseSelect
          value={row.medicalCode || null}
          onChange={(_, option) => {
            onChange({
              medicalCode: option.value,
              description: option.description,
            });
            findMedicareCost(option.value).then((medicareCost) =>
              onChange({ medicareCost }),
            );
          }}
          options={options}
          search={search}
          onSearchChange={setSearch}
          filterLocally={false}
          isSearching={isSearching}
          placeholder="Select code"
          searchPlaceholder="Search codes..."
          className="w-full"
        />
      </td>
      <td className="py-2 px-3">
        <input
          type="number"
          value={row.billingAmount || ""}
          onChange={(e) =>
            onChange({ billingAmount: Number(e.target.value) || 0 })
          }
          className="w-20 border border-gray-200 rounded px-2 py-1 text-sm text-right"
        />
      </td>
      <td className="py-2 px-3">
        <input
          type="number"
          value={row.medicareCost || ""}
          onChange={(e) =>
            onChange({ medicareCost: Number(e.target.value) || 0 })
          }
          className="w-20 border border-gray-200 rounded px-2 py-1 text-sm text-right"
        />
      </td>
      <td className="py-2 px-3">
        <input
          type="number"
          value={row.targetSaleAmount || ""}
          onChange={(e) =>
            onChange({ targetSaleAmount: Number(e.target.value) || 0 })
          }
          className="w-20 border border-gray-200 rounded px-2 py-1 text-sm text-right"
        />
      </td>
      <td className="text-center">
        <button
          type="button"
          onClick={onRemove}
          aria-label="Remove row"
          className="text-gray-300 hover:text-red-500"
        >
          <i className="ri-close-line" />
        </button>
      </td>
    </tr>
  );
}

interface EditMedicalPricingModalProps {
  lienId: string;
  rows: MedicalPricingRowDetail[];
  askAmount: number | null;
  onClose: () => void;
  onSaved: () => void;
}

export function EditMedicalPricingModal({
  lienId,
  rows: initialRows,
  askAmount: initialAskAmount,
  onClose,
  onSaved,
}: EditMedicalPricingModalProps) {
  const { show: showToast } = useToast();
  const [rows, setRows] = useState<PricingRow[]>(() =>
    initialRows.length > 0
      ? initialRows.map((row) => {
          const data = parsePricingRow(row);
          return {
            key: row.id,
            ...data,
            description: data.description ?? undefined,
            serviceDate: data.serviceDate ?? undefined,
          };
        })
      : [emptyRow()],
  );
  const [askAmount, setAskAmount] = useState(
    initialAskAmount ? String(initialAskAmount) : "",
  );
  const [saving, setSaving] = useState(false);

  const totalBillingAmount = useMemo(
    () => rows.reduce((sum, r) => sum + (Number(r.billingAmount) || 0), 0),
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
        askAmount: Number(askAmount) || undefined,
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
        <div className="flex items-center justify-end gap-3">
          <label className="text-sm font-medium text-gray-700">
            Total Ask Amount<span className="text-red-500 ml-0.5">*</span>
          </label>
          <div className="relative w-40">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-sm text-gray-400">
              $
            </span>
            <input
              type="number"
              value={askAmount}
              onChange={(e) => setAskAmount(e.target.value)}
              className="w-full border border-gray-200 rounded-lg pl-7 pr-3 py-2 text-sm text-right focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>
        </div>
      </div>
    </FormModal>
  );
}
