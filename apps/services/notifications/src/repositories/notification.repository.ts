import { Op } from "sequelize";
import { Notification, NotificationStatus } from "../models/notification.model";
import { NotificationChannel, FailureCategory } from "../types";

interface CreateNotificationInput {
  tenantId: string;
  channel: NotificationChannel;
  recipientJson: string;
  messageJson: string;
  metadataJson?: string | null;
  idempotencyKey?: string | null;
  templateId?: string;
  templateVersionId?: string;
  templateKey?: string;
  renderedSubject?: string;
  renderedBody?: string;
  renderedText?: string;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
}

interface UpdateNotificationInput {
  status?: NotificationStatus;
  providerUsed?: string;
  failureCategory?: FailureCategory | null;
  lastErrorMessage?: string | null;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
  blockedByPolicy?: boolean;
  blockedReasonCode?: string | null;
  overrideUsed?: boolean;
}

interface ListNotificationsFilter {
  tenantId: string;
  channel?: NotificationChannel;
  status?: NotificationStatus;
  limit?: number;
  offset?: number;
}

export class NotificationRepository {
  async findById(id: string, tenantId?: string): Promise<Notification | null> {
    const where: Record<string, unknown> = { id };
    if (tenantId) where["tenantId"] = tenantId;
    return Notification.findOne({ where });
  }

  async findByIdempotencyKey(
    tenantId: string,
    idempotencyKey: string
  ): Promise<Notification | null> {
    return Notification.findOne({ where: { tenantId, idempotencyKey } });
  }

  async create(input: CreateNotificationInput): Promise<Notification> {
    return Notification.create({
      ...input,
      status: "accepted",
    });
  }

  async update(id: string, input: UpdateNotificationInput): Promise<void> {
    await Notification.update(input, { where: { id } });
  }

  async list(filter: ListNotificationsFilter): Promise<{ rows: Notification[]; count: number }> {
    const where: Record<string, unknown> = { tenantId: filter.tenantId };
    if (filter.channel) where["channel"] = filter.channel;
    if (filter.status) where["status"] = filter.status;

    const limit = Math.min(filter.limit ?? 20, 100);
    const offset = filter.offset ?? 0;

    const result = await Notification.findAndCountAll({
      where,
      limit,
      offset,
      order: [["createdAt", "DESC"]],
    });

    return { rows: result.rows, count: result.count };
  }

  async findByTenant(tenantId: string): Promise<Notification[]> {
    return Notification.findAll({ where: { tenantId }, order: [["createdAt", "DESC"]] });
  }
}
