import { lienCaseNotesLegacyApi } from "./lien-case-notes-legacy.api";
import type {
  CaseFeedNote,
  CaseNoteFeedSort,
} from "./lien-case-notes-legacy.types";

export type { CaseFeedNote, CaseNoteFeedSort };

// Unlike the rest of the lien backend (which serializes raw UTC instants
// with a 'Z' suffix — see format-date.ts), the legacy get-notes endpoint
// returns `created` as an already-formatted, timezone-less display string:
// "MM/DD/YYYY hh:mm AM/PM", assumed UTC (matching the backend's UtcNow
// convention elsewhere). Passed straight through, that string doesn't
// contain a 'T', so DateDisplay's ISO parser falls back to `new Date(value)`
// and silently mis-parses it as the viewer's local time instead of UTC —
// converting to the tenant's timezone becomes a no-op whenever the viewer's
// machine happens to be in that same timezone. Normalize to a real ISO UTC
// instant here, once, so every consumer (just DateDisplay today) gets a
// value it can actually convert.
const LEGACY_FEED_TIMESTAMP_PATTERN =
  /^(\d{2})\/(\d{2})\/(\d{4}) (\d{2}):(\d{2}) (AM|PM)$/;

function toIsoUtc(created: string): string {
  const match = LEGACY_FEED_TIMESTAMP_PATTERN.exec(created.trim());
  if (!match) return created;
  const [, month, day, year, hour12, minute, meridiem] = match;
  let hour = Number(hour12) % 12;
  if (meridiem === "PM") hour += 12;
  return `${year}-${month}-${day}T${String(hour).padStart(2, "0")}:${minute}:00Z`;
}

export const lienCaseNotesLegacyService = {
  async getNotes(
    caseId: string,
    showDeleted: boolean,
    sort: CaseNoteFeedSort,
  ): Promise<CaseFeedNote[]> {
    const res = await lienCaseNotesLegacyApi.list(caseId, showDeleted, sort);
    if (!res.data.isSuccess)
      throw new Error(res.data.message || "Failed to load notes");
    return (res.data.data ?? []).map((note) => ({
      ...note,
      created: note.created,
    }));
  },

  async addNote(caseId: string, note: string): Promise<void> {
    const res = await lienCaseNotesLegacyApi.create(caseId, note);
    if (!res.data.isSuccess)
      throw new Error(res.data.message || "Failed to add note");
  },

  async deleteNote(noteId: string): Promise<void> {
    const res = await lienCaseNotesLegacyApi.remove(noteId);
    if (!res.data.isSuccess)
      throw new Error(res.data.message || "Failed to delete note");
  },
};
