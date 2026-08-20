import { ConfigService } from './ConfigService';

describe('ConfigService environment', () => {
  const originalEnvironment = process.env.EXPO_PUBLIC_APP_ENV;
  const originalDeepLinkHost = process.env.EXPO_PUBLIC_DEEP_LINK_HOST;

  afterEach(() => {
    process.env.EXPO_PUBLIC_APP_ENV = originalEnvironment;
    process.env.EXPO_PUBLIC_DEEP_LINK_HOST = originalDeepLinkHost;
  });

  it('identifies production builds', () => {
    process.env.EXPO_PUBLIC_APP_ENV = 'production';

    expect(ConfigService.getEnvironment()).toBe('production');
    expect(ConfigService.isProduction()).toBe(true);
  });

  it('keeps the legacy API controls available outside production', () => {
    process.env.EXPO_PUBLIC_APP_ENV = 'qa';

    expect(ConfigService.isProduction()).toBe(false);
  });

  it('returns only an explicitly configured deep-link host', () => {
    process.env.EXPO_PUBLIC_DEEP_LINK_HOST = ' links.qa.example.test ';
    expect(ConfigService.getDeepLinkHost()).toBe('links.qa.example.test');

    process.env.EXPO_PUBLIC_DEEP_LINK_HOST = '';
    expect(ConfigService.getDeepLinkHost()).toBeNull();
  });
});
