import type { DeepLinkResolution, ResolvedDeepLink } from './DeepLinkTypes';

export type DeepLinkAuthStatus = 'hydrating' | 'authenticated' | 'unauthenticated';

export interface DeepLinkAuthSnapshot {
  status: DeepLinkAuthStatus;
  identityKey: string | null;
  sessionVersion: number;
}

export type ReadyDeepLinkListener = (intent: ResolvedDeepLink) => void;

const INITIAL_AUTH_STATE: DeepLinkAuthSnapshot = {
  status: 'hydrating',
  identityKey: null,
  sessionVersion: 0,
};

export class DeepLinkAuthCoordinator {
  private authState: DeepLinkAuthSnapshot = INITIAL_AUTH_STATE;
  private pendingIntent: ResolvedDeepLink | null = null;

  constructor(private readonly onReadyIntent: ReadyDeepLinkListener) {}

  processResolution(resolution: DeepLinkResolution): void {
    if (resolution.status !== 'resolved') {
      return;
    }

    if (this.authState.status === 'authenticated') {
      this.onReadyIntent(resolution);
      return;
    }

    this.pendingIntent = resolution;
  }

  updateAuthState(nextState: DeepLinkAuthSnapshot): void {
    const previousState = this.authState;
    const sessionChanged = nextState.sessionVersion !== previousState.sessionVersion;
    const authenticatedIdentityChanged =
      previousState.status === 'authenticated' &&
      nextState.status === 'authenticated' &&
      previousState.identityKey !== nextState.identityKey;

    if (sessionChanged || authenticatedIdentityChanged) {
      this.pendingIntent = null;
    }

    this.authState = nextState;

    if (nextState.status !== 'authenticated' || !this.pendingIntent) {
      return;
    }

    const readyIntent = this.pendingIntent;
    this.pendingIntent = null;
    this.onReadyIntent(readyIntent);
  }

  clearPending(): void {
    this.pendingIntent = null;
  }

  getPendingIntent(): ResolvedDeepLink | null {
    return this.pendingIntent;
  }
}
