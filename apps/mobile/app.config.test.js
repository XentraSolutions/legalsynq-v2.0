const {
  createNativeDeepLinkConfig,
  deriveAndroidRouteClaims,
  resolveDeepLinkHost,
} = require('./app.config.helpers');

describe('Mobile verified-link Expo configuration', () => {
  it('derives narrow Android route claims from the shared registry', () => {
    expect(deriveAndroidRouteClaims()).toEqual([
      { path: '/dashboard' },
      { pathPrefix: '/deals/' },
      { pathPrefix: '/contacts/' },
      { pathPrefix: '/applications/' },
      { pathPrefix: '/reports/' },
    ]);
  });

  it.each(['development', 'qa'])(
    'omits native verified-link claims when %s has no approved host',
    (appEnvironment) => {
      expect(createNativeDeepLinkConfig({ EXPO_PUBLIC_APP_ENV: appEnvironment })).toEqual({
        androidIntentFilters: [],
        iosAssociatedDomains: [],
      });
    }
  );

  it('creates isolated QA iOS and Android claims for an injected test host', () => {
    const config = createNativeDeepLinkConfig({
      EXPO_PUBLIC_APP_ENV: 'qa',
      EXPO_PUBLIC_DEEP_LINK_HOST: 'links.qa.example.test',
    });

    expect(config.iosAssociatedDomains).toEqual(['applinks:links.qa.example.test']);
    expect(config.androidIntentFilters).toEqual([
      expect.objectContaining({
        action: 'VIEW',
        autoVerify: true,
        category: ['BROWSABLE', 'DEFAULT'],
        data: expect.arrayContaining([
          {
            scheme: 'https',
            host: 'links.qa.example.test',
            path: '/dashboard',
          },
          {
            scheme: 'https',
            host: 'links.qa.example.test',
            pathPrefix: '/deals/',
          },
        ]),
      }),
    ]);
    expect(JSON.stringify(config)).not.toContain('links.example.test');
  });

  it('creates isolated Production claims for an injected HTTPS-compatible host', () => {
    const config = createNativeDeepLinkConfig({
      EXPO_PUBLIC_APP_ENV: 'production',
      EXPO_PUBLIC_DEEP_LINK_HOST: 'links.example.test',
    });

    expect(config.iosAssociatedDomains).toEqual(['applinks:links.example.test']);
    expect(config.androidIntentFilters[0]).toMatchObject({
      autoVerify: true,
    });
    expect(JSON.stringify(config)).not.toContain('links.qa.example.test');
  });

  it('fails Production config resolution when the host is missing', () => {
    expect(() => resolveDeepLinkHost({ EXPO_PUBLIC_APP_ENV: 'production' })).toThrow(
      'EXPO_PUBLIC_DEEP_LINK_HOST is required'
    );
  });

  it.each([
    'https://links.example.test',
    'links.example.test/path',
    'links.example.test:443',
    'localhost',
    'bad host.example.test',
  ])('rejects an invalid verified-link host: %s', (host) => {
    expect(() =>
      resolveDeepLinkHost({
        EXPO_PUBLIC_APP_ENV: 'production',
        EXPO_PUBLIC_DEEP_LINK_HOST: host,
      })
    ).toThrow('must be a DNS hostname');
  });
});
