/**
 * Types for the legacy case-notes-feed endpoints (LS-LIENS-CASE-005 legacy
 * compatibility routes: /api/liens/cases/{add-note,get-notes,delete-note}).
 * Distinct from the richer categorized notes in lien-case-notes.types.ts.
 */

export type CaseNoteFeedSort = "newest" | "oldest";

export interface CaseFeedNote {
  id: string;
  caseId: string;
  note: string;
  isDeleted: "Y" | "N";
  /** ISO UTC instant — normalized by the service from the API's raw "MM/DD/YYYY hh:mm AM/PM" string. */
  created: string;
  createdAtUtc: string;
  createdBy: string;
  userId: string;
  canDelete: boolean;
}

export interface LegacyEnvelope<T> {
  isSuccess: boolean;
  message: string;
  data: T;
}

export type GetCaseNotesFeedResponse = LegacyEnvelope<CaseFeedNote[]>;
export type MutateCaseNoteFeedResponse = LegacyEnvelope<null>;
