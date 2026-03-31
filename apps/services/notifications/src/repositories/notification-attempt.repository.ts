import { NotificationAttempt, AttemptStatus } from "../models/notification-attempt.model";
import { FailureCategory } from "../types";

interface CreateAttemptInput {
  tenantId: string;
  notificationId: string;
  attemptNumber: number;
  provider: string;
  failoverTriggered?: boolean;
  providerOwnershipMode?: string | null;
  providerConfigId?: string | null;
  platformFallbackUsed?: boolean;
}

interface CompleteAttemptInput {
  status: AttemptStatus;
  providerMessageId?: string | null;
  failureCategory?: FailureCategory | null;
  errorMessage?: string | null;
}

export class NotificationAttemptRepository {
  async findByNotificationId(notificationId: string): Promise<NotificationAttempt[]> {
    return NotificationAttempt.findAll({
      where: { notificationId },
      order: [["attemptNumber", "ASC"]],
    });
  }

  async countByNotificationId(notificationId: string): Promise<number> {
    return NotificationAttempt.count({ where: { notificationId } });
  }

  async create(input: CreateAttemptInput): Promise<NotificationAttempt> {
    return NotificationAttempt.create({
      ...input,
      status: "created",
      failoverTriggered: input.failoverTriggered ?? false,
      startedAt: new Date(),
    });
  }

  async complete(id: string, input: CompleteAttemptInput): Promise<void> {
    await NotificationAttempt.update(
      {
        status: input.status,
        providerMessageId: input.providerMessageId ?? null,
        failureCategory: input.failureCategory ?? null,
        errorMessage: input.errorMessage ?? null,
        completedAt: new Date(),
      },
      { where: { id } }
    );
  }
}
