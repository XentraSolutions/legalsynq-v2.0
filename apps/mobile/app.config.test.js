const {
  createNativeDeepLinkConfig,
  deriveAndroidAssociationClaims,
  PHASE_ONE_ASSOCIATION_PATH,
  resolveDeepLinkHost,
} = require('./app.config.helpers');

function loadExpoConfig(environment) {
  const originalAppEnvironment = process.env.EXPO_PUBLIC_APP_ENV;
  const originalDeepLinkHost = process.env.EXPO_PUBLIC_DEEP_LINK_HOST;

  if (environment.EXPO_PUBLIC_APP_ENV === undefined) {
    delete process.env.EXPO_PUBLIC_APP_ENV;
  } else {
    process.env.EXPO_PUBLIC_APP_ENV = environment.EXPO_PUBLIC_APP_ENV;
  }

  if (environment.EXPO_PUBLIC_DEEP_LINK_HOST === undefined) {
    delete process.env.EXPO_PUBLIC_DEEP_LINK_HOST;
  } else {
    process.env.EXPO_PUBLIC_DEEP_LINK_HOST = environment.EXPO_PUBLIC_DEEP_LINK_HOST;
  }

  jest.resetModules();

  try {
    return require('./app.config').expo;
  } finally {
    if (originalAppEnvironment === undefined) {
      delete process.env.EXPO_PUBLIC_APP_ENV;
    } else {
      process.env.EXPO_PUBLIC_APP_ENV = originalAppEnvironment;
    }

    if (originalDeepLinkHost === undefined) {
      delete process.env.EXPO_PUBLIC_DEEP_LINK_HOST;
    } else {
      process.env.EXPO_PUBLIC_DEEP_LINK_HOST = originalDeepLinkHost;
    }

    jest.resetModules();
  }
}

describe('Mobile verified-link Expo configuration', () => {
  it('uses a distinct exact-root association scope', () => {
    expect(PHASE_ONE_ASSOCIATION_PATH).toBe('/');
    expect(deriveAndroidAssociationClaims()).toEqual([{ path: '/' }]);
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
        data: [{ scheme: 'https', host: 'links.qa.example.test', path: '/' }],
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

  it('wires helper output into platform-correct exported Expo configuration', () => {
    const expoConfig = loadExpoConfig({
      EXPO_PUBLIC_APP_ENV: 'qa',
      EXPO_PUBLIC_DEEP_LINK_HOST: 'links.integration.example.test',
    });

    expect(expoConfig.ios.bundleIdentifier).toBe('com.legalsynq.qa');
    expect(expoConfig.ios.associatedDomains).toEqual(['applinks:links.integration.example.test']);
    expect(expoConfig.ios.associatedDomains).not.toEqual(
      expect.arrayContaining([expect.any(Object)])
    );

    expect(expoConfig.android.package).toBe('com.legalsynq.qa');
    const expectedAndroidData = deriveAndroidAssociationClaims().map((claim) => ({
      scheme: 'https',
      host: 'links.integration.example.test',
      ...claim,
    }));
    expect(expoConfig.android.intentFilters).toEqual([
      {
        action: 'VIEW',
        autoVerify: true,
        category: ['BROWSABLE', 'DEFAULT'],
        data: expectedAndroidData,
      },
    ]);
    expect(expoConfig.android.intentFilters).not.toEqual(
      expect.arrayContaining([expect.stringMatching(/^applinks:/)])
    );
    expect(JSON.stringify(expoConfig.android.intentFilters)).not.toMatch(
      /\/dashboard|\/contacts|\/applications|\/deals|\/reports/
    );
  });

  it('preserves Production identities in exported Expo configuration', () => {
    const expoConfig = loadExpoConfig({
      EXPO_PUBLIC_APP_ENV: 'production',
      EXPO_PUBLIC_DEEP_LINK_HOST: 'links.production.example.test',
    });

    expect(expoConfig.ios.bundleIdentifier).toBe('com.legalsynq');
    expect(expoConfig.android.package).toBe('com.legalsynq');
    expect(JSON.stringify(expoConfig)).not.toContain('links.integration.example.test');
  });

  it('safely omits native claims in exported Development config without a host', () => {
    const expoConfig = loadExpoConfig({ EXPO_PUBLIC_APP_ENV: 'development' });

    expect(expoConfig.ios.bundleIdentifier).toBe('com.legalsynq.qa');
    expect(expoConfig.ios.associatedDomains).toEqual([]);
    expect(expoConfig.android.package).toBe('com.legalsynq.qa');
    expect(expoConfig.android.intentFilters).toEqual([]);
  });

  it('fails exported Production config without a host instead of falling back', () => {
    expect(() => loadExpoConfig({ EXPO_PUBLIC_APP_ENV: 'production' })).toThrow(
      'EXPO_PUBLIC_DEEP_LINK_HOST is required for production Mobile builds.'
    );
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
