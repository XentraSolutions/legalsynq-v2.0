import assert from "node:assert/strict";
import { afterEach, test } from "node:test";

import {
  DeepLinkError,
  deepLinkRoutes,
  generateRegisteredDeepLinkUrl,
  type DeepLinkRouteDefinition,
} from "../../../../../shared/contracts/deep-links";

import {
  buildDeepLink,
  resolveWebDeepLinkConfiguration,
} from "../deep-links";

const originalBaseUrl = process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
const originalEnvironment = process.env.NEXT_PUBLIC_ENV;
const originalApiUrl = process.env.NEXT_PUBLIC_API_URL;
const originalAppUrl = process.env.NEXT_PUBLIC_APP_URL;

function restoreEnvironmentVariable(
  name: string,
  value: string | undefined,
): void {
  if (value === undefined) {
    delete process.env[name];
  } else {
    process.env[name] = value;
  }
}

afterEach(() => {
  restoreEnvironmentVariable("NEXT_PUBLIC_DEEP_LINK_BASE_URL", originalBaseUrl);
  restoreEnvironmentVariable("NEXT_PUBLIC_ENV", originalEnvironment);
  restoreEnvironmentVariable("NEXT_PUBLIC_API_URL", originalApiUrl);
  restoreEnvironmentVariable("NEXT_PUBLIC_APP_URL", originalAppUrl);
});

function withBaseUrl<T>(baseUrl: string | undefined, action: () => T): T {
  if (baseUrl === undefined) {
    delete process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL;
  } else {
    process.env.NEXT_PUBLIC_DEEP_LINK_BASE_URL = baseUrl;
  }
  return action();
}

function assertDeepLinkError(
  action: () => unknown,
  code: DeepLinkError["code"],
): void {
  assert.throws(
    action,
    (error) => error instanceof DeepLinkError && error.code === code,
  );
}

test("loads pathTemplate values directly from the authoritative registry", () => {
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

test("generates every registered route from route intent", () => {
  withBaseUrl("https://links.example.test/", () => {
    assert.equal(
      buildDeepLink({ routeKey: "dashboard" }),
      "https://links.example.test/dashboard",
    );
    assert.equal(
      buildDeepLink({
        routeKey: "contactDetails",
        pathParams: { contactId: "contact-1" },
      }),
      "https://links.example.test/contacts/contact-1",
    );
    assert.equal(
      buildDeepLink({
        routeKey: "applicationDetails",
        pathParams: { applicationId: "application-1" },
      }),
      "https://links.example.test/applications/application-1",
    );
    assert.equal(
      buildDeepLink({
        routeKey: "dealDetails",
        pathParams: { dealId: "deal-1" },
      }),
      "https://links.example.test/deals/deal-1",
    );
    assert.equal(
      buildDeepLink({
        routeKey: "reportDetails",
        pathParams: { reportId: "report-1" },
      }),
      "https://links.example.test/reports/report-1",
    );
  });
});

test("encodes each path parameter as one segment without unresolved placeholders", () => {
  withBaseUrl("https://links.example.test", () => {
    const cases = [
      ["space value", "space%20value"],
      ["slash/value", "slash%2Fvalue"],
      ["question?value", "question%3Fvalue"],
      ["hash#value", "hash%23value"],
      ["percent%value", "percent%25value"],
      ["already%20encoded", "already%2520encoded"],
      ["Makati 日本", "Makati%20%E6%97%A5%E6%9C%AC"],
    ] as const;

    for (const [value, expected] of cases) {
      const url = buildDeepLink({
        routeKey: "contactDetails",
        pathParams: { contactId: value },
      });
      assert.equal(url, `https://links.example.test/contacts/${expected}`);
      assert.doesNotMatch(new URL(url).pathname, /:[A-Za-z]/);
    }
  });
});

test("rejects unknown routes without treating route keys as paths", () => {
  withBaseUrl("https://links.example.test", () => {
    assertDeepLinkError(
      () => buildDeepLink({ routeKey: "unknown/path" }),
      "UNKNOWN_ROUTE",
    );
  });
});

test("rejects missing, null, undefined, empty, and blank required parameters", () => {
  withBaseUrl("https://links.example.test", () => {
    const invalidValues = [undefined, null, "", "   "] as const;
    for (const dealId of invalidValues) {
      assertDeepLinkError(
        () =>
          buildDeepLink({
            routeKey: "dealDetails",
            pathParams: dealId === undefined ? undefined : { dealId },
          }),
        "MISSING_PATH_PARAMETER",
      );
    }
  });
});

test("rejects unexpected path parameters", () => {
  withBaseUrl("https://links.example.test", () => {
    assertDeepLinkError(
      () =>
        buildDeepLink({
          routeKey: "dealDetails",
          pathParams: { dealId: "deal-1", tenantId: "tenant-1" },
        }),
      "INVALID_PATH_PARAMETER",
    );
  });
});

test("the delegated shared generator rejects disabled route fixtures", () => {
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
        {
          baseUrl: "https://links.example.test",
          environment: "production",
        },
      ),
    "DISABLED_ROUTE",
  );
});

test("accepts only an HTTPS origin and normalizes its trailing slash", () => {
  assert.deepEqual(
    resolveWebDeepLinkConfiguration({
      NEXT_PUBLIC_DEEP_LINK_BASE_URL: "https://links.example.test/",
    }),
    { baseUrl: "https://links.example.test", environment: "production" },
  );

  const invalidValues = [
    undefined,
    "",
    "not-a-url",
    "links.example.test",
    "http://links.example.test",
    "javascript:alert(1)",
    "data:text/plain,test",
    "file:///tmp/test",
    "https://",
    "https://user:password@links.example.test",
    "https://links.example.test/path",
    "https://links.example.test?query=value",
    "https://links.example.test#fragment",
  ];

  for (const value of invalidValues) {
    assertDeepLinkError(
      () =>
        resolveWebDeepLinkConfiguration({
          NEXT_PUBLIC_DEEP_LINK_BASE_URL: value,
        }),
      value ? "INVALID_BASE_URL" : "MISSING_BASE_URL",
    );
  }
});

test("missing configuration never falls back to a Web or API origin", () => {
  process.env.NEXT_PUBLIC_ENV = "production";
  process.env.NEXT_PUBLIC_API_URL = "https://api.example.test";
  process.env.NEXT_PUBLIC_APP_URL = "https://web.example.test";

  withBaseUrl(undefined, () => {
    assertDeepLinkError(
      () => buildDeepLink({ routeKey: "dashboard" }),
      "MISSING_BASE_URL",
    );
  });
});
