import { LoggerService } from '@/shared/services/Logger';

export const NotificationsService = {
  async requestPermissions(): Promise<boolean> {
    LoggerService.log('Notification permission request deferred to native integration');
    return false;
  },
};
