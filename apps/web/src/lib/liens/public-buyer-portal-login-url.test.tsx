import { describe, expect, it } from "vitest";
import {
  normalizeSynqLienBuyerLoginUrl,
  SYNQLIEN_BUYER_LOGIN_URL,
} from "./public-buyer-portal";

describe("SynqLien buyer portal login URL", () => {
  it("defaults buyer activation login to the dashboard", () => {
    expect(SYNQLIEN_BUYER_LOGIN_URL).toBe(
      "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation",
    );
  });

  it("normalizes legacy Offered Liens return targets to the dashboard", () => {
    expect(
      normalizeSynqLienBuyerLoginUrl(
        "/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation",
      ),
    ).toBe(SYNQLIEN_BUYER_LOGIN_URL);
    expect(
      normalizeSynqLienBuyerLoginUrl(
        "https://synqlien-demo.legalsynq.com/login?returnTo=%2Ffunding%2Foffered-liens&reason=synqlien-buyer-activation",
      ),
    ).toBe(
      "https://synqlien-demo.legalsynq.com/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation",
    );
  });
});
