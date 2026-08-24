import React, { useEffect, useState } from "react";
import { useSessionContext } from "@/providers/session-provider";
import {
  CaseInformationFields,
  type CaseInformationFieldsValue,
} from "./case-information-fields";

export interface FundingCompanyInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM: CaseInformationFieldsValue = {
  medicalProviderId: "",
  medicalProvider: "",
  fundingCompanyId: "",
  fundingCompany: "",
  fundingCompanyContactId: "",
  fundingCompanyContact: "",
  lawfirmId: "",
  caseManagerId: "",
};

export default function FundingCompanyInfo(props: FundingCompanyInfoProps) {
  const { lookup } = useSessionContext();
  const { data, onFormValid } = props;
  const [form, setForm] = useState(!data ? { ...INITIAL_FORM } : data);

  const statusList =
    lookup?.LienStatus.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];

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
    const valid = !!form.lawfirmId;
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
          Case Information
        </span>
        <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
          Provide the case information for this lien. Funding company details
          are optional and can be added later.
        </p>
      </div>

      <div className="row form-indent">
        <div className="col-12 mb-2 mx-2">
          <CaseInformationFields
            value={form}
            onChange={(patch) => setForm({ ...form, ...patch })}
            required
          />
        </div>
      </div>
    </div>
  );
}
