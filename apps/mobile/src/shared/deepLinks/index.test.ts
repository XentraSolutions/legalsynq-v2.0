import { deepLinkRoutes, getDeepLinkRoute } from './index';

describe('mobile shared deep-link contract', () => {
  it('consumes the authoritative route registry without adding link handling', () => {
    expect(deepLinkRoutes).toHaveLength(5);
    expect(getDeepLinkRoute('dealDetails')).toMatchObject({
      key: 'dealDetails',
      pathTemplate: '/deals/:dealId',
      requiredPathParameters: ['dealId'],
    });
  });
});
