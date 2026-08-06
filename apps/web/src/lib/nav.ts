import type {
  NavSection,
  PlatformSession,
  ProductRoleValue,
  OrgTypeValue,
} from "@/types";
import { ProductRole, OrgType } from "@/types";

// ── Per-product sidebar navigation (sections) ─────────────────────────────────

export const PRODUCT_NAV: Record<string, NavSection[]> = {
  careconnect: [
    {
      items: [
        {
          href: "/careconnect/dashboard",
          label: "Dashboard",
          icon: "ri-dashboard-line",
        },
        {
          href: "/careconnect/referrals",
          label: "Referrals",
          icon: "ri-file-list-3-line",
          badgeKey: "newReferrals",
        },
        // CC-REFERRER-BROWSE: for elevated law firm referrers (tenant portal).
        // Hidden from network managers AND from lien-owner orgs (they manage their
        // own network; they never browse other networks).
        {
          href: "/careconnect/browse-networks",
          label: "Available Networks",
          icon: "ri-share-circle-line",
          requiredRoles: [ProductRole.CareConnectReferrer],
          excludedRoles: [ProductRole.CareConnectNetworkManager],
          hiddenForOrgTypes: [OrgType.LienOwner],
        },
        // Lien company network management — visible to network managers AND to
        // any TenantAdmin at a LIEN_OWNER org (covers the case where the admin
        // hasn't been explicitly granted the NetworkManager product role yet).
        {
          href: "/careconnect/my-network",
          label: "My Network",
          icon: "ri-settings-4-line",
          requiredRoles: [ProductRole.CareConnectNetworkManager],
          visibleForTenantAdminInOrgTypes: [OrgType.LienOwner],
        },
        // Multi-network admin view — internal/admin use only; hidden from lien company orgs.
        {
          href: "/careconnect/networks",
          label: "Networks",
          icon: "ri-share-forward-2-line",
          requiredRoles: [ProductRole.CareConnectNetworkManager],
          hiddenForOrgTypes: [OrgType.LienOwner],
        },
        // Referral Attribution configuration — tenant admin only. Tenant-portal
        // configuration, not part of the restricted single-product CareConnect
        // common portal (careconnect-demo.* / PORTAL_CARECONNECT_SUBDOMAIN).
        {
          href: "/careconnect/referral-attributions",
          label: "Referral Attributions",
          icon: "ri-price-tag-3-line",
          adminOnly: true,
          hiddenInProductPortal: true,
        },
      ],
    },
  ],

  fund: [
    {
      items: [
        {
          href: "/fund/dashboard",
          label: "Dashboard",
          icon: "ri-dashboard-line",
        },
        {
          href: "/fund/processing",
          label: "Processing",
          icon: "ri-loader-4-line",
        },
        {
          href: "/fund/underwriting",
          label: "Underwriting",
          icon: "ri-file-search-line",
        },
        {
          href: "/fund/payouts",
          label: "Payouts",
          icon: "ri-money-dollar-circle-line",
        },
        {
          href: "/fund/reports",
          label: "Reports",
          icon: "ri-bar-chart-2-line",
        },
      ],
    },
  ],

  lien: [
    {
      heading: "MY TASKS",
      items: [
        {
          href: "/lien/dashboard",
          label: "Dashboard",
          icon: "ri-dashboard-line",
        },
        {
          href: "/lien/task-manager",
          label: "Task Manager",
          icon: "ri-todo-line",
        },
        { href: "/lien/cases", label: "Cases", icon: "ri-survey-line" },
        { href: "/lien/liens", label: "Liens", icon: "ri-file-transfer-line" },
        {
          href: "/lien/servicing",
          label: "Servicing",
          icon: "ri-file-settings-line",
        },
        {
          href: "/lien/contacts",
          label: "Contacts",
          icon: "ri-contacts-book-line",
        },
      ],
    },
    {
      // LSV3-628: Marketplace is not part of the Phase 1 migration scope — hidden
      // pending Phase 1 completion, kept in the definition for easy re-enable.
      heading: "MARKETPLACE",
      sellModeOnly: true,
      notInPhase1: true,
      items: [
        {
          href: "/lien/my-liens",
          label: "My Liens",
          icon: "ri-price-tag-3-line",
          requiredRoles: [ProductRole.SynqLienSeller],
        },
        {
          href: "/lien/sales",
          label: "Lien Sales",
          icon: "ri-exchange-dollar-line",
          requiredRoles: [ProductRole.SynqLienSeller],
        },
        {
          href: "/lien/marketplace",
          label: "Marketplace",
          icon: "ri-store-2-line",
          requiredRoles: [ProductRole.SynqLienBuyer],
        },
      ],
    },
    {
      heading: "MY TOOLS",
      items: [
        {
          href: "/lien/templates",
          label: "Report Templates",
          icon: "ri-file-list-3-line",
        },
        {
          href: "/lien/reports",
          label: "Reports",
          icon: "ri-file-list-2-line",
        },
        {
          href: "/lien/batch-entry",
          label: "Batch Upload",
          icon: "ri-upload-line",
          requiredRoles: [ProductRole.SynqLienSeller],
        },
      ],
    },
    {
      // LSV3-628: product-level Settings is not part of the Phase 1 migration
      // scope — hidden pending Phase 1 completion, kept in the definition for
      // easy re-enable. (Not to be confused with the global ACCOUNT > User
      // Management item in GLOBAL_BOTTOM_NAV, which stays visible.)
      heading: "SETTINGS",
      notInPhase1: true,
      items: [
        {
          href: "/lien/settings/workflow",
          label: "Workflow Settings",
          icon: "ri-git-branch-line",
        },
        {
          href: "/lien/settings/task-templates",
          label: "Task Templates",
          icon: "ri-file-list-3-line",
        },
        {
          href: "/lien/settings/task-automation",
          label: "Task Automation",
          icon: "ri-robot-line",
        },
        {
          href: "/lien/settings/task-governance",
          label: "Task Governance",
          icon: "ri-shield-check-line",
        },
        {
          href: "/lien/settings/email-sources",
          label: "Email Sources",
          icon: "ri-mail-settings-line",
          adminOnly: true,
        },
        {
          href: "/lien/settings/email-inbox",
          label: "Email Inbox",
          icon: "ri-inbox-line",
          adminOnly: true,
        },
      ],
    },
  ],

  selling: [
    {
      items: [
        {
          href: "/selling/dashboard",
          label: "Dashboard",
          icon: "ri-dashboard-line",
        },
      ],
    },
    {
      heading: "Lien Portfolio",
      items: [
        {
          href: "/selling/portfolio",
          label: "Portfolio",
          icon: "ri-folder-3-line",
          children: [
            {
              href: "/selling/cases",
              label: "Cases",
              icon: "ri-briefcase-line",
              disabledMessage: "Cases is coming soon",
            },
            {
              href: "/selling/liens",
              label: "Liens",
              icon: "ri-file-transfer-line",
              disabledMessage: "Liens is coming soon",
            },
          ],
        },
        {
          href: "/selling/documents",
          label: "LOP / Documents",
          icon: "ri-file-text-line",
          disabledMessage: "LOP / Documents is coming soon",
        },
        {
          href: "/selling/reductions",
          label: "Reductions",
          icon: "ri-line-chart-line",
          disabledMessage: "Reductions is coming soon",
        },
        {
          href: "/selling/settlements",
          label: "Settlements",
          icon: "ri-scales-line",
          disabledMessage: "Settlements is coming soon",
        },
        {
          href: "/selling/contacts",
          label: "Contacts",
          icon: "ri-contacts-book-line",
        },
      ],
    },
    {
      heading: "Receivables",
      items: [
        {
          href: "/selling/receivables",
          label: "Receivables",
          icon: "ri-file-list-2-line",
          disabledMessage: "Receivables is coming soon",
        },
        {
          href: "/selling/aging",
          label: "Aging",
          icon: "ri-time-line",
          disabledMessage: "Aging is coming soon",
        },
        {
          href: "/selling/payments",
          label: "Payments",
          icon: "ri-money-dollar-circle-line",
          disabledMessage: "Payments is coming soon",
        },
      ],
    },
    {
      heading: "Reports",
      items: [
        {
          href: "/selling/analytics",
          label: "Analytics",
          icon: "ri-pie-chart-line",
          disabledMessage: "Analytics is coming soon",
        },
        {
          href: "/selling/reports",
          label: "Reports",
          icon: "ri-file-chart-line",
          disabledMessage: "Reports is coming soon",
        },
      ],
    },
    {
      // LSV3-628: Marketplace is not part of the Phase 1 migration scope — hidden
      // pending Phase 1 completion, kept in the definition for easy re-enable.
      heading: "MARKETPLACE",
      sellModeOnly: true,
      notInPhase1: true,
      items: [
        {
          href: "/selling/my-liens",
          label: "My Liens",
          icon: "ri-price-tag-3-line",
          requiredRoles: [ProductRole.SynqLienSeller],
        },
        {
          href: "/selling/sales",
          label: "Lien Sales",
          icon: "ri-exchange-dollar-line",
          requiredRoles: [ProductRole.SynqLienSeller],
        },
        {
          href: "/selling/marketplace",
          label: "Marketplace",
          icon: "ri-store-2-line",
          requiredRoles: [ProductRole.SynqLienBuyer],
        },
        {
          href: "/selling/portfolio",
          label: "Portfolio",
          icon: "ri-briefcase-line",
          requiredRoles: [
            ProductRole.SynqLienBuyer,
            ProductRole.SynqLienHolder,
          ],
        },
      ],
    },
    {
      // LSV3-628: product-level Settings is not part of the Phase 1 migration
      // scope — hidden pending Phase 1 completion, kept in the definition for
      // easy re-enable. (Not to be confused with the global ACCOUNT > User
      // Management item in GLOBAL_BOTTOM_NAV, which stays visible.)
      heading: "SETTINGS",
      notInPhase1: true,
      items: [
        {
          href: "/selling/settings/workflow",
          label: "Workflow Settings",
          icon: "ri-git-branch-line",
        },
        {
          href: "/selling/settings/task-templates",
          label: "Task Templates",
          icon: "ri-file-list-3-line",
        },
        {
          href: "/selling/settings/task-automation",
          label: "Task Automation",
          icon: "ri-robot-line",
        },
        {
          href: "/selling/settings/task-governance",
          label: "Task Governance",
          icon: "ri-shield-check-line",
        },
        {
          href: "/selling/settings/email-sources",
          label: "Email Sources",
          icon: "ri-mail-settings-line",
          adminOnly: true,
        },
        {
          href: "/selling/settings/email-inbox",
          label: "Email Inbox",
          icon: "ri-inbox-line",
          adminOnly: true,
        },
      ],
    },
  ],
  xenia: [
    { items: [{ href: "/xenia", label: "Assistant", icon: "ri-robot-line" }] },
  ],

  insights: [
    {
      items: [
        {
          href: "/insights/dashboard",
          label: "Dashboard",
          icon: "ri-dashboard-line",
        },
        {
          href: "/insights/reports",
          label: "Reports",
          icon: "ri-file-chart-line",
        },
        {
          href: "/insights/schedules",
          label: "Schedules",
          icon: "ri-calendar-schedule-line",
        },
      ],
    },
  ],
};

