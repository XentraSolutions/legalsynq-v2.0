import { liensService } from "@/lib/selling";
import { parsePricingRow } from "@/lib/selling/selling-detail.mapper";

export const TOTAL_STEPS = 4;

// Query param a wizard step reads to tell "opened as a standalone edit from
// the lien detail page" apart from "opened as part of the create/edit
// wizard" — same route and component either way.
export const DETAIL_EDIT_PARAM = "returnTo";
export const DETAIL_EDIT_VALUE = "detail";

export function detailHref(lienId: string) {
  return `/selling/portfolio/lien/${lienId}`;
}

export function detailEditHref(lienId: string, step: number) {
  return `${detailHref(lienId)}/edit/step-${step}?${DETAIL_EDIT_PARAM}=${DETAIL_EDIT_VALUE}`;
}

type Lien = Awaited<ReturnType<typeof liensService.getLienById>>;

// Per-step form payloads, parsed from a fetched lien. Each step component
// hydrates only the slot it needs — this is the single source of truth for
// how the API model maps to each step's form shape.
export function buildFormsFromLien(lien: Lien) {
  const rows = lien.medicalPricing.rows.map((row) => {
    const parsed = parsePricingRow(row);
    return {
      id: row.id,
      code: parsed.medicalCode,
      description: parsed.description ?? "",
      billingAmount: parsed.billingAmount,
      medicareCost: parsed.medicareCost,
      targetSaleAmount: parsed.targetSaleAmount,
    };
  });

  return {
    lienInfo: {
      status: lien.lienInformation.sellerStatus,
      listingVisibility: lien.lienInformation.listingVisibility,
      caseId: lien.caseInformation?.id ?? "",
      initialServiceDate: lien.lienInformation.initialServiceDate ?? "",
      endServiceDate: lien.lienInformation.endServiceDate ?? "",
      notes: lien.lienInformation.notes ?? "",
    },
    providerFunding: {
      medicalProviderId: lien.medicalProvider?.id ?? "",
      medicalProvider: lien.medicalProvider?.name ?? "",
      facilityId: lien.facility?.id ?? "",
      facility: lien.facility?.name ?? "",
      fundingCompanyId: lien.fundingCompany?.id ?? "",
      fundingCompany: lien.fundingCompany?.name ?? "",
      fundingCompanyContactId: lien.fundingCompany?.contact?.id ?? "",
      fundingCompanyContact: lien.fundingCompany?.contact?.name ?? "",
    },
    medicalCodes: { codeRows: rows },
  };
}

// Every step's "Continue" persists to the backend and moves the URL to the
// next step's real route — this is the one place that knows that URL shape.
export function goToStep(
  router: { push: (href: string) => void },
  lienId: string,
  step: number,
) {
  router.push(`/selling/portfolio/lien/${lienId}/edit/step-${step}`);
}
