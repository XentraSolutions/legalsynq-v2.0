import { deepLinkRoutes, type DeepLinkRouteDefinition } from '@/shared/deepLinks';

import type {
  DeepLinkFailure,
  DeepLinkResolution,
  DeepLinkResolverConfiguration,
  ResolvedDeepLink,
} from './DeepLinkTypes';

const HTTPS_PROTOCOL = 'https:';
const DNS_HOST_PATTERN =
  /^(?=.{1,253}$)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$/i;
const RAW_URL_PATTERN = /^[A-Za-z][A-Za-z0-9+.-]*:\/\/[^/?#]*(?<path>[^?#]*)/;
const INVALID_PERCENT_ENCODING_PATTERN = /%(?![0-9A-Fa-f]{2})/;

interface RouteMatch {
  route: DeepLinkRouteDefinition;
  pathParameters: Record<string, string>;
}

function failure(
  status: DeepLinkFailure['status'],
  originalUrl: string,
  reason: string,
  normalizedUrl?: string
): DeepLinkFailure {
  return { status, originalUrl, reason, ...(normalizedUrl ? { normalizedUrl } : {}) };
}

function decodeComponent(value: string): string | null {
  if (INVALID_PERCENT_ENCODING_PATTERN.test(value)) {
    return null;
  }

  try {
    return decodeURIComponent(value);
  } catch {
    return null;
  }
}

function extractRawPath(url: string): string | null {
  const match = RAW_URL_PATTERN.exec(url);
  return match?.groups?.path || '/';
}

function decodePathSegments(rawPath: string): string[] | null {
  if (!rawPath.startsWith('/') || rawPath.includes('\\')) {
    return null;
  }

  const rawSegments = rawPath.slice(1).split('/');
  if (rawSegments.some((segment) => !segment)) {
    return null;
  }

  const decoded = rawSegments.map(decodeComponent);
  if (
    decoded.some(
      (segment) => segment === null || segment === '' || segment === '.' || segment === '..'
    )
  ) {
    return null;
  }

  return decoded as string[];
}

function matchRoute(
  route: DeepLinkRouteDefinition,
  pathSegments: readonly string[]
): RouteMatch | null {
  const templateSegments = route.pathTemplate.slice(1).split('/');
  if (!route.enabled || templateSegments.length !== pathSegments.length) {
    return null;
  }

  const pathParameters: Record<string, string> = {};
  for (let index = 0; index < templateSegments.length; index += 1) {
    const templateSegment = templateSegments[index];
    const pathSegment = pathSegments[index];

    if (templateSegment.startsWith(':')) {
      pathParameters[templateSegment.slice(1)] = pathSegment;
    } else if (templateSegment !== pathSegment) {
      return null;
    }
  }

  if (route.requiredPathParameters.some((parameter) => !pathParameters[parameter]?.trim())) {
    return null;
  }

  return { route, pathParameters };
}

function parseQuery(
  rawSearch: string,
  route: DeepLinkRouteDefinition
): Record<string, string> | null {
  if (!rawSearch) {
    return {};
  }

  const queryParameters: Record<string, string> = {};
  for (const pair of rawSearch.slice(1).split('&')) {
    if (!pair) {
      return null;
    }

    const separatorIndex = pair.indexOf('=');
    const rawName = separatorIndex === -1 ? pair : pair.slice(0, separatorIndex);
    const rawValue = separatorIndex === -1 ? '' : pair.slice(separatorIndex + 1);
    const name = decodeComponent(rawName.replace(/\+/g, ' '));
    const value = decodeComponent(rawValue.replace(/\+/g, ' '));

    if (
      !name ||
      value === null ||
      !route.optionalQueryParameters.includes(name) ||
      Object.hasOwn(queryParameters, name)
    ) {
      return null;
    }

    queryParameters[name] = value;
  }

  return queryParameters;
}

function buildNormalizedUrl(
  host: string,
  pathSegments: readonly string[],
  queryParameters: Readonly<Record<string, string>>
): string {
  const normalized = new URL(`https://${host}`);
  normalized.pathname = `/${pathSegments.map(encodeURIComponent).join('/')}`;

  for (const name of Object.keys(queryParameters).sort()) {
    normalized.searchParams.set(name, queryParameters[name]);
  }

  return normalized.toString();
}

function normalizeConfiguredHost(host: string | null): string | null {
  if (host === null) {
    return null;
  }

  const normalized = host.trim().toLowerCase();
  if (!DNS_HOST_PATTERN.test(normalized)) {
    throw new Error('Deep-link resolver expectedHttpsHost must be a DNS hostname.');
  }

  return normalized;
}

export class DeepLinkResolver {
  private readonly expectedHttpsHost: string | null;

  constructor(configuration: DeepLinkResolverConfiguration) {
    this.expectedHttpsHost = normalizeConfiguredHost(configuration.expectedHttpsHost);
  }

  resolve(originalUrl: string): DeepLinkResolution {
    let parsedUrl: URL;
    try {
      parsedUrl = new URL(originalUrl);
    } catch {
      return failure('malformed', originalUrl, 'The incoming URL is malformed.');
    }

    if (parsedUrl.protocol !== HTTPS_PROTOCOL) {
      return failure(
        'unsupported_scheme',
        originalUrl,
        `Scheme '${parsedUrl.protocol.replace(':', '')}' is not supported.`
      );
    }

    if (
      !this.expectedHttpsHost ||
      parsedUrl.hostname.toLowerCase() !== this.expectedHttpsHost ||
      parsedUrl.port ||
      parsedUrl.username ||
      parsedUrl.password
    ) {
      return failure(
        'unsupported_host',
        originalUrl,
        'The HTTPS host is not approved for this Mobile environment.'
      );
    }

    const rawPath = extractRawPath(originalUrl);
    if (rawPath === null || parsedUrl.hash) {
      return failure(
        'invalid_parameters',
        originalUrl,
        parsedUrl.hash
          ? 'URL fragments are not supported by the deep-link contract.'
          : 'The URL path is invalid.'
      );
    }

    const pathSegments = decodePathSegments(rawPath);
    if (!pathSegments) {
      const hasMalformedEncoding = rawPath
        .split('/')
        .some((segment) => decodeComponent(segment) === null);
      return failure(
        hasMalformedEncoding ? 'malformed' : 'unsupported_route',
        originalUrl,
        'The URL path is malformed or does not match a supported route.'
      );
    }

    const matches = deepLinkRoutes
      .map((route) => matchRoute(route, pathSegments))
      .filter((match): match is RouteMatch => match !== null);

    if (matches.length !== 1) {
      return failure(
        'unsupported_route',
        originalUrl,
        matches.length > 1
          ? 'The URL path matches more than one shared route.'
          : 'The URL path is not supported.'
      );
    }

    const match = matches[0];
    const queryParameters = parseQuery(parsedUrl.search, match.route);
    if (!queryParameters) {
      return failure(
        'invalid_parameters',
        originalUrl,
        'The URL contains malformed, duplicate, or unsupported query parameters.'
      );
    }

    const normalizedUrl = buildNormalizedUrl(this.expectedHttpsHost, pathSegments, queryParameters);
    const result: ResolvedDeepLink = {
      status: 'resolved',
      routeKey: match.route.key,
      pathParameters: Object.freeze({ ...match.pathParameters }),
      queryParameters: Object.freeze({ ...queryParameters }),
      originalUrl,
      normalizedUrl,
    };

    return result;
  }
}
