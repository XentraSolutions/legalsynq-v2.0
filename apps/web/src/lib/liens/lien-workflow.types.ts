export type WorkflowUpdateSource = 'TENANT_PRODUCT_SETTINGS' | 'CONTROL_CENTER';

export interface WorkflowStageDto {
  id: string;
  workflowConfigId: string;
  stageName: string;
  stageOrder: number;
  description?: string;
  isActive: boolean;
  defaultOwnerRole?: string;
  slaMetadata?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface WorkflowConfigDto {
  id: string;
  tenantId: string;
  productCode: string;
  workflowName: string;
  version: number;
  isActive: boolean;
  lastUpdatedAt: string;
  lastUpdatedByUserId?: string;
  lastUpdatedByName?: string;
  lastUpdatedSource: WorkflowUpdateSource;
  stages: WorkflowStageDto[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateWorkflowConfigRequest {
  workflowName: string;
  updateSource: WorkflowUpdateSource;
  updatedByName?: string;
}

export interface UpdateWorkflowConfigRequest {
  workflowName: string;
  isActive: boolean;
  updateSource: WorkflowUpdateSource;
  updatedByName?: string;
  version: number;
}

export interface AddWorkflowStageRequest {
  stageName: string;
  stageOrder: number;
  description?: string;
  defaultOwnerRole?: string;
  slaMetadata?: string;
}

export interface UpdateWorkflowStageRequest {
  stageName: string;
  stageOrder: number;
  isActive: boolean;
  description?: string;
  defaultOwnerRole?: string;
  slaMetadata?: string;
}

export interface ReorderStagesRequest {
  stages: { stageId: string; stageOrder: number }[];
}
