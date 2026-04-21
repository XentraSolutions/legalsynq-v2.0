import { apiClient } from '@/lib/api-client';
import type { TaskHistoryEvent, TaskHistoryResponse } from './lien-task-history.types';

interface RawAuditActor { id?: string | null; name?: string | null; type?: string | null; }
interface RawAuditItem {
  auditId:       string;
  eventType:     string;
  action:        string;
  description:   string;
  occurredAtUtc: string;
  actor?:        RawAuditActor | null;
  before?:       string | null;
  after?:        string | null;
  metadata?:     string | null;
}
interface RawAuditQueryResponse { items: RawAuditItem[]; totalCount: number; page: number; pageSize: number; }

// The audit service wraps all responses in { success, data: <payload>, ... }
interface AuditEnvelope { success: boolean; data: RawAuditQueryResponse; message?: string | null; }

export const lienTaskHistoryService = {
  async getHistory(taskId: string, pageSize = 100): Promise<TaskHistoryResponse> {
    // Gateway route: /audit-service/audit/{**} → strips /audit-service → audit service receives /audit/{**}
    const { data: envelope } = await apiClient.get<AuditEnvelope>(
      `/audit-service/audit/entity/LienTask/${encodeURIComponent(taskId)}?pageSize=${pageSize}&sortOrder=asc`,
    );

    // Unwrap the audit service envelope: { success, data: { items, totalCount, ... } }
    const payload = envelope?.data ?? (envelope as unknown as RawAuditQueryResponse);

    const events: TaskHistoryEvent[] = (payload?.items ?? []).map((e) => ({
      auditId:       e.auditId,
      eventType:     e.eventType,
      action:        e.action,
      description:   e.description,
      occurredAtUtc: e.occurredAtUtc,
      actor:         e.actor ?? null,
      before:        e.before,
      after:         e.after,
      metadata:      e.metadata,
    }));

    return {
      items:      events,
      totalCount: payload?.totalCount ?? 0,
      page:       payload?.page       ?? 1,
      pageSize:   payload?.pageSize   ?? pageSize,
    };
  },
};
