import { TenantRateLimitPolicy } from "../models/tenant-rate-limit-policy.model";
import { RateLimitPolicyStatus } from "../types";

export class TenantRateLimitPolicyRepository {
  async findById(id: string): Promise<TenantRateLimitPolicy | null> {
    return TenantRateLimitPolicy.findByPk(id);
  }

  async findByIdAndTenant(id: string, tenantId: string): Promise<TenantRateLimitPolicy | null> {
    return TenantRateLimitPolicy.findOne({ where: { id, tenantId } });
  }

  async findAllByTenant(tenantId: string): Promise<TenantRateLimitPolicy[]> {
    return TenantRateLimitPolicy.findAll({ where: { tenantId }, order: [["createdAt", "DESC"]] });
  }

  async findActivePolicies(tenantId: string, channel?: string | null): Promise<TenantRateLimitPolicy[]> {
    const policies: TenantRateLimitPolicy[] = [];

    // Always load global policy (channel = null)
    const globalPolicy = await TenantRateLimitPolicy.findOne({
      where: { tenantId, status: "active", channel: null },
      order: [["createdAt", "DESC"]],
    });
    if (globalPolicy) policies.push(globalPolicy);

    // Load channel-specific policy if channel provided
    if (channel) {
      const channelPolicy = await TenantRateLimitPolicy.findOne({
        where: { tenantId, status: "active", channel },
        order: [["createdAt", "DESC"]],
      });
      if (channelPolicy) policies.push(channelPolicy);
    }

    return policies;
  }

  async create(input: {
    tenantId: string;
    channel?: string | null;
    maxRequestsPerMinute?: number | null;
    maxAttemptsPerMinute?: number | null;
    maxDailyUsage?: number | null;
    maxMonthlyUsage?: number | null;
    status?: RateLimitPolicyStatus;
  }): Promise<TenantRateLimitPolicy> {
    return TenantRateLimitPolicy.create({
      ...input,
      channel: input.channel ?? null,
      maxRequestsPerMinute: input.maxRequestsPerMinute ?? null,
      maxAttemptsPerMinute: input.maxAttemptsPerMinute ?? null,
      maxDailyUsage: input.maxDailyUsage ?? null,
      maxMonthlyUsage: input.maxMonthlyUsage ?? null,
      status: input.status ?? "active",
    });
  }

  async update(
    id: string,
    updates: Partial<{
      channel: string | null;
      maxRequestsPerMinute: number | null;
      maxAttemptsPerMinute: number | null;
      maxDailyUsage: number | null;
      maxMonthlyUsage: number | null;
      status: RateLimitPolicyStatus;
    }>
  ): Promise<void> {
    await TenantRateLimitPolicy.update(updates, { where: { id } });
  }
}
