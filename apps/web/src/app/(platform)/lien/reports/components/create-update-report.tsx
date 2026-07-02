"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Modal } from "@/components/lien/modal";
import { reportsService } from "@/lib/reports/reports.service";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { casesService } from "@/lib/cases";
import { lookupService } from "@/lib/lookup";
import { contactsService } from "@/lib/contacts";
import Field from "@/components/lien/field";

const AVAILABLE_COLUMNS = [
  { code: "plaintiff_first_name", label: "Plaintiff First Name" },
  { code: "plaintiff_last_name", label: "Plaintiff Last Name" },
  { code: "case_id", label: "Case ID" },
  { code: "lien_id", label: "Lien ID" },
  { code: "purchase_date", label: "Purchase Date" },
  { code: "purchase_amt", label: "Purchase Amount" },
  { code: "billing_amt", label: "Billing Amount" },
  { code: "date_closed", label: "Date Closed" },
  { code: "returned_amt", label: "Returned Amount" },
  { code: "initial_service_date", label: "Initial Service Date" },
  { code: "lawfirm", label: "Law Firm" },
  { code: "case_type", label: "Case Type" },
  { code: "case_manager", label: "Case Manager" },
  { code: "case_status", label: "Case Status" },
  { code: "date_of_loss", label: "Date of Loss" },
];

const INITIAL_FORM = {
  name: "",
  description: "",
  reportType: "",
  statusView: "ALL",
  lienStatusIds: [],
  purchaseDateFrom: null,
  purchaseDateTo: null,
  closedDateFrom: null,
  closedDateTo: null,
  isBulk: "N",
  plaintiffCaseIds: [],
  lawFirmIds: [],
  attorneyIds: [],
  fundingCompanyIds: [],
  medicalFacilityIds: [],
  caseManagerIds: [],
  medicalProviderIds: [],
  columns: [],
};

type ColsType = {
  code: string;
  label: string;
};

const STEPS = ["Details", "Filters", "Columns"];

