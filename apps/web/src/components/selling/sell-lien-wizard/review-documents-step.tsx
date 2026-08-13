"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import { useSession } from "@/hooks/use-session";
import { useSessionContext } from "@/providers/session-provider";
import { useToast } from "@/lib/toast-context";
import { ConfirmDialog, Modal } from "@/components/selling/modal";
import { Button } from "@/components/ui/button";
import { LienInformationPanel } from "@/components/selling/lien-detail/lien-information-panel";
import { FundingCompanyAndCaseInformationPanel } from "@/components/selling/lien-detail/funding-company-information-panel";
import { MedicalCodesInformationPanel } from "@/components/selling/lien-detail/medical-codes-information-panel";
import { EditLienInformationModal } from "@/components/selling/lien-detail/edit-lien-information-modal";
import { EditCaseInformationModal } from "@/components/selling/lien-detail/edit-case-information-modal";
import { EditMedicalPricingModal } from "@/components/selling/lien-detail/edit-medical-pricing-modal";
import { sellingLookupsApi } from "@/lib/selling/lookup.api";
import type { LienDetailsResult } from "@/types/lien-selling";
import {
  REQUIRED_SALE_DOCUMENT_TYPES,
  OPTIONAL_SALE_DOCUMENT_TYPES,
  SALE_DOCUMENT_LABELS,
  SALE_DOCUMENT_TYPE_TO_CATEGORY_CODE,
  SALE_DOCUMENT_TYPE_TO_SELLING_TYPE,
  parseDocumentReference,
} from "@/lib/selling/selling-detail.mapper";
import { TOTAL_STEPS, goToStep } from "./shared";

// Selling's brand accent, matching the convention used on other selling pages.
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

interface DocSlotState {
  uploading: boolean;
  documentId: string | null;
  displayName: string | null;
}

function emptyDocSlots(): Record<string, DocSlotState> {
  const slots: Record<string, DocSlotState> = {};
  for (const type of [
    ...REQUIRED_SALE_DOCUMENT_TYPES,
    ...OPTIONAL_SALE_DOCUMENT_TYPES,
  ]) {
    slots[type] = { uploading: false, documentId: null, displayName: null };
  }
  return slots;
}

// Reverse of SALE_DOCUMENT_TYPE_TO_SELLING_TYPE — used to repopulate docSlots
// from documents already saved on the lien (lien.documents). "Other" is
// ambiguous (both the PoliceReport slot and a true "Other" upload save as
// "Other"), but PoliceReport is the only wizard slot that maps to it, so it's
// the correct slot to restore into.
const SELLING_TYPE_TO_SALE_DOCUMENT_TYPE: Record<string, string> = {
  LienAgreement: "LienAgreement",
  MedicalBill: "MedicalBill",
  MedicalRecord: "MedicalRecord",
  Other: "PoliceReport",
};

function docSlotsFromLien(
  documents: LienDetailsResult["documents"],
): Record<string, DocSlotState> {
  const slots = emptyDocSlots();
  for (const doc of documents) {
    const data = parseDocumentReference(doc);
    if (!data.documentId) continue;
    const slotType = SELLING_TYPE_TO_SALE_DOCUMENT_TYPE[data.documentType];
    if (!slotType || !slots[slotType]) continue;
    slots[slotType] = {
      uploading: false,
      documentId: data.documentId,
      displayName: data.displayName,
    };
  }
  return slots;
}

export interface ReviewDocumentsStepProps {
  lienId: string;
}

