"use client";

import { useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { useCreateContactPersonType } from "@/hooks/use-selling-companies";
import type { ContactPersonTypeLookupItem } from "@/lib/selling/companies.types";

interface AddContactPersonRoleModalProps {
  open: boolean;
  companyTypeId: string;
  /** sortOrder to assign to the new role — the caller computes this from the last known sortOrder. */
  nextSortOrder: number;
  onClose: () => void;
  onCreated: (role: ContactPersonTypeLookupItem) => void;
}

// Role codes are derived from the name, not entered separately — e.g. "Sample Type" -> "SampleType".
function codeFromName(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join("");
}

export function AddContactPersonRoleModal({
  open,
  companyTypeId,
  nextSortOrder,
  onClose,
  onCreated,
}: AddContactPersonRoleModalProps) {
  const [name, setName] = useState("");
  const [error, setError] = useState<string | undefined>();
  const createContactPersonType = useCreateContactPersonType();

  const reset = () => {
    setName("");
    setError(undefined);
    onClose();
  };

  const handleSubmit = async () => {
    const trimmed = name.trim();
    if (!trimmed) {
      setError("Role name is required");
      return;
    }
    try {
      const role = await createContactPersonType.mutateAsync({
        companyTypeId,
        code: codeFromName(trimmed),
        name: trimmed,
        sortOrder: nextSortOrder,
      });
      toast.success("Role created", { description: role.name });
      setName("");
      setError(undefined);
      onCreated(role);
    } catch (err) {
      toast.error("Couldn't create role", {
        description: err instanceof Error ? err.message : undefined,
      });
    }
  };

  return (
    <FormModal
      open={open}
      onClose={reset}
      onSubmit={handleSubmit}
      title="Add New Role"
      subtitle="Provide the required information to add a new contact person role."
      submitLabel={createContactPersonType.isPending ? "Saving..." : "Save"}
      cancelLabel="Back"
      loading={createContactPersonType.isPending}
      size="sm"
    >
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Role Name<span className="text-red-500 ml-0.5">*</span>
        </label>
        <input
          type="text"
          autoFocus
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            if (error) setError(undefined);
          }}
          placeholder="Enter role name"
          className={`w-full border rounded-lg px-3 py-2 text-sm ${error ? "border-red-300" : "border-gray-200"} focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary`}
        />
        {error && <p className="text-xs text-red-500 mt-1">{error}</p>}
      </div>
    </FormModal>
  );
}
