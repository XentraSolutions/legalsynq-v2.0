"use client";

import { useState } from "react";
import { Copy } from "lucide-react";
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
import { sellerStatusLabel, SALE_DOCUMENT_LABELS } from "@/lib/selling/selling-detail.mapper";
import { useLienDocuments, useSaveLienDocuments } from "@/lib/selling/use-lien-documents";
import { SkeletonFileRow } from "@/components/lien/skeleton-loader";
import { useToast } from "@/lib/toast-context";
import { Tabs } from "@/components/ui/tabs";
import { LienRowActionsMenu } from "./lien-row-actions-menu";
import { Button } from "@/components/selling/button";

interface LienDetailPanelProps {
  lien: LienDetailsResult;
  onRefresh: () => void;
}

const SELLER_STATUS_STYLES: Record<string, string> = {
  Draft: "bg-gray-50 text-gray-600 border-gray-200",
  Pending: "bg-amber-50 text-amber-700 border-amber-200",
  Internal: "bg-blue-50 text-blue-700 border-blue-200",
  PreparedForSale: "bg-blue-50 text-blue-700 border-blue-200",
  SubmittedForSale: "bg-amber-50 text-amber-700 border-amber-200",
  Accepted: "bg-green-50 text-green-700 border-green-200",
  Declined: "bg-red-50 text-red-600 border-red-200",
  Sold: "bg-green-50 text-green-700 border-green-200",
  Withdrawn: "bg-red-50 text-red-600 border-red-200",
  Archived: "bg-gray-50 text-gray-500 border-gray-200",
};

function SellerStatusBadge({ status }: { status: string }) {
  const style =
    SELLER_STATUS_STYLES[status] ?? "bg-gray-50 text-gray-600 border-gray-200";
  return (
    <span
      className={`inline-flex items-center rounded-full border font-medium px-2.5 py-1 text-sm ${style}`}
    >
      {sellerStatusLabel(status)}
    </span>
  );
}

const TABS = [
  { key: "details", label: "Details" },
  { key: "documents", label: "Documents" },
] as const;
type TabKey = (typeof TABS)[number]["key"];

type EditModalKey = "lien-information" | "case-information" | "medical-pricing";

export function PortfolioDetailPanel({
  lien,
  onRefresh,
}: LienDetailPanelProps) {
  const [activeTab, setActiveTab] = useState<TabKey>("details");
  const [editModal, setEditModal] = useState<EditModalKey | null>(null);

  const title = lien.fundingCompany?.name || lien.lienInformation.lienNumber;
  const sellerStatus = lien.lienInformation.sellerStatus;
  const canEdit = ["Draft", "Pending", "Internal"].includes(sellerStatus);

  return (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg">
        <div className="px-6 py-4 flex items-start justify-between gap-4">
          <div className="min-w-0">
            <h1 className="text-xl font-bold text-gray-900 truncate">
              {title}
            </h1>
            <p className="text-xs text-gray-400 mt-1 font-medium">
              {lien.lienInformation.lienNumber}
            </p>
          </div>
          <div className="flex items-center gap-3 shrink-0">
            <SellerStatusBadge status={sellerStatus} />
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
        <div className="border-t border-gray-100 px-6 py-3">
          <div className="basis-2/4">
            <Tabs
              bordered={false}
              defaultTab={activeTab}
              onChange={(key) => setActiveTab(key as TabKey)}
              tabs={TABS.map((tab) => ({
                key: tab.key,
                label: tab.label,
                badge:
                  tab.key === "documents" ? lien.documents.length : undefined,
              }))}
            />
          </div>
        </div>
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
      {activeTab === "documents" && (
        <DocumentsTab lien={lien} onRefresh={onRefresh} />
      )}

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
          caseInformation={lien.caseInformation}
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

function DocumentsTab({
  lien,
  onRefresh,
}: {
  lien: LienDetailsResult;
  onRefresh: () => void;
}) {
  const { show: showToast } = useToast();
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
      showToast("Document removed.", "success");
      setDeleteTarget(null);
      onRefresh();
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to remove document",
        "error",
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
                  doc.documentType
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
          onClose={() => {
            setShowUpload(false);
            onRefresh();
          }}
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
