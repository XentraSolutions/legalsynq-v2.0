"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, TriangleAlert } from "lucide-react";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import { useSession } from "@/hooks/use-session";
import { useSessionContext } from "@/providers/session-provider";
import { ApiError } from "@/lib/api-client";
import { toast } from "sonner";
import { ConfirmDialog, Modal } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";
import UploadDocumentComponent, {
  FileDropzoneRef,
} from "@/components/selling/upload-document";
import Field from "@/components/lien/field";
import {
  fileExtLabel,
  fileIconFor,
  UploadedFileRow,
} from "@/components/selling/uploaded-file-row";
import { LienInformationPanel } from "@/components/selling/lien-detail/lien-information-panel";
import { ProviderFundingDetailsPanel } from "@/components/selling/lien-detail/provider-funding-details-panel";
import { MedicalCodesInformationPanel } from "@/components/selling/lien-detail/medical-codes-information-panel";
import { EditLienInformationModal } from "@/components/selling/lien-detail/edit-lien-information-modal";
import { EditProviderFundingModal } from "@/components/selling/lien-detail/edit-provider-funding-modal";
import { EditMedicalPricingModal } from "@/components/selling/lien-detail/edit-medical-pricing-modal";
import { sellingLookupsApi } from "@/lib/selling/lookup.api";
import { SkeletonFileRow } from "@/components/lien/skeleton-loader";
import type { LienDetailsResult } from "@/types/lien-selling";
import {
  REQUIRED_SALE_DOCUMENT_TYPES,
  SALE_DOCUMENT_LABELS,
  camelCaseToLabel,
  optionalSaleDocumentTypes,
  parseDocumentReference,
  resolveDocumentCategory,
} from "@/lib/selling/selling-detail.mapper";
import { TOTAL_STEPS, goToStep } from "./shared";

