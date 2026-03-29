'use server';

import { controlCenterServerApi } from '@/lib/control-center-api';
import type { SupportCaseDetail, SupportCaseStatus, SupportNote, SupportCase } from '@/types/control-center';

export interface UpdateStatusResult {
  success:  boolean;
  case?:    SupportCase;
  error?:   string;
}

export interface AddNoteResult {
  success: boolean;
  note?:   SupportNote;
  error?:  string;
}

export interface CreateCaseResult {
  success: boolean;
  case?:   SupportCaseDetail;
  error?:  string;
}

/**
 * Update the status of a support case.
 * TODO: replace with POST /identity/api/admin/support/{id}/status
 */
export async function updateCaseStatus(
  caseId: string,
  status: SupportCaseStatus,
): Promise<UpdateStatusResult> {
  try {
    const updated = await controlCenterServerApi.support.updateStatus(caseId, status);
    return { success: true, case: updated };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to update status.' };
  }
}

/**
 * Add an internal note to a support case.
 * TODO: replace with POST /identity/api/admin/support/{id}/notes
 */
export async function addCaseNote(
  caseId:  string,
  message: string,
): Promise<AddNoteResult> {
  if (!message.trim()) return { success: false, error: 'Note cannot be empty.' };
  try {
    const note = await controlCenterServerApi.support.addNote(caseId, message.trim());
    return { success: true, note };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to add note.' };
  }
}

/**
 * Create a new support case.
 * TODO: replace with POST /identity/api/admin/support
 */
export async function createSupportCase(data: {
  title:      string;
  tenantId:   string;
  tenantName: string;
  userId?:    string;
  userName?:  string;
  category:   string;
  priority:   SupportCase['priority'];
}): Promise<CreateCaseResult> {
  if (!data.title.trim()) return { success: false, error: 'Title is required.' };
  try {
    const created = await controlCenterServerApi.support.create(data);
    return { success: true, case: created };
  } catch (err) {
    return { success: false, error: err instanceof Error ? err.message : 'Failed to create case.' };
  }
}
