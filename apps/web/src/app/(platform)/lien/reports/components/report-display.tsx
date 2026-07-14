"use client";

import { StatusBadge } from "@/components/careconnect/status-badge";
import { KpiCard } from "@/components/lien/kpi-card";
import { ApiError } from "@/lib/api-client";
import { CaseListItem } from "@/lib/cases";
import {
  CreateReports,
  ExportReportRequest,
  ReportListResponse,
  ReportsResponse,
  ReportTotals,
} from "@/lib/liens/lien-report.types";
import { ReportListItem } from "@/lib/liens/lien-reports.mapper";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { useLienStore } from "@/stores/lien-store";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

type SummaryTotals = {
  summaryTotals: ReportTotals;
};
interface ReportDisplayProps {
  report: ReportListResponse &
    SummaryTotals &
    ExportReportRequest &
    CreateReports;
  onBack: () => void;
  onEdit: () => void;
  onSaved: () => void;
}

export default function ReportDisplay({
  report,
  onBack,
  onEdit,
  onSaved,
}: ReportDisplayProps) {
  const [loading, setLoading] = useState(true);
  const [cases, setCases] = useState<CaseListItem[]>(
    (report.items as CaseListItem[]) ?? [],
  );
  const addToast = useLienStore((s) => s.addToast);
  const viewBy = report?.reportType.toLowerCase() ?? "case"; // 'cases' | 'liens'
  report;
  const metrics =
    viewBy === "case"
      ? [
          {
            label: "Total Cases",
            value: report?.summaryTotals?.totalCases ?? report.totalCount,
          },
          {
            label: "Open Cases",
            value: report?.summaryTotals?.totalOpenCases ?? 0,
          },
          {
            label: "Closed Cases",
            value: report?.summaryTotals?.totalClosedCases ?? 0,
          },
          {
            label: "Total Purchase Amount",
            value: report?.summaryTotals?.totalPurchaseAmt ?? `$ 0.00`,
          },
          {
            label: "Total Returned",
            value: report?.summaryTotals?.totalReturnedAmt ?? `$ 0.00`,
          },
          {
            label: "Total Billing Amount",
            value: report?.summaryTotals?.totalBillingAmt ?? `$ 0.00`,
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
            value: report?.summaryTotals?.totalPurchaseAmt ?? `$ 0.00`,
          },
          {
            label: "Total Returned",
            value: report?.summaryTotals?.totalReturnedAmt ?? `$ 0.00`,
          },
          {
            label: "Total Billing Amount",
            value: report?.summaryTotals?.totalBillingAmt ?? `$ 0.00`,
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
      reportId: report.reportId,
      filters: report.filters,
      columns: report.columns,
      format: "csv",
    });

    const csv = atob(response.data.toString());

    const now = new Date();
    const date = now.toISOString().split("T")[0]; // YYYY-MM-DD
    const time = now.toTimeString().split(" ")[0].replace(/:/g, "-"); // HH-MM-SS
    const filename = `reports_${date}_${time}.csv`;

    // Create a Blob and trigger download
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const link = document.createElement("a");
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
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
  useEffect(() => {
    const timer = setTimeout(() => {
      setLoading(false);
    }, 2000); // 2 seconds

    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    if (report.items) {
      setCases(report.items as CaseListItem[]);
    }
  }, [report.items]);

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

        <div className="flex gap-2">
          <button
            className="px-3 py-2 bg-primary text-white rounded-lg text-sm hover:shadow-sm"
            onClick={onEdit}
          >
            Edit Template
          </button>

          {/* <button onClick={onBack} className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            Back
          </button>
          <button className="px-3 py-2 border border-gray-200 rounded-lg text-sm">
            Export CSV
          </button>
          <button className="px-3 py-2 bg-primary text-white rounded-lg text-sm">
            Save Template
          </button> */}
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
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        {loading ? (
          <div className="py-12 text-center">
            <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading cases...</p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="bg-gray-50/80 border-b border-gray-100">
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Case ID
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Plaintiff Name
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Law Firm
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Case Manager
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Accident Type
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Date of Loss
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      DOB
                    </th>
                    <th className="px-3 py-2.5 text-left text-[11px] font-medium text-gray-500 uppercase tracking-wide">
                      Status
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {cases.map((c: CaseListItem, i) => (
                    <tr
                      key={c.id + c.caseNumber + i}
                      className={`hover:bg-gray-50/80 transition-colors cursor-pointer`}
                    >
                      <td className="px-3 py-2.5">{c.caseNumber}</td>
                      <td className="px-3 py-2.5 text-sm text-gray-700 font-medium">
                        {c.clientName}
                      </td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">
                        {c.lawFirm || "—"}
                      </td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">
                        {c.caseManager || "—"}
                      </td>
                      <td className="px-3 py-2.5 text-sm text-gray-600">
                        {c.accidentType || "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">
                        {c.dateOfIncident || "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 tabular-nums">
                        {c.clientDob || "—"}
                      </td>
                      <td className="px-3 py-2.5">
                        <StatusBadge status={c.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {cases.length === 0 && !loading && (
              <div className="py-12 text-center">
                <i className="ri-folder-open-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">No data found.</p>
              </div>
            )}
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
              onClick={onDelete}
              className="px-3 py-2 border border-gray-200 text-red-500 rounded-lg text-sm hover:shadow-sm"
            >
              Delete Template
            </button>

            <button
              onClick={onExport}
              className="px-3 py-2 border border-gray-200 text-blue-500 rounded-lg text-sm hover:shadow-sm"
            >
              Export CSV
            </button>

            <button
              onClick={onSave}
              className="px-3 py-2 bg-primary text-white rounded-lg text-sm hover:shadow-sm"
            >
              Save Template
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
