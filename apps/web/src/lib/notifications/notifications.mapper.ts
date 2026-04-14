import type {
  NotifSummaryDto,
  NotifStatsDto,
  NotificationItem,
  NotificationStats,
} from './notifications.types';

function formatTimestamp(val: string): string {
  if (!val) return '';
  try {
    const d = new Date(val);
    if (isNaN(d.getTime())) return val;
    return d.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return val;
  }
}

function parseRecipient(json: string): string {
  try {
    const r = JSON.parse(json) as Record<string, string>;
    return r.email ?? r.phone ?? r.address ?? '—';
  } catch {
    return '—';
  }
}

function parseMetadata(json: string | null): { templateKey: string | null; subject: string | null } {
  if (!json) return { templateKey: null, subject: null };
  try {
    const m = JSON.parse(json) as Record<string, unknown>;
    return {
      templateKey: (m.templateKey as string) ?? (m.template as string) ?? null,
      subject: (m.subject as string) ?? null,
    };
  } catch {
    return { templateKey: null, subject: null };
  }
}

export function mapNotificationItem(dto: NotifSummaryDto): NotificationItem {
  const meta = parseMetadata(dto.metadataJson);
  const statusLower = dto.status.toLowerCase();
  return {
    id: dto.id,
    channel: dto.channel,
    status: dto.status,
    recipient: parseRecipient(dto.recipientJson),
    provider: dto.providerUsed,
    errorMessage: dto.lastErrorMessage,
    templateKey: meta.templateKey,
    subject: meta.subject,
    timestamp: formatTimestamp(dto.createdAt),
    timestampRaw: dto.createdAt,
    isFailed: statusLower === 'failed',
    isBlocked: statusLower === 'blocked',
  };
}

export function mapNotificationStats(dto: NotifStatsDto): NotificationStats {
  const sent = dto.byStatus['sent'] ?? 0;
  const failed = dto.byStatus['failed'] ?? 0;
  const blocked = dto.byStatus['blocked'] ?? 0;
  return {
    total: dto.total,
    sent,
    failed,
    blocked,
    last24hTotal: dto.last24h.total,
    last24hSent: dto.last24h.sent,
    last24hFailed: dto.last24h.failed,
    deliveryRate: dto.total > 0 ? Math.round((sent / dto.total) * 100) : null,
  };
}
