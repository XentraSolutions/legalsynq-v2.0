"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { FormModal, Modal } from "@/components/lien/modal";
import { useLienStore } from "@/stores/lien-store";
import {
  casesService,
  type CaseDuplicateMatchDto,
  type CreateCaseRequestDto,
} from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { getCreateCaseFormErrors } from "./create-case-form-validator";
import Field from "../field";
import { contactsService } from "@/lib/contacts";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import { dateConverter, dateConvertertoIso } from "@/lib/cases/cases.mapper";
import { useCreateCase } from "@/hooks/use-case-liens";
import { LitigationStatusForm } from "./litigation-form";
import { DropdownOption } from "@/lib/lookup/lookup.types";
import { lookupService } from "@/lib/lookup";
import type {
  MoveSellingLienToManagementCaseInfoRequest,
  MoveSellingLienToManagementV2Result,
} from "@/lib/selling";

interface CreateCaseFormProps {
  caseNumber?: string;
  open: boolean;
  onClose: () => void;
  onCreated?: (caseId: string) => void;
  /**
   * Reuses the case UI for a Selling lien's atomic move-to-management flow.
   * The standalone case screen intentionally omits this callback and retains
   * its existing create-case behavior.
   */
  onMoveToManagement?: (
    caseInfo: MoveSellingLienToManagementCaseInfoRequest,
  ) => Promise<MoveSellingLienToManagementV2Result>;
}

const INITIAL_FORM = {
  caseNumber: "",
  clientFirstName: "",
  clientLastName: "",
  externalReference: "",
  title: "",
  clientDob: "",
  clientPhone: "",
  clientEmail: "",
  clientAddress: "",
  clientCity: "",
  clientState: "",
  clientZipcode: "",
  dateOfIncident: "",
  insuranceCarrier: "",
  policyNumber: "",
  claimNumber: "",
  description: "",
  notes: "",
  caseStatusId: "PreDemand",
  caseManagerId: "",
  lawfirmId: "",
  accidentTypeId: "",
  accidentStateId: "",
  isServicing: "true",
};

