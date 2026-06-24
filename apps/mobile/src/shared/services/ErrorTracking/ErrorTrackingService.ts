import { LoggerService } from '@/shared/services/Logger';

export const ErrorTrackingService = {
  captureException(error: Error, context?: Record<string, unknown>): void {
    LoggerService.error('Captured exception', error, context);
  },
};
