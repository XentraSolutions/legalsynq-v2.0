import type { PublicBuyerPortalError } from "@/lib/liens/public-buyer-portal";
import type { OfferedLienAction, OfferedLienMessage } from "./types";

export type FundingOfferedLienResponseAction = Extract<OfferedLienAction, "accept" | "decline">;

export interface FundingOfferedLienMessageResult {
  ok: boolean;
  status: number;
  correlationId: string | null;
  message?: OfferedLienMessage;
  error?: PublicBuyerPortalError;
}

export interface FundingOfferedLienResponseResult {
  ok: boolean;
  status: number;
  correlationId: string | null;
  error?: PublicBuyerPortalError;
}

export interface FundingOfferedLienMessageOptions {
  fetchImpl?: typeof fetch;
}

export interface FundingOfferedLienResponseOptions {
  fetchImpl?: typeof fetch;
  idempotencyKey?: string;
  notes?: string;
  reason?: string;
}

const MAX_MESSAGE_LENGTH = 400;

export async function postFundingOfferedLienMessage(
  id: string,
  message: string,
  options: FundingOfferedLienMessageOptions = {},
): Promise<FundingOfferedLienMessageResult> {
  const normalizedId = id.trim();
  const trimmedMessage = message.trim();

  if (!normalizedId) {
    return errorResult(
      400,
      "missing-offered-lien",
      "Offered lien unavailable",
      "The offered lien is missing from this request.",
    );
  }

  if (!trimmedMessage) {
    return errorResult(
      400,
      "message-required",
      "Message could not be sent",
      "Enter a message before sending.",
    );
  }

  if (trimmedMessage.length > MAX_MESSAGE_LENGTH) {
    return errorResult(
      400,
      "message-too-long",
      "Message could not be sent",
      `Message must be ${MAX_MESSAGE_LENGTH} characters or fewer.`,
    );
  }

  const fetcher = options.fetchImpl ?? fetch;

  try {
    const response = await fetcher(buildFundingOfferedLienMessageUrl(normalizedId), {
      method: "POST",
      headers: {
        accept: "application/json",
        "content-type": "application/json",
      },
      body: JSON.stringify({ message: trimmedMessage }),
      cache: "no-store",
    });
    const correlationId = response.headers.get("x-correlation-id");
    const body = await readJson(response);

    if (!response.ok) {
      return {
        ok: false,
        status: response.status,
        correlationId,
        error: normalizeError(body, "Message could not be sent", "The message could not be sent. Please try again."),
      };
    }

    return {
      ok: true,
      status: response.status,
      correlationId,
      message: normalizeMessage(body),
    };
  } catch {
    return errorResult(
      0,
      "network-error",
      "Message could not be sent",
      "Network error. Please check your connection and try again.",
    );
  }
}

export async function submitFundingOfferedLienResponse(
  id: string,
  action: FundingOfferedLienResponseAction,
  options: FundingOfferedLienResponseOptions = {},
): Promise<FundingOfferedLienResponseResult> {
  const normalizedId = id.trim();
  if (!normalizedId) {
    return errorResult(
      400,
      "missing-offered-lien",
      "Offered lien unavailable",
      "The offered lien is missing from this request.",
    );
  }

  const fetcher = options.fetchImpl ?? fetch;

  try {
    const response = await fetcher(buildFundingOfferedLienActionUrl(normalizedId, action), {
      method: "POST",
      headers: buildActionHeaders(options.idempotencyKey),
      body: JSON.stringify(buildActionBody(action, options)),
      cache: "no-store",
    });
    const correlationId = response.headers.get("x-correlation-id");
    const body = await readJson(response);

    if (!response.ok) {
      return {
        ok: false,
        status: response.status,
        correlationId,
        error: normalizeError(
          body,
          "Lien offer unavailable",
          "The lien offer response could not be recorded.",
        ),
      };
    }

    return {
      ok: true,
      status: response.status,
      correlationId,
    };
  } catch {
    return errorResult(
      0,
      "network-error",
      "Lien offer unavailable",
      "The lien offer response could not be recorded.",
    );
  }
}

export function buildFundingOfferedLienMessageUrl(id: string): string {
  return `${buildFundingOfferedLienUrl(id)}/messages`;
}

export function buildFundingOfferedLienActionUrl(
  id: string,
  action: FundingOfferedLienResponseAction,
): string {
  return `${buildFundingOfferedLienUrl(id)}/${action}`;
}

function buildFundingOfferedLienUrl(id: string): string {
  return `/api/lien/api/liens/selling/buyer/liens/${encodeURIComponent(id)}`;
}

function buildActionHeaders(idempotencyKey?: string): Record<string, string> {
  const headers: Record<string, string> = {
    accept: "application/json",
    "content-type": "application/json",
  };

  const key = idempotencyKey?.trim() || createIdempotencyKey();
  if (key) headers["Idempotency-Key"] = key;
  return headers;
}

function buildActionBody(
  action: FundingOfferedLienResponseAction,
  options: FundingOfferedLienResponseOptions,
): Record<string, string> {
  if (action === "accept")
    return options.notes ? { notes: options.notes } : {};

  return options.reason ? { reason: options.reason } : {};
}

function createIdempotencyKey(): string | null {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return `funding-offered-lien-response-${crypto.randomUUID()}`;
  }

  return null;
}

async function readJson(response: Response): Promise<unknown> {
  const text = await response.text();
  if (!text.trim()) return null;

  try {
    return JSON.parse(text) as unknown;
  } catch {
    return null;
  }
}

function normalizeMessage(body: unknown): OfferedLienMessage | undefined {
  if (!body || typeof body !== "object") return undefined;
  const candidate = body as Partial<OfferedLienMessage>;
  if (
    typeof candidate.id !== "string" ||
    typeof candidate.senderType !== "string" ||
    typeof candidate.senderName !== "string" ||
    typeof candidate.message !== "string" ||
    typeof candidate.createdAtUtc !== "string"
  ) {
    return undefined;
  }

  return {
    id: candidate.id,
    senderType: candidate.senderType,
    senderName: candidate.senderName,
    senderInitials: candidate.senderInitials ?? buildInitials(candidate.senderName),
    senderEmail: typeof candidate.senderEmail === "string" ? candidate.senderEmail : null,
    message: candidate.message,
    createdAtUtc: candidate.createdAtUtc,
    isCurrentUser: true,
  };
}

function normalizeError(
  body: unknown,
  fallbackTitle: string,
  fallbackMessage: string,
): PublicBuyerPortalError {
  if (isErrorBody(body)) return body.error;

  return {
    code: "unavailable",
    title: fallbackTitle,
    message: fallbackMessage,
  };
}

function isErrorBody(body: unknown): body is { error: PublicBuyerPortalError } {
  if (!body || typeof body !== "object" || !("error" in body)) return false;
  const error = (body as { error?: unknown }).error;
  if (!error || typeof error !== "object") return false;
  const candidate = error as Partial<PublicBuyerPortalError>;
  return (
    typeof candidate.code === "string" &&
    typeof candidate.title === "string" &&
    typeof candidate.message === "string"
  );
}

function errorResult(
  status: number,
  code: string,
  title: string,
  message: string,
): FundingOfferedLienMessageResult & FundingOfferedLienResponseResult {
  return {
    ok: false,
    status,
    correlationId: null,
    error: { code, title, message },
  };
}

function buildInitials(value: string): string {
  const parts = value.split(/\s+/).filter(Boolean);
  if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
  return (value.slice(0, 2) || "SL").toUpperCase();
}
