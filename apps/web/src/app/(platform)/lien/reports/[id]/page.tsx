"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import ReportDisplay from "../components/report-display";
import { ReportTemplate } from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { PaginationMeta } from "@/lib/contacts";

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

  const [report, setReport] = useState<any | null>(null);
  const [template, setTemplate] = useState<any | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingData, setLoadingData] = useState(true);

  const [editMode, setEditMode] = useState(false);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 10,
    totalCount: 0,
    totalPages: 0,
  });
  const fetchReport = useCallback(async () => {
    try {
      const result = await lienReportsService.getReportsById(
        id?.toString() ?? "",
      );
      const generatedTemplate = await lienReportsService.generateTemplate({
        ...result,
        limit: 10,
      });

      setReport(result);
      setTemplate({
        ...generatedTemplate,
        page: pagination.page,
        limit: pagination.pageSize,
        totalCount: generatedTemplate?.totalCount
          ? generatedTemplate?.totalCount
          : 0,
        totalPages: generatedTemplate?.totalCount
          ? Math.floor(generatedTemplate?.totalCount / 10)
          : 1,
      });
    } catch (err) {
    } finally {
      setLoading(false);
      setLoadingData(false);
    }
  }, [pagination]);

  const fetchReportData = useCallback(async () => {
    setLoadingData(true);
    try {
      const result = await lienReportsService.getReportsById(
        id?.toString() ?? "",
      );
      const generatedTemplate = await lienReportsService.generateTemplate({
        ...result,
        limit: 10,
      });

      setReport(result);
      setTemplate({
        ...generatedTemplate,
        page: pagination.page,
        limit: pagination.pageSize,
        totalCount: generatedTemplate?.totalCount
          ? generatedTemplate?.totalCount
          : 0,
        totalPages: generatedTemplate?.totalCount
          ? Math.round(generatedTemplate?.totalCount / 10)
          : 1,
      });
    } catch (err) {
    } finally {
      setLoadingData(false);
    }
  }, [pagination]);
  useEffect(() => {
    setLoading(true);
    fetchReport();
  }, [id]);

  useEffect(() => {
    fetchReportData();
  }, [pagination]);

  if (loading) {
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
          report={{ ...report, ...template, reportId: id }}
          onBack={() => router.push("/lien/reports")}
          onEdit={() => setEditMode(true)}
          onSaved={() => {
            setEditMode(false);
            setTemplate(null);
            setTimeout(() => {
              router.push("/lien/reports");
            }, 500);
          }}
          onPaginate={(e) => setPagination(e)}
          loadingData={loadingData}
        />
      )}
    </div>
  );
}
