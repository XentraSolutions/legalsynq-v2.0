import { useEffect } from 'react';

import { DeepLinkNavigationService } from '@/navigation/DeepLinkNavigation';

export function DeepLinkNavigationIntegration() {
  useEffect(() => {
    DeepLinkNavigationService.start();
    return DeepLinkNavigationService.stop;
  }, []);

  return null;
}
