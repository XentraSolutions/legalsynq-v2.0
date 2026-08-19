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

const SELLER_STATUS_LABEL_OVERRIDES: Record<string, string> = {
  SubmittedForSale: "Under Review",
};

export function sellerStatusLabel(sellerStatus: string | null | undefined): string {
  if (!sellerStatus) return "—";
  return SELLER_STATUS_LABEL_OVERRIDES[sellerStatus] ?? camelCaseToLabel(sellerStatus);
}

const WHO = "WHO";
const HOWMUCH = "HOWMUCH";

export const REQUIRED_SALE_DOCUMENT_TYPES = [WHO, HOWMUCH] as const;

export function optionalSaleDocumentTypes(sellingDocumentTypes: string[]) {
  return sellingDocumentTypes.filter(
    (type) => !(REQUIRED_SALE_DOCUMENT_TYPES as readonly string[]).includes(type),
  );
}

export const SALE_DOCUMENT_LABELS: Record<string, { title: string }> = {
  [WHO]: { title: "Signed Lien / LOP (Letter of Protection)" },
  [HOWMUCH]: { title: "Itemized Bill / HCFA-1500 Form" },
};

/** "PoliceReport" -> "Police Report" */
export function camelCaseToLabel(type: string): string {
  return type.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

const REQUIRED_SALE_DOCUMENT_CATEGORY_CODES: Record<string, string[]> = {
  [WHO]: ["SignedLien", "LOP", "LetterOfProtection"],
  [HOWMUCH]: ["ItemizedBill", "HCFA-1500", "CMS-1500", "HCFA"],
};

/** Resolves the DocumentCategory row an upload for `saleDocumentType` should use. */
export function resolveDocumentCategory<
  T extends { id: string; code: string },
>(saleDocumentType: string, documentCategories: T[]): T | undefined {
  const candidateCodes = REQUIRED_SALE_DOCUMENT_CATEGORY_CODES[saleDocumentType];
  const match = candidateCodes
    ? documentCategories.find((c) =>
        candidateCodes.some(
          (code) => code.toLowerCase() === c.code.toLowerCase(),
        ),
      )
    : undefined;
  return match ?? documentCategories.find((c) => c.code.toLowerCase() === "other");
}
