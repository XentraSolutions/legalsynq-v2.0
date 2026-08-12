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
import { ReassignCompanyModal } from "@/components/selling/forms/reassign-company-modal";
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
  { key: "cases", label: "Cases" },
  { key: "activities", label: "Activities" },
  { key: "contacts", label: "Contact Person" },
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
  const [reassignOpen, setReassignOpen] = useState(false);
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
    const verb = company.isActive ? "Deleting" : "Reactivating";
    const doneVerb = company.isActive ? "deleted" : "reactivated";
    const toastId = toast.loading(`${verb} ${company.name}...`);
    setConfirmAction(null);
    mutation.mutate(company.id, {
      onSuccess: () => {
        toast.success(`Company ${doneVerb}`, { id: toastId, description: company.name });
      },
      onError: (err) => {
        toast.error(`Couldn't ${company.isActive ? "delete" : "reactivate"} company`, {
          id: toastId,
          description: err instanceof Error ? err.message : company.name,
        });
      },
    });
  };

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            href={BASE_PATH}
            aria-label="Back to Contacts"
            className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors shrink-0"
          >
            <i className="ri-arrow-left-line text-lg" />
          </Link>
          <div className="min-w-0">
            <div className="flex items-center gap-2.5">
              <h1 className="text-2xl font-bold text-gray-900 truncate">{company.name}</h1>
              {!company.isActive && (
                <span className="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-semibold bg-red-50 text-red-600 border-red-200 shrink-0">
                  Inactive
                </span>
              )}
            </div>
            <p className="text-sm text-gray-400 mt-1 truncate">Contact ID: {company.id}</p>
          </div>
        </div>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button
              className={`${PRIMARY_BUTTON_CLASSNAME} shrink-0`}
              iconDivider
              rightIcon={<i className="ri-arrow-down-s-line text-base" />}
            >
              Manage Company
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {canEdit && (
              <DropdownMenuItem onClick={() => setReassignOpen(true)}>
                <i className="ri-repeat-line" />
                Reassign
              </DropdownMenuItem>
            )}
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
            {company.isActive ? (
              <DropdownMenuItem
                onClick={() => setConfirmAction("deactivate")}
                className="text-red-600 focus:bg-red-50"
              >
                <i className="ri-delete-bin-line" />
                Delete Company
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem onClick={() => setConfirmAction("reactivate")}>
                <i className="ri-check-line" />
                Reactivate Company
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <nav className="flex items-center gap-1 bg-gray-100 rounded-xl p-1">
        {TABS.map((tab) => {
          const href = `${BASE_PATH}/${id}/${tab.key}`;
          const isActive = pathname?.startsWith(href);
          return (
            <Link
              key={tab.key}
              href={href}
              className={[
                "flex-1 text-center px-4 py-2.5 text-sm font-medium rounded-lg transition-colors whitespace-nowrap",
                isActive
                  ? "bg-white text-gray-900 shadow-sm"
                  : "text-gray-500 hover:text-gray-700",
              ].join(" ")}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>

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

      {reassignOpen && (
        <ReassignCompanyModal
          open
          company={company}
          onClose={() => setReassignOpen(false)}
        />
      )}

      {confirmAction && (
        <ConfirmDialog
          open
          onClose={() => setConfirmAction(null)}
          onConfirm={handleToggleActive}
          title={confirmAction === "deactivate" ? "Delete Company" : "Reactivate Company"}
          description={`Are you sure you want to ${confirmAction === "deactivate" ? "delete" : "reactivate"} ${company.name}?${confirmAction === "deactivate" ? " This action cannot be undone." : ""}`}
          confirmLabel={confirmAction === "deactivate" ? "Delete" : "Reactivate"}
          confirmVariant={confirmAction === "deactivate" ? "danger" : "primary"}
          loading={deactivateMutation.isPending || reactivateMutation.isPending}
          warningTitle={confirmAction === "deactivate" ? "Warning: Deleting this company will also remove:" : undefined}
          warningItems={
            confirmAction === "deactivate"
              ? ["All associated contact persons", "All case associations", "All activity history"]
              : undefined
          }
        />
      )}
    </div>
  );
}

function CompanyDetailShellSkeleton() {
  return (
    <div className="space-y-5 animate-pulse">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-full bg-gray-100 shrink-0" />
          <div className="space-y-2">
            <div className="h-6 w-56 bg-gray-100 rounded" />
            <div className="h-3.5 w-24 bg-gray-100 rounded" />
          </div>
        </div>
        <div className="h-10 w-40 bg-gray-100 rounded-lg" />
      </div>

      <div className="h-12 bg-gray-100 rounded-xl" />

      <div className="h-64 bg-white border border-gray-200 rounded-xl" />
    </div>
  );
}
