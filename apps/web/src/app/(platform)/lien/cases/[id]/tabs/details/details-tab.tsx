"use client";

import { useCallback, useState } from "react";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CaseDetail } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import type {
  CaseUpdatesItem,
  UpdateCaseRequestDto,
} from "@/lib/cases/cases.types";
import { dateConverter, dateConvertertoIso } from "@/lib/cases/cases.mapper";
import { useSessionContext } from "@/providers/session-provider";
import { FeedsSection } from "../../components/feeds-section";
import { CaseTrackingSection } from "./sections/case-tracking-section";
import { PlaintiffSection } from "./sections/plaintiff-section";
import { UpdatesSection } from "./sections/updates-section";

function formatPhoneNumber(rawValue: string | number): string {
  const digits = String(rawValue).replace(/\D/g, "");
  if (digits.length <= 3) return digits;
  if (digits.length <= 6) return `(${digits.slice(0, 3)}) ${digits.slice(3)}`;
  return `(${digits.slice(0, 3)}) ${digits.slice(3, 6)}-${digits.slice(6, 10)}`;
}

export function DetailsTab({
  d,
  u,
  panelMode,
  onPanelModeChange,
  canEdit,
  onCaseUpdated,
}: {
  d: CaseDetail;
  u: CaseUpdatesItem[];
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  canEdit: boolean;
  onCaseUpdated: (updated: CaseDetail) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);

  const [editingPlaintiff, setEditingPlaintiff] = useState(false);
  const [editingTracking, setEditingTracking] = useState(false);

  const [pFirstName] = useState(d.clientFirstName);
  const [pLastName] = useState(d.clientLastName);
  const [pPhone, setPPhone] = useState(formatPhoneNumber(d.clientPhone));
  const [pEmail] = useState(d.clientEmail);
  const [pDob, setPDob] = useState(dateConvertertoIso(d.clientDob));

  const [pSaving, setPSaving] = useState(false);
  const [pErrors, setPErrors] = useState<Record<string, string>>({});

  const formattedDoI = dateConvertertoIso(d.dateOfIncident);
  const formattedFd = dateConvertertoIso(d.trackingFollowUpDate);

  const [tDateOfIncident, setTDateOfIncident] = useState(formattedDoI);
  const [tTrackingFollowUpDate, setTTrackingFollowUpDate] =
    useState(formattedFd);

  const [tSaving, setTSaving] = useState(false);
  const [tErrors, setTErrors] = useState<Record<string, string>>({});

  const [form, setForm] = useState({ ...d });

  const { lookup } = useSessionContext();

  const updateField = (field: keyof typeof d, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const accidentType =
    lookup?.AccidentType?.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];
  const state =
    lookup?.State?.map((c) => {
      return { key: c.id, value: c.code, label: c.code };
    }) ?? [];
  const medicalStatus =
    lookup?.MedicalStatus?.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];

  const caseStatusList =
    lookup?.CaseStatus.map((s) => {
      return { key: s.id, value: s.code, label: s.name };
    }) ?? [];

  const resetPlaintiffForm = useCallback(() => {
    setForm({ ...d });
    setPErrors({});
  }, [d]);

  const resetTrackingForm = useCallback(() => {
    setTDateOfIncident(dateConvertertoIso(d.dateOfIncident));
    setTTrackingFollowUpDate(dateConvertertoIso(d.trackingFollowUpDate));
    setForm({ ...d });
    setTErrors({});
  }, [d]);

  const validatePlaintiff = (): boolean => {
    const errs: Record<string, string> = {};
    if (!pFirstName.trim()) errs.firstName = "First name is required";
    if (!pLastName.trim()) errs.lastName = "Last name is required";
    if (pEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(pEmail.trim()))
      errs.email = "Invalid email format";
    if (pPhone.trim() && !/^[\d\s()+-]{7,20}$/.test(pPhone.trim()))
      errs.phone = "Invalid phone format";
    const pdob = dateConverter(pDob) ?? "";
    if (
      pdob.trim() &&
      !/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(pdob.trim()) &&
      !/^\w{3}\s\d{1,2},\s\d{4}$/.test(pdob.trim())
    )
      errs.dob = "Invalid date format (use MM/DD/YYYY)";
    setPErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handlePlaintiffSave = useCallback(async () => {
    if (!validatePlaintiff()) return;
    setPSaving(true);
    const payload = {
      caseId: d.id,
      firstName: form.clientFirstName.trim(),
      lastName: form.clientLastName.trim(),
      phone: form.clientPhone.trim().replace(/\D/g, "") || "",
      email: form.clientEmail.trim() || "",
      dob: dateConverter(form.clientDob) || "",
      address: form.clientStreetAddress.trim() || "",
      sex: form.sex || "",
      city: form.clientCity,
      state: form.clientState,
      zipcode: form.clientZipcode,
    };
    try {
      await casesService.updateCasePersonal(payload);
      setTimeout(() => {
        onCaseUpdated({ ...d, ...payload });
      }, 100);

      setEditingPlaintiff(false);
      addToast({
        type: "success",
        title: "Plaintiff Updated",
        description: "Plaintiff information saved successfully.",
      });
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save plaintiff info";
      addToast({ type: "error", title: "Save Failed", description: message });
    } finally {
      setPSaving(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [d, form, onCaseUpdated, addToast]);

  const handleTrackingSave = useCallback(async () => {
    setTSaving(true);
    const payload: UpdateCaseRequestDto = {
      caseId: d.id,
      currentStatus: form.status,
      currentMedicalStatus: form.currentMedicalStatus,
      caseType: form.caseType,
      stateOfIncident: form.stateOfIncident,
      trackingFollowUp: dateConverter(form.trackingFollowUpDate),
      dateOfLoss: dateConverter(form.dateOfIncident),
      leadId: form.leadId,
      description: form.description || "",
      notes: form.notes || "",
      demandAmount: d.demandAmount ?? 0.0,
      settlementAmount: d.settlementAmount ?? 0.0,
      shareCase: form.shareCase == "Yes" ? "true" : "false",
      minorComp: isMinor() ? "true" : "false",
      caseDropped: form.caseDropped == "Yes" ? "true" : "false",
      childSupportLiens: form.childSupportLiens == "Yes" ? "true" : "false",
      isUccFiled: form.isUccFiled == "Yes" ? "true" : "false",
    };
    try {
      await casesService.updateCase(payload);
      setTimeout(() => {
        onCaseUpdated({ ...d });
      }, 100);

      setEditingTracking(false);
      addToast({
        type: "success",
        title: "Case Tracking Updated",
        description: "Case tracking information saved successfully.",
      });
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save case tracking";
      addToast({ type: "error", title: "Save Failed", description: message });
    } finally {
      setTSaving(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    d,
    tSaving,
    tDateOfIncident,
    tTrackingFollowUpDate,
    form,
    onCaseUpdated,
    addToast,
  ]);

  const isMinor = () => {
    let result = false;
    let age = 0;
    // Convert strings to Date objects
    const dob = new Date(form.clientDob);
    const dol = new Date(form.dateOfIncident);
    if (isNaN(dob.getTime()) || isNaN(dol.getTime())) {
      age = 0; // invalid date
      return;
    }
    age = dol.getFullYear() - dob.getFullYear();
    // Adjust if the birthday hasn't occurred yet this year
    const m = dol.getMonth() - dob.getMonth();
    if (m < 0 || (m === 0 && dol.getDate() < dob.getDate())) {
      age--;
    }
    age = age;
    result = age < 18;
    return result;
  };
  const updateCaseFlag = useCallback(
    async (key: keyof CaseDetail, value: string) => {
      try {
        await casesService.updateCase({
          caseId: d.id,
          [key]: value,
        });
        addToast({
          type: "success",
          title: "Case Flag Updated",
        });
      } catch (err) {
        if (err instanceof ApiError) {
          addToast({
            type: "error",
            title: "Case Flag Update Failed",
          });
        }
      }
    },
    [form],
  );

  const leftContent = (
    <div className="space-y-4">
      <CaseTrackingSection
        d={d}
        canEdit={canEdit}
        editing={editingTracking}
        onStartEdit={() => {
          resetTrackingForm();
          setEditingTracking(true);
        }}
        form={form}
        updateField={updateField}
        tDateOfIncident={tDateOfIncident}
        setTDateOfIncident={setTDateOfIncident}
        tTrackingFollowUpDate={tTrackingFollowUpDate}
        setTTrackingFollowUpDate={setTTrackingFollowUpDate}
        caseStatusList={caseStatusList}
        medicalStatus={medicalStatus}
        accidentType={accidentType}
        state={state}
        tSaving={tSaving}
        onSave={handleTrackingSave}
        onCancel={() => {
          resetTrackingForm();
          setEditingTracking(false);
          setTErrors({});
        }}
        onUpdateCaseFlag={updateCaseFlag}
      />

      <PlaintiffSection
        d={d}
        canEdit={canEdit}
        editing={editingPlaintiff}
        onStartEdit={() => {
          resetPlaintiffForm();
          setEditingPlaintiff(true);
        }}
        form={form}
        updateField={updateField}
        state={state}
        pPhone={pPhone}
        setPPhone={setPPhone}
        pDob={pDob}
        setPDob={setPDob}
        pSaving={pSaving}
        onSave={handlePlaintiffSave}
        onCancel={() => {
          resetPlaintiffForm();
          setEditingPlaintiff(false);
          setPErrors({});
        }}
      />

      <UpdatesSection u={u} />
    </div>
  );

  const rightContent = (
    <FeedsSection
      caseId={d.id}
      panelMode={panelMode}
      onPanelModeChange={onPanelModeChange}
    />
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
      showControls={false}
    />
  );
}
