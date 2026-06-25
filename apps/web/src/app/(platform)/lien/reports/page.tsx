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

type ShowCreateModalProps = {
  isOpen: boolean;
  mode?: "create" | "edit";
};
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

  const [showCreate, setShowCreate] = useState<ShowCreateModalProps>({
    isOpen: false,
    mode: "create",
  });

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
                onClick={() => setShowCreate({ isOpen: true, mode: "create" })}
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
        // <ReportDisplay
        //   report={template}
        //   onBack={() => {
        //     setTemplate(null);
        //     setIsSettingTemplate(false);
        //   }}
        //   onSaved={() => {
        //     // setTemplate(data);
        //     setIsSettingTemplate(false);
        //     setTemplate(null);
        //     fetchReports();
        //   }}
        //   // onExport={() => {
        //   //   console.log("export report:", template);
        //   // }}
        //   onEdit={() => {
        //     console.log(template);
        //     setShowCreate({ isOpen: true, mode: "edit" });
        //   }}

        // />
        <ReportDisplay
          report={{ ...template }}
          onBack={() => {
            setTemplate(null);
            setIsSettingTemplate(false);
          }}
          onEdit={() => setShowCreate({ isOpen: true, mode: "edit" })}
          onSaved={() => {
            setIsSettingTemplate(false);
            setTemplate(null);
            setTimeout(() => {
              fetchReports();
            }, 500);
          }}
        />
      )}
      {showCreate.isOpen && (
        <CreateUpdateReport
          mode={showCreate.mode}
          onClose={() => setShowCreate({ isOpen: false })}
          template={template}
          initialData={template}
          onSaved={(data: any) => {
            console.log("saved report:", data);
            setShowCreate({ isOpen: false });
            setTemplate(data);
            setIsSettingTemplate(true);
          }}
        />
      )}
    </div>
  );
}
