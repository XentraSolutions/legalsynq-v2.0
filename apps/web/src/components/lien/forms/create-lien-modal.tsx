"use client";

import { useCallback, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { ApiError } from "@/lib/api-client";
import { liensService, type CreateLienRequestDto } from "@/lib/liens";
import Field from "../field";
import { CaseListItem, casesService } from "@/lib/cases";
import { DropdownOption } from "@/lib/lookup/lookup.types";
import { useSessionContext } from "@/providers/session-provider";

interface CreateLienModalProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const LIEN_TYPE_OPTIONS = [
  { value: "MedicalLien", label: "Medical Lien" },
  { value: "AttorneyLien", label: "Attorney Lien" },
  { value: "SettlementAdvance", label: "Settlement Advance" },
  { value: "WorkersCompLien", label: "Workers' Comp Lien" },
  { value: "PropertyLien", label: "Property Lien" },
  { value: "Other", label: "Other" },
];

export function CreateLienModal({
  open,
  onClose,
  onCreated,
}: CreateLienModalProps) {
  const { lookup } = useSessionContext();
  const [form, setForm] = useState({
    lienNumber: "",
    lienType: "",
    caseId: "",
    originalAmount: "",
    jurisdiction: "",
    subjectFirst: "",
    subjectLast: "",
    isConfidential: false,
    description: "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [cases, setCases] = useState<DropdownOption[]>([]);
  const state =
    lookup?.State?.map((c) => {
      return { key: c.id, value: c.code, label: c.code };
    }) ?? [];

  const lienTypes =
    lookup?.LienType?.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];
  const fetchData = useCallback(async () => {
    const casesRes = await casesService.getCases();

    setCases(
      casesRes.items.map((c) => {
        return { key: c.id, value: c.id, label: c.caseNumber };
      }) ?? [],
    );
  }, []);

  const validate = () => {
    const e: Record<string, string> = {};
    // if (!form.lienNumber.trim()) e.lienNumber = "Lien number is required";
    if (!form.lienType) e.lienType = "Lien type is required";
    if (
      !form.originalAmount ||
      isNaN(Number(form.originalAmount)) ||
      Number(form.originalAmount) <= 0
    )
      e.originalAmount = "Valid amount is required";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: CreateLienRequestDto = {
        lienNumber: form.lienNumber.trim(),
        lienType: form.lienType,
        caseId: form.caseId,
        originalAmount: Number(form.originalAmount),
        jurisdiction: form.jurisdiction || undefined,
        isConfidential: form.isConfidential,
        subjectFirstName: form.subjectFirst || undefined,
        subjectLastName: form.subjectLast || undefined,
        description: form.description || undefined,
      };
      await liensService.createLien(request);
      resetForm();
      setTimeout(() => {
        onCreated?.();
      }, 1000);
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to create lien";
      setErrors({ _form: message });
    } finally {
      setSubmitting(false);
    }
  };

  const resetForm = () => {
    setForm({
      lienNumber: "",
      lienType: "",
      caseId: "",
      originalAmount: "",
      jurisdiction: "",
      subjectFirst: "",
      subjectLast: "",
      isConfidential: false,
      description: "",
    });
    setErrors({});
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={handleClose}
      onSubmit={handleSubmit}
      title="Create Lien"
      subtitle="Add a new lien record"
      submitLabel={submitting ? "Creating..." : "Create Lien"}
      submitDisabled={submitting}
      size="lg"
    >
      <div className="space-y-4">
        {errors._form && (
          <div className="p-3 bg-red-50 border border-red-200 rounded-lg">
            <p className="text-sm text-red-700">{errors._form}</p>
          </div>
        )}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <Field
              label="Lien Type"
              required
              value={form.lienType}
              options={lienTypes}
              placeholder="Select one case"
              onChange={(v) =>
                setForm({
                  ...form,
                  lienType: v.toString(),
                })
              }
              onClick={() => fetchData()}
              type="select"
            />
          </div>
          <div>
            <Field
              label="Case"
              required
              value={form.caseId}
              options={cases}
              placeholder="Select one case"
              onChange={(v) =>
                setForm({
                  ...form,
                  caseId: v.toString(),
                })
              }
              onClick={() => fetchData()}
              type="select"
            />
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <Field
              label="Original Amount"
              required
              value={form.originalAmount}
              onChange={(v) =>
                setForm({ ...form, originalAmount: v.toString() })
              }
              placeholder="0.00"
              type="number"
              error={errors.originalAmount}
              prefix="$"
            />
          </div>
          <div>
            <Field
              label="Jurisdiction"
              required
              value={form.jurisdiction}
              options={state}
              onChange={(v) =>
                setForm({
                  ...form,
                  jurisdiction: v.toString(),
                })
              }
              type="select"
              placeholder="e.g. NV"
            />
          </div>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <Field
              label="Subject First Name"
              value={form.subjectFirst}
              onChange={(v) => setForm({ ...form, subjectFirst: v.toString() })}
              placeholder="First name"
            />
          </div>
          <div>
            <Field
              label="Subject Last Name"
              value={form.subjectLast}
              onChange={(v) => setForm({ ...form, subjectLast: v.toString() })}
              placeholder="Last name"
            />
          </div>
        </div>
        <div>
          <Field
            label="Description"
            value={form.description}
            onChange={(v) => setForm({ ...form, description: v.toString() })}
            placeholder="Optional description..."
            type="textarea"
          />
        </div>
        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="confidential"
            checked={form.isConfidential}
            onChange={(e) =>
              setForm({ ...form, isConfidential: e.target.checked })
            }
            className="rounded border-gray-300"
          />
          <label htmlFor="confidential" className="text-sm text-gray-600">
            Mark as confidential
          </label>
        </div>
      </div>
    </FormModal>
  );
}
