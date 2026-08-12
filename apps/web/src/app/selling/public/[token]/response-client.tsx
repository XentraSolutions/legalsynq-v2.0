"use client";

import { useState } from "react";
import {
  SYNQLIEN_BUYER_LOGIN_URL,
  type PublicBuyerPortalData,
} from "@/lib/liens/public-buyer-portal";
import {
  submitPublicBuyerPortalResponse,
  type PublicBuyerPortalResponseAction,
} from "@/lib/liens/public-buyer-portal-actions";

interface PublicBuyerPortalInteractiveContentProps {
  token: string;
  data: PublicBuyerPortalData;
}

interface FieldRow {
  label: string;
  value: string | number | null | undefined;
  href?: string;
}

export function PublicBuyerPortalInteractiveContent({
  token,
  data,
}: PublicBuyerPortalInteractiveContentProps) {
  const [portalData, setPortalData] = useState(data);
  const [submitting, setSubmitting] = useState<PublicBuyerPortalResponseAction | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function handleResponse(action: PublicBuyerPortalResponseAction) {
    setSubmitting(action);
    setError(null);

    const result = await submitPublicBuyerPortalResponse(token, action);
    setSubmitting(null);

    if (result.ok) {
      setPortalData(result.data);
      return;
    }

    setError(result.error.message);
  }

  return (
    <>
      {portalData.audience === "seller" ? null : (
        <ResponseCard
          data={portalData}
          submitting={submitting}
          error={error}
          onRespond={handleResponse}
        />
      )}
      <LienSummaryCard data={portalData} />
    </>
  );
}

function ResponseCard({
  data,
  submitting,
  error,
  onRespond,
}: {
  data: PublicBuyerPortalData;
  submitting: PublicBuyerPortalResponseAction | null;
  error: string | null;
  onRespond: (action: PublicBuyerPortalResponseAction) => void;
}) {
  const responseStatus = normalizeResponseStatus(data.accessLink.responseStatus);
  const hasResponded = Boolean(responseStatus);
  const accepted = responseStatus === "Accepted";
  const declined = responseStatus === "Declined";
  const disabled = hasResponded || Boolean(submitting);

  return (
    <section
      className="flex w-full max-w-[700px] flex-col gap-10 rounded-2xl border border-[#e5e5e5] bg-white p-6 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="response-title"
    >
      <div className="flex flex-col gap-2">
        <h2 id="response-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
          Your Response
        </h2>
        <p className="m-0 text-base leading-[1.6] text-[#737373]">
          Respond directly from this page, or log in to your funding company dashboard.
        </p>
      </div>
      <div className="flex flex-col gap-2">
        <div className="flex gap-3 max-sm:flex-col">
          <button
            type="button"
            disabled={disabled}
            onClick={() => onRespond("accept")}
            className="public-portal-primary inline-flex h-[38px] flex-1 cursor-pointer items-center justify-center rounded-[10px] border border-transparent px-4 py-2 text-sm font-semibold leading-[1.6] text-white shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:shadow-[0_4px_10px_rgba(238,113,50,0.24)] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting === "accept" ? "Accepting..." : accepted ? "Accepted" : "Accept Lien"}
          </button>
          <button
            type="button"
            disabled={disabled}
            onClick={() => onRespond("decline")}
            className="inline-flex h-[38px] flex-1 cursor-pointer items-center justify-center rounded-[10px] border border-red-600 bg-white px-4 py-2 text-sm font-semibold leading-[1.6] text-red-600 shadow-[0_1px_2px_rgba(0,0,0,0.1)] transition-colors hover:bg-red-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600 active:bg-red-100 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting === "decline" ? "Declining..." : declined ? "Declined" : "Decline Lien"}
          </button>
        </div>
        <p className="m-0 text-sm leading-[1.6] text-[#737373]">
          {hasResponded ? "Your response was securely recorded." : "Your response is securely recorded."}{" "}
          <a
            href={data.account?.loginUrl || SYNQLIEN_BUYER_LOGIN_URL}
            className="cursor-pointer text-[#ee7132] underline underline-offset-2 transition-colors hover:text-[#d85f25]"
          >
            Log in
          </a>{" "}
          to manage from your dashboard.
        </p>
        {error ? (
          <p role="alert" className="m-0 text-sm font-semibold leading-[1.6] text-red-600">
            {error}
          </p>
        ) : null}
      </div>
    </section>
  );
}

