"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { liensService } from "@/lib/selling";
import { documentsService } from "@/lib/documents";
import { useSession } from "@/hooks/use-session";
import { useSessionContext } from "@/providers/session-provider";
import { useToast } from "@/lib/toast-context";
import { ConfirmDialog, Modal } from "@/components/lien/modal";
import { LienInformationPanel } from "@/components/selling/lien-detail/lien-information-panel";
import { FundingCompanyAndCaseInformationPanel } from "@/components/selling/lien-detail/funding-company-information-panel";
import { MedicalCodesInformationPanel } from "@/components/selling/lien-detail/medical-codes-information-panel";
import { sellingLookupsApi, type SellingLookupItem } from "@/lib/selling/lookup.api";
import type { LienDetailsResult } from "@/types/lien-selling";
import {
  REQUIRED_SALE_DOCUMENT_TYPES,
  OPTIONAL_SALE_DOCUMENT_TYPES,
  SALE_DOCUMENT_LABELS,
  SALE_DOCUMENT_TYPE_TO_CATEGORY_CODE,
  SALE_DOCUMENT_TYPE_TO_SELLING_TYPE,
} from "@/lib/selling/selling-detail.mapper";

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

export function SellLienWizard({ lienId }: { lienId: string }) {
  const router = useRouter();
  const { session } = useSession();
  const { lookup } = useSessionContext();
  const { show: showToast } = useToast();
  const documentCategories = lookup?.DocumentCategory ?? [];

  const [loading, setLoading] = useState(true);
  const [lien, setLien] = useState<LienDetailsResult | null>(null);
  const [step, setStep] = useState<1 | 2>(1);

  // Step 1 — buyer selection
  const [companies, setCompanies] = useState<SellingLookupItem[]>([]);
  const [companySearch, setCompanySearch] = useState("");
  const [companyId, setCompanyId] = useState<string | null>(null);
  const [companyName, setCompanyName] = useState<string>("");
  const [contacts, setContacts] = useState<SellingLookupItem[]>([]);
  const [contactId, setContactId] = useState<string | null>(null);
  const [contactSearch, setContactSearch] = useState("");
  const [loadingContacts, setLoadingContacts] = useState(false);

  // Step 2 — documents + message. Pricing/ask amount are edited on the lien
  // detail page (Medical Code & Marketplace Pricing panel), not here — this
  // step only displays what's already been set, matching how the rest of
  // this page treats those fields as read-only-until-explicitly-edited.
  const [messageToBuyer, setMessageToBuyer] = useState("");
  const [docSlots, setDocSlots] = useState<Record<string, DocSlotState>>(
    emptyDocSlots(),
  );
  const [sellingDocumentTypes, setSellingDocumentTypes] = useState<string[]>(
    [],
  );

  const [showConfirm, setShowConfirm] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [detail, companyList, documentTypesRes] = await Promise.all([
          liensService.getLienById(lienId),
          liensService.getFundingCompanies(),
          sellingLookupsApi.documentTypes(),
        ]);
        if (cancelled) return;
        setLien(detail);
        setCompanies(companyList);
        setSellingDocumentTypes(documentTypesRes.data.items);
        if (detail.fundingCompany) {
          setCompanyId(detail.fundingCompany.id);
          setCompanyName(detail.fundingCompany.name);
          if (detail.fundingCompany.contact) {
            setContactId(detail.fundingCompany.contact.id);
          }
        }
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

  useEffect(() => {
    if (!companyId) {
      setContacts([]);
      return;
    }
    let cancelled = false;
    setLoadingContacts(true);
    liensService
      .getFundingCompanyContacts(companyId)
      .then((items) => {
        if (cancelled) return;
        setContacts(items);
        if (items.length === 1) setContactId(items[0].id);
      })
      .catch(() => {
        if (!cancelled) setContacts([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingContacts(false);
      });
    return () => {
      cancelled = true;
    };
  }, [companyId]);

  const filteredCompanies = useMemo(() => {
    if (!companySearch.trim()) return companies;
    const q = companySearch.trim().toLowerCase();
    return companies.filter((c) => c.name.toLowerCase().includes(q));
  }, [companies, companySearch]);

  // Cap the unfiltered render — the lookup has been observed to return
  // thousands of rows for a single company (see the comment above the
  // contact list JSX), so render nothing until the user searches.
  const filteredContacts = useMemo(() => {
    if (!contactSearch.trim()) return contacts.slice(0, 25);
    const q = contactSearch.trim().toLowerCase();
    return contacts.filter((c) => c.name.toLowerCase().includes(q)).slice(0, 50);
  }, [contacts, contactSearch]);

  const askAmount = lien?.medicalPricing.askAmount ?? null;
  const pricingReady =
    (lien?.medicalPricing.rows.length ?? 0) > 0 && (askAmount ?? 0) > 0;
  const requiredDocsReady = REQUIRED_SALE_DOCUMENT_TYPES.every(
    (type) => docSlots[type]?.documentId,
  );
  const canAuthorize =
    !!companyId && !!contactId && pricingReady && requiredDocsReady;

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
        throw new Error("Document type list is still loading. Please try again.");
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
      setDocSlots((prev) => ({
        ...prev,
        [documentType]: {
          uploading: false,
          documentId: uploaded.id,
          displayName: file.name,
        },
      }));
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

  const uploadedDocumentRefs = () =>
    Object.entries(docSlots)
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
      const documents = uploadedDocumentRefs();
      if (documents.length > 0) {
        await liensService.saveDocuments(lienId, { documents });
      }
      showToast("Progress saved.", "success");
      router.push(`/selling/portfolio/${lienId}`);
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
    if (!companyId || !contactId || askAmount === null) return;
    setSubmitting(true);
    try {
      await liensService.saveDocuments(lienId, {
        documents: uploadedDocumentRefs(),
      });
      await liensService.prepareSale(lienId, {
        buyerFundingCompanyId: companyId,
        buyerContactId: contactId,
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
        <Link
          href={`/selling/portfolio/${lienId}`}
          className="text-gray-400 hover:text-gray-600"
        >
          <i className="ri-arrow-left-line text-xl" />
        </Link>
        <div className="flex-1 flex gap-2">
          <div
            className={`h-1 flex-1 rounded-full ${step >= 1 ? "bg-[#EE7132]" : "bg-gray-200"}`}
          />
          <div
            className={`h-1 flex-1 rounded-full ${step >= 2 ? "bg-[#EE7132]" : "bg-gray-200"}`}
          />
        </div>
      </div>

      {step === 1 && (
        <div className="space-y-4">
          <p className="text-xs text-gray-400">Step 1/2</p>
          <h1 className="text-2xl font-bold text-gray-900">
            Select a Funding Company
          </h1>
          <p className="text-sm text-gray-500">
            Choose the funding company that will receive this lien for review
            and potential purchase.
          </p>

          <div className="relative">
            <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
            <input
              type="text"
              placeholder="Search..."
              value={companySearch}
              onChange={(e) => setCompanySearch(e.target.value)}
              className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
            />
          </div>

          <div className="border border-gray-200 rounded-lg max-h-96 overflow-y-auto">
            {filteredCompanies.length === 0 && (
              <p className="px-4 py-6 text-sm text-gray-400 text-center">
                No funding companies found.
              </p>
            )}
            {filteredCompanies.map((company) => (
              <label
                key={company.id}
                className="flex items-center gap-3 px-4 py-3 border-b border-gray-100 last:border-0 cursor-pointer hover:bg-gray-50"
              >
                <input
                  type="radio"
                  name="fundingCompany"
                  checked={companyId === company.id}
                  onChange={() => {
                    setCompanyId(company.id);
                    setCompanyName(company.name);
                    setContactId(null);
                    setContactSearch("");
                  }}
                  className="accent-[#EE7132]"
                />
                <span className="text-sm text-gray-700">{company.name}</span>
              </label>
            ))}
          </div>

          {companyId && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Contact Person<span className="text-red-500 ml-0.5">*</span>
              </label>
              {loadingContacts ? (
                <p className="text-xs text-gray-400">Loading contacts...</p>
              ) : contacts.length === 0 ? (
                <p className="text-xs text-amber-600">
                  This funding company has no active contacts on file — a
                  contact is required before this lien can be sent to them.
                </p>
              ) : (
                <>
                  {/* The funding-company-contacts lookup can return far more
                      rows than belong to the selected company (seen live:
                      thousands, spanning unrelated law firms/people) — a
                      plain <select> would be unusable at that volume, so
                      this is a searchable list instead of a native dropdown. */}
                  <div className="relative">
                    <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
                    <input
                      type="text"
                      placeholder="Search contacts..."
                      value={contactSearch}
                      onChange={(e) => setContactSearch(e.target.value)}
                      className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                    />
                  </div>
                  {contactId && (
                    <p className="text-xs text-gray-500 mt-1.5">
                      Selected: {contacts.find((c) => c.id === contactId)?.name}
                    </p>
                  )}
                  <div className="border border-gray-200 rounded-lg max-h-48 overflow-y-auto mt-2">
                    {filteredContacts.length === 0 && (
                      <p className="px-4 py-3 text-sm text-gray-400 text-center">
                        No contacts match your search.
                      </p>
                    )}
                    {filteredContacts.map((c) => (
                      <label
                        key={c.id}
                        className="flex items-center gap-3 px-4 py-2 border-b border-gray-100 last:border-0 cursor-pointer hover:bg-gray-50"
                      >
                        <input
                          type="radio"
                          name="fundingContact"
                          checked={contactId === c.id}
                          onChange={() => setContactId(c.id)}
                          className="accent-[#EE7132]"
                        />
                        <span className="text-sm text-gray-700">{c.name}</span>
                      </label>
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          <div className="flex justify-end gap-3 pt-4">
            <Link
              href={`/selling/portfolio/${lienId}`}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
            >
              Cancel
            </Link>
            <button
              disabled={!companyId || !contactId}
              onClick={() => setStep(2)}
              className="text-sm px-6 py-2 bg-[#EE7132] hover:bg-[#EE7132]/90 text-white rounded-lg disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Continue
            </button>
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="space-y-4">
          <p className="text-xs text-gray-400">Step 2/2</p>
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
                This lien has no medical pricing or ask amount set yet. Go
                back to{" "}
                <Link
                  href={`/selling/portfolio/${lienId}`}
                  className="underline font-medium"
                >
                  the lien details page
                </Link>{" "}
                and edit &ldquo;Medical Code &amp; Marketplace Pricing&rdquo;
                before this lien can be sold.
              </p>
            </div>
          )}

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
            <div className="space-y-4">
              <LienInformationPanel lien={lien.lienInformation} />
              <FundingCompanyAndCaseInformationPanel
                fundingCompany={
                  companyId
                    ? {
                        id: companyId,
                        name: companyName,
                        contact: contactId
                          ? {
                              id: contactId,
                              name:
                                contacts.find((c) => c.id === contactId)
                                  ?.name ?? "",
                            }
                          : null,
                      }
                    : null
                }
                caseInformation={lien.caseInformation}
              />
              <MedicalCodesInformationPanel lien={lien.medicalPricing.rows} />
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
            <button
              onClick={() => setStep(1)}
              className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
            >
              Back
            </button>
            <div className="flex gap-3">
              <button
                onClick={saveForLater}
                disabled={submitting}
                className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600 disabled:opacity-50"
              >
                Save for Later
              </button>
              <button
                disabled={!canAuthorize || submitting}
                onClick={() => setShowConfirm(true)}
                className="text-sm px-6 py-2 bg-[#EE7132] hover:bg-[#EE7132]/90 text-white rounded-lg disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Authorize &amp; Send
              </button>
            </div>
          </div>
        </div>
      )}

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
              <strong>{companyName}</strong> for purchase consideration. Are
              you sure you want to continue?
            </p>
            <div>
              <label className="block text-xs font-medium text-gray-500 mb-1">
                Message to Buyer (optional)
              </label>
              <textarea
                value={messageToBuyer}
                onChange={(e) => setMessageToBuyer(e.target.value)}
                rows={2}
                placeholder="Optional note included with the buyer notification..."
                className="w-full border border-gray-200 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
              />
            </div>
          </div>
        }
        confirmLabel="Yes, Sell"
      />

      <Modal
        open={showSuccess}
        onClose={() => router.push(`/selling/portfolio/${lienId}`)}
        title="Lien Submitted Successfully"
        size="sm"
        footer={
          <button
            onClick={() => router.push(`/selling/portfolio/${lienId}`)}
            className="text-sm px-4 py-2 bg-[#EE7132] hover:bg-[#EE7132]/90 text-white rounded-lg"
          >
            Done
          </button>
        }
      >
        <p className="text-sm text-gray-600">
          Lien <strong>{lien.lienInformation.lienNumber}</strong> has been
          successfully submitted to <strong>{companyName}</strong> for
          review. The buyer has been notified and can now begin the
          evaluation process.
        </p>
      </Modal>
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
            : meta?.description ?? "(Optional)"}
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
