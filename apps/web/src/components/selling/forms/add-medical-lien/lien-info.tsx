import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import { useSessionContext } from "@/providers/session-provider";
import { BaseSelectOption } from "@/components/ui/base-select";
import Field from "@/components/lien/field";

export interface LienInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM = {
  status: "Pending",
  listingVisibility: "",
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

  // Values must match the backend's ListingVisibility casing ("Public" /
  // "Private") — a lowercase mismatch here means a hydrated lien's saved
  // value never matches an option, showing the select as unset.
  const listingVisibility = [
    { key: "Public", value: "Public", label: "Public" },
    { key: "Private", value: "Private", label: "Private" },
  ];

  // Default new liens to "Pending" status once the status list is available

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  function validateForm() {
    const valid =
      !!form.status && !!form.listingVisibility && !!form.initialServiceDate;
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

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
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
          <Field
            type="date"
            required
            label="Initial Service Date"
            value={form.initialServiceDate}
            onChange={(v) =>
              setForm({ ...form, initialServiceDate: v.toString() })
            }
          />
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
          <Field
            type="date"
            label="End Service Date"
            value={form.endServiceDate}
            onChange={(v) => setForm({ ...form, endServiceDate: v.toString() })}
          />
          <Field
            type="select"
            options={listingVisibility}
            required
            label="Listing Visibility"
            value={form.listingVisibility}
            onChange={(v: string) =>
              setForm({ ...form, listingVisibility: v.toString() })
            }
          />
        </div>
        <div className="grid grid-cols-1 gap-4 mt-4">
          <Field
            type="textarea"
            label="Notes"
            value={form.notes}
            onChange={(v) => setForm({ ...form, notes: v.toString() })}
          />
        </div>
      </div>
    </div>
  );
}
