"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import { Modal } from "@/components/lien/modal";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { casesService } from "@/lib/cases";
import { lookupService } from "@/lib/lookup";
import { contactsService } from "@/lib/contacts";
import Field from "@/components/lien/field";
import { GripVertical } from "lucide-react";
import {
  DndContext,
  closestCenter,
  DragOverlay,
  PointerSensor,
  useSensor,
  useSensors,
  DragStartEvent,
  DragEndEvent,
} from "@dnd-kit/core";

import {
  SortableContext,
  verticalListSortingStrategy,
  useSortable,
  arrayMove,
} from "@dnd-kit/sortable";

import { CSS } from "@dnd-kit/utilities";
import { useContacts } from "@/hooks/use-contacts";

const INITIAL_FORM = {
  name: "",
  description: "",
  reportType: "",
  statusView: "",
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
  key: string;
  value: string;
};



const STEPS = ["Details", "Filters", "Columns"];

export default function CreateUpdateReport({
  mode,
  onClose,
  onSaved,
  initialData,
  onValid
}: any) {
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedCols, setSelectedCols] = useState<Array<any>>([]);
  const [available, setAvailable] = useState<Array<any>>([]);
  const [cols, setCols] = useState<any[]>([]);
  const [searchInput, setSearchInput] = useState<string>("")
  const [form, setForm] = useState(
    initialData ? { ...initialData } : { ...INITIAL_FORM },
  );

  const [checkedAvailable, setCheckedAvailable] = useState<any>([]);
  const [checkedSelected, setCheckedSelected] = useState<any>([]);
  const [isValid, setIsValid] = useState(false);

  function categoryDescription(category: string) {
    let res = category;
    switch (category) {
      case "liensInfo":
        res = "Lien Information";
        break;
      case "settlementInfo":
        res = "Settlement Information";
        break;
      case "procedureInfo":
        res = "Procedure Information";
        break;
      case "caseInfo":
        res = "Case Information";
        break;
      case "caseTrackingInfo":
        res = "Case Tracking Information";
        break;
      case "plaintiffInfo":
        res = "Plaintiff Information";
        break;
    }
    return res;
  }

  const [activeDragItem, setActiveDragItem] = useState<any>(null);
  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 5,
      },
    }),
  );
  const fetchConfig = async () => {
  const colsResponse = await lienReportsService.getColumns(form.reportType);

  const { ...columnGroups } = colsResponse;

  const excludedKeys = new Set([
    "isSuccess",
    "message",
    "reportType",
    "data",
    "defaultColumn",
  ]);

  const groupedCols = Object.entries(columnGroups)
    .filter(([key]) => !excludedKeys.has(key))
    .filter(([_, value]) => Array.isArray(value))
    .map(([key, value]) => ({
      key,
      value,
    }));

  setCols(groupedCols);
  setAvailable(groupedCols);

  const hasSelectedCols =
    Array.isArray(form?.config?.columns) && form?.config?.columns.length > 0;

  let selected;

  if (hasSelectedCols) {
    const groupedSelection:any = {};

    form.config.columns.forEach((columnKey:string, index:number) => {
      const section = groupedCols.find((group) =>
        group.value.some((item:ColsType) => item.key === columnKey),
      );

      if (!section) return;

      const column = section.value.find((item:ColsType) => item.key === columnKey);

      if (!groupedSelection[section.key]) {
        groupedSelection[section.key] = [];
      }

      groupedSelection[section.key].push({
        ...column,
        sectionKey: section.key,
        sortOrder: index + 1,
      });
    });

    selected = Object.entries(groupedSelection).map(([key, value]) => ({
      key,
      value,
    }));
  } else {
    let sortOrder = 1;

    selected = groupedCols
      .map((section) => ({
        key: section.key,
        value: section.value
          .filter((item:any) => item.isDefault)
          .map((item:any) => ({
            ...item,
            sectionKey: section.key,
            sortOrder: sortOrder++,
          })),
      }))
      .filter((section) => section.value.length > 0);
  }

  setSelectedCols(selected);

  setCheckedAvailable(
    selected.flatMap((section) =>
      section.value.map((item:ColsType) => ({
        ...item,
      })),
    ),
  );
};
  const resetAll = () => {
    setAvailable((prev) => {
      const updated = [...prev];

      selectedCols.forEach((selectedSection) => {
        let section = updated.find((s) => s.key === selectedSection.key);

        if (!section) {
          section = {
            key: selectedSection.key,
            value: [],
          };

          updated.push(section);
        }

        selectedSection.value.forEach((item:ColsType) => {
          const exists = section.value.some((x:ColsType) => x.key === item.key);

          if (!exists) {
            section.value.push({
              ...item,
            });
          }
        });

        // Keep available items ordered
        section.value.sort((a:any, b:any) => (a.sortOrder || 0) - (b.sortOrder || 0));
      });

      return updated;
    });

    setSelectedCols([]);
    setCheckedAvailable([]);
  };
  const [data, setData] = useState<any>(
    mode == "create"
      ? {
          reportType: [
            { key: "LIENS", value: "LIENS", label: "LIENS" },
            { key: "CASE", value: "CASE", label: "CASE" },
          ],
          statusView: [],
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
          statusView: [],
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
      contactsService.getContacts({"ContactType":"LawFirm"}),
      contactsService.getContacts({"ContactType":"FundingCompany"}),
      contactsService.getContacts({"ContactType":"MedicalFacility"}),
      contactsService.getContacts({"ContactType":"Provider"}),
      contactsService.getCaseManagers(),
      lookupService.getLiensStatus(),
    ]);
    setData((prev: any) => ({
      ...prev,
      statusView:
        caseStatusRes.status === "fulfilled"
          ? [{ key: "all", value: "", label: "All" },
            ...caseStatusRes.value.items.map((c) => {
              return { key: c.id, value: c.code, label: c.name };
            })]: [{ key: "all", value: "", label: "All" }],
      lawfirm:
        lawfirmRes.status === "fulfilled"
          ? lawfirmRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.displayName };
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
              return { key: c.id, value: c.id, label: c.displayName };
            })
          : [],
      medicalFacility:
        facilityRes.status === "fulfilled"
          ? facilityRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.displayName };
            })
          : [],
      medicalProviders:
        providerRes.status === "fulfilled"
          ? providerRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.displayName };
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
          ? [{ key: "all", value: "all", label: "All" },
             ...liensStatusRes.value.items.map((c) => {
              return { key: c.id, value: c.id, label: c.name };
            })]
          : [{ key: "all", value: "all", label: "All" }],
    }));
  }, []);

  useEffect(() => {
    fetchData();
    fetchConfig();
  }, [fetchData, mode]);

  useEffect(()=>{
     if(currentStep == 0){
    const valid = !!form.name
      setIsValid(valid)  
    }
    if(currentStep == 1){
      const valid = !!form.reportType
      setIsValid(valid)  
    }
  },[currentStep, form])

  const isLastStep = currentStep === STEPS.length - 1;

  const moveCheckedToSelected = () => {
    setSelectedCols((prev) => {
      const updated = [...prev];

      // Get current highest sortOrder
      let nextSortOrder =
        Math.max(
          0,
          ...updated.flatMap((section) =>
            section.value.map((item:any) => item.sortOrder || 0),
          ),
        ) + 1;

      checkedAvailable.forEach((item:any) => {
        let section = updated.find((s) => s.key === item.sectionKey);

        if (!section) {
          section = {
            key: item.sectionKey,
            value: [],
          };

          updated.push(section);
        }

        const exists = section.value.some((x:any) => x.key === item.key);

        if (!exists) {
          section.value.push({
            ...item,
            sortOrder: nextSortOrder++,
          });
        }
      });

      return updated;
    });
  };
  const moveCheckedToAvailable = () => {
    setSelectedCols((prev) =>
      prev
        .map((section) => ({
          ...section,
          value: section.value.filter(
            (item:any) =>
              !checkedSelected.some(
                (checked:any) =>
                  checked.key === item.key &&
                  checked.sectionKey === section.key,
              ),
          ),
        }))
        .filter((section) => section.value.length > 0),
    );

    // remove moved items from selected checks
    setCheckedSelected((prev:any) =>
      prev.filter(
        (checked:any) =>
          !checkedSelected.some(
            (moved:any) =>
              moved.key === checked.key &&
              moved.sectionKey === checked.sectionKey,
          ),
      ),
    );

    // uncheck them in available
    setCheckedAvailable((prev:any) =>
      prev.filter(
        (checked:any) =>
          !checkedSelected.some(
            (moved:any) =>
              moved.key === checked.key &&
              moved.sectionKey === checked.sectionKey,
          ),
      ),
    );
  };

  const selectAll = () => {
    const allAvailableItems = available.flatMap((section) =>
      section.value.map((item:any) => ({
        ...item,
        sectionKey: section.key,
      })),
    );

    setCheckedAvailable(allAvailableItems);
  };

