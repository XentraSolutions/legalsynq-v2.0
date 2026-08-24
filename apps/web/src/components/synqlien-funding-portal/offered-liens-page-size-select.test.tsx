import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, test, vi } from "vitest";
import { OfferedLiensPageSizeSelect } from "./offered-liens-page-size-select";

const pushMock = vi.fn();
let currentSearchParams = new URLSearchParams();

vi.mock("next/navigation", () => ({
  usePathname: () => "/funding/offered-liens",
  useRouter: () => ({ push: pushMock }),
  useSearchParams: () => currentSearchParams,
}));

describe("OfferedLiensPageSizeSelect", () => {
  beforeEach(() => {
    pushMock.mockReset();
    currentSearchParams = new URLSearchParams();
  });

  test("renders the working-set trigger using the shared select UI", () => {
    render(<OfferedLiensPageSizeSelect pageSize={10} firstItem={1} lastItem={10} />);

    expect(screen.getByRole("combobox", { name: "Working set" })).toHaveTextContent("1-10");
  });
});
