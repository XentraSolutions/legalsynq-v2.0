import React, { useEffect, useState } from "react";
import {
  ProviderFundingFields,
  type ProviderFundingFieldsValue,
} from "./provider-funding-fields";

export interface FundingCompanyInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM: ProviderFundingFieldsValue = {
  medicalProviderId: "",
  medicalProvider: "",
  facilityId: "",
  facility: "",
  fundingCompanyId: "",
  fundingCompany: "",
  fundingCompanyContactId: "",
  fundingCompanyContact: "",
};

export default function FundingCompanyInfo(props: FundingCompanyInfoProps) {
  const { data, onFormValid } = props;
  const [form, setForm] = useState(!data ? { ...INITIAL_FORM } : data);

  function validateForm() {
    onFormValid?.(true, form);
  }

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  return (
    <div className="row mt-5">
      <div className="col-12 mb-2">
        <span className="font-semibold mb-2 text-2xl mt-1">
          Provider & Funding Details
        </span>
        <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
          Review and update the provider and funding company information as
          needed.
        </p>
      </div>

      <div className="row form-indent">
        <div className="col-12 mb-2 mx-2">
          <ProviderFundingFields
            value={form}
            onChange={(patch) => setForm({ ...form, ...patch })}
          />
        </div>
      </div>
    </div>
  );
}
