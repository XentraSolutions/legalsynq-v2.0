import React, { useCallback, useEffect, useMemo, useState } from "react";
import { SellingEntitySelect } from "@/components/selling/selling-entity-select";
import { useSessionContext } from "@/providers/session-provider";
import { BaseSelectOption } from "@/components/ui/base-select";

export interface FundingCompanyInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM = {
  fundingCompanyId: "",
  fundingCompany: "",
  medicalProviderId: "",
  medicalProvider: "",
};

type DropdownData = {
  status: Array<Record<string, string>>;
};

export default function FundingCompanyInfo(props: FundingCompanyInfoProps) {
  const { lookup } = useSessionContext();
  const { data, onFormValid } = props;
  const [form, setForm] = useState(!data ? { ...INITIAL_FORM } : data);
  const [errors, setErrors] = useState<Record<string, string>>({});

  const statusList =
    lookup?.LienStatus.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];

  const listingVisibility = [
    { key: "public", value: "public", label: "Public" },
    { key: "private", value: "private", label: "Private" },
  ];

  // Default new liens to "Open" status once the status list is available
  useEffect(() => {
    if (statusList.length > 0 && !form.status && !data) {
      const openStatus = statusList.find(
        (o) =>
          o.label.toLowerCase() === "open" || o.value.toLowerCase() === "open",
      );
      if (openStatus) {
        setForm((prev: typeof form) => ({ ...prev, status: openStatus.value }));
      }
    }
  }, [statusList, data]);

  function validateForm() {
    const valid = !!form.lawfirmId && !!form.fundingCompanyId;
    onFormValid?.(valid, form);
  }

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  return (
    <div className="row mt-5">
      <div className="col-12 mb-2">
        <span className="font-semibold mb-2 text-2xl mt-1">
          Funding Company & Case Information
        </span>
        <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
          Provide the neccessary funding company and case information to create
          and associate this lien
        </p>
      </div>

      <div className="row form-indent">
        <div className="col-12 mb-2">
          <div className="grid grid-cols-2 gap-4 mt-4 mx-2">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Funding Company<span className="text-red-500 ml-0.5">*</span>
              </label>
              <SellingEntitySelect
                entityType="FundingCompany"
                value={form.fundingCompanyId}
                onChange={(v, option) =>
                  setForm({
                    ...form,
                    fundingCompanyId: v,
                    fundingCompany: option?.label ?? "",
                  })
                }
                placeholder="Select funding company..."
                searchPlaceholder="Search funding companies..."
                allowCreate
                createLabel="Add Funding Company"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Select Contact Person
              </label>
              <SellingEntitySelect
                entityType="FundingCompany"
                companyId={form.fundingCompanyId}
                isContactPerson
                requireParent
                parentHint="Select a funding company first"
                value={form.fundingCompanyContactId}
                onChange={(v, option) =>
                  setForm({
                    ...form,
                    fundingCompanyContactId: v,
                    fundingCompanyContact: option?.label ?? "",
                  })
                }
                placeholder="Select contact person..."
                searchPlaceholder="Search contacts..."
                allowCreate
                createLabel="Add New Contact Person"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Medical Provider
              </label>
              <SellingEntitySelect
                entityType="MedicalProvider"
                value={form.medicalProviderId}
                onChange={(v, option) =>
                  setForm({
                    ...form,
                    medicalProviderId: v,
                    medicalProvider: option?.label ?? "",
                  })
                }
                placeholder="Select medical provider..."
                searchPlaceholder="Search medical providers..."
                allowCreate
                createLabel="Add Medical Provider"
              />
            </div>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3 mt-4 mx-2">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Handling Law Firm<span className="text-red-500 ml-0.5">*</span>
            </label>
            <SellingEntitySelect
              entityType="LawFirm"
              value={form.lawfirmId}
              onChange={(v) => {
                setForm((prev: any) => ({
                  ...prev,
                  lawfirmId: v,
                  caseManagerId: "",
                }));
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
              companyId={form.lawfirmId}
              contactType="CaseManager"
              isContactPerson
              requireParent
              parentHint="Select a law firm first"
              value={form.caseManagerId}
              onChange={(v) => setForm({ ...form, caseManagerId: v })}
              placeholder="Select case manager..."
              searchPlaceholder="Search case managers..."
              allowCreate
              createLabel="Add Case Manager"
            />
          </div>
        </div>
      </div>
    </div>
  );
}
