import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";
import { postFundingOfferedLienMessage } from "@/lib/synqlien-funding-portal/client-actions";
import { OfferedLienMessages } from "./offered-lien-messages";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: vi.fn() }),
}));

vi.mock("@/lib/synqlien-funding-portal/client-actions", () => ({
  postFundingOfferedLienMessage: vi.fn(),
}));

const postFundingOfferedLienMessageMock = vi.mocked(postFundingOfferedLienMessage);
const scrollToMock = vi.fn();

describe("OfferedLienMessages", () => {
  beforeEach(() => {
    postFundingOfferedLienMessageMock.mockReset();
    scrollToMock.mockReset();
    Object.defineProperty(HTMLElement.prototype, "scrollTo", {
      configurable: true,
      value: scrollToMock,
    });
  });

  test("posts selected files with an offered lien message", async () => {
    postFundingOfferedLienMessageMock.mockResolvedValue({
      ok: true,
      status: 201,
      correlationId: "corr-message",
      message: {
        id: "message-1",
        senderType: "buyer",
        senderName: "Buyer Reviewer",
        senderEmail: "buyer@example.test",
        message: "",
        createdAtUtc: "2026-08-31T13:19:00Z",
        isCurrentUser: true,
        attachments: [
          {
            id: "attachment-1",
            fileName: "xray-result.jpg",
            contentType: "image/jpeg",
            fileSizeBytes: 76800,
            createdAtUtc: "2026-08-31T13:19:00Z",
            viewUrl: "/view",
            downloadUrl: "/download",
          },
        ],
      },
    });
    const file = new File(["image"], "xray-result.jpg", { type: "image/jpeg" });

    render(<OfferedLienMessages id="access-link-1" initialMessages={[]} />);

    await userEvent.upload(screen.getByLabelText("Message attachments"), file);
    expect(screen.getByText("xray-result.jpg")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(postFundingOfferedLienMessageMock).toHaveBeenCalledWith("access-link-1", "", [file]);
    await waitFor(() => {
      expect(screen.getByRole("link", { name: "View attachment" })).toHaveAttribute("href", "/view");
    });
    expect(screen.getByRole("link", { name: "Download attachment" })).toHaveAttribute("href", "/download");
    expect(screen.queryByRole("button", { name: "Remove" })).not.toBeInTheDocument();
  });
});
