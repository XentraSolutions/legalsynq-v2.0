"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import ReportDisplay from "../components/report-display";
import { ReportTemplate } from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { PaginationMeta } from "@/lib/contacts";
import { useLienReport } from "@/hooks/use-report";

const SAMPLE_REPORTS: any[] = [
  {
    id: "1",
    reportName: "Lien Summary Report",
    reportDescription: "Overview of all lien activities",
    createdAt: "2026-05-20",
    config: {
      columns: ["Plaintiff Name", "Law Firm", "Total Liens"],
    },
  },
  {
    id: "2",
    reportName: "Billing Performance Report",
    reportDescription: "Financial breakdown of billing",
    createdAt: "2026-05-18",
    config: {
      columns: ["Attorney", "Total Billing Amount", "Total Returned"],
    },
  },
];

export default function ReportDetailsPage() {
  const { id } = useParams();
  const router = useRouter();

  const { report, template, setPage, isLoading, isLoadingData } = useLienReport(
    {
      id: id?.toString(),
      initialPage: 1,
      initialPageSize: 10,
    },
  );

  useEffect(() => {}, [template, report]);

  if (isLoading) {
    return <div className="p-6 text-sm text-gray-500">Loading report...</div>;
  }

  if (!report) {
    return (
      <div className="p-6 space-y-2">
        <p className="text-sm text-gray-500">Report not found</p>
        <button
          onClick={() => router.push("/lien/reports")}
          className="text-primary text-sm"
        >
          Back to Reports
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {template && (
        <ReportDisplay
          report={{ ...report, ...template, reportId: id?.toString() ?? "" }}
          onBack={() => router.push("/lien/reports")}
          onPaginate={(e) => setPage(e.page)}
          loadingData={isLoadingData}
        />
      )}
    </div>
  );
}
