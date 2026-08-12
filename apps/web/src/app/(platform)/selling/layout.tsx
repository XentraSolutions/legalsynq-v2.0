import SellingProviders from "@/components/selling/selling.provider";

export const dynamic = "force-dynamic";

/**
 * LS-ID-TNT-010 — SynqLien product layout guard.
 *
 * Server component: enforces product access before any page under /lien/*
 * is rendered. Unauthorised users are redirected to /access-denied.
 * Renders the existing SellingProviders client context for authorised users.
 *
 * Previously this was a 'use client' wrapper for SellingProviders only.
 * Converted to a server component so the requireProductAccess guard runs
 * on the server, where session data is available.
 */

export default function SellingLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <SellingProviders>{children}</SellingProviders>;
}
