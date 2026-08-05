import {
  DEEP_LINK_ENVIRONMENTS,
  DeepLinkError,
  type DeepLinkConfiguration,
  type DeepLinkEnvironment,
  type DeepLinkGenerationInput,
  type DeepLinkRouteDefinition,
} from "./route-contract";

function encode(value: string): string {
  return encodeURIComponent(value).replace(
    /[!'()*]/g,
    (character) => `%${character.charCodeAt(0).toString(16).toUpperCase()}`,
  );
}

export function parseDeepLinkEnvironment(
  value: string | undefined,
): DeepLinkEnvironment {
  const normalized = value?.trim().toLowerCase();
  if (!DEEP_LINK_ENVIRONMENTS.includes(normalized as DeepLinkEnvironment)) {
    throw new DeepLinkError(
      "INVALID_ENVIRONMENT",
      `Unsupported deep-link environment '${value ?? ""}'.`,
    );
  }
  return normalized as DeepLinkEnvironment;
}

export function normalizeDeepLinkBaseUrl(
  value: string | undefined,
  environment: DeepLinkEnvironment,
): string {
  if (!value?.trim()) {
    throw new DeepLinkError(
      "MISSING_BASE_URL",
      "Deep-link base URL configuration is required.",
    );
  }

  let url: URL;
  try {
    url = new URL(value.trim());
  } catch {
    throw new DeepLinkError(
      "INVALID_BASE_URL",
      "Deep-link base URL must be an absolute URL.",
    );
  }

  if (url.protocol !== "http:" && url.protocol !== "https:") {
    throw new DeepLinkError(
      "INVALID_BASE_URL",
      "Deep-link base URL must use the HTTP or HTTPS protocol.",
    );
  }
  if (environment === "production" && url.protocol !== "https:") {
    throw new DeepLinkError(
      "INVALID_BASE_URL",
      "Production deep-link base URL must use HTTPS.",
    );
  }
  if (
    url.username ||
    url.password ||
    url.search ||
    url.hash ||
    url.pathname !== "/"
  ) {
    throw new DeepLinkError(
      "INVALID_BASE_URL",
      "Deep-link base URL must contain only a protocol and host.",
    );
  }

  return url.origin;
}

export function generateRegisteredDeepLinkUrl(
  routes: readonly DeepLinkRouteDefinition[],
  routeKey: string,
  configuration: DeepLinkConfiguration,
  input: DeepLinkGenerationInput = {},
): string {
  const route = routes.find((candidate) => candidate.key === routeKey);
  if (!route) {
    throw new DeepLinkError(
      "UNKNOWN_ROUTE",
      `Unknown deep-link route '${routeKey}'.`,
    );
  }
  if (!route.enabled) {
    throw new DeepLinkError(
      "DISABLED_ROUTE",
      `Deep-link route '${routeKey}' is disabled.`,
    );
  }

  const suppliedPathParameters = input.pathParameters ?? {};
  const invalidPathParameter = Object.keys(suppliedPathParameters).find(
    (name) => !route.requiredPathParameters.includes(name),
  );
  if (invalidPathParameter) {
    throw new DeepLinkError(
      "INVALID_PATH_PARAMETER",
      `Path parameter '${invalidPathParameter}' is not declared for route '${routeKey}'.`,
    );
  }

  let path = route.pathTemplate;
  for (const name of route.requiredPathParameters) {
    const value = suppliedPathParameters[name];
    if (typeof value !== "string" || value.trim() === "") {
      throw new DeepLinkError(
        "MISSING_PATH_PARAMETER",
        `Path parameter '${name}' is required for route '${routeKey}'.`,
      );
    }
    path = path.replace(`:${name}`, encode(value));
  }

  const suppliedQueryParameters = input.queryParameters ?? {};
  const unsupportedQueryParameter = Object.keys(suppliedQueryParameters).find(
    (name) => !route.optionalQueryParameters.includes(name),
  );
  if (unsupportedQueryParameter) {
    throw new DeepLinkError(
      "UNSUPPORTED_QUERY_PARAMETER",
      `Query parameter '${unsupportedQueryParameter}' is not supported by route '${routeKey}'.`,
    );
  }

  const query = Object.entries(suppliedQueryParameters)
    .filter((entry): entry is [string, string] => {
      const value = entry[1];
      return typeof value === "string" && value.trim() !== "";
    })
    .sort(([left], [right]) => (left < right ? -1 : left > right ? 1 : 0))
    .map(([name, value]) => `${encode(name)}=${encode(value)}`)
    .join("&");

  const baseUrl = normalizeDeepLinkBaseUrl(
    configuration.baseUrl,
    configuration.environment,
  );
  return `${baseUrl}${path}${query ? `?${query}` : ""}`;
}
