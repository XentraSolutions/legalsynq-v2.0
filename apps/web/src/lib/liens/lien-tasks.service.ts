import { lienTasksApi } from "./lien-tasks.api";
import type {
  TaskDto,
  PaginatedTasksDto,
  CreateTaskRequest,
  UpdateTaskRequest,
  AssignTaskRequest,
  TaskStatus,
  TasksQuery,
} from "./lien-tasks.types";

export const lienTasksService = {
  async getTasks(query: TasksQuery = {}): Promise<{ items: TaskDto[] }> {
    const { data } = await lienTasksApi.list(query);
    return { items: data.data };
  },

  async getTask(id: string): Promise<TaskDto> {
    const { data } = await lienTasksApi.getById(id);
    return data;
  },

  async createTask(request: CreateTaskRequest): Promise<TaskDto> {
    const { data } = await lienTasksApi.create(request);
    return data;
  },

  async updateTask(request: UpdateTaskRequest): Promise<TaskDto> {
    const { data } = await lienTasksApi.update(request);
    return data;
  },
  async deleteTask(id: string): Promise<TaskDto> {
    const { data } = await lienTasksApi.delete(id);
    return data;
  },

  async assignTask(id: string, request: AssignTaskRequest): Promise<TaskDto> {
    const { data } = await lienTasksApi.assign(id, request);
    return data;
  },

  async updateStatus(id: string, status: TaskStatus): Promise<TaskDto> {
    const { data } = await lienTasksApi.updateStatus(id, { status });
    return data;
  },

  async completeTask(id: string): Promise<TaskDto> {
    const { data } = await lienTasksApi.complete(id);
    return data;
  },

  async cancelTask(id: string): Promise<TaskDto> {
    const { data } = await lienTasksApi.cancel(id);
    return data;
  },
};