// Step 2/2 — review lien/buyer info, upload the sale documents, then
// authorize and send. Buyer selection was already persisted by step 1, so
// this step only reads it off the lien to render the summary panel.
export default function ReviewDocumentsStep({ lienId }: ReviewDocumentsStepProps) {
  const router = useRouter();
  const { session } = useSession();
  const { lookup } = useSessionContext();
  const { show: showToast } = useToast();
  const documentCategories = lookup?.DocumentCategory ?? [];

  const [loading, setLoading] = useState(true);
  const [lien, setLien] = useState<LienDetailsResult | null>(null);

  const [messageToBuyer] = useState("");
  const [docSlots, setDocSlots] =
    useState<Record<string, DocSlotState>>(emptyDocSlots());
  const [sellingDocumentTypes, setSellingDocumentTypes] = useState<string[]>(
    [],
  );

  const [showConfirm, setShowConfirm] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [editModal, setEditModal] = useState<
    "lien-information" | "case-information" | "medical-pricing" | null
  >(null);

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
        setDocSlots(docSlotsFromLien(detail.documents));
      } catch (err) {
        showToast(
          err instanceof Error ? err.message : "Failed to load lien",
          "error",
        );
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
      showToast(
        err instanceof Error ? err.message : "Failed to load lien",
        "error",
      );
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

  const handleFileSelect = async (documentType: string, file: File) => {
    setDocSlots((prev) => ({
      ...prev,
      [documentType]: { ...prev[documentType], uploading: true },
    }));
    try {
      const categoryCode = SALE_DOCUMENT_TYPE_TO_CATEGORY_CODE[documentType];
      const documentTypeId = documentCategories.find(
        (c) => c.code === categoryCode,
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
      const nextSlots = {
        ...docSlots,
        [documentType]: {
          uploading: false,
          documentId: uploaded.id,
          displayName: file.name,
        },
      };
      setDocSlots(nextSlots);
      // Persist the reference immediately so it survives navigating away —
      // this is the only place documents are saved; "Save for Later" and
      // "Authorize & Send" rely on it already being in sync.
      await liensService.saveDocuments(lienId, {
        documents: uploadedDocumentRefs(nextSlots),
      });
    } catch (err) {
      setDocSlots((prev) => ({
        ...prev,
        [documentType]: { ...prev[documentType], uploading: false },
      }));
      showToast(
        err instanceof Error ? err.message : "Document upload failed",
        "error",
      );
    }
  };

  const uploadedDocumentRefs = (slots: Record<string, DocSlotState> = docSlots) =>
    Object.entries(slots)
      .filter(([, slot]) => slot.documentId)
      .map(([documentType, slot]) => {
        const sellingType =
          SALE_DOCUMENT_TYPE_TO_SELLING_TYPE[documentType] ?? documentType;
        return {
          documentId: slot.documentId!,
          documentType: sellingDocumentTypes.includes(sellingType)
            ? sellingType
            : "Other",
          displayName: slot.displayName ?? undefined,
        };
      });

  const saveForLater = async () => {
    setSubmitting(true);
    try {
      showToast("Progress saved.", "success");
      router.push(`/selling/portfolio/lien/${lienId}`);
    } catch (err) {
      showToast(
        err instanceof Error ? err.message : "Failed to save progress",
        "error",
      );
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
      showToast(
        err instanceof Error ? err.message : "Failed to submit lien for sale",
        "error",
      );
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
      </div>
    );
  }

  if (!lien) return null;

  return (
    <div className="max-w-4xl mx-auto space-y-6 pb-10">
      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={() => goToStep(router, lienId, 1)}
          className="text-gray-400 hover:text-gray-600"
        >
          <i className="ri-arrow-left-line text-xl" />
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
          Review the lien information and complete all required documents
          before submitting it to the selected funding company.
        </p>

        {!pricingReady && (
          <div className="flex items-center gap-2 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg">
            <i className="ri-alert-line text-amber-600 shrink-0" />
            <p className="text-xs text-amber-700">
              This lien has no medical pricing or ask amount set yet. Edit
              &ldquo;Medical Code &amp; Marketplace Pricing&rdquo; below
              before this lien can be sold.
            </p>
          </div>
        )}

        {!requiredDocsReady && (
          <div className="flex items-center gap-2 px-4 py-3 bg-amber-50 border border-amber-200 rounded-lg">
            <i className="ri-alert-line text-amber-600 shrink-0" />
            <p className="text-xs text-amber-700">
              Upload all required documents below before this lien can be
              authorized and sent.
            </p>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
          <div className="space-y-4">
            <LienInformationPanel
              lien={lien.lienInformation}
              onEdit={() => setEditModal("lien-information")}
            />
            <FundingCompanyAndCaseInformationPanel
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
              caseInformation={lien.caseInformation}
              medicalProvider={lien.medicalProvider}
              onEdit={() => setEditModal("case-information")}
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
                />
              ))}
            </div>
            <div className="bg-white border border-gray-200 rounded-lg p-5 space-y-4">
              <h3 className="text-md font-semibold">
                Optional Supporting Documents
              </h3>
              {OPTIONAL_SALE_DOCUMENT_TYPES.map((type) => (
                <DocumentSlot
                  key={type}
                  type={type}
                  slot={docSlots[type]}
                  onSelect={(file) => handleFileSelect(type, file)}
                />
              ))}
            </div>
          </div>
        </div>

        <div className="flex justify-between items-center pt-4">
          <Button variant="secondary" onClick={() => goToStep(router, lienId, 1)}>
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
              className={`px-6 ${PRIMARY_BUTTON_CLASSNAME}`}
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

      <Modal
        open={showSuccess}
        onClose={() => router.push(`/selling/portfolio/lien/${lienId}`)}
        title="Lien Submitted Successfully"
        size="sm"
        footer={
          <Button
            className={PRIMARY_BUTTON_CLASSNAME}
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
      {editModal === "case-information" && (
        <EditCaseInformationModal
          lienId={lienId}
          fundingCompany={lien.fundingCompany}
          medicalProvider={lien.medicalProvider}
          caseInformation={lien.caseInformation}
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

function DocumentSlot({
  type,
  required,
  slot,
  onSelect,
}: {
  type: string;
  required?: boolean;
  slot: DocSlotState;
  onSelect: (file: File) => void;
}) {
  const meta = SALE_DOCUMENT_LABELS[type];
  const inputId = `doc-slot-${type}`;
  return (
    <div className="flex items-center justify-between gap-3 border border-gray-100 rounded-lg px-3 py-2.5">
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-800">
          {meta?.title ?? type}
          {required && <span className="text-red-500 ml-0.5">*</span>}
        </p>
        <p className="text-xs text-gray-400 truncate">
          {slot.documentId
            ? slot.displayName
            : (meta?.description ?? "(Optional)")}
        </p>
      </div>
      <label
        htmlFor={inputId}
        className={`shrink-0 text-xs font-medium px-3 py-1.5 rounded-lg border cursor-pointer ${
          slot.documentId
            ? "border-green-200 text-green-700 bg-green-50"
            : "border-gray-200 text-gray-600 hover:bg-gray-50"
        }`}
      >
        {slot.uploading
          ? "Uploading..."
          : slot.documentId
            ? "Replace File"
            : "Choose File"}
      </label>
      <input
        id={inputId}
        type="file"
        className="hidden"
        disabled={slot.uploading}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onSelect(file);
        }}
      />
    </div>
  );
}
