import React, { useEffect, useState } from "react";
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
          Lien Associations
        </span>
        <p className="font-normal text-sm text-gray-600 mb-2 mt-1">
          Add the medical provider and funding company associated with this
          lien. Case information is managed separately from the case itself.
        </p>
      </div>

      <div className="row form-indent">
        <div className="col-12 mb-2 mx-2">
          <CaseInformationFields
            value={form}
            onChange={(patch) => setForm({ ...form, ...patch })}
          />
        </div>
      </div>
    </div>
  );
}
