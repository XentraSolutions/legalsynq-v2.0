import React, { useCallback, useEffect, useMemo, useState } from "react";
import Field from "../../field";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import { useSessionContext } from "@/providers/session-provider";
import { BaseSelectOption } from "@/components/ui/base-select";

export interface MedicalLienInfoProps {
  caseId?: string;
  lienId?: string;
  data?: any;
  onFormValid?: (valid: boolean, data?: any) => void;
  openAddFundingCompanyModal?: () => void;
}

const INITIAL_FORM = {
  status: "",
  purchaseDate: "",
  initialServiceDate: "",
  endServiceDate: "",
  note: "",
  fundingCompanyId: "",
  fundingCompany: "",
  isBulk: "false",
  isServicing: "true",
};

type DropdownData = {
  status: Array<Record<string, string>>;
};

export default function MedicalLienInfo(props: MedicalLienInfoProps) {
  const { lookup } = useSessionContext();
  const { data, onFormValid } = props;
  const [form, setForm] = useState(!data ? { ...INITIAL_FORM } : data);
  const [errors, setErrors] = useState<Record<string, string>>({});

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

  useEffect(() => {
    validateForm();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  function validateForm() {
    const valid =
      !!form.status && !!form.purchaseDate && !!form.initialServiceDate;
    onFormValid?.(valid, form);
  }

  return (
    <div className="container-fluid">
      <div className="row border-bottom border-solid pb-3 mb-3">
        <div className="col-12 mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-primary">
            <i className="ri-stethoscope-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">
            Medical Lien Information{" "}
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
            label="Purchase Date"
            value={form.purchaseDate}
            onChange={(v) => setForm({ ...form, purchaseDate: v.toString() })}
          />
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
          <Field
            type="date"
            required
            label="Initial Service Date"
            value={form.initialServiceDate}
            onChange={(v) =>
              setForm({ ...form, initialServiceDate: v.toString() })
            }
          />

          <Field
            type="date"
            label="End Service Date"
            value={form.endServiceDate}
            onChange={(v) => setForm({ ...form, endServiceDate: v.toString() })}
          />
        </div>
        <div className="grid grid-cols-1 gap-4 mt-4">
          <Field
            type="textarea"
            label="Notes"
            value={form.note}
            onChange={(v) => setForm({ ...form, note: v.toString() })}
          />
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mt-4">
          <Field
            type="checkbox"
            label="Bulk"
            isChecked={form.isBulk == "true"}
            onChange={(v) => setForm({ ...form, isBulk: v.toString() })}
          />

          <Field
            type="checkbox"
            label="Servicing"
            isChecked={form.isServicing == "true"}
            onChange={(v) => {
              setForm({ ...form, isServicing: v.toString() });
            }}
          />
        </div>
      </div>

      <div className="row mt-5">
        <div className="col-12 mb-2">
          <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#81D07D]">
            <i className="ri-money-dollar-box-line text-light" />
          </span>
          <span className="font-semibold mb-2 mt-1">Funding Company</span>
        </div>

        <div className="row form-indent">
          <div className="col-12 mb-2">
            <label htmlFor="facilityName" className="form-label">
              {" "}
            </label>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Funding Company
            </label>
            <ContactEntitySelect
              contactType="FundingCompany"
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
        </div>
      </div>
    </div>
  );
}
