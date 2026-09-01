import { DeepLinkDuplicateGuard } from './DeepLinkDuplicateGuard';
import { type DeepLinkPlatformAdapter, DeepLinkIntakeService } from './DeepLinkIntakeService';
import { DeepLinkResolver } from './DeepLinkResolver';

function createHarness(initialUrl: string | null = null) {
  let runtimeListener: ((url: string) => void) | null = null;
  const remove = jest.fn();
  const adapter: DeepLinkPlatformAdapter = {
    getInitialUrl: jest.fn(async () => initialUrl),
    subscribeToUrls: jest.fn((listener) => {
      runtimeListener = listener;
      return remove;
    }),
  };
  let now = 1_000;
  const intake = new DeepLinkIntakeService({
    resolver: new DeepLinkResolver({ expectedHttpsHost: 'links.qa.example.test' }),
    duplicateGuard: new DeepLinkDuplicateGuard({
      windowMs: 2_000,
      now: () => now,
    }),
    platformAdapter: adapter,
  });

  return {
    adapter,
    intake,
    remove,
    emit(url: string) {
      if (!runtimeListener) {
        throw new Error('Runtime listener is not registered.');
      }
      runtimeListener(url);
    },
    advance(milliseconds: number) {
      now += milliseconds;
    },
  };
}

describe('DeepLinkIntakeService', () => {
  it('safely ignores an absent initial URL', async () => {
    const { intake } = createHarness();
    const listener = jest.fn();

    await expect(intake.processInitialUrl(listener)).resolves.toBeNull();
    expect(listener).not.toHaveBeenCalled();
  });

  it('resolves and delivers a cold-start URL without navigation', async () => {
    const { intake } = createHarness('https://links.qa.example.test/deals/123');
    const listener = jest.fn();

    await expect(intake.processInitialUrl(listener)).resolves.toMatchObject({
      status: 'resolved',
      routeKey: 'dealDetails',
    });
    expect(listener.mock.calls[0][0]).toMatchObject({
      status: 'resolved',
      routeKey: 'dealDetails',
    });
  });

  it('delivers a stable failure for an invalid cold-start URL', async () => {
    const { intake } = createHarness('not a url');
    const listener = jest.fn();

    await expect(intake.processInitialUrl(listener)).resolves.toMatchObject({
      status: 'malformed',
    });
    expect(listener.mock.calls[0][0]).toMatchObject({ status: 'malformed' });
  });

  it('subscribes to multiple runtime events and cleans up', () => {
    const { adapter, emit, intake, remove } = createHarness();
    const listener = jest.fn();

    const unsubscribe = intake.subscribe(listener);
    emit('https://links.qa.example.test/dashboard');
    emit('https://links.qa.example.test/reports/r1');
    unsubscribe();

    expect(adapter.subscribeToUrls).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledTimes(2);
    expect(remove).toHaveBeenCalledTimes(1);
  });

  it('suppresses the same normalized URL across cold-start and runtime intake', async () => {
    const { emit, intake } = createHarness('https://LINKS.QA.EXAMPLE.TEST/deals/A%20B');
    const listener = jest.fn();
    intake.subscribe(listener);

    await intake.processInitialUrl(listener);
    emit('https://links.qa.example.test/deals/A%20B');

    expect(listener).toHaveBeenCalledTimes(1);
    expect(intake.processUrl('https://links.qa.example.test/deals/A%20B')).toMatchObject({
      status: 'duplicate',
    });
  });

  it('allows different URLs and the same URL after the duplicate window', () => {
    const { advance, intake } = createHarness();
    const firstUrl = 'https://links.qa.example.test/deals/123';

    expect(intake.processUrl(firstUrl)).toMatchObject({ status: 'resolved' });
    expect(intake.processUrl('https://links.qa.example.test/deals/456')).toMatchObject({
      status: 'resolved',
    });
    advance(2_001);
    expect(intake.processUrl(firstUrl)).toMatchObject({ status: 'resolved' });
  });
});
