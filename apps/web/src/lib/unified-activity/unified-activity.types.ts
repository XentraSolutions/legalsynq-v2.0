export type ActivitySource = 'audit' | 'notification';

export interface ActivityEntityRef {
  type: string;
  id: string;
}

export interface ActivityActorRef {
  name: string;
  type: string;
}

export interface UnifiedActivityItem {
  id: string;
  source: ActivitySource;
  title: string;
  description: string;
  actor: ActivityActorRef | null;
  entity: ActivityEntityRef | null;
  timestamp: string;
  timestampRaw: string;
  icon: string;
  iconColor: string;
  severity: string;
  sourceDetail: AuditSourceDetail | NotificationSourceDetail;
}

export interface AuditSourceDetail {
  kind: 'audit';
  eventType: string;
  eventCategory: string;
  action: string;
  entityType: string;
  entityId: string;
  severity: string;
}

export interface NotificationSourceDetail {
  kind: 'notification';
  channel: string;
  status: string;
  recipient: string;
  templateKey: string | null;
  subject: string | null;
  isFailed: boolean;
  isBlocked: boolean;
  errorMessage: string | null;
}

export type UnifiedSourceDetail = AuditSourceDetail | NotificationSourceDetail;

export interface UnifiedActivityQuery {
  source?: ActivitySource;
  limit?: number;
  page?: number;
}

export interface UnifiedActivityResult {
  items: UnifiedActivityItem[];
  hasMore: boolean;
}
