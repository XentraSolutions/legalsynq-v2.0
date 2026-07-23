const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";

export interface PublicBuyerPortalData {
  accessLink: {
    createdAtUtc: string;
    expiresAtUtc: string;
    lastAccessedAtUtc: string | null;
    notificationSubmittedAtUtc: string | null;
    responseStatus?: string | null;
    responseAmount?: number | null;
    responseNotes?: string | null;
    respondedAtUtc?: string | null;
  };
  lien: {
    id: string;
    lienCode: string;
    status: string;
    sellerStatus: string | null;
    submittedAtUtc: string;
    listingVisibility: string | null;
    initialServiceDate: string | null;
    endServiceDate: string | null;
    originalAmount: number;
    askAmount: number | null;
    offerPrice: number | null;
    notes: string | null;
  };
  seller: {
    name: string | null;
    company: string | null;
    email: string | null;
  };
  buyer: {
    contactName: string | null;
    company: string | null;
    email: string | null;
  };
  case: {
    handlingLawFirm: string | null;
    caseManager: string | null;
  };
  documents: PublicBuyerPortalDocument[];
}

export interface PublicBuyerPortalDocument {
  fileName: string;
  category: string | null;
  sizeOrType: string;
}

export interface PublicBuyerPortalError {
  code: string;
  title: string;
  message: string;
}

export type PublicBuyerPortalResult =
  | {
      ok: true;
      status: number;
      correlationId: string | null;
      data: PublicBuyerPortalData;
    }
  | {
      ok: false;
      status: number;
      correlationId: string | null;
      error: PublicBuyerPortalError;
    };

export interface PublicBuyerPortalFetchOptions {
  fetchImpl?: typeof fetch;
  requestHost?: string | null;
  requestProto?: string | null;
}

export async function fetchPublicBuyerPortal(
  token: string,
  options: PublicBuyerPortalFetchOptions = {},
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
  const response = await fetcher(buildPublicBuyerPortalGatewayUrl(normalizedToken), {
    method: "GET",
    headers: buildPublicBuyerPortalHeaders(options),
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
    data: body as PublicBuyerPortalData,
  };
}

export function buildPublicBuyerPortalGatewayUrl(token: string): string {
  return `${GATEWAY_URL.replace(/\/+$/, "")}/liens/api/liens/selling/public/${encodeURIComponent(token)}`;
}

function buildPublicBuyerPortalHeaders(
  options: PublicBuyerPortalFetchOptions,
): Record<string, string> {
  const headers: Record<string, string> = {
    accept: "application/json",
  };

  if (options.requestHost)
    headers["x-legal-synq-public-host"] = options.requestHost;
  if (options.requestProto)
    headers["x-legal-synq-public-proto"] = options.requestProto.replace(/:$/, "");

  return headers;
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
    message: "The lien offer data could not be resolved.",
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
