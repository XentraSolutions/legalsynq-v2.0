import { useCallback, useEffect, useState } from "react";
import { PageHeader } from "@/components/lien/page-header";
import UploadDocumentComponent from "@/components/lien/upload-document";
import { batchService } from "@/lib/batch/batch.service";
import { dateConverter } from "@/lib/cases/cases.mapper";
import Field from "@/components/lien/field";
import { ApiError } from "@/lib/api-client";
import { ActionMenu } from "@/components/lien/action-menu";
import BatchUploadDocumentComponent from "./components/batch-upload-document";
import DataMappingComponent from "./components/data-mapping";
import DataValidationComponent from "./components/data-validaton";
import ImportCompleteComponent from "./components/import-complete";
import { useLienStore } from "@/stores/lien-store";
import { TemplateItem } from "@/lib/batch/batch.types";

export const dynamic = "force-dynamic";

const STEPS = ["Upload File", "Map Fields", "Validate", "Import"];
type BatchUploadComponentProps = {
  action: "create" | "edit";
  data: any;
};

export default function BatchUploadComponent({
  action,
  data,
}: BatchUploadComponentProps) {
  const addToast = useLienStore((s) => s.addToast);

  const [currentStep, setCurrentStep] = useState(action == "create" ? 0 : 1);
  const [totalImports, setTotalImport] = useState<number>(0);
  const [templateData, setTemplateData] = useState<TemplateItem>();

  const [template, setTemplate] = useState<{
    columns: string[];
    tableData: Record<string, unknown>[];
    id: string;
    batchUploadId: string;
    caseId: string;
  }>({
    columns: [],
    tableData: [],
    id: "",
    batchUploadId: "",
    caseId: "",
  });
  const [validations, setValidations] = useState<{
    isSuccess?: boolean;
    message?: string;
    totalRows?: number;
    successCount?: number;
    failedCount?: number;
    data?: Array<{
      id: string;
      batchUploadId: string;
      row: number;
      status: string;
      reason: string;
      data: Record<string, unknown>;
    }>;
  } | null>(null);

  const templateList = [
    {
      id: "1",
      code: "ADD_LIENS_EXISTING_CASE",
      name: "Add Liens to Existing Case",
      icon: "ri-stack-line",
      color: "text-indigo-600",
    },
    {
      id: "2",
      code: "ADD_PAYMENTS_EXISTING_LIENS",
      name: "Add Payments for Existing Liens",
      icon: "ri-folder-open-line",
      color: "text-blue-600",
    },
    {
      id: "3",
      code: "INITIAL_CASE_IMPORT",
      name: "Initial Case Import",
      icon: "ri-contacts-book-line",
      color: "text-teal-600",
    },
    {
      id: "4",
      code: "UPDATE_CASE_TRACKING_STATUS",
      name: "Update Case Tracking Status",
      icon: "ri-route-line",
      color: "text-cyan-600",
    },
  ];

  const fetchDataContext = useCallback(
    async (id: string) => {
      const dataContext = await batchService.dataContext({
        id: id,
        page: 1,
        limit: 20,
      });

      const rows = Array.isArray(dataContext?.data) ? dataContext.data : [];
      const excludedColumns = new Set(["id", "row", "status", "reason"]);
      const columns = rows.length
        ? Object.keys(rows[0]).filter((key) => !excludedColumns.has(key))
        : [];

      setTemplate((prev) => ({
        ...prev,
        columns,
        id: id,
        tableData: rows,
        batchUploadId: dataContext.id,
        caseId: dataContext.caseId,
      }));
    },
    [template.id],
  );

  const download = async (code: string) => {
    const response = await batchService.download(code);

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  const upload = useCallback(async () => {
    const formData = new FormData();
    if (!templateData?.file) return;
    formData.append("File", templateData?.file);
    formData.append("Label", templateData?.templateLabel ?? "");
    formData.append("Template", templateData?.template ?? "");
    formData.append("date", dateConverter(new Date().toDateString()));
    formData.append("caseId", templateData?.caseId ?? "");

    const response = await batchService.upload(formData);
    setTemplate((prev) => ({ ...prev, id: response.id }));
    setTimeout(() => {
      fetchDataContext(response.id);
    }, 1000);
  }, [templateData]);

  const process = async () => {
    try {
      const response = await batchService.process({
        batchUploadId: template.id,
        templateId: templateData?.template ?? "INITIAL_CASE_IMPORT",
        caseId: template.caseId,
      });

      setValidations(response);
      return response;
    } catch (err) {
      if (err instanceof ApiError) {
        console.log(err);
        addToast({
          type: "error",
          title: "Process Failed",
          description: "",
        });
      }
    }
  };

  const importBatch = async () => {
    if (!templateData?.file) return;

    const dataContextLines = [
      template.columns.join(","),
      ...template.tableData.map((row) =>
        template.columns
          .map((column) => {
            const value = row?.[column];
            return value === null || value === undefined ? "" : String(value);
          })
          .join(","),
      ),
    ];

    const importPayload = {
      label: templateData.templateLabel || "Case tracking import",
      template: templateData.template ?? "",
      caseId: template.caseId || "",
      file: templateData.file.name || "tracking.csv",
      date: dateConverter(new Date().toDateString()),
      rows: template.tableData.length,
      dataContext: dataContextLines.join("\n"),
    };

    try {
      const response = await batchService.createBatch(importPayload);

      setTotalImport(response.createdCount);
    } catch (err) {
      if (err instanceof ApiError) {
        console.log(err);
        addToast({
          type: "error",
          title: "Import Failed",
          description: err?.message,
        });
      }
    }
  };

  const nextStep = async () => {
    if (currentStep == 0) {
      await upload();
    }
    if (currentStep == 1) {
      await process();
    }
    if (currentStep == 2) {
      await importBatch();
    }
    setCurrentStep(Math.min(STEPS.length - 1, currentStep + 1));
  };

  const getDetails = useCallback(async (id: string) => {
    try {
      const result = await batchService.getDetails(id);
    } catch (err) {
      if (err instanceof ApiError) {
        console.log(err);
      }
    } finally {
      //   setLoading(false);
    }
  }, []);

  const removeDetails = useCallback(
    async (id: string) => {
      try {
        await batchService.deleteDetails(id);
        const newList = template.tableData.filter((t) => t.id !== id);
        setTemplate((prev) => ({ ...prev, tableData: newList }));
        addToast({
          type: "success",
          title: "Row Deleted",
          description: `Row has been deleted.`,
        });
      } catch (err) {
        if (err instanceof ApiError) {
          console.log(err);
        }
      } finally {
        //   setLoading(false);
      }
    },
    [template],
  );

  useEffect(() => {
    if (data) {
      getDetails(data.id);
    }
  }, [action]);

  return (
    <div className="space-y-5">
      <PageHeader
        title="Batch Entry"
        subtitle="Import liens, cases, and contacts in bulk"
      />

      <div className="bg-white border border-gray-200 rounded-xl p-6">
        <div className="flex items-center justify-between mb-8">
          {STEPS.map((step, i) => (
            <div key={step} className="flex items-center flex-1">
              <div className="flex items-center gap-2">
                <div
                  className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
                    i <= currentStep
                      ? "bg-primary text-white"
                      : "bg-gray-100 text-gray-400"
                  }`}
                >
                  {i < currentStep ? <i className="ri-check-line" /> : i + 1}
                </div>
                <span
                  className={`text-sm font-medium ${i <= currentStep ? "text-gray-900" : "text-gray-400"}`}
                >
                  {step}
                </span>
              </div>
              {i < STEPS.length - 1 && (
                <div
                  className={`flex-1 h-px mx-4 ${i < currentStep ? "bg-primary" : "bg-gray-200"}`}
                />
              )}
            </div>
          ))}
        </div>

        {currentStep === 0 && (
          <BatchUploadDocumentComponent
            templateList={templateList}
            onTemplateUpdate={(data) => setTemplateData(data)}
            onDownload={download}
          ></BatchUploadDocumentComponent>
        )}

        {currentStep === 1 && (
          <DataMappingComponent
            template={template}
            onRemoveDetails={removeDetails}
          ></DataMappingComponent>
        )}

        {currentStep === 2 && (
          <DataValidationComponent
            validations={validations}
          ></DataValidationComponent>
        )}

        {currentStep === 3 && (
          <ImportCompleteComponent
            onRestart={() => setCurrentStep(0)}
            totalCount={totalImports}
          ></ImportCompleteComponent>
        )}

        <div className="flex justify-between mt-8 pt-6 border-t border-gray-100">
          <button
            onClick={() => setCurrentStep(Math.max(0, currentStep - 1))}
            disabled={currentStep === 0}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            Back
          </button>
          <button
            onClick={() => nextStep()}
            disabled={currentStep === STEPS.length - 1}
            className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {currentStep === 2 ? "Start Import" : "Next"}
          </button>
        </div>
      </div>
    </div>
  );
}
