"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect } from "@/components/ui/base-select";
import { useContactPersons, useReassignContactPerson } from "@/hooks/selling/use-selling-companies";
import type { ContactPerson } from "@/lib/selling/companies.types";

interface ReassignContactPersonModalProps {
  open: boolean;
  companyId: string;
  contactPerson: ContactPerson;
  onClose: () => void;
  onReassigned?: () => void;
}

export function ReassignContactPersonModal({
  open,
  companyId,
  contactPerson,
  onClose,
  onReassigned,
}: ReassignContactPersonModalProps) {
  const [targetContactPersonId, setTargetContactPersonId] = useState("");

  const contactPersonsQuery = useContactPersons(companyId, true, { enabled: open });
  const reassignMutation = useReassignContactPerson();

  useEffect(() => {
    if (!open) setTargetContactPersonId("");
  }, [open]);

  // Never offer the contact person itself as a reassign target.
  const options = (contactPersonsQuery.data ?? [])
    .filter((c) => c.id !== contactPerson.id)
    .map((c) => ({ value: c.id, label: c.displayName }));

  const handleSubmit = async () => {
    if (!targetContactPersonId) return;
    try {
      await reassignMutation.mutateAsync({
        companyId,
        contactId: contactPerson.id,
        request: { targetContactPersonId },
      });
      toast.success("Contact person reassigned", { description: contactPerson.displayName });
      onReassigned?.();
      onClose();
    } catch (err) {
      toast.error("Couldn't reassign contact person", {
        description: err instanceof Error ? err.message : contactPerson.displayName,
      });
    }
  };

  return (
    <FormModal
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Reassign Contact Person"
      subtitle={`Move everything associated with ${contactPerson.displayName} to another contact person.`}
      submitLabel="Reassign"
      submitDisabled={!targetContactPersonId}
      loading={reassignMutation.isPending}
    >
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Target Contact Person<span className="text-red-500 ml-0.5">*</span>
        </label>
        <BaseSelect
          value={targetContactPersonId}
          onChange={setTargetContactPersonId}
          options={options}
          isLoading={contactPersonsQuery.isLoading}
          placeholder="Select a contact person"
          searchPlaceholder="Search contact persons..."
          emptyText={contactPersonsQuery.isLoading ? "Loading..." : "No contact persons found."}
        />
      </div>
    </FormModal>
  );
}
