export interface TaskNoteResponse {
  id: string;
  taskId: string;
  tenantId: string;
  content: string;
  createdByUserId: string;
  createdByUserName?: string;
  isEdited: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTaskNoteRequest {
  content: string;
}

export interface UpdateTaskNoteRequest {
  content: string;
}
