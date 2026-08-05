import { apiClient } from "@/lib/api-client";
import type {
  TaskDto,
  PaginatedTasksDto,
  CreateTaskRequest,
  UpdateTaskRequest,
  AssignTaskRequest,
  UpdateTaskStatusRequest,
  TasksQuery,
} from "./lien-tasks.types";

function toQs(params: Record<string, unknown>): string {
  const pairs = Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(
      ([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`,
    );
  return pairs.length ? `?${pairs.join("&")}` : "";
}

export const lienTasksApi = {
  list(query: TasksQuery = {}) {
    return apiClient.get<{ data: TaskDto[] }>(
      `/lien/api/liens/cases/get-task/${query.caseId}`,
    );
  },

  getById(id: string) {
    return apiClient.get<TaskDto>(`/lien/api/liens/tasks/${id}`);
  },

  create(request: CreateTaskRequest) {
    return apiClient.post<TaskDto>(
      "/lien/api/liens/cases/task/create",
      request,
    );
  },

  update(request: UpdateTaskRequest) {
    return apiClient.patch<TaskDto>(
      "/lien/api/liens/cases/task/update",
      request,
    );
  },

  delete(id: string) {
    return apiClient.delete<TaskDto>(`/lien/api/liens/cases/task/delete/${id}`);
  },

  assign(id: string, request: AssignTaskRequest) {
    return apiClient.post<TaskDto>(
      `/lien/api/liens/tasks/${id}/assign`,
      request,
    );
  },

  updateStatus(id: string, request: UpdateTaskStatusRequest) {
    return apiClient.post<TaskDto>(
      `/lien/api/liens/tasks/${id}/status`,
      request,
    );
  },

  complete(id: string) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/complete`, {});
  },

  cancel(id: string) {
    return apiClient.post<TaskDto>(`/lien/api/liens/tasks/${id}/cancel`, {});
  },
};
