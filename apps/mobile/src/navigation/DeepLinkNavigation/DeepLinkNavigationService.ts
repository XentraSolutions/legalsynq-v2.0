import { rootNavigationRef } from '@/navigation/RootNavigator/navigationRef';
import { ReadyDeepLinkService } from '@/shared/services/DeepLinking';
import { LoggerService } from '@/shared/services/Logger';

import { DeepLinkNavigationCoordinator } from './DeepLinkNavigationCoordinator';

const coordinator = new DeepLinkNavigationCoordinator({
  navigation: {
    isReady: () => rootNavigationRef.isReady(),
    navigate: (target) => rootNavigationRef.navigate(target.name, target.params),
  },
  readyIntentSource: ReadyDeepLinkService,
  onResult: (result) => {
    if (
      result.status === 'destination_unavailable' ||
      result.status === 'invalid_parameters' ||
      result.status === 'navigation_failed' ||
      result.status === 'unsupported_route'
    ) {
      LoggerService.warn('Deep-link navigation was not dispatched.', {
        reason: result.reason,
        routeKey: result.routeKey,
        status: result.status,
      });
    }
  },
});

export const DeepLinkNavigationService = {
  start: () => coordinator.start(),
  stop: () => coordinator.stop(),
  onNavigationReady: () => coordinator.onNavigationReady(),
};