// ── Product metadata ──────────────────────────────────────────────────────────

export const PRODUCT_META: Record<
  string,
  { label: string; icon: string; color: string; iconSrc: string }
> = {
  careconnect: {
    label: "Synq CareConnect",
    icon: "ri-shield-cross-line",
    color: "#2563eb",
    iconSrc: "/product-icons/synqconnect.png",
  },
  fund: {
    label: "Synq Funds",
    icon: "ri-bank-line",
    color: "#16a34a",
    iconSrc: "/product-icons/synqfund.png",
  },
  lien: {
    label: "Synq Liens",
    icon: "ri-stack-line",
    color: "#7c3aed",
    iconSrc: "/product-icons/synqlien.png",
  },
  xenia: {
    label: "Xenia",
    icon: "ri-robot-line",
    color: "#d97706",
    iconSrc: "/product-icons/synqai.png",
  },
  selling: {
    label: "Synq Lien Selling",
    icon: "ri-stack-line",
    color: "#7c3aed",
    iconSrc: "/product-icons/synqlien.png",
  },
  insights: {
    label: "Synq Insights",
    icon: "ri-bar-chart-2-line",
    color: "#0891b2",
    iconSrc: "/product-icons/synqinsight.png",
  },
};

/**
 * Maps backend product codes (as returned by auth/me `enabledProducts`) to the
 * PRODUCT_META key used in the tenant portal.
 * Values on the left are the frontend-friendly codes emitted by the Identity service
 * (e.g. "CareConnect", "SynqFund"). Values on the right are PRODUCT_META keys.
 */