// Mirrors the loaded page below: header/progress, title/description, the
// left column's info panels, and the right column's document sections.
function ReviewDocumentsSkeleton() {
  return (
    <div className="w-full space-y-6 pb-10 animate-pulse">
      <div className="flex items-center gap-4">
        <div className="h-5 w-5 rounded bg-gray-100 shrink-0" />
        <div className="flex-1 flex gap-2">
          {Array.from({ length: TOTAL_STEPS }, (_, index) => (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full ${index < 2 ? "bg-[#EE7132]/40" : "bg-gray-200"}`}
            />
          ))}
        </div>
      </div>

      <div className="space-y-4">
        <div className="h-3 bg-gray-100 rounded w-16" />
        <div className="h-7 bg-gray-100 rounded w-80" />
        <div className="h-3 bg-gray-100 rounded w-full max-w-xl" />

        <div className="grid grid-cols-1 lg:grid-cols-[2fr_3fr] gap-4 items-start">
          <div className="space-y-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div
                key={i}
                className="bg-white border border-gray-200 rounded-lg p-5 space-y-3"
              >
                <div className="h-4 bg-gray-100 rounded w-1/3" />
                <div className="h-3 bg-gray-100 rounded w-full" />
                <div className="h-3 bg-gray-100 rounded w-2/3" />
              </div>
            ))}
          </div>

          <div className="space-y-4">
            <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
              <div className="h-4 bg-gray-100 rounded w-40" />
              {Array.from({ length: 3 }).map((_, i) => (
                <SkeletonFileRow key={i} />
              ))}
            </div>
            <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
              <div className="h-4 bg-gray-100 rounded w-52" />
              {Array.from({ length: 2 }).map((_, i) => (
                <SkeletonFileRow key={i} />
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

interface DocSlotState {
  uploading: boolean;
  documentId: string | null;
  displayName: string | null;
  createdAt: string | null;
}

function emptyDocSlots(sellingDocumentTypes: string[]): Record<string, DocSlotState> {
  const slots: Record<string, DocSlotState> = {};
  for (const type of [
    ...REQUIRED_SALE_DOCUMENT_TYPES,
    ...optionalSaleDocumentTypes(sellingDocumentTypes),
  ]) {
    slots[type] = {
      uploading: false,
      documentId: null,
      displayName: null,
      createdAt: null,
    };
  }
  return slots;
}

// Enriches each slot with the upload timestamp — lien.documents only carries
// documentId/type/displayName, so the createdAt shown in the card comes from
// a follow-up fetch per attached document (same pattern as the edit wizard's
// UploadDocuments component).
async function docSlotsFromLien(
  documents: LienDetailsResult["documents"],
  sellingDocumentTypes: string[],
): Promise<Record<string, DocSlotState>> {
  const slots = emptyDocSlots(sellingDocumentTypes);
  const refs: {
    slotType: string;
    documentId: string;
    displayName: string | null;
  }[] = [];
  for (const doc of documents) {
    const data = parseDocumentReference(doc);
    if (!data.documentId) continue;
    // Older uploads persisted the human-readable label (e.g. "Police Report")
    // instead of the raw enum key ("PoliceReport") as documentType, so a
    // direct slot lookup misses them — fall back to matching by label.
    const slotType = slots[data.documentType]
      ? data.documentType
      : Object.keys(slots).find(
          (key) => camelCaseToLabel(key) === data.documentType,
        );
    if (!slotType) continue;
    refs.push({
      slotType,
      documentId: data.documentId,
      displayName: data.displayName,
    });
  }
  await Promise.all(
    refs.map(async ({ slotType, documentId, displayName }) => {
      let createdAt: string | null = null;
      try {
        createdAt = (await documentsService.getById(documentId)).createdAt;
      } catch {
        // Non-fatal — the card still renders without a timestamp.
      }
      slots[slotType] = {
        uploading: false,
        documentId,
        displayName,
        createdAt,
      };
    }),
  );
  return slots;
}

export interface ReviewDocumentsStepProps {
  lienId: string;
}

// Step 2/2 — review lien/buyer info, upload the sale documents, then
// authorize and send. Buyer selection was already persisted by step 1, so
// this step only reads it off the lien to render the summary panel.
export default function ReviewDocumentsStep({
  lienId,
}: ReviewDocumentsStepProps) {
  const router = useRouter();
  const { session } = useSession();
  const { lookup } = useSessionContext();
  const documentCategories = lookup?.DocumentCategory ?? [];

  const [loading, setLoading] = useState(true);
  const [lien, setLien] = useState<LienDetailsResult | null>(null);

  const [messageToBuyer] = useState("");
  const [docSlots, setDocSlots] = useState<Record<string, DocSlotState>>(
    emptyDocSlots([]),
  );
  const [sellingDocumentTypes, setSellingDocumentTypes] = useState<string[]>(
    [],
  );

  const [showConfirm, setShowConfirm] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [editModal, setEditModal] = useState<
    "lien-information" | "provider-funding" | "medical-pricing" | null
  >(null);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [showAddSupportingDoc, setShowAddSupportingDoc] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [detail, documentTypesRes] = await Promise.all([
          liensService.getLienById(lienId),
          sellingLookupsApi.documentTypes(),
        ]);
        if (cancelled) return;
        setLien(detail);
        setSellingDocumentTypes(documentTypesRes.data.items);
        const slots = await docSlotsFromLien(
          detail.documents,
          documentTypesRes.data.items,
        );
        if (cancelled) return;
        setDocSlots(slots);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load lien");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lienId]);

  const refreshLien = async () => {
    try {
      const detail = await liensService.getLienById(lienId);
      setLien(detail);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to load lien");
    }
  };

  const companyId = lien?.fundingCompany?.id ?? "";
  const companyName = lien?.fundingCompany?.name ?? "";
  const contactId = lien?.fundingCompany?.contact?.id ?? "";
  const contactName = lien?.fundingCompany?.contact?.name ?? "";

  const askAmount = lien?.medicalPricing.askAmount ?? null;
  const pricingReady =
    (lien?.medicalPricing.rows.length ?? 0) > 0 && (askAmount ?? 0) > 0;
  const requiredDocsReady = REQUIRED_SALE_DOCUMENT_TYPES.every(
    (type) => docSlots[type]?.documentId,
  );
  const canAuthorize = !!companyId && pricingReady && requiredDocsReady;

  const optionalTypes = optionalSaleDocumentTypes(sellingDocumentTypes);
  const uploadedOptionalTypes = optionalTypes.filter(
    (type) => docSlots[type]?.documentId,
  );
  const availableOptionalTypes = optionalTypes.filter(
    (type) => !docSlots[type]?.documentId,
  );

  const handleFileSelect = async (
    documentType: string,
    file: File,
  ): Promise<boolean> => {
    setDocSlots((prev) => ({
      ...prev,
      [documentType]: {
        ...prev[documentType],
        uploading: true,
        displayName: file.name,
      },
    }));
    let uploadedToDocumentService = false;
    try {
      const documentTypeId = resolveDocumentCategory(
        documentType,
        documentCategories,
      )?.id;
      if (!documentTypeId) {
        throw new Error(
          "Document type list is still loading. Please try again.",
        );
      }
      const uploaded = await documentsService.upload({
        file,
        tenantId: session?.tenantId ?? "",
        productId: "SYNQ_LIENS",
        referenceType: "Lien",
        referenceId: lienId,
        documentTypeId,
        title: file.name,
      });
      uploadedToDocumentService = true;
      const nextSlots = {
        ...docSlots,
        [documentType]: {
          uploading: false,
          documentId: uploaded.id,
          displayName: file.name,
          createdAt: uploaded.createdAt,
        },
      };
      // Persist the reference immediately so it survives navigating away —
      // this is the only place documents are saved; "Save for Later" and
      // "Authorize & Send" rely on it already being in sync.
      await liensService.saveDocuments(lienId, {
        documents: uploadedDocumentRefs(nextSlots),
      });
      setDocSlots(nextSlots);
      return true;
    } catch (err) {
      setDocSlots((prev) => ({
        ...prev,
        [documentType]: {
          ...prev[documentType],
          uploading: false,
          displayName: prev[documentType].documentId
            ? prev[documentType].displayName
            : null,
        },
      }));
      const cause =
        err instanceof Error && err.message.trim()
          ? err.message
          : "The server did not provide an error reason.";
      const reference =
        err instanceof ApiError && err.correlationId !== "unknown"
          ? ` Reference: ${err.correlationId}.`
          : "";
      toast.error(
        uploadedToDocumentService
          ? `“${file.name}” uploaded but could not be attached to this lien. ${cause}${reference}`
          : `Unable to upload “${file.name}”. ${cause}${reference}`,
      );
      return false;
    }
  };

  const uploadedDocumentRefs = (
    slots: Record<string, DocSlotState> = docSlots,
  ) =>
    Object.entries(slots)
      .filter(([, slot]) => slot.documentId)
      .map(([documentType, slot]) => ({
        documentId: slot.documentId!,
        documentType,
        displayName: slot.displayName ?? undefined,
      }));

  const runDelete = async () => {
    if (!deleteTarget) return;
    setDeleting(true);
    try {
      const nextSlots = {
        ...docSlots,
        [deleteTarget]: {
          uploading: false,
          documentId: null,
          displayName: null,
          createdAt: null,
        },
      };
      await liensService.saveDocuments(lienId, {
        documents: uploadedDocumentRefs(nextSlots),
      });
      setDocSlots(nextSlots);
      setDeleteTarget(null);
      toast.success("Document removed.");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to remove document");
    } finally {
      setDeleting(false);
    }
  };

  const saveForLater = async () => {
    setSubmitting(true);
    try {
      toast.success("Progress saved.");
      router.push(`/selling/portfolio/lien/${lienId}`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to save progress");
    } finally {
      setSubmitting(false);
    }
  };

  const confirmSell = async () => {
    if (!companyId || askAmount === null) return;
    setSubmitting(true);
    try {
      await liensService.prepareSale(lienId, {
        buyerFundingCompanyId: companyId,
        buyerContactId: contactId || undefined,
        askAmount,
        listingVisibility: "Private",
        messageToBuyer: messageToBuyer || undefined,
      });
      await liensService.confirmSale(lienId, {
        confirmationAccepted: true,
        sendBuyerNotification: true,
      });
      setShowConfirm(false);
      setShowSuccess(true);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to submit lien for sale");
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <ReviewDocumentsSkeleton />;
  }

  if (!lien) return null;

  return (
    <div className="w-full space-y-6 pb-10">
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={() => goToStep(router, lienId, 1)}
          className="text-gray-400 hover:text-gray-600"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div className="flex-1 flex gap-2">
          {Array.from({ length: TOTAL_STEPS }, (_, index) => (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full ${index < 2 ? "bg-[#EE7132]" : "bg-gray-200"}`}
            />
          ))}
        </div>
      </div>

      <div className="space-y-4">
        <p className="text-xs text-gray-400">Step 2/{TOTAL_STEPS}</p>
        <h1 className="text-2xl font-bold text-gray-900">
          Prepare Your Lien for Sale
        </h1>
        <p className="text-sm text-gray-500">
          Review the lien information and complete all required documents before
          submitting it to the selected funding company.
        </p>

        {!pricingReady && (
          <div className="flex items-center gap-2 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg">
            <TriangleAlert className="h-4 w-4 text-amber-600 shrink-0" />
            <p className="text-xs text-amber-700">
              This lien has no medical pricing or ask amount set yet. Edit
              &ldquo;Medical Code &amp; Marketplace Pricing&rdquo; below before
              this lien can be sold.
            </p>
          </div>
        )}

        {!requiredDocsReady && (
          <div className="flex items-center gap-2 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg">
            <TriangleAlert className="h-4 w-4 text-amber-600 shrink-0" />
            <p className="text-xs text-amber-700">
              Upload all required documents below before this lien can be
              authorized and sent.
            </p>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-[2fr_3fr] gap-4 items-start">
          <div className="space-y-4">
            <LienInformationPanel
              lien={lien.lienInformation}
              caseInformation={lien.caseInformation}
              onEdit={() => setEditModal("lien-information")}
            />
            <ProviderFundingDetailsPanel
              fundingCompany={
                companyId
                  ? {
                      id: companyId,
                      name: companyName,
                      contactPerson: contactName || null,
                      emailAddress: null,
                      contact: contactId
                        ? { id: contactId, name: contactName }
                        : null,
                    }
                  : null
              }
              facility={lien.facility}
              medicalProvider={lien.medicalProvider}
              onEdit={() => setEditModal("provider-funding")}
            />
            <MedicalCodesInformationPanel
              lien={lien.medicalPricing.rows}
              onEdit={() => setEditModal("medical-pricing")}
            />
          </div>

          <div className="space-y-4">
            <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
              <h3 className="text-md font-semibold">Required Documents</h3>
              {REQUIRED_SALE_DOCUMENT_TYPES.map((type) => (
                <DocumentSlot
                  key={type}
                  type={type}
                  required
                  slot={docSlots[type]}
                  onSelect={(file) => handleFileSelect(type, file)}
                  onDelete={() => setDeleteTarget(type)}
                />
              ))}
            </div>
            <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-md font-semibold">
                  Optional Supporting Documents
                </h3>
                <Button
                  type="button"
                  variant="secondary"
                  rightIcon="plus"
                  disabled={availableOptionalTypes.length === 0}
                  onClick={() => setShowAddSupportingDoc(true)}
                >
                  Add
                </Button>
              </div>
              {uploadedOptionalTypes.length === 0 ? (
                <p className="text-sm text-gray-400">
                  No supporting documents added yet.
                </p>
              ) : (
                uploadedOptionalTypes.map((type) => (
                  <DocumentSlot
                    key={type}
                    type={type}
                    slot={docSlots[type]}
                    onSelect={(file) => handleFileSelect(type, file)}
                    onDelete={() => setDeleteTarget(type)}
                  />
                ))
              )}
            </div>
          </div>
        </div>

        <div className="flex justify-between items-center pt-4">
          <Button
            variant="secondary"
            onClick={() => goToStep(router, lienId, 1)}
          >
            Back
          </Button>
          <div className="flex gap-3">
            <Button
              variant="secondary"
              disabled={submitting}
              onClick={saveForLater}
            >
              Save for Later
            </Button>
            <Button
              variant="primary"
              disabled={!canAuthorize || submitting}
              onClick={() => setShowConfirm(true)}
            >
              Authorize &amp; Send
            </Button>
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={showConfirm}
        onClose={() => setShowConfirm(false)}
        onConfirm={confirmSell}
        loading={submitting}
        title="Sell This Lien?"
        description={
          <div className="space-y-3">
            <p>
              You&apos;re about to sell lien{" "}
              <strong>{lien.lienInformation.lienNumber}</strong> to{" "}
              <strong>{companyName}</strong> for purchase consideration. Are you
              sure you want to continue?
            </p>
          </div>
        }
        confirmLabel="Yes, Sell"
      />

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

      {showAddSupportingDoc && (
        <AddSupportingDocumentModal
          types={availableOptionalTypes}
          onClose={() => setShowAddSupportingDoc(false)}
          onUpload={async (type, file) => {
            if (await handleFileSelect(type, file)) {
              setShowAddSupportingDoc(false);
            }
          }}
        />
      )}

      <Modal
        open={showSuccess}
        onClose={() => router.push(`/selling/portfolio/lien/${lienId}`)}
        title="Lien Submitted Successfully"
        size="sm"
        footer={
          <Button
            variant="primary"
            onClick={() => router.push(`/selling/portfolio/lien/${lienId}`)}
          >
            Done
          </Button>
        }
      >
        <p className="text-sm text-gray-600">
          Lien <strong>{lien.lienInformation.lienNumber}</strong> has been
          successfully submitted to <strong>{companyName}</strong> for review.
          The buyer has been notified and can now begin the evaluation process.
        </p>
      </Modal>

      {editModal === "lien-information" && (
        <EditLienInformationModal
          lienId={lienId}
          lien={lien.lienInformation}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            refreshLien();
          }}
        />
      )}
      {editModal === "provider-funding" && (
        <EditProviderFundingModal
          lienId={lienId}
          fundingCompany={lien.fundingCompany}
          medicalProvider={lien.medicalProvider}
          facility={lien.facility}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            refreshLien();
          }}
        />
      )}
      {editModal === "medical-pricing" && (
        <EditMedicalPricingModal
          lienId={lienId}
          rows={lien.medicalPricing.rows}
          onClose={() => setEditModal(null)}
          onSaved={() => {
            setEditModal(null);
            refreshLien();
          }}
        />
      )}
    </div>
  );
}

