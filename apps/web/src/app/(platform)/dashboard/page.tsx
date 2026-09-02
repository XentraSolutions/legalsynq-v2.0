import { headers } from "next/headers";
import { redirect } from "next/navigation";
import { requireOrg } from "@/lib/auth-guards";
import {
  PRODUCT_META,
  PRODUCT_NAV,
  orgTypeLabel,
  resolveEnabledNavKeys,
} from "@/lib/nav";
import { getServerPortalConfig } from "@/lib/portal";
import { Button } from "@/components/ui/button";
import Link from "next/link";
import { NavItem } from "@/types";
import { Building2, UsersRound, type LucideIcon } from "lucide-react";

export const dynamic = "force-dynamic";

/**
 * Dashboard — default landing page after login.
 * Shows a welcome card and quick-access tiles for each product.
 */
export default async function DashboardPage() {
  const session = await requireOrg();

  // Portal-specific overrides: redirect to the portal's own landing page.
  const headersList = await headers();
  const rawHost =
    headersList.get("x-forwarded-host") ?? headersList.get("host") ?? "";
  const portalConfig = getServerPortalConfig(rawHost);
  if (portalConfig) redirect(portalConfig.landingPath);

  // Filter product tiles to only those enabled for this tenant.
  const productList = session.userProducts?.length
    ? session.userProducts
    : (session.enabledProducts ?? []);
  const enabledKeys = resolveEnabledNavKeys(productList);

  // Liens buying and selling are presented as one "Synq Liens" tile with
  // Buying/Selling sub-items, rather than two separate product tiles.
  const hasLienBuying = enabledKeys.has("lien");
  const hasLienSelling = enabledKeys.has("selling");
  const productEntries = Object.entries(PRODUCT_META).filter(([id]) => {
    if (id === "selling") return false;
    if (id === "lien") return hasLienBuying || hasLienSelling;
    return enabledKeys.has(id);
  });

  return (
    <div className="space-y-10">
      {/* Welcome header */}
      <div>
        <h1 className="text-[32px] font-bold leading-10 text-[#0A0A0A]">
          Welcome back{session.orgName ? `, ${session.orgName}` : ""}
        </h1>
        <p className="text-base font-normal leading-[160%] text-[#737373] mt-2">
          {orgTypeLabel(session.orgType)} · {session.email}
        </p>
      </div>

      {/* Product tiles */}
      <div>
        <p className="text-[20px] font-medium leading-7 text-[#A3A3A3] mb-4">Your Products</p>
        {productEntries.length > 0 ? (
          <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
            {productEntries.map(([id, meta]) => (
              <ProductCard
                key={id}
                id={id}
                meta={meta}
                items={id === "lien" ? buildLienItems(hasLienBuying, hasLienSelling) : []}
                primaryHref={firstNavHref(id)}
              />
            ))}
          </div>
        ) : (
          <div className="rounded-xl border border-dashed border-gray-200 bg-white px-5 py-8 text-sm text-gray-400">
            No products assigned.
          </div>
        )}
      </div>

      {/* Admin shortcut */}
      {(session.isTenantAdmin || session.isPlatformAdmin) && (
        <div>
          <p className="text-[20px] font-medium leading-7 text-[#A3A3A3] mb-4">Administration</p>
          <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
            <AdminCard
              href="/admin/users"
              icon={UsersRound}
              label="Users"
              description="Manage users and their roles."
            />
            <AdminCard
              href="/admin/organizations"
              icon={Building2}
              label="Organizations"
              description="View and manage organizations."
            />
          </div>
        </div>
      )}
    </div>
  );
}

// Builds the Buying/Selling sub-items for the combined "Synq Liens" tile.
function buildLienItems(
  hasBuying: boolean,
  hasSelling: boolean,
): (NavItem & { href: string })[] {
  const items: (NavItem & { href: string })[] = [];
  if (hasBuying) {
    items.push({
      href: PRODUCT_NAV.lien?.[0]?.items[0]?.href ?? "/lien/dashboard",
      label: "Buying",
    });
  }
  if (hasSelling) {
    items.push({
      href: PRODUCT_NAV.selling?.[0]?.items[0]?.href ?? "/selling/dashboard",
      label: "Selling",
    });
  }
  return items;
}

// Resolves the first navigable link for a product, used as the plain card's
// single arrow-button target.
function firstNavHref(id: string): string {
  for (const section of PRODUCT_NAV[id] ?? []) {
    for (const item of section.items) {
      if (item.href) return item.href;
      if (item.children?.[0]?.href) return item.children[0].href;
    }
  }
  return "#";
}

// ── Product card ──────────────────────────────────────────────────────────────

function ProductCard({
  meta,
  items,
  primaryHref,
}: {
  id: string;
  meta: {
    label: string;
    icon: string;
    color: string;
    iconSrc: string;
    description: string;
  };
  items: (NavItem & { href: string })[];
  primaryHref: string;
}) {
  return (
    <div className="flex h-full flex-col rounded-xl border border-gray-200 bg-white p-6 hover:shadow-md hover:border-gray-300 transition-all">
      {meta.iconSrc ? (
        <img
          src={meta.iconSrc}
          alt=""
          aria-hidden
          className="w-10 h-10 object-contain mb-4"
        />
      ) : (
        <i
          className={`${meta.icon} text-3xl mb-4`}
          style={{ color: meta.color }}
        />
      )}

      <p className="text-[20px] font-medium leading-7 text-[#0A0A0A]">{meta.label}</p>
      <p className="mt-1.5 text-base font-normal leading-[160%] text-[#737373]">{meta.description}</p>

      {items.length > 0 ? (
        <div className="mt-4 rounded-lg border border-gray-200">
          {items.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="group flex items-center justify-between px-4 py-3 border-b border-gray-200 last:border-b-0 hover:bg-gray-50 transition-colors"
            >
              <span className="text-base font-medium leading-[160%] text-[#404040]">{item.label}</span>
              <Button
                variant="icon-rounded"
                tabIndex={-1}
                className="pointer-events-none"
              >
                <i className="ri-arrow-right-line text-sm" />
              </Button>
            </Link>
          ))}
        </div>
      ) : (
        <div className="mt-4 flex flex-1 items-end justify-end">
          <Link href={primaryHref}>
            <Button
              variant="icon-rounded"
              tabIndex={-1}
              className="pointer-events-none"
            >
              <i className="ri-arrow-right-line text-base" />
            </Button>
          </Link>
        </div>
      )}
    </div>
  );
}

// ── Admin card ────────────────────────────────────────────────────────────────

function AdminCard({
  href,
  icon,
  label,
  description,
}: {
  href: string;
  icon: LucideIcon;
  label: string;
  description: string;
}) {
  const Icon = icon;
  return (
    <Link
      href={href}
      className="group flex items-start gap-4 rounded-xl border border-gray-200 bg-white p-6 hover:shadow-md hover:border-gray-300 transition-all"
    >
      <div className="inline-flex items-center justify-center w-11 h-11 rounded-xl bg-gray-100 shrink-0">
        <Icon className="w-5 h-5 text-[#0f1928]" />
      </div>
      <div className="flex flex-col items-start gap-2">
        <p className="text-[20px] font-medium leading-7 text-[#0A0A0A] group-hover:text-orange-600 transition-colors">
          {label}
        </p>
        <p className="text-base font-normal leading-[160%] text-[#737373]">{description}</p>
      </div>
    </Link>
  );
}
