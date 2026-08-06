"use client";

import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
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
import { ColumnGroup, ReportColumnOption } from "@/lib/liens/lien-report.types";
import { useLienStore } from "@/stores/lien-store";
import { ApiError } from "@/lib/api-client";
import { useDebounce, useReportFilterOptions } from "@/hooks/use-report";

const INITIAL_FORM = {
  name: "",
  description: "",
  reportType: [],
  statusView: [],
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
  onValid,
}: any) {
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedCols, setSelectedCols] = useState<Array<any>>([]);
  const [available, setAvailable] = useState<Array<any>>([]);
  const [cols, setCols] = useState<any[]>([]);
  const [filteredSelectedCols, setFilteredSelectedCols] = useState<any[]>([]);

  const [searchInput, setSearchInput] = useState<string>("");
  const [searchInputSelected, setSearchInputSelected] = useState<string>("");
  const [searchSelectInput, setSearchSelectInput] = useState<string>("");
  const [filterField, setfilterField] = useState<string>("");

  const [form, setForm] = useState(
    initialData
      ? { ...initialData, ...initialData.config }
      : { ...INITIAL_FORM },
  );
  const [checkedAvailable, setCheckedAvailable] = useState<any>([]);
  const [checkedSelected, setCheckedSelected] = useState<any>([]);
  const [isValid, setIsValid] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  // const [isLoading, setIsLoading] = useState(true);
  const addToast = useLienStore((s) => s.addToast);

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

    const groupedCols: ColumnGroup[] = Object.entries(
      columnGroups as Record<string, unknown>,
    )
      .filter(([key]) => !excludedKeys.has(key))
      .filter(([_, value]) => Array.isArray(value))
      .map(([key, value]) => ({
        key,
        value: value as ReportColumnOption[],
      }));

    setCols(groupedCols);
    setAvailable(groupedCols);

    const hasSelectedCols =
      Array.isArray(form?.config?.columns) && form?.config?.columns.length > 0;

    let selected;

    if (hasSelectedCols) {
      const groupedSelection: Record<string, ReportColumnOption[]> = {};
      form.config.columns.forEach((columnKey: string, index: number) => {
        const section = groupedCols.find((group: ColumnGroup) =>
          group.value.some(
            (item: ReportColumnOption) => item.key === columnKey,
          ),
        );

        if (!section) return;

        const column = section.value.find(
          (item: ReportColumnOption) => item.key === columnKey,
        );

        if (!groupedSelection[section.key]) {
          groupedSelection[section.key] = [];
        }

        groupedSelection[section.key].push({
          ...column,
          sectionKey: section.key,
          sortOrder: index + 1,
        } as ReportColumnOption);
      });

      selected = Object.entries(groupedSelection).map(([key, value]) => ({
        key,
        value,
      }));
    } else {
      let sortOrder = 1;

      const globallyOrderedItems = groupedCols
        .flatMap((section) =>
          (section.value || []).map((item) => ({
            ...item,
            sectionKey: section.key,
          })),
        )
        .filter((item) => item?.isDefault)
        .sort((a, b) => {
          const rawResponse = colsResponse as Record<string, unknown>;

          const defaultColsArray = Array.isArray(rawResponse.defaultColumn)
            ? (rawResponse.defaultColumn as string[])
            : [];

          const indexA = defaultColsArray.indexOf(a.key);
          const indexB = defaultColsArray.indexOf(b.key);

          return (
            (indexA === -1 ? Infinity : indexA) -
            (indexB === -1 ? Infinity : indexB)
          );
        })
        .map((item) => ({
          ...item,
          sortOrder: sortOrder++,
        }));

      // Step 2: Re-group them back into the original format based on the original groupedCols order
      selected = groupedCols
        .map((section) => {
          // Pull out only the items that belong to this specific section
          const sectionItems = globallyOrderedItems.filter(
            (item) => item.sectionKey === section.key,
          );

          return {
            key: section.key,
            value: sectionItems,
          };
        })
        // Filter out any sections that ended up with 0 items
        .filter((section) => section.value.length > 0);
    }
    setSelectedCols(selected);
    setFilteredSelectedCols(selected);
    setCheckedAvailable(
      selected.flatMap((section) =>
        section.value.map((item: ReportColumnOption) => ({
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

        selectedSection.value.forEach((item: ColsType) => {
          const exists = section.value.some(
            (x: ColsType) => x.key === item.key,
          );

          if (!exists) {
            section.value.push({
              ...item,
            });
          }
        });

        // Keep available items ordered
        section.value.sort(
          (a: any, b: any) => (a.sortOrder || 0) - (b.sortOrder || 0),
        );
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

  const debouncedSearch = useDebounce(searchSelectInput, 500);

  const plaintiffFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "plaintiff",
    keyword: debouncedSearch,
    enabled: false,
  });

  const medicalProviderFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "medicalprovider",
    keyword: debouncedSearch,
    enabled: false,
  });

  const lawFirmFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "lawfirm",
    keyword: debouncedSearch,
    enabled: false,
  });

  const fundingCompanyFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "fundingcompany",
    keyword: debouncedSearch,
    enabled: false,
  });

  const medicalFacilityFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "medicalfacility",
    keyword: debouncedSearch,
    enabled: false,
  });

  const caseManagerFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "casemanager",
    keyword: debouncedSearch,
    enabled: false,
  });

  const attorneyFilter = useReportFilterOptions({
    reportType: form.reportType,
    filterField: "attorney",
    keyword: debouncedSearch,
    enabled: false,
  });
  // const { caseManagers } = useDemandLookups({
  //   type: "caseManagers",
  //   pageSize: 10,
  //   search: searchSelectInput,
  // });
  useEffect(() => {
    // fetchData();
    fetchConfig();
  }, [mode]);
  useEffect(() => {
    // fetchData();
    console.log(data);
  }, [data]);

  useEffect(() => {
    if (currentStep == 0) {
      const valid = !!form.name;
      setIsValid(valid);
    }

    if (currentStep == 1) {
      const valid =
        form.reportType.length > 0 && form.reportType == "CASE"
          ? typeof form.statusView === "string"
          : typeof form.lienStatusIds === "string";

      setIsValid(valid);
    }
  }, [currentStep, form]);

  const isLastStep = currentStep === STEPS.length - 1;

  const moveCheckedToSelected = () => {
    setSelectedCols((prev) => {
      const updated = [...prev];

      // Get current highest sortOrder
      let nextSortOrder =
        Math.max(
          0,
          ...updated.flatMap((section) =>
            section.value.map((item: any) => item.sortOrder || 0),
          ),
        ) + 1;

      checkedAvailable.forEach((item: any) => {
        let section = updated.find((s) => s.key === item.sectionKey);

        if (!section) {
          section = {
            key: item.sectionKey,
            value: [],
          };

          updated.push(section);
        }

        const exists = section.value.some((x: any) => x.key === item.key);

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
            (item: any) =>
              !checkedSelected.some(
                (checked: any) =>
                  checked.key === item.key &&
                  checked.sectionKey === section.key,
              ),
          ),
        }))
        .filter((section) => section.value.length > 0),
    );

    // remove moved items from selected checks
    setCheckedSelected((prev: any) =>
      prev.filter(
        (checked: any) =>
          !checkedSelected.some(
            (moved: any) =>
              moved.key === checked.key &&
              moved.sectionKey === checked.sectionKey,
          ),
      ),
    );

    // uncheck them in available
    setCheckedAvailable((prev: any) =>
      prev.filter(
        (checked: any) =>
          !checkedSelected.some(
            (moved: any) =>
              moved.key === checked.key &&
              moved.sectionKey === checked.sectionKey,
          ),
      ),
    );
  };

  const selectAll = () => {
    const allAvailableItems = available.flatMap((section) =>
      section.value.map((item: any) => ({
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

    const section = selectedCols.find((section) => section.key === sectionKey);

    const item = section?.value.find((item: any) => item.key === itemKey);

    if (!item) {
      return;
    }

    setActiveDragItem({
      sectionKey,
      item,
    });
  };

  const handleDragEnd = ({ active, over }: DragEndEvent) => {
    setActiveDragItem(null);

    if (!over || active.id === over.id) return;

    setSelectedCols((prev) => {
      const oldIndex = flattenedItems.findIndex(
        (item) => `${item.sectionKey}__${item.key}` === active.id,
      );

      const newIndex = flattenedItems.findIndex(
        (item) => `${item.sectionKey}__${item.key}` === over.id,
      );

      if (oldIndex === -1 || newIndex === -1) return prev;

      const reordered = arrayMove(flattenedItems, oldIndex, newIndex).map(
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
    setSubmitting(true);

    const reportData = await createReportTemplate();
    resetTemplateData();
    onSaved(reportData);
  };
  const resetTemplateData = () => {
    setForm(INITIAL_FORM);
    setIsValid(false);
    setSelectedCols([]);
    setAvailable([]);
    setCols([]);
    setFilteredSelectedCols([]);
  };

  const createReportTemplate = async () => {
    const cols = flattenedItems.flatMap((item: any) => item.key);
    try {
      const payload = {
        viewBy: form.reportType,
        reportType: form.reportType,
        statusView: form.statusView ?? "",
        lienStatusIds: form.lienStatusIds ?? "",
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
        page: 1,
        limit: 10,
      });
    } catch {
    } finally {
      setTimeout(() => {
        onSave();
      }, 100);
    }
  };

  const onSave = async () => {
    const cols = flattenedItems.flatMap((item: any) => item.key);

    try {
      const response = await lienReportsService.createReports({
        name: form.name,
        description: form.description ?? form.reportDescription,
        config: { columns: cols },
        attorneyIds: form.attorneyIds,
        caseManagerIds: form.caseManagerIds,
        closedDateFrom: null,
        closedDateTo: null,
        fundingCompanyIds: form.fundingCompanyIds,
        isBulk: form.isBulk,
        lawFirmIds: form.lawFirmIds,
        lienStatusIds: form.lienStatusIds,
        medicalFacilityIds: form.medicalFacilityIds,
        medicalProviderIds: form.medicalProviderIds,
        plaintiffCaseIds: form.plaintiffCaseIds,
        purchaseDateFrom: form.purchaseDateFrom,
        purchaseDateTo: form.purchaseDateTo,
        reportType: form.reportType,
        statusView: form.statusView,
      });
      if (response) {
        addToast({
          type: "success",
          title: "Report Template Saved",
        });
        onSaved();
      }
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save report";
      addToast({ type: "error", title: "Save Failed", description: message });
    } finally {
      setSubmitting(false);
    }
  };

  const flattenedItems = useMemo(
    () =>
      selectedCols
        .flatMap((section) =>
          section.value.map((item: any) => ({
            ...item,
            sectionKey: section.key,
          })),
        )
        .sort((a, b) => a.sortOrder - b.sortOrder),
    [selectedCols],
  );

  const handleSearch = (value: any) => {
    setSearchInput(value);

    if (!value) {
      setAvailable(cols);
      return;
    }

    const filtered = available
      .map((group) => ({
        key: group.key,
        value: group.value.filter((column: any) =>
          column.label?.toLowerCase().includes(value.toLowerCase()),
        ),
      }))
      .filter((group) => group.value.length > 0);

    setAvailable(filtered);
  };
  const handleSearchSelected = (value: any) => {
    setSearchInputSelected(value);

    if (!value) {
      setSelectedCols(filteredSelectedCols);
      return;
    }

    const filtered = selectedCols
      .map((group) => ({
        key: group.key,
        value: group.value.filter((column: any) =>
          column.label?.toLowerCase().includes(value.toLowerCase()),
        ),
      }))
      .filter((group) => group.value.length > 0);

    setSelectedCols(filtered);
  };
  return (
    <Modal
      open={true}
      onClose={onClose}
      title={mode == "create" ? "Create template" : "Edit template"}
      subtitle="Configure your template step by step"
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
            disabled={!isValid || submitting}
            onClick={handleNextOrSubmit}
            className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:bg-primary/70"
          >
            {submitting ? "Saving..." : isLastStep ? "Save" : "Next"}
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
                setForm({
                  ...form,
                  reportType: v,
                  lienStatusIds: "",
                  statusView: "",
                });
              }}
              type="select"
            />

            {form.reportType == "CASE" ? (
              <Field
                label="Status"
                required
                value={form.statusView}
                options={data.statusView ?? []}
                placeholder="Select one status"
                onChange={(v: string) => {
                  setForm({ ...form, statusView: v });
                }}
                type="select"
              />
            ) : (
              <Field
                label="Lien Status"
                required
                value={form.lienStatusIds}
                options={data.lienStatus ?? []}
                placeholder="Select one status"
                onChange={(v: string) =>
                  setForm({
                    ...form,
                    lienStatusIds: v,
                  })
                }
                type="select"
                onOpen={() => setfilterField("lienStatus")}
                onSearchChange={(e) => {
                  setSearchSelectInput(e);
                }}
              />
            )}

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
              options={lawFirmFilter.options}
              placeholder="Select law firms"
              onChange={(values) =>
                setForm({
                  ...form,
                  lawFirmIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                lawFirmFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={lawFirmFilter.isLoadingFilter}
            />

            <Field
              label="Plaintiff Name"
              value={form.plaintiffCaseIds}
              options={plaintiffFilter.options}
              placeholder="Select one or more plaintiffs"
              onChange={(values) =>
                setForm({
                  ...form,
                  plaintiffCaseIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                plaintiffFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={plaintiffFilter.isLoadingFilter}
            />

            <Field
              label="Attorney"
              value={form.attorneyIds}
              options={attorneyFilter.options}
              placeholder="Select attorneys"
              onChange={(values) =>
                setForm({
                  ...form,
                  attorneyIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                attorneyFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={attorneyFilter.isLoadingFilter}
            />

            <Field
              label="Funding Company"
              value={form.fundingCompanyIds}
              options={fundingCompanyFilter.options}
              placeholder="Select funding companies"
              onChange={(values) =>
                setForm({
                  ...form,
                  fundingCompanyIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                fundingCompanyFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={fundingCompanyFilter.isLoadingFilter}
            />
            <Field
              label="Medical Facility"
              value={form.medicalFacilityIds}
              options={medicalFacilityFilter.options}
              placeholder="Select facilities"
              onChange={(values) =>
                setForm({
                  ...form,
                  medicalFacilityIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                medicalFacilityFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={medicalFacilityFilter.isLoadingFilter}
            />

            <Field
              label="Case Manager"
              value={form.caseManagerIds}
              options={caseManagerFilter.options}
              placeholder="Select case managers"
              onChange={(values) =>
                setForm({
                  ...form,
                  caseManagerIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                caseManagerFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={caseManagerFilter.isLoadingFilter}
            />
            <Field
              label="Medical Provider"
              value={form.medicalProviderIds}
              options={medicalProviderFilter.options}
              placeholder="Select providers"
              onChange={(values) =>
                setForm({
                  ...form,
                  medicalProviderIds: values,
                })
              }
              type="select"
              multiple
              onOpen={() => {
                setSearchSelectInput("");
                medicalProviderFilter.refetch();
              }}
              onSearchChange={setSearchSelectInput}
              isLoading={medicalProviderFilter.isLoadingFilter}
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
        <div className="flex justify-evenly gap-2">
          {/* LEFT */}

          <div className="space-y-4 flex-1 border-1 border-gray-400 rounded-lg p-4">
            <div className="min-h-[100px]">
              <div className="flex justify-between items-center mb-2">
                <p className="font-medium text-sm">Available Columns</p>

                <button
                  type="button"
                  onClick={selectAll}
                  className="text-sm px-3 py-1.5 rounded bg-blue-500 text-white
                 text-sm
                  px-3 py-1.5
                  rounded-md
                  border
                "
                >
                  Select All
                </button>
              </div>
              <div>
                <Field
                  label="Search"
                  value={searchInput}
                  onChange={handleSearch}
                  type="text"
                />
              </div>
            </div>

            <div className="overflow-auto max-h-64">
              {available.map((section) => (
                <div key={section.key}>
                  <div className="mb-2 border-b pb-1">
                    <p className="font-medium text-sm">
                      {categoryDescription(section.key)}
                    </p>
                  </div>

                  <div className="space-y-1">
                    {section.value.map((item: any) => {
                      return (
                        <label
                          key={item.key}
                          className="flex items-center gap-2 rounded px-2 py-1 hover:bg-gray-100 cursor-pointer"
                        >
                          <input
                            type="checkbox"
                            checked={checkedAvailable.some(
                              (x: any) =>
                                x.key === item.key &&
                                x.sectionKey === section.key,
                            )}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setCheckedAvailable((prev: any) => [
                                  ...prev,
                                  {
                                    ...item,
                                    sectionKey: section.key,
                                  },
                                ]);
                              } else {
                                setCheckedAvailable((prev: any) =>
                                  prev.filter(
                                    (x: any) =>
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
          </div>

          {/* MIDDLE BUTTONS */}
          <div className="flex justify-center flex-col max-w-[100px]">
            <button
              onClick={moveCheckedToSelected}
              className="px-3 py-2 m-1.5 border rounded bg-primary text-white"
            >
              →
            </button>
            <button
              onClick={moveCheckedToAvailable}
              className="px-3 py-2 m-1.5 border rounded bg-primary text-white"
            >
              ←
            </button>
          </div>

          {/* RIGHT */}
          <div className="space-y-4 flex-1 border-1 border-gray-400 rounded-lg p-4">
            <div className="space-y-4">
              <div className="min-h-[100px]">
                <div className="flex justify-between items-center mb-2">
                  <p className="font-medium text-sm">Selected Columns</p>

                  <button
                    type="button"
                    onClick={resetAll}
                    className="text-sm px-3 py-1.5 rounded bg-white
                 text-sm
                  px-3 py-1.5
                  rounded-md
                  border
                "
                  >
                    Reset All
                  </button>
                </div>
                <div>
                  <Field
                    label="Search"
                    value={searchInputSelected}
                    onChange={handleSearchSelected}
                    type="text"
                  />
                </div>
              </div>
            </div>
            <div className="max-h-64 mt-6 overflow-y-auto">
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
              </DndContext>
            </div>
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
}: any) => {
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
    (x: any) => x.key === item.key && x.sectionKey === sectionKey,
  );

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="
        flex items-center gap-3
        rounded-lg
        bg-white
        px-3 py-2
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
            setCheckedSelected((prev: any) => [
              ...prev,
              {
                ...item,
                sectionKey,
              },
            ]);
          } else {
            setCheckedSelected((prev: any) =>
              prev.filter(
                (x: any) =>
                  !(x.key === item.key && x.sectionKey === sectionKey),
              ),
            );
          }
        }}
      />

      <span className="flex-1 text-sm">{item.label}</span>
    </div>
  );
};
