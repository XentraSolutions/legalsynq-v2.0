"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, ChevronDown, CircleAlert, SquarePen, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { ActionMenu, type ActionMenuItem } from "@/components/selling/action-menu";
import { Button } from "@/components/selling/button";
import { StatusBadge } from "@/components/lien/status-badge";
import { DateDisplay } from "@/components/ui/date-display";
import { ApiError } from "@/lib/api-client";
import { useSellingCaseDetail } from "@/hooks/selling/use-case-drafts";

const LIST_PATH = "/selling/portfolio/cases";

const TABS = [
  { key: "details", label: "Details" },
  { key: "liens", label: "Liens" },
  { key: "documents", label: "Documents" },
  { key: "payments", label: "Payments" },
  { key: "messages", label: "Messages" },
];

export default function CaseDetailPage() {
  const params = useParams<{ id: string }>();
  const caseId = params?.id ?? "";
  const router = useRouter();
  const [activeTab, setActiveTab] = useState("details");

  const { data: caseDetail, isLoading, error } = useSellingCaseDetail(caseId);

  if (isLoading) {
    return <CaseDetailSkeleton />;
  }

  if (error) {
    let message = "Failed to load case.";
    if (error instanceof ApiError) {
      if (error.isNotFound) message = "Case not found.";
      else if (error.isForbidden) message = "You do not have access to this case.";
      else message = error.message;
    }
    return (
      <div className="space-y-4">
        <Link
          href={LIST_PATH}
          aria-label="Back to Portfolio"
          className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors"
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <div className="p-10 text-center space-y-3">
          <CircleAlert className="h-6 w-6 text-gray-300 mx-auto" />
          <p className="text-sm text-gray-500">{message}</p>
        </div>
      </div>
    );
  }

  if (!caseDetail) {
    return null;
  }

  const fullName = [caseDetail.firstName, caseDetail.lastName]
    .filter(Boolean)
    .join(" ") || "Unnamed Plaintiff";

  const handleTabClick = (tab: (typeof TABS)[number]) => {
    if (tab.key === "details") {
      setActiveTab("details");
      return;
    }
    toast.info(`${tab.label} is coming soon.`);
  };

  const address = [
    caseDetail.address,
    [caseDetail.city, caseDetail.state].filter(Boolean).join(", "),
    caseDetail.zipcode,
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <div className="space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            href={LIST_PATH}
            aria-label="Back to Portfolio"
            className="w-9 h-9 rounded-full border border-gray-200 flex items-center justify-center text-gray-500 hover:bg-gray-50 transition-colors shrink-0"
          >
            <ArrowLeft className="h-5 w-5" />
          </Link>
          <div className="min-w-0">
            <h1 className="text-2xl font-bold text-gray-900 truncate">{fullName}</h1>
            <p className="text-sm text-gray-400 mt-1 truncate">{caseDetail.caseNumber}</p>
          </div>
        </div>

        <ActionMenu
          align="end"
          trigger={
            <Button variant="primary" className="shrink-0" rightIcon="chevronDown">
              Manage Case
            </Button>
          }
          items={[
            {
              label: "Edit Case",
              icon: SquarePen,
              onClick: () => router.push(`${LIST_PATH}/${caseId}/edit/case-info`),
            } satisfies ActionMenuItem,
            {
              label: "Delete Case",
              icon: Trash2,
              variant: "danger",
              onClick: () => toast.info("Deleting cases is coming soon."),
            },
          ]}
        />
      </div>

      <nav className="flex items-center h-[38px] gap-1 bg-[#FAFAFA] rounded-md p-1">
        {TABS.map((tab) => {
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => handleTabClick(tab)}
              className={[
                "flex-1 h-[30px] flex items-center justify-center text-sm font-medium rounded-md transition-colors whitespace-nowrap border",
                isActive
                  ? "bg-white border-[#E5E5E5] shadow-sm text-gray-900"
                  : "border-transparent text-gray-500 hover:text-gray-700",
              ].join(" ")}
            >
              {tab.label}
            </button>
          );
        })}
      </nav>

      {activeTab === "details" && (
        <div className="space-y-5">
          <section className="bg-white border border-gray-200 rounded-xl p-6">
            <div className="flex items-center justify-between gap-4 mb-5">
              <div className="flex items-center gap-2">
                <ChevronDown className="h-4 w-4 text-gray-400" />
                <h2 className="text-base font-semibold text-gray-900">
                  Case Information
                </h2>
              </div>
              <Button
                variant="secondary"
                leftIcon="squarePen"
                onClick={() => router.push(`${LIST_PATH}/${caseId}/edit/case-info`)}
              >
                Edit
              </Button>
            </div>

            <div className="grid grid-cols-2 gap-x-6 gap-y-4">
              <InfoField label="Status">
                <StatusBadge status={caseDetail.caseStatus} />
              </InfoField>
              <InfoField label="Accident Type">
                {/* accidentTypeId is a GUID — this endpoint doesn't return
                    the display name, so we render the raw id as a
                    placeholder. Resolving it to a name is a follow-up. */}
                {caseDetail.accidentTypeId ?? "—"}
              </InfoField>
              <InfoField label="Accident State">
                {caseDetail.accidentState ?? "—"}
              </InfoField>
              <InfoField label="Date of Loss">
                <DateDisplay value={caseDetail.dateOfLoss} format="date" />
              </InfoField>
              <InfoField label="Law Firm">
                {/* handlingLawFirmId is a GUID — same follow-up as above. */}
                {caseDetail.handlingLawFirmId ?? "—"}
              </InfoField>
              <InfoField label="Case Manager">
                {caseDetail.caseManagerId ?? "—"}
              </InfoField>
              <div className="col-span-2">
                <InfoField label="Case Tracking Note">
                  {caseDetail.caseTrackingNotes || "—"}
                </InfoField>
              </div>
            </div>
          </section>

          <section className="bg-white border border-gray-200 rounded-xl p-6">
            <div className="flex items-center justify-between gap-4 mb-5">
              <div className="flex items-center gap-2">
                <ChevronDown className="h-4 w-4 text-gray-400" />
                <h2 className="text-base font-semibold text-gray-900">
                  Plaintiff Information
                </h2>
              </div>
              <Button
                variant="secondary"
                leftIcon="squarePen"
                onClick={() => router.push(`${LIST_PATH}/${caseId}/edit/plaintiff`)}
              >
                Edit
              </Button>
            </div>

            <div className="grid grid-cols-2 gap-x-6 gap-y-4">
              <InfoField label="Full Name">{fullName}</InfoField>
              <InfoField label="Birthdate">
                <DateDisplay value={caseDetail.birthdate} format="date" />
              </InfoField>
              <InfoField label="Email">{caseDetail.email || "—"}</InfoField>
              <InfoField label="Phone Number">{caseDetail.phone || "—"}</InfoField>
              <InfoField label="Sex">{caseDetail.gender || "—"}</InfoField>
              <InfoField label="Address">{address || "—"}</InfoField>
            </div>
          </section>
        </div>
      )}
    </div>
  );
}

function InfoField({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs text-gray-400 mb-1">{label}</p>
      <div className="text-sm text-gray-900">{children}</div>
    </div>
  );
}

function CaseDetailSkeleton() {
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

      <div className="h-[38px] bg-[#FAFAFA] rounded-md" />

      <div className="h-64 bg-white border border-gray-200 rounded-xl" />
      <div className="h-64 bg-white border border-gray-200 rounded-xl" />
    </div>
  );
}
