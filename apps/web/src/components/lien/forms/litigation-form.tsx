"use client";

import { useState } from "react";
import { FormModal } from "@/components/lien/modal";
import Field from "../field";
import { DropdownOption } from "@/lib/lookup/lookup.types";

interface LitigationStatusFormProps {
  open: boolean;
  onClose: () => void;
  onSubmitted: (value: DropdownOption) => void;
}

export function LitigationStatusForm({
  open,
  onClose,
  onSubmitted,
}: LitigationStatusFormProps) {
  const [isValid, setIsValid] = useState(false);
  const [data] = useState<{
    status: Array<{ key: string; value: string; label: string }>;
  }>({
    status: [
      {
        key: "7",
        value: "Litigation(Pending)",
        label: "Pending",
      },
      { key: "8", value: "Litigation(Open)", label: "Open" },
      { key: "9", value: "Litigation(Closed)", label: "Closed" },
    ],
  });
  const [status, setStatus] = useState<string>("");
  const handleSubmit = () => {
    const foundItem = data.status.find((s) => s.value == status);
    if (foundItem) onSubmitted(foundItem);
  };

  return (
    <>
      <FormModal
        open={open}
        onClose={onClose}
        onSubmit={handleSubmit}
        title="Litigation Status"
        subtitle=""
        submitLabel={"Select"}
        submitDisabled={!isValid}
      >
        <div className="space-y-4">
          <Field
            label="Status"
            required
            value={status}
            options={data?.status}
            placeholder="Please Select"
            onChange={(v: string) => {
              setStatus(v.toString());
              setIsValid(true);
            }}
            type="select"
          />
        </div>
      </FormModal>
    </>
  );
}
