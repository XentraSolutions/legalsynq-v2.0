import { apiClient } from '@/lib/api-client';
import type {
  CaseNoteFeedSort,
  GetCaseNotesFeedResponse,
  MutateCaseNoteFeedResponse,
} from './lien-case-notes-legacy.types';

export const lienCaseNotesLegacyApi = {
  listCaseNotes(caseId: string) {
    return apiClient.get<GetCaseNotesFeedResponse>(
      `/lien/api/liens/cases/notes/${caseId}`,
    );
  },

  list(caseId: string, showDeleted: boolean, sort: CaseNoteFeedSort) {
    return apiClient.post<GetCaseNotesFeedResponse>('/lien/api/liens/cases/get-notes', {
      caseId,
      showDeleted: String(showDeleted),
      sort,
    });
  },

  create(caseId: string, note: string) {
    return apiClient.post<MutateCaseNoteFeedResponse>('/lien/api/liens/cases/add-note', {
      caseId,
      note,
    });
  },

  remove(noteId: string) {
    return apiClient.post<MutateCaseNoteFeedResponse>('/lien/api/liens/cases/delete-note', {
      noteId,
    });
  },
};
