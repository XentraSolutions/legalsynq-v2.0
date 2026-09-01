"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Plus, Search } from "lucide-react";
import { liensService } from "@/lib/selling";
import { toast } from "sonner";
import { Button } from "@/components/selling/button";
import { useCompanyTypes, useCompanies } from "@/hooks/selling/use-selling-companies";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { FundingCompanyContactField } from "@/components/selling/funding-company-contact-field";
import Field from "@/components/lien/field";
import { SkeletonListRows } from "@/components/lien/skeleton-loader";
import { TOTAL_STEPS, goToStep } from "./shared";

// Mirrors the loaded page below: back+progress header, step label, title,
// description, search field, and the funding-company list.
function BuyerSelectionSkeleton() {
  return (
    <div className="max-w-4xl mx-auto space-y-6 pb-10 animate-pulse">
      <div className="flex items-center gap-4">
        <div className="h-5 w-5 rounded bg-gray-100 shrink-0" />
        <div className="flex-1 flex gap-2">
          {Array.from({ length: TOTAL_STEPS }, (_, index) => (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full ${index < 1 ? "bg-[#EE7132]/40" : "bg-gray-200"}`}
            />
          ))}
        </div>
      </div>
      <div className="space-y-4">
        <div className="h-3 bg-gray-100 rounded w-16" />
        <div className="h-7 bg-gray-100 rounded w-64" />
        <div className="h-3 bg-gray-100 rounded w-full max-w-lg" />
        <div className="h-10 bg-gray-100 rounded-lg w-full" />
        <SkeletonListRows rows={5} />
      </div>
    </div>
  );
}

export interface BuyerSelectionStepProps {
  lienId: string;
}

// Step 1/2 — pick the funding company and contact this lien will be sold to.
// The lien-owned association is persisted so the selection survives refreshes.
export default function BuyerSelectionStep({ lienId }: BuyerSelectionStepProps) {
  const router = useRouter();

  const [hydrating, setHydrating] = useState(true);
  const [companySearch, setCompanySearch] = useState("");
  const [companyId, setCompanyId] = useState<string>("");
  const [contactId, setContactId] = useState<string>("");
  const [savingBuyerSelection, setSavingBuyerSelection] = useState(false);
  const [showContactRequiredError, setShowContactRequiredError] = useState(false);
  const [showAddCompany, setShowAddCompany] = useState(false);
  const selectedCompanyRef = useRef<HTMLLabelElement | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        if (cancelled) return;
        if (lien.fundingCompany) {
          setCompanyId(lien.fundingCompany.id);
          if (lien.fundingCompany.contact) {
            setContactId(lien.fundingCompany.contact.id);
          }
        }
      } catch (err) {
        toast.error(err instanceof Error ? err.message : "Failed to load lien");
      } finally {
        if (!cancelled) setHydrating(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // Mount-only: lienId is fixed for the lifetime of this page.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const companyTypesQuery = useCompanyTypes();
  const fundingCompanyType = companyTypesQuery.data?.find(
    (t) => t.code === "FundingCompany",
  );
  const companiesQuery = useCompanies(
    { companyTypeId: fundingCompanyType?.id },
    { enabled: Boolean(fundingCompanyType?.id) },
  );
  const companies = companiesQuery.options;
  const selectedCompany = companies.find((c) => c.value === companyId);

  useEffect(() => {
    if (!companyId) return;
    selectedCompanyRef.current?.scrollIntoView({
      block: "center",
      behavior: "smooth",
    });
  }, [companyId, companies]);

  const filteredCompanies = useMemo(() => {
    if (!companySearch.trim()) return companies;
    const q = companySearch.trim().toLowerCase();
    return companies.filter((c) => c.label.toLowerCase().includes(q));
  }, [companies, companySearch]);

  const handleContinue = async () => {
    if (!companyId) return;
    if (!contactId) {
      setShowContactRequiredError(true);
      return;
    }
    setSavingBuyerSelection(true);
    try {
      await liensService.saveProviderFundingDetails(lienId, {
        fundingCompanyId: companyId,
        fundingCompanyContactId: contactId || undefined,
      });
      goToStep(router, lienId, 2);
    } catch (err) {
      toast.error(err instanceof Error
          ? err.message
          : "Failed to save funding company selection");
    } finally {
      setSavingBuyerSelection(false);
    }
  };

  if (hydrating) {
    return <BuyerSelectionSkeleton />;
  }

  return (
    <div className="max-w-4xl mx-auto space-y-6 pb-10">
      <div className="flex items-center gap-4">
        <Link
          href={`/selling/portfolio/lien/${lienId}`}
          className="text-gray-400 hover:text-gray-600"
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <div className="flex-1 flex gap-2">
          {Array.from({ length: TOTAL_STEPS }, (_, index) => (
            <div
              key={index}
              className={`h-1 flex-1 rounded-full ${index < 1 ? "bg-[#EE7132]" : "bg-gray-200"}`}
            />
          ))}
        </div>
      </div>

      <div className="space-y-4">
        <p className="text-xs text-gray-400">Step 1/{TOTAL_STEPS}</p>
        <h1 className="text-2xl font-bold text-gray-900">
          Select a Funding Company
        </h1>
        <p className="text-sm text-gray-500">
          Choose the funding company that will receive this lien for review
          and potential purchase.
        </p>

        <Field
          type="text"
          label=""
          placeholder="Search..."
          value={companySearch}
          onChange={setCompanySearch}
          prefix={<Search className="h-4 w-4" />}
        />

        <div className="border border-gray-200 rounded-lg overflow-hidden">
          <div className="max-h-[250px] overflow-y-auto">
            {filteredCompanies.length === 0 && (
              <p className="px-4 py-6 text-sm text-gray-400 text-center">
                No funding companies found.
              </p>
            )}
            {filteredCompanies.map((company) => (
              <label
                key={company.value}
                ref={
                  companyId === company.value ? selectedCompanyRef : undefined
                }
                className="flex items-center gap-3 px-4 py-3 border-b border-gray-100 last:border-0 cursor-pointer hover:bg-gray-50"
              >
                <input
                  type="radio"
                  name="fundingCompany"
                  checked={companyId === company.value}
                  onChange={() => {
                    setCompanyId(company.value);
                    setContactId("");
                    setShowContactRequiredError(false);
                  }}
                  className="accent-[#EE7132]"
                />
                <span className="text-sm text-gray-700">{company.label}</span>
              </label>
            ))}
          </div>
          <button
            type="button"
            onClick={() => setShowAddCompany(true)}
            className="flex w-full items-center gap-1.5 text-left px-4 py-3 text-sm font-semibold text-primary bg-white border-t border-gray-100 hover:bg-gray-50"
          >
            <Plus className="h-3.5 w-3.5" />
            Add Company
          </button>
        </div>

        {companyId && (
          <FundingCompanyContactField
            companyId={companyId}
            companyName={selectedCompany?.label ?? ""}
            value={contactId}
            required
            onChange={(id) => {
              setContactId(id);
              if (id) setShowContactRequiredError(false);
            }}
          />
        )}
        {showContactRequiredError && (
          <p className="text-xs text-red-500 mt-1">
            Select a contact person to continue.
          </p>
        )}

        <div className="flex justify-end gap-3 pt-4">
          <Link
            href={`/selling/portfolio/lien/${lienId}`}
            className="text-sm px-4 py-2 border border-gray-200 rounded-lg hover:bg-gray-50 text-gray-600"
          >
            Cancel
          </Link>
          <Button
            variant="primary"
            disabled={!companyId || !contactId || savingBuyerSelection}
            loading={savingBuyerSelection}
            onClick={handleContinue}
          >
            {savingBuyerSelection ? "Saving..." : "Continue"}
          </Button>
        </div>
      </div>

      {showAddCompany && fundingCompanyType?.id && (
        <CompanyFormModal
          open={showAddCompany}
          title="Add Funding Company"
          companyTypeId={fundingCompanyType.id}
          lockCompanyType
          onClose={() => setShowAddCompany(false)}
          onSaved={(company) => {
            setCompanyId(company.id);
            setContactId("");
            setShowContactRequiredError(false);
            setShowAddCompany(false);
          }}
        />
      )}
    </div>
  );
}
