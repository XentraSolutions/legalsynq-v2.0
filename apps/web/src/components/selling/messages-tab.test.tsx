import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, test, vi } from "vitest";
import { liensService } from "@/lib/selling";
import { MessagesTab } from "./messages-tab";

vi.mock("@/lib/selling", () => ({
  liensService: {
    getLienMessages: vi.fn(),
    sendLienMessage: vi.fn(),
  },
}));

const getLienMessagesMock = vi.mocked(liensService.getLienMessages);
const sendLienMessageMock = vi.mocked(liensService.sendLienMessage);
const scrollToMock = vi.fn();

describe("MessagesTab", () => {
  beforeEach(() => {
    getLienMessagesMock.mockReset();
    sendLienMessageMock.mockReset();
    scrollToMock.mockReset();
    Object.defineProperty(HTMLElement.prototype, "scrollTo", {
      configurable: true,
      value: scrollToMock,
    });
  });

  test("loads persisted seller lien messages", async () => {
    getLienMessagesMock.mockResolvedValue({
      items: [
        {
          id: "message-1",
          senderType: "buyer",
          senderName: "Buyer Reviewer",
          senderEmail: "buyer@example.test",
          message: "Can you confirm the signed LOP is final?",
          createdAtUtc: "2026-07-28T12:30:00Z",
          isCurrentUser: false,
        },
        {
          id: "message-2",
          senderType: "seller",
          senderName: "Seller Processor",
          senderEmail: "seller@example.test",
          message: "The LOP is final.",
          createdAtUtc: "2026-07-28T12:35:00Z",
          isCurrentUser: true,
        },
      ],
    });

    render(<MessagesTab lienId="lien-1" />);

    expect(await screen.findByText("Buyer Reviewer")).toBeInTheDocument();
    expect(screen.getByText("Can you confirm the signed LOP is final?")).toBeInTheDocument();
    expect(screen.getByText("The LOP is final.")).toBeInTheDocument();
    expect(getLienMessagesMock).toHaveBeenCalledWith("lien-1");
  });

  test("sends a seller message and appends it to the thread", async () => {
    getLienMessagesMock.mockResolvedValue({ items: [] });
    sendLienMessageMock.mockResolvedValue({
      id: "message-3",
      senderType: "seller",
      senderName: "Seller Processor",
      senderInitials: "SP",
      senderEmail: "seller@example.test",
      message: "The LOP is final and attached to the package.",
      createdAtUtc: "2026-07-28T12:40:00Z",
      isCurrentUser: true,
    });

    render(<MessagesTab lienId="lien-1" />);

    await screen.findByText("No Messages Yet");
    await userEvent.type(
      screen.getByRole("textbox", { name: "Message" }),
      "The LOP is final and attached to the package.",
    );
    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(sendLienMessageMock).toHaveBeenCalledWith(
      "lien-1",
      "The LOP is final and attached to the package.",
    );
    expect(await screen.findByText("The LOP is final and attached to the package.")).toBeInTheDocument();
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

  test("shows an error when the message cannot be sent", async () => {
    getLienMessagesMock.mockResolvedValue({ items: [] });
    sendLienMessageMock.mockRejectedValue(new Error("Messages can be sent after the lien has an offer thread with a buyer."));

    render(<MessagesTab lienId="lien-1" />);

    await screen.findByText("No Messages Yet");
    await userEvent.type(screen.getByRole("textbox", { name: "Message" }), "Following up.");
    await userEvent.click(screen.getByRole("button", { name: "Send message" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "The message could not be sent. Please try again.",
    );
  });
});
