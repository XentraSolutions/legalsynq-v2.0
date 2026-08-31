import { describe, expect, test, vi } from "vitest";
import {
  buildFundingOfferedLienActionUrl,
  buildFundingOfferedLienMessageUrl,
  postFundingOfferedLienMessage,
  submitFundingOfferedLienResponse,
} from "./client-actions";

describe("SynqLien funding offered lien client actions", () => {
  test("builds authenticated offered lien message and response URLs", () => {
    expect(buildFundingOfferedLienMessageUrl("019f9120-815c-748c-bb53-3fe73869cce7")).toBe(
      "/api/lien/api/liens/selling/buyer/liens/019f9120-815c-748c-bb53-3fe73869cce7/messages",
    );
    expect(buildFundingOfferedLienMessageUrl("id with space")).toBe(
      "/api/lien/api/liens/selling/buyer/liens/id%20with%20space/messages",
    );
    expect(buildFundingOfferedLienActionUrl("offer-1", "accept")).toBe(
      "/api/lien/api/liens/selling/buyer/liens/offer-1/accept",
    );
    expect(buildFundingOfferedLienActionUrl("offer-1", "decline")).toBe(
      "/api/lien/api/liens/selling/buyer/liens/offer-1/decline",
    );
  });

  test("does not submit blank messages", async () => {
    const fetchImpl = vi.fn();

    const result = await postFundingOfferedLienMessage("offer-1", "   ", [], { fetchImpl });

    expect(fetchImpl).not.toHaveBeenCalled();
    expect(result.ok).toBe(false);
    expect(result.error?.code).toBe("message-required");
  });

  test("posts trimmed messages and returns the created buyer message", async () => {
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

    const result = await postFundingOfferedLienMessage(
      "offer-1",
      "  Following up.  ",
      [],
      { fetchImpl },
    );

    expect(fetchImpl).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/buyer/liens/offer-1/messages",
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
    expect(result.message?.senderInitials).toBe("BR");
    expect(result.message?.isCurrentUser).toBe(true);
  });

  test("posts accept and decline responses with idempotency keys", async () => {
    const fetchImpl = vi
      .fn()
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ accessLink: { responseStatus: "Accepted" } }), {
          status: 200,
          headers: { "x-correlation-id": "corr-accept" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ accessLink: { responseStatus: "Declined" } }), {
          status: 200,
          headers: { "x-correlation-id": "corr-decline" },
        }),
      ) as unknown as typeof fetch;

    const acceptResult = await submitFundingOfferedLienResponse("offer-1", "accept", {
      fetchImpl,
      idempotencyKey: "idem-accept",
      notes: "Accepted in portal.",
    });
    const declineResult = await submitFundingOfferedLienResponse("offer-1", "decline", {
      fetchImpl,
      idempotencyKey: "idem-decline",
      reason: "Outside criteria.",
    });

    expect(fetchImpl).toHaveBeenNthCalledWith(
      1,
      "/api/lien/api/liens/selling/buyer/liens/offer-1/accept",
      {
        method: "POST",
        headers: {
          accept: "application/json",
          "content-type": "application/json",
          "Idempotency-Key": "idem-accept",
        },
        body: JSON.stringify({ notes: "Accepted in portal." }),
        cache: "no-store",
      },
    );
    expect(fetchImpl).toHaveBeenNthCalledWith(
      2,
      "/api/lien/api/liens/selling/buyer/liens/offer-1/decline",
      {
        method: "POST",
        headers: {
          accept: "application/json",
          "content-type": "application/json",
          "Idempotency-Key": "idem-decline",
        },
        body: JSON.stringify({ reason: "Outside criteria." }),
        cache: "no-store",
      },
    );
    expect(acceptResult.ok).toBe(true);
    expect(declineResult.ok).toBe(true);
  });

  test("maps Liens API errors", async () => {
    const fetchImpl = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          error: {
            code: "not_found",
            title: "Offered lien unavailable",
            message: "Offered lien not found.",
          },
        }),
        { status: 404 },
      ),
    );

    const result = await postFundingOfferedLienMessage("offer-1", "Following up.", [], {
      fetchImpl,
    });

    expect(result.ok).toBe(false);
    expect(result.status).toBe(404);
    expect(result.error?.message).toBe("Offered lien not found.");
  });

  test("posts offered lien message attachments as multipart form data", async () => {
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
              fileName: "xray-result.jpg",
              contentType: "image/jpeg",
              fileSizeBytes: 76800,
              createdAtUtc: "2026-07-28T12:30:00Z",
              viewUrl: "/view",
              downloadUrl: "/download",
            },
          ],
        }),
        { status: 201 },
      ),
    );
    const file = new File(["image"], "xray-result.jpg", { type: "image/jpeg" });

    const result = await postFundingOfferedLienMessage("offer-1", "  ", [file], {
      fetchImpl,
    });

    expect(fetchImpl).toHaveBeenCalledWith(
      "/api/lien/api/liens/selling/buyer/liens/offer-1/messages",
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
    expect(result.message?.attachments?.[0]?.fileName).toBe("xray-result.jpg");
  });
});
