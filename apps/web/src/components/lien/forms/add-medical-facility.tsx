"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CreateCaseRequestDto } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { facilityService } from "@/lib/facility";
import { CreateFacilityRequest } from "@/lib/facility/facility.types";
import { useSessionContext } from "@/providers/session-provider";

interface CreateMedicalFacilityProps {
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const INITIAL_FORM = {
  name: "",
  code: "",
  address: "",
  city: "",
  state: "",
  zipcode: "",
  email: "",
};

export function CreateMedicalFacility({
  open,
  onClose,
  onCreated,
}: CreateMedicalFacilityProps) {
  const addToast = useLienStore((s) => s.addToast);
  const { lookup } = useSessionContext();
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const states = lookup?.State.map((s) => {
    return {
      key: s.id,
      value: s.code,
      label: s.name,
    };
  });
  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.name.trim()) e.name = "Name is required";
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: CreateFacilityRequest = {
        name: form.name.trim(),
        email: form.email.trim(),
        address: form.email.trim(),
        city: form.city.trim(),
        state: form.state.trim(),
        zipcode: form.zipcode.trim(),
      };
      await facilityService.createFacility(request);
      addToast({
        type: "success",
        title: "Facility Created",
        description: `Medical Facility has been created.`,
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
      title="New Medical Facility"
      submitLabel={submitting ? "Creating..." : "Create Medical Facility"}
      submitDisabled={submitting || !form.name}
    >
      <div className="space-y-4">
        <Field
          label="Facility Name"
          required
          value={form.name}
          onChange={(v) => setForm({ ...form, name: v.toString() })}
          error={errors.name}
          placeholder=""
        />
        <Field
          label="Email Address"
          type="email"
          value={form.email}
          onChange={(v) => setForm({ ...form, email: v.toString() })}
          error={errors.email}
          placeholder=""
        />

        <Field
          label="Address"
          value={form.address}
          onChange={(v) => setForm({ ...form, address: v.toString() })}
          error={errors.address}
          placeholder=""
        />

        <Field
          label="City"
          value={form.city}
          onChange={(v) => setForm({ ...form, city: v.toString() })}
          error={errors.city}
          placeholder=""
        />
        <Field
          label="State"
          value={form.state}
          options={states}
          onChange={(v) => setForm({ ...form, state: v.toString() })}
          error={errors.state}
          placeholder=""
          type="select"
        />
        <Field
          label="Zipcode"
          value={form.zipcode}
          onChange={(v) => setForm({ ...form, zipcode: v.toString() })}
          error={errors.zipcode}
          placeholder=""
        />
      </div>
    </FormModal>
  );
}
