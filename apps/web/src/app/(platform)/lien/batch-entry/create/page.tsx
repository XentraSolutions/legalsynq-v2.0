"use client";

import BatchUploadComponent from "../batch-upload";

export const dynamic = "force-dynamic";

export default function BatchEntryPage() {
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<string | null>();
  const [templateLabel, setTemplateLabel] = useState<string | null>();
  const [caseId, setCaseId] = useState<string | null>();

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
  const [importSummary, setImportSummary] = useState<{
    totalRows?: number;
    importedCount?: number;
    createdCount?: number;
    updatedCount?: number;
    failedCount?: number;
    message?: string;
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

  const onUploaded = useCallback((e: File[]) => {
    setSelectedFile(e[0]);
  }, []);

  const download = async (code: string) => {
    const response = await batchService.download(code);

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  const upload = async () => {
    const formData = new FormData();
    if (!selectedFile) return;
    formData.append("File", selectedFile);
    formData.append("Label", templateLabel ?? "");
    formData.append("Template", selectedTemplate ?? "");
    formData.append("date", dateConverter(new Date().toDateString()));
    formData.append("caseId", caseId ?? "");

    const response = await batchService.upload(formData);
    console.log(response);
    setTemplate((prev) => ({ ...prev, id: response.id }));
    setTimeout(() => {
      console.log(template);
      fetchDataContext(response.id);
    }, 1000);
  };

  const process = async () => {
    console.log(template);
    const response = await batchService.process({
      batchUploadId: template.id,
      templateId: selectedTemplate ?? "INITIAL_CASE_IMPORT",
    });

    setValidations(response);
  };

  const importBatch = async () => {
    if (!selectedFile) return;

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
      label: templateLabel || "Case tracking import",
      template: selectedTemplate ?? "",
      caseId: template.caseId || "",
      file: selectedFile.name || "tracking.csv",
      date: dateConverter(new Date().toDateString()),
      rows: template.tableData.length,
      dataContext: dataContextLines.join("\n"),
    };

    const response = await batchService.createBatch(importPayload);
    setImportSummary({
      totalRows: response.totalRows,
      importedCount: response.importedCount,
      createdCount: response.createdCount,
      updatedCount: response.updatedCount,
      failedCount: response.failedCount,
      message: response.message,
    });
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
          <div className="space-y-6">
            <Field
              label="Label"
              required
              value={templateLabel ?? ""}
              onChange={(v) => setTemplateLabel(v.toString())}
              placeholder=""
            />
            {selectedTemplate == "ADD_PAYMENTS_EXISTING_LIENS" && (
              <Field
                label="Case Id"
                required
                value={templateLabel ?? ""}
                onChange={(v) => setCaseId(v.toString())}
                placeholder="Enter Case Id"
              />
            )}

            <h3 className="text-sm font-semibold text-gray-800 mb-3">
              Documents <span className="text-red-500 ml-0.5">*</span>
            </h3>

            <UploadDocumentComponent
              config={{ isMultiple: false, accepted: ".csv" }}
              // ref={dropzoneRef}
              onUploaded={(e) => onUploaded(e)}
            />

            <div className="border border-gray-200 rounded-xl p-5">
              <h3 className="text-sm font-semibold text-gray-800 mb-3">
                Templates
              </h3>
              <p className="text-xs text-gray-600 mb-4">Select Template</p>
              <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
                {templateList.map((t) => (
                  <div
                    key={t.id}
                    className={`flex items-center gap-3 p-3 border border-gray-100 rounded-lg text-gray-700 transition-colors text-left hover:cursor-pointer hover:bg-gray-100   ${selectedTemplate == t.code ? "bg-gray-100 border-gray-500" : ""}`}
                    onClick={(e) => {
                      e.preventDefault();
                      setSelectedTemplate(t.code);
                    }}
                  >
                    <i className={`${t.icon} text-lg ${t.color}`} />
                    <div className="flex-1">
                      <p className="text-sm font-medium">{t.name}</p>
                    </div>
                    <button
                      className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100  hover:text-primary transition-colors"
                      title="Download"
                      onClick={() => download(t.code)}
                    >
                      <i className="ri-download-2-line text-sm" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}

        {currentStep === 1 && (
          <div className="space-y-4">
            <p className="text-sm text-gray-600">
              Map your file columns to system fields:
            </p>
            <div className="border border-gray-200 rounded-lg overflow-hidden">
              <div className="px-4 py-3 bg-gray-50 text-xs text-gray-500 uppercase">
                Previewing first rows from data context
              </div>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-100">
                  <thead>
                    <tr className="bg-gray-50">
                      {template.columns.length > 0 ? (
                        template.columns.map((column) => (
                          <th
                            key={column}
                            className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase whitespace-nowrap"
                          >
                            {column}
                          </th>
                        ))
                      ) : (
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                          No columns available
                        </th>
                      )}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 bg-white">
                    {template.columns.length > 0 ? (
                      template.tableData.slice(0, 3).map((row, rowIndex) => (
                        <tr
                          key={`${rowIndex}-${JSON.stringify(row)}`}
                          className="hover:bg-gray-50"
                        >
                          {template.columns.map((column) => {
                            const value = row?.[column];
                            const previewText =
                              value === null || value === undefined
                                ? "—"
                                : String(value);

                            return (
                              <td
                                key={`${column}-${rowIndex}`}
                                className="px-4 py-3 text-sm text-gray-700 whitespace-nowrap"
                              >
                                {previewText}
                              </td>
                            );
                          })}
                        </tr>
                      ))
                    ) : (
                      <tr>
                        <td className="px-4 py-6 text-sm text-gray-500 text-center">
                          No data context columns available yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {currentStep === 2 && (
          <div className="space-y-4">
            <div
              className={`flex items-center gap-3 p-4 rounded-lg border ${
                (validations?.failedCount ?? 0) > 0
                  ? "bg-amber-50 border-amber-200"
                  : "bg-green-50 border-green-200"
              }`}
            >
              <i
                className={`text-xl ${
                  (validations?.failedCount ?? 0) > 0
                    ? "ri-error-warning-line text-amber-600"
                    : "ri-checkbox-circle-line text-green-600"
                }`}
              />
              <div>
                <p
                  className={`text-sm font-medium ${
                    (validations?.failedCount ?? 0) > 0
                      ? "text-amber-700"
                      : "text-green-700"
                  }`}
                >
                  {validations?.failedCount
                    ? "Validation Issues Found"
                    : "Validation Complete"}
                </p>
                <p
                  className={`text-xs ${
                    (validations?.failedCount ?? 0) > 0
                      ? "text-amber-600"
                      : "text-green-600"
                  }`}
                >
                  {validations?.message ??
                    "Processing validation results will appear here."}
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-gray-900">
                  {validations?.successCount ?? 0}
                </p>
                <p className="text-xs text-gray-500">Valid Records</p>
              </div>
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-amber-600">
                  {validations?.failedCount ?? 0}
                </p>
                <p className="text-xs text-gray-500">Failed Records</p>
              </div>
              <div className="bg-white border border-gray-200 rounded-lg p-4 text-center">
                <p className="text-2xl font-bold text-gray-900">
                  {validations?.totalRows ?? 0}
                </p>
                <p className="text-xs text-gray-500">Total Rows</p>
              </div>
            </div>

            {validations?.data?.some((item) => item.status === "FAILED") && (
              <div className="border border-gray-200 rounded-lg overflow-hidden">
                <div className="px-4 py-3 bg-gray-50 text-xs font-medium text-gray-500 uppercase">
                  Failed rows
                </div>
                <div className="overflow-x-auto">
                  <table className="min-w-full divide-y divide-gray-100">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                          Row
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                          Status
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                          Reason
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase">
                          Data
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100 bg-white">
                      {validations.data
                        .filter((item) => item.status === "FAILED")
                        .map((item) => (
                          <tr
                            key={item.id}
                            className="hover:bg-gray-50 align-top"
                          >
                            <td className="px-4 py-3 text-sm text-gray-700">
                              {item.row}
                            </td>
                            <td className="px-4 py-3 text-sm text-amber-600 font-medium">
                              {item.status}
                            </td>
                            <td className="px-4 py-3 text-sm text-gray-700 max-w-[260px]">
                              {item.reason}
                            </td>
                            <td className="px-4 py-3 text-sm text-gray-600">
                              <div className="space-y-1">
                                {Object.entries(item.data ?? {}).map(
                                  ([key, value]) => (
                                    <div
                                      key={key}
                                      className="flex gap-2 text-xs"
                                    >
                                      <span className="font-medium text-gray-500">
                                        {key}:
                                      </span>
                                      <span>
                                        {value === null || value === undefined
                                          ? "—"
                                          : String(value)}
                                      </span>
                                    </div>
                                  ),
                                )}
                              </div>
                            </td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        )}

        {currentStep === 3 && (
          <div className="text-center py-8">
            <i className="ri-checkbox-circle-line text-5xl text-green-500 mb-4" />
            <h3 className="text-lg font-semibold text-gray-900 mb-1">
              Import Complete
            </h3>
            <p className="text-sm text-gray-500 mb-4">
              {(importSummary?.importedCount ?? 0).toString()} records have been successfully imported.
            </p>
            {importSummary?.failedCount ? (
              <p className="text-xs text-amber-600 mb-4">
                {importSummary.failedCount} rows failed during import.
              </p>
            ) : null}
            <button
              onClick={() => setCurrentStep(0)}
              className="text-sm px-4 py-2 bg-primary text-white rounded-lg hover:bg-primary/90"
            >
              Start New Import
            </button>
          </div>
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
