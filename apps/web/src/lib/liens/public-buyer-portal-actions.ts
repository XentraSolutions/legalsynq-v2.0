import type {
  PublicBuyerPortalData,
  PublicBuyerPortalError,
  PublicBuyerPortalResult,
} from "./public-buyer-portal";

export type PublicBuyerPortalResponseAction = "accept" | "decline";

export interface PublicBuyerPortalResponseOptions {
  fetchImpl?: typeof fetch;
  idempotencyKey?: string;
  notes?: string;
  reason?: string;
}

export async function submitPublicBuyerPortalResponse(
  token: string,
  action: PublicBuyerPortalResponseAction,
  options: PublicBuyerPortalResponseOptions = {},
): Promise<PublicBuyerPortalResult> {
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
  const request = buildPublicBuyerPortalActionRequest(action, options);
  const result = await sendPublicBuyerPortalResponse(
    fetcher,
    buildPublicBuyerPortalActionUrl(normalizedToken, action),
    request,
  );

  if (!result.ok) {
    return result.error;
  }

  const { response, body } = result;

  const correlationId = response.headers.get("x-correlation-id");

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
    data: body as PublicBuyerPortalData,
  };
}

export function buildPublicBuyerPortalActionUrl(
  token: string,
  action: PublicBuyerPortalResponseAction,
): string {
  return buildPublicBuyerPortalActionUrls(token, action)[0];
}

export function buildPublicBuyerPortalActionUrls(
  token: string,
  action: PublicBuyerPortalResponseAction,
): string[] {
  return [buildPublicBuyerPortalBffActionUrl(token, action)];
}

export function buildPublicBuyerPortalBffActionUrl(
  token: string,
  action: PublicBuyerPortalResponseAction,
): string {
  return `/api/lien/api/liens/selling/public/${encodeURIComponent(token)}/${action}`;
}

function buildPublicBuyerPortalActionRequest(
  action: PublicBuyerPortalResponseAction,
  options: PublicBuyerPortalResponseOptions,
): RequestInit {
  return {
    method: "POST",
    headers: buildActionHeaders(options.idempotencyKey),
    body: JSON.stringify(buildActionBody(action, options)),
    cache: "no-store",
  };
}

async function sendPublicBuyerPortalResponse(
  fetcher: typeof fetch,
  url: string,
  request: RequestInit,
): Promise<
  | {
      ok: true;
      response: Response;
      body: unknown;
    }
  | {
      ok: false;
      error: PublicBuyerPortalResult;
    }
> {
  try {
    const response = await fetcher(url, request);
    const body = await readJson(response);
    return { ok: true, response, body };
  } catch {
    return {
      ok: false,
      error: networkErrorResult(),
    };
  }
}

function networkErrorResult(): PublicBuyerPortalResult {
  return {
    ok: false,
    status: 0,
    correlationId: null,
    error: {
      code: "network-error",
      title: "Lien offer unavailable",
      message: "The lien offer response could not be recorded.",
    },
  };
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
  action: PublicBuyerPortalResponseAction,
  options: PublicBuyerPortalResponseOptions,
): Record<string, string> {
  if (action === "accept")
    return options.notes ? { notes: options.notes } : {};

  return options.reason ? { reason: options.reason } : {};
}

function createIdempotencyKey(): string | null {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return `public-buyer-response-${crypto.randomUUID()}`;
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
    code: "unavailable",
    title: "Lien offer unavailable",
    message: "The lien offer response could not be recorded.",
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
