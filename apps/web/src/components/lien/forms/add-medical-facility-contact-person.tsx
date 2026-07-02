"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CreateCaseRequestDto } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { facilityService } from "@/lib/facility";
import {
  ContactPersonRequest,
  CreateFacilityRequest,
} from "@/lib/facility/facility.types";
import { useSessionContext } from "@/providers/session-provider";

interface CreateMedicalFacilityContactPersonProps {
  data: any;
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const INITIAL_FORM = {
  firstname: "",
  lastname: "",
  email: "",
  facilityName: "",
  facilityId: "",
};

export function CreateMedicalFacilityContactPerson({
  data,
  open,
  onClose,
  onCreated,
}: CreateMedicalFacilityContactPersonProps) {
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({ ...INITIAL_FORM });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const facilities = data;
  const validate = () => {
    const e: Record<string, string> = {};
    if (!form.firstname.trim()) e.name = "First Name is required";
    if (!form.lastname.trim()) e.name = "Last Name is required";
    if (!form.email.trim()) e.name = "Email Address is required";
    if (!form.facilityId.trim()) e.name = "Facility is required";

    setErrors(e);
    return Object.keys(e).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      const request: ContactPersonRequest = {
        firstname: form.firstname.trim(),
        lastname: form.lastname.trim(),
        email: form.email.trim(),
        facilityId: form.facilityId,
      };
      await facilityService.contactPerson(request);
      addToast({
        type: "success",
        title: "Contact Person Created",
        description: `Contact Person has been created.`,
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

  const getFacilityName = (id) => {
    return data.find((f) => f.value == id).label;
  };

  return (
    <FormModal
      open={open}
      onClose={reset}
      onSubmit={handleSubmit}
      title="New Contact Person"
      submitLabel={submitting ? "Creating..." : "Save"}
      submitDisabled={
        submitting ||
        !form.firstname ||
        !form.lastname ||
        !form.email ||
        !form.facilityId
      }
    >
      <div className="space-y-4">
        <Field
          label="First Name"
          required
          value={form.firstname}
          onChange={(v) => setForm({ ...form, firstname: v.toString() })}
          error={errors.firstname}
          placeholder=""
        />
        <Field
          label="Last Name"
          required
          value={form.lastname}
          onChange={(v) => setForm({ ...form, lastname: v.toString() })}
          error={errors.lastname}
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
          label="Facility"
          value={form.facilityName}
          options={facilities}
          onChange={(v) =>
            setForm({
              ...form,
              facilityId: v.toString(),
              facilityName: getFacilityName(v),
            })
          }
          error={errors.facilityId}
          placeholder=""
          type="select"
        />
      </div>
    </FormModal>
  );
}