const handleDragStart = ({ active }: DragStartEvent) => {
  const [sectionKey, itemKey] = String(active.id).split("__");

  if (!sectionKey || !itemKey) {
    return;
  }

  const section = selectedCols.find(
    (section) => section.key === sectionKey
  );

  const item = section?.value.find((item:any) => item.key === itemKey);

  if (!item) {
    return;
  }

  setActiveDragItem({
    sectionKey,
    item,
  });
};

  const handleDragEnd = ({ active, over }:DragEndEvent) => {
    setActiveDragItem(null);

    if (!over || active.id === over.id) return;

    setSelectedCols((prev) => {
      const flattened = prev.flatMap((section) =>
        section.value.map((item:any) => ({
          ...item,
          sectionKey: section.key,
        })),
      );

      const oldIndex = flattened.findIndex(
        (item) => `${item.sectionKey}__${item.key}` === active.id,
      );

      const newIndex = flattened.findIndex(
        (item) => `${item.sectionKey}__${item.key}` === over.id,
      );

      if (oldIndex === -1 || newIndex === -1) return prev;

      const reordered = arrayMove(flattened, oldIndex, newIndex).map(
        (item, index) => ({
          ...item,
          sortOrder: index + 1,
        }),
      );

      // Rebuild grouped structure
      return prev.map((section) => ({
        ...section,
        value: reordered
          .filter((item) => item.sectionKey === section.key)
          .map(({ sectionKey, ...item }) => item),
      }));
    });
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
    console.log(reportData);
      onSaved(reportData);
  };

  const createReportTemplate = async () => {
    const cols = selectedCols.flatMap((section) =>
      section.value.map((item:any) => item.key),
    );
    const payload = {
      viewBy: form.viewBy ?? form.reportType,
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
      columns: cols,
    };
    const reportDataRes = await lienReportsService.generateTemplate({
      ...payload,
      page: "1",
      limit: "10",
    });
    const reportRows = Array.isArray(reportDataRes.data)
      ? reportDataRes.data
      : [];
    return {
      data: reportDataRes.data,
      summaryTotals: reportDataRes.summaryTotals,
      ...payload,
      config: { columns: cols },
      name: form.name,
      description: form.description,
    };
  };

  const flattenedItems = selectedCols
    .flatMap((section) =>
      section.value.map((item:any) => ({
        ...item,
        sectionKey: section.key,
      })),
    )
    .sort((a, b) => a.sortOrder - b.sortOrder);

    const handleSearch = (value:any) => {
        setSearchInput(value)

  if (!value) {
    setAvailable(cols);
    return;
  }


  const filtered = available
    .map((group) => ({
      key: group.key,
      value: group.value.filter((column:any) =>
        column.label?.toLowerCase().includes(value.toLowerCase())
      ),
    }))
    .filter((group) => group.value.length > 0);

  setAvailable(filtered);
};
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
            disabled={!isValid}
            onClick={handleNextOrSubmit}
            className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:bg-primary/70"
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
              required
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
              required
              value={form.reportType}
              options={data.reportType}
              onChange={(v: string) => {
                setForm({ ...form, reportType: v });
              }}
              type="select"
            />

            <Field
              label="Status"
              required
              value={form.statusView}
              options={data.statusView ? data.statusView : []}
              placeholder=""
              onChange={(v: string) => {
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
        <div className="flex justify-evenly items-center gap-4">
          {/* LEFT */}

          <div className="space-y-4 flex-1 max-h-64 overflow-auto">
            <div className="flex justify-between items-center mb-2">
              <p className="font-medium text-sm">Available Columns</p>

              <button
                type="button"
                onClick={selectAll}
                className="text-sm px-3 py-1 rounded bg-blue-500 text-white"
              >
                Select All
              </button>

              <Field
                label="Search"
                value={searchInput}
                onChange={handleSearch}
                type="text"
              />
            </div>
            {available.map((section) => (
              <div key={section.key}>
                <div className="mb-2 border-b pb-1">
                  <p className="font-medium text-sm">
                    {categoryDescription(section.key)}
                  </p>
                </div>

                <div className="space-y-1">
                  {section.value.map((item:any) => {

                    return (
                      <label
                        key={item.key}
                        className="flex items-center gap-2 rounded px-2 py-1 hover:bg-gray-100 cursor-pointer"
                      >
                        <input
                          type="checkbox"
                          checked={checkedAvailable.some(
                            (x:any) =>
                              x.key === item.key &&
                              x.sectionKey === section.key,
                          )}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setCheckedAvailable((prev:any) => [
                                ...prev,
                                {
                                  ...item,
                                  sectionKey: section.key,
                                },
                              ]);
                            } else {
                              setCheckedAvailable((prev:any) =>
                                prev.filter(
                                  (x:any) =>
                                    !(
                                      x.key === item.key &&
                                      x.sectionKey === section.key
                                    ),
                                ),
                              );
                            }
                          }}
                        />
                        <span className="text-sm">{item.label}</span>
                      </label>
                    );
                  })}
                </div>
              </div>
            ))}
          </div>

          {/* MIDDLE BUTTONS */}
          <div className="flex  flex-col max-w-[100px]">
            <button
              onClick={moveCheckedToSelected}
              className="px-3 py-2 border rounded"
            >
              →
            </button>
            <button
              onClick={moveCheckedToAvailable}
              className="px-3 py-2 border rounded"
            >
              ←
            </button>
          </div>

          {/* RIGHT */}
          <div className="max-h-64  overflow-y-auto flex-1 rounded-lg">
            <div className="flex items-center justify-between mb-3">
              <p className="font-medium text-sm">Selected Columns</p>

              <button
                type="button"
                onClick={resetAll}
                className="
                  text-sm
                  px-3 py-1.5
                  rounded-md
                  border
                  bg-white
                  hover:bg-gray-50
                "
              >
                Reset All
              </button>
            </div>
            <DndContext
              sensors={sensors}
              collisionDetection={closestCenter}
              onDragStart={handleDragStart}
              onDragEnd={handleDragEnd}
              onDragCancel={() => setActiveDragItem(null)}
            >
              <SortableContext
                items={flattenedItems.map(
                  (item) => `${item.sectionKey}__${item.key}`,
                )}
                strategy={verticalListSortingStrategy}
              >
                {flattenedItems.map((item) => (
                  <SortableSelectedRow
                    key={`${item.sectionKey}__${item.key}`}
                    item={item}
                    sectionKey={item.sectionKey}
                    checkedSelected={checkedSelected}
                    setCheckedSelected={setCheckedSelected}
                    globalIndex={item.sortOrder}
                  />
                ))}
              </SortableContext>

              <DragOverlay>
                {activeDragItem ? (
                  <div className="rounded-lg border bg-white px-4 py-2 shadow-2xl">
                    {activeDragItem.item.label}
                  </div>
                ) : null}
              </DragOverlay>
            </DndContext>
          </div>
        </div>
      )}
    </Modal>
  );
}
const SortableSelectedRow = ({
  item,
  sectionKey,
  checkedSelected,
  setCheckedSelected,
  globalIndex,
}:any) => {
  const dragId = `${sectionKey}__${item.key}`;

  const { attributes, listeners, setNodeRef, transform, transition } =
    useSortable({
      id: dragId,
    });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  };

  const isChecked = checkedSelected.some(
    (x:any) => x.key === item.key && x.sectionKey === sectionKey,
  );

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="
        flex items-center gap-3
        rounded-lg
        border
        bg-white
        px-3 py-2
        shadow-sm
      "
    >
      {/* Drag handle */}
      <button
        {...attributes}
        {...listeners}
        className="
          cursor-grab
          active:cursor-grabbing
          text-gray-400
        "
      >
        <GripVertical className="w-5 h-5" />
      </button>

      <div className="text-xs text-white bg-primary p-1 px-2 rounded">
        {globalIndex}
      </div>

      {/* Select for moving back */}
      <input
        type="checkbox"
        checked={isChecked}
        onChange={(e) => {
          if (e.target.checked) {
            setCheckedSelected((prev:any) => [
              ...prev,
              {
                ...item,
                sectionKey,
              },
            ]);
          } else {
            setCheckedSelected((prev:any) =>
              prev.filter(
                (x:any) => !(x.key === item.key && x.sectionKey === sectionKey),
              ),
            );
          }
        }}
      />

      <span className="flex-1 text-sm">{item.label}</span>
    </div>
  );
};
