"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect } from "@/components/ui/base-select";
import { useSessionContext } from "@/providers/session-provider";
import { formatPhoneInput, isValidPhone } from "@/lib/phone";
import { formatZipInput, isValidUsZipCode } from "@/lib/address";
import { useCompanyTypes, useCreateCompany, useUpdateCompany } from "@/hooks/selling/use-selling-companies";
import type { Company } from "@/lib/selling/companies.types";

interface CompanyFormModalProps {
  open: boolean;
  title: string;
  companyTypeId: string;
  /** Locks the Company Type field to `companyTypeId` (e.g. when created from an entity-scoped picker). */
  lockCompanyType?: boolean;
  /** Present in edit mode; the company being edited. */
  editTarget?: Company | null;
  onClose: () => void;
  onSaved: (company: Company) => void;
}

const EMPTY_FORM = {
  companyTypeId: "",
  name: "",
  addressLine1: "",
  city: "",
  state: "",
  postalCode: "",
  phone: "",
  email: "",
};

type ValidatableField = "companyTypeId" | "name" | "email" | "phone" | "postalCode";
const FIELDS: readonly ValidatableField[] = ["companyTypeId", "name", "email", "phone", "postalCode"];

function formFromCompany(company: Company) {
  return {
    companyTypeId: company.companyTypeId,
    name: company.name,
    addressLine1: company.addressLine1 ?? "",
    city: company.city ?? "",
    state: company.state ?? "",
    postalCode: company.postalCode ?? "",
    phone: company.phone ?? "",
    email: company.email ?? "",
  };
}

export function CompanyFormModal({
  open,
  title,
  companyTypeId,
  lockCompanyType,
  editTarget,
  onClose,
  onSaved,
}: CompanyFormModalProps) {
  const { lookup } = useSessionContext();
  const isEdit = Boolean(editTarget);
  const [form, setForm] = useState(
    editTarget ? formFromCompany(editTarget) : { ...EMPTY_FORM, companyTypeId },
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const createCompany = useCreateCompany();
  const updateCompany = useUpdateCompany();
  const companyTypesQuery = useCompanyTypes();
  const submitting = createCompany.isPending || updateCompany.isPending;

  const states =
    lookup?.State?.map((s) => ({ value: s.code, label: `${s.name} (${s.code})` })) ?? [];

  useEffect(() => {
    if (!open) return;
    setForm(editTarget ? formFromCompany(editTarget) : { ...EMPTY_FORM, companyTypeId });
    setErrors({});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, editTarget, companyTypeId]);

  const inputCls = (field: string) =>
    `w-full border rounded-lg px-3 py-2 text-sm ${errors[field] ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`;

  const validateField = (
    field: ValidatableField,
    f: typeof form,
  ): string | undefined => {
    switch (field) {
      case "companyTypeId":
        return f.companyTypeId ? undefined : "Company type is required";
      case "name":
        return f.name.trim() ? undefined : "Name is required";
      case "email":
        return f.email && !/^\S+@\S+\.\S+$/.test(f.email)
          ? "Invalid email format"
          : undefined;
      case "phone":
        return f.phone && !isValidPhone(f.phone)
          ? "Phone number must be 10 digits"
          : undefined;
      case "postalCode":
        return f.postalCode && !isValidUsZipCode(f.postalCode)
          ? "Zip code must be 5 or 9 digits (XXXXX or XXXXX-XXXX)"
          : undefined;
    }
  };

  const validate = () => {
    const e: Record<string, string> = {};
    for (const field of FIELDS) {
      const msg = validateField(field, form);
      if (msg) e[field] = msg;
    }
    setErrors(e);
    return Object.keys(e).length === 0;
  };

  // Revalidates a field the moment it changes — required-but-untouched fields
  // still only surface their error on submit (see `validate`), while edited
  // fields update live.
  const setField = <K extends keyof typeof EMPTY_FORM>(field: K, value: string) => {
    const next = { ...form, [field]: value };
    setForm(next);
    setErrors((e) => {
      const isValidatedField = (FIELDS as readonly string[]).includes(field);
      const msg = isValidatedField
        ? validateField(field as ValidatableField, next)
        : undefined;
      if (!msg) {
        if (!e[field]) return e;
        const { [field]: _removed, ...rest } = e;
        return rest;
      }
      return { ...e, [field]: msg };
    });
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    const request = {
      name: form.name.trim(),
      addressLine1: form.addressLine1.trim() || undefined,
      city: form.city.trim() || undefined,
      state: form.state || undefined,
      postalCode: form.postalCode.trim() || undefined,
      phone: form.phone.trim() || undefined,
      email: form.email.trim() || undefined,
    };
    try {
      const company = isEdit
        ? await updateCompany.mutateAsync({ id: editTarget!.id, request })
        : await createCompany.mutateAsync({ ...request, companyTypeId: form.companyTypeId });
      toast.success(isEdit ? "Company updated" : "Company created", { description: company.name });
      onSaved(company);
    } catch (err) {
      toast.error(isEdit ? "Couldn't update company" : "Couldn't create company", {
        description: err instanceof Error ? err.message : undefined,
      });
    }
  };

  return (
    <FormModal
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title={title}
      subtitle={isEdit ? "Update the company information." : "Provide the required information to add a new company."}
      submitLabel={submitting ? "Saving..." : isEdit ? "Save Changes" : "Create"}
      loading={submitting}
    >
      <div className="space-y-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Company Type<span className="text-red-500 ml-0.5">*</span>
            </label>
            <BaseSelect
              value={form.companyTypeId}
              onChange={(v) => setField("companyTypeId", v)}
              options={companyTypesQuery.options}
              placeholder="Select type"
              disabled={isEdit || lockCompanyType}
            />
            {errors.companyTypeId && (
              <p className="text-xs text-red-500 mt-1">{errors.companyTypeId}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Name<span className="text-red-500 ml-0.5">*</span>
            </label>
            <input
              type="text"
              value={form.name}
              onChange={(e) => setField("name", e.target.value)}
              className={inputCls("name")}
            />
            {errors.name && <p className="text-xs text-red-500 mt-1">{errors.name}</p>}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Email Address</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => setField("email", e.target.value)}
              placeholder="email@example.com"
              className={inputCls("email")}
            />
            {errors.email && <p className="text-xs text-red-500 mt-1">{errors.email}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Phone Number</label>
            <input
              type="text"
              value={form.phone}
              onChange={(e) => setField("phone", formatPhoneInput(e.target.value))}
              placeholder="(555) 555-0000"
              className={inputCls("phone")}
            />
            {errors.phone && <p className="text-xs text-red-500 mt-1">{errors.phone}</p>}
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Address</label>
            <input
              type="text"
              value={form.addressLine1}
              onChange={(e) => setField("addressLine1", e.target.value)}
              className={inputCls("addressLine1")}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">City</label>
            <input
              type="text"
              value={form.city}
              onChange={(e) => setField("city", e.target.value)}
              className={inputCls("city")}
            />
          </div>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">State</label>
            <BaseSelect
              value={form.state}
              onChange={(v) => setField("state", v)}
              options={states}
              placeholder="Select state"
              clearable
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">ZIP Code</label>
            <input
              type="text"
              value={form.postalCode}
              onChange={(e) => setField("postalCode", formatZipInput(e.target.value))}
              placeholder="Enter ZIP code"
              className={inputCls("postalCode")}
            />
            {errors.postalCode && <p className="text-xs text-red-500 mt-1">{errors.postalCode}</p>}
          </div>
        </div>
      </div>
    </FormModal>
  );
}
