"use client";

import { useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { toast } from "sonner";
import { useRoleAccess } from "@/hooks/use-role-access";
import {
  useCompany,
  useCompanyTypes,
  useDeactivateCompany,
  useReactivateCompany,
} from "@/hooks/use-selling-companies";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { ConfirmDialog } from "@/components/selling/modal";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { CompanyDetailContextProvider } from "./context";
import { Button } from "@/components/ui/button";

const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";
const BASE_PATH = "/selling/contacts";

const TABS = [
  { key: "overview", label: "Overview" },
  { key: "contacts", label: "Contact Persons" },
];

export function CompanyDetailShell({
  id,
  children,
}: {
  id: string;
  children: React.ReactNode;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const ra = useRoleAccess();

  const companyQuery = useCompany(id);
  const companyTypesQuery = useCompanyTypes();
  const deactivateMutation = useDeactivateCompany();
  const reactivateMutation = useReactivateCompany();

  const [editOpen, setEditOpen] = useState(false);
  const [confirmAction, setConfirmAction] = useState<"deactivate" | "reactivate" | null>(null);

  const canEdit = ra.can("contact:edit");

  if (companyQuery.isLoading) {
    return <CompanyDetailShellSkeleton />;
  }

  const company = companyQuery.data;

  if (!company) {
    return (
      <div className="p-10 text-center space-y-3">
        <i className="ri-error-warning-line text-3xl text-gray-300" />
        <p className="text-sm text-gray-500">Company not found.</p>
        <Link href={BASE_PATH} className="text-sm text-primary hover:underline">
          Back to Contacts
        </Link>
      </div>
    );
  }

  const handleToggleActive = () => {
    const mutation = company.isActive ? deactivateMutation : reactivateMutation;
    const verb = company.isActive ? "Deactivating" : "Reactivating";
    const doneVerb = company.isActive ? "deactivated" : "reactivated";
    const toastId = toast.loading(`${verb} ${company.name}...`);
    setConfirmAction(null);
    mutation.mutate(company.id, {
      onSuccess: () => {
        toast.success(`Company ${doneVerb}`, { id: toastId, description: company.name });
      },
      onError: (err) => {
        toast.error(`Couldn't ${company.isActive ? "deactivate" : "reactivate"} company`, {
          id: toastId,
          description: err instanceof Error ? err.message : company.name,
        });
      },
    });
  };

  return (
    <div className="space-y-5">
      <div className="text-xs text-gray-400 flex items-center gap-1">
        <Link href={BASE_PATH} className="hover:text-gray-600 transition-colors">
          Contacts
        </Link>
        <i className="ri-arrow-right-s-line text-sm" />
        <span className="text-gray-500">{company.companyTypeName}</span>
      </div>

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="px-6 py-5">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 lg:items-center">
            <div className="min-w-0">
              <div className="flex items-center gap-2.5">
                <h1 className="text-2xl font-bold text-gray-900 truncate">{company.name}</h1>
                <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold bg-gray-50 text-gray-600 border-gray-200 shrink-0">
                  {company.companyTypeName}
                </span>
                {!company.isActive && (
                  <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold bg-red-50 text-red-600 border-red-200 shrink-0">
                    Inactive
                  </span>
                )}
              </div>
              <p className="text-sm text-gray-400 mt-1.5 truncate">Company ID: {company.id}</p>
            </div>

            <div className="grid grid-cols-3 gap-x-8 gap-y-4">
              <HeaderStat icon="ri-mail-line" label="Email" value={company.email} />
              <HeaderStat icon="ri-phone-line" label="Phone" value={company.phone} />
              <HeaderStat
                icon="ri-map-pin-line"
                label="Address"
                value={[company.city, company.state].filter(Boolean).join(", ")}
              />

              <div className="flex items-center">
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button
                      className={PRIMARY_BUTTON_CLASSNAME}
                      iconDivider
                      rightIcon={<i className="ri-arrow-down-s-line text-base" />}
                    >
                      Actions
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    {canEdit && (
                      <DropdownMenuItem onClick={() => setEditOpen(true)}>
                        <i className="ri-pencil-line" />
                        Edit Company
                      </DropdownMenuItem>
                    )}
                    {company.email && (
                      <DropdownMenuItem asChild>
                        <a href={`mailto:${company.email}`}>
                          <i className="ri-mail-line" />
                          Send Email
                        </a>
                      </DropdownMenuItem>
                    )}
                    <DropdownMenuSeparator />
                    <DropdownMenuItem
                      onClick={() => setConfirmAction(company.isActive ? "deactivate" : "reactivate")}
                      className={company.isActive ? "text-red-600 focus:bg-red-50" : undefined}
                    >
                      <i className={company.isActive ? "ri-forbid-line" : "ri-check-line"} />
                      {company.isActive ? "Deactivate Company" : "Reactivate Company"}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </div>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-100 px-6">
          <nav className="flex flex-wrap gap-4 -mb-px">
            {TABS.map((tab) => {
              const href = `${BASE_PATH}/${id}/${tab.key}`;
              const isActive = pathname?.startsWith(href);
              return (
                <Link
                  key={tab.key}
                  href={href}
                  className={[
                    "px-4 py-2.5 text-sm font-medium border-b-2 transition-colors whitespace-nowrap",
                    isActive
                      ? "border-primary text-primary"
                      : "border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300",
                  ].join(" ")}
                >
                  {tab.label}
                </Link>
              );
            })}
          </nav>
        </div>
      </div>

      <CompanyDetailContextProvider value={{ id, company, canEdit }}>
        {children}
      </CompanyDetailContextProvider>

      {editOpen && (
        <CompanyFormModal
          open={editOpen}
          title="Edit Company"
          companyTypeId={company.companyTypeId}
          editTarget={company}
          onClose={() => setEditOpen(false)}
          onSaved={() => setEditOpen(false)}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleToggleActive}
          title={confirmAction === "deactivate" ? "Deactivate Company" : "Reactivate Company"}
          description={`Are you sure you want to ${confirmAction} ${company.name}?`}
          confirmLabel={confirmAction === "deactivate" ? "Deactivate" : "Reactivate"}
          confirmVariant={confirmAction === "deactivate" ? "danger" : "primary"}
          loading={deactivateMutation.isPending || reactivateMutation.isPending}
        />
      )}
    </div>
  );
}

function HeaderStat({
  icon,
  label,
  value,
}: {
  icon: string;
  label: string;
  value?: React.ReactNode;
}) {
  return (
    <div className="min-w-0 flex items-start gap-2">
      <i className={`${icon} text-base text-gray-300 mt-0.5`} />
      <div className="min-w-0">
        <p className="text-[11px] text-gray-400 uppercase tracking-wide leading-tight">{label}</p>
        <p className="text-sm text-gray-700 font-medium mt-1 truncate">{value || ""}</p>
      </div>
    </div>
  );
}

function CompanyDetailShellSkeleton() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="h-3.5 w-32 bg-gray-100 rounded" />

      <div className="bg-white border border-gray-200 rounded-xl">
        <div className="px-6 py-5">
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 lg:items-center">
            <div className="min-w-0 space-y-2">
              <div className="h-6 w-56 bg-gray-100 rounded" />
              <div className="h-3.5 w-24 bg-gray-100 rounded" />
            </div>
            <div className="grid grid-cols-3 gap-x-8 gap-y-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="space-y-2">
                  <div className="h-2.5 w-12 bg-gray-100 rounded" />
                  <div className="h-4 w-20 bg-gray-100 rounded" />
                </div>
              ))}
            </div>
          </div>
        </div>
        <div className="border-t border-gray-100 px-6 py-4">
          <div className="flex gap-6">
            {Array.from({ length: 2 }).map((_, i) => (
              <div key={i} className="h-3.5 w-16 bg-gray-100 rounded" />
            ))}
          </div>
        </div>
      </div>

      <div className="h-64 bg-white border border-gray-200 rounded-xl" />
    </div>
  );
}
