"use client";

import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { FormModal } from "@/components/selling/modal";
import { BaseSelect, type BaseSelectOption } from "@/components/ui/base-select";
import { useInfiniteCompanies, useReassignCompany } from "@/hooks/selling/use-selling-companies";
import type { Company } from "@/lib/selling/companies.types";

interface ReassignCompanyModalProps {
  open: boolean;
  company: Company;
  onClose: () => void;
  onReassigned?: () => void;
}

export function ReassignCompanyModal({
  open,
  company,
  onClose,
  onReassigned,
}: ReassignCompanyModalProps) {
  const [targetCompanyId, setTargetCompanyId] = useState("");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  useEffect(() => {
    const timeout = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timeout);
  }, [search]);

  const companiesQuery = useInfiniteCompanies(
    {
      companyTypeId: company.companyTypeId,
      isActive: true,
      search: debouncedSearch || undefined,
    },
    { enabled: open },
  );
  const reassignMutation = useReassignCompany();

  useEffect(() => {
    if (!open) {
      setTargetCompanyId("");
      setSearch("");
      setDebouncedSearch("");
    }
  }, [open]);

  // A debounced-search refetch in flight, distinct from the very first load
  // and from paginating — each has its own skeleton treatment in BaseSelect.
  const isSearching =
    companiesQuery.isFetching &&
    !companiesQuery.isLoading &&
    !companiesQuery.isFetchingNextPage;

  // Never offer the company itself as a reassign target.
  const options: BaseSelectOption[] = useMemo(
    () =>
      (companiesQuery.data?.pages ?? [])
        .flatMap((page) => page.items)
        .filter((c) => c.id !== company.id)
        .map((c) => ({ value: c.id, label: c.name })),
    [companiesQuery.data, company.id],
  );

  const handleSubmit = async () => {
    if (!targetCompanyId) return;
    try {
      await reassignMutation.mutateAsync({
        id: company.id,
        request: { targetCompanyId },
      });
      toast.success("Company reassigned", { description: company.name });
      onReassigned?.();
      onClose();
    } catch (err) {
      toast.error("Couldn't reassign company", {
        description: err instanceof Error ? err.message : company.name,
      });
    }
  };

  return (
    <FormModal
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title="Reassign Company"
      subtitle={`Move everything associated with ${company.name} to another company.`}
      submitLabel="Reassign"
      submitDisabled={!targetCompanyId}
      loading={reassignMutation.isPending}
    >
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Target Company<span className="text-red-500 ml-0.5">*</span>
        </label>
        <BaseSelect
          value={targetCompanyId}
          onChange={setTargetCompanyId}
          options={options}
          loadingMode="infinite"
          isLoading={companiesQuery.isLoading}
          isSearching={isSearching}
          isFetchingMore={companiesQuery.isFetchingNextPage}
          hasNextPage={companiesQuery.hasNextPage}
          onLoadMore={companiesQuery.fetchNextPage}
          search={search}
          onSearchChange={setSearch}
          filterLocally={false}
          placeholder="Select a company"
          searchPlaceholder="Search companies..."
        />
      </div>
    </FormModal>
  );
}
