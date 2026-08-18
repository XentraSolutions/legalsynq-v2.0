import type { ResolvedDeepLink } from '@/shared/services/DeepLinking';

import {
  DeepLinkNavigationCoordinator,
  type DeepLinkNavigationAdapter,
  type ReadyDeepLinkSource,
} from './DeepLinkNavigationCoordinator';

function intent(routeKey: string, pathParameters: Record<string, string> = {}): ResolvedDeepLink {
  return {
    status: 'resolved',
    routeKey,
    pathParameters,
    queryParameters: {},
    originalUrl: `https://links.example.test/${routeKey}`,
    normalizedUrl: `https://links.example.test/${routeKey}`,
  };
}

function createHarness(initiallyReady = false) {
  let ready = initiallyReady;
  let listener: ((value: ResolvedDeepLink) => void) | null = null;
  const unsubscribe = jest.fn(() => {
    listener = null;
  });
  const source: ReadyDeepLinkSource = {
    subscribe: jest.fn((nextListener: (value: ResolvedDeepLink) => void) => {
      listener = nextListener;
      return unsubscribe;
    }),
  };
  const navigate = jest.fn();
  const navigation: DeepLinkNavigationAdapter = {
    isReady: jest.fn(() => ready),
    navigate,
  };
  const onResult = jest.fn();
  const coordinator = new DeepLinkNavigationCoordinator({
    navigation,
    readyIntentSource: source,
    onResult,
  });

  return {
    coordinator,
    emit: (value: ResolvedDeepLink) => listener?.(value),
    navigate,
    navigation,
    onResult,
    setReady: (value: boolean) => {
      ready = value;
    },
    source,
    unsubscribe,
  };
}

describe('DeepLinkNavigationCoordinator', () => {
  it('dispatches immediately when navigation is ready', () => {
    const harness = createHarness(true);
    harness.coordinator.start();

    harness.emit(intent('contactDetails', { contactId: 'contact-1' }));

    expect(harness.navigation.navigate).toHaveBeenCalledWith({
      name: 'Main',
      params: { screen: 'ContactDetail', params: { contactId: 'contact-1' } },
    });
    expect(harness.onResult).toHaveBeenCalledWith({
      status: 'navigated',
      routeKey: 'contactDetails',
    });
  });

  it('queues before readiness, flushes once, and clears before dispatch', () => {
    const harness = createHarness();
    harness.coordinator.start();
    harness.emit(intent('dashboard'));
    expect(harness.navigation.navigate).not.toHaveBeenCalled();

    harness.setReady(true);
    const firstResult = harness.coordinator.onNavigationReady();
    const secondResult = harness.coordinator.onNavigationReady();

    expect(firstResult).toEqual({ status: 'navigated', routeKey: 'dashboard' });
    expect(secondResult).toBeNull();
    expect(harness.coordinator.getPendingIntent()).toBeNull();
    expect(harness.navigation.navigate).toHaveBeenCalledTimes(1);
  });

  it('uses latest-wins for two mapped intents before readiness', () => {
    const harness = createHarness();
    harness.coordinator.start();
    harness.emit(intent('dashboard'));
    harness.emit(intent('contactDetails', { contactId: 'contact-2' }));

    harness.setReady(true);
    harness.coordinator.onNavigationReady();

    expect(harness.navigation.navigate).toHaveBeenCalledTimes(1);
    expect(harness.navigation.navigate).toHaveBeenCalledWith({
      name: 'Main',
      params: { screen: 'ContactDetail', params: { contactId: 'contact-2' } },
    });
  });

  it('replaces a queued intent if readiness becomes true before the callback', () => {
    const harness = createHarness();
    harness.coordinator.start();
    harness.emit(intent('dashboard'));
    harness.setReady(true);

    harness.emit(intent('contactDetails', { contactId: 'contact-3' }));
    harness.coordinator.onNavigationReady();

    expect(harness.navigation.navigate).toHaveBeenCalledTimes(1);
    expect(harness.coordinator.getPendingIntent()).toBeNull();
  });

  it('subscribes once, cleans up once, and does not receive events after cleanup', () => {
    const harness = createHarness(true);
    harness.coordinator.start();
    harness.coordinator.start();
    harness.coordinator.stop();
    harness.coordinator.stop();

    expect(harness.source.subscribe).toHaveBeenCalledTimes(1);
    expect(harness.unsubscribe).toHaveBeenCalledTimes(1);
    harness.emit(intent('dashboard'));
    expect(harness.navigate).not.toHaveBeenCalled();
  });

  it('retains pending state across a lifecycle remount without duplicate dispatch', () => {
    const harness = createHarness();
    harness.coordinator.start();
    harness.emit(intent('dashboard'));
    harness.coordinator.stop();
    harness.coordinator.start();
    harness.setReady(true);

    harness.coordinator.onNavigationReady();
    harness.coordinator.onNavigationReady();

    expect(harness.navigation.navigate).toHaveBeenCalledTimes(1);
  });

  it('returns controlled mapping failures and never calls navigation', () => {
    const harness = createHarness(true);

    const missing = harness.coordinator.processIntent(intent('contactDetails'));
    const unknown = harness.coordinator.processIntent(intent('futureRoute'));
    const unavailable = harness.coordinator.processIntent(
      intent('dealDetails', { dealId: 'deal-1' })
    );

    expect(missing.status).toBe('invalid_parameters');
    expect(unknown.status).toBe('unsupported_route');
    expect(unavailable.status).toBe('destination_unavailable');
    expect(harness.navigation.navigate).not.toHaveBeenCalled();
  });

  it('contains a navigation dispatch exception and clears pending state', () => {
    const harness = createHarness();
    harness.navigate.mockImplementation(() => {
      throw new Error('container unavailable');
    });
    harness.coordinator.start();
    harness.emit(intent('dashboard'));
    harness.setReady(true);

    const result = harness.coordinator.onNavigationReady();

    expect(result).toEqual({
      status: 'navigation_failed',
      routeKey: 'dashboard',
      reason: 'container unavailable',
    });
    expect(harness.coordinator.getPendingIntent()).toBeNull();
  });
});
