"use client";

import { useCallback, useEffect, useState } from "react";
import { FormModal } from "@/components/lien/modal";
import { lookupService } from "@/lib/lookup";
import { contactsService } from "@/lib/contacts";
import { useSessionContext } from "@/providers/session-provider";
import Field from "@/components/lien/field";

interface CasesFilterForm {
  lawFirmId: string[];
  accidentTypeId: string[];
  caseManagerId: string[];
  statusId: string[];
}

interface CasesFilterProps {
  open: boolean;
  onClose: () => void;
  onApplyFilter?: (e: CasesFilterForm) => void;
}

const INITIAL_FORM: CasesFilterForm = {
  lawFirmId: [],
  accidentTypeId: [],
  caseManagerId: [],
  statusId: [],
};

export function CasesFilter({
  open,
  onClose,
  onApplyFilter,
}: CasesFilterProps) {
  const { lookup } = useSessionContext();

  const [form, setForm] = useState<CasesFilterForm>({ ...INITIAL_FORM });
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async () => {
    setSubmitting(true);
    setTimeout(() => {
      setSubmitting(false);
      onApplyFilter?.(form);
    }, 2000); // 2 seconds
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    onClose();
  };

  const [data, setData] = useState<{
    status: Array<{ key: string; value: string; label: string }>;
    lawFirm: Array<{ key: string; value: string; label: string }>;
    caseManagers: Array<{ key: string; value: string; label: string }>;
    accidentType: Array<{ key: string; value: string; label: string }>;
  }>({
    status: [],
    lawFirm: [],
    caseManagers: [],
    accidentType: [],
  });

  const fetchData = useCallback(async () => {
    const [lawfirmRes, caseManagersRes] = await Promise.allSettled([
      lookupService.getLawfirm(),
      contactsService.getCaseManagers(),
    ]);
    if (
      lawfirmRes.status === "fulfilled" &&
      caseManagersRes.status === "fulfilled"
    ) {
      setData((prev: any) => ({
        ...prev,
        status:
          lookup?.CaseStatus?.map((c) => {
            return { key: c.id, value: c.code, label: c.name };
          }) ?? [],
        lawFirm: lawfirmRes.value.items.map((c) => {
          return { key: c.id, value: c.id, label: c.displayName };
        }),
        caseManagers: caseManagersRes.value.items.map((c) => {
          return { key: c.id, value: c.id, label: c.displayName };
        }),
        accidentType:
          lookup?.AccidentType?.map((c) => {
            return { key: c.id, value: c.code, label: c.name };
          }) ?? [],
      }));
    }
  }, [open, setData]);

  useEffect(() => {
    fetchData();
  }, [fetchData, open]);

  return (
    <FormModal
      open={open}
      onClose={reset}
      onSubmit={handleSubmit}
      size="lg"
      title="Filter Cases"
      subtitle="Narrow down cases using filters to quickly find relevant results."
      submitLabel={submitting ? "Filtering..." : "Apply Filters"}
    >
      <div className="space-y-4 min-h-[250px]">
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="Law Firm"
            value={form.lawFirmId}
            options={data.lawFirm ? data.lawFirm : []}
            placeholder="Select one or more"
            onChange={(v) => {
              setForm({
                ...form,
                lawFirmId: Array.isArray(v)
                  ? v
                  : typeof v === "string" && v
                    ? [v]
                    : [],
              });
            }}
            type="select"
            multiple
          />

          <Field
            label="Accident Type"
            value={form.accidentTypeId}
            options={data.accidentType ? data.accidentType : []}
            placeholder="Select one or more"
            onChange={(v) => {
              setForm({
                ...form,
                accidentTypeId: Array.isArray(v)
                  ? v
                  : typeof v === "string" && v
                    ? [v]
                    : [],
              });
            }}
            type="select"
            multiple
          />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field
            label="Case Manager"
            value={form.caseManagerId}
            options={data.caseManagers ? data.caseManagers : []}
            placeholder="Select one or more"
            onChange={(v) =>
              setForm({
                ...form,
                caseManagerId: Array.isArray(v)
                  ? v
                  : typeof v === "string" && v
                    ? [v]
                    : [],
              })
            }
            type="select"
            multiple
          />

          <Field
            label="Status"
            value={form.statusId}
            options={data.status}
            placeholder="Select one or more"
            onChange={(v) => {
              setForm({
                ...form,
                statusId: Array.isArray(v)
                  ? v
                  : typeof v === "string" && v
                    ? [v]
                    : [],
              });
            }}
            type="select"
            multiple
          />
        </div>
      </div>
    </FormModal>
  );
}
