import registryJson from "./routes.json";

import type {
  DeepLinkRouteDefinition,
  DeepLinkRouteRegistryDocument,
} from "./route-contract";

const PLACEHOLDER_PATTERN = /:([A-Za-z][A-Za-z0-9]*)/g;
const KEY_PATTERN = /^[a-z][A-Za-z0-9]*$/;
const PARAMETER_NAME_PATTERN = /^[A-Za-z][A-Za-z0-9]*$/;

function unique(values: readonly string[]): boolean {
  return new Set(values).size === values.length;
}

function placeholders(pathTemplate: string): string[] {
  return Array.from(
    pathTemplate.matchAll(PLACEHOLDER_PATTERN),
    (match) => match[1],
  );
}

function sameMembers(
  left: readonly string[],
  right: readonly string[],
): boolean {
  return (
    left.length === right.length && left.every((value) => right.includes(value))
  );
}

export function validateRouteRegistry(
  document: DeepLinkRouteRegistryDocument,
): readonly DeepLinkRouteDefinition[] {
  if (
    document.version !== 1 ||
    !Array.isArray(document.routes) ||
    document.routes.length === 0
  ) {
    throw new Error(
      "The deep-link route registry must use version 1 and contain routes.",
    );
  }

  const keys = new Set<string>();
  return Object.freeze(
    document.routes.map((route) => {
      if (!KEY_PATTERN.test(route.key) || keys.has(route.key)) {
        throw new Error(
          `Deep-link route key '${route.key}' is blank or duplicated.`,
        );
      }
      keys.add(route.key);

      if (!route.pathTemplate.startsWith("/")) {
        throw new Error(
          `Deep-link route '${route.key}' must use an absolute path template.`,
        );
      }

      const templateParameters = placeholders(route.pathTemplate);
      if (
        !unique(templateParameters) ||
        !unique(route.requiredPathParameters) ||
        !route.requiredPathParameters.every((name: string) =>
          PARAMETER_NAME_PATTERN.test(name),
        ) ||
        !sameMembers(templateParameters, route.requiredPathParameters)
      ) {
        throw new Error(
          `Deep-link route '${route.key}' path placeholders must match requiredPathParameters.`,
        );
      }

      if (
        !unique(route.optionalQueryParameters) ||
        !route.optionalQueryParameters.every((name: string) =>
          PARAMETER_NAME_PATTERN.test(name),
        )
      ) {
        throw new Error(
          `Deep-link route '${route.key}' has duplicate optional query parameters.`,
        );
      }

      if (
        !route.mobileDestination ||
        !route.fallbackDestination ||
        !route.analyticsEvent
      ) {
        throw new Error(
          `Deep-link route '${route.key}' has incomplete metadata.`,
        );
      }

      return Object.freeze({
        ...route,
        optionalQueryParameters: Object.freeze([
          ...route.optionalQueryParameters,
        ]),
        requiredPathParameters: Object.freeze([
          ...route.requiredPathParameters,
        ]),
      });
    }),
  );
}

export const deepLinkRoutes = validateRouteRegistry(
  registryJson as DeepLinkRouteRegistryDocument,
);

export function getDeepLinkRoute(
  key: string,
): DeepLinkRouteDefinition | undefined {
  return deepLinkRoutes.find((route) => route.key === key);
}
