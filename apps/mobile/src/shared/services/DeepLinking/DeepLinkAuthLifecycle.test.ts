import { DeepLinkAuthCoordinator } from './DeepLinkAuthCoordinator';
import { DeepLinkAuthLifecycle } from './DeepLinkAuthLifecycle';
import type { DeepLinkResolutionListener, ResolvedDeepLink } from './DeepLinkTypes';

const intent: ResolvedDeepLink = {
  status: 'resolved',
  routeKey: 'dashboard',
  pathParameters: {},
  queryParameters: {},
  originalUrl: 'https://links.example.test/dashboard',
  normalizedUrl: 'https://links.example.test/dashboard',
};

function createHarness() {
  let runtimeListener: DeepLinkResolutionListener | null = null;
  let initialListener: DeepLinkResolutionListener | null = null;
  const cleanup = jest.fn();
  const intake = {
    processInitialUrl: jest.fn(async (listener: DeepLinkResolutionListener) => {
      initialListener = listener;
      return null;
    }),
    subscribe: jest.fn((listener: DeepLinkResolutionListener) => {
      runtimeListener = listener;
      return cleanup;
    }),
  };
  const onReady = jest.fn();
  const coordinator = new DeepLinkAuthCoordinator(onReady);
  const lifecycle = new DeepLinkAuthLifecycle({ coordinator, intake });

  return {
    cleanup,
    coordinator,
    getInitialListener: () => initialListener,
    getRuntimeListener: () => runtimeListener,
    intake,
    lifecycle,
    onReady,
  };
}

describe('DeepLinkAuthLifecycle', () => {
  it('starts cold and runtime intake once and cleans up once', () => {
    const harness = createHarness();

    harness.lifecycle.start();
    harness.lifecycle.start();
    harness.lifecycle.stop();
    harness.lifecycle.stop();

    expect(harness.intake.subscribe).toHaveBeenCalledTimes(1);
    expect(harness.intake.processInitialUrl).toHaveBeenCalledTimes(1);
    expect(harness.cleanup).toHaveBeenCalledTimes(1);
  });

  it('holds a cold-start intent until hydration resolves authenticated', () => {
    const harness = createHarness();
    harness.lifecycle.start();
    harness.getInitialListener()?.(intent);

    expect(harness.onReady).not.toHaveBeenCalled();
    harness.coordinator.updateAuthState({
      status: 'authenticated',
      identityKey: 'tenant-1:user-1',
      sessionVersion: 0,
    });

    expect(harness.onReady).toHaveBeenCalledWith(intent);
  });

  it('delivers a cold-start intent immediately when auth is already authenticated', () => {
    const harness = createHarness();
    harness.coordinator.updateAuthState({
      status: 'authenticated',
      identityKey: 'tenant-1:user-1',
      sessionVersion: 0,
    });
    harness.lifecycle.start();

    harness.getInitialListener()?.(intent);

    expect(harness.onReady).toHaveBeenCalledWith(intent);
    expect(harness.coordinator.getPendingIntent()).toBeNull();
  });

  it('keeps a cold-start intent pending when auth is already unauthenticated', () => {
    const harness = createHarness();
    harness.coordinator.updateAuthState({
      status: 'unauthenticated',
      identityKey: null,
      sessionVersion: 0,
    });
    harness.lifecycle.start();

    harness.getInitialListener()?.(intent);

    expect(harness.onReady).not.toHaveBeenCalled();
    expect(harness.coordinator.getPendingIntent()).toBe(intent);
  });

  it('handles runtime intents for authenticated, unauthenticated, and hydrating auth', () => {
    const harness = createHarness();
    harness.lifecycle.start();
    const listener = harness.getRuntimeListener();

    harness.coordinator.updateAuthState({
      status: 'authenticated',
      identityKey: 'tenant-1:user-1',
      sessionVersion: 0,
    });
    listener?.(intent);
    expect(harness.onReady).toHaveBeenCalledTimes(1);

    harness.coordinator.updateAuthState({
      status: 'unauthenticated',
      identityKey: null,
      sessionVersion: 1,
    });
    listener?.(intent);
    expect(harness.coordinator.getPendingIntent()).toBe(intent);

    harness.coordinator.updateAuthState({
      status: 'hydrating',
      identityKey: null,
      sessionVersion: 1,
    });
    listener?.(intent);
    expect(harness.onReady).toHaveBeenCalledTimes(1);
  });

  it('ignores late cold/runtime delivery after cleanup and can restart cleanly', () => {
    const harness = createHarness();
    harness.lifecycle.start();
    const firstInitialListener = harness.getInitialListener();
    const firstRuntimeListener = harness.getRuntimeListener();
    harness.lifecycle.stop();

    firstInitialListener?.(intent);
    firstRuntimeListener?.(intent);
    expect(harness.coordinator.getPendingIntent()).toBeNull();

    harness.lifecycle.start();
    expect(harness.intake.subscribe).toHaveBeenCalledTimes(2);
  });

  it('contains an initial URL adapter rejection without affecting cleanup', async () => {
    const harness = createHarness();
    harness.intake.processInitialUrl.mockRejectedValueOnce(new Error('Linking unavailable'));

    harness.lifecycle.start();
    await Promise.resolve();
    harness.lifecycle.stop();

    expect(harness.cleanup).toHaveBeenCalledTimes(1);
    expect(harness.onReady).not.toHaveBeenCalled();
  });
});
