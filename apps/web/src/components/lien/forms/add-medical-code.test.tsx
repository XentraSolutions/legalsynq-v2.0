import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, describe, expect, test, vi } from "vitest";
import { CreateMedicalCode } from "./add-medical-code";

const mocks = vi.hoisted(() => ({
  addToast: vi.fn(),
  createMedicalCode: vi.fn(),
}));

vi.mock("@/lib/cases", () => ({
  casesService: {
    createMedicalCode: mocks.createMedicalCode,
  },
}));

vi.mock("@/stores/lien-store", () => ({
  useLienStore: (selector: (state: { addToast: typeof mocks.addToast }) => unknown) =>
    selector({ addToast: mocks.addToast }),
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("CreateMedicalCode", () => {
  test("refreshes procedure codes after creating a manual CPT code", async () => {
    mocks.createMedicalCode.mockResolvedValue({});
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const invalidateQueries = vi.spyOn(queryClient, "invalidateQueries");

    render(
      <QueryClientProvider client={queryClient}>
        <CreateMedicalCode open onClose={() => {}} />
      </QueryClientProvider>,
    );

    await screen.findByRole("dialog");
    const [codeInput, descriptionInput] = screen.getAllByRole("textbox");
    fireEvent.change(codeInput, {
      target: { value: "CPT-12345" },
    });
    fireEvent.change(descriptionInput, {
      target: { value: "Example procedure" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create Medical Code" }));

    await waitFor(() =>
      expect(mocks.createMedicalCode).toHaveBeenCalledWith({
        code: "CPT-12345",
        description: "Example procedure",
      }),
    );
    await waitFor(() =>
      expect(invalidateQueries).toHaveBeenCalledWith({
        queryKey: ["procedureCodes"],
      }),
    );
  });
});
