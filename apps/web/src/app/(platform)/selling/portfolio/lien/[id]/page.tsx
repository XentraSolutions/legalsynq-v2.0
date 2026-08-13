"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { useSession } from "@/hooks/use-session";
import { ProductRole } from "@/types";
import { ApiError } from "@/lib/api-client";
import type { LienDetailsResult } from "@/types/lien-selling";
import { liensService } from "@/lib/selling";
import { PortfolioDetailPanel } from "@/components/selling/portfolio-details";

/**
 * /lien/portfolio/[id] — Held lien detail for buyers and holders.
 *
 * Shows full detail (subject identity revealed post-purchase, even if
 * the original listing was confidential).
 *
 * Phase 1: read-only. Phase 2 will add management actions (e.g. transfer).
 */
export default function PortfolioLienDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();

  const isBuyer =
    session?.productRoles.includes(ProductRole.SynqLienBuyer) ?? false;
  const isHolder =
    session?.productRoles.includes(ProductRole.SynqLienHolder) ?? false;

  const [lien, setLien] = useState<LienDetailsResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await liensService.getLienById(params?.id ?? "");
      setLien(data);
      setError(null);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isUnauthorized) {
          router.push("/login");
          return;
        }
        if (err.isNotFound) {
          setError("Lien not found in your portfolio.");
          return;
        }
        if (err.isForbidden) {
          setError("You do not have access to this lien.");
          return;
        }
        setError(err.message);
      } else {
        setError("Failed to load lien.");
      }
    } finally {
      setLoading(false);
    }
  }, [params?.id, router]);

  useEffect(() => {
    if (sessionLoading) return;
    if (!session) {
      router.push("/login");
      return;
    }
    if (!isBuyer && !isHolder) {
      router.push("/dashboard");
      return;
    }

    load();
  }, [session, sessionLoading, isBuyer, isHolder, router, load]);

  if (error) {
    return (
      <div className="space-y-4">
        <nav>
          <Link
            href="/selling/portfolio"
            className="text-sm text-gray-500 hover:text-gray-800"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
        </nav>
        <div className="bg-red-50 border border-red-200 rounded-lg px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      </div>
    );
  }

  if (!lien) return null;

  return (
    <div className="space-y-4">
      <nav>
        <Link
          href="/selling/portfolio"
          className="text-sm text-gray-500 hover:text-gray-800"
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
      </nav>

      <PortfolioDetailPanel lien={lien} onRefresh={load} />
    </div>
  );
}
