const GATEWAY_URL = process.env.GATEWAY_URL ?? "http://127.0.0.1:5010";
const SYNQLIEN_BUYER_ACTIVATION_REASON = "synqlien-buyer-activation";
const SYNQLIEN_BUYER_DASHBOARD_RETURN_TO = "/funding/dashboard";
const SYNQLIEN_BUYER_OFFERED_LIENS_RETURN_TO = "/funding/offered-liens";

export const SYNQLIEN_BUYER_LOGIN_URL =
  "/login?returnTo=%2Ffunding%2Fdashboard&reason=synqlien-buyer-activation";

export function normalizeSynqLienBuyerLoginUrl(
  loginUrl: string | null | undefined,
): string {
  const candidate = loginUrl?.trim();
  if (!candidate) return SYNQLIEN_BUYER_LOGIN_URL;

  try {
    const isAbsolute = /^[a-z][a-z\d+\-.]*:/i.test(candidate);
    const parsed = new URL(candidate, "https://portal.legalsynq.local");

    if (
      parsed.pathname === "/login" &&
      parsed.searchParams.get("reason") === SYNQLIEN_BUYER_ACTIVATION_REASON &&
      parsed.searchParams.get("returnTo") === SYNQLIEN_BUYER_OFFERED_LIENS_RETURN_TO
    ) {
      parsed.searchParams.set("returnTo", SYNQLIEN_BUYER_DASHBOARD_RETURN_TO);
      return isAbsolute ? parsed.toString() : `${parsed.pathname}${parsed.search}${parsed.hash}`;
    }
  } catch {
    return candidate;
  }

  return candidate;
}

export interface PublicBuyerPortalData {
  audience: "buyer" | "seller";
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
    phone?: string | null;
  };
  case: {
    handlingLawFirm: string | null;
    handlingLawFirmContactName?: string | null;
    handlingLawFirmEmail?: string | null;
    caseManager: string | null;
  };
  documents: PublicBuyerPortalDocument[];
  messages?: PublicBuyerPortalMessage[];
  account?: PublicBuyerPortalAccount | null;
}

export interface PublicBuyerPortalAccount {
  hasExistingAccount: boolean;
  loginUrl: string;
}

export interface PublicBuyerPortalDocument {
  id?: string | null;
  fileName: string;
  category: string | null;
  sizeOrType: string;
  viewUrl?: string | null;
  downloadUrl?: string | null;
}

export interface PublicBuyerPortalMessage {
  id: string;
  senderType: "buyer" | "seller";
  senderName: string;
  senderEmail: string | null;
  message: string;
  createdAtUtc: string;
  attachments?: PublicBuyerPortalMessageAttachment[];
}

export interface PublicBuyerPortalMessageAttachment {
  id: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  createdAtUtc: string;
  viewUrl?: string | null;
  downloadUrl?: string | null;
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
    data: normalizePublicBuyerPortalData(body as PublicBuyerPortalData),
  };
}

export function normalizePublicBuyerPortalData(
  data: PublicBuyerPortalData,
): PublicBuyerPortalData {
  if (!data.account) return data;

  return {
    ...data,
    account: {
      ...data.account,
      loginUrl: normalizeSynqLienBuyerLoginUrl(data.account.loginUrl),
    },
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
