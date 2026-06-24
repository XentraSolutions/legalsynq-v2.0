import { LoggerService } from '@/shared/services/Logger';
import type { AnalyticsEventName } from '@/shared/constants/analyticsEvents';

export const AnalyticsService = {
  track(eventName: AnalyticsEventName, properties?: Record<string, unknown>): void {
    LoggerService.log('Analytics event', { eventName, properties });
  },
};
