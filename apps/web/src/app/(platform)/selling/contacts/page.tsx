"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { ActionMenu } from "@/components/lien/action-menu";
import { SideDrawer } from "@/components/lien/side-drawer";
import { useToast } from "@/lib/toast-context";
import {
  useSellingFundingCompanies,
  useSellingFundingCompanyContacts,
  useSellingLawFirms,
  useSellingCaseManagers,
  useSellingFacilities,
} from "@/hooks/use-selling-lookups";
import type {
  SellingLookupItem,
  SellingFacilityItem,
} from "@/lib/selling/lookup.api";

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

type TabKey = "fundingCompanies" | "lawFirms" | "facilities";

const TABS: { key: TabKey; label: string; columnLabel: string }[] = [
  { key: "fundingCompanies", label: "Funding Companies", columnLabel: "Funding Company" },
  { key: "lawFirms", label: "Law Firms", columnLabel: "Law Firm" },
  { key: "facilities", label: "Medical Facilities", columnLabel: "Medical Facility" },
];

export const dynamic = "force-dynamic";

// Selling has no full contacts CRUD API of its own — only read-only lookups
// (funding companies, law firms, facilities, plus each funding company's/law
// firm's own contacts). This page is built entirely on those selling lookup
// endpoints rather than the tenant-wide sync-liens contacts API, so listing
// is limited to what those lookups cover and actions with no selling-backed
// endpoint (add/edit/delete/reassign/export) surface a "not available" toast
// instead of silently doing nothing.
export default function SellingContactsPage() {
  const searchParams = useSearchParams();
  const { show } = useToast();

  const typeFilter = (searchParams.get("type") as TabKey | null) ?? "fundingCompanies";
  const activeTab = TABS.find((t) => t.key === typeFilter) ?? TABS[0];

  const [searchInput, setSearchInput] = useState("");
  const [previewItem, setPreviewItem] = useState<SellingLookupItem | null>(null);

  const fundingCompaniesQuery = useSellingFundingCompanies();
  const lawFirmsQuery = useSellingLawFirms();
  const facilitiesQuery = useSellingFacilities();

  const listByTab: Record<TabKey, { data: SellingLookupItem[] | undefined; isLoading: boolean }> = {
    fundingCompanies: { data: fundingCompaniesQuery.data, isLoading: fundingCompaniesQuery.isLoading },
    lawFirms: { data: lawFirmsQuery.data, isLoading: lawFirmsQuery.isLoading },
    facilities: { data: facilitiesQuery.data, isLoading: facilitiesQuery.isLoading },
  };

  const activeList = listByTab[activeTab.key];

  const filteredItems = useMemo(() => {
    const items = activeList.data ?? [];
    const q = searchInput.trim().toLowerCase();
    if (!q) return items;
    return items.filter((item) => item.name.toLowerCase().includes(q));
  }, [activeList.data, searchInput]);

  // Selling contacts of a funding company / case managers of a law firm are
  // real, selling-scoped lookups — fetched only once a row is previewed.
  const fundingCompanyContactsQuery = useSellingFundingCompanyContacts(
    activeTab.key === "fundingCompanies" ? previewItem?.id : undefined,
    { enabled: activeTab.key === "fundingCompanies" && Boolean(previewItem) },
  );
  const caseManagersQuery = useSellingCaseManagers(
    activeTab.key === "lawFirms" ? previewItem?.id : undefined,
    { enabled: activeTab.key === "lawFirms" && Boolean(previewItem) },
  );

  const notAvailable = (action: string) =>
    show(`${action} isn't available yet for Selling contacts.`, "info");

  const columns = useMemo<ColumnDef<SellingLookupItem, any>[]>(() => {
    const base: ColumnDef<SellingLookupItem, any>[] = [
      {
        id: "name",
        header: activeTab.columnLabel,
        cell: ({ row }) => (
          <span className="text-sm font-medium text-gray-700">{row.original.name}</span>
        ),
      },
    ];
    if (activeTab.key === "facilities") {
      base.push({
        id: "code",
        header: "Code",
        cell: ({ row }) => (
          <span className="text-sm text-gray-500">
            {(row.original as SellingFacilityItem).code || "—"}
          </span>
        ),
      });
    }
    base.push({
      id: "actions",
      header: "",
      meta: { align: "right" },
      cell: ({ row }) => (
        <div onClick={(e) => e.stopPropagation()}>
          <ActionMenu
            items={[
              {
                label: "View Details",
                icon: "ri-eye-line",
                onClick: () => setPreviewItem(row.original),
              },
              {
                label: "Edit Contact",
                icon: "ri-pencil-line",
                onClick: () => notAvailable("Editing contacts"),
              },
              {
                label: "Delete",
                icon: "ri-delete-bin-line",
                onClick: () => notAvailable("Deleting contacts"),
              },
            ]}
          />
        </div>
      ),
    });
    return base;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab]);

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Contacts</h1>
          <p className="text-sm text-gray-500 mt-1">
            Keep your contact records organized and easily accessible.
          </p>
        </div>
        <button
          onClick={() => notAvailable("Adding contacts")}
          className="shrink-0 flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
        >
          <i className="ri-add-line text-base" />
          Add Contact
        </button>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-1 bg-gray-100 rounded-lg p-1 w-full">
        {TABS.map((tab) => (
          <Link
            key={tab.key}
            href={`/selling/contacts?type=${tab.key}`}
            className={`flex-1 text-center px-4 py-2 text-sm font-medium rounded-md transition-colors whitespace-nowrap ${
              typeFilter === tab.key
                ? "bg-white text-gray-900 shadow-sm"
                : "text-gray-500 hover:text-gray-700"
            }`}
          >
            {tab.label}
            {listByTab[tab.key].data !== undefined && ` (${listByTab[tab.key].data!.length})`}
          </Link>
        ))}
      </div>

      <div className="bg-white border border-gray-200 rounded-xl px-4 py-3 flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[200px]">
          <i className="ri-search-line absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm" />
          <input
            type="text"
            placeholder={`Search ${activeTab.label.toLowerCase()} by name...`}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
          />
        </div>
        <button
          onClick={() => notAvailable("Exporting contacts")}
          className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
        >
          Export
        </button>
      </div>

      <BaseTable
        data={filteredItems}
        columns={columns}
        getRowId={(c) => c.id}
        isLoading={activeList.isLoading}
        emptyMessage="No contacts found."
        onRowClick={(c) => setPreviewItem(c)}
        pageSizeOptions={PAGE_SIZE_OPTIONS}
        className="bg-white border-gray-200 rounded-xl"
      />

      <SideDrawer
        open={!!previewItem}
        onClose={() => setPreviewItem(null)}
        title={previewItem?.name || ""}
        subtitle={activeTab.label}
      >
        {previewItem && activeTab.key === "facilities" && (
          <div className="text-sm">
            <p className="text-xs text-gray-400">Facility Code</p>
            <p className="text-gray-700">
              {(previewItem as SellingFacilityItem).code || "—"}
            </p>
            <p className="text-xs text-gray-400 mt-4">
              No additional contact details are available for facilities yet.
            </p>
          </div>
        )}

        {previewItem && activeTab.key === "fundingCompanies" && (
          <div className="space-y-3">
            <p className="text-xs font-medium text-gray-400 uppercase tracking-wide">
              Contacts
            </p>
            {fundingCompanyContactsQuery.isLoading ? (
              <p className="text-sm text-gray-400">Loading...</p>
            ) : fundingCompanyContactsQuery.data?.length ? (
              <ul className="space-y-2">
                {fundingCompanyContactsQuery.data.map((c) => (
                  <li key={c.id} className="border border-gray-100 rounded-lg px-3 py-2">
                    <p className="text-sm font-medium text-gray-700">{c.name}</p>
                    <p className="text-xs text-gray-500">{c.email || "—"}</p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-gray-400">
                No contacts found for this funding company.
              </p>
            )}
          </div>
        )}

        {previewItem && activeTab.key === "lawFirms" && (
          <div className="space-y-3">
            <p className="text-xs font-medium text-gray-400 uppercase tracking-wide">
              Case Managers
            </p>
            {caseManagersQuery.isLoading ? (
              <p className="text-sm text-gray-400">Loading...</p>
            ) : caseManagersQuery.data?.length ? (
              <ul className="space-y-2">
                {caseManagersQuery.data.map((c) => (
                  <li key={c.id} className="border border-gray-100 rounded-lg px-3 py-2">
                    <p className="text-sm font-medium text-gray-700">{c.name}</p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-gray-400">
                No case managers found for this law firm.
              </p>
            )}
          </div>
        )}
      </SideDrawer>
    </div>
  );
}
