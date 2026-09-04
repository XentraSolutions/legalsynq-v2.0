import { DeepLinkAuthCoordinator, type DeepLinkAuthSnapshot } from './DeepLinkAuthCoordinator';
import type { DeepLinkFailure, ResolvedDeepLink } from './DeepLinkTypes';

const intentA: ResolvedDeepLink = {
  status: 'resolved',
  routeKey: 'dashboard',
  pathParameters: {},
  queryParameters: {},
  originalUrl: 'https://links.example.test/dashboard',
  normalizedUrl: 'https://links.example.test/dashboard',
};

const intentB: ResolvedDeepLink = {
  ...intentA,
  routeKey: 'dealDetails',
  pathParameters: { dealId: 'deal-2' },
  originalUrl: 'https://links.example.test/deals/deal-2',
  normalizedUrl: 'https://links.example.test/deals/deal-2',
};

const hydrating: DeepLinkAuthSnapshot = {
  status: 'hydrating',
  identityKey: null,
  sessionVersion: 0,
};
const unauthenticated: DeepLinkAuthSnapshot = {
  status: 'unauthenticated',
  identityKey: null,
  sessionVersion: 0,
};
const authenticated: DeepLinkAuthSnapshot = {
  status: 'authenticated',
  identityKey: 'tenant-1:user-1',
  sessionVersion: 0,
};

describe('DeepLinkAuthCoordinator', () => {
  it('delivers a resolved intent immediately when authenticated without storing it', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(authenticated);

    coordinator.processResolution(intentA);

    expect(onReady).toHaveBeenCalledTimes(1);
    expect(onReady).toHaveBeenCalledWith(intentA);
    expect(coordinator.getPendingIntent()).toBeNull();
  });

  it('holds during hydration and releases once when hydration resolves authenticated', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(hydrating);
    coordinator.processResolution(intentA);

    expect(onReady).not.toHaveBeenCalled();
    expect(coordinator.getPendingIntent()).toBe(intentA);

    coordinator.updateAuthState(authenticated);
    coordinator.updateAuthState(authenticated);

    expect(onReady).toHaveBeenCalledTimes(1);
    expect(coordinator.getPendingIntent()).toBeNull();
  });

  it('keeps a hydrating intent pending when hydration resolves unauthenticated', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.processResolution(intentA);

    coordinator.updateAuthState(unauthenticated);

    expect(onReady).not.toHaveBeenCalled();
    expect(coordinator.getPendingIntent()).toBe(intentA);
  });

  it('holds while unauthenticated and clears before login delivery', () => {
    const pendingObservedDuringDelivery: Array<ResolvedDeepLink | null> = [];
    let coordinator: DeepLinkAuthCoordinator;
    coordinator = new DeepLinkAuthCoordinator(() => {
      pendingObservedDuringDelivery.push(coordinator.getPendingIntent());
    });
    coordinator.updateAuthState(unauthenticated);
    coordinator.processResolution(intentA);

    coordinator.updateAuthState(authenticated);

    expect(pendingObservedDuringDelivery).toEqual([null]);
  });

  it('uses latest-wins for multiple pending resolved intents', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(unauthenticated);
    coordinator.processResolution(intentA);
    coordinator.processResolution(intentB);

    coordinator.updateAuthState(authenticated);

    expect(onReady).toHaveBeenCalledTimes(1);
    expect(onReady).toHaveBeenCalledWith(intentB);
  });

  it('clears pending on logout/session reset and prevents later replay', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(unauthenticated);
    coordinator.processResolution(intentA);

    coordinator.updateAuthState({ ...unauthenticated, sessionVersion: 1 });
    coordinator.updateAuthState({ ...authenticated, sessionVersion: 1 });

    expect(coordinator.getPendingIntent()).toBeNull();
    expect(onReady).not.toHaveBeenCalled();
  });

  it('clears pending if an authenticated account identity changes', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(authenticated);
    coordinator.updateAuthState(hydrating);
    coordinator.processResolution(intentA);
    coordinator.updateAuthState({
      ...authenticated,
      identityKey: 'tenant-2:user-2',
      sessionVersion: 1,
    });

    expect(onReady).not.toHaveBeenCalled();
  });

  it('ignores every APP-002 failure outcome', () => {
    const onReady = jest.fn();
    const coordinator = new DeepLinkAuthCoordinator(onReady);
    coordinator.updateAuthState(unauthenticated);
    const statuses: DeepLinkFailure['status'][] = [
      'malformed',
      'unsupported_scheme',
      'unsupported_host',
      'unsupported_route',
      'invalid_parameters',
      'duplicate',
    ];

    for (const status of statuses) {
      coordinator.processResolution({ status, reason: status, originalUrl: 'invalid' });
    }

    expect(coordinator.getPendingIntent()).toBeNull();
    expect(onReady).not.toHaveBeenCalled();
  });

  for (const authState of [hydrating, unauthenticated, authenticated]) {
    it(`treats a generic portal entry as a no-op in auth state ${authState.status}`, () => {
      const onReady = jest.fn();
      const coordinator = new DeepLinkAuthCoordinator(onReady);
      coordinator.updateAuthState(authState);

      coordinator.processResolution({
        status: 'portal_entry',
        originalUrl: 'https://links.example.test/',
        normalizedUrl: 'https://links.example.test/',
      });

      expect(coordinator.getPendingIntent()).toBeNull();
      expect(onReady).not.toHaveBeenCalled();
    });
  }
});
