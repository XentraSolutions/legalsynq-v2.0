"use client";

import { ConfirmDialog } from "@/components/lien/modal";
import { BaseTable } from "@/components/ui/base-table";
import { useFetchReportColumns } from "@/hooks/use-report";
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
import { NoteCell } from "./note-cell";

type SummaryTotals = {
  summaryTotals: ReportTotals;
};
interface ReportDisplayProps {
  report: ReportListResponse &
    SummaryTotals &
    ExportReportRequest &
    CreateReports;
  onBack: () => void;
  onEdit?: () => void;
  onSaved?: () => void;
  onPaginate?: (pagination: PaginationMeta) => void;
  loadingData?: boolean;
}

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined || isNaN(Number(amount)))
    return "";

  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
    minimumFractionDigits: 2, // Forces decimals like .00 if needed
  }).format(Number(amount));
}
function formatNumber(amount: number): string {
  return new Intl.NumberFormat("en-IN", { maximumSignificantDigits: 3 }).format(
    amount,
  );
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
  const [exporting, setExporting] = useState(false);
  const { defaultColumns, isColumnsLoading } = useFetchReportColumns(
    report.reportType,
    report,
  );

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
  const metrics =
    viewBy === "case"
      ? [
          {
            label: "Total Cases",
            value:
              formatNumber(report?.summaryTotals?.totalCases) ??
              report.totalCount,
          },
          {
            label: "Open Cases",
            value: formatNumber(report?.summaryTotals?.totalOpenCases) ?? 0,
          },
          {
            label: "Closed Cases",
            value: formatNumber(report?.summaryTotals?.totalClosedCases) ?? 0,
          },
          {
            label: "Total Purchase Amount",
            value:
              formatCurrency(report?.summaryTotals?.totalPurchaseAmt) ??
              `$ 0.00`,
          },
          {
            label: "Total Returned",
            value:
              formatCurrency(report?.summaryTotals?.totalReturnedAmt) ??
              `$ 0.00`,
          },
          {
            label: "Total Billing Amount",
            value:
              formatCurrency(report?.summaryTotals?.totalBillingAmt) ??
              `$ 0.00`,
          },
        ]
      : [
          {
            label: "Total Liens",
            value: report?.summaryTotals?.totalLiens ?? 0,
          },
          {
            label: "Open Liens",
            value: report?.summaryTotals?.totalOpenLiens ?? 0,
          },
          {
            label: "Closed Liens",
            value: report?.summaryTotals?.totalClosedLiens ?? 0,
          },
          {
            label: "Total Purchase Amount",
            value:
              formatCurrency(report?.summaryTotals?.totalPurchaseAmt) ??
              `$ 0.00`,
          },
          {
            label: "Total Returned",
            value:
              formatCurrency(report?.summaryTotals?.totalReturnedAmt) ??
              `$ 0.00`,
          },
          {
            label: "Total Billing Amount",
            value:
              formatCurrency(report?.summaryTotals?.totalBillingAmt) ??
              `$ 0.00`,
          },
        ];

  const onExport = async () => {
    setExporting(true);
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

    setExporting(false);
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
    if (!report.config?.columns) {
      const cols = defaultColumns
      .flatMap((config: any) => config.value)
      .map((item: any) => {
        const isCurrencyField =
          /amt|amount|price|cost|fee|total/i.test(item.key);

        const isNoteField = item.key.toLowerCase().includes("note");

        const isDefaultMinWidth =
          item.key.includes("facility") ||
          item.key.includes("law") ||
          item.key.includes("case_type")
            ? { minWidth: "220px", width: "100%" }
            : isNoteField
              ? { minWidth: "300px" }
              : item.key.includes("id") || item.key.includes("status")
                ? { minWidth: "135px", width: "100%" }
                : { minWidth: "50px" };

        return {
          id: item.key,
          header: item.label,
          accessorFn: (row: any) => row[item.key],

          meta: {
            ...isDefaultMinWidth,
          },

          cell: ({ row }: any) => {
            const value = row.original[item.key];

            // Handle note fields
            if (isNoteField) {
              return <NoteCell value={value} />;
            }

            let formattedValue = value;

            // Handle currency fields
            if (isCurrencyField && value !== null && value !== undefined) {
              const cleanString = String(value).replace(/[^0-9.-]+/g, "");
              const numericValue = parseFloat(cleanString);

              if (!isNaN(numericValue)) {
                formattedValue = formatCurrency(numericValue);
              }
            }

            return (
              <span className="text-sm text-gray-700 whitespace-nowrap">
                {formattedValue}
              </span>
            );
          },
        };
      });

    setColumns(cols);
  } else {
    const tableColumns = defaultColumns.map((item: any) => {
      const isCurrencyField =
        /amt|amount|price|cost|fee|total/i.test(item.key);

      const isNoteField = item.key.toLowerCase().includes("note");

      const isDefaultMinWidth =
        item.key.includes("facility") ||
        item.key.includes("law") ||
        item.key.includes("case_type") ||
        item.key.includes("manager")
          ? { minWidth: "220px", width: "100%" }
          : item.key.includes("lien_id") ||
              item.key.includes("case_id") ||
              item.key.includes("status")
            ? { minWidth: "135px", width: "100%" }
            : isNoteField
              ? { minWidth: "300px" }
              : { minWidth: "50px" };

      return {
        id: item.key,
        header: item.label,
        accessorFn: (row: any) => row[item.key],

        meta: {
          ...isDefaultMinWidth,
        },

        cell: ({ row }: any) => {
          const value = row.original[item.key];

          // Handle note fields
          if (isNoteField) {
            return <NoteCell value={value} />;
          }

          let formattedValue = value;

          // Handle currency fields
          if (isCurrencyField && value !== null && value !== undefined) {
            const cleanString = String(value).replace(/[^0-9.-]+/g, "");
            const numericValue = parseFloat(cleanString);

            if (!isNaN(numericValue)) {
              formattedValue = formatCurrency(numericValue);
            }
          }

          return (
            <span className="text-sm text-gray-700 whitespace-nowrap">
              {formattedValue}
            </span>
          );
        },
      };
    });
      setColumns(tableColumns);
    }

    setLoading(false);
  }, [isColumnsLoading]);

  useEffect(() => {
    fetchColumns();
  }, [isColumnsLoading]);

  useEffect(() => {
    setCases(report.data ?? []);
  }, [report, report.data]);

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
      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-6  gap-4">
        {metrics.map((m) => (
          <div
            key={m.label}
            className="border border-gray-200 rounded-xl px-4 py-2 hover:shadow-sm break-words"
          >
            <p className="text-xs text-gray-500">{m.label}</p>
            <p className="text-lg font-semibold text-right py-3">{m.value}</p>
          </div>
        ))}
      </div>

      {/* TABLE PLACEHOLDER */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-x-scroll h-full">
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
                enableSorting={false}
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
                className="bg-white border-gray-200 !rounded-none"
              />
            </div>
          </>
        )}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl p-6 text-sm text-gray-500">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          {/* LEFT */}
          <button
            onClick={onBack}
            className="px-3 py-2 border border-gray-200 text-[#0A0A0A] font-semibold rounded-lg text-sm self-start hover:shadow-sm"
          >
            Back
          </button>
          {/* RIGHT */}
          <div className="flex flex-wrap gap-2 sm:gap-2 sm:flex-row sm:items-center sm:justify-end">
            <button
              disabled={exporting}
              onClick={onExport}
              className="px-3 py-2 border border-gray-200 bg-[#F5F5F5] text-[#0A0A0A] font-semibold rounded-lg text-sm hover:shadow-sm"
            >
              {exporting ? "Exporting..." : "Export CSV"}
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