function AddSupportingDocumentModal({
  types,
  onClose,
  onUpload,
}: {
  types: string[];
  onClose: () => void;
  onUpload: (type: string, file: File) => Promise<void>;
}) {
  const [type, setType] = useState("");
  const [uploading, setUploading] = useState(false);
  const dropzoneRef = useRef<FileDropzoneRef>(null);

  const options = types.map((t) => ({
    key: t,
    value: t,
    label: camelCaseToLabel(t),
  }));

  const handleFiles = async (files: File[]) => {
    const file = files[0];
    if (!file || !type) return;
    setUploading(true);
    try {
      await onUpload(type, file);
    } finally {
      setUploading(false);
      dropzoneRef.current?.reset();
    }
  };

  return (
    <Modal
      open
      onClose={onClose}
      title="Add Supporting Document"
      size="sm"
    >
      <div className="space-y-4">
        <Field
          label="Document Type"
          required
          type="select"
          value={type}
          options={options}
          onChange={(v: string) => setType(v)}
          placeholder="Select document type"
          clearable
        />
        <UploadDocumentComponent
          ref={dropzoneRef}
          isMultiple={false}
          disabled={!type || uploading}
          onUploaded={handleFiles}
        />
      </div>
    </Modal>
  );
}

function DocumentSlot({
  type,
  required,
  slot,
  onSelect,
  onDelete,
}: {
  type: string;
  required?: boolean;
  slot: DocSlotState;
  onSelect: (file: File) => void;
  onDelete: () => void;
}) {
  const meta = SALE_DOCUMENT_LABELS[type];
  const inputRef = useRef<HTMLInputElement>(null);

  return (
    <UploadedFileRow
      icon={slot.displayName ? fileIconFor(slot.displayName) : undefined}
      title={
        <>
          {meta?.title ?? camelCaseToLabel(type)}
          {required && <span className="text-red-500 ml-0.5">*</span>}
        </>
      }
      subtitle={
        slot.uploading
          ? "Uploading..."
          : slot.documentId
            ? `${slot.displayName} · ${fileExtLabel(slot.displayName ?? "")}`
            : (required ? "(Required)" : "(Optional)")
      }
      timestamp={slot.documentId && !slot.uploading ? slot.createdAt : null}
      actions={
        <>
          {slot.documentId ? (
            <>
              <Button
                type="button"
                variant="icon-square"
                icon="cloudBackup"
                loading={slot.uploading}
                onClick={() => inputRef.current?.click()}
                aria-label={slot.uploading ? "Uploading" : "Replace document"}
              />
              <Button
                type="button"
                variant="icon-square-destructive"
                icon="trash2"
                disabled={slot.uploading}
                onClick={onDelete}
                aria-label="Delete document"
              />
            </>
          ) : (
            <Button
              type="button"
              variant={slot.uploading ? "icon-square" : "secondary"}
              icon={slot.uploading ? "cloudUpload" : undefined}
              loading={slot.uploading}
              onClick={() => inputRef.current?.click()}
              aria-label={slot.uploading ? "Uploading" : "Choose file"}
              rightIcon="cloudUpload"
            >
              Choose File
            </Button>
          )}
          <input
            ref={inputRef}
            type="file"
            className="hidden"
            disabled={slot.uploading}
            onChange={(e) => {
              const file = e.target.files?.[0];
              if (file) onSelect(file);
              e.target.value = "";
            }}
          />
        </>
      }
    />
  );
}
