import assert from "node:assert/strict";
import { test } from "node:test";

import {
  DeepLinkError,
  deepLinkRoutes,
  generateRegisteredDeepLinkUrl,
  type DeepLinkRouteDefinition,
} from "../../../../../shared/contracts/deep-links";

import {
  generateDeepLinkUrl,
  resolveWebDeepLinkConfiguration,
} from "../deep-links";

const qaEnvironment = {
  NEXT_PUBLIC_DEEP_LINK_BASE_URL: "https://links.qa.example.test/",
  NEXT_PUBLIC_ENV: "qa",
};

function assertDeepLinkError(
  action: () => unknown,
  code: DeepLinkError["code"],
): void {
  assert.throws(
    action,
    (error) => error instanceof DeepLinkError && error.code === code,
  );
}

test("loads the five initial routes once from the authoritative registry", () => {
  assert.deepEqual(
    deepLinkRoutes.map((route) => [route.key, route.pathTemplate]),
    [
      ["dashboard", "/dashboard"],
      ["dealDetails", "/deals/:dealId"],
      ["contactDetails", "/contacts/:contactId"],
      ["applicationDetails", "/applications/:applicationId"],
      ["reportDetails", "/reports/:reportId"],
    ],
  );
  assert.equal(new Set(deepLinkRoutes.map((route) => route.key)).size, 5);
});

test("generates a static route and normalizes the base-domain trailing slash", () => {
  assert.equal(
    generateDeepLinkUrl("dashboard", {}, qaEnvironment),
    "https://links.qa.example.test/dashboard",
  );
});

test("generates a parameterized route with safely encoded path values", () => {
  assert.equal(
    generateDeepLinkUrl(
      "dealDetails",
      { pathParameters: { dealId: "deal/42 (final)" } },
      qaEnvironment,
    ),
    "https://links.qa.example.test/deals/deal%2F42%20%28final%29",
  );
});

test("rejects an unknown route key", () => {
  assertDeepLinkError(
    () => generateDeepLinkUrl("unknown", {}, qaEnvironment),
    "UNKNOWN_ROUTE",
  );
});

test("rejects missing and blank required path parameters", () => {
  assertDeepLinkError(
    () => generateDeepLinkUrl("dealDetails", {}, qaEnvironment),
    "MISSING_PATH_PARAMETER",
  );
  assertDeepLinkError(
    () =>
      generateDeepLinkUrl(
        "dealDetails",
        { pathParameters: { dealId: "   " } },
        qaEnvironment,
      ),
    "MISSING_PATH_PARAMETER",
  );
});

test("rejects undeclared path parameters", () => {
  assertDeepLinkError(
    () =>
      generateDeepLinkUrl(
        "dealDetails",
        { pathParameters: { dealId: "deal-1", tenantId: "tenant-1" } },
        qaEnvironment,
      ),
    "INVALID_PATH_PARAMETER",
  );
});

test("rejects unsupported query parameters", () => {
  assertDeepLinkError(
    () =>
      generateDeepLinkUrl(
        "dashboard",
        { queryParameters: { redirect: "/admin" } },
        qaEnvironment,
      ),
    "UNSUPPORTED_QUERY_PARAMETER",
  );
});

test("rejects disabled routes", () => {
  const disabledRoute: DeepLinkRouteDefinition = {
    ...deepLinkRoutes[0],
    enabled: false,
    key: "disabledDashboard",
  };

  assertDeepLinkError(
    () =>
      generateRegisteredDeepLinkUrl(
        [disabledRoute],
        disabledRoute.key,
        { baseUrl: "https://links.example.test", environment: "production" },
        {},
      ),
    "DISABLED_ROUTE",
  );
});

test("encodes allowed query values, omits empty values, and sorts keys deterministically", () => {
  const queryRoute: DeepLinkRouteDefinition = {
    ...deepLinkRoutes[0],
    key: "queryFixture",
    optionalQueryParameters: ["source", "view"],
  };
  const input = {
    queryParameters: { source: "email & sms", view: "full/page" },
  } as const;

  assert.equal(
    generateRegisteredDeepLinkUrl(
      [queryRoute],
      queryRoute.key,
      { baseUrl: "https://links.example.test/", environment: "qa" },
      input,
    ),
    "https://links.example.test/dashboard?source=email%20%26%20sms&view=full%2Fpage",
  );
  assert.deepEqual(input, {
    queryParameters: { source: "email & sms", view: "full/page" },
  });
  assert.equal(
    generateRegisteredDeepLinkUrl(
      [queryRoute],
      queryRoute.key,
      { baseUrl: "https://links.example.test", environment: "qa" },
      { queryParameters: { source: "", view: undefined } },
    ),
    "https://links.example.test/dashboard",
  );
});

test("rejects missing and invalid base-domain configuration", () => {
  assertDeepLinkError(
    () =>
      resolveWebDeepLinkConfiguration({
        NEXT_PUBLIC_DEEP_LINK_BASE_URL: "",
        NEXT_PUBLIC_ENV: "qa",
      }),
    "MISSING_BASE_URL",
  );
  assertDeepLinkError(
    () =>
      resolveWebDeepLinkConfiguration({
        NEXT_PUBLIC_DEEP_LINK_BASE_URL: "not-a-url",
        NEXT_PUBLIC_ENV: "qa",
      }),
    "INVALID_BASE_URL",
  );
  assertDeepLinkError(
    () =>
      resolveWebDeepLinkConfiguration({
        NEXT_PUBLIC_DEEP_LINK_BASE_URL: "ftp://links.example.test",
        NEXT_PUBLIC_ENV: "qa",
      }),
    "INVALID_BASE_URL",
  );
});

test("requires HTTPS for production and rejects unsupported environments", () => {
  assertDeepLinkError(
    () =>
      resolveWebDeepLinkConfiguration({
        NEXT_PUBLIC_DEEP_LINK_BASE_URL: "http://links.example.test",
        NEXT_PUBLIC_ENV: "production",
      }),
    "INVALID_BASE_URL",
  );
  assertDeepLinkError(
    () =>
      resolveWebDeepLinkConfiguration({
        NEXT_PUBLIC_DEEP_LINK_BASE_URL: "https://links.example.test",
        NEXT_PUBLIC_ENV: "staging",
      }),
    "INVALID_ENVIRONMENT",
  );
});
