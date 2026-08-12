import { describe, expect, it } from "vitest";
import { buildSettlementPaymentRequest } from "./payment-request";

describe("buildSettlementPaymentRequest", () => {
  it("sends By Attorney as settlement type without replacing it with the lien status", () => {
    const byAttorneyLookupId = "019fdaaa-1111-7111-8111-111111111111";
    const fullPaymentLookupId = "019fdbbb-2222-7222-8222-222222222222";
    const request = buildSettlementPaymentRequest({
      lienId: "lien-1",
      caseId: "case-1",
      amount: 3_590,
      paymentDate: "2026-08-06",
      paymentMethod: "Check",
      referenceNumber: "453346",
      notes: "",
      type: byAttorneyLookupId,
      status: fullPaymentLookupId,
      lienStatus: "Closed",
    });

    expect(request).toMatchObject({
      settlementType: byAttorneyLookupId,
      settlementStatus: fullPaymentLookupId,
      lienStatus: "Closed",
    });
    expect(request.settlementType).not.toBe("other");
  });
});
