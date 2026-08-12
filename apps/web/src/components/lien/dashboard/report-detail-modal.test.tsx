import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, test, vi } from "vitest";
import { ReportDetailModal } from "./report-detail-modal";

vi.mock("./donut-chart", () => ({ DonutChart: () => <div>Chart</div> }));

describe("ReportDetailModal", () => {
  test("uses the full record count and delegates page changes to the server query", async () => {
    const onPageChange = vi.fn();

    render(
      <ReportDetailModal
        open
        onClose={() => {}}
        periodLabel="08/01/2026 – 08/06/2026"
        config={{
          title: "Total Lien Report",
          totalLabel: "Total Liens",
          total: 25,
          segments: [],
          columns: [{ label: "Lien", render: (row) => String(row) }],
          rows: ["Lien 11"],
          rowKey: (row) => String(row),
        }}
        page={2}
        pageSize={10}
        totalCount={25}
        onPageChange={onPageChange}
      />,
    );

    expect(await screen.findByText("25 records")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Next page" }));
    expect(onPageChange).toHaveBeenCalledWith(3);
  });
});
