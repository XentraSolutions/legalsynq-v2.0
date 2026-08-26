export const DEEP_LINK_ENVIRONMENTS = [
  "local",
  "development",
  "qa",
  "uat",
  "production",
] as const;

export type DeepLinkEnvironment = (typeof DEEP_LINK_ENVIRONMENTS)[number];

export interface DeepLinkRouteDefinition {
  readonly analyticsEvent: string;
  readonly enabled: boolean;
  readonly fallbackDestination: string;
  readonly key: string;
  readonly mobileDestination: string;
  readonly optionalQueryParameters: readonly string[];
  readonly pathTemplate: string;
  readonly requiredPathParameters: readonly string[];
  readonly requiresAuthentication: boolean;
  readonly requiresAuthorization: boolean;
}

export interface DeepLinkRouteRegistryDocument {
  readonly routes: readonly DeepLinkRouteDefinition[];
  readonly version: 1;
}

export interface DeepLinkGenerationInput {
  readonly pathParameters?: Readonly<Record<string, string | null | undefined>>;
  readonly queryParameters?: Readonly<
    Record<string, string | null | undefined>
  >;
}

export interface DeepLinkConfiguration {
  readonly baseUrl: string;
  readonly environment: DeepLinkEnvironment;
}

export type DeepLinkErrorCode =
  | "DISABLED_ROUTE"
  | "INVALID_BASE_URL"
  | "INVALID_ENVIRONMENT"
  | "INVALID_PATH_PARAMETER"
  | "MISSING_BASE_URL"
  | "MISSING_PATH_PARAMETER"
  | "UNKNOWN_ROUTE"
  | "UNSUPPORTED_QUERY_PARAMETER";

export class DeepLinkError extends Error {
  constructor(
    readonly code: DeepLinkErrorCode,
    message: string,
  ) {
    super(message);
    this.name = "DeepLinkError";
  }
}
