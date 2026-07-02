"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import ReportDisplay from "../components/report-display";
import CreateUpdateReport from "../components/create-update-report";
import { ReportTemplate } from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";

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
  const [editMode, setEditMode] = useState(false);
  const fetchReport = useCallback(async () => {
    setLoading(true);
    try {
      const result = await lienReportsService.getReportsById(
        id?.toString() ?? "",
      );
      const generatedTemplate =
        await lienReportsService.generateTemplate(result);
      setReport(result);
      setTemplate(generatedTemplate);
    } catch (err) {
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    setLoading(true);
    fetchReport();
  }, [id]);

  useEffect(() => {}, [template]);

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
        />
      )}
      {editMode ? (
        <CreateUpdateReport
          mode="edit"
          initialData={{ ...report, ...template }}
          onClose={(data: ReportTemplate | null) => {
            setEditMode(false);
          }}
          onSaved={(data: any) => {
            console.log("saved report:", data);
            setEditMode(false);
            setTemplate(data);
          }}
        />
      ) : (
        ""
      )}
    </div>
  );
}
