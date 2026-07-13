"use client";

import {
  useState,
  useEffect,
  useCallback,
  useMemo,
  type ReactNode,
  useRef,
} from "react";
import Link from "next/link";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { useSession } from "@/hooks/use-session";
import {
  casesService,
  type CaseDetail,
  type CaseLienItem,
  type CaseLienItemMetadata,
} from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { StatusBadge } from "@/components/lien/status-badge";
import { TaskPanel } from "@/components/lien/task-panel";
import { CaseTaskManager } from "@/components/lien/case-task-manager";
import { useTimezone } from "@/lib/use-timezone";

import { ConfirmDialog } from "@/components/lien/modal";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import MedicalLienComponent from "@/components/lien/add-medical-lien/add-medical-lien/medical-lien-component";
import { useCaseWorkflows } from "@/hooks/use-case-workflows";
import { workflowApi, type WorkflowInstanceDetail } from "@/lib/workflow";
import {
  lienCaseNotesService,
  type CaseNoteResponse,
  type CaseNoteCategory,
} from "@/lib/liens/lien-case-notes.service";
import { emailToDisplayName, isNoteOwner } from "@/lib/liens/note-utils";
import {
  CaseUpdatesItem,
  CreateMedicalCodeLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalLiensDto,
  CreateMedicalPaymentDto,
  UpdateCaseRequestDto,
} from "@/lib/cases/cases.types";
import { lookupService } from "@/lib/lookup";
import type {
  DocumentTypeResponse,
  DropdownOption,
} from "@/lib/lookup/lookup.types";
import { settlementService } from "@/lib/settlement";
import { useSessionContext } from "@/providers/session-provider";
import { SetupReductionForm } from "./components/setup-reduction-form";
import { NoRecoveryForm } from "./components/no-recovery-form";
import { AddPaymentForm } from "./components/add-payment-form";
import { LienSettlementForm } from "./components/lien-settlement-form";
import { LienTable, LienTableToolbar } from "@/components/lien/lien-table";
import type {
  LienColumnDef,
  LienFooterCell,
} from "@/components/lien/lien-table";
import { LienListItem, liensService } from "@/lib/liens";
import { useQueryClient } from "@tanstack/react-query";
import { useCaseLiens, useLienPaymentsByCase } from "@/hooks/use-case-liens";
import { useSettlementHistory } from "@/hooks/use-settlement-history";
import { Pagination } from "@/components/ui/pagination";
import type { SettlementHistoryItemV3 } from "@/lib/settlement/settlement.types";
import { contactsService } from "@/lib/contacts";
import { ContactEntitySelect } from "@/components/lien/contact-entity-select";
import MedicalLienInfo from "@/components/lien/forms/add-medical-lien/medical-lien-info";
import MedicalFacilityProviderInfo from "@/components/lien/forms/add-medical-lien/medical-facility-provider-info";
import MedicalCodesDescription from "@/components/lien/forms/add-medical-lien/medical-codes-description";
import UploadDocuments from "@/components/lien/forms/add-medical-lien/medical-upload-document";
import Field from "@/components/lien/field";
import { dateConverter, dateConvertertoIso } from "@/lib/cases/cases.mapper";
import { PaginationMeta } from "@/lib/billofsale";
import { servicingService } from "@/lib/servicing";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/lien/upload-document";
import { useRouter } from "next/navigation";
import { MergeCaseForm } from "@/components/lien/forms/merge-case-form";

const STATUS_LABELS: Record<string, string> = {
  PreDemand: "Pre-demand",
  DemandSent: "Demand Sent",
  InNegotiation: "In Negotiation",
  CaseSettled: "Case Settled",
  Closed: "Closed",
};
const STATUSES = [
  "PreDemand",
  "DemandSent",
  "InNegotiation",
  "CaseSettled",
  "Closed",
];

const TABS = [
  { key: "details", label: "Details" },
  { key: "liens", label: "Liens" },
  { key: "documents", label: "Documents" },
  { key: "servicing", label: "Servicing" },
  { key: "notes", label: "Notes" },
  { key: "taskmanager", label: "Task Manager" },
] as const;

type TabKey = (typeof TABS)[number]["key"];

function formatCurrency(amount: number | null): string {
  if (amount === null || amount === undefined) return "---";
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

function describeSettlementHistoryItem(item: SettlementHistoryItemV3): string {
  let description: string;
  switch (item.type) {
    case "payment":
      description = `Payment of ${formatCurrency(item.amount)}${item.payee ? ` to ${item.payee}` : ""}${item.checkNumber ? ` (Check #${item.checkNumber})` : ""}`;
      break;
    case "reduction":
      description = `Reduction of ${formatCurrency(item.amount)}`;
      break;
    case "settlement":
      description = `Settlement of ${formatCurrency(item.amount)}${item.status ? ` — ${item.status}` : ""}`;
      break;
  }
  description += ` to lien ID ${item.lienId}`;
  return item.note ? `${description}: ${item.note}` : description;
}

export function CaseDetailClient({
  id,
  tab = "details",
}: {
  id: string;
  tab: string | TabKey;
}) {
  const { lookup } = useSessionContext();
  const queryClient = useQueryClient();

  const router = useRouter();
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();
  const timezone = useTimezone();

  const [caseDetail, setCaseDetail] = useState<CaseDetail | null>(null);
  const [caseUpdates, setCaseUpdates] = useState<any | null>(null);

  const [documentTypes, setDocumentTypes] = useState<DropdownOption[]>([]);

  const {
    data: relatedLiensWithMetadata = { items: [], totalCount: 0 },
    dataUpdatedAt: liensUpdatedAt,
    refetch: refetchLiens,
    isFetching: isLiensFetching,
  } = useCaseLiens(id, { pageSize: 5 });
  const relatedLiens = relatedLiensWithMetadata?.items;
  const totalCount = relatedLiensWithMetadata?.totalCount ?? 0;

  const {
    data: casePayments = [],
    dataUpdatedAt: paymentsUpdatedAt,
    refetch: refetchPayments,
    isFetching: isPaymentsFetching,
  } = useLienPaymentsByCase(id);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabKey | string>(tab);
  const [panelMode, setPanelMode] = useState<PanelMode>("split");
  const [confirmAction, setConfirmAction] = useState<{
    id: string;
    status?: string;
    name: string;
    actionType: "advanceStatus" | "deleteCase" | "mergeCase";
  } | null>(null);
  const [showMedicalLienModal, setShowMedicalLienModal] = useState(false);
  const [actionOpen, setActionOpen] = useState(false);
  const [showMergeCase, setShowMergeCase] = useState(false);

  const fetchCase = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const detail = await casesService.getCase(id);
      setCaseDetail(detail);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? "Case not found." : err.message);
      } else {
        setError("Failed to load case details");
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  const fetchCaseUpdates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const updates = await casesService.getCaseUpdates(id);
      setCaseUpdates(updates ?? []);
    } catch (err) {}
  }, [id]);

  const fetchDocumentTypes = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const types = await lookupService.getDocumentType();

      setDocumentTypes(
        types.map((t) => {
          return { key: t.id, value: t.id, label: t.name };
        }),
      );
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.isNotFound ? "Document types not found." : err.message);
      } else {
        setError("Failed to load document types");
      }
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    fetchCase();
    fetchDocumentTypes();
    fetchCaseUpdates();
  }, []);

  const canEdit = ra.can("case:edit");

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
        <p className="text-sm text-gray-400 mt-2">Loading case details...</p>
      </div>
    );
  }

  if (error || !caseDetail) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">{error || "Case not found."}</p>
        <Link
          href="/lien/cases"
          className="text-sm text-primary hover:underline"
        >
          Back to Cases
        </Link>
      </div>
    );
  }

  const d = caseDetail;

  const docType = documentTypes;

  const handleAdvanceStatus = async () => {
    const status = lookup?.CaseStatus;
    const currentStatus = status?.find((s) => s.code === caseDetail.status);

    if (!currentStatus) return;

    const nextStatus = status?.find(
      (s) => s.sortOrder === currentStatus.sortOrder + 1,
    );

    if (nextStatus) {
      setConfirmAction({
        id: caseDetail.id,
        status: nextStatus.code,
        name: nextStatus.name,
        actionType: "advanceStatus",
      });
    }
  };

  const handleDeleteCase = () => {
    setConfirmAction({
      id: caseDetail.id,
      name: caseDetail.caseNumber,
      actionType: "deleteCase",
    });
  };
  const handleMergeCase = () => {
    setTimeout(() => {
      queryClient.invalidateQueries({
        queryKey: ["cases"],
      });
      router.push("/lien/cases");
    }, 1000);
  };

  const generatePayoff = async () => {
    try {
      const response = await casesService.payoffQoute(id);
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to generate payoff";
      addToast({
        type: "error",
        title: "Generate Payoff Failed",
        description: message,
      });
      setConfirmAction(null);
    }
  };

  const handleConfirmAction = async () => {
    if (!confirmAction) return;

    try {
      if (confirmAction.actionType === "advanceStatus") {
        const response = await casesService.updateCaseStatus(
          confirmAction.id,
          confirmAction.status!,
        );
        addToast({
          type: "success",
          title: "Status Updated",
          description: `Case moved to ${response.status}`,
        });
      } else if (confirmAction.actionType === "deleteCase") {
        await casesService.deleteCase(confirmAction.id);
        // TODO: Implement deleteCase API endpoint and add it to casesService
        // For now, show a placeholder message
        addToast({
          type: "success",
          title: "Case Deleted",
          description: `Case ${confirmAction.id} has been successfully deleted.`,
        });
        setTimeout(() => {
          router.push("/lien/cases");
        }, 500);
      }
      setConfirmAction(null);
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to complete action";
      addToast({ type: "error", title: "Action Failed", description: message });
      setConfirmAction(null);
    }
  };

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="px-6 pt-3 pb-0 text-xs text-gray-400 flex items-center gap-1">
        <Link
          href="/lien/cases"
          className="hover:text-gray-600 transition-colors"
        >
          Cases
        </Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">Liens Management</span>
      </div>

      <div className="mx-6 mt-2 bg-white border border-gray-200 rounded-lg">
        <div className="px-6 py-4">
          <div className="flex items-center gap-8">
            <div className="shrink-0 min-w-[160px]">
              {/* TEMP: UI mock data for visual review only */}
              <h1 className="text-xl font-bold text-gray-900 leading-tight">
                {d.clientName || "Maj Test"}
              </h1>
              <p className="text-xs text-gray-400 mt-1.5 font-medium">
                {d.caseNumber}
              </p>
              <p className="text-xs text-gray-400 mt-1.5 font-medium">{d.id}</p>
            </div>

            <div className="flex-1 min-w-0">
              <div className="grid grid-cols-2 md:grid-cols-4 gap-x-6 gap-y-3">
                <HeaderMeta
                  label="Case Type"
                  value={d.caseType || "Lien Case"}
                />
                <HeaderMeta label="Case Status">
                  <StatusBadge status={d.status} />
                </HeaderMeta>
                <HeaderMeta
                  label="Date of Loss"
                  value={d.dateOfIncident || "---"}
                />
                <HeaderMeta
                  label="Date of Birth"
                  value={d.clientDob || "---"}
                />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta
                  label="State of Incident"
                  value={d.stateOfIncident}
                />
                <HeaderMeta label="Law Firm" value={d.insuranceCarrier || ""} />
                {/* TEMP: UI mock data for visual review only */}
                <HeaderMeta label="Case Manager" value="" />
                {canEdit ? (
                  <div className="flex items-end">
                    {/* <button
                      onClick={handleAdvanceStatus}
                      disabled={d.status === "Closed"}
                      className="text-sm font-medium px-4 py-1.5 bg-primary text-white rounded-lg hover:bg-primary/90 disabled:opacity-40 transition-colors whitespace-nowrap"
                    >
                      Actions
                    </button> */}
                    <div className="relative">
                      {/* Dropdown Button */}
                      <button
                        onClick={() => setActionOpen(!actionOpen)}
                        className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-2 py-2 transition-colors"
                      >
                        Actions
                        <i className="ri-arrow-down-s-line text-base" />
                      </button>
                      {/* Dropdown Menu */}
                      {actionOpen && (
                        <div className="absolute right-0 mt-2 w-48 bg-white border border-gray-200 rounded-lg shadow-lg z-50">
                          {/* Create Lien */}
                          {ra.can("lien:edit") && (
                            <button
                              onClick={() => {
                                handleAdvanceStatus();
                                setActionOpen(false);
                              }}
                              disabled={d.status === "Closed"}
                              className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                            >
                              Advance Status
                            </button>
                          )}
                          <button
                            onClick={() => {
                              setShowMergeCase(true);
                              setActionOpen(false);
                            }}
                            className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                          >
                            Merge Case
                          </button>
                          {/* Filter */}
                          <button
                            onClick={() => {
                              generatePayoff();
                              setActionOpen(false);
                            }}
                            className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                          >
                            Payoff Qoute
                          </button>
                          <button
                            onClick={() => {
                              handleDeleteCase();
                              setActionOpen(false);
                            }}
                            className="w-full text-left px-4 py-2 text-sm hover:bg-gray-100 text-red-600"
                          >
                            Delete Case
                          </button>
                        </div>
                      )}
                    </div>
                  </div>
                ) : (
                  <div />
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-100 px-6">
          <nav className="flex gap-4 -mb-px">
            {TABS.map((tab) => (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={[
                  "px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap",
                  activeTab === tab.key
                    ? "border-primary text-primary"
                    : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300",
                ].join(" ")}
              >
                {tab.label}
                {tab.key === "liens" && (
                  <span className="ml-1.5 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                    {totalCount}
                  </span>
                )}
              </button>
            ))}
          </nav>
        </div>
      </div>

      <div className="flex-1 min-h-0 overflow-auto bg-gray-50 px-6 py-5">
        {activeTab === "details" && (
          <DetailsTab
            d={d}
            panelMode={panelMode}
            onPanelModeChange={setPanelMode}
            canEdit={canEdit}
            onCaseUpdated={() => fetchCase()}
            u={caseUpdates}
          />
        )}
        {activeTab === "liens" && (
          <LiensTab
            caseId={id}
            liens={relatedLiens}
            caseDetail={d}
            panelMode={panelMode}
            onPanelModeChange={setPanelMode}
            onAddMedicalLien={(e: boolean) => setShowMedicalLienModal(e)}
          />
        )}
        {activeTab === "documents" && (
          <DocumentsTab
            docTypes={docType}
            caseDetail={d}
            panelMode={panelMode}
            lienid={id}
            onPanelModeChange={setPanelMode}
          />
        )}
        {activeTab === "servicing" && (
          <ServicingTab
            caseDetail={d}
            liens={relatedLiensWithMetadata.items}
            liensLoadedAt={liensUpdatedAt ? new Date(liensUpdatedAt) : null}
            onRefreshLiens={refetchLiens}
            isLiensFetching={isLiensFetching}
            payments={casePayments}
            paymentsLoadedAt={
              paymentsUpdatedAt ? new Date(paymentsUpdatedAt) : null
            }
            onRefreshPayments={async () => {
              await refetchPayments();
              refetchLiens();
            }}
            isPaymentsFetching={isPaymentsFetching}
            panelMode={panelMode}
            onPanelModeChange={setPanelMode}
          />
        )}
        {activeTab === "notes" && <NotesTab caseId={id} />}
        {activeTab === "taskmanager" && <TaskManagerTab caseDetail={d} />}
      </div>

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleConfirmAction}
          title={
            confirmAction.actionType === "advanceStatus"
              ? "Advance Case Status"
              : "Delete Case"
          }
          description={
            confirmAction.actionType === "advanceStatus"
              ? `Move ${d.caseNumber} to ${confirmAction.name}?`
              : `Are you sure you want to delete case ${confirmAction.name}? This action cannot be undone.`
          }
          confirmLabel={
            confirmAction.actionType === "advanceStatus" ? "Advance" : "Delete"
          }
        />
      )}

      {showMergeCase && (
        <MergeCaseForm
          open={showMergeCase}
          caseNumber={d.id}
          onClose={() => setShowMergeCase(false)}
          onCreated={handleMergeCase}
        />
      )}

      {showMedicalLienModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 overflow-y-auto">
          <div className="bg-white rounded-lg shadow-lg max-w-2xl w-full mx-4 my-6">
            <MedicalLienComponent
              caseInfo={{ ...caseDetail }}
              caseId={id}
              onClose={() => setShowMedicalLienModal(false)}
            />
          </div>
        </div>
      )}
    </div>
  );
}

