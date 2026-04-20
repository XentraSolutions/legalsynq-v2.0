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

export const lienTaskHistoryService = {
  async getHistory(taskId: string, pageSize = 100): Promise<TaskHistoryResponse> {
    const { data } = await apiClient.get<RawAuditQueryResponse>(
      `/audit/audit/entity/LienTask/${encodeURIComponent(taskId)}?pageSize=${pageSize}&sortOrder=asc`,
    );

    const events: TaskHistoryEvent[] = (data?.items ?? []).map((e) => ({
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
      totalCount: data?.totalCount ?? 0,
      page:       data?.page       ?? 1,
      pageSize:   data?.pageSize   ?? pageSize,
    };
  },
};
