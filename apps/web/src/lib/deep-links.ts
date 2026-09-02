import {
  DeepLinkError,
  deepLinkRoutes,
  generateRegisteredDeepLinkUrl,
  normalizeDeepLinkBaseUrl,
  type DeepLinkConfiguration,
} from "../../../../shared/contracts/deep-links";

export { DeepLinkError };

export interface BuildDeepLinkInput {
  readonly pathParams?: Readonly<Record<string, string | null | undefined>>;
  readonly routeKey: string;
}

export interface WebDeepLinkEnvironmentSource {
  readonly NEXT_PUBLIC_DEEP_LINK_BASE_URL?: string;
}

function currentEnvironmentSource(): WebDeepLinkEnvironmentSource {
  return {
    NEXT_PUBLIC_DEEP_LINK_BASE_URL: process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL,
  };
}

export function resolveWebDeepLinkConfiguration(
  source: WebDeepLinkEnvironmentSource = currentEnvironmentSource(),
): DeepLinkConfiguration {
  return {
    // Web-generated links must always be verified-link-compatible HTTPS URLs.
    environment: "production",
    baseUrl: normalizeDeepLinkBaseUrl(
      source.NEXT_PUBLIC_DEEP_LINK_BASE_URL,
      "production",
    ),
  };
}

export function buildDeepLink({
  routeKey,
  pathParams,
}: BuildDeepLinkInput): string {
  const url = generateRegisteredDeepLinkUrl(
    deepLinkRoutes,
    routeKey,
    resolveWebDeepLinkConfiguration(),
    { pathParameters: pathParams },
  );

  if (/:([A-Za-z][A-Za-z0-9]*)/.test(new URL(url).pathname)) {
    throw new Error(
      `Deep-link route '${routeKey}' produced an unresolved path placeholder.`,
    );
  }

  return url;
}