export function CreateCaseForm({
  caseNumber,
  open,
  onClose,
  onCreated,
  onMoveToManagement,
}: CreateCaseFormProps) {
  const router = useRouter();
  const { mutateAsync: createCase } = useCreateCase();
  const isMoveToManagement = Boolean(onMoveToManagement);

  const addToast = useLienStore((s) => s.addToast);
  const [form, setForm] = useState({
    ...INITIAL_FORM,
    caseNumber: caseNumber ?? "",
  });

  const [caseManagerRoleCode, setCaseManagerRoleCode] = useState<string>();
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [data, setData] = useState<{
    state: DropdownOption[];
    accidentState: DropdownOption[];
    status: DropdownOption[];
    accidentType: DropdownOption[];
    lawFirm: DropdownOption[];
  }>({
    state: [],
    accidentState: [],
    status: [],
    accidentType: [],
    lawFirm: [],
  });

  const [isValid, setIsValid] = useState(false);
  const [showLitigationForm, setShowLitigationForm] = useState(false);
  const [duplicateWarning, setDuplicateWarning] = useState<{
    message: string;
    matches: CaseDuplicateMatchDto[];
  } | null>(null);

  const [touched, setTouched] = useState<
    Record<keyof typeof INITIAL_FORM, boolean>
  >(
    Object.keys(INITIAL_FORM).reduce(
      (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: false }),
      {} as Record<keyof typeof INITIAL_FORM, boolean>,
    ),
  );

  const updateField = (field: keyof typeof INITIAL_FORM, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setTouched((prev) => ({ ...prev, [field]: true }));
  };

  const validate = () => {
    const newErrors = getCreateCaseFormErrors(form);
    if (onMoveToManagement && !form.accidentStateId) {
      newErrors.accidentStateId = "Accident State is required";
    }
    setErrors(newErrors);
    const valid = Object.keys(newErrors).length === 0;
    setIsValid(valid);
    return valid;
  };

  const fetchData = useCallback(async () => {
    const [caseStatusRes, statesRes, accidentTypeRes, lawFirmRes] =
      await Promise.allSettled([
        lookupService.getCaseStatus(),
        lookupService.getStates(),
        lookupService.getAccidentType(),
        lookupService.getLawfirm(),
      ]);
    setData({
      status:
        caseStatusRes.status === "fulfilled"
          ? caseStatusRes.value.items.map((item) => ({
              key: item.id,
              value: item.code,
              label: item.name,
            }))
          : [],
      state:
        statesRes.status === "fulfilled"
          ? statesRes.value.items.map((item) => ({
              key: item.id,
              value: item.code ?? item.name,
              label: item.code ?? item.name,
            }))
          : [],
      accidentState:
        statesRes.status === "fulfilled"
          ? statesRes.value.items.map((item) => ({
              key: item.id,
              value: item.code ?? item.name,
              label: item.code ?? item.name,
            }))
          : [],
      accidentType:
        accidentTypeRes.status === "fulfilled"
          ? accidentTypeRes.value.items.map((item) => ({
              key: item.id,
              value: item.id,
              label: item.name,
            }))
          : [],
      lawFirm:
        lawFirmRes.status === "fulfilled"
          ? lawFirmRes.value.items.map((item) => ({
              key: item.id,
              value: item.id,
              label: item.displayName,
            }))
          : [],
    });
  }, []);

  useEffect(() => {
    if (!Object.values(touched).some(Boolean)) return;

    const debounceId = window.setTimeout(() => {
      validate();
    }, 250);

    return () => window.clearTimeout(debounceId);
  }, [form, touched]);

  useEffect(() => {
    if (open) fetchData();
  }, [fetchData, open]);

  useEffect(() => {
    if (open) {
      contactsService.getCaseManagerRoleCode().then(setCaseManagerRoleCode);
    }
  }, [open]);

  const formatDate = (dateString: string) => {
    const input = dateString;
    const date = new Date(input); // parse string into Date
    const formatter = new Intl.DateTimeFormat("en-CA"); // en-CA gives YYYY-MM-DD
    return formatter.format(date);
  };

  const buildRequest = (): CreateCaseRequestDto => {
    return {
      // caseNumber: form.caseNumber.trim(),
      firstname: form.clientFirstName.trim(),
      lastname: form.clientLastName.trim(),
      externalReference: form.externalReference.trim(),
      title: form.title.trim() || undefined,
      dob: dateConverter(form.clientDob) || undefined,
      phone: form.clientPhone.trim(),
      email: form.clientEmail.trim(),
      address: form.clientAddress.trim(),
      city: form.clientCity.trim(),
      state: form.clientState,
      zipcode: form.clientZipcode,
      dateOfLoss: dateConverter(form.dateOfIncident) || undefined,
      insuranceCarrier: form.insuranceCarrier.trim() || undefined,
      policyNumber: form.policyNumber.trim(),
      claimNumber: form.claimNumber.trim(),
      description: form.notes.trim() || undefined,
      notes: form.notes.trim(),
      caseStatusId: form.caseStatusId,
      lawfirmId: form.lawfirmId || undefined,
      accidentTypeId: form.accidentTypeId || undefined,
      accidentStateId:
        data.accidentState.find((s) => s.value == form.accidentStateId)?.key ??
        "",
      caseManagerId: form.caseManagerId || undefined,
      isServicing: form.isServicing == "true",
      caseType: form.accidentTypeId || undefined,
      dateOfIncident: dateConverter(form.dateOfIncident) || undefined,
      stateOfIncident: form.accidentStateId || undefined,
      minorComp: isMinor() ? "true" : "false",
    };
  };

  const buildMoveToManagementCaseInfo = (): MoveSellingLienToManagementCaseInfoRequest => ({
    clientFirstName: form.clientFirstName.trim(),
    clientLastName: form.clientLastName.trim(),
    clientDob: dateConvertertoIso(form.clientDob),
    clientAddress: form.clientAddress.trim() || undefined,
    clientCity: form.clientCity.trim() || undefined,
    clientState: form.clientState || undefined,
    clientZipCode: form.clientZipcode.trim() || undefined,
    isServicing: form.isServicing === "true",
    statusLabel:
      data.status.find((item) => item.value === form.caseStatusId)?.label ??
      form.caseStatusId,
    accidentTypeId: form.accidentTypeId,
    stateOfIncident: form.accidentStateId,
    dateOfIncident: form.dateOfIncident
      ? dateConvertertoIso(form.dateOfIncident)
      : undefined,
    lawFirmId: form.lawfirmId,
    caseManagerId: form.caseManagerId || undefined,
    notes: form.notes.trim() || undefined,
  });

  const markAllTouched = () => {
    setTouched(
      Object.keys(INITIAL_FORM).reduce(
        (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: true }),
        {} as Record<keyof typeof INITIAL_FORM, boolean>,
      ),
    );
  };

  const handleSubmit = async () => {
    if (!validate()) {
      markAllTouched();
      return;
    }

    setSubmitting(true);
    try {
      if (onMoveToManagement) {
        const result = await onMoveToManagement(buildMoveToManagementCaseInfo());
        addToast({
          type: "success",
          title: "Lien Moved to Management",
          description: result.message,
        });
        setForm({ ...INITIAL_FORM });
        setErrors({});
        onCreated?.(result.caseId);
        return;
      }

      const request = buildRequest();
      const duplicate = await casesService.checkDuplicateCase(request);
      if (duplicate.isDuplicate && duplicate.matches.length > 0) {
        setDuplicateWarning({
          message:
            duplicate.message ||
            "A case with similar information already exists. Would you like to view the existing case?",
          matches: duplicate.matches,
        });
        return;
      }

      const res = await createCase(request);
      addToast({
        type: "success",
        title: "Case Created",
        description: `Case has been created.`,
      });

      setTimeout(() => {
        onCreated?.(res.id);
        setForm({ ...INITIAL_FORM });
        setErrors({});
      }, 500);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Create Failed",
          description: "An unexpected error occurred",
        });
      }
    } finally {
      setSubmitting(false);
    }
  };

  const reset = () => {
    setForm({ ...INITIAL_FORM });
    setErrors({});
    setIsValid(false);
    setDuplicateWarning(null);
    setTouched(
      Object.keys(INITIAL_FORM).reduce(
        (acc, key) => ({ ...acc, [key as keyof typeof INITIAL_FORM]: false }),
        {} as Record<keyof typeof INITIAL_FORM, boolean>,
      ),
    );
    onClose();
  };

  const closeDuplicateWarning = () => {
    setDuplicateWarning(null);
  };

  const viewDuplicateCase = (caseId: string) => {
    setDuplicateWarning(null);
    onClose();
    router.push(`/lien/cases/${caseId}`);
  };

  const checkStatus = (caseStatus: string) => {
    if (caseStatus.toLowerCase().includes("litigation")) {
      setShowLitigationForm(true);
    }
  };

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

  const setLitigationStatus = (status: DropdownOption) => {
    setData((prev) => ({
      ...prev,
      status: prev.status.map((s) =>
        s.value.includes("Litigation")
          ? { ...s, value: status.value, label: `Litigation (${status.label})` }
          : s,
      ),
    }));
    updateField("caseStatusId", status.value);
    setShowLitigationForm(false);
  };

  return (
    <>
      <FormModal
        open={open}
        onClose={submitting ? () => {} : reset}
        onSubmit={handleSubmit}
        title={
          isMoveToManagement ? "Keep & Move to Lien Management" : "Create Case"
        }
        subtitle={
          isMoveToManagement
            ? "Create a case to keep this lien as an internal asset"
            : "Add a new case to the system"
        }
        submitLabel={
          submitting
            ? isMoveToManagement
              ? "Keeping & Moving..."
              : "Creating..."
            : isMoveToManagement
              ? "Keep & Move"
              : "Create Case"
        }
        submitDisabled={!isValid || submitting}
        size="xl"
      >
        <div className="space-y-4">
          <div className="col-12 mb-6 mt-2">
            <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#0a375a]">
              <i className="ri-user-3-line text-light" />
            </span>
            <span className="font-semibold mb-2 mt-1">
              Personal Information
            </span>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="First Name"
              required
              value={form.clientFirstName}
              onChange={(v) => updateField("clientFirstName", v.toString())}
              error={touched.clientFirstName ? errors.clientFirstName : ""}
              placeholder="First name"
            />
            <Field
              label="Last Name"
              required
              value={form.clientLastName}
              onChange={(v) => updateField("clientLastName", v.toString())}
              error={touched.clientLastName ? errors.clientLastName : ""}
              placeholder="Last name"
            />
          </div>
          <Field
            label="Date of Birth"
            required
            value={form.clientDob}
            onChange={(v) => {
              updateField("clientDob", v.toString());
            }}
            error={touched.clientDob ? errors.clientDob : ""}
            type="date"
            maxDate={new Date()}
          />
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Address"
              value={form.clientAddress}
              onChange={(v) => updateField("clientAddress", v.toString())}
              error={touched.clientAddress ? errors.clientAddress : ""}
              placeholder="Address"
            />
            <Field
              label="City"
              value={form.clientCity}
              onChange={(v) => updateField("clientCity", v.toString())}
              error={touched.clientCity ? errors.clientCity : ""}
              placeholder="City"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field
              label="State"
              value={form.clientState}
              options={data.state}
              onChange={(v: string) => updateField("clientState", v.toString())}
              placeholder="State"
              type="select"
            />

            <Field
              label="Zipcode"
              value={form.clientZipcode}
              onChange={(v) => updateField("clientZipcode", v.toString())}
              error={touched.clientZipcode ? errors.clientZipcode : ""}
              placeholder="Zipcode"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              type="checkbox"
              label="SERVICING"
              isChecked={form.isServicing == "true"}
              onChange={(v) => setForm({ ...form, isServicing: v.toString() })}
            />
          </div>
          <div className="col-12 mb-6 mt-6">
            <span className="inline-block w-[30px] text-center text-white mr-2 rounded bg-[#EE712F]">
              <i className="ri-survey-line text-light" />
            </span>
            <span className="font-semibold mb-2 mt-1">Case Information</span>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Status"
              required
              value={form.caseStatusId}
              options={data?.status}
              placeholder=""
              onChange={(v: string) => {
                checkStatus(v);
                setForm({
                  ...form,
                  caseStatusId: v.toString(),
                });
              }}
              type="select"
            />
            <Field
              label="Accident Type"
              required
              value={form.accidentTypeId}
              options={data?.accidentType}
              placeholder=""
              onChange={(v: string) => {
                setForm({
                  ...form,
                  accidentTypeId: v.toString(),
                });
              }}
              type="select"
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field
              label="Accident State"
              required
              value={form.accidentStateId}
              options={data?.accidentState}
              placeholder=""
              onChange={(v: string) => {
                setForm({
                  ...form,
                  accidentStateId: v.toString(),
                });
              }}
              type="select"
            />
            <Field
              label="Date of Loss"
              value={form.dateOfIncident}
              onChange={(v) => updateField("dateOfIncident", v.toString())}
              type="date"
              maxDate={new Date()}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            {isMoveToManagement ? (
              <>
                <div>
                  <Field
                    label="Law Firm"
                    required
                    type="select"
                    value={form.lawfirmId}
                    options={data.lawFirm}
                    onChange={(v: string) => {
                      setForm((prev) => ({
                        ...prev,
                        lawfirmId: v,
                        caseManagerId: "",
                      }));
                      setTouched((prev) => ({ ...prev, lawfirmId: true }));
                    }}
                    placeholder="Select law firm..."
                  />
                  {touched.lawfirmId && errors.lawfirmId && (
                    <p className="mt-1 text-xs text-red-500">
                      {errors.lawfirmId}
                    </p>
                  )}
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    Case Manager
                  </label>
                  <ContactEntitySelect
                    contactType="LawFirm"
                    contactSubtype={caseManagerRoleCode}
                    lawFirmId={form.lawfirmId}
                    requireParent
                    parentHint="Select a law firm first"
                    value={form.caseManagerId}
                    onChange={(v) => updateField("caseManagerId", v)}
                    placeholder="Select case manager..."
                    searchPlaceholder="Search case managers..."
                  />
                </div>
              </>
            ) : (
              <>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    Law Firm<span className="ml-0.5 text-red-500">*</span>
                  </label>
                  <ContactEntitySelect
                    contactType="LawFirm"
                    value={form.lawfirmId}
                    onChange={(v) => {
                      setForm((prev) => ({
                        ...prev,
                        lawfirmId: v,
                        caseManagerId: "",
                      }));
                      setTouched((prev) => ({ ...prev, lawfirmId: true }));
                    }}
                    error={Boolean(touched.lawfirmId && errors.lawfirmId)}
                    placeholder="Select law firm..."
                    searchPlaceholder="Search law firms..."
                    allowCreate
                    createLabel="Add New Law Firm"
                  />
                  {touched.lawfirmId && errors.lawfirmId && (
                    <p className="mt-1 text-xs text-red-500">
                      {errors.lawfirmId}
                    </p>
                  )}
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">
                    Case Manager
                  </label>
                  <ContactEntitySelect
                    contactType="LawFirm"
                    contactSubtype={caseManagerRoleCode}
                    lawFirmId={form.lawfirmId}
                    requireParent
                    parentHint="Select a law firm first"
                    value={form.caseManagerId}
                    onChange={(v) => updateField("caseManagerId", v)}
                    placeholder="Select case manager..."
                    searchPlaceholder="Search case managers..."
                    allowCreate
                    createLabel="Add Case Manager"
                  />
                </div>
              </>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Case Tracking Notes
            </label>
            <textarea
              value={form.notes}
              onChange={(e) => updateField("notes", e.target.value)}
              placeholder="Brief case notes (optional)"
              rows={3}
              className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary resize-none"
            />
          </div>
        </div>
      </FormModal>

      {showLitigationForm && (
        <LitigationStatusForm
          open={showLitigationForm}
          onClose={() => setShowLitigationForm(false)}
          onSubmitted={(v: DropdownOption) => {
            setLitigationStatus(v);
          }}
        />
      )}
      {duplicateWarning && (
        <Modal
          open={Boolean(duplicateWarning)}
          onClose={closeDuplicateWarning}
          title="Potential Duplicate Case"
          size="md"
          footer={
            <>
              <button
                type="button"
                onClick={closeDuplicateWarning}
                className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
              >
                Close
              </button>
              <button
                type="button"
                onClick={() => viewDuplicateCase(duplicateWarning.matches[0].id)}
                className="text-sm px-4 py-2 rounded-lg text-white bg-primary hover:bg-primary/90"
              >
                View Existing Case
              </button>
            </>
          }
        >
          <div className="space-y-3">
            <p className="text-sm text-gray-700">{duplicateWarning.message}</p>
            <div className="space-y-2">
              {duplicateWarning.matches.map((match) => (
                <button
                  key={match.id}
                  type="button"
                  onClick={() => viewDuplicateCase(match.id)}
                  className="w-full text-left rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 hover:bg-amber-100"
                >
                  <span className="block text-sm font-medium text-gray-900">
                    {match.clientDisplayName ||
                      `${match.clientFirstName} ${match.clientLastName}`.trim()}
                  </span>
                  <span className="block text-xs text-gray-600">
                    {match.caseNumber} | DOB{" "}
                    {formatDisplayDate(match.clientDob)} | DOL{" "}
                    {formatDisplayDate(match.dateOfIncident)}
                  </span>
                </button>
              ))}
            </div>
          </div>
        </Modal>
      )}
    </>
  );
}

function formatDisplayDate(value?: string | null): string {
  if (!value) return "N/A";
  const isoDate = value.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (isoDate) return `${isoDate[2]}/${isoDate[3]}/${isoDate[1]}`;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat("en-US").format(date);
}
