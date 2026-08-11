"use client";

import { useState } from "react";
import { FormModal } from "@/components/selling/modal";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import type { LienCaseDetail, LienFundingCompanyDetail } from "@/types/lien-selling";

interface EditCaseInformationModalProps {
  lienId: string;
  fundingCompany: LienFundingCompanyDetail | null;
  caseInformation: LienCaseDetail | null;
  onClose: () => void;
  onSaved: () => void;
}

export function EditCaseInformationModal({
  lienId,
  fundingCompany,
  caseInformation,
  onClose,
  onSaved,
}: EditCaseInformationModalProps) {
  const { show: showToast } = useToast();
  const [fundingCompanyId, setFundingCompanyId] = useState(
    fundingCompany?.id ?? "",
  );
  const [fundingCompanyContactId, setFundingCompanyContactId] = useState(
    fundingCompany?.contact?.id ?? "",
  );
  const [lawFirmId, setLawFirmId] = useState(caseInformation?.lawFirmId ?? "");
  const [caseManagerId, setCaseManagerId] = useState(
    caseInformation?.caseManagerId ?? "",
  );
  const [saving, setSaving] = useState(false);

  const handleSubmit = async () => {
    setSaving(true);
    try {
      await liensService.saveCaseInformation(lienId, {
        fundingCompanyId: fundingCompanyId || undefined,
        fundingCompanyContactId: fundingCompanyContactId || undefined,
        handlingLawFirmId: lawFirmId || undefined,
        caseManagerId: caseManagerId || undefined,
        caseId: caseInformation?.id,
        createCaseIfMissing: !caseInformation?.id,
      });
      showToast("Funding company & case information updated.", "success");
      onSaved();
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to save case information",
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
      title="Edit Funding Company & Case Information"
      submitLabel={saving ? "Saving..." : "Save"}
      loading={saving}
    >
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Funding Company
          </label>
          <SellingEntitySelect
            entityType="FundingCompany"
            value={fundingCompanyId}
            onChange={(v) => {
              setFundingCompanyId(v);
              setFundingCompanyContactId("");
            }}
            placeholder="Select funding company..."
            searchPlaceholder="Search funding companies..."
            allowCreate
            createLabel="Add Funding Company"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Contact Person
          </label>
          <SellingEntitySelect
            entityType="FundingCompany"
            companyId={fundingCompanyId}
            isContactPerson
            requireParent
            parentHint="Select a funding company first"
            value={fundingCompanyContactId}
            onChange={(v) => setFundingCompanyContactId(v)}
            placeholder="Select contact person..."
            searchPlaceholder="Search contacts..."
            allowCreate
            createLabel="Add New Contact Person"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Handling Law Firm
          </label>
          <SellingEntitySelect
            entityType="LawFirm"
            value={lawFirmId}
            onChange={(v) => {
              setLawFirmId(v);
              setCaseManagerId("");
            }}
            placeholder="Select law firm..."
            searchPlaceholder="Search law firms..."
            allowCreate
            createLabel="Add New Law Firm"
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Case Manager
          </label>
          <SellingEntitySelect
            entityType="LawFirm"
            contactType="CaseManager"
            companyId={lawFirmId}
            isContactPerson
            requireParent
            parentHint="Select a law firm first"
            value={caseManagerId}
            onChange={(v) => setCaseManagerId(v)}
            placeholder="Select case manager..."
            searchPlaceholder="Search case managers..."
            allowCreate
            createLabel="Add Case Manager"
          />
        </div>
      </div>
    </FormModal>
  );
}