function HeaderMeta({
  label,
  value,
  children,
}: {
  label: string;
  value?: string;
  children?: ReactNode;
}) {
  return (
    <div className="min-w-0">
      <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">
        {label}
      </p>
      {children ? (
        <div className="mt-1">{children}</div>
      ) : (
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">
          {value || "---"}
        </p>
      )}
    </div>
  );
}

function CollapsibleSection({
  title,
  icon,
  defaultExpanded = true,
  onEdit,
  children,
}: {
  title: string;
  icon: string;
  defaultExpanded?: boolean;
  onEdit?: () => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(defaultExpanded);

  return (
    <div className="bg-white border border-gray-200 rounded-lg overflow-visible">
      <div
        className="flex items-center justify-between px-5 py-3 cursor-pointer select-none hover:bg-gray-50/50 transition-colors"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <i
            className={`ri-arrow-${expanded ? "down" : "right"}-s-line text-gray-400 text-base`}
          />
          <i className={`${icon} text-sm text-gray-500`} />
          <h3 className="text-sm font-semibold text-gray-800">{title}</h3>
        </div>
        <div className="flex items-center gap-1">
          {onEdit && (
            <button
              onClick={(e) => {
                e.stopPropagation();
                onEdit();
              }}
              className="w-7 h-7 flex items-center justify-center rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
            >
              <i className="ri-pencil-line text-sm" />
            </button>
          )}
        </div>
      </div>
      {expanded && (
        <div className="px-5 py-4 border-t border-gray-100">{children}</div>
      )}
    </div>
  );
}

function FieldGrid({ children }: { children: ReactNode }) {
  return <dl className="grid grid-cols-2 gap-x-8 gap-y-4">{children}</dl>;
}

function FieldItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">
        {label}
      </dt>
      <dd className="text-sm text-gray-700 mt-1">{value || "---"}</dd>
    </div>
  );
}

/* TEMP: visual fallback data for UI review only */
const TEMP_UPDATES = [
  {
    id: "1",
    timestamp: "04/14/2026 2:45 PM",
    action: "Status Changed",
    description: "Case moved from Pre-demand to Demand Sent",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "2",
    timestamp: "04/10/2026 10:12 AM",
    action: "Note Added",
    description: "Follow-up scheduled with insurance adjuster",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "3",
    timestamp: "04/05/2026 3:30 PM",
    action: "Document Uploaded",
    description: "Medical records package uploaded for review",
    updatedBy: "James Rivera",
  },
  {
    id: "4",
    timestamp: "04/01/2026 9:00 AM",
    action: "Case Created",
    description: "New lien case opened for plaintiff",
    updatedBy: "System",
  },
];

