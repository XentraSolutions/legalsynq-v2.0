"use client";

import { useCallback, useEffect, useState } from "react";
import { Modal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CreateCaseRequestDto } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import Field from "../field";
import { useSessionContext } from "@/providers/session-provider";
import { useCases, useCreateCase } from "@/hooks/use-case-liens";

interface MergeCaseFormProps {
  caseNumber: string;
  open: boolean;
  onClose: () => void;
  onCreated?: () => void;
}

const INITIAL_FORM = {
  id: "",
  caseType: "---",
  caseNumber: "---",
  externalReference: "---",
  title: "---",
  clientName: "---",
  clientFirstName: "---",
  clientLastName: "---",
  clientDisplayName: "---",
  status: "---",
  statusLabel: "---",
  dateOfIncident: "---",
  clientDob: "---",
  clientPhone: "---",
  clientEmail: "---",
  clientAddress: "---",
  clientStreetAddress: "---",
  clientCity: "---",
  clientState: "---",
  clientZipcode: "---",
  sex: "---",
  currentMedicalStatus: "---",
  stateOfIncident: "---",
  trackingFollowUpDate: "---",
  leadId: "---",
  insuranceCarrier: "---",
  policyNumber: "---",
  claimNumber: "---",
  demandAmount: "---",
  settlementAmount: "---",
  description: "---",
  notes: "---",
  trackingFollowUp: "---",
  openedAt: "---",
  closedAt: "---",
  createdAt: "---",
  updatedAt: "---",
};

export function MergeCaseForm({
  caseNumber,
  open,
  onClose,
  onCreated,
}: MergeCaseFormProps) {
  const { lookup } = useSessionContext();
  const [isOpen, setOpen] = useState<boolean>(open ?? true);
  const query = {
    keyword: "",
    page: 1,
    limit: 500,
    sortBy: "createdAt",
    sortDirection: "desc",
    accidentTypeId: "",
    caseManagerId: "",
    lawFirmId: "",
    statusId: "",
  };

  const { data, isLoading, isFetching } = useCases(query);
  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState<any>({
    ...INITIAL_FORM,
    caseNumber: caseNumber ?? "",
  });

  const [clientData, setClientData] = useState<any>({
    ...INITIAL_FORM,
  });

  const [selectedId, setSelectedId] = useState("");
  const [cases, setCases] = useState<any>();
  const [submitting, setSubmitting] = useState<boolean>(false);

  useEffect(() => {
    if (!isLoading && data) {
      setCases(
        data
          ? data.items.map((c) => {
              return { key: c.id, value: c.id, label: c.clientName };
            })
          : [],
      );
    }
  }, [data]);

  const handleSubmit = async () => {
    try {
      await casesService.mergeCase({
        caseIdA: caseNumber,
        caseIdB: selectedId,
      });
      addToast({
        type: "success",
        title: "Merged Cases",
      });
    } catch (err) {
    } finally {
      onCreated?.();
      setOpen(false);
    }
  };

  const fetchCase = useCallback(async () => {
    if (selectedId == "") return;
    try {
      const detail = await casesService.getCase(selectedId);
      setClientData(detail);
    } catch (err) {
    } finally {
    }
  }, [selectedId]);

  useEffect(() => {
    fetchCase();
  }, [selectedId]);

  useEffect(() => {}, [clientData]);

  return (
    <>
      <Modal
        open={isOpen}
        onClose={onClose}
        title="Merge Case"
        size="lg"
        footer={
          <>
            <button
              onClick={() => {}}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
            >
              Close
            </button>
            <button
              onClick={() => {
                handleSubmit();
                setSubmitting(true);
              }}
              disabled={submitting}
              className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50"
            >
              {submitting ? "Merging..." : "Merge"}
            </button>
          </>
        }
      >
        <div className="mb-3">
          <div className="bg-[#fef7e5] text-[#d4aa45] p-2 rounded-md text-sm">
            <strong>
              Warning: You are about to merge two cases. This action will
              combine case records and cannot be reversed. Ensure the selected
              case is correct before continuing.
            </strong>
          </div>
        </div>
        <div className="space-y-3">
          <div>
            <Field
              label="Case"
              required
              value={form.id}
              options={cases}
              placeholder="Select one case"
              onChange={(v: string) => setSelectedId(v.toString())}
              type="select"
            />
          </div>

          <p className="text-xs mt-6 font-medium text-gray-500 uppercase tracking-wide">
            Case Details
          </p>

          <div className="grid grid-cols-3 gap-x-8 gap-y-3">
            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Plaintiff Name
              </label>
              {clientData?.clientName ?? "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Case Id
              </label>
              {clientData?.caseNumber ?? "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Case Status
              </label>
              {clientData?.status ?? "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Case Type
              </label>
              {clientData?.caseType != "" ? clientData?.caseType : "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Date Of loss
              </label>
              {clientData?.dateOfIncident != ""
                ? clientData?.dateOfIncident
                : "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Date Of Birth
              </label>
              {clientData?.clientDob ?? "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Law firm
              </label>
              {clientData?.lawfirm ?? "---"}
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-600 uppercase tracking-wide mb-1">
                Case Manager
              </label>
              {clientData?.caseManager ?? "---"}
            </div>
          </div>
        </div>
      </Modal>
    </>
  );
}
