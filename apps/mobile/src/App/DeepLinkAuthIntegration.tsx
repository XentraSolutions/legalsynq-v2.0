import { useEffect, useRef } from 'react';
import { useAtomValue } from 'jotai';

import {
  DeepLinkAuthCoordinator,
  DeepLinkAuthLifecycle,
  DeepLinkIntakeService,
  ReadyDeepLinkService,
} from '@/shared/services/DeepLinking';
import { authAtom } from '@/shared/state/atoms/authAtom';

export function DeepLinkAuthIntegration() {
  const auth = useAtomValue(authAtom);
  const coordinatorRef = useRef<DeepLinkAuthCoordinator | null>(null);
  const lifecycleRef = useRef<DeepLinkAuthLifecycle | null>(null);

  if (!coordinatorRef.current) {
    coordinatorRef.current = new DeepLinkAuthCoordinator(ReadyDeepLinkService.emit);
    lifecycleRef.current = new DeepLinkAuthLifecycle({
      coordinator: coordinatorRef.current,
      intake: DeepLinkIntakeService.createConfigured(),
    });
  }

  useEffect(() => {
    coordinatorRef.current?.updateAuthState({
      status: auth.status,
      identityKey: auth.user ? `${auth.user.tenantId}:${auth.user.id}` : null,
      sessionVersion: auth.sessionVersion,
    });
  }, [auth.sessionVersion, auth.status, auth.user]);

  useEffect(() => {
    const lifecycle = lifecycleRef.current;
    lifecycle?.start();
    return () => lifecycle?.stop();
  }, []);

  return null;
}
