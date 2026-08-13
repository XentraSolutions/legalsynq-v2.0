"use client";

import { useEffect, useState } from "react";
import { LienDetailsResult } from "@/types/lien-selling";
import { LienInformationPanel } from "./lien-detail/lien-information-panel";
import { FundingCompanyAndCaseInformationPanel } from "./lien-detail/funding-company-information-panel";
import { MedicalCodesInformationPanel } from "./lien-detail/medical-codes-information-panel";
import { EditLienInformationModal } from "./lien-detail/edit-lien-information-modal";
import { EditCaseInformationModal } from "./lien-detail/edit-case-information-modal";
import { EditMedicalPricingModal } from "./lien-detail/edit-medical-pricing-modal";
import { ConfirmDialog, FormModal } from "@/components/selling/modal";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import { useSession } from "@/hooks/use-session";
import { useSessionContext } from "@/providers/session-provider";
import Field from "@/components/lien/field";
import UploadDocumentComponent from "@/components/lien/upload-document";
import {
  parseDocumentReference,
  sellerStatusLabel,
  SALE_DOCUMENT_LABELS,
} from "@/lib/selling/selling-detail.mapper";
import { useToast } from "@/lib/toast-context";
import { Tabs } from "@/components/ui/tabs";
import { LienRowActionsMenu } from "./lien-row-actions-menu";
import { Button } from "@/components/ui/button";

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
                <Button
                  className="bg-[#EE7132] hover:bg-[#EE7132]/90 text-white"
                  rightIcon={<i className="ri-arrow-down-s-line text-base" />}
                >
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
  const { session } = useSession();
  const { show: showToast } = useToast();
  const [enriched, setEnriched] = useState<
    Record<string, { title: string; createdAt: string; fileSize: string }>
  >({});
  const [showUpload, setShowUpload] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    Promise.all(
      lien.documents.map(async (doc) => {
        const data = parseDocumentReference(doc);
        if (!data.documentId) return null;
        try {
          const detail = await documentsService.getById(data.documentId);
          return [
            data.documentId,
            {
              title: detail.title,
              createdAt: detail.createdAt,
              fileSize: detail.fileSize,
            },
          ] as const;
        } catch {
          return null;
        }
      }),
    ).then((results) => {
      if (cancelled) return;
      const map: Record<
        string,
        { title: string; createdAt: string; fileSize: string }
      > = {};
      for (const r of results) {
        if (r) map[r[0]] = r[1];
      }
      setEnriched(map);
    });
    return () => {
      cancelled = true;
    };
  }, [lien.documents]);

  const runDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const remaining = lien.documents
        .filter((doc) => doc.id !== deleteTarget)
        .map((doc) => {
          const data = parseDocumentReference(doc);
          return {
            documentId: data.documentId,
            documentType: data.documentType,
            displayName: data.displayName ?? undefined,
          };
        });
      await liensService.saveDocuments(lien.lienId, { documents: remaining });
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
            className="px-3 py-1.5"
            rightIcon={<i className="ri-upload-cloud-2-line text-sm" />}
            onClick={() => setShowUpload(true)}
          >
            Upload Document
          </Button>
        </div>

        {lien.documents.length === 0 ? (
          <div className="py-10 text-center">
            <i className="ri-file-copy-2-line text-3xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">
              No documents attached to this lien yet
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-8 gap-y-4">
            {lien.documents.map((doc) => {
              const data = parseDocumentReference(doc);
              const meta = enriched[data.documentId];
              return (
                <div key={doc.id} className="flex items-center gap-3 py-1">
                  <div className="w-10 h-10 rounded bg-gray-50 border border-gray-100 flex items-center justify-center shrink-0">
                    <i className="ri-file-text-line text-lg text-gray-500" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-medium text-gray-800 truncate">
                      {meta?.title || data.displayName || doc.description}
                    </p>
                    <p className="text-xs text-gray-400">
                      {meta?.createdAt ? `${meta.createdAt} · ` : ""}
                      {SALE_DOCUMENT_LABELS[data.documentType]?.title ??
                        data.documentType}
                    </p>
                  </div>
                  <Button
                    variant="icon-square"
                    className="w-8 h-8 border-red-100 text-red-500 hover:bg-red-50 shrink-0"
                    onClick={() => setDeleteTarget(doc.id)}
                  >
                    <i className="ri-delete-bin-6-line text-sm" />
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {showUpload && (
        <UploadDocumentModal
          lienId={lien.lienId}
          tenantId={session?.tenantId ?? ""}
          existingDocuments={lien.documents}
          onClose={() => setShowUpload(false)}
          onUploaded={() => {
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

function UploadDocumentModal({
  lienId,
  tenantId,
  existingDocuments,
  onClose,
  onUploaded,
}: {
  lienId: string;
  tenantId: string;
  existingDocuments: LienDetailsResult["documents"];
  onClose: () => void;
  onUploaded: () => void;
}) {
  const { show: showToast } = useToast();
  const { lookup } = useSessionContext();
  const [file, setFile] = useState<File | null>(null);
  const documentTypes = lookup?.DocumentCategory ?? [];
  const documentTypeOptions = documentTypes.map((type) => ({
    value: type.id,
    label: type.name,
  }));
  const [documentTypeId, setDocumentTypeId] = useState("");
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    if (!documentTypeId && documentTypes.length > 0) {
      setDocumentTypeId(documentTypes[0].id);
      // setDocumentTypeId('00000000-0000-0000-0000-000000000001');
    }
  }, [documentTypes, documentTypeId]);

  const handleSubmit = async () => {
    if (!file || !documentTypeId) return;
    setUploading(true);
    try {
      const uploaded = await documentsService.upload({
        file,
        tenantId,
        productId: "SYNQ_LIENS",
        referenceType: "Lien",
        referenceId: lienId,
        documentTypeId,
        title: file.name,
      });
      const documentType =
        documentTypes.find((t) => t.id === documentTypeId)?.name ??
        documentTypeId;
      const documents = [
        ...existingDocuments.map((doc) => {
          const data = parseDocumentReference(doc);
          return {
            documentId: data.documentId,
            documentType: data.documentType,
            displayName: data.displayName ?? undefined,
          };
        }),
        { documentId: uploaded.id, documentType, displayName: file.name },
      ];
      await liensService.saveDocuments(lienId, { documents });
      showToast("Document uploaded.", "success");
      onUploaded();
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to upload document",
        "error",
      );
    } finally {
      setUploading(false);
    }
  };

  return (
    <FormModal
      open
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Upload Document"
      submitLabel={uploading ? "Uploading..." : "Upload"}
      submitDisabled={!file || !documentTypeId || uploading}
      size="sm"
    >
      <div className="space-y-4">
        <Field
          label="Document Type"
          type="select"
          value={documentTypeId}
          options={documentTypeOptions}
          onChange={(value: string) => setDocumentTypeId(value)}
        />
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            File
          </label>
          <UploadDocumentComponent
            isMultiple={false}
            onUploaded={(files) => setFile(files[0] ?? null)}
          />
        </div>
      </div>
    </FormModal>
  );
}
