import {
  normalizeSynqLienBuyerLoginUrl,
  type PublicBuyerPortalError,
} from "./public-buyer-portal";

export interface PublicBuyerPortalActivationRequest {
  companyName?: string;
  email?: string;
  firstName: string;
  lastName?: string;
  phone?: string;
  password: string;
}

export interface PublicBuyerPortalActivationData {
  userId: string;
  isNew: boolean;
  loginUrl: string;
}

export type PublicBuyerPortalActivationResult =
  | {
      ok: true;
      status: number;
      correlationId: string | null;
      data: PublicBuyerPortalActivationData;
    }
  | {
      ok: false;
      status: number;
      correlationId: string | null;
      error: PublicBuyerPortalError;
    };

export interface PublicBuyerPortalActivationOptions {
  fetchImpl?: typeof fetch;
  idempotencyKey?: string;
}

export async function activatePublicBuyerPortalAccount(
  token: string,
  request: PublicBuyerPortalActivationRequest,
  options: PublicBuyerPortalActivationOptions = {},
): Promise<PublicBuyerPortalActivationResult> {
  const normalizedToken = token.trim();
  if (!normalizedToken) {
    return {
      ok: false,
      status: 400,
      correlationId: null,
      error: {
        code: "missing-token",
        title: "Lien offer link unavailable",
        message: "The secure link is missing from this request.",
      },
    };
  }

  const fetcher = options.fetchImpl ?? fetch;

  try {
    const response = await fetcher(buildPublicBuyerPortalActivationUrl(normalizedToken), {
      method: "POST",
      headers: buildActivationHeaders(options.idempotencyKey),
      body: JSON.stringify(request),
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
      data: normalizePublicBuyerPortalActivationData(body as PublicBuyerPortalActivationData),
    };
  } catch {
    return {
      ok: false,
      status: 0,
      correlationId: null,
      error: {
        code: "network-error",
        title: "Account activation failed",
        message: "The account activation request could not be completed.",
      },
    };
  }
}

function normalizePublicBuyerPortalActivationData(
  data: PublicBuyerPortalActivationData,
): PublicBuyerPortalActivationData {
  return {
    ...data,
    loginUrl: normalizeSynqLienBuyerLoginUrl(data.loginUrl),
  };
}

export function buildPublicBuyerPortalActivationUrl(token: string): string {
  return `/api/lien/api/liens/selling/public/${encodeURIComponent(token)}/activate-account`;
}

function buildActivationHeaders(idempotencyKey?: string): Record<string, string> {
  const headers: Record<string, string> = {
    accept: "application/json",
    "content-type": "application/json",
  };

  const key = idempotencyKey?.trim() || createIdempotencyKey();
  if (key) headers["Idempotency-Key"] = key;
  return headers;
}

function createIdempotencyKey(): string | null {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return `public-buyer-activation-${crypto.randomUUID()}`;
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

function normalizePublicBuyerPortalError(body: unknown): PublicBuyerPortalError {
  if (isPublicBuyerPortalErrorBody(body)) return body.error;

  return {
    code: "activation-failed",
    title: "Account activation failed",
    message: "Account activation could not be completed.",
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