export const PRODUCT_CODE_TO_NAV_KEY: Record<string, string> = {
  CareConnect: "careconnect",
  SynqFund: "fund",
  SynqLien: "lien",
  SYNQ_SELLING: "selling",
  Xenia: "xenia",
  XENIA: "xenia",
  SynqAI: "xenia",
  SynqInsights: "insights",
  SynqBill: "bill",
  SynqRx: "rx",
  SynqPayout: "payout",
};

/**
 * Converts a list of backend enabledProducts codes into the set of PRODUCT_META
 * keys that should be shown on the dashboard.
 * Returns no product keys when the list is empty.
 */
export function resolveEnabledNavKeys(enabledProducts: string[]): Set<string> {
  if (enabledProducts.length === 0) return new Set();
  const keys = new Set<string>();
  for (const code of enabledProducts) {
    const key = PRODUCT_CODE_TO_NAV_KEY[code];
    if (key && key in PRODUCT_META) keys.add(key);
  }
  return keys;
}

export function filterNavByRoles(
  sections: NavSection[],
  userRoles: ProductRoleValue[],
  isTenantAdmin = false,
  orgType?: OrgTypeValue | null,
): NavSection[] {
  return sections
    .filter((section) => !section.notInPhase1)
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => {
        // LSV3-628: hide items not part of the Phase 1 migration scope.
        if (item.notInPhase1) return false;

        // Hide immediately if the user holds any excluded role.
        if (item.excludedRoles?.some((role) => userRoles.includes(role)))
          return false;

        // TenantAdmin override: show this item if the user is a tenant admin in
        // a matching org type, regardless of product-role state.
        if (
          item.visibleForTenantAdminInOrgTypes &&
          isTenantAdmin &&
          orgType &&
          item.visibleForTenantAdminInOrgTypes.includes(orgType)
        )
          return true;

        if (!item.requiredRoles || item.requiredRoles.length === 0) return true;
        return item.requiredRoles.some((role) => userRoles.includes(role));
      }),
    }))
    .filter((section) => section.items.length > 0);
}