function LienSummaryCard({ data }: { data: PublicBuyerPortalData }) {
  const isSellerView = data.audience === "seller";
  const sellerRows: FieldRow[] = [
    { label: "Seller Name", value: data.seller.name },
    { label: "Seller Company", value: data.seller.company },
    {
      label: "Email Address",
      value: data.seller.email,
      href: data.seller.email ? `mailto:${data.seller.email}` : undefined,
    },
  ];
  const buyerRows: FieldRow[] = [
    { label: "Buyer Name", value: data.buyer.contactName },
    { label: "Funding Company", value: data.buyer.company },
    {
      label: "Email Address",
      value: data.buyer.email,
      href: data.buyer.email ? `mailto:${data.buyer.email}` : undefined,
    },
    { label: "Phone Number", value: data.buyer.phone },
  ];
  const lienRows: FieldRow[] = [
    { label: "Submitted Date", value: formatDateTime(data.lien.submittedAtUtc) },
    { label: "Listing Visibility", value: data.lien.listingVisibility },
    { label: "Initial Service Date", value: formatDate(data.lien.initialServiceDate) },
    { label: "End Service Date", value: formatDate(data.lien.endServiceDate) },
  ];
  const fundingRows: FieldRow[] = [
    { label: "Funding Company", value: data.buyer.company },
    { label: "Handling Law Firm", value: data.case.handlingLawFirm },
    { label: "Contact Person", value: data.case.handlingLawFirmContactName },
    { label: "Case Manager", value: data.case.caseManager },
    {
      label: "Email Address",
      value: data.case.handlingLawFirmEmail,
      href: data.case.handlingLawFirmEmail
        ? `mailto:${data.case.handlingLawFirmEmail}`
        : undefined,
    },
  ];
  const caseRows: FieldRow[] = [
    { label: "Handling Law Firm", value: data.case.handlingLawFirm },
    { label: "Contact Person", value: data.case.handlingLawFirmContactName },
    { label: "Case Manager", value: data.case.caseManager },
    {
      label: "Email Address",
      value: data.case.handlingLawFirmEmail,
      href: data.case.handlingLawFirmEmail
        ? `mailto:${data.case.handlingLawFirmEmail}`
        : undefined,
    },
  ];

  return (
    <details
      open
      className="public-portal-details group w-full max-w-[700px] rounded-2xl border border-[#e5e5e5] bg-white px-6 py-6 pb-2 shadow-[0_1px_1.5px_rgba(0,0,0,0.1)] max-sm:rounded-[14px]"
      aria-labelledby="lien-summary-title"
    >
      <summary className="-mx-2 flex min-h-10 cursor-pointer list-none items-center justify-between gap-4 rounded-lg px-2 py-1 transition-colors hover:bg-[#f5f5f5] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#ee7132] max-sm:flex-col max-sm:items-start [&::-webkit-details-marker]:hidden">
        <div className="flex items-center gap-3">
          <i className="ri-arrow-down-s-line -rotate-90 text-2xl leading-none text-[#0a0a0a] transition-transform group-open:rotate-0" aria-hidden="true" />
          <h2 id="lien-summary-title" className="m-0 text-lg font-extrabold leading-[1.6] tracking-normal">
            Lien Summary
          </h2>
        </div>
        <ResponseStatusBadge
          status={data.accessLink.responseStatus}
          fallbackStatus={isSellerView ? data.lien.status : undefined}
        />
      </summary>
      <div className="details-content mt-6 flex flex-col gap-6">
        <FieldSection
          title={isSellerView ? "Buyer Information" : "Seller Information"}
          icon="ri-file-text-line"
          rows={isSellerView ? buyerRows : sellerRows}
        />
        <FieldSection
          title="Lien Information"
          icon="ri-file-text-line"
          rows={lienRows}
          notes={data.lien.notes}
        />
        <FieldSection
          title={isSellerView ? "Case Information" : "Funding Company & Case Information"}
          icon="ri-building-2-line"
          rows={isSellerView ? caseRows : fundingRows}
        />
      </div>
    </details>
  );
}

