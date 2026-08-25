"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/lien/page-header";
import CreateUpdateReport from "./components/create-update-report";
import { lienReportsApi } from "@/lib/liens/lien-reports.api";
import { lookupService } from "@/lib/lookup";
import { CreateReports, ReportTemplate } from "@/lib/liens/lien-report.types";
import { lienReportsService } from "@/lib/liens/lien-reports.service";
import { ReportListItem } from "@/lib/liens/lien-reports.mapper";
import { ConfirmDialog } from "@/components/lien/modal";
import { ApiError } from "@/lib/api-client";
import { useLienStore } from "@/stores/lien-store";

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

export default function TemplatesPage() {
  const [reports, setReports] = useState<Array<ReportListItem>>([]);
  const [template, setTemplate] = useState<any | null>(null);
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [confirmAction, setConfirmAction] = useState<{
    action: boolean;
    id: string;
  }>({ action: false, id: "" });

  const [showCreate, setShowCreate] = useState<ShowCreateModalProps>({
    isOpen: false,
    mode: "create",
  });

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const addToast = useLienStore((s) => s.addToast);

  const fetchReports = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await lienReportsService.getReports();
      setReports(result?.items as ReportListItem[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load tasks");
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchReport = useCallback(async () => {
    if (!templateId) return;
    setLoading(true);

    try {
      const result = await lienReportsService.getReportsById(
        templateId?.toString() ?? "",
      );
      setTemplate(result);
      setShowCreate({ isOpen: true, mode: "edit" });
    } catch (err) {
    } finally {
      setLoading(false);
    }
  }, [templateId]);
  useEffect(() => {
    fetchReport();
  }, [templateId]);

  useEffect(() => {
    fetchReports();
  }, []);
  const handleConfirmAction = () => {
    onDelete();
  };
  const onDelete = async () => {
    try {
      const response = await lienReportsService.deleteReports(confirmAction.id);

      if (response) {
        addToast({
          type: "success",
          title: "Template Deleted",
        });
      }
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to delete report";
      addToast({ type: "error", title: "Delete Failed", description: message });
    } finally {
      setConfirmAction({ action: false, id: "" });
      fetchReports();
    }
  };
  return (
    <div className="space-y-4">
      {/* HEADER */}
      <>
        <PageHeader
          title="Report Templates"
          subtitle={`${reports?.length} saved templates`}
          actions={
            <button
              onClick={() => {
                setTemplateId(null);
                setShowCreate({ isOpen: true, mode: "create" });
              }}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              <i className="ri-add-line text-base" />
              Create New Report Template
            </button>
          }
        />

        {/* LIST */}
        {loading ? (
          <div className="overflow-hidden">
            <div className="text-center py-8">
              <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
              <p className="text-sm text-gray-400 mt-2">Loading reports...</p>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {reports &&
              reports.map((r: ReportListItem) => (
                <div
                  key={r.id}
                  className="border border-gray-200 rounded-xl p-3 bg-white hover:bg-gray-50 cursor-pointer transition-colors"
                >
                  <div className="flex justify-between items-center">
                    <div className="w-full" onClick={() => setTemplateId(r.id)}>
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
                    <div>
                      <button
                        onClick={() =>
                          setConfirmAction({ action: true, id: r.id })
                        }
                        className="p-1.5 text-gray-400 hover:text-red-500 cursor-pointer"
                      >
                        <i className="ri-delete-bin-line text-sm" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
          </div>
        )}
      </>
      {showCreate.isOpen && (
        <CreateUpdateReport
          mode={showCreate.mode}
          onClose={() => {
            setTemplateId(null);
            setShowCreate({ isOpen: false });
          }}
          template={template}
          initialData={template}
          onSaved={(data: any) => {
            setTemplate(null);

            setShowCreate({ isOpen: false });
            fetchReports();
          }}
        />
      )}
      {confirmAction.action && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction({ action: false, id: "" })}
          onConfirm={handleConfirmAction}
          title={"Delete Template"}
          description={`Are you sure you want to delete? This action cannot be undone.`}
          confirmLabel={"Delete"}
        />
      )}
    </div>
  );
}
