"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Search } from "lucide-react";
import { liensService } from "@/lib/selling";
import { useToast } from "@/lib/toast-context";
import { Button } from "@/components/selling/button";
import { useCompanyTypes, useCompanies, useContactPersons } from "@/hooks/use-selling-companies";
import Field from "@/components/lien/field";
import { TOTAL_STEPS, goToStep } from "./shared";

export interface BuyerSelectionStepProps {
  lienId: string;
}

// Step 1/2 — pick the funding company (and optionally a contact) this lien
// will be sold to. Persisted on Continue via saveCaseInformation so the
// selection survives a refresh or a return visit.
export default function BuyerSelectionStep({ lienId }: BuyerSelectionStepProps) {
  const router = useRouter();
  const { show: showToast } = useToast();

  const [hydrating, setHydrating] = useState(true);
  const [caseId, setCaseId] = useState<string | undefined>(undefined);
  const [companySearch, setCompanySearch] = useState("");
  const [companyId, setCompanyId] = useState<string>("");
  const [contactSearch, setContactSearch] = useState("");
  const [contactId, setContactId] = useState<string>("");
  const [savingBuyerSelection, setSavingBuyerSelection] = useState(false);
  const selectedCompanyRef = useRef<HTMLLabelElement | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const lien = await liensService.getLienById(lienId);
        if (cancelled) return;
        setCaseId(lien.caseInformation?.id);
        if (lien.fundingCompany) {
          setCompanyId(lien.fundingCompany.id);
          if (lien.fundingCompany.contact) {
            setContactId(lien.fundingCompany.contact.id);
          }
        }
      } catch (err) {
        showToast(
          err instanceof Error ? err.message : "Failed to load lien",
          "error",
        );
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

  const contactPersonsQuery = useContactPersons(companyId || null, true, {
    enabled: Boolean(companyId),
  });
  const contacts = contactPersonsQuery.options;
  const loadingContacts = contactPersonsQuery.isLoading;

  // Auto-select the only contact when a company has exactly one.
  useEffect(() => {
    if (contacts.length === 1) {
      setContactId(contacts[0].value);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contactPersonsQuery.data]);

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

  // Cap the unfiltered render — the contacts lookup can return a lot of rows
  // for a single company, so render nothing until the user searches.
  const filteredContacts = useMemo(() => {
    if (!contactSearch.trim()) return contacts.slice(0, 25);
    const q = contactSearch.trim().toLowerCase();
    return contacts
      .filter((c) => c.label.toLowerCase().includes(q))
      .slice(0, 50);
  }, [contacts, contactSearch]);

  const handleContinue = async () => {
    if (!companyId) return;
    setSavingBuyerSelection(true);
    try {
      await liensService.saveCaseInformation(lienId, {
        fundingCompanyId: companyId,
        fundingCompanyContactId: contactId || undefined,
        caseId,
        createCaseIfMissing: !caseId,
      });
      goToStep(router, lienId, 2);
    } catch (err) {
      showToast(
        err instanceof Error
          ? err.message
          : "Failed to save funding company selection",
        "error",
      );
    } finally {
      setSavingBuyerSelection(false);
    }
  };

  if (hydrating) {
    return (
      <div className="p-10 text-center">
        <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
      </div>
    );
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

        <div className="border border-gray-200 rounded-lg max-h-96 overflow-y-auto">
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
                  setContactSearch("");
                }}
                className="accent-[#EE7132]"
              />
              <span className="text-sm text-gray-700">{company.label}</span>
            </label>
          ))}
        </div>

        {companyId && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Contact Person
            </label>
            {loadingContacts ? (
              <p className="text-xs text-gray-400">Loading contacts...</p>
            ) : contacts.length === 0 ? (
              <p className="text-xs text-gray-400">
                This funding company has no active contacts on file.
              </p>
            ) : (
              <>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 h-4 w-4" />
                  <input
                    type="text"
                    placeholder="Search contacts..."
                    value={contactSearch}
                    onChange={(e) => setContactSearch(e.target.value)}
                    className="w-full pl-9 pr-3 py-2 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary"
                  />
                </div>
                <div className="border border-gray-200 rounded-lg max-h-48 overflow-y-auto mt-2">
                  {filteredContacts.length === 0 && (
                    <p className="px-4 py-3 text-sm text-gray-400 text-center">
                      No contacts match your search.
                    </p>
                  )}
                  {filteredContacts.map((c) => (
                    <label
                      key={c.value}
                      className="flex items-center gap-3 px-4 py-2 border-b border-gray-100 last:border-0 cursor-pointer hover:bg-gray-50"
                    >
                      <input
                        type="radio"
                        name="fundingContact"
                        checked={contactId === c.value}
                        onChange={() => setContactId(c.value)}
                        className="accent-[#EE7132]"
                      />
                      <span className="text-sm text-gray-700">{c.label}</span>
                    </label>
                  ))}
                </div>
              </>
            )}
          </div>
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
            disabled={!companyId || savingBuyerSelection}
            loading={savingBuyerSelection}
            onClick={handleContinue}
          >
            {savingBuyerSelection ? "Saving..." : "Continue"}
          </Button>
        </div>
      </div>
    </div>
  );
}
