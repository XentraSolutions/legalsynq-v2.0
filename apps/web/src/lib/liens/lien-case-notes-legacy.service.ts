import { lienCaseNotesLegacyApi } from './lien-case-notes-legacy.api';
import type { CaseFeedNote, CaseNoteFeedSort } from './lien-case-notes-legacy.types';

export type { CaseFeedNote, CaseNoteFeedSort };

export const lienCaseNotesLegacyService = {
  async getNotes(
    caseId: string,
    showDeleted: boolean,
    sort: CaseNoteFeedSort,
  ): Promise<CaseFeedNote[]> {
    const res = await lienCaseNotesLegacyApi.list(caseId, showDeleted, sort);
    if (!res.data.isSuccess) throw new Error(res.data.message || 'Failed to load notes');
    return res.data.data ?? [];
  },

  async addNote(caseId: string, note: string): Promise<void> {
    const res = await lienCaseNotesLegacyApi.create(caseId, note);
    if (!res.data.isSuccess) throw new Error(res.data.message || 'Failed to add note');
  },

  async deleteNote(noteId: string): Promise<void> {
    const res = await lienCaseNotesLegacyApi.remove(noteId);
    if (!res.data.isSuccess) throw new Error(res.data.message || 'Failed to delete note');
  },
};
