import {
  deepLinkRoutes,
  generateRegisteredDeepLinkUrl,
  normalizeDeepLinkBaseUrl,
  parseDeepLinkEnvironment,
  type DeepLinkConfiguration,
  type DeepLinkGenerationInput,
} from "../../../../shared/contracts/deep-links";

export interface WebDeepLinkEnvironmentSource {
  readonly NEXT_PUBLIC_DEEP_LINK_BASE_URL?: string;
  readonly NEXT_PUBLIC_ENV?: string;
}

function currentEnvironmentSource(): WebDeepLinkEnvironmentSource {
  return {
    NEXT_PUBLIC_DEEP_LINK_BASE_URL: process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL,
    NEXT_PUBLIC_ENV: process.env.NEXT_PUBLIC_ENV,
  };
}

export function resolveWebDeepLinkConfiguration(
  source: WebDeepLinkEnvironmentSource = currentEnvironmentSource(),
): DeepLinkConfiguration {
  const environment = parseDeepLinkEnvironment(source.NEXT_PUBLIC_ENV);
  return {
    environment,
    baseUrl: normalizeDeepLinkBaseUrl(
      source.NEXT_PUBLIC_DEEP_LINK_BASE_URL,
      environment,
    ),
  };
}

export function generateDeepLinkUrl(
  routeKey: string,
  input: DeepLinkGenerationInput = {},
  source: WebDeepLinkEnvironmentSource = currentEnvironmentSource(),
): string {
  return generateRegisteredDeepLinkUrl(
    deepLinkRoutes,
    routeKey,
    resolveWebDeepLinkConfiguration(source),
    input,
  );
}