export function filterNavByAccess(
  sections: NavSection[],
  userRoles: ProductRoleValue[],
  isSellMode: boolean,
  orgType?: OrgTypeValue | null,
  isTenantAdmin = false,
  isProductPortal = false,
): NavSection[] {
  return filterNavByRoles(sections, userRoles, isTenantAdmin, orgType)
    .filter((s) => !s.sellModeOnly || isSellMode)
    .map((s) => ({
      ...s,
      items: s.items.filter((item) => {
        if (item.sellModeOnly && !isSellMode) return false;
        if (
          item.hiddenForOrgTypes &&
          orgType &&
          item.hiddenForOrgTypes.includes(orgType)
        )
          return false;
        if (item.hiddenInProductPortal && isProductPortal) return false;
        return true;
      }),
    }))
    .filter((s) => s.items.length > 0);
}

// ── Infer product from pathname ───────────────────────────────────────────────

export function inferProductFromPath(pathname: string): string | null {
  if (pathname.startsWith("/careconnect")) return "careconnect";
  if (pathname.startsWith("/fund")) return "fund";
  if (pathname.startsWith("/lien")) return "lien";
  if (pathname.startsWith("/selling")) return "selling";
  if (pathname.startsWith("/xenia")) return "xenia";
  if (pathname.startsWith("/ai")) return "xenia";
  if (pathname.startsWith("/insights")) return "insights";
  return null;
}

// ── Org type label ────────────────────────────────────────────────────────────

export function orgTypeLabel(orgType: string | undefined): string {
  const labels: Record<string, string> = {
    LAW_FIRM: "Law Firm",
    PROVIDER: "Provider",
    FUNDER: "Funder",
    LIEN_OWNER: "Lien Owner",
    INTERNAL: "Internal",
  };
  return orgType ? (labels[orgType] ?? orgType) : "No Organization";
}

// ── Global bottom nav (always shown at the foot of every product sidebar) ─────

export const GLOBAL_BOTTOM_NAV: NavSection = {
  heading: "ACCOUNT",
  items: [
    {
      href: "/my-work",
      label: "My Work",
      icon: "ri-task-line",
      adminOnly: true,
    },
    {
      href: "/notifications",
      label: "Notifications",
      icon: "ri-mail-send-line",
      adminOnly: true,
    },
    {
      href: "/activity",
      label: "Activity Log",
      icon: "ri-history-line",
      adminOnly: true,
    },
    {
      href: "/support",
      label: "Support",
      icon: "ri-customer-service-2-line",
      adminOnly: true,
    },
    {
      href: "/tenant/authorization/users",
      label: "User Management",
      icon: "ri-user-community-line",
      adminOnly: true,
    },
    {
      href: "/tenant/settings",
      label: "Tenant Settings",
      icon: "ri-settings-3-line",
      adminOnly: true,
    },
  ],
};

// ── Admin nav sections (shown when session has admin role) ────────────────────

/**
 * Returns the Administration NavSection[] for sidebar rendering.
 * Returns an empty array for standard users — they see nothing.
 */
export function buildNavGroups(_session: PlatformSession): NavSection[] {
  return [];
}
