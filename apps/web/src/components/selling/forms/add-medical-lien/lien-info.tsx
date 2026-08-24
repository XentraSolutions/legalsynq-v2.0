import React, { useEffect, useState } from "react";
import Field from "@/components/lien/field";
import {
  DEFAULT_LISTING_VISIBILITY,
  LienScheduleFields,
} from "./lien-schedule-fields";

export interface LienInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM = {
  status: "Pending",
  listingVisibility: DEFAULT_LISTING_VISIBILITY,
  initialServiceDate: "",
  endServiceDate: "",
  notes: "",
  fundingCompanyId: "",
  fundingCompany: "",
  isBulk: "false",
  isServicing: "true",
};

// New liens can only be created as Pending or Internal — the API rejects any
// other sellerStatus during intake (see NormalizeIntakeStatus in
// apps/services/liens/Liens.Api/Endpoints/SellingV2Endpoints.cs; undocumented
// elsewhere, so that file is the source of truth).
const STATUSES = [
  { id: "Pending", code: "Pending", name: "Pending" },
  { id: "Internal", code: "Internal", name: "Internal" },
];

export default function LienInfo(props: LienInfoProps) {
  const { data, onFormValid } = props;
  const [form, setForm] = useState(!data ? { ...INITIAL_FORM } : data);

  const statusList =
    STATUSES.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  function validateForm() {
    const valid = !!form.status && !!form.initialServiceDate;
    onFormValid?.(valid, form);
  }

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        <div className="col-12 mb-2">
          <span className="font-semibold mb-2 text-2xl mt-1">
            Lien Information
          </span>
        </div>

        <div className="grid grid-cols-1 gap-4 mt-4">
          <Field
            required
            label="Lien Status"
            value={form.status}
            options={statusList}
            onChange={(v: string) => {
              setForm({ ...form, status: v.toString() });
            }}
            type="select"
          />
        </div>
        <LienScheduleFields
          value={form}
          onChange={(patch) => setForm({ ...form, ...patch })}
          requireInitialServiceDate
        />
      </div>
    </div>
  );
}
