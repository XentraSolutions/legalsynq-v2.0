import type {
  MedicalPricingRowData,
  MedicalPricingRowDetail,
  SellingDocumentReference,
  SellingDocumentReferenceData,
} from "@/types/lien-selling";

// SellingV2Endpoints stores pricing rows and document references as generic
// ServicingItems and JSON-encodes their real fields into `notes` (see
// SaveMedicalPricing / SaveDocuments in SellingV2Endpoints.cs) — there are no
// dedicated columns for them yet. These parse that JSON back out.
export function parsePricingRow(
  row: MedicalPricingRowDetail,
): MedicalPricingRowData {
  try {
    const parsed = row.notes ? JSON.parse(row.notes) : {};
    return {
      medicalCode: parsed.MedicalCode ?? parsed.medicalCode ?? row.description ?? "",
      description: parsed.Description ?? parsed.description ?? null,
      serviceDate: parsed.ServiceDate ?? parsed.serviceDate ?? null,
      billingAmount: Number(parsed.BillingAmount ?? parsed.billingAmount ?? 0),
      medicareCost: Number(parsed.MedicareCost ?? parsed.medicareCost ?? 0),
      targetSaleAmount: Number(parsed.targetSaleAmount ?? parsed.TargetSaleAmount ?? 0),
    };
  } catch {
    return {
      medicalCode: row.description ?? "",
      description: null,
      serviceDate: null,
      billingAmount: 0,
      medicareCost: 0,
      targetSaleAmount: 0,
    };
  }
}

export function parseDocumentReference(
  doc: SellingDocumentReference,
): SellingDocumentReferenceData {
  try {
    const parsed = doc.notes ? JSON.parse(doc.notes) : {};
    return {
      documentId: parsed.DocumentId ?? parsed.documentId ?? "",
      documentType: parsed.DocumentType ?? parsed.documentType ?? "Other",
      displayName: parsed.DisplayName ?? parsed.displayName ?? doc.description ?? null,
    };
  } catch {
    return {
      documentId: "",
      documentType: "Other",
      displayName: doc.description ?? null,
    };
  }
}

const SELLER_STATUS_LABELS: Record<string, string> = {
  Draft: "Draft",
  Pending: "Pending",
  Internal: "Internal",
  PreparedForSale: "Prepared for Sale",
  SubmittedForSale: "Under Review",
  Accepted: "Accepted",
  Declined: "Declined",
  Sold: "Sold",
  Withdrawn: "Withdrawn",
  Archived: "Archived",
};

export function sellerStatusLabel(sellerStatus: string | null | undefined): string {
  if (!sellerStatus) return "—";
  return SELLER_STATUS_LABELS[sellerStatus] ?? sellerStatus;
}

export const REQUIRED_SALE_DOCUMENT_TYPES = ["LienAgreement", "MedicalBill"] as const;
export const OPTIONAL_SALE_DOCUMENT_TYPES = ["MedicalRecord", "PoliceReport"] as const;

export const SALE_DOCUMENT_LABELS: Record<string, { title: string; description: string }> = {
  LienAgreement: {
    title: "Signed Lien / LOP (Letter of Protection)",
    description: "Proves you have the legal right to collect on the case",
  },
  MedicalBill: {
    title: "Itemized Bill / HCFA-1500 Form",
    description: "Proves the exact amount of medical debt being sold",
  },
  MedicalRecord: {
    title: "Clinical Chart Notes / Medical Records",
    description: "Proves the medical necessity and active treatment of the injuries",
  },
  PoliceReport: {
    title: "Case Underwriting / Police Report",
    description: "Proves accident liability and available insurance policy limits",
  },
  // Saved documentType for the PoliceReport slot — the selling domain's
  // valid-values enum has no PoliceReport entry (see
  // SALE_DOCUMENT_TYPE_TO_SELLING_TYPE below), so uploads from that slot
  // are persisted as "Other". Kept in sync with the PoliceReport label so
  // the documents list still shows a friendly title.
  Other: {
    title: "Case Underwriting / Police Report",
    description: "Proves accident liability and available insurance policy limits",
  },
};

// The wizard's sale document slots use their own keys (above), but the
// document service's DocumentCategory lookup (GET /liens/api/liens/document/type,
// exposed via useSessionContext().lookup.DocumentCategory) has its own,
// differently-named codes. This aliases each slot to the closest existing
// lookup code so the file upload uses a real documentTypeId instead of a
// made-up one.
export const SALE_DOCUMENT_TYPE_TO_CATEGORY_CODE: Record<string, string> = {
  LienAgreement: "LienAgreement",
  MedicalBill: "HicfaOrBill",
  MedicalRecord: "MedicalRecord",
  PoliceReport: "PoliceReport",
};

// GET /selling/lookups/document-types returns the selling domain's own
// smaller enum of valid `documentType` values for the saveDocuments payload
// (MedicalBill, MedicalRecord, LienAgreement, SettlementStatement, Other) —
// it has no PoliceReport value, so that slot is saved as "Other".
export const SALE_DOCUMENT_TYPE_TO_SELLING_TYPE: Record<string, string> = {
  LienAgreement: "LienAgreement",
  MedicalBill: "MedicalBill",
  MedicalRecord: "MedicalRecord",
  PoliceReport: "Other",
};
