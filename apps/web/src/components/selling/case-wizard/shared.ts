export const TOTAL_STEPS = 2;

// Query param a wizard step reads to tell "opened as a standalone edit from
// the case detail page" apart from "opened as part of the create wizard" —
// same route shape as the lien wizard's DETAIL_EDIT_PARAM
// (@/components/selling/lien-wizard/shared).
export const DETAIL_EDIT_PARAM = "returnTo";
export const DETAIL_EDIT_VALUE = "detail";

export function detailHref(caseId: string) {
  return `/selling/portfolio/cases/${caseId}`;
}

export function detailEditHref(caseId: string, step: number) {
  return `${detailHref(caseId)}/edit/step-${step}?${DETAIL_EDIT_PARAM}=${DETAIL_EDIT_VALUE}`;
}

// A case doesn't exist yet during creation — only its draft does. This is
// the resumable URL a draft's step lives at, mirroring the lien wizard's
// /lien/[lienId]/edit/step-{n} shape but keyed by draftId instead.
export function draftStepHref(draftId: string, step: number) {
  return `/selling/portfolio/cases/draft/${draftId}/step-${step}`;
}

// Full multi-step edit of an existing, already-finalized case (walks
// step-1 -> step-2 with the progress bar and Back/Continue, same mechanics
// as the lien wizard's /lien/[lienId]/edit/step-{n}) — as opposed to
// detailEditHref's single-section Cancel/Save edit.
export function caseEditStepHref(caseId: string, step: number) {
  return `${detailHref(caseId)}/edit/step-${step}`;
}
