"use client";

import { Briefcase } from "lucide-react";
import { Modal } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";

export function NewCaseAddedModal({
  open,
  caseNumber,
  onClose,
  onAddLien,
}: {
  open: boolean;
  caseNumber?: string;
  onClose: () => void;
  onAddLien: () => void;
}) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="New Case Added"
      size="sm"
      icon={
        <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-gray-100">
          <Briefcase className="h-4 w-4 text-gray-600" />
        </span>
      }
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Maybe Later
          </Button>
          <Button variant="primary" onClick={onAddLien}>
            Add Lien
          </Button>
        </>
      }
    >
      <p className="text-sm text-gray-600">
        {caseNumber ? (
          <>
            Case <span className="font-medium text-gray-900">{caseNumber}</span> has
            been added.
          </>
        ) : (
          "The new case has been added."
        )}{" "}
        Would you like to add lien to this case? You can always do this later.
      </p>
    </Modal>
  );
}
