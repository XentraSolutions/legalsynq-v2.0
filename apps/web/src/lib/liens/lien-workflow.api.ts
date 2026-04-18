import { apiClient, ApiError } from '@/lib/api-client';
import type {
  WorkflowConfigDto,
  CreateWorkflowConfigRequest,
  UpdateWorkflowConfigRequest,
  AddWorkflowStageRequest,
  UpdateWorkflowStageRequest,
  ReorderStagesRequest,
} from './lien-workflow.types';

const BASE = '/lien/api/liens/workflow-config';

async function safeGet(path: string): Promise<WorkflowConfigDto | null> {
  try {
    const { data } = await apiClient.get<WorkflowConfigDto>(path);
    return data ?? null;
  } catch (err) {
    if (err instanceof ApiError && (err.status === 204 || err.status === 404)) return null;
    throw err;
  }
}

export const lienWorkflowApi = {
  async get(): Promise<WorkflowConfigDto | null> {
    return safeGet(BASE);
  },

  async create(request: CreateWorkflowConfigRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(BASE, request);
    return data;
  },

  async update(id: string, request: UpdateWorkflowConfigRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.put<WorkflowConfigDto>(`${BASE}/${id}`, request);
    return data;
  },

  async addStage(id: string, request: AddWorkflowStageRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(`${BASE}/${id}/stages`, request);
    return data;
  },

  async updateStage(id: string, stageId: string, request: UpdateWorkflowStageRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.put<WorkflowConfigDto>(`${BASE}/${id}/stages/${stageId}`, request);
    return data;
  },

  async removeStage(id: string, stageId: string): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(`${BASE}/${id}/stages/${stageId}/deactivate`, {});
    return data;
  },

  async reorderStages(id: string, request: ReorderStagesRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(`${BASE}/${id}/stages/reorder`, request);
    return data;
  },

  // Admin endpoints (explicit tenantId, Control Center only)
  async adminGet(tenantId: string): Promise<WorkflowConfigDto | null> {
    return safeGet(`/lien/api/liens/admin/workflow-config/tenants/${tenantId}`);
  },

  async adminCreate(tenantId: string, request: CreateWorkflowConfigRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}`, request
    );
    return data;
  },

  async adminUpdate(tenantId: string, id: string, request: UpdateWorkflowConfigRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.put<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}/${id}`, request
    );
    return data;
  },

  async adminAddStage(tenantId: string, id: string, request: AddWorkflowStageRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}/${id}/stages`, request
    );
    return data;
  },

  async adminUpdateStage(tenantId: string, id: string, stageId: string, request: UpdateWorkflowStageRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.put<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}/${id}/stages/${stageId}`, request
    );
    return data;
  },

  async adminRemoveStage(tenantId: string, id: string, stageId: string): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}/${id}/stages/${stageId}/deactivate`, {}
    );
    return data;
  },

  async adminReorderStages(tenantId: string, id: string, request: ReorderStagesRequest): Promise<WorkflowConfigDto> {
    const { data } = await apiClient.post<WorkflowConfigDto>(
      `/lien/api/liens/admin/workflow-config/tenants/${tenantId}/${id}/stages/reorder`, request
    );
    return data;
  },
};
