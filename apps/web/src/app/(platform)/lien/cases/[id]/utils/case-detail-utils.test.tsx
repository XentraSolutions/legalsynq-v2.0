import { describe, expect, it } from "vitest";
import type { SettlementHistoryItemV3 } from "@/lib/settlement/settlement.types";
import { describeSettlementHistoryItem } from "./case-detail-utils";

describe("describeSettlementHistoryItem", () => {
  it("renders law-firm changes without an undefined lien prefix", () => {
    const item: SettlementHistoryItemV3 = {
      id: "history-1",
      type: "law-firm-change",
      lienId: "",
      amount: 0,
      description: "Law firm switched from Old Law to New Law by QA",
      note: "Law firm switched from Old Law to New Law by QA",
      createdAt: "2026-08-10T14:44:00Z",
      updatedBy: "QA Case Manager",
    };

    expect(describeSettlementHistoryItem(item)).toBe(
      "Law firm switched from Old Law to New Law by QA",
    );
    expect(describeSettlementHistoryItem(item)).not.toContain("undefined");
    expect(describeSettlementHistoryItem(item)).not.toContain("to lien");
  });

  it("omits the lien suffix when a standard history item has no lien reference", () => {
    const item: SettlementHistoryItemV3 = {
      id: "history-2",
      type: "reduction",
      lienId: "",
      amount: 100,
      note: "",
      date: "2026-08-10",
      createdAt: "2026-08-10T14:44:00Z",
      updatedBy: "QA Case Manager",
    };

    expect(describeSettlementHistoryItem(item)).toBe("Reduction of $100.00");
  });

  it("uses the recorded payment note as the activity description", () => {
    const item: SettlementHistoryItemV3 = {
      id: "history-3",
      type: "payment",
      lienId: "lien-1",
      amount: 100,
      paymentNumber: 1,
      payee: "",
      checkNumber: "3626",
      note: "Paid with CK#3626",
      createdAt: "2026-08-10T14:44:00Z",
      updatedBy: "QA Case Manager",
    };

    expect(describeSettlementHistoryItem(item)).toBe("Paid with CK#3626");
  });

  it("uses the check number when a payment has no recorded note", () => {
    const item: SettlementHistoryItemV3 = {
      id: "history-4",
      type: "payment",
      lienId: "lien-1",
      amount: 100,
      paymentNumber: 1,
      payee: "",
      checkNumber: "3626",
      note: "",
      createdAt: "2026-08-10T14:44:00Z",
      updatedBy: "QA Case Manager",
    };

    expect(describeSettlementHistoryItem(item)).toBe("Paid with CK#3626");
  });
});
