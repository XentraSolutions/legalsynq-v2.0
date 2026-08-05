export type StartStageMode = "FIRST_ACTIVE_STAGE" | "EXPLICIT_STAGE";
export type GovernanceUpdateSource =
  | "TENANT_PRODUCT_SETTINGS"
  | "CONTROL_CENTER";

export interface TaskGovernanceSettings {
  id: string;
  tenantId: string;
  productCode: string;
  requireAssigneeOnCreate: boolean;
  requireCaseLinkOnCreate: boolean;
  allowMultipleAssignees: boolean;
  requireWorkflowStageOnCreate: boolean;
  defaultStartStageMode: StartStageMode;
  explicitStartStageId?: string;
  version: number;
  lastUpdatedAt: string;
  lastUpdatedByUserId?: string;
  lastUpdatedByName?: string;
  lastUpdatedSource: GovernanceUpdateSource;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateTaskGovernanceRequest {
  requireAssigneeOnCreate: boolean;
  requireCaseLinkOnCreate: boolean;
  allowMultipleAssignees: boolean;
  requireWorkflowStageOnCreate: boolean;
  defaultStartStageMode: StartStageMode;
  explicitStartStageId?: string;
  updateSource: GovernanceUpdateSource;
  version: number;
  updatedByName?: string;
}
export type TaskStatus =
  | "UPCOMING"
  | "INPROGRESS"
  | "INREVIEW"
  | "COMPLETED"
  | "CANCELLED";
export type TaskPriority = "LOW" | "MEDIUM" | "HIGH";

export interface TaskLienLinkDto {
  taskId: string;
  lienId: string;
  createdAtUtc: string;
}

export type TaskSourceType = "MANUAL" | "AUTOMATED";

export interface TaskDto {
  id: string;
  tenantId: string;
  title: string;
  description?: string;
  status: TaskStatus;
  priority: TaskPriority;
  assignedTo?: string;
  caseId?: string;
  caseCode?: string;
  workflowStageId?: string;
  dueDate?: string;
  completedAt?: string;
  closedByUserId?: string;
  linkedLiens: TaskLienLinkDto[];
  createdByUserId?: string;
  sourceType?: TaskSourceType;
  generationRuleId?: string;
  generatingTemplateId?: string;
  isSystemGenerated?: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  workflowInstanceId?: string;
  workflowStepKey?: string;
}

export interface PaginatedTasksDto {
  items: TaskDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  priority?: TaskPriority;
  assignedTo?: string;
  caseId?: string;
  lienIds?: string[];
  workflowStageId?: string;
  dueDate?: string;
  templateId?: string;
  status: string;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  priority?: TaskPriority;
  caseId?: string;
  taskId?: string;
  lienIds?: string[];
  workflowStageId?: string;
  dueDate?: string;
  status: string;
  assignedTo?: string;
}

export interface AssignTaskRequest {
  assignedUserId?: string;
}

export interface UpdateTaskStatusRequest {
  status: TaskStatus;
}

export interface TasksQuery {
  search?: string;
  status?: TaskStatus;
  priority?: TaskPriority;
  assignedUserId?: string;
  caseId?: string;
  lienId?: string;
  workflowStageId?: string;
  assignmentScope?: "me" | "others" | "unassigned" | "all";
  page?: number;
  pageSize?: number;
}

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  UPCOMING: "Upcoming",
  INPROGRESS: "In Progress",
  INREVIEW: "In Review",
  COMPLETED: "Completed",
  CANCELLED: "Cancelled",
};

export const TASK_STATUS_COLORS: Record<
  TaskStatus,
  { bg: string; text: string; border: string }
> = {
  UPCOMING: {
    bg: "bg-gray-100",
    text: "text-gray-700",
    border: "",
  },
  INPROGRESS: {
    bg: "bg-blue-50",
    text: "text-blue-700",
    border: "",
  },
  INREVIEW: {
    bg: "bg-amber-50",
    text: "text-amber-700",
    border: "",
  },
  COMPLETED: {
    bg: "bg-green-50",
    text: "text-green-700",
    border: "",
  },
  CANCELLED: {
    bg: "bg-red-50",
    text: "text-red-700",
    border: "",
  },
};

export const TASK_PRIORITY_COLORS: Record<TaskPriority, string> = {
  LOW: "text-gray-500",
  MEDIUM: "text-blue-500",
  HIGH: "text-orange-500",
};

export const TASK_PRIORITY_ICONS: Record<TaskPriority, string> = {
  LOW: "ri-arrow-down-line",
  MEDIUM: "ri-subtract-line",
  HIGH: "ri-arrow-up-line",
};

export const ALL_TASK_STATUSES: TaskStatus[] = [
  "UPCOMING",
  "INPROGRESS",
  "INREVIEW",
  "COMPLETED",
  "CANCELLED",
];
export const ACTIVE_TASK_STATUSES: TaskStatus[] = [
  "UPCOMING",
  "INPROGRESS",
  "INREVIEW",
];
export const BOARD_COLUMNS: TaskStatus[] = [
  "UPCOMING",
  "INPROGRESS",
  "INREVIEW",
  "COMPLETED",
];

export const PRIORITY_LABELS: Record<string, string> = {
  LOW: "Low",
  MEDIUM: "Medium",
  HIGH: "High",
  URGENT: "Urgent",
};
export const AVATAR_COLORS = [
  "bg-violet-500",
  "bg-blue-500",
  "bg-teal-500",
  "bg-indigo-500",
  "bg-pink-500",
  "bg-amber-500",
];
