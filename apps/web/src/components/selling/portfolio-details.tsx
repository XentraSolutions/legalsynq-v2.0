"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { ArrowLeft, Clipboard, Copy } from "lucide-react";
import { LienDetailsResult } from "@/types/lien-selling";
import { LienInformationPanel } from "./lien-detail/lien-information-panel";
import { FundingCompanyAndCaseInformationPanel } from "./lien-detail/funding-company-information-panel";
import { MedicalCodesInformationPanel } from "./lien-detail/medical-codes-information-panel";
import { EditLienInformationModal } from "./lien-detail/edit-lien-information-modal";
import { EditCaseInformationModal } from "./lien-detail/edit-case-information-modal";
import { EditMedicalPricingModal } from "./lien-detail/edit-medical-pricing-modal";
import { ConfirmDialog, Modal } from "@/components/selling/modal";
import UploadDocuments from "./forms/add-medical-lien/medical-upload-document";
import { fileIconFor, UploadedFileRow } from "./uploaded-file-row";
import {
  SALE_DOCUMENT_LABELS,
  camelCaseToLabel,
} from "@/lib/selling/selling-detail.mapper";
import { useLienDocuments, useSaveLienDocuments } from "@/lib/selling/use-lien-documents";
import { useLienActivity } from "@/lib/selling/use-lien-activity";
import { PaymentTab } from "./lien-detail/payment-tab";
import { SkeletonFileRow } from "@/components/lien/skeleton-loader";
import { toast } from "sonner";
import { Tabs } from "@/components/selling/tabs";
import { LienRowActionsMenu } from "./lien-row-actions-menu";
import { Button } from "@/components/selling/button";
import { ContactsEmptyState } from "@/components/selling/contacts/contacts-empty-state";

interface LienDetailPanelProps {
  lien: LienDetailsResult;
  onRefresh: () => void;
}

const BASE_PATH = "/selling/portfolio";

const TABS = [
  { key: "details", label: "Details" },
  { key: "documents", label: "Documents" },
  { key: "payment", label: "Payment" },
  { key: "activity", label: "Activity" },
] as const;
type TabKey = (typeof TABS)[number]["key"];

type EditModalKey = "lien-information" | "case-information" | "medical-pricing";