function ResponseStatusBadge({
  status,
  fallbackStatus,
}: {
  status?: string | null;
  fallbackStatus?: string | null;
}) {
  const presentation = resolveStatusPresentation(status, fallbackStatus);
  const className =
    presentation.responseStatus === "Accepted"
      ? "bg-emerald-500/15 text-emerald-700"
      : presentation.responseStatus === "Declined"
        ? "bg-red-500/15 text-red-700"
        : "bg-yellow-500/15 text-[#a16207]";

  return (
    <span className={`inline-flex h-7 items-center justify-center whitespace-nowrap rounded-full px-3 py-1 text-sm font-semibold leading-[1.6] max-sm:text-xs ${className}`}>
      {presentation.label}
    </span>
  );
}

function FieldSection({
  title,
  icon,
  rows,
  notes,
}: {
  title: string;
  icon: string;
  rows: FieldRow[];
  notes?: string | null;
}) {
  const visibleRows = rows.filter(row => row.value !== null && row.value !== undefined && `${row.value}`.trim());
  if (visibleRows.length === 0 && !notes) return null;

  return (
    <section className="border-b border-[#e5e5e5] pb-4 last:border-b-0">
      <div className="mb-4 flex items-center gap-2 text-sm font-bold leading-[1.6] text-[#0a0a0a]">
        <i className={`${icon} text-lg leading-none`} aria-hidden="true" />
        <span>{title}</span>
      </div>
      {visibleRows.length > 0 ? (
        <div className="grid grid-cols-2 gap-x-12 gap-y-4 max-sm:grid-cols-1 max-sm:gap-y-3.5">
          {visibleRows.map(row => (
            <div key={row.label} className="flex min-w-0 flex-col gap-1.5">
              <span className="text-sm leading-[1.6] text-[#737373]">{row.label}</span>
              {row.href ? (
                <a
                  href={row.href}
                  className="break-words text-sm font-semibold leading-[1.6] text-[#0a0a0a] no-underline"
                >
                  {row.value}
                </a>
              ) : (
                <span className="break-words text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
                  {row.value}
                </span>
              )}
            </div>
          ))}
        </div>
      ) : null}
      {notes ? (
        <div className="mt-4 flex flex-col gap-1.5">
          <span className="text-sm leading-[1.6] text-[#737373]">Lien Notes</span>
          <p className="m-0 text-sm font-semibold leading-[1.6] text-[#0a0a0a]">
            {notes}
          </p>
        </div>
      ) : null}
    </section>
  );
}

function normalizeResponseStatus(status?: string | null): "Accepted" | "Declined" | null {
  if (status === "Accepted" || status === "Declined") return status;
  return null;
}

function resolveStatusPresentation(
  responseStatus?: string | null,
  fallbackStatus?: string | null,
): { label: string; responseStatus: "Accepted" | "Declined" | null } {
  const normalized = normalizeResponseStatus(responseStatus) ?? normalizeResponseStatus(fallbackStatus);
  return {
    label: normalized ?? formatStatusLabel(fallbackStatus) ?? "Awaiting Your Response",
    responseStatus: normalized,
  };
}

function formatStatusLabel(status?: string | null): string | null {
  const trimmed = status?.trim();
  if (!trimmed) return null;

  return trimmed
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ");
}

function formatDate(value: string | null): string | null {
  if (!value) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return value;
  return `${match[2]}/${match[3]}/${match[1]}`;
}

function formatDateTime(value: string | null): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;

  const datePart = new Intl.DateTimeFormat("en-US", {
    timeZone: "UTC",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
  const timePart = new Intl.DateTimeFormat("en-US", {
    timeZone: "UTC",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(date);

  return `${datePart} - ${timePart}`;
}
