import type { ResolvedDeepLink } from './DeepLinkTypes';
import { ReadyDeepLinkService } from './ReadyDeepLinkService';

const intent: ResolvedDeepLink = {
  status: 'resolved',
  routeKey: 'dashboard',
  pathParameters: {},
  queryParameters: {},
  originalUrl: 'https://links.example.test/dashboard',
  normalizedUrl: 'https://links.example.test/dashboard',
};

describe('ReadyDeepLinkService', () => {
  it('emits ready intents to current listeners and supports cleanup', () => {
    const listener = jest.fn();
    const unsubscribe = ReadyDeepLinkService.subscribe(listener);

    ReadyDeepLinkService.emit(intent);
    unsubscribe();
    ReadyDeepLinkService.emit(intent);

    expect(listener).toHaveBeenCalledTimes(1);
    expect(listener).toHaveBeenCalledWith(intent);
  });
});