export function PortfolioDetailSkeleton() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-full bg-gray-100 shrink-0" />
          <div className="space-y-2">
            <div className="h-6 w-56 bg-gray-100 rounded" />
            <div className="h-3.5 w-40 bg-gray-100 rounded" />
          </div>
        </div>
        <div className="flex items-center gap-3">
          <div className="h-7 w-20 bg-gray-100 rounded-full" />
          <div className="h-10 w-32 bg-gray-100 rounded-lg" />
        </div>
      </div>

      <div className="h-[38px] w-64 bg-[#FAFAFA] rounded-md" />

      {Array.from({ length: 3 }).map((_, i) => (
        <div
          key={i}
          className="bg-white border border-gray-200 rounded-lg px-6 py-5 space-y-4"
        >
          <div className="h-4 w-40 bg-gray-100 rounded" />
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            {Array.from({ length: 4 }).map((_, j) => (
              <div key={j} className="space-y-2">
                <div className="h-3 w-24 bg-gray-100 rounded" />
                <div className="h-4 w-32 bg-gray-100 rounded" />
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

export function PortfolioDetailPanel({
  lien,
  onRefresh,
}: LienDetailPanelProps) {
  const [activeTab, setActiveTab] = useState<TabKey>("details");
  const [editModal, setEditModal] = useState<EditModalKey | null>(null);
  const { data: docs = [] } = useLienDocuments(lien.lienId);

  const title = lien.fundingCompany?.name || lien.lienInformation.lienNumber;
  const sellerStatus = lien.lienInformation.sellerStatus;
  const canEdit = ["Draft", "Pending", "Internal"].includes(sellerStatus);

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            href={BASE_PATH}
            aria-label="Back to Portfolio"
            className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors shrink-0"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <div className="min-w-0">
            <h1 className="text-2xl font-bold text-gray-900 truncate">
              {title}
            </h1>
            <p className="text-sm text-gray-400 mt-1 truncate">
              {lien.lienInformation.lienNumber}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3 shrink-0">
          <LienRowActionsMenu
            lienId={lien.lienId}
            availableActions={lien.availableActions}
            onActionComplete={onRefresh}
            autoOpenDecision={sellerStatus === "Pending"}
            trigger={
              <Button variant="primary" rightIcon="chevronDown">
                Manage Lien
              </Button>
            }
          />
        </div>
      </div>

      <div className="basis-2/4">
        <Tabs
          bordered={false}
          defaultTab={activeTab}
          onChange={(key) => setActiveTab(key as TabKey)}
          tabs={TABS.map((tab) => ({
            key: tab.key,
            label: tab.label,
            badge: tab.key === "documents" ? docs.length : undefined,
          }))}
        />
      </div>

      {activeTab === "details" && (
        <>
          <LienInformationPanel
            lien={lien.lienInformation}
            onEdit={
              canEdit ? () => setEditModal("lien-information") : undefined
            }
          />
          <FundingCompanyAndCaseInformationPanel
            fundingCompany={lien.fundingCompany}
            facility={lien.facility}
            medicalProvider={lien.medicalProvider}
            caseInformation={lien.caseInformation}
            onEdit={
              canEdit ? () => setEditModal("case-information") : undefined
            }
          />
          <MedicalCodesInformationPanel
            lien={lien.medicalPricing.rows}
            onEdit={canEdit ? () => setEditModal("medical-pricing") : undefined}
          />
        </>
      )}
      {activeTab === "documents" && <DocumentsTab lien={lien} />}
      {activeTab === "payment" && <PaymentTab lien={lien} />}
      {activeTab === "activity" && <ActivityTab lienId={lien.lienId} />}

      {editModal === "lien-information" && (
        <EditLienInformationModal
          lienId={lien.lienId}
          lien={lien.lienInformation}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            onRefresh();
          }}
        />
      )}
      {editModal === "case-information" && (
        <EditCaseInformationModal
          lienId={lien.lienId}
          fundingCompany={lien.fundingCompany}
          medicalProvider={lien.medicalProvider}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            onRefresh();
          }}
        />
      )}
      {editModal === "medical-pricing" && (
        <EditMedicalPricingModal
          lienId={lien.lienId}
          rows={lien.medicalPricing.rows}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            onRefresh();
          }}
        />
      )}
    </div>
  );
}

const ACTIVITY_PAGE_SIZE = 5;

function ActivityTab({ lienId }: { lienId: string }) {
  const { data, isLoading, isError } = useLienActivity(lienId);
  const items = data?.items ?? [];

  // The API returns the full activity history in one response — pagination
  // here is purely client-side, slicing further into the already-fetched
  // array on each "Load More" click.
  const [visibleCount, setVisibleCount] = useState(ACTIVITY_PAGE_SIZE);

  useEffect(() => {
    setVisibleCount(ACTIVITY_PAGE_SIZE);
  }, [lienId]);

  const visibleItems = items.slice(0, visibleCount);
  const hasMore = visibleCount < items.length;

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5">
        <h3 className="text-md font-semibold mb-4">Activity</h3>

        {isLoading ? (
          <div className="space-y-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="flex gap-3 animate-pulse">
                <div className="w-5 h-5 rounded-full bg-gray-100 shrink-0" />
                <div className="flex-1 space-y-2">
                  <div className="h-3.5 w-2/3 bg-gray-100 rounded" />
                  <div className="h-3 w-24 bg-gray-100 rounded" />
                </div>
              </div>
            ))}
          </div>
        ) : isError ? (
          <p className="text-sm text-red-600 py-6 text-center">
            Failed to load activity.
          </p>
        ) : items.length === 0 ? (
          <ContactsEmptyState
            icon={Clipboard}
            title="No Recent Activity"
            description="There are no recent activities to display. New updates will appear here as they occur."
          />
        ) : (
          <>
            <ol className="relative space-y-5">
              {visibleItems.length > 1 && (
                <span
                  aria-hidden="true"
                  className="absolute left-[7px] top-4 h-[calc(100%-32px)] w-px bg-gray-200"
                />
              )}
              {visibleItems.map((item) => (
                <li key={item.id} className="relative flex gap-3">
                  <span className="mt-1 flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-primary">
                    <span className="h-1.5 w-1.5 rounded-full bg-white" />
                  </span>
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-gray-800">
                      {item.description}
                    </p>
                    <p className="text-xs text-gray-400 mt-0.5">
                      {new Date(item.timestampUtc).toLocaleDateString()} ·{" "}
                      {new Date(item.timestampUtc).toLocaleTimeString()}
                    </p>
                  </div>
                </li>
              ))}
            </ol>

            {hasMore ? (
              <div className="pt-5 flex flex-col items-center gap-1.5">
                <button
                  type="button"
                  onClick={() =>
                    setVisibleCount((count) => count + ACTIVITY_PAGE_SIZE)
                  }
                  className="text-sm font-medium text-primary hover:underline"
                >
                  Load More
                </button>
                <span className="text-xs text-gray-400">
                  Showing {visibleItems.length} of {items.length}
                </span>
              </div>
            ) : (
              items.length > ACTIVITY_PAGE_SIZE && (
                <p className="pt-5 text-xs text-gray-400 text-center">
                  You&apos;re all caught up — showing all {items.length} activities.
                </p>
              )
            )}
          </>
        )}
      </div>
    </div>
  );
}

