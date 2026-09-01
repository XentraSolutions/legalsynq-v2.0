import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";
import { postPublicBuyerPortalMessage } from "@/lib/liens/public-buyer-portal-messages";
import { PublicPortalMessagesCard } from "./messages-client";

vi.mock("@/lib/liens/public-buyer-portal-messages", () => ({
  postPublicBuyerPortalMessage: vi.fn(),
}));

const postPublicBuyerPortalMessageMock = vi.mocked(postPublicBuyerPortalMessage);
const scrollToMock = vi.fn();

describe("PublicPortalMessagesCard", () => {
  beforeEach(() => {
    postPublicBuyerPortalMessageMock.mockReset();
    scrollToMock.mockReset();
    Object.defineProperty(HTMLElement.prototype, "scrollTo", {
      configurable: true,
      value: scrollToMock,
    });
  });

  test("posts a buyer message and appends it to the thread", async () => {
    postPublicBuyerPortalMessageMock.mockResolvedValue({
      ok: true,
      status: 201,
      correlationId: "corr-message",
      message: {
        id: "message-1",
        senderType: "buyer",
        senderName: "Buyer Reviewer",
        senderEmail: "buyer@example.test",
        message: "Can you confirm the signed LOP is final?",
        createdAtUtc: "2026-07-28T12:30:00Z",
      },
    });

    render(
      <PublicPortalMessagesCard
        token="token-abc"
        audience="buyer"
        initialMessages={[]}
      />,
    );

    expect(scrollToMock).not.toHaveBeenCalled();

    await userEvent.type(
      screen.getByRole("textbox", { name: "Message" }),
      "Can you confirm the signed LOP is final?",
    );
    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(postPublicBuyerPortalMessageMock).toHaveBeenCalledWith(
      "token-abc",
      "Can you confirm the signed LOP is final?",
      [],
    );
    await waitFor(() => {
      expect(screen.getByText("Can you confirm the signed LOP is final?")).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(scrollToMock).toHaveBeenCalledWith(
        expect.objectContaining({
          behavior: "smooth",
          top: expect.any(Number),
        }),
      );
    });
    expect(screen.getByRole("textbox", { name: "Message" })).toHaveValue("");
  });

  test("renders seller links with a composer and existing buyer messages", () => {
    render(
      <PublicPortalMessagesCard
        token="seller-token"
        audience="seller"
        initialMessages={[
          {
            id: "message-1",
            senderType: "buyer",
            senderName: "Buyer Reviewer",
            senderEmail: "buyer@example.test",
            message: "We are reviewing the lien package.",
            createdAtUtc: "2026-07-28T12:30:00Z",
          },
        ]}
      />,
    );

    expect(screen.getByRole("textbox", { name: "Message" })).toBeInTheDocument();
    expect(screen.getByText("Buyer Reviewer")).toBeInTheDocument();
    expect(screen.getByText("We are reviewing the lien package.")).toBeInTheDocument();
  });

  test("shows the API error when sending fails", async () => {
    postPublicBuyerPortalMessageMock.mockResolvedValue({
      ok: false,
      status: 410,
      correlationId: null,
      error: {
        code: "expired",
        title: "Lien offer link expired",
        message: "This secure link has expired.",
      },
    });

    render(
      <PublicPortalMessagesCard
        token="expired-token"
        audience="seller"
        initialMessages={[]}
      />,
    );

    await userEvent.type(screen.getByRole("textbox", { name: "Message" }), "Following up.");
    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("This secure link has expired.");
  });

  test("passes selected files when posting a public message", async () => {
    postPublicBuyerPortalMessageMock.mockResolvedValue({
      ok: true,
      status: 201,
      correlationId: "corr-message",
      message: {
        id: "message-1",
        senderType: "buyer",
        senderName: "Buyer Reviewer",
        senderEmail: "buyer@example.test",
        message: "",
        createdAtUtc: "2026-07-28T12:30:00Z",
        attachments: [
          {
            id: "attachment-1",
            fileName: "signed-lop.pdf",
            contentType: "application/pdf",
            fileSizeBytes: 1234,
            createdAtUtc: "2026-07-28T12:30:00Z",
            viewUrl: "/view",
            downloadUrl: "/download",
          },
        ],
      },
    });
    const file = new File(["pdf"], "signed-lop.pdf", { type: "application/pdf" });

    render(
      <PublicPortalMessagesCard
        token="token-abc"
        audience="buyer"
        initialMessages={[]}
      />,
    );

    await userEvent.upload(screen.getByLabelText("Message attachments"), file);
    expect(screen.getByText("signed-lop.pdf")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(postPublicBuyerPortalMessageMock).toHaveBeenCalledWith("token-abc", "", [file]);
    await waitFor(() => {
      expect(screen.getByRole("link", { name: "View attachment" })).toHaveAttribute("href", "/view");
    });
    expect(screen.getByRole("link", { name: "Download attachment" })).toHaveAttribute("href", "/download");
    expect(screen.queryByRole("button", { name: "Remove" })).not.toBeInTheDocument();
  });
});
