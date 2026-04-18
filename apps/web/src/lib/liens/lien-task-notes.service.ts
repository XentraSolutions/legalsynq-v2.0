import { lienTaskNotesApi } from './lien-task-notes.api';
import type {
  TaskNoteResponse,
  CreateTaskNoteRequest,
  UpdateTaskNoteRequest,
} from './lien-task-notes.types';

export const lienTaskNotesService = {
  async getNotes(taskId: string): Promise<TaskNoteResponse[]> {
    return lienTaskNotesApi.list(taskId);
  },

  async createNote(taskId: string, content: string): Promise<TaskNoteResponse> {
    const request: CreateTaskNoteRequest = { content };
    return lienTaskNotesApi.create(taskId, request);
  },

  async updateNote(taskId: string, noteId: string, content: string): Promise<TaskNoteResponse> {
    const request: UpdateTaskNoteRequest = { content };
    return lienTaskNotesApi.update(taskId, noteId, request);
  },

  async deleteNote(taskId: string, noteId: string): Promise<void> {
    return lienTaskNotesApi.delete(taskId, noteId);
  },
};
