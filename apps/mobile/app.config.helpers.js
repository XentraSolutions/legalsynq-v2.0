const VERIFIED_LINK_HOST_PATTERN =
  /^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/i;

function resolveMobileEnvironment(environment = process.env) {
  if (environment.EXPO_PUBLIC_APP_ENV === 'production') {
    return 'production';
  }

  if (environment.EXPO_PUBLIC_APP_ENV === 'qa') {
    return 'qa';
  }

  return 'development';
}

function resolveDeepLinkHost(environment = process.env) {
  const mobileEnvironment = resolveMobileEnvironment(environment);
  const configuredHost = environment.EXPO_PUBLIC_DEEP_LINK_HOST?.trim();

  if (!configuredHost) {
    if (mobileEnvironment === 'production') {
      throw new Error('EXPO_PUBLIC_DEEP_LINK_HOST is required for production Mobile builds.');
    }

    return null;
  }

  if (!VERIFIED_LINK_HOST_PATTERN.test(configuredHost)) {
    throw new Error(
      'EXPO_PUBLIC_DEEP_LINK_HOST must be a DNS hostname without a scheme, port, path, query, or fragment.'
    );
  }

  return configuredHost.toLowerCase();
}

const PHASE_ONE_ASSOCIATION_PATH = '/';

function deriveAndroidAssociationClaims() {
  return [{ path: PHASE_ONE_ASSOCIATION_PATH }];
}

function createNativeDeepLinkConfig(environment = process.env) {
  const host = resolveDeepLinkHost(environment);

  if (!host) {
    return {
      androidIntentFilters: [],
      iosAssociatedDomains: [],
    };
  }

  return {
    androidIntentFilters: [
      {
        action: 'VIEW',
        autoVerify: true,
        category: ['BROWSABLE', 'DEFAULT'],
        data: deriveAndroidAssociationClaims().map((claim) => ({
          scheme: 'https',
          host,
          ...claim,
        })),
      },
    ],
    iosAssociatedDomains: [`applinks:${host}`],
  };
}

module.exports = {
  createNativeDeepLinkConfig,
  deriveAndroidAssociationClaims,
  PHASE_ONE_ASSOCIATION_PATH,
  resolveDeepLinkHost,
  resolveMobileEnvironment,
};
