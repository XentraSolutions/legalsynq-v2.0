"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/lien/page-header";
import CreateUpdateReport from "./components/create-update-report";
import { lienReportsApi } from "@/lib/liens/lien-reports.api";
import { lookupService } from "@/lib/lookup";
import ReportDisplay from "./components/report-display";
import { CreateReports, ReportTemplate } from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { ReportListItem } from "@/lib/liens/lien-reports.mapper";

const SAMPLE_REPORTS = [
  {
    id: "1",
    name: "Case Summary Report",
    type: "cases",
    createdAt: "2026-05-20",
    description: "Cases overview report",
  },
  {
    id: "2",
    name: "Lien Financial Report",
    type: "liens",
    createdAt: "2026-05-22",
    description: "Lien financial breakdown",
  },
];

export default function ReportsPage() {
  const router = useRouter();
  const [reports, setReports] = useState<Array<ReportListItem>>([]);
  const [template, setTemplate] = useState<any | null>(null);
  const [isSettingTemplate, setIsSettingTemplate] = useState<boolean>(false);

  const [showCreate, setShowCreate] = useState(false);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchReports = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await lienReportsService.getReports();
      setReports(result?.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load tasks");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchReports();
  }, []);

  return (
    <div className="space-y-4">
      {/* HEADER */}
      {!isSettingTemplate ? (
        <>
          <PageHeader
            title="Reports"
            subtitle={`${reports?.length} saved reports`}
            actions={
              <button
                onClick={() => setShowCreate(true)}
                className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
              >
                <i className="ri-add-line text-base" />
                Create New Report
              </button>
            }
          />

          {/* LIST */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {reports &&
              reports.map((r: ReportListItem) => (
                <div
                  key={r.id}
                  onClick={() => router.push(`/lien/reports/${r.id}`)}
                  className="border border-gray-200 rounded-xl p-3 bg-white hover:bg-gray-50 cursor-pointer transition-colors"
                >
                  <div className="text-sm font-medium text-gray-900">
                    {r.name}
                  </div>
                  <div className="text-xs text-gray-500 mt-1">
                    {r.description}
                  </div>
                  <div className="text-[11px] text-gray-400 mt-2">
                    Created {r.createdAt}
                  </div>
                </div>
              ))}
          </div>
        </>
      ) : (
        <ReportDisplay
          report={template}
          onBack={() => {
            setTemplate(null);
            setIsSettingTemplate(false);
          }}
          onSave={(data: CreateReports) => {
            console.log("saved report:", data);
            // setTemplate(data);
            setIsSettingTemplate(false);
            setTemplate(null);
          }}
          onExport={() => {
            console.log("export report:", template);
          }}
          // onEdit={() => setEditMode(true)}
        />
      )}
      {showCreate && (
        <CreateUpdateReport
          mode={isSettingTemplate ? "create" : "edit"}
          onClose={() => setShowCreate(false)}
          template={template}
          onSaved={(data: any) => {
            console.log("saved report:", data);
            setShowCreate(false);
            setTemplate(data);
            setIsSettingTemplate(true);
          }}
        />
      )}
    </div>
  );
}
