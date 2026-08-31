import type { PublicBuyerPortalError, PublicBuyerPortalMessage } from "./public-buyer-portal";

export interface PublicBuyerPortalMessageResult {
  ok: boolean;
  status: number;
  correlationId: string | null;
  message?: PublicBuyerPortalMessage;
  error?: PublicBuyerPortalError;
}

export interface PublicBuyerPortalMessageOptions {
  fetchImpl?: typeof fetch;
}

const MAX_PUBLIC_MESSAGE_LENGTH = 400;

export async function postPublicBuyerPortalMessage(
  token: string,
  message: string,
  files: File[] = [],
  options: PublicBuyerPortalMessageOptions = {},
): Promise<PublicBuyerPortalMessageResult> {
  const normalizedToken = token.trim();
  const trimmedMessage = message.trim();
  const hasFiles = files.length > 0;

  if (!normalizedToken) {
    return errorResult(
      400,
      "missing-token",
      "Lien offer link unavailable",
      "The secure link is missing from this request.",
    );
  }

  if (!trimmedMessage && !hasFiles) {
    return errorResult(
      400,
      "message-required",
      "Message could not be sent",
      "Enter a message or attach a file before sending.",
    );
  }

  if (trimmedMessage.length > MAX_PUBLIC_MESSAGE_LENGTH) {
    return errorResult(
      400,
      "message-too-long",
      "Message could not be sent",
      `Message must be ${MAX_PUBLIC_MESSAGE_LENGTH} characters or fewer.`,
    );
  }

  const fetcher = options.fetchImpl ?? fetch;

  try {
    const response = hasFiles
      ? await fetcher(buildPublicBuyerPortalMessageUrl(normalizedToken), {
          method: "POST",
          headers: {
            accept: "application/json",
          },
          body: buildMessageForm(trimmedMessage, files),
          cache: "no-store",
        })
      : await fetcher(buildPublicBuyerPortalMessageUrl(normalizedToken), {
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
        error: normalizePublicBuyerPortalError(body),
      };
    }

    return {
      ok: true,
      status: response.status,
      correlationId,
      message: body as PublicBuyerPortalMessage,
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

function buildMessageForm(message: string, files: File[]): FormData {
  const formData = new FormData();
  formData.set("message", message);
  for (const file of files) {
    formData.append("files", file, file.name);
  }
  return formData;
}

export function buildPublicBuyerPortalMessageUrl(token: string): string {
  return `/api/lien/api/liens/selling/public/${encodeURIComponent(token)}/messages`;
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

function normalizePublicBuyerPortalError(body: unknown): PublicBuyerPortalError {
  if (isPublicBuyerPortalErrorBody(body)) return body.error;

  return {
    code: "unavailable",
    title: "Message could not be sent",
    message: "The message could not be sent. Please try again.",
  };
}

function isPublicBuyerPortalErrorBody(
  body: unknown,
): body is { error: PublicBuyerPortalError } {
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
): PublicBuyerPortalMessageResult {
  return {
    ok: false,
    status,
    correlationId: null,
    error: { code, title, message },
  };
}
