"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CreateCaseRequestDto } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { CreateMedicalCodeDto } from "@/lib/cases/cases.types";

interface CreateMedicalCodeProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const INITIAL_FORM = {
  code: "",
  description: "",
};

export function CreateMedicalCode({
  open,
  onClose,
  onCreated,
}: CreateMedicalCodeProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.code.trim()) e.code = "Code number is required";
    if (!form.description.trim()) e.description = "Description is required";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: CreateMedicalCodeDto = {
        code: form.code.trim(),
        description: form.description.trim(),
      };
      await casesService.createMedicalCode(request);
      addToast({
        type: "success",
        title: "Code Created",
        description: `Medical Code has been created.`,
      });
      setForm({ ...INITIAL_FORM });
      setErrors({});
      onCreated?.();
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          setErrors({ code: "A code with this number already exists" });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    setErrors({});
    onClose();
  };

  return (
    <FormModal
      open={open}
      onClose={reset}
      onSubmit={handleSubmit}
      title="New Medical Code"
      submitLabel={submitting ? "Creating..." : "Create Medical Code"}
      submitDisabled={submitting || !form.code || !form.description}
    >
      <div className="space-y-4">
        <Field
          label="Code"
          required
          value={form.code}
          onChange={(v) => setForm({ ...form, code: v.toString() })}
          error={errors.code}
          placeholder=""
        />
        <Field
          label="Description"
          required
          value={form.description}
          onChange={(v) => setForm({ ...form, description: v.toString() })}
          error={errors.description}
          placeholder="Description"
          type="textarea"
        />
      </div>
    </FormModal>
  );
}
