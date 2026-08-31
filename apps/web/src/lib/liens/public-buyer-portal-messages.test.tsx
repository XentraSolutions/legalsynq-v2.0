import { describe, expect, test, vi } from "vitest";
import {
  buildPublicBuyerPortalMessageUrl,
  postPublicBuyerPortalMessage,
} from "./public-buyer-portal-messages";

describe("public buyer portal messages", () => {
  test("builds the tenant-portal BFF message URL", () => {
    expect(buildPublicBuyerPortalMessageUrl("token-abc")).toBe(
      "/api/lien/api/liens/selling/public/token-abc/messages",
    );
    expect(buildPublicBuyerPortalMessageUrl("token with space")).toBe(
      "/api/lien/api/liens/selling/public/token%20with%20space/messages",
    );
  });

  test("does not submit blank messages", async () => {
    const fetchImpl = vi.fn();

    const result = await postPublicBuyerPortalMessage("token-abc", "   ", [], { fetchImpl });

    expect(fetchImpl).not.toHaveBeenCalled();
    expect(result.ok).toBe(false);
    expect(result.error?.code).toBe("message-required");
  });

  test("posts trimmed messages and returns the created message", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: "message-1",
          senderType: "buyer",
          senderName: "Buyer Reviewer",
          senderEmail: "buyer@example.test",
          message: "Following up.",
          createdAtUtc: "2026-07-28T12:30:00Z",
        }),
        {
          status: 201,
          headers: {
            "content-type": "application/json",
            "x-correlation-id": "corr-message",
          },
        },
      ),
    );

    const result = await postPublicBuyerPortalMessage(
      "token-abc",
      "  Following up.  ",
      [],
      { fetchImpl },
    );

    expect(fetchImpl).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/public/token-abc/messages",
      {
        method: "POST",
        headers: {
          accept: "application/json",
          "content-type": "application/json",
        },
        body: JSON.stringify({ message: "Following up." }),
        cache: "no-store",
      },
    );
    expect(result.ok).toBe(true);
    expect(result.correlationId).toBe("corr-message");
    expect(result.message?.message).toBe("Following up.");
  });

  test("maps Liens public error responses", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          error: {
            code: "expired",
            title: "Lien offer link expired",
            message: "This secure link has expired.",
          },
        }),
        { status: 410 },
      ),
    );

    const result = await postPublicBuyerPortalMessage("token-abc", "Following up.", [], {
      fetchImpl,
    });

    expect(result.ok).toBe(false);
    expect(result.status).toBe(410);
    expect(result.error?.message).toBe("This secure link has expired.");
  });

  test("posts message attachments as multipart form data", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
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
        }),
        { status: 201 },
      ),
    );
    const file = new File(["pdf"], "signed-lop.pdf", { type: "application/pdf" });

    const result = await postPublicBuyerPortalMessage("token-abc", "  ", [file], {
      fetchImpl,
    });

    expect(fetchImpl).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/public/token-abc/messages",
      expect.objectContaining({
        method: "POST",
        headers: { accept: "application/json" },
        body: expect.any(FormData),
        cache: "no-store",
      }),
    );
    const formData = (fetchImpl.mock.calls[0]?.[1] as RequestInit).body as FormData;
    expect(formData.get("message")).toBe("");
    expect(formData.getAll("files")).toEqual([file]);
    expect(result.ok).toBe(true);
    expect(result.message?.attachments?.[0]?.fileName).toBe("signed-lop.pdf");
  });
});
