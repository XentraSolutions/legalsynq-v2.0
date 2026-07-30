"use client";

import { StatusBadge } from "@/components/careconnect/status-badge";
import { KpiCard } from "@/components/lien/kpi-card";
import { ConfirmDialog } from "@/components/lien/modal";
import { BaseTable } from "@/components/ui/base-table";
import { ApiError } from "@/lib/api-client";
import { CaseListItem } from "@/lib/cases";
import { PaginationMeta } from "@/lib/contacts";
import {
  ColumnGroup,
  CreateReports,
  ExportReportRequest,
  ReportColumnOption,
  ReportListResponse,
  ReportTotals,
} from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { useLienStore } from "@/stores/lien-store";
import { useCallback, useEffect, useMemo, useState } from "react";

type SummaryTotals = {
  summaryTotals: ReportTotals;
};

function formatAmount(value?: number): string {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(value ?? 0);
}
interface ReportDisplayProps {
  report: ReportListResponse &
    SummaryTotals &
    ExportReportRequest &
    CreateReports;
  onBack: () => void;
  onEdit: () => void;
  onSaved: () => void;
  onPaginate?: (pagination: PaginationMeta) => void;
  loadingData?: boolean;
}

export default function ReportDisplay({
  report,
  onBack,
  onEdit,
  onSaved,
  onPaginate,
  loadingData,
}: ReportDisplayProps) {
  const [loading, setLoading] = useState(true);
  const [cases, setCases] = useState<CaseListItem[]>([]);
  const [columns, setColumns] = useState<any>();
  const addToast = useLienStore((s) => s.addToast);
  const [confirmAction, setConfirmAction] = useState<boolean>(false);
  const pagination: PaginationMeta = {
    page: report.page ?? 1,
    pageSize: report.pageSize ?? 10,
    totalCount: report?.totalCount ?? 0,
    totalPages: report?.totalPages ?? 1,
  };
  const viewBy = report?.reportType.toLowerCase() ?? "case"; // 'cases' | 'liens'
  report;
  const metrics =
    viewBy === "case"
      ? [
          {
            label: "Total Cases",
            value: (
              report?.summaryTotals?.totalCases ?? report.totalCount
            ).toLocaleString("en-US"),
          },
          {
            label: "Open Cases",
            value: (
              report?.summaryTotals?.totalOpenCases ?? 0
            ).toLocaleString("en-US"),
          },
          {
            label: "Closed Cases",
            value: (
              report?.summaryTotals?.totalClosedCases ?? 0
            ).toLocaleString("en-US"),
          },
          {
            label: "Total Purchase Amount",
            value: formatAmount(report?.summaryTotals?.totalPurchaseAmt),
          },
          {
            label: "Total Returned",
            value: formatAmount(report?.summaryTotals?.totalReturnedAmt),
          },
          {
            label: "Total Billing Amount",
            value: formatAmount(report?.summaryTotals?.totalBillingAmt),
          },
        ]
      : [
          {
            label: "Total Liens",
            value: (report?.summaryTotals?.totalLiens ?? 0).toLocaleString(
              "en-US",
            ),
          },
          {
            label: "Open Liens",
            value: (
              report?.summaryTotals?.totalOpenLiens ?? 0
            ).toLocaleString("en-US"),
          },
          {
            label: "Closed Liens",
            value: (
              report?.summaryTotals?.totalClosedLiens ?? 0
            ).toLocaleString("en-US"),
          },
          {
            label: "Total Purchase Amount",
            value: formatAmount(report?.summaryTotals?.totalPurchaseAmt),
          },
          {
            label: "Total Returned",
            value: formatAmount(report?.summaryTotals?.totalReturnedAmt),
          },
          {
            label: "Total Billing Amount",
            value: formatAmount(report?.summaryTotals?.totalBillingAmt),
          },
        ];

  const onSave = async () => {
    try {
      const response = await lienReportsService.createReports({
        name: report.name,
        description: report.description ?? report.reportDescription,
        config: { columns: report.config?.columns ?? report.columns },
        attorneyIds: report.attorneyIds,
        caseManagerIds: report.caseManagerIds,
        closedDateFrom: null,
        closedDateTo: null,
        fundingCompanyIds: report.fundingCompanyIds,
        isBulk: report.isBulk,
        lawFirmIds: report.lawFirmIds,
        lienStatusIds: report.lienStatusIds,
        medicalFacilityIds: report.medicalFacilityIds,
        medicalProviderIds: report.medicalProviderIds,
        plaintiffCaseIds: report.plaintiffCaseIds,
        purchaseDateFrom: report.purchaseDateFrom,
        purchaseDateTo: report.purchaseDateTo,
        reportType: report.reportType,
        statusView: report.statusView,
      });
      if (response) {
        addToast({
          type: "success",
          title: "Report Saved",
        });
        onSaved();
      }
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save report";
      addToast({ type: "error", title: "Save Failed", description: message });
    }
  };
  const onExport = async () => {
    const response = await lienReportsService.exportReports({
      ...report.reportConfig,
      reportId: report.reportId,
      format: "csv",
    });

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  const handleConfirmAction = () => {
    setConfirmAction(false);
    onDelete();
  };
  const onDelete = async () => {
    try {
      const response = await lienReportsService.deleteReports(report.reportId);

      if (response) {
        addToast({
          type: "success",
          title: "Report Deleted",
        });
        onBack();
      }
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to delete report";
      addToast({ type: "error", title: "Delete Failed", description: message });
    }
  };

  const fetchColumns = useCallback(async () => {
    const colsResponse = await lienReportsService.getColumns(report.reportType);
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

    if (!report.config?.columns) {
      const cols = groupedCols
        .flatMap((config: any) => config.value)
        .map((item) => {
          return {
            id: item.key,
            header: item.label,
            accessorFn: (row: any) => row[item.key],
            cell: ({ row }: any) => (
              <span className="text-xs font-mono text-gray-700">
                {row.original[item.key]}
              </span>
            ),
          };
        });

      setColumns(cols);
      setCases(report.data ?? []);
    } else {
      let sortOrder = 1;
      let selected;

      const globallyOrderedItems = groupedCols
        .flatMap((section) =>
          (section.value || []).map((item) => ({
            ...item,
            sectionKey: section.key,
          })),
        )
        .filter((item) => report.config?.columns.includes(item.key))
        .sort((a, b) => {
          const rawResponse = report.config?.columns;

          const defaultColsArray = Array.isArray(rawResponse)
            ? (rawResponse as string[])
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

      const selectedValues = selected
        .flatMap((section) =>
          section.value.map((item: any) => ({
            ...item,
            sectionKey: section.key,
          })),
        )
        .sort((a, b) => a.sortOrder - b.sortOrder);
      const tableColumns = selectedValues.map((item) => ({
        id: item.key,
        header: item.label,
        accessorFn: (row: any) => row[item.key],
        cell: ({ row }: any) => (
          <span className="text-xs font-mono text-gray-700">
            {row.original[item.key]}
          </span>
        ),
      }));
      setColumns(tableColumns);
      setCases(report.data ?? []);
    }

    setLoading(false);
  }, [report]);

  useEffect(() => {
    fetchColumns();
  }, [report.data, report.columns]);

  useEffect(() => {}, []);

  return (
    <div className="min-h-screen bg-gray-50 p-6 space-y-6">
      {/* HEADER */}
      <div className="flex justify-between items-center bg-white p-4 rounded-xl border border-gray-200">
        <div>
          <h2 className="text-lg font-semibold">{report?.name}</h2>
          <p className="text-sm text-gray-500">
            {viewBy === "case" ? "Cases Report" : "Liens Report"}
          </p>
        </div>
      </div>

      {/* METRICS GRID */}
      <div className="grid grid-cols-1 md:grid-cols-6 gap-4">
        {metrics.map((m) => (
          <div
            key={m.label}
            className="border border-gray-200 rounded-xl p-5 hover:shadow-sm"
          >
            <p className="text-xs text-gray-500">{m.label}</p>
            <p className="text-lg font-semibold">{m.value}</p>
          </div>
        ))}
      </div>

      {/* TABLE PLACEHOLDER */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-scroll h-full max-h-[60vh]">
        {loading ? (
          <div className="py-12 text-center">
            <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading cases...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <BaseTable
                data={cases ?? []}
                columns={columns}
                getRowId={(c) => c.id}
                isLoading={loadingData}
                emptyMessage="No data found."
                manualPagination
                pageCount={pagination.totalPages}
                totalCount={pagination.totalCount}
                pagination={{
                  pageIndex: pagination.page - 1,
                  pageSize: pagination.pageSize,
                }}
                onPaginationChange={(updater) => {
                  const next =
                    typeof updater === "function"
                      ? updater({
                          pageIndex: pagination.page - 1,
                          pageSize: pagination.pageSize,
                        })
                      : updater;
                  onPaginate?.({ ...pagination, page: next.pageIndex + 1 });
                }}
                className="bg-white border-gray-200 rounded-xl"
              />

              {/* <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 border-b border-gray-100">
                    {columns &&
                      columns.map((col: any) => (
                        <th
                          key={col.key}
                          className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide"
                        >
                          {col.label}
                        </th>
                      ))}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {cases.map((c: any, index) => (
                    <tr
                      key={index}
                      className="hover:bg-gray-50/80 transition-colors cursor-pointer"
                    >
                      {columns &&
                        columns.map((col: any) => (
                          <td key={col.key} className="px-3 py-2.5">
                            {c[col.key]}
                          </td>
                        ))}
                    </tr>
                  ))}
                </tbody>
              </table> */}
            </div>
          </>
        )}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-6 text-sm text-gray-500">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {/* LEFT */}
          <button
            onClick={onBack}
            className="px-3 py-2 border border-gray-200 rounded-lg text-sm self-start hover:shadow-sm"
          >
            Go Back
          </button>
          {/* RIGHT */}
          <div className="flex flex-wrap gap-2 sm:gap-2 sm:flex-row sm:items-center sm:justify-end">
            <button
              onClick={onExport}
              className="px-3 py-2 border border-gray-200 text-blue-500 rounded-lg text-sm hover:shadow-sm"
            >
              Export CSV
            </button>
          </div>
        </div>
      </div>
      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(false)}
          onConfirm={handleConfirmAction}
          title={"Delete Report"}
          description={`Are you sure you want to delete? This action cannot be undone.`}
          confirmLabel={"Delete"}
        />
      )}
    </div>
  );
}
