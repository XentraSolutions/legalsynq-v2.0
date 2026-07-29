import { apiClient } from '@/shared/api/client';

import type {
  CreateTaskRequest,
  LienTask,
  TaskHistoryEntry,
  TaskListResult,
  TaskNote,
  TaskQueryParams,
  UpdateTaskRequest,
} from './types';

const BASE_PATH = '/liens/api/liens/tasks';

export const taskKeys = {
  all: ['tasks'] as const,
  list: (params: TaskQueryParams) => [...taskKeys.all, 'list', params] as const,
  detail: (id: string) => [...taskKeys.all, 'detail', id] as const,
};

export const TasksApi = {
  async list(params: TaskQueryParams = {}): Promise<TaskListResult> {
    const response = await apiClient.get<TaskListResult>(BASE_PATH, { params });
    return response.data;
  },

  async get(id: string): Promise<LienTask> {
    const response = await apiClient.get<LienTask>(`${BASE_PATH}/${id}`);
    return response.data;
  },

  async create(body: CreateTaskRequest): Promise<LienTask> {
    const response = await apiClient.post<LienTask>(BASE_PATH, body);
    return response.data;
  },

  async update(id: string, body: UpdateTaskRequest): Promise<LienTask> {
    const response = await apiClient.put<LienTask>(`${BASE_PATH}/${id}`, body);
    return response.data;
  },

  async assign(id: string, assignedUserId?: string): Promise<LienTask> {
    const response = await apiClient.post<LienTask>(`${BASE_PATH}/${id}/assign`, {
      assignedUserId,
    });
    return response.data;
  },

  async updateStatus(id: string, status: string): Promise<LienTask> {
    const response = await apiClient.post<LienTask>(`${BASE_PATH}/${id}/status`, { status });
    return response.data;
  },

  async complete(id: string): Promise<LienTask> {
    const response = await apiClient.post<LienTask>(`${BASE_PATH}/${id}/complete`);
    return response.data;
  },

  async cancel(id: string): Promise<LienTask> {
    const response = await apiClient.post<LienTask>(`${BASE_PATH}/${id}/cancel`);
    return response.data;
  },

  async listNotes(taskId: string): Promise<TaskNote[]> {
    const response = await apiClient.get<TaskNote[]>(`${BASE_PATH}/${taskId}/notes`);
    return response.data;
  },

  async addNote(taskId: string, content: string): Promise<TaskNote> {
    const response = await apiClient.post<TaskNote>(`${BASE_PATH}/${taskId}/notes`, { content });
    return response.data;
  },

  async updateNote(taskId: string, noteId: string, content: string): Promise<TaskNote> {
    const response = await apiClient.put<TaskNote>(`${BASE_PATH}/${taskId}/notes/${noteId}`, {
      content,
    });
    return response.data;
  },

  async deleteNote(taskId: string, noteId: string): Promise<void> {
    await apiClient.delete(`${BASE_PATH}/${taskId}/notes/${noteId}`);
  },

  async getHistory(taskId: string): Promise<TaskHistoryEntry[]> {
    const response = await apiClient.get<TaskHistoryEntry[]>(`${BASE_PATH}/${taskId}/history`);
    return response.data;
  },
};
