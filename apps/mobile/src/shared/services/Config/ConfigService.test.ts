import { ConfigService } from './ConfigService';

describe('ConfigService environment', () => {
  const originalEnvironment = process.env.EXPO_PUBLIC_APP_ENV;

  afterEach(() => {
    process.env.EXPO_PUBLIC_APP_ENV = originalEnvironment;
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
});
