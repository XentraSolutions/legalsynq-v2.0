export type DeepLinkFailureStatus =
  | 'malformed'
  | 'unsupported_scheme'
  | 'unsupported_host'
  | 'unsupported_route'
  | 'invalid_parameters'
  | 'duplicate';

export interface ResolvedDeepLink {
  status: 'resolved';
  routeKey: string;
  pathParameters: Readonly<Record<string, string>>;
  queryParameters: Readonly<Record<string, string>>;
  originalUrl: string;
  normalizedUrl: string;
}

export interface DeepLinkFailure {
  status: DeepLinkFailureStatus;
  reason: string;
  originalUrl: string;
  normalizedUrl?: string;
}

export type DeepLinkResolution = ResolvedDeepLink | DeepLinkFailure;

export interface DeepLinkResolverConfiguration {
  expectedHttpsHost: string | null;
}

export type DeepLinkResolutionListener = (resolution: DeepLinkResolution) => void;