function DocumentsTab({ lien }: { lien: LienDetailsResult }) {
  const { data: docs = [], isLoading } = useLienDocuments(lien.lienId);
  const saveLienDocuments = useSaveLienDocuments(lien.lienId);
  const [showUpload, setShowUpload] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const runDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      await saveLienDocuments((current) =>
        current.filter((d) => d.documentId !== deleteTarget),
      );
      toast.success("Document removed.");
      setDeleteTarget(null);
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Failed to remove document",
      );
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="bg-white border border-gray-200 rounded-lg">
      <div className="px-6 py-5">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-md font-semibold">Documents</h3>
          <Button
            variant="secondary"
            rightIcon="cloudUpload"
            onClick={() => setShowUpload(true)}
          >
            Upload Document
          </Button>
        </div>

        {isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            <SkeletonFileRow />
            <SkeletonFileRow />
          </div>
        ) : docs.length === 0 ? (
          <div className="py-10 text-center">
            <Copy className="h-6 w-6 text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">
              No documents attached to this lien yet
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            {docs.map((doc) => (
              <UploadedFileRow
                key={doc.documentId}
                icon={fileIconFor(doc.displayName)}
                title={doc.displayName}
                subtitle={
                  SALE_DOCUMENT_LABELS[doc.documentType]?.title ??
                  camelCaseToLabel(doc.documentType)
                }
                timestamp={doc.createdAt}
                actions={
                  <Button
                    variant="icon-square-destructive"
                    className="w-8 h-8"
                    icon="trash2"
                    onClick={() => setDeleteTarget(doc.documentId)}
                    aria-label="Delete document"
                  />
                }
              />
            ))}
          </div>
        )}
      </div>

      {showUpload && (
        <UploadDocumentModal
          lienId={lien.lienId}
          onClose={() => setShowUpload(false)}
        />
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        onClose={() => setDeleteTarget(null)}
        onConfirm={runDelete}
        loading={deleting}
        title="Remove Document"
        description="This document will no longer be attached to this lien."
        confirmLabel="Remove"
        confirmVariant="danger"
      />
    </div>
  );
}

// Files upload (and persist to the lien) as soon as each one is dropped in
// UploadDocuments — same auto-commit behavior as the edit wizard's step-4 —
// so there's no separate submit step, just a "Done" button to close up.
function UploadDocumentModal({
  lienId,
  onClose,
}: {
  lienId: string;
  onClose: () => void;
}) {
  return (
    <Modal
      open
      onClose={onClose}
      title="Upload Documents"
      size="lg"
      footer={
        <Button variant="primary" onClick={onClose}>
          Done
        </Button>
      }
    >
      <UploadDocuments lienId={lienId} hideHeading hideExistingDocuments />
    </Modal>
  );
}