export default function CreateUpdateReport({
  mode,
  onClose,
  onSaved,
  template,
  initialData,
}: any) {
  console.log(template, initialData, mode);
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedCols, setSelectedCols] = useState<Array<ColsType>>([]);
  const [available, setAvailable] = useState([]);

  const [leftSearch, setLeftSearch] = useState("");
  const [rightSearch, setRightSearch] = useState("");
  const [form, setForm] = useState(
    initialData ? { ...initialData } : { ...INITIAL_FORM },
  );

  const [data, setData] = useState<any>(
    mode == "create"
      ? {
          reportType: [
            { key: "LIENS", value: "LIENS", label: "LIENS" },
            { key: "CASE", value: "CASE", label: "CASE" },
          ],
          statusView: "ALL",
          lawfirm: [],
          plaintiff: [],
          attorney: [],
          funding: [],
          facility: [],
          provider: [],
          caseManagers: [],
          liensStatus: [],
        }
      : {
          reportType: [
            { key: "LIENS", value: "LIENS", label: "LIENS" },
            { key: "CASE", value: "CASE", label: "CASE" },
          ],
          statusView: "ALL",
          lawfirm: [],
          plaintiff: [],
          attorney: [],
          funding: [],
          facility: [],
          provider: [],
          caseManagers: [],
          liensStatus: [],
        },
  );
  console.log(data);

  const fetchData = useCallback(async () => {
    const [
      caseStatusRes,
      casesRes,
      lawfirmRes,
      fundingRes,
      facilityRes,
      providerRes,
      caseManagersRes,
      liensStatusRes,
    ] = await Promise.allSettled([
      lookupService.getCaseStatus(),
      casesService.getCases(),
      lookupService.getLawfirm(),
      lookupService.getFundingCompany(),
      lookupService.getMedicalFacility(),
      lookupService.getMedicalProviders(),
      contactsService.getContacts({ ContactType: "CaseManager" }),
      lookupService.getLiensStatus(),
    ]);
    setData((prev: any) => ({
      ...prev,
      statusView:
        caseStatusRes.status === "fulfilled"
          ? caseStatusRes.value.items.map((c) => {
              return { key: c.id, value: c.code, label: c.name };
            })
          : [],
      lawfirm:
        lawfirmRes.status === "fulfilled"
          ? lawfirmRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.organization };
            })
          : [],
      plaintiff:
        casesRes.status === "fulfilled"
          ? casesRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.clientName };
            })
          : [],
      attorney: [],
      funding:
        fundingRes.status === "fulfilled"
          ? fundingRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.name };
            })
          : [],
      facility:
        facilityRes.status === "fulfilled"
          ? facilityRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.name };
            })
          : [],
      provider:
        providerRes.status === "fulfilled"
          ? providerRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.name };
            })
          : [],
      caseManagers:
        caseManagersRes.status === "fulfilled"
          ? caseManagersRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.displayName };
            })
          : [],
      liensStatus:
        liensStatusRes.status === "fulfilled"
          ? liensStatusRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.name };
            })
          : [],
    }));

    const filteredSelectedColumns =
      initialData?.config?.columns?.map((c: string) => {
        return {
          code: c,
          label: AVAILABLE_COLUMNS.find((col) => col.code == c)?.label,
        };
      }) ?? [];
    const filteredAvailableColumns =
      AVAILABLE_COLUMNS.filter(
        (availableCol) =>
          !initialData?.config?.columns?.some(
            (selectedCol) => selectedCol === availableCol.code,
          ),
      ) ?? [];
    setSelectedCols(filteredSelectedColumns);
    setAvailable(filteredAvailableColumns);
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const isLastStep = currentStep === STEPS.length - 1;

  const moveToSelected = (col: ColsType) => {
    setAvailable((a) => a.filter((c) => c.code !== col.code));
    setSelectedCols((s) => [...s, col]);
  };

  const moveToAvailable = (col: ColsType) => {
    setSelectedCols((s) => s.filter((c) => c.code !== col.code));
    setAvailable((a) => [...a, col]);
  };

  const selectAll = () => {
    setSelectedCols([...AVAILABLE_COLUMNS]);
    setAvailable([]);
  };

  const resetAll = () => {
    setSelectedCols([]);
    setAvailable([...AVAILABLE_COLUMNS]);
  };

  const handleBackOrCancel = () => {
    if (currentStep > 0) {
      setCurrentStep((s) => s - 1);
    } else {
      onClose();
    }
  };

  const handleNextOrSubmit = async () => {
    if (!isLastStep) {
      setCurrentStep((s) => s + 1);
      return;
    }
    const reportData = await createReportTemplate();
    console.log("Generate report", { selectedCols }, reportData);
    onSaved(reportData);
  };

  const createReportTemplate = async () => {
    const payload = {
      viewBy: form.viewBy,
      reportType: form.reportType,
      statusView: form.statusView ?? [],
      lienStatusIds: form.lienStatusIds ?? [],
      purchaseDateFrom: form.purchaseDateFrom ?? [],
      purchaseDateTo: form.purchaseDateTo ?? null,
      closedDateFrom: form.closedDateFrom ?? null,
      closedDateTo: form.closedDateTo ?? null,
      isBulk: form.isBulk == "true" ? "Y" : "N",
      plaintiffCaseIds: form.plaintiffCaseIds ?? [],
      lawFirmIds: form.lawFirmIds ?? [],
      attorneyIds: form.attorneyIds ?? [],
      fundingCompanyIds: form.fundingCompanyIds ?? [],
      medicalFacilityIds: form.medicalFacilityIds ?? [],
      caseManagerIds: form.caseManagerIds ?? [],
      medicalProviderIds: form.medicalProviderIds ?? [],
      columns: selectedCols.map((c: ColsType) => c.code),
    };
    const reportDataRes = await lienReportsService.generateTemplate({
      ...payload,
      page: "1",
      limit: "10",
    });
    return {
      items: reportDataRes.data.map((c) => {
        return {
          id: c.l_id,
          caseNumber: c.case_id,
          clientName: c.plaintiff_first_name + c.plaintiff_last_name,
          lawFirm: c.lawfirm,
          caseManager: c.case_manager ? c.case_manager : [],
          status: c.case_status,
          accidentType: c.case_type,
          dateOfIncident: c.date_of_loss,
        };
      }),
      summaryTotals: reportDataRes.summaryTotals,
      ...payload,
      config: { columns: selectedCols.map((c: ColsType) => c.code) },
      name: form.name,
      description: form.description,
    };
  };

  const filteredAvailable = available.filter((c) =>
    c.label.toLowerCase().includes(leftSearch.toLowerCase()),
  );

  const filteredSelected = selectedCols?.filter(
    (c) => c.label.toLowerCase().includes(rightSearch.toLowerCase()) ?? [],
  );

  return (
    <Modal
      open={true}
      onClose={onClose}
      title={mode == "create" ? "Create Report" : "Edit Report"}
      subtitle="Configure your report step by step"
      size="lg"
      footer={
        <div className="flex justify-between w-full">
          {/* LEFT BUTTON */}
          <button
            onClick={handleBackOrCancel}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
          >
            {currentStep === 0 ? "Cancel" : "Back"}
          </button>

          {/* RIGHT BUTTON */}
          <button
            onClick={handleNextOrSubmit}
            className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
          >
            {isLastStep ? "Generate" : "Next"}
          </button>
        </div>
      }
    >
      {/* STEP PROGRESS */}
      <div className="relative mb-8 px-4">
        <div className="absolute top-4 left-4 right-4 h-px bg-gray-200" />

        <div
          className="absolute top-4 left-4 h-px bg-primary transition-all duration-300"
          style={{
            width:
              currentStep === 0
                ? "0%"
                : `calc(${(currentStep / (STEPS.length - 1)) * 100}% - 2rem)`,
          }}
        />

        <div className="relative flex justify-between">
          {STEPS.map((step, i) => (
            <div
              key={step}
              className="flex flex-col items-center bg-white px-2"
            >
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                  i <= currentStep
                    ? "bg-primary text-white"
                    : "bg-gray-100 text-gray-400 border border-gray-200"
                }`}
              >
                {i < currentStep ? <i className="ri-check-line" /> : i + 1}
              </div>

              <span
                className={`mt-2 text-xs font-medium ${
                  i <= currentStep ? "text-gray-900" : "text-gray-400"
                }`}
              >
                {step}
              </span>
            </div>
          ))}
        </div>
      </div>

      {/* STEP 1 */}
      {currentStep === 0 && (
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
          <div className="grid grid-cols-1 gap-4">
            <Field
              label="Report Name"
              value={form.name}
              onChange={(v) => setForm({ ...form, name: v })}
              type="text"
            />
            <Field
              label="Description"
              value={form.description}
              onChange={(v) => setForm({ ...form, description: v })}
              type="textarea"
            />
          </div>
        </div>
      )}

      {/* STEP 2 */}
      {currentStep === 1 && (
        <div className="bg-white border border-gray-200 rounded-lg px-5 py-5">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Field
              label="View By"
              value={form.reportType}
              options={data.reportType}
              onChange={(v) => {
                setForm({ ...form, reportType: v });
              }}
              type="select"
            />

            <Field
              label="Status"
              value={form.statusView}
              options={data.statusView ? data.statusView : []}
              placeholder=""
              onChange={(v) => {
                console.log(v);
                setForm({ ...form, statusView: v });
              }}
              type="select"
            />

            <Field
              type="date"
              label="Closed Date"
              value={form.closedDateFrom}
              onChange={(v) => setForm({ ...form, closedDateFrom: v })}
            />
            <Field
              type="date"
              label="Purchase Date"
              value={form.purchaseDateFrom}
              onChange={(v) => setForm({ ...form, purchaseDateFrom: v })}
            />

            <Field
              label="Law Firm"
              value={form.lawFirmIds}
              options={data.lawfirm ? data.lawfirm : []}
              placeholder="Select one or more law firms"
              onChange={(v) =>
                setForm({
                  ...form,
                  lawFirmIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Plaintiff Name"
              value={form.plaintiffCaseIds}
              options={data.plaintiff ? data.plaintiff : []}
              placeholder="Select one or more plaintiffs"
              onChange={(v) =>
                setForm({
                  ...form,
                  plaintiffCaseIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Attorney"
              value={form.attorneyIds}
              options={data.attorney ? data.attorney : []}
              placeholder="Select one or more attorneys"
              onChange={(v) =>
                setForm({
                  ...form,
                  attorneyIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Funding Company"
              value={form.fundingCompanyIds}
              options={data.funding ? data.funding : []}
              placeholder="Select one or more funding companies"
              onChange={(v) =>
                setForm({
                  ...form,
                  fundingCompanyIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Medical Facility"
              value={form.medicalFacilityIds}
              options={data.medicalFacility ? data.medicalFacility : []}
              placeholder="Select one or more facilities"
              onChange={(v) =>
                setForm({
                  ...form,
                  medicalFacilityIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Case Manager"
              value={form.caseManagerIds}
              options={data.caseManagers ? data.caseManagers : []}
              placeholder="Select one or more case managers"
              onChange={(v) =>
                setForm({
                  ...form,
                  caseManagerIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <Field
              label="Medical Provider"
              value={form.medicalProviderIds}
              options={data.medicalProviders ? data.medicalProviders : []}
              placeholder="Select one or more providers"
              onChange={(v) =>
                setForm({
                  ...form,
                  medicalProviderIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />
            <Field
              label="Lien Status"
              value={form.lienStatusIds}
              options={data.liensStatus ? data.liensStatus : []}
              placeholder="Select one or more lien statuses"
              onChange={(v) =>
                setForm({
                  ...form,
                  lienStatusIds: Array.isArray(v) ? v : v ? [v] : [],
                })
              }
              type="select"
              multiple
            />

            <div className="sm:col-span-2">
              <label className="flex items-center gap-3 cursor-pointer">
                <input
                  type="checkbox"
                  className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary"
                  onChange={(v) =>
                    setForm({ ...form, isBulk: v.target.checked ? "Y" : "N" })
                  }
                  value={form.isBulk}
                />
                <div>
                  <p className="text-sm font-medium text-gray-700">BULK</p>
                  <p className="text-xs text-gray-400">Mark as Bulk.</p>
                </div>
              </label>
            </div>
          </div>
        </div>
      )}

      {/* STEP 3 */}
      {currentStep === 2 && (
        <div className="grid grid-cols-2 gap-4">
          {/* LEFT */}
          <div className="border border-gray-200 rounded p-3">
            <div className="flex justify-between text-sm mb-2">
              <span>Available Columns</span>
              <button className="text-xs text-primary" onClick={selectAll}>
                Select All
              </button>
            </div>
            <input
              value={leftSearch}
              onChange={(e) => setLeftSearch(e.target.value)}
              placeholder="Search..."
              className="w-full mb-2 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <div className="space-y-2 max-h-64 overflow-auto">
              {filteredAvailable.map((c) => (
                <div
                  onClick={() => moveToSelected(c)}
                  key={c.code}
                  className="flex justify-between border border-gray-200 p-2 rounded text-sm hover:bg-gray-200"
                >
                  {c.label}
                  <button>→</button>
                </div>
              ))}
            </div>
          </div>

          {/* RIGHT */}
          <div className="border border-gray-200 rounded p-3">
            <div className="flex justify-between text-sm mb-2">
              <span>Selected Columns</span>
              <button onClick={resetAll} className="text-xs text-red-500">
                Reset
              </button>
            </div>
            <input
              value={rightSearch}
              onChange={(e) => setRightSearch(e.target.value)}
              placeholder="Search..."
              className="w-full mb-2 border border-gray-300 rounded px-2 py-1 text-sm"
            />
            <div className="space-y-2 max-h-64 overflow-auto">
              {filteredSelected &&
                filteredSelected.map((c) => (
                  <div
                    onClick={() => moveToAvailable(c)}
                    key={c.code}
                    className="flex justify-between border border-gray-200 p-2 rounded text-sm hover:bg-gray-200"
                  >
                    {c.label}
                    <button>←</button>
                  </div>
                ))}
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}