function DetailsTab({
  d,
  u,
  panelMode,
  onPanelModeChange,
  canEdit,
  onCaseUpdated,
}: {
  d: CaseDetail;
  u: CaseUpdatesItem[];
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  canEdit: boolean;
  onCaseUpdated: (updated: CaseDetail) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const ra = useRoleAccess();

  const [editingPlaintiff, setEditingPlaintiff] = useState(false);
  const [editingTracking, setEditingTracking] = useState(false);

  const [pFirstName, setPFirstName] = useState(d.clientFirstName);
  const [pLastName, setPLastName] = useState(d.clientLastName);
  const [pPhone, setPPhone] = useState(d.clientPhone);
  const [pEmail, setPEmail] = useState(d.clientEmail);
  const [pDob, setPDob] = useState(d.clientDob);
  const [pAddress, setPAddress] = useState(d.clientAddress);
  const [pSaving, setPSaving] = useState(false);
  const [pErrors, setPErrors] = useState<Record<string, string>>({});

  const [tTitle, setTTitle] = useState(d.title);
  const [tAccident, setTAccident] = useState(d.caseType);

  const formattedDoI = dateConvertertoIso(d.dateOfIncident);
  const formattedFd = dateConvertertoIso(d.trackingFollowUpDate);

  const [tDescription, setTDescription] = useState(d.description);
  const [tDateOfIncident, setTDateOfIncident] = useState(formattedDoI);
  const [tTrackingFollowUpDate, setTTrackingFollowUpDate] =
    useState(formattedFd);

  const [tStatus, setTStatus] = useState(d.status);
  const [tSaving, setTSaving] = useState(false);
  const [tErrors, setTErrors] = useState<Record<string, string>>({});

  const [form, setForm] = useState({ ...d });

  const { lookup } = useSessionContext();

  const updateField = (field: keyof typeof d, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    // setTouched((prev) => ({ ...prev, [field]: true }));
  };

  const accidentType =
    lookup?.AccidentType?.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];
  const state =
    lookup?.State?.map((c) => {
      return { key: c.id, value: c.code, label: c.code };
    }) ?? [];
  const medicalStatus =
    lookup?.MedicalStatus?.map((c) => {
      return { key: c.id, value: c.code, label: c.name };
    }) ?? [];

  const resetPlaintiffForm = useCallback(() => {
    setForm({ ...d });
    setPErrors({});
  }, [d]);

  const resetTrackingForm = useCallback(() => {
    setTDateOfIncident(dateConvertertoIso(d.dateOfIncident));
    setTTrackingFollowUpDate(dateConvertertoIso(d.trackingFollowUpDate));
    setForm({ ...d });
    setTErrors({});
  }, [d]);

  const validatePlaintiff = (): boolean => {
    const errs: Record<string, string> = {};
    if (!pFirstName.trim()) errs.firstName = "First name is required";
    if (!pLastName.trim()) errs.lastName = "Last name is required";
    if (pEmail.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(pEmail.trim()))
      errs.email = "Invalid email format";
    if (pPhone.trim() && !/^[\d\s()+-]{7,20}$/.test(pPhone.trim()))
      errs.phone = "Invalid phone format";
    const pdob = dateConverter(pDob) ?? "";
    if (
      pdob.trim() &&
      !/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(pdob.trim()) &&
      !/^\w{3}\s\d{1,2},\s\d{4}$/.test(pdob.trim())
    )
      errs.dob = "Invalid date format (use MM/DD/YYYY)";
    setPErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const validateTracking = (): boolean => {
    const errs: Record<string, string> = {};
    const dateOfIncident = dateConverter(tDateOfIncident);

    if (
      dateOfIncident &&
      dateOfIncident.trim() &&
      !/^\d{1,2}\/\d{1,2}\/\d{4}$/.test(dateOfIncident.trim()) &&
      !/^\w{3}\s\d{1,2},\s\d{4}$/.test(dateOfIncident.trim())
    ) {
      errs.dateOfIncident = "Invalid date format (use MM/DD/YYYY)";
    }
    setTErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handlePlaintiffSave = useCallback(async () => {
    if (!validatePlaintiff()) return;
    setPSaving(true);
    const payload = {
      caseId: d.id,
      firstName: form.clientFirstName.trim(),
      lastName: form.clientLastName.trim(),
      phone: form.clientPhone.trim() || "",
      email: form.clientEmail.trim() || "",
      dob: dateConverter(form.clientDob) || "",
      address: form.clientStreetAddress.trim() || "",
      sex: form.sex || "",
      city: form.clientCity,
      state: form.clientState,
      zipcode: form.clientZipcode,
    };
    try {
      await casesService.updateCasePersonal(payload);
      setTimeout(() => {
        onCaseUpdated({ ...d, ...payload });
      }, 100);

      setEditingPlaintiff(false);
      addToast({
        type: "success",
        title: "Plaintiff Updated",
        description: "Plaintiff information saved successfully.",
      });
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save plaintiff info";
      addToast({ type: "error", title: "Save Failed", description: message });
    } finally {
      setPSaving(false);
    }
  }, [d, form, onCaseUpdated, addToast]);

  const handleTrackingSave = useCallback(async () => {
    // if (!validateTracking()) return;
    setTSaving(true);
    const payload: UpdateCaseRequestDto = {
      caseId: d.id,
      currentStatus: form.status,
      currentMedicalStatus: form.currentMedicalStatus,
      caseType: form.caseType,
      stateOfIncident: form.stateOfIncident,
      trackingFollowUp: dateConverter(form.trackingFollowUpDate),
      dateOfLoss: dateConverter(form.dateOfIncident),
      leadId: form.leadId,
      description: form.description || "",
      notes: form.notes || "",
      demandAmount: d.demandAmount ?? 0.0,
      settlementAmount: d.settlementAmount ?? 0.0,
    };
    try {
      await casesService.updateCase(payload);
      setTimeout(() => {
        onCaseUpdated({ ...d });
      }, 100);

      setEditingTracking(false);
      addToast({
        type: "success",
        title: "Case Tracking Updated",
        description: "Case tracking information saved successfully.",
      });
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message : "Failed to save case tracking";
      addToast({ type: "error", title: "Save Failed", description: message });
    } finally {
      setTSaving(false);
    }
  }, [
    d,
    tDateOfIncident,
    tTrackingFollowUpDate,
    form,
    onCaseUpdated,
    addToast,
  ]);

  const inputCls =
    "w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all";
  const errCls = "text-[11px] text-red-500 mt-0.5";

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection
        title="Plaintiff"
        icon="ri-user-line"
        onEdit={
          canEdit && !editingPlaintiff
            ? () => {
                resetPlaintiffForm();
                setEditingPlaintiff(true);
              }
            : undefined
        }
      >
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">
            Plaintiff Info
          </p>
        </div>

        {editingPlaintiff ? (
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-x-8 gap-y-3 relative">
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  First Name *
                </label>
                <Field
                  label=""
                  value={form.clientFirstName}
                  onChange={(e) => updateField("clientFirstName", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Last Name *
                </label>
                <Field
                  label=""
                  value={form.clientLastName}
                  onChange={(e) => updateField("clientLastName", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Phone Number
                </label>
                <Field
                  label=""
                  value={form.clientPhone}
                  onChange={(e) => updateField("clientPhone", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Email
                </label>
                <Field
                  label=""
                  value={form.clientEmail}
                  onChange={(e) => updateField("clientEmail", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Date of Birth
                </label>
                <Field
                  label=""
                  type="date"
                  value={form.clientDob}
                  onChange={(e) => updateField("clientDob", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                  Sex
                </label>
                <Field
                  label=""
                  value={form.sex}
                  type="select"
                  options={[
                    { key: "male", value: "male", label: "Male" },
                    { key: "female", value: "female", label: "Female" },
                  ]}
                  onChange={(e) => updateField("sex", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Address
                </label>
                <Field
                  label=""
                  value={form.clientStreetAddress}
                  onChange={(e) =>
                    updateField("clientStreetAddress", e.toString())
                  }
                />
              </div>

              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  City
                </label>
                <Field
                  label=""
                  value={form.clientCity}
                  onChange={(e) => updateField("clientCity", e.toString())}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  State
                </label>
                <Field
                  label=""
                  value={form.clientState}
                  type="select"
                  options={state}
                  onChange={(e) => {
                    updateField("clientState", e.toString());
                  }}
                />
              </div>

              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Zip code
                </label>
                <Field
                  label=""
                  value={form.clientZipcode}
                  onChange={(e) => updateField("clientZipcode", e.toString())}
                />
              </div>
            </div>
            <div className="flex items-center gap-2 pt-1">
              <button
                onClick={handlePlaintiffSave}
                disabled={pSaving}
                className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-1.5 disabled:opacity-60"
              >
                {pSaving ? (
                  <>
                    <i className="ri-loader-4-line text-sm animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <i className="ri-save-line text-sm" />
                    Save
                  </>
                )}
              </button>
              <button
                onClick={() => {
                  setEditingPlaintiff(false);
                  setPErrors({});
                }}
                disabled={pSaving}
                className="px-4 py-2 text-sm font-medium text-gray-500 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <FieldGrid>
            <FieldItem label="Full Name" value={d.clientName} />
            <FieldItem label="Phone Number" value={d.clientPhone} />
            <FieldItem label="Email" value={d.clientEmail} />
            <FieldItem label="Birthdate" value={d.clientDob} />
            <FieldItem label="Sex" value={d.sex} />
            <FieldItem label="Address" value={d.clientAddress} />
          </FieldGrid>
        )}
      </CollapsibleSection>

      <CollapsibleSection
        title="Case Tracking"
        icon="ri-compass-3-line"
        onEdit={
          canEdit && !editingTracking
            ? () => {
                resetTrackingForm();
                setEditingTracking(true);
              }
            : undefined
        }
      >
        <div className="mb-3">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">
            Case Details
          </p>
        </div>

        {editingTracking ? (
          <div className="space-y-3">
            <div className="grid grid-cols-2 gap-x-8 gap-y-3">
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                  Tracking Follow Up
                </label>

                <Field
                  label=""
                  type="date"
                  value={tTrackingFollowUpDate}
                  onChange={(e) => {
                    updateField("trackingFollowUpDate", e.toString());
                    setTTrackingFollowUpDate(e.toString());
                  }}
                />
              </div>

              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Case Status
                </label>
                <div className="relative">
                  <select
                    value={form.status}
                    onChange={(e) =>
                      setForm((prev) => ({ ...prev, status: e.target.value }))
                    }
                    className={`${inputCls} appearance-none cursor-pointer`}
                  >
                    {lookup?.CaseStatus.map((s) => (
                      <option key={s.id} value={s.code}>
                        {s.name}
                      </option>
                    ))}
                  </select>
                  <i className="ri-arrow-down-s-line absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
                </div>
              </div>

              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                  Current Medical Status
                </label>
                <Field
                  label=""
                  value={form.currentMedicalStatus}
                  options={medicalStatus}
                  onChange={(v) =>
                    updateField("currentMedicalStatus", v.toString())
                  }
                  placeholder="Medical Status"
                  type="select"
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Case Type
                </label>

                <Field
                  label=""
                  value={form.caseType}
                  options={accidentType}
                  placeholder=""
                  onChange={(v) => {
                    updateField("caseType", v.toString());
                  }}
                  type="select"
                />
              </div>

              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                  Date of Loss
                </label>
                <Field
                  label=""
                  type="date"
                  value={tDateOfIncident}
                  onChange={(e) => {
                    setTDateOfIncident(e.toString());
                    updateField("dateOfIncident", e.toString());
                  }}
                  placeholder={tDateOfIncident}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                  State of Incident
                </label>
                <Field
                  label=""
                  value={form.stateOfIncident}
                  options={state}
                  onChange={(v: string) =>
                    updateField("stateOfIncident", v.toString())
                  }
                  placeholder="State"
                  type="select"
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-300 uppercase tracking-wide mb-1">
                  Lead
                </label>
                <ContactEntitySelect
                  contactType="Lead"
                  value={form.leadId}
                  onChange={(v) => updateField("leadId", v)}
                  placeholder="Select lead..."
                  searchPlaceholder="Search leads..."
                  allowCreate
                  createLabel="Add Lead"
                />
              </div>
            </div>
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1">
                Case Tracking Note
              </label>
              <Field
                label=""
                value={form.notes}
                type="textarea"
                onChange={(v) => updateField("notes", v.toString())}
                placeholder=""
              />
            </div>
            <div className="flex items-center gap-2 pt-1">
              <button
                onClick={handleTrackingSave}
                disabled={tSaving}
                className="px-4 py-2 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-1.5 disabled:opacity-60"
              >
                {tSaving ? (
                  <>
                    <i className="ri-loader-4-line text-sm animate-spin" />
                    Saving...
                  </>
                ) : (
                  <>
                    <i className="ri-save-line text-sm" />
                    Save
                  </>
                )}
              </button>
              <button
                onClick={() => {
                  setEditingTracking(false);
                  setTErrors({});
                }}
                disabled={tSaving}
                className="px-4 py-2 text-sm font-medium text-gray-500 bg-white border border-gray-200 rounded-lg hover:bg-gray-50 transition-colors"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <>
            <FieldGrid>
              {/* TEMP: Tracking Follow Up not supported by API */}
              <FieldItem
                label="Tracking Follow Up"
                value={d.trackingFollowUpDate || "---"}
              />
              <div>
                <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">
                  Current Status
                </dt>
                <dd className="mt-1">
                  <StatusBadge status={d.status} />
                </dd>
              </div>
              {/* TEMP: Current Medical Status not supported by API */}
              <FieldItem
                label="Current Medical Status"
                value={d.currentMedicalStatus || "---"}
              />
              <FieldItem label="Case Type" value={d.caseType || "---"} />
              <FieldItem
                label="Date of Incident"
                value={d.dateOfIncident || "---"}
              />
              <FieldItem
                label="State of Incident"
                value={d.stateOfIncident || "---"}
              />
              <FieldItem label="Lead" value="---" />
            </FieldGrid>

            <div className="mt-4 pt-4 border-t border-gray-100">
              <dt className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">
                Case Tracking Note
              </dt>
              <dd className="text-sm text-gray-600 mt-1.5 leading-relaxed">
                {d.description || d.notes || "---"}
              </dd>
            </div>
          </>
        )}

        {/* Case Flags — not API-backed, read-only placeholders */}
        <div className="mt-4 pt-4 border-t border-gray-100">
          <div className="flex items-center gap-2 mb-3">
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wide leading-tight">
              Case Flags
            </p>
            <span className="text-[10px] text-gray-300 italic">
              Not yet supported
            </span>
          </div>
          <div className="grid grid-cols-3 gap-x-6 gap-y-2.5">
            {[
              "Share with Law Firm",
              "UCC Filed",
              "Case Dropped",
              "Child Support",
              "Minor Comp",
            ].map((flag) => (
              <label
                key={flag}
                className="flex items-center gap-2.5 opacity-50 cursor-not-allowed"
              >
                <input
                  type="checkbox"
                  checked={false}
                  disabled
                  className="w-4 h-4 rounded border-gray-300 cursor-not-allowed"
                />
                <span className="text-sm text-gray-400 select-none">
                  {flag}
                </span>
              </label>
            ))}
          </div>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Updates" icon="ri-history-line">
        <div className="overflow-x-auto -mx-5 px-5">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100">
                <th className="pr-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                  Timestamp
                </th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                  Actions
                </th>
                <th className="px-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                  Description
                </th>
                <th className="pl-4 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                  Updated By
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {/* TEMP: visual fallback data for UI review only */}
              {u && u?.length == 0 && (
                <tr className="hover:bg-gray-50/50 transition-colors">
                  <td className="pr-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                    No records found.
                  </td>
                </tr>
              )}
              {u?.length > 0 ? (
                u?.map((u) => (
                  <tr
                    key={u.id}
                    className="hover:bg-gray-50/50 transition-colors"
                  >
                    <td className="pr-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                      {u.timestamp}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
                        {u.action}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-sm text-gray-600">
                      {u.description}
                    </td>
                    <td className="pl-4 py-2.5 text-sm text-gray-500 whitespace-nowrap">
                      {u.updatedBy}
                    </td>
                  </tr>
                ))
              ) : (
                <tr className="hover:bg-gray-50/50 transition-colors">
                  <td className="pr-4 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                    No updates found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">Showing {u?.length} entries</p>
        </div>
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Tasks" icon="ri-task-line">
        <TaskPanel caseId={d.id} />
      </CollapsibleSection>

      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                {d.insuranceCarrier || ""}
              </p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
    />
  );
}

/* TEMP: visual fallback data for UI review only */
const TEMP_LIEN_EXTRAS: Record<
  string,
  {
    facility: string;
    facilityName: string;
    serviceDate: string;
    purchaseDate: string;
    purchaseAmount: number;
  }
> = {};
const TEMP_LIEN_FALLBACK_ROWS = [
  {
    id: "temp-1",
    lienNumber: "LN-2026-0041",
    lienType: "Medical",
    status: "Active",
    originalAmount: 12500,
    facility: "Tampa General Hospital",
    serviceDate: "01/15/2026",
    purchaseDate: "02/10/2026",
    purchaseAmount: 8750,
  },
  {
    id: "temp-2",
    lienNumber: "LN-2026-0042",
    lienType: "Medical",
    status: "Active",
    originalAmount: 4200,
    facility: "Clearwater Radiology",
    serviceDate: "01/22/2026",
    purchaseDate: "02/15/2026",
    purchaseAmount: 2940,
  },
  {
    id: "temp-3",
    lienNumber: "LN-2026-0043",
    lienType: "Medical",
    status: "UnderReview",
    originalAmount: 8900,
    facility: "Bay Area Physical Therapy",
    serviceDate: "02/03/2026",
    purchaseDate: "03/01/2026",
    purchaseAmount: 6230,
  },
];

const TEMP_LIEN_UPDATES = [
  {
    id: "1",
    timestamp: "04/14/2026 3:15 PM",
    lienId: "LN-2026-0041",
    action: "Status Changed",
    description: "Lien status updated to Active",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "2",
    timestamp: "04/12/2026 11:30 AM",
    lienId: "LN-2026-0043",
    action: "Document Uploaded",
    description: "Medical records received from Bay Area PT",
    updatedBy: "James Rivera",
  },
  {
    id: "3",
    timestamp: "04/10/2026 9:45 AM",
    lienId: "LN-2026-0042",
    action: "Lien Linked",
    description: "Lien linked to case from Clearwater Radiology",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "4",
    timestamp: "04/08/2026 2:00 PM",
    lienId: "LN-2026-0041",
    action: "Purchase Completed",
    description: "Lien purchased from Tampa General Hospital",
    updatedBy: "System",
  },
];

function LiensTab({
  caseId,
  liens,
  caseDetail,
  panelMode,
  onPanelModeChange,
  onAddMedicalLien,
}: {
  caseId: string;
  liens: CaseLienItem[];
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  onAddMedicalLien: (m: boolean) => void;
}) {
  const [search, setSearch] = useState("");
  type CaseLienUpdateRow = CaseUpdatesItem & { lienId?: string };

  const [liensUpdates, setLiensUpdates] = useState<CaseLienUpdateRow[]>([]);
  const [lienId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forms, setForms] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });

  const [data, setData] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });
  const addToast = useLienStore((s) => s.addToast);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 1,
  });

  const fetchData = useCallback(async () => {
    const updates = await casesService.getCaseLiensUpdates(caseId);
    setLiensUpdates(
      Array.isArray(updates)
        ? updates.map((item) => ({
            ...item,
            lienId: (item as CaseLienUpdateRow).lienId ?? undefined,
          }))
        : [],
    );
  }, [caseId]);

  const findValueById = (list: any[], id: any, field: string) => {
    const item = list.find((i) => String(i.id) === String(id));
    return item ? item[field] : "";
  };

  const fetchLienDetails = useCallback(async () => {
    if (lienId) {
      try {
        setLoading(true);
        const taskPromises = [
          casesService.getMedicalInfo(lienId),
          casesService.getMedicalFacility(lienId),
          casesService.getMedicalCodes(lienId),
          casesService.loadLiensDocuments(lienId),
          casesService.getPayee(lienId),
        ];

        // 2. Wait for ALL tasks to either resolve or reject
        const results = await Promise.allSettled(taskPromises);

        // 3. Process the results individually if needed
        results.forEach((result, index) => {
          if (result.status === "fulfilled") {
            if (result.value.data) {
              setData((prev) => ({
                ...prev,
                [index]: { ...result.value.data, hasInitialValue: true },
              }));
            }
            if (index == 3) {
              setData((prev) => ({
                ...prev,
                [index]: result.value.data,
              }));
            }

            // }
          } else {
            console.error(`Task ${index} failed due to:`, result.reason);
          }
        });
      } catch (error) {
        // Promise.allSettled itself rarely throws unless input is invalid
        console.error("Unexpected execution error", error);
      } finally {
        setLoading(false);
      }
    }
  }, [lienId]);

  const fetchLienDocuments = useCallback(async () => {
    if (lienId) {
      try {
        const docs = await casesService.loadLiensDocuments(lienId);
        setData((prev) => ({
          ...prev,
          [3]: docs.data,
        }));
      } catch (error) {
        // Promise.allSettled itself rarely throws unless input is invalid
        console.error("Unexpected execution error", error);
      } finally {
        setLoading(false);
      }
    }
  }, [forms[3], lienId]);

  useEffect(() => {
    fetchData();
    fetchLienDetails();
  }, [fetchLienDetails, lienId]);
  /* TEMP: visual fallback data for UI review only */
  const usingFallback = liens.length === 0;
  const displayLiens = liens.map((l) => {
    return {
      ...l,
      facility: l.facility || "---",
      facilityName: l.facilityName || "---",
      serviceDate: l.serviceDate || "---",
      purchaseDate: l.purchaseDate || "---",
      purchaseAmount: l.purchaseAmount || 0,
    };
  });

  const filtered = useMemo(() => {
    if (!search.trim()) return displayLiens;

    const q = search.toLowerCase();
    return displayLiens.filter((l) => {
      return (
        l.lienNumber.toLowerCase().includes(q) ||
        l.facilityName.toLowerCase().includes(q) ||
        l.lienType.toLowerCase().includes(q) ||
        l.status.toLowerCase().includes(q)
      );
    });
  }, [displayLiens, search]);

  const paginatedLiens = useMemo(() => {
    const startIndex = (pagination.page - 1) * pagination.pageSize;
    return filtered.slice(startIndex, startIndex + pagination.pageSize);
  }, [filtered, pagination.page, pagination.pageSize]);

  useEffect(() => {
    const totalCount = filtered.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pagination.pageSize));
    const safePage = Math.min(pagination.page, totalPages);

    setPagination((prev) => {
      if (
        prev.totalCount === totalCount &&
        prev.totalPages === totalPages &&
        prev.page === safePage
      ) {
        return prev;
      }

      return { ...prev, totalCount, totalPages, page: safePage };
    });
  }, [filtered.length, pagination.page, pagination.pageSize]);

  useEffect(() => {}, [data[3]]);

  const totalBilling = filtered.reduce(
    (sum, l) => sum + (l.originalAmount ?? 0),
    0,
  );
  const totalPurchase = filtered.reduce(
    (sum, l) => sum + (l.purchaseAmount ?? 0),
    0,
  );

  const exportCaseLiens = async () => {
    const response = await casesService.exportCaseLiens({
      caseId: caseId,
      liensId: null,
      lawFirmId: null,
      medicalFacilityId: null,
      purchaseDate: null,
      caseManagerId: null,
      lienStatusId: null,
    });

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  function onFormValid(data: any, index: number) {
    setForms((prev: Record<number, any>) => {
      const copy = prev;
      copy[index] = data ?? copy[index];
      return copy;
    });
  }

  const dateConverter = (dateData: string) => {
    if (!dateData) return;

    const date = new Date(dateData);

    // Format the date using the US locale to automatically get MM/DD/YYYY
    const formatter = new Intl.DateTimeFormat("en-US", {
      month: "2-digit",
      day: "2-digit",
      year: "numeric",
    });

    const formattedDate = formatter.format(date);
    return formattedDate;
  };

  async function save() {
    try {
      // Implement save logic here (API call)
      Promise.allSettled([
        await saveMedicalLien({
          ...forms[0],
          purchaseDate: dateConverter(forms[0].purchaseDate),
          initialServiceDate: dateConverter(forms[0].initialServiceDate),
          endServiceDate: dateConverter(forms[0].endServiceDate),
        }),
        await saveMedicalFacilityLiens(forms[1]),

        forms[2]?.codeRows?.forEach(async (element: any) => {
          await updateMedicalCodeLiens({
            payee: forms[2].payee,
            outboundCheckNumber: forms[2].outboundCheckNumber,
            ...element,
          });
        }),
        await saveMedicalPayee(forms[2]),
      ]);

      addToast({
        type: "success",
        title: "Liens Updated",
        description: `Liens has been updated.`,
      });
      setSelectedId(null);
      // closeModal();
    } finally {
      // stopLoading();
    }
  }

  const saveMedicalLien = async (payload: CreateMedicalLiensDto) => {
    try {
      const request: CreateMedicalLiensDto = {
        id: forms[0].id,
        caseId: caseId,
        status: payload.status,
        purchaseDate: payload.purchaseDate,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate,
        note: payload.note,
        isBulk: payload.isBulk == "true" ? "Yes" : "No",
        isServicing: payload.isServicing == "true" ? "Yes" : "No",
        fundingCompanyId: payload.fundingCompanyId,
      };
      !forms[0].hasInitialValue
        ? await casesService.createMedicalLiens(request)
        : await casesService.updateMedicalLiens(request);

      //
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          setErrors({ caseNumber: "A case with this number already exists" });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Update Medical Information Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalFacilityLiens = async (
    payload: CreateMedicalFacilityDto,
  ) => {
    if (!payload.facilityId) return;
    try {
      if (!lienId) return;

      const request: CreateMedicalFacilityDto = {
        liensId: lienId,
        facilityId: payload.facilityId,
        facility: payload.facility,
        facilityContactId: payload.facilityContactId,
        facilityContact: payload.facilityContact,
        email: payload.email,
        medicalProviderId: payload.medicalProviderId,
        medicalProvider: payload.medicalProvider,
      };
      !forms[1].hasInitialValue
        ? await casesService.createMedicalFacilityLiens(request)
        : await casesService.updateMedicalFacilityLiens(request);
      addToast({
        type: "success",
        title: "Facility Updated",
        description: `Facility has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalPayee = async (payload: CreateMedicalPaymentDto) => {
    try {
      if (!lienId) return;

      const request: CreateMedicalPaymentDto = {
        id: null,
        liensId: lienId,
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      await casesService.createMedicalPaymentLiens(request);
      addToast({
        type: "success",
        title: "Payee Updated",
        description: `Payee has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const updateMedicalCodeLiens = async (payload: CreateMedicalCodeLiensDto) => {
    try {
      const request: CreateMedicalCodeLiensDto = {
        id: payload?.id?.includes("temp") ? null : payload.id,
        liensId: lienId ?? "",
        code: payload.code,
        medicareCost: parseFloat(payload.medicareCost).toFixed(2),
        billingAmount: parseFloat(payload.billingAmount).toFixed(2),
        purchaseAmount: parseFloat(payload.purchaseAmount).toFixed(2),
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      request.id == null
        ? await casesService.createMedicalCodeLiens(request)
        : await casesService.updateMedicalCodeLiens(request);
      // addToast({
      //   type: "success",
      //   title: "Medical Code Updated",
      //   description: `Medical Code has been updated.`,
      // });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
    // finally {
    //   setSubmitting(false);
    // }
  };

  const leftContent = (
    <div className="space-y-4">
      {!lienId ? (
        <>
          <CollapsibleSection title="Liens" icon="ri-stack-line">
            <div className="flex items-center gap-3 mb-4">
              <div className="relative flex-1">
                <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
                <input
                  type="text"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPagination((prev) => ({ ...prev, page: 1 }));
                  }}
                  placeholder="Search liens..."
                  className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
                />
              </div>
              <button
                className="px-3.5 py-2 text-sm font-medium text-primary bg-primary/5 border border-primary/20 rounded-lg hover:bg-primary/10 transition-colors inline-flex items-center gap-1.5 whitespace-nowrap"
                onClick={() => onAddMedicalLien(true)}
              >
                <i className="ri-link text-sm" />
                Add Medical Lien
              </button>

              <button
                className="px-3.5 py-2 text-sm font-medium text-primary bg-primary/5 border border-primary/20 rounded-lg hover:bg-primary/10 transition-colors inline-flex items-center gap-1.5 whitespace-nowrap"
                onClick={() => exportCaseLiens()}
              >
                Export
              </button>
            </div>

            {filtered.length === 0 ? (
              <div className="text-center py-8">
                <i className="ri-stack-line text-2xl text-gray-300" />
                <p className="text-sm text-gray-400 mt-2">
                  {search
                    ? "No liens match your search"
                    : "No liens linked to this case"}
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto -mx-5 px-5">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100">
                      <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Lien ID
                      </th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Facility Name
                      </th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Service Date
                      </th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Purchase Date
                      </th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Purchase Amt
                      </th>
                      <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Billing Amt
                      </th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Status
                      </th>
                      <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[50px]"></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {paginatedLiens.map((l) => (
                      <tr
                        key={l.id}
                        className="hover:bg-gray-50/50 transition-colors"
                      >
                        <td
                          className="pr-3 py-2.5"
                          onClick={() => setSelectedId(l.id)}
                        >
                          <span className="text-xs font-mono cursor-pointer text-primary hover:underline">
                            {l.id}
                          </span>
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600 truncate max-w-[160px]">
                          {l.facilityName}
                        </td>
                        <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {l.serviceDate}
                        </td>
                        <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {l.purchaseDate}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 tabular-nums text-right">
                          {formatCurrency(l.purchaseAmount)}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right">
                          {formatCurrency(l.originalAmount)}
                        </td>
                        <td className="px-3 py-2.5">
                          <StatusBadge status={l.status} />
                        </td>
                        <td className="pl-3 py-2.5 text-center">
                          <Link
                            href={`/lien/liens/${l.id}`}
                            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
                          >
                            <i className="ri-eye-line text-sm" />
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-gray-200 bg-gray-50/50">
                      <td
                        colSpan={4}
                        className="pr-3 py-2.5 text-xs font-semibold text-gray-500 uppercase tracking-wide"
                      >
                        Totals ({filtered.length} lien
                        {filtered.length !== 1 ? "s" : ""})
                      </td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">
                        {formatCurrency(totalPurchase)}
                      </td>
                      <td className="px-3 py-2.5 text-sm font-semibold text-gray-700 tabular-nums text-right">
                        {formatCurrency(totalBilling)}
                      </td>
                      <td colSpan={2} />
                    </tr>
                    <tr>
                      <td colSpan={8} className="py-3">
                        {pagination.totalPages > 0 && (
                          <div className="flex items-center justify-between gap-3">
                            <p className="text-xs text-gray-500">
                              Page {pagination.page} of {pagination.totalPages}{" "}
                              · {pagination.totalCount} total
                            </p>
                            <div className="flex gap-1.5">
                              <button
                                onClick={() =>
                                  setPagination((p) => ({
                                    ...p,
                                    page: Math.max(1, p.page - 1),
                                  }))
                                }
                                disabled={pagination.page <= 1}
                                className="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
                              >
                                Previous
                              </button>
                              <button
                                onClick={() =>
                                  setPagination((p) => ({
                                    ...p,
                                    page: Math.min(p.totalPages, p.page + 1),
                                  }))
                                }
                                disabled={
                                  pagination.page >= pagination.totalPages
                                }
                                className="px-3 py-1.5 text-xs font-medium border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40 transition-colors"
                              >
                                Next
                              </button>
                            </div>
                          </div>
                        )}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>
            )}
          </CollapsibleSection>

          <CollapsibleSection title="Updates" icon="ri-history-line">
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Timestamp
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Lien ID
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Actions
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                      Description
                    </th>
                    <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Updated By
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {/* TEMP: visual fallback data for UI review only */}
                  {liensUpdates && liensUpdates.length > 0 ? (
                    liensUpdates.map((u) => (
                      <tr
                        key={u.id}
                        className="hover:bg-gray-50/50 transition-colors"
                      >
                        <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {u.timestamp}
                        </td>
                        <td className="px-3 py-2.5 text-xs font-mono text-primary">
                          {u.lienId ?? "—"}
                        </td>
                        <td className="px-3 py-2.5">
                          <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
                            {u.action}
                          </span>
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">
                          {u.description}
                        </td>
                        <td className="pl-3 py-2.5 text-sm text-gray-500 whitespace-nowrap">
                          {u.updatedBy}
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr className="hover:bg-gray-50/50 transition-colors">
                      <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                        No updates found.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">
                Showing {liens.length} entries
              </p>
            </div>
          </CollapsibleSection>
        </>
      ) : (
        <CollapsibleSection title="Medical Liens" icon="ri-stack-line">
          {!loading && (
            <>
              <div className="border-b-1 pb-6 border-gray-300">
                <MedicalLienInfo
                  caseId={caseId}
                  lienId={lienId}
                  data={data[0]}
                  onFormValid={(e: boolean, data?: any) => {
                    onFormValid(data, 0);
                  }}
                />
              </div>

              <div className="border-b-1 pb-6 pt-6 border-gray-300">
                <MedicalFacilityProviderInfo
                  caseId={caseId}
                  lienId={lienId}
                  data={data[1]}
                  onFormValid={(e: boolean, data?: any) => onFormValid(data, 1)}
                />
              </div>

              <div className="border-b-1 pb-6 pt-6 border-gray-300">
                <MedicalCodesDescription
                  caseId={caseId}
                  lienId={lienId}
                  data={{ ...data[2], ...data[4] }}
                  onFormValid={(e: boolean, data?: any) => onFormValid(data, 2)}
                />
              </div>

              <div className="border-b-1 pb-6 pt-6 border-gray-300">
                <UploadDocuments
                  caseId={caseId}
                  lienId={lienId}
                  data={data[3]}
                  onUploaded={() => fetchLienDocuments()}
                  onFormValid={(e: boolean, data?: any) => onFormValid(data, 3)}
                />
              </div>
            </>
          )}

          <div className="flex justify-between mt-6">
            <button
              onClick={() => setSelectedId(null)}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
            >
              Go back
            </button>
            <button
              onClick={() => {
                save();
              }}
              className="text-sm px-4 py-2 bg-primary hover:bg-primary/90 text-white rounded-lg disabled:opacity-50"
            >
              {/* //disabled={submitDisabled || loading} */}
              {/* {loading ? 'Saving...' : submitLabel} */}
              Save
            </button>
          </div>
        </CollapsibleSection>
      )}
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                {caseDetail.insuranceCarrier || ""}
              </p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
    />
  );
}

/* TEMP: visual fallback data for UI review only */
const TEMP_DOCUMENT_TYPES = [
  "Medical Records",
  "Billing Statement",
  "Lien Agreement",
  "Demand Letter",
  "Settlement Agreement",
  "Insurance Correspondence",
  "Legal Filing",
  "Other",
];

/* TEMP: visual fallback data for UI review only */
const TEMP_CASE_DOCUMENTS = [
  {
    id: "doc-1",
    name: "Medical_Records_Regional_Hospital.pdf",
    documentType: "Medical Records",
    lastUpdate: "04/12/2026",
    size: "2.4 MB",
  },
  {
    id: "doc-2",
    name: "Billing_Statement_March_2026.pdf",
    documentType: "Billing Statement",
    lastUpdate: "04/10/2026",
    size: "840 KB",
  },
  {
    id: "doc-3",
    name: "Demand_Letter_v2.docx",
    documentType: "Demand Letter",
    lastUpdate: "04/08/2026",
    size: "156 KB",
  },
  {
    id: "doc-4",
    name: "Insurance_Response_StateFarm.pdf",
    documentType: "Insurance Correspondence",
    lastUpdate: "04/05/2026",
    size: "1.1 MB",
  },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_LIEN_DOCUMENTS = [
  {
    id: "ldoc-1",
    name: "Lien_Agreement_LN-2026-0451.pdf",
    documentType: "Lien Agreement",
    lastUpdate: "04/11/2026",
    lienNumber: "LN-2026-0451",
    size: "320 KB",
  },
  {
    id: "ldoc-2",
    name: "Medical_Records_Sunrise_Imaging.pdf",
    documentType: "Medical Records",
    lastUpdate: "04/09/2026",
    lienNumber: "LN-2026-0452",
    size: "5.2 MB",
  },
  {
    id: "ldoc-3",
    name: "Billing_Summary_PhysioPlus.xlsx",
    documentType: "Billing Statement",
    lastUpdate: "04/07/2026",
    lienNumber: "LN-2026-0453",
    size: "92 KB",
  },
];

type DocumentType = {
  id: string;
  name: string;
  documentType: string;
  updated: string;
  liensId: string;
  size: string;
};

function DocumentsTab({
  docTypes,
  caseDetail,
  panelMode,
  lienid,
  onPanelModeChange,
}: {
  docTypes: DropdownOption[];
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  lienid: string;
  onPanelModeChange: (m: PanelMode) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  const [selectedDocType, setSelectedDocType] = useState("");
  const [selectedFiles, setSelectedFiles] = useState<File[] | null>(null);

  const [caseDocuments, setCaseDocuments] = useState<DocumentType[]>([]);
  const [liensDocuments, setLiensDocuments] = useState<DocumentType[]>([]);

  const onUploaded = useCallback((e: File[] | null) => {
    setSelectedFiles(e);
  }, []);

  const uploadCaseDocuments = async (payload: any) => {
    if (!payload || payload.length == 0) return;
    try {
      payload.forEach(async (element: File) => {
        const formData = new FormData();
        formData.append("File", element ?? "");
        formData.append("caseId", caseDetail.id ?? "");
        formData.append("DocName", element.name);
        formData.append("DocDescription", "Legacy Case Document upload");
        formData.append("DocFileTypeId", selectedDocType);

        await casesService.uploadCaseDocuments(formData);
        addToast({
          type: "success",
          title: "Document Uploaded",
          description: `Document has been updated.`,
        });
        setTimeout(() => {
          dropzoneRef?.current?.reset();
          setSelectedDocType("");
          fetchDocuments();
        }, 1000);
      });
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const fetchDocuments = async () => {
    const docs = await casesService.loadDocuments(caseDetail.id);
    setCaseDocuments(docs.caseDocuments);
    setLiensDocuments(docs.liensDocuments);
  };

  function download(file: any) {
    window.open(file.url || URL.createObjectURL(file as any), "_blank");
  }

  useEffect(() => {
    fetchDocuments();
  }, []);

  const leftContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Upload Document" icon="ri-upload-cloud-2-line">
        <div className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-gray-500 uppercase tracking-wide mb-1.5">
              Document Type
            </label>
            <div className="relative">
              <Field
                label=""
                value={selectedDocType}
                options={docTypes}
                onChange={(v) => setSelectedDocType(v.toString())}
                placeholder="Select document type..."
                type="select"
              />
            </div>
          </div>

          <UploadDocumentComponent
            ref={dropzoneRef}
            onUploaded={(e) => onUploaded(e)}
          />

          <button
            disabled={selectedFiles != null && !selectedDocType}
            className={[
              "w-full px-4 py-2.5 text-sm font-medium rounded-lg transition-colors flex items-center justify-center gap-2",
              selectedFiles && selectedDocType
                ? "bg-primary text-white hover:bg-primary/90"
                : "bg-gray-100 text-gray-400 cursor-not-allowed",
            ].join(" ")}
            onClick={() => {
              uploadCaseDocuments(selectedFiles);
            }}
          >
            <i className="ri-add-line text-sm" />
            Add Document
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Case Documents" icon="ri-file-copy-2-line">
        {caseDocuments.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-file-copy-2-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">
              No case documents uploaded
            </p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                      Name
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Document Type
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Last Update
                    </th>
                    <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[80px]">
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {caseDocuments.map((doc) => (
                    <tr
                      key={doc.id}
                      className="hover:bg-gray-50/50 transition-colors"
                    >
                      <td className="pr-3 py-2.5">
                        <div className="flex items-center gap-2">
                          <i
                            className={`ri-file-text-line text-sm text-gray-400`}
                          />
                          <span className="text-sm text-gray-700 truncate max-w-[200px]">
                            {doc.name}
                          </span>
                        </div>
                      </td>
                      <td className="px-3 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
                          {doc.documentType}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                        {doc.updated}
                      </td>
                      <td className="pl-3 py-2.5 text-center">
                        <div className="inline-flex items-center gap-1">
                          <button
                            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors"
                            title="Download"
                            onClick={() => download(doc)}
                          >
                            <i className="ri-download-2-line text-sm" />
                          </button>
                          <button
                            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-red-500 transition-colors"
                            title="Delete"
                          >
                            <i className="ri-delete-bin-6-line text-sm" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">
                {caseDocuments.length} document
                {caseDocuments.length !== 1 ? "s" : ""}
              </p>
            </div>
          </>
        )}
      </CollapsibleSection>

      <CollapsibleSection title="Lien Documents" icon="ri-attachment-2">
        {liensDocuments.length === 0 ? (
          <div className="text-center py-8">
            <i className="ri-attachment-2 text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">
              No lien documents available
            </p>
          </div>
        ) : (
          <>
            <div className="overflow-x-auto -mx-5 px-5">
              <table className="min-w-full text-sm">
                <thead>
                  <tr className="border-b border-gray-100">
                    <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                      Name
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Document Type
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Lien
                    </th>
                    <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                      Last Update
                    </th>
                    <th className="pl-3 py-2 text-center text-[11px] font-medium text-gray-400 uppercase tracking-wide w-[80px]">
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-50">
                  {liensDocuments.map((doc) => (
                    <tr
                      key={doc.id}
                      className="hover:bg-gray-50/50 transition-colors"
                    >
                      <td className="pr-3 py-2.5">
                        <div className="flex items-center gap-2">
                          <i className={`ri-file-line text-sm text-gray-400`} />
                          <span className="text-sm text-gray-700 truncate max-w-[200px]">
                            {doc.name}
                          </span>
                        </div>
                      </td>
                      <td className="px-3 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 text-xs font-medium rounded bg-gray-100 text-gray-600">
                          {doc.documentType}
                        </span>
                      </td>
                      <td className="px-3 py-2.5 text-xs font-mono text-primary">
                        {doc.liensId}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                        {doc.updated}
                      </td>
                      <td className="pl-3 py-2.5 text-center">
                        <div className="inline-flex items-center gap-1">
                          <button
                            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors"
                            title="Download"
                          >
                            <i className="ri-download-2-line text-sm" />
                          </button>
                          {/* <button
                            className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-primary transition-colors"
                            title="View Lien"
                            onClick={() => download(doc)}
                          >
                            <i className="ri-eye-line text-sm" />
                          </button> */}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
              <p className="text-xs text-gray-400">
                {liensDocuments.length} document
                {liensDocuments.length !== 1 ? "s" : ""}
              </p>
            </div>
          </>
        )}
      </CollapsibleSection>
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                {caseDetail.insuranceCarrier || ""}
              </p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
    />
  );
}

// function getFileIcon(filename: string): string {
//   const ext = filename.split(".").pop()?.toLowerCase() ?? "";
//   if (ext === "pdf") return "ri-file-pdf-2-line";
//   if (["doc", "docx"].includes(ext)) return "ri-file-word-2-line";
//   if (["xls", "xlsx"].includes(ext)) return "ri-file-excel-2-line";
//   if (["jpg", "jpeg", "png", "gif", "webp"].includes(ext))
//     return "ri-image-line";
//   return "ri-file-text-line";
// }

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_OPEN_LIENS = [
  {
    id: "ol-1",
    lienNumber: "LN-2026-0041",
    facility: "Tampa General Hospital",
    originalAmount: 12500,
    reductionAmount: null as number | null,
    paymentAmount: null as number | null,
    balance: 12500,
    status: "Open",
    lienType: "",
  },
  {
    id: "ol-2",
    lienNumber: "LN-2026-0042",
    facility: "Clearwater Radiology",
    originalAmount: 4200,
    reductionAmount: null as number | null,
    paymentAmount: null as number | null,
    balance: 4200,
    status: "Open",
    lienType: "",
  },
  {
    id: "ol-3",
    lienNumber: "LN-2026-0043",
    facility: "Bay Area Physical Therapy",
    originalAmount: 8900,
    reductionAmount: null as number | null,
    paymentAmount: null as number | null,
    balance: 8900,
    status: "Open",
    lienType: "",
  },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_CLOSED_LIENS = [
  {
    id: "cl-1",
    lienNumber: "LN-2025-0891",
    facility: "Sunshine MRI Center",
    originalAmount: 3200,
    reductionAmount: 800,
    paymentAmount: 2400,
    balance: 0,
    status: "Closed",
    lienType: "",
  },
];

/* TEMP: visual fallback data for UI review only */
const TEMP_SERVICING_HISTORY = [
  {
    id: "sh-1",
    timestamp: "04/14/2026 3:20 PM",
    description: "Case status updated to Pre-demand",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "sh-2",
    timestamp: "04/10/2026 11:00 AM",
    description: "Law firm switched from Prior & Associates to AZ Injury Care",
    updatedBy: "James Rivera",
  },
  {
    id: "sh-3",
    timestamp: "04/05/2026 4:15 PM",
    description: "Settlement negotiation initiated with carrier",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "sh-4",
    timestamp: "03/28/2026 2:30 PM",
    description: "Payment of $2,400.00 applied to LN-2025-0891",
    updatedBy: "System",
  },
  {
    id: "sh-5",
    timestamp: "03/20/2026 10:00 AM",
    description: "Reduction of $800.00 approved for LN-2025-0891",
    updatedBy: "Sarah Mitchell",
  },
  {
    id: "sh-6",
    timestamp: "03/01/2026 9:00 AM",
    description: "Case servicing record created",
    updatedBy: "System",
  },
];

function PaymentHistorySection({
  payments,
  liens,
  paymentsLoadedAt,
  onRefreshPayments,
  isPaymentsFetching,
}: {
  payments: import("@/lib/settlement/settlement.types").CasePayment[];
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  paymentsLoadedAt: Date | null;
  onRefreshPayments: () => void;
  isPaymentsFetching: boolean;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const handleDelete = async (id: string) => {
    setDeletingId(id);
    setOpenMenuId(null);
    try {
      await settlementService.deleteSettlementPayment(id);
      addToast({
        type: "success",
        title: "Payment Deleted",
        description: "The payment record was removed.",
      });
      onRefreshPayments();
    } catch {
      addToast({
        type: "error",
        title: "Delete Failed",
        description: "Failed to delete the payment.",
      });
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <CollapsibleSection title="Payment History" icon="ri-exchange-dollar-line">
      <div className="flex items-center justify-between py-2 border-b border-gray-100 mb-3">
        <span className="text-[11px] text-gray-400">
          Last loaded:{" "}
          {paymentsLoadedAt
            ? paymentsLoadedAt.toLocaleString(undefined, {
                month: "short",
                day: "numeric",
                year: "numeric",
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
              })
            : "—"}
        </span>
        <button
          type="button"
          onClick={onRefreshPayments}
          disabled={isPaymentsFetching}
          className="flex items-center gap-1 text-[11px] text-gray-400 hover:text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        >
          <i
            className={`ri-refresh-line text-xs${isPaymentsFetching ? " animate-spin" : ""}`}
          />
          {isPaymentsFetching ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      {payments.length === 0 ? (
        <div className="text-center py-8">
          <i className="ri-exchange-dollar-line text-2xl text-gray-300" />
          <p className="text-sm text-gray-400 mt-2">No payment history</p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto -mx-5 px-5">
            <table className="min-w-full text-sm">
              <thead>
                <tr className="border-b border-gray-100">
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Payment #
                  </th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Lien ID
                  </th>
                  <th className="px-3 py-2 text-right text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Amount
                  </th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Payment Date
                  </th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Payee
                  </th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Check #
                  </th>
                  <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                    Note
                  </th>
                  <th className="pl-3 py-2 w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50">
                {payments.map((p, idx) => {
                  const rowKey = `paymentHistory${idx}`;
                  const lien = liens.find((l) => l.id === p.lienId);
                  const isDeleting = deletingId === rowKey;
                  const amount =
                    p.amount != null ? parseFloat(String(p.amount)) : null;

                  return (
                    <tr
                      key={rowKey}
                      className="hover:bg-gray-50/50 transition-colors"
                    >
                      <td className="px-3 py-2.5 text-xs font-mono text-gray-500 whitespace-nowrap">
                        {p.paymentNumber != null ? `#${p.paymentNumber}` : "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs font-mono text-primary whitespace-nowrap">
                        {lien?.lienNumber ?? p.lienId ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-sm text-gray-700 font-medium tabular-nums text-right whitespace-nowrap">
                        {amount != null ? formatCurrency(amount) : "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                        {p.paymentDate ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                        {p.payee ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs font-mono text-gray-500 whitespace-nowrap">
                        {p.checkNumber ?? "—"}
                      </td>
                      <td className="px-3 py-2.5 text-xs text-gray-600 whitespace-nowrap">
                        {p.note ?? "—"}
                      </td>
                      <td className="pl-3 py-2.5 text-center relative">
                        {p.id ? (
                          <>
                            <button
                              type="button"
                              disabled={isDeleting}
                              onClick={() =>
                                setOpenMenuId(
                                  openMenuId === rowKey ? null : rowKey,
                                )
                              }
                              className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors disabled:opacity-40"
                            >
                              {isDeleting ? (
                                <i className="ri-loader-4-line text-sm animate-spin" />
                              ) : (
                                <i className="ri-more-2-line text-sm" />
                              )}
                            </button>
                            {openMenuId === rowKey && (
                              <div className="absolute right-0 top-full mt-1 w-32 bg-white border border-gray-200 rounded-lg shadow-lg z-50">
                                <button
                                  type="button"
                                  onClick={() => handleDelete(p.id!)}
                                  className="w-full text-left px-3 py-2 text-sm text-red-600 hover:bg-red-50 transition-colors flex items-center gap-2"
                                >
                                  <i className="ri-delete-bin-line text-sm" />
                                  Delete
                                </button>
                              </div>
                            )}
                          </>
                        ) : (
                          <span className="text-gray-300 text-xs">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="mt-3 pt-3 border-t border-gray-100">
            <p className="text-xs text-gray-400">
              {payments.length} payment{payments.length !== 1 ? "s" : ""}
            </p>
          </div>
        </>
      )}
    </CollapsibleSection>
  );
}

type ServicingSubTab = "servicing-details" | "settlement-details" | "history";

const SERVICING_SUB_TABS: {
  key: ServicingSubTab;
  label: string;
  icon: string;
}[] = [
  {
    key: "servicing-details",
    label: "Servicing Details",
    icon: "ri-settings-3-line",
  },
  {
    key: "settlement-details",
    label: "Settlement Details",
    icon: "ri-money-dollar-circle-line",
  },
  { key: "history", label: "History", icon: "ri-history-line" },
];

function ServicingTab({
  caseDetail,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  payments,
  paymentsLoadedAt,
  onRefreshPayments,
  isPaymentsFetching,
  panelMode,
  onPanelModeChange,
}: {
  caseDetail: CaseDetail;
  liens: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  payments: import("@/lib/settlement/settlement.types").CasePayment[];
  paymentsLoadedAt: Date | null;
  onRefreshPayments: () => void;
  isPaymentsFetching: boolean;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const { data = { items: [], totalCount: 0 }, refetch: refetchLiens } =
    useCaseLiens(caseDetail.id, {}, "all-liens");
  const liens = data.items ?? [];
  const timezone = useTimezone();
  const [subTab, setSubTab] = useState<ServicingSubTab>("servicing-details");
  const [isAddPaymentOpen, setIsAddPaymentOpen] = useState(false);
  const [isNoRecoveryOpen, setIsNoRecoveryOpen] = useState(false);
  const [setupReductionFormShown, showSetupReductionForm] = useState(false);
  const [isLienSettlementOpen, setIsLienSettlementOpen] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const HISTORY_PAGE_SIZE = 10;
  const historyQueryClient = useQueryClient();
  const isHistoryVisible = subTab === "history";
  const historyQuery = useSettlementHistory(
    { caseId: caseDetail.id, page: historyPage, limit: HISTORY_PAGE_SIZE },
    { enabled: isHistoryVisible },
  );
  const historyItems = historyQuery.data?.items ?? [];
  const historyTotalPages = historyQuery.data?.pagination.totalPages ?? 0;
  const historyTotalCount = historyQuery.data?.pagination.totalCount ?? 0;
  const historyLoadedAt = historyQuery.dataUpdatedAt
    ? new Date(historyQuery.dataUpdatedAt)
    : null;
  // Mark history stale on servicing mutations; only force a refetch if the
  // History sub-tab is actually visible, otherwise it'll refetch lazily
  // once the user switches to it (query is disabled while hidden).
  const refetchHistory = () => {
    historyQueryClient.invalidateQueries({
      queryKey: ["settlement-history"],
      refetchType: isHistoryVisible ? "active" : "none",
    });
  };

  /* TEMP: visual fallback data for UI review only */
  const [caseStatus, setCaseStatus] = useState(
    caseDetail.status || "PreDemand",
  );
  const [switchedLawFirm, setSwitchedLawFirm] = useState(false);
  const [switchedDate, setSwitchedDate] = useState("");
  const [currentLawFirm, setCurrentLawFirm] = useState("");
  const [currentLawyer, setCurrentLawyer] = useState("");
  const [currentCaseManager, setCurrentCaseManager] = useState("");

  const [lawyerList, setLawyerList] = useState<
    { key: string; value: string; label: string }[]
  >([]);
  const [caseManagerList, setCaseManagerList] = useState<
    { key: string; value: string; label: string }[]
  >([]);
  const [lawFirmList, setLawFirmList] = useState<
    { key: string; value: string; label: string }[]
  >([]);
  const saveDisabled = true;

  let openLiens = liens.filter((i) => i.closedAtUtc === null);
  let closedLiens = liens.filter((i) => i.closedAtUtc !== null);

  const openLiensTotalBilling = openLiens.reduce(
    (s, l) => s + l.originalAmount,
    0,
  );
  const openLiensTotalBalance = openLiens.reduce((s, l) => s + l.balance, 0);
  const closedLiensTotalBilling = closedLiens.reduce(
    (s, l) => s + l.originalAmount,
    0,
  );
  const closedLiensTotalReduction = closedLiens.reduce(
    (s, l) => s + (l.reductionAmount ?? 0),
    0,
  );
  const closedLiensTotalPayment = closedLiens.reduce(
    (s, l) => s + (l.paymentAmount ?? 0),
    0,
  );

  const { lookup } = useSessionContext();

  const lienDisplayColumns: LienColumnDef[] = [
    {
      id: "lienId",
      header: "Lien ID",
      cell: (l) => (
        <span className="text-xs font-mono text-primary">{l.lienNumber}</span>
      ),
    },
    {
      id: "billing",
      header: "Billing Amt",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.originalAmount)}
        </span>
      ),
    },
    {
      id: "reduction",
      header: "Reduction",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-500 tabular-nums">
          {l.reductionAmount !== null
            ? formatCurrency(l.reductionAmount)
            : "---"}
        </span>
      ),
    },
    {
      id: "payment",
      header: "Payment",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-500 tabular-nums">
          {l.paymentAmount !== null ? formatCurrency(l.paymentAmount) : "---"}
        </span>
      ),
    },
    {
      id: "balance",
      header: "Balance",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 font-medium tabular-nums">
          {formatCurrency(l.balance)}
        </span>
      ),
    },
  ];

  const closedLienDisplayColumns: LienColumnDef[] = [
    {
      id: "lienId",
      header: "Lien ID",
      cell: (l) => (
        <span className="text-xs font-mono text-gray-500">{l.lienNumber}</span>
      ),
    },
    {
      id: "billing",
      header: "Billing Amt",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.originalAmount)}
        </span>
      ),
    },
    {
      id: "reduction",
      header: "Reduction",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-green-600 tabular-nums">
          {formatCurrency(l.reductionAmount)}
        </span>
      ),
    },
    {
      id: "payment",
      header: "Payment",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(l.paymentAmount)}
        </span>
      ),
    },
    {
      id: "balance",
      header: "Balance",
      align: "right",
      cell: (l) => (
        <span className="text-sm text-gray-700 font-medium tabular-nums">
          {formatCurrency(l.balance)}
        </span>
      ),
    },
  ];

  const caseStatusList = lookup?.CaseStatus.map((s) => {
    return { key: s.id, value: s.code, label: s.name };
  });
  const fetchDataLawfirms = useCallback(async () => {
    const lawfirms = await lookupService.getLawfirm();
    setLawFirmList(
      lawfirms.items.map((lf) => ({
        key: lf.id,
        value: lf.id,
        label: lf.displayName,
      })) ?? [],
    );
  }, []);
  const fetchDataLawyers = useCallback(async () => {
    const lawyers = await contactsService.getContacts({
      ContactType: "Lawyer",
    });
    setLawyerList(
      lawyers.items.map((lf) => ({
        key: lf.id,
        value: lf.id,
        label: lf.displayName,
      })) ?? [],
    );
  }, []);

  const fetchDataCaseManagers = useCallback(async () => {
    const caseManagers = await contactsService.getCaseManagers();
    setCaseManagerList(
      caseManagers.items.map((lf) => ({
        key: lf.id,
        value: lf.id,
        label: lf.displayName,
      })) ?? [],
    );
  }, []);

  const handleSaveServicingDetails = async () => {
    const payload = {
      caseId: caseDetail.id,
      caseStatusId: caseStatus,
      isUCCFiled: switchedLawFirm ? "Yes" : "No",
      switchedDate: switchedDate || new Date().toISOString(),
      lawFirmId: currentLawFirm,
      attorney: currentLawyer,
      caseManager: currentCaseManager,
    };

    await servicingService.updateDetails(payload);
    addToast({
      type: "success",
      title: "Servicing Details Saved",
      description: "Your servicing details were saved.",
    });
    setSwitchedLawFirm(false);
  };

  const getCase = async () => {
    const payload = {
      keyword: "",
      page: 1,
      limit: 20,
      sortBy: "",
      sortDirection: "",
    };

    await servicingService.getCase(caseDetail.id);
  };

  useEffect(() => {
    // getCase();
  }, []);

  const leftContent = (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="flex border-b border-gray-100">
          {SERVICING_SUB_TABS.map((st) => (
            <button
              key={st.key}
              onClick={() => setSubTab(st.key)}
              className={[
                "flex-1 px-4 py-2.5 text-xs font-medium transition-colors flex items-center justify-center gap-1.5",
                subTab === st.key
                  ? "text-primary border-b-2 border-primary bg-primary/5"
                  : "text-gray-500 hover:text-gray-700 hover:bg-gray-50",
              ].join(" ")}
            >
              <i className={`${st.icon} text-sm`} />
              {st.label}
            </button>
          ))}
        </div>
      </div>

      {/* <div className="mb-3 px-3 py-2 bg-amber-50 border border-amber-200 rounded-md">
        <p className="text-xs text-amber-700">
          <i className="ri-information-line mr-1" />
          Sample data shown for UI review. Real data will load from the API.
        </p>
      </div> */}

      {subTab === "servicing-details" && (
        <CollapsibleSection title="Servicing Details" icon="ri-settings-3-line">
          <div className="space-y-4">
            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
                Case Status
              </label>
              <Field
                label=""
                value={caseStatus}
                type="select"
                options={caseStatusList}
                onChange={(e) => setCaseStatus(e.toString())}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={switchedLawFirm}
                    onChange={(e) => setSwitchedLawFirm(e.target.checked)}
                    className="w-4 h-4 rounded border-gray-300 text-primary focus:ring-primary/30 cursor-pointer"
                  />
                  <span className="text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                    Switched Law Firm
                  </span>
                </label>
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
                  Switched Date
                </label>
                <input
                  type="date"
                  value={switchedDate}
                  onChange={(e) => setSwitchedDate(e.target.value)}
                  disabled={!switchedLawFirm}
                  className="w-full px-3 py-2 text-sm border border-gray-200 rounded-lg bg-gray-50/50 focus:bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                />
              </div>
            </div>

            <div>
              <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
                Current Law Firm
              </label>
              <Field
                label=""
                disabled={!switchedLawFirm}
                value={currentLawFirm}
                type="select"
                options={lawFirmList}
                onChange={(e) => setCurrentLawFirm(e.toString())}
                onClick={() => {
                  fetchDataLawfirms();
                }}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
                  Current Lawyer
                </label>
                <Field
                  label=""
                  disabled={!switchedLawFirm}
                  value={currentLawyer}
                  type="select"
                  options={lawyerList}
                  onChange={(e) => setCurrentLawyer(e.toString())}
                  onClick={() => {
                    fetchDataLawyers();
                  }}
                />
              </div>
              <div>
                <label className="block text-[11px] font-medium text-gray-400 uppercase tracking-wide mb-1.5">
                  Current Case Manager
                </label>
                <Field
                  label=""
                  disabled={!switchedLawFirm}
                  value={currentCaseManager}
                  type="select"
                  options={caseManagerList}
                  onChange={(e) => setCurrentCaseManager(e.toString())}
                  onClick={() => {
                    fetchDataCaseManagers();
                  }}
                />
              </div>
            </div>

            <div className="pt-2 flex items-center gap-3">
              <button
                disabled={!switchedLawFirm}
                onClick={() => handleSaveServicingDetails()}
                className="px-6 py-2.5 text-sm font-medium bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <i className="ri-save-line text-sm" />
                Save
              </button>
            </div>
          </div>
        </CollapsibleSection>
      )}

      {subTab === "settlement-details" && (
        <div className="space-y-4">
          {/*
            OLD PATTERN (commented out): Standalone "Reduction" CollapsibleSection
            from the legacy UX. The new design moves "Setup Reduction" as a
            contextual action button on the Open Liens table below, which ties
            the reduction directly to the liens being reduced and eliminates the
            duplicate entry point. The Open Liens table already shows per-lien
            Reduction / Payment / Balance columns making this section redundant.

          <CollapsibleSection title="Reduction" icon="ri-percent-line">
            <div className="text-center py-4">
              <p className="text-sm text-gray-500 mb-3">
                No reductions have been configured for open liens.
              </p>
              <button className="...">
                <i className="ri-add-line text-sm" />
                Setup Reduction
              </button>
            </div>
          </CollapsibleSection>
          */}

          <CollapsibleSection title="Open Liens" icon="ri-stack-line">
            {openLiens.length === 0 ? (
              <div className="-mx-5 border border-gray-100 rounded-none border-x-0 overflow-hidden">
                <LienTableToolbar
                  loadedAt={liensLoadedAt}
                  onRefresh={onRefreshLiens}
                  isRefreshing={isLiensFetching}
                />
                <div className="text-center py-8">
                  <i className="ri-stack-line text-2xl text-gray-300" />
                  <p className="text-sm text-gray-400 mt-2">No open liens</p>
                </div>
              </div>
            ) : (
              <>
                <div className="-mx-5">
                  <LienTable
                    liens={openLiens}
                    columns={lienDisplayColumns}
                    footer={[
                      {
                        colSpan: 2,
                        content: (
                          <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                            Totals ({openLiens.length} lien
                            {openLiens.length !== 1 ? "s" : ""})
                          </span>
                        ),
                      },
                      {
                        align: "right",
                        content: (
                          <span className="text-sm font-semibold text-gray-700 tabular-nums">
                            {formatCurrency(openLiensTotalBilling)}
                          </span>
                        ),
                      },
                      {
                        align: "right",
                        content: (
                          <span className="text-sm text-gray-400">---</span>
                        ),
                      },
                      {
                        align: "right",
                        content: (
                          <span className="text-sm text-gray-400">---</span>
                        ),
                      },
                      {
                        align: "right",
                        content: (
                          <span className="text-sm font-semibold text-gray-700 tabular-nums">
                            {formatCurrency(openLiensTotalBalance)}
                          </span>
                        ),
                      },
                    ]}
                    loadedAt={liensLoadedAt}
                    onRefresh={onRefreshLiens}
                    isRefreshing={isLiensFetching}
                    className="rounded-none border-x-0 border-t-0"
                  />
                </div>
                <div className="mt-3 pt-3 border-t border-gray-100 flex items-center gap-2">
                  <button
                    onClick={() => showSetupReductionForm(true)}
                    className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1"
                  >
                    <i className="ri-percent-line text-sm" />
                    Setup Reduction
                  </button>
                  <button
                    onClick={() => setIsNoRecoveryOpen(true)}
                    className="px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 border border-red-200 rounded-md hover:bg-red-100 transition-colors inline-flex items-center gap-1"
                  >
                    <i className="ri-close-circle-line text-sm" />
                    No Recovery
                  </button>
                  <button
                    onClick={() => setIsAddPaymentOpen(true)}
                    className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1"
                  >
                    <i className="ri-money-dollar-circle-line text-sm" />
                    Add Payment
                  </button>
                  {/* not existing on legacy disabled for now, also my implementation could be wrong */}
                  {/* <button
                    onClick={() => setIsLienSettlementOpen(true)}
                    className="px-3 py-1.5 text-xs font-medium text-primary bg-primary/5 border border-primary/20 rounded-md hover:bg-primary/10 transition-colors inline-flex items-center gap-1"
                  >
                    <i className="ri-hand-coin-line text-sm" />
                    Lien Settlement
                  </button> */}
                </div>
              </>
            )}
          </CollapsibleSection>

          <CollapsibleSection
            title="Closed Liens"
            icon="ri-checkbox-circle-line"
          >
            {closedLiens.length === 0 ? (
              <div className="-mx-5 border border-gray-100 rounded-none border-x-0 overflow-hidden">
                <LienTableToolbar
                  loadedAt={liensLoadedAt}
                  onRefresh={onRefreshLiens}
                  isRefreshing={isLiensFetching}
                />
                <div className="text-center py-8">
                  <i className="ri-checkbox-circle-line text-2xl text-gray-300" />
                  <p className="text-sm text-gray-400 mt-2">No closed liens</p>
                </div>
              </div>
            ) : (
              <div className="-mx-5">
                <LienTable
                  liens={closedLiens}
                  columns={closedLienDisplayColumns}
                  footer={[
                    {
                      colSpan: 2,
                      content: (
                        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                          Totals ({closedLiens.length} lien
                          {closedLiens.length !== 1 ? "s" : ""})
                        </span>
                      ),
                    },
                    {
                      align: "right",
                      content: (
                        <span className="text-sm font-semibold text-gray-700 tabular-nums">
                          {formatCurrency(closedLiensTotalBilling)}
                        </span>
                      ),
                    },
                    {
                      align: "right",
                      content: (
                        <span className="text-sm font-semibold text-green-600 tabular-nums">
                          {formatCurrency(closedLiensTotalReduction)}
                        </span>
                      ),
                    },
                    {
                      align: "right",
                      content: (
                        <span className="text-sm font-semibold text-gray-700 tabular-nums">
                          {formatCurrency(closedLiensTotalPayment)}
                        </span>
                      ),
                    },
                    {
                      align: "right",
                      content: (
                        <span className="text-sm font-semibold text-gray-700 tabular-nums">
                          {formatCurrency(0)}
                        </span>
                      ),
                    },
                  ]}
                  loadedAt={liensLoadedAt}
                  onRefresh={onRefreshLiens}
                  isRefreshing={isLiensFetching}
                  className="rounded-none border-x-0 border-t-0"
                />
              </div>
            )}
          </CollapsibleSection>

          <PaymentHistorySection
            payments={payments}
            liens={liens}
            paymentsLoadedAt={paymentsLoadedAt}
            onRefreshPayments={() => {
              onRefreshPayments();
              refetchHistory();
            }}
            isPaymentsFetching={isPaymentsFetching}
          />
        </div>
      )}

      {subTab === "history" && (
        <CollapsibleSection title="Servicing History" icon="ri-history-line">
          <div className="flex items-center justify-between py-2 border-b border-gray-100 mb-3">
            <span className="text-[11px] text-gray-400">
              Last loaded:{" "}
              {historyLoadedAt
                ? historyLoadedAt.toLocaleString(undefined, {
                    month: "short",
                    day: "numeric",
                    year: "numeric",
                    hour: "2-digit",
                    minute: "2-digit",
                    second: "2-digit",
                  })
                : "—"}
            </span>
            <button
              type="button"
              onClick={() => historyQuery.refetch()}
              disabled={historyQuery.isFetching}
              className="flex items-center gap-1 text-[11px] text-gray-400 hover:text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <i
                className={`ri-refresh-line text-xs${historyQuery.isFetching ? " animate-spin" : ""}`}
              />
              {historyQuery.isFetching ? "Refreshing..." : "Refresh"}
            </button>
          </div>

          {historyQuery.isLoading ? (
            <div className="text-center py-8">
              <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
              <p className="text-sm text-gray-400 mt-2">Loading history...</p>
            </div>
          ) : historyItems.length === 0 ? (
            <div className="text-center py-8">
              <i className="ri-history-line text-2xl text-gray-300" />
              <p className="text-sm text-gray-400 mt-2">No history records</p>
            </div>
          ) : (
            <>
              <div className="overflow-x-auto -mx-5 px-5">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b border-gray-100">
                      <th className="pr-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Timestamp
                      </th>
                      <th className="px-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide">
                        Description
                      </th>
                      <th className="pl-3 py-2 text-left text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap">
                        Updated By
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-50">
                    {historyItems.map((h) => (
                      <tr
                        key={h.id}
                        className="hover:bg-gray-50/50 transition-colors"
                      >
                        <td className="pr-3 py-2.5 text-xs text-gray-500 whitespace-nowrap">
                          {formatNoteTimestamp(h.createdAt, timezone)}
                        </td>
                        <td className="px-3 py-2.5 text-sm text-gray-600">
                          {describeSettlementHistoryItem(h)}
                        </td>
                        <td className="pl-3 py-2.5 text-sm text-gray-500 whitespace-nowrap">
                          —
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-3 pt-3 border-t border-gray-100 flex items-center justify-between">
                <p className="text-xs text-gray-400">
                  Page {historyPage} of {historyTotalPages} ·{" "}
                  {historyTotalCount} total
                </p>
                <Pagination
                  page={historyPage}
                  totalPages={historyTotalPages}
                  onPageChange={setHistoryPage}
                />
              </div>
            </>
          )}
        </CollapsibleSection>
      )}
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <CollapsibleSection title="Email" icon="ri-mail-send-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-mail-send-line text-sm" />
            Compose New Email
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="SMS" icon="ri-message-2-line">
        <div className="flex justify-center py-2">
          <button className="w-full px-6 py-2.5 bg-primary text-white text-sm font-medium rounded-lg hover:bg-primary/90 transition-colors flex items-center justify-center gap-2">
            <i className="ri-message-2-line text-sm" />
            Send SMS
          </button>
        </div>
      </CollapsibleSection>

      <CollapsibleSection title="Contacts" icon="ri-contacts-line">
        {/* TEMP: visual fallback data for UI review only */}
        <div className="space-y-2">
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <i className="ri-user-line text-sm text-primary" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                Sarah Mitchell
              </p>
              <p className="text-xs text-gray-400">Case Manager</p>
            </div>
          </div>
          <div className="flex items-center gap-3 p-2.5 rounded-lg bg-gray-50">
            <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center shrink-0">
              <i className="ri-building-line text-sm text-blue-500" />
            </div>
            <div className="min-w-0">
              <p className="text-sm text-gray-700 font-medium truncate">
                {caseDetail.insuranceCarrier || ""}
              </p>
              <p className="text-xs text-gray-400">Law Firm</p>
            </div>
          </div>
        </div>
      </CollapsibleSection>
    </div>
  );

  return (
    <>
      <LayoutSplit
        left={leftContent}
        right={rightContent}
        mode={panelMode}
        onModeChange={onPanelModeChange}
      />
      <SetupReductionForm
        open={setupReductionFormShown}
        onClose={() => showSetupReductionForm(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          showSetupReductionForm(false);
          onRefreshLiens();
          refetchHistory();
        }}
      />
      <NoRecoveryForm
        open={isNoRecoveryOpen}
        onClose={() => setIsNoRecoveryOpen(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          setIsNoRecoveryOpen(false);
          onRefreshLiens();
          refetchHistory();
        }}
      />
      <AddPaymentForm
        open={isAddPaymentOpen}
        onClose={() => setIsAddPaymentOpen(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          setIsAddPaymentOpen(false);
          onRefreshPayments();
          refetchHistory();
        }}
      />
      {/* <LienSettlementForm
        open={isLienSettlementOpen}
        onClose={() => setIsLienSettlementOpen(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => { setIsLienSettlementOpen(false); onRefreshLiens(); }}
      /> */}
    </>
  );
}

/* TEMP: visual fallback data for UI review only */
const NOTE_CATEGORY_LABELS: Record<string, string> = {
  general: "General",
  internal: "Internal",
  "follow-up": "Follow-Up",
};

const NOTE_CATEGORY_COLORS: Record<string, string> = {
  general: "bg-blue-50 text-blue-600 border-blue-200",
  internal: "bg-purple-50 text-purple-600 border-purple-200",
  "follow-up": "bg-amber-50 text-amber-600 border-amber-200",
};

function formatNoteDate(iso: string, timezone: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHrs = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHrs < 24) return `${diffHrs}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return d.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: timezone,
  });
}

function formatNoteTimestamp(iso: string, timezone: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "";
  return d.toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
    timeZone: timezone,
  });
}

function getInitials(name: string): string {
  return name
    .split(" ")
    .map((w) => w[0])
    .join("")
    .toUpperCase()
    .slice(0, 2);
}

const AVATAR_COLORS = [
  "bg-blue-100 text-blue-700",
  "bg-emerald-100 text-emerald-700",
  "bg-purple-100 text-purple-700",
  "bg-amber-100 text-amber-700",
  "bg-rose-100 text-rose-700",
  "bg-cyan-100 text-cyan-700",
];

function avatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++)
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

function NotesTab({ caseId }: { caseId: string }) {
  const addToast = useLienStore((s) => s.addToast);
  const { session } = useSession();
  const timezone = useTimezone();

  const [notes, setNotes] = useState<CaseNoteResponse[]>([]);
  const [notesLoading, setNotesLoading] = useState(true);
  const [notesError, setNotesError] = useState<string | null>(null);

  const [composerText, setComposerText] = useState("");
  const [composerCategory, setComposerCategory] =
    useState<CaseNoteCategory>("general");
  const [composerExpanded, setComposerExpanded] = useState(false);
  const [composerSubmitting, setComposerSubmitting] = useState(false);

  const [editingNoteId, setEditingNoteId] = useState<string | null>(null);
  const [editingText, setEditingText] = useState("");
  const [editingCategory, setEditingCategory] =
    useState<CaseNoteCategory>("general");
  const [editSubmitting, setEditSubmitting] = useState(false);
  const [deletingNoteId, setDeletingNoteId] = useState<string | null>(null);
  const [pinningNoteId, setPinningNoteId] = useState<string | null>(null);

  const [sortOrder, setSortOrder] = useState<"newest" | "oldest">("newest");
  const [categoryFilter, setCategoryFilter] = useState<
    "all" | CaseNoteCategory
  >("all");
  const [searchQuery, setSearchQuery] = useState("");

  const authorName = emailToDisplayName(session?.email);
  const currentUserId = session?.userId;

  const loadNotes = useCallback(async () => {
    setNotesLoading(true);
    setNotesError(null);
    try {
      const data = await lienCaseNotesService.getNotes(caseId);
      setNotes(data);
    } catch {
      setNotesError("Failed to load notes");
    } finally {
      setNotesLoading(false);
    }
  }, [caseId]);

  useEffect(() => {
    loadNotes();
  }, [loadNotes]);

  const filteredNotes = useMemo(() => {
    let result = [...notes];

    if (categoryFilter !== "all") {
      result = result.filter((n) => n.category === categoryFilter);
    }

    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      result = result.filter(
        (n) =>
          n.content.toLowerCase().includes(q) ||
          n.createdByName.toLowerCase().includes(q),
      );
    }

    result.sort((a, b) => {
      const ta = new Date(a.createdAtUtc).getTime() || 0;
      const tb = new Date(b.createdAtUtc).getTime() || 0;
      return sortOrder === "newest" ? tb - ta : ta - tb;
    });

    const pinned = result.filter((n) => n.isPinned);
    const unpinned = result.filter((n) => !n.isPinned);
    return [...pinned, ...unpinned];
  }, [notes, categoryFilter, searchQuery, sortOrder]);

  const hasActiveFilters =
    categoryFilter !== "all" || searchQuery.trim() !== "";

  const handleSubmit = async () => {
    const text = composerText.trim();
    if (!text || composerSubmitting) return;
    setComposerSubmitting(true);
    try {
      const created = await lienCaseNotesService.createNote(
        caseId,
        text,
        composerCategory,
        authorName,
      );
      setNotes((prev) => [created, ...prev]);
      setComposerText("");
      setComposerCategory("general");
      setComposerExpanded(false);
      addToast({
        type: "success",
        title: "Note Added",
        description: "Your note was saved.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to add note.",
      });
    } finally {
      setComposerSubmitting(false);
    }
  };

  const handleStartEdit = (note: CaseNoteResponse) => {
    setEditingNoteId(note.id);
    setEditingText(note.content);
    setEditingCategory(note.category);
  };

  const handleCancelEdit = () => {
    setEditingNoteId(null);
    setEditingText("");
  };

  const handleSaveEdit = async (note: CaseNoteResponse) => {
    if (editSubmitting) return;
    setEditSubmitting(true);
    try {
      const updated = await lienCaseNotesService.updateNote(
        caseId,
        note.id,
        editingText.trim(),
        editingCategory,
      );
      setNotes((prev) => prev.map((n) => (n.id === updated.id ? updated : n)));
      setEditingNoteId(null);
      addToast({
        type: "success",
        title: "Note Updated",
        description: "Your note was saved.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to update note.",
      });
    } finally {
      setEditSubmitting(false);
    }
  };

  const handleDelete = async (noteId: string) => {
    if (deletingNoteId === noteId) return;
    setDeletingNoteId(noteId);
    try {
      await lienCaseNotesService.deleteNote(caseId, noteId);
      setNotes((prev) => prev.filter((n) => n.id !== noteId));
      addToast({
        type: "success",
        title: "Note Deleted",
        description: "The note was removed.",
      });
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to delete note.",
      });
    } finally {
      setDeletingNoteId(null);
    }
  };

  const handlePin = async (note: CaseNoteResponse) => {
    if (pinningNoteId === note.id) return;
    setPinningNoteId(note.id);
    try {
      const updated = note.isPinned
        ? await lienCaseNotesService.unpinNote(caseId, note.id)
        : await lienCaseNotesService.pinNote(caseId, note.id);
      setNotes((prev) => prev.map((n) => (n.id === updated.id ? updated : n)));
    } catch {
      addToast({
        type: "error",
        title: "Error",
        description: "Failed to update pin status.",
      });
    } finally {
      setPinningNoteId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="px-5 py-3 flex items-center justify-between border-b border-gray-100">
          <div className="flex items-center gap-2">
            <i className="ri-chat-quote-line text-sm text-gray-500" />
            <h3 className="text-sm font-semibold text-gray-800">Case Notes</h3>
            {!notesLoading && (
              <span className="ml-1 inline-flex items-center justify-center min-w-[18px] h-[18px] px-1 text-[10px] font-semibold rounded-full bg-primary/10 text-primary">
                {filteredNotes.length}
                {hasActiveFilters ? `/${notes.length}` : ""}
              </span>
            )}
          </div>
          <p className="text-[11px] text-gray-400">
            Internal case commentary and collaboration
          </p>
        </div>

        <div className="px-5 py-4 border-b border-gray-100 bg-gray-50/30">
          <div
            className={[
              "border rounded-lg bg-white transition-all",
              composerExpanded
                ? "border-primary/30 shadow-sm ring-1 ring-primary/10"
                : "border-gray-200",
            ].join(" ")}
          >
            <div className="flex items-start gap-3 p-3">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-xs font-semibold ${avatarColor(authorName)}`}
              >
                {getInitials(authorName)}
              </div>
              <div className="flex-1 min-w-0">
                <textarea
                  value={composerText}
                  onChange={(e) => setComposerText(e.target.value)}
                  onFocus={() => setComposerExpanded(true)}
                  placeholder="Add a note to this case..."
                  rows={composerExpanded ? 4 : 2}
                  className="w-full text-sm text-gray-700 placeholder:text-gray-400 focus:outline-none resize-none bg-transparent"
                />
              </div>
            </div>
            {composerExpanded && (
              <div className="px-3 pb-3 flex items-center justify-between border-t border-gray-100 pt-2.5">
                <div className="flex items-center gap-2">
                  <div className="relative">
                    <select
                      value={composerCategory}
                      onChange={(e) =>
                        setComposerCategory(e.target.value as CaseNoteCategory)
                      }
                      className="pl-2 pr-6 py-1 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none"
                    >
                      <option value="general">General</option>
                      <option value="internal">Internal</option>
                      <option value="follow-up">Follow-Up</option>
                    </select>
                    <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => {
                      setComposerExpanded(false);
                      setComposerText("");
                    }}
                    className="px-3 py-1.5 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleSubmit}
                    disabled={!composerText.trim() || composerSubmitting}
                    className="px-4 py-1.5 text-xs font-medium text-white bg-primary rounded-lg hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed transition-colors inline-flex items-center gap-1.5"
                  >
                    {composerSubmitting ? (
                      <i className="ri-loader-4-line text-xs animate-spin" />
                    ) : (
                      <i className="ri-send-plane-line text-xs" />
                    )}
                    Add Note
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="px-5 py-2.5 border-b border-gray-100 flex items-center gap-2 flex-wrap">
          <div className="relative flex-1 min-w-[160px] max-w-[240px]">
            <i className="ri-search-line absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400 text-xs" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search notes..."
              className="w-full pl-7 pr-3 py-1.5 text-xs border border-gray-200 rounded-lg bg-white focus:border-primary/40 focus:ring-1 focus:ring-primary/20 outline-none transition-all"
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery("")}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
              >
                <i className="ri-close-line text-xs" />
              </button>
            )}
          </div>

          <div className="flex items-center bg-gray-100 rounded-lg p-0.5">
            {(["all", "general", "internal", "follow-up"] as const).map(
              (cat) => (
                <button
                  key={cat}
                  onClick={() => setCategoryFilter(cat)}
                  className={[
                    "px-2.5 py-1 text-[11px] font-medium rounded-md transition-colors",
                    categoryFilter === cat
                      ? "bg-white text-gray-800 shadow-sm"
                      : "text-gray-500 hover:text-gray-700",
                  ].join(" ")}
                >
                  {cat === "all" ? "All" : NOTE_CATEGORY_LABELS[cat]}
                </button>
              ),
            )}
          </div>

          <div className="ml-auto flex items-center gap-1.5">
            <button
              onClick={() =>
                setSortOrder(sortOrder === "newest" ? "oldest" : "newest")
              }
              className="px-2.5 py-1.5 text-[11px] font-medium text-gray-500 border border-gray-200 rounded-lg bg-white hover:border-gray-300 inline-flex items-center gap-1 transition-colors"
            >
              <i
                className={`ri-sort-${sortOrder === "newest" ? "desc" : "asc"} text-xs`}
              />
              {sortOrder === "newest" ? "Newest First" : "Oldest First"}
            </button>
          </div>
        </div>

        <div className="px-5 py-4">
          {notesLoading ? (
            <div className="text-center py-8">
              <i className="ri-loader-4-line text-2xl text-gray-300 animate-spin" />
              <p className="text-sm text-gray-400 mt-2">Loading notes...</p>
            </div>
          ) : notesError ? (
            <div className="text-center py-8">
              <i className="ri-error-warning-line text-2xl text-red-300" />
              <p className="text-sm text-red-500 mt-2">{notesError}</p>
              <button
                onClick={loadNotes}
                className="text-xs text-primary hover:text-primary/80 mt-2 transition-colors"
              >
                Retry
              </button>
            </div>
          ) : filteredNotes.length === 0 ? (
            <div className="text-center py-8">
              <i
                className={`${hasActiveFilters ? "ri-filter-off-line" : "ri-chat-quote-line"} text-2xl text-gray-300`}
              />
              <p className="text-sm text-gray-400 mt-2">
                {hasActiveFilters
                  ? "No notes match the current filters"
                  : "No notes yet"}
              </p>
              {hasActiveFilters && (
                <button
                  onClick={() => {
                    setCategoryFilter("all");
                    setSearchQuery("");
                  }}
                  className="text-xs text-primary hover:text-primary/80 mt-1 transition-colors"
                >
                  Clear filters
                </button>
              )}
              {!hasActiveFilters && (
                <p className="text-xs text-gray-300 mt-1">
                  Use the composer above to add the first note
                </p>
              )}
            </div>
          ) : (
            <div className="relative">
              <div className="absolute left-[19px] top-4 bottom-4 w-px bg-gray-100" />

              <div className="space-y-0">
                {filteredNotes.map((note, idx) => {
                  const noteDate = new Date(note.createdAtUtc);
                  const noteDateStr = isNaN(noteDate.getTime())
                    ? ""
                    : noteDate.toDateString();
                  const prevDate =
                    idx > 0
                      ? new Date(filteredNotes[idx - 1].createdAtUtc)
                      : null;
                  const prevDateStr =
                    prevDate && !isNaN(prevDate.getTime())
                      ? prevDate.toDateString()
                      : "";
                  const showDateSeparator =
                    idx === 0 || noteDateStr !== prevDateStr;
                  const isOwner = isNoteOwner(
                    currentUserId,
                    note.createdByUserId,
                  );
                  const isEditing = editingNoteId === note.id;
                  const isDeleting = deletingNoteId === note.id;
                  const isPinning = pinningNoteId === note.id;

                  return (
                    <div key={note.id}>
                      {showDateSeparator && noteDateStr && (
                        <div className="flex items-center gap-3 py-2 pl-[30px]">
                          <span className="text-[10px] font-semibold text-gray-400 uppercase tracking-wide">
                            {noteDate.toLocaleDateString("en-US", {
                              weekday: "long",
                              month: "short",
                              day: "numeric",
                              timeZone: timezone,
                            })}
                          </span>
                          <div className="flex-1 h-px bg-gray-100" />
                        </div>
                      )}

                      <div className="flex gap-3 py-2.5 group relative">
                        <div className="relative z-10 shrink-0">
                          <div
                            className={`w-[38px] h-[38px] rounded-full flex items-center justify-center text-[11px] font-semibold ${avatarColor(note.createdByName)}`}
                          >
                            {getInitials(note.createdByName)}
                          </div>
                        </div>

                        <div className="flex-1 min-w-0">
                          {isEditing ? (
                            <div className="bg-white rounded-lg border border-primary/30 shadow-sm ring-1 ring-primary/10 px-4 py-3">
                              <div className="flex items-center gap-2 mb-2">
                                <div className="relative">
                                  <select
                                    value={editingCategory}
                                    onChange={(e) =>
                                      setEditingCategory(
                                        e.target.value as CaseNoteCategory,
                                      )
                                    }
                                    className="pl-2 pr-6 py-0.5 text-[11px] font-medium border border-gray-200 rounded-md bg-white appearance-none cursor-pointer focus:border-primary/40 outline-none"
                                  >
                                    <option value="general">General</option>
                                    <option value="internal">Internal</option>
                                    <option value="follow-up">Follow-Up</option>
                                  </select>
                                  <i className="ri-arrow-down-s-line absolute right-1.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none text-[10px]" />
                                </div>
                              </div>
                              <textarea
                                value={editingText}
                                onChange={(e) => setEditingText(e.target.value)}
                                rows={4}
                                className="w-full text-sm text-gray-700 focus:outline-none resize-none bg-transparent"
                                autoFocus
                              />
                              <div className="flex items-center justify-end gap-2 mt-2 pt-2 border-t border-gray-100">
                                <button
                                  onClick={handleCancelEdit}
                                  className="px-3 py-1 text-xs font-medium text-gray-500 hover:text-gray-700 transition-colors"
                                >
                                  Cancel
                                </button>
                                <button
                                  onClick={() => handleSaveEdit(note)}
                                  disabled={
                                    !editingText.trim() || editSubmitting
                                  }
                                  className="px-3 py-1 text-xs font-medium text-white bg-primary rounded-md hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed inline-flex items-center gap-1 transition-colors"
                                >
                                  {editSubmitting ? (
                                    <i className="ri-loader-4-line text-xs animate-spin" />
                                  ) : null}
                                  Save
                                </button>
                              </div>
                            </div>
                          ) : (
                            <div className="bg-gray-50 rounded-lg px-4 py-3 border border-gray-100 hover:border-gray-200 transition-colors">
                              <div className="flex items-center gap-2 mb-1.5">
                                <span className="text-xs font-semibold text-gray-700">
                                  {note.createdByName}
                                </span>
                                {note.category &&
                                  note.category !== "general" && (
                                    <span
                                      className={`inline-flex items-center px-1.5 py-0.5 text-[10px] font-medium rounded border ${NOTE_CATEGORY_COLORS[note.category]}`}
                                    >
                                      {NOTE_CATEGORY_LABELS[note.category]}
                                    </span>
                                  )}
                                {note.isPinned && (
                                  <span className="inline-flex items-center gap-0.5 text-[10px] text-amber-500">
                                    <i className="ri-pushpin-2-fill text-[10px]" />
                                    Pinned
                                  </span>
                                )}
                                {note.isEdited && (
                                  <span
                                    className="text-[10px] text-gray-400 italic"
                                    title={
                                      note.updatedAtUtc
                                        ? `Edited ${formatNoteTimestamp(note.updatedAtUtc, timezone)}`
                                        : "Edited"
                                    }
                                  >
                                    edited
                                  </span>
                                )}
                                <span
                                  className="text-[11px] text-gray-400 ml-auto"
                                  title={formatNoteTimestamp(
                                    note.createdAtUtc,
                                    timezone,
                                  )}
                                >
                                  {formatNoteDate(note.createdAtUtc, timezone)}
                                </span>
                                <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                                  <button
                                    onClick={() => handlePin(note)}
                                    disabled={isPinning}
                                    title={note.isPinned ? "Unpin" : "Pin"}
                                    className="p-1 rounded text-gray-400 hover:text-amber-500 hover:bg-amber-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                  >
                                    {isPinning ? (
                                      <i className="ri-loader-4-line text-xs animate-spin" />
                                    ) : (
                                      <i
                                        className={`${note.isPinned ? "ri-pushpin-fill" : "ri-pushpin-line"} text-xs`}
                                      />
                                    )}
                                  </button>
                                  {isOwner && (
                                    <>
                                      <button
                                        onClick={() => handleStartEdit(note)}
                                        disabled={isDeleting || isPinning}
                                        title="Edit"
                                        className="p-1 rounded text-gray-400 hover:text-primary hover:bg-primary/5 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                      >
                                        <i className="ri-edit-line text-xs" />
                                      </button>
                                      <button
                                        onClick={() => handleDelete(note.id)}
                                        disabled={isDeleting || isPinning}
                                        title="Delete"
                                        className="p-1 rounded text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
                                      >
                                        {isDeleting ? (
                                          <i className="ri-loader-4-line text-xs animate-spin" />
                                        ) : (
                                          <i className="ri-delete-bin-line text-xs" />
                                        )}
                                      </button>
                                    </>
                                  )}
                                </div>
                              </div>
                              <p className="text-sm text-gray-600 leading-relaxed whitespace-pre-wrap">
                                {note.content}
                              </p>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>

        <div className="px-5 py-3 border-t border-gray-100 flex items-center justify-between">
          <p className="text-xs text-gray-400">
            {notesLoading
              ? "Loading..."
              : `${filteredNotes.length} note${filteredNotes.length !== 1 ? "s" : ""}${hasActiveFilters ? ` (filtered from ${notes.length})` : ""}`}
          </p>
        </div>
      </div>
    </div>
  );
}

function TaskManagerTab({ caseDetail }: { caseDetail: CaseDetail }) {
  const { active } = useCaseWorkflows(caseDetail.id);
  const [workflowDetail, setWorkflowDetail] =
    useState<WorkflowInstanceDetail | null>(null);

  useEffect(() => {
    const instanceId = active?.workflowInstanceId;
    if (!instanceId) {
      setWorkflowDetail(null);
      return;
    }
    workflowApi
      .getDetail(caseDetail.id, instanceId)
      .then((res) => setWorkflowDetail(res.data ?? null))
      .catch(() => setWorkflowDetail(null));
  }, [caseDetail.id, active?.workflowInstanceId]);

  return (
    <CaseTaskManager
      caseId={caseDetail.id}
      workflowStageId={workflowDetail?.currentStageId ?? undefined}
    />
  );
}
