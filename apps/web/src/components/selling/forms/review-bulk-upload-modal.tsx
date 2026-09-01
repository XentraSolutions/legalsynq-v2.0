"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { ColumnDef, PaginationState } from "@tanstack/react-table";
import { ChevronDown, ChevronUp, Plus, TriangleAlert } from "lucide-react";
import { Modal } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";
import { BaseSelect } from "@/components/ui/base-select";
import { BaseTable } from "@/components/ui/base-table";
import { CompanyFormModal } from "@/components/selling/forms/company-form-modal";
import { liensService } from "@/lib/selling";
import { applyCsvColumnCorrections } from "@/lib/selling/csv-utils";
import { nameSimilarity } from "@/lib/selling/string-similarity";
import { useCompanyTypes, useCompanies } from "@/hooks/selling/use-selling-companies";
import type { BulkImportRowItem } from "@/lib/selling/liens.types";
import type { SellingEntityType } from "@/components/selling/selling-entity-select";
import {
  TABLE_CELL_CLASSNAME,
  TABLE_HEADER_CLASSNAME,
  TABLE_HEADER_CELL_CLASSNAME,
} from "@/components/selling/table-cell-styles";

interface ReviewBulkUploadModalProps {
  open: boolean;
  importId: string | null;
  onClose: () => void;
  onConfirm: () => void;
  confirming?: boolean;
  /**
   * The exact CSV text last sent to `/bulk-imports` — the source the
   * unmatched-entities panel rewrites to apply a correction. Not available
   * for .xlsx uploads, in which case the panel still flags unmatched
   * entities but can't offer to fix them here (see `canApplyCorrections`).
   */
  sourceCsvText?: string | null;
  /**
   * Re-uploads `correctedCsvText` as a replacement for the import currently
   * under review (cancelling the old one) and points this modal at the new
   * importId once it's validated. The confirm endpoint only ever
   * exact-matches a row's raw text against existing records, so this is
   * what actually makes a corrected entity name link on Confirm & Process.
   */
  onApplyCorrections?: (correctedCsvText: string) => Promise<void>;
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";
const ALL_ROWS_PAGE_SIZE = 100;
const ALL_ROWS_PAGE_CAP = 50; // 5,000 rows — generous ceiling for client-side detection.
const SUGGESTION_THRESHOLD = 0.5;

interface ParsedRow extends BulkImportRowItem {
  data: Record<string, string>;
}

function parseRowData(row: BulkImportRowItem): Record<string, string> {
  try {
    return JSON.parse(row.dataJson) ?? {};
  } catch {
    return {};
  }
}

function formatCurrency(value?: string): string {
  if (!value) return "—";
  const amount = Number(value.replace(/[^0-9.-]/g, ""));
  if (!Number.isFinite(amount)) return value;
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(amount);
}

// The bulk-import template's column set (SellingBulkImportTemplateColumns on
// the backend) can change independently of this component, and dataJson is
// keyed by those exact column names. Rather than hand-maintain a matching
// list here (which drifted out of sync before), derive the columns straight
// from the row data returned by GET /bulk-imports/{id}/rows. Operational
// defaults can remain in that data without appearing in the user-facing table.
const CURRENCY_FIELD_PATTERN = /cost|amount/i;
const HIDDEN_PREVIEW_FIELDS = new Set([
  "listing visibility",
  "lien visibility",
]);

function toHeader(fieldKey: string): string {
  return fieldKey.replace(/\*$/, "");
}

function buildColumns(rows: ParsedRow[]): ColumnDef<ParsedRow, any>[] {
  const keys: string[] = [];
  const seen = new Set<string>();
  for (const row of rows) {
    for (const key of Object.keys(row.data)) {
      if (
        !HIDDEN_PREVIEW_FIELDS.has(key.trim().toLowerCase()) &&
        !seen.has(key)
      ) {
        seen.add(key);
        keys.push(key);
      }
    }
  }

  return keys.map((key) => {
    const currency = CURRENCY_FIELD_PATTERN.test(key);
    return {
      id: key,
      header: toHeader(key),
      meta: currency ? { align: "right" } : undefined,
      cell: ({ row }) => {
        const raw = row.original.data[key];
        return (
          <span className={TABLE_CELL_CLASSNAME}>
            {currency ? formatCurrency(raw) : raw || "—"}
          </span>
        );
      },
    };
  });
}

// Mirrors SellingBulkImportSchema on the backend (Liens.Api/Endpoints) — the
// three free-text columns the confirm endpoint resolves by exact name match
// against an existing Facility/Provider/FundingCompany record, and so the
// only columns this panel checks for unmatched entities.
const ENTITY_FIELDS: { header: string; entityType: SellingEntityType; noun: string }[] = [
  { header: "Funding Company", entityType: "FundingCompany", noun: "Funding Company" },
  { header: "Facility Name*", entityType: "MedicalFacility", noun: "Medical Facility" },
  { header: "Medical Provider", entityType: "MedicalProvider", noun: "Medical Provider" },
];

interface UnmatchedGroup {
  header: string;
  entityType: SellingEntityType;
  noun: string;
  /** The imported text, in the casing it first appeared with. */
  value: string;
  rowCount: number;
}

// Fetches every row of the import (not just the visible page) so the
// unmatched-entities panel reflects the whole file, not page 1.
function useAllBulkImportRows(importId: string | null, enabled: boolean) {
  return useQuery({
    queryKey: ["bulk-import-all-rows", importId],
    queryFn: async () => {
      const first = await liensService.getBulkImportRows(importId as string, {
        status: "all",
        page: 1,
        pageSize: ALL_ROWS_PAGE_SIZE,
      });
      const totalPages = Math.min(
        Math.ceil(first.totalCount / ALL_ROWS_PAGE_SIZE),
        ALL_ROWS_PAGE_CAP,
      );
      const rest = await Promise.all(
        Array.from({ length: Math.max(0, totalPages - 1) }, (_, i) =>
          liensService.getBulkImportRows(importId as string, {
            status: "all",
            page: i + 2,
            pageSize: ALL_ROWS_PAGE_SIZE,
          }),
        ),
      );
      return [first, ...rest].flatMap((page) => page.items);
    },
    enabled: enabled && !!importId,
    staleTime: 30_000,
  });
}

export function ReviewBulkUploadModal({
  open,
  importId,
  onClose,
  onConfirm,
  confirming,
  sourceCsvText,
  onApplyCorrections,
}: ReviewBulkUploadModalProps) {
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const [panelExpanded, setPanelExpanded] = useState(true);
  const [applyingKey, setApplyingKey] = useState<string | null>(null);
  const [overrides, setOverrides] = useState<Record<string, string>>({});
  const [creatingGroup, setCreatingGroup] = useState<UnmatchedGroup | null>(null);

  const { data, isLoading } = useQuery({
    queryKey: ["bulk-import-rows", importId, pagination.pageIndex, pagination.pageSize],
    queryFn: () =>
      liensService.getBulkImportRows(importId as string, {
        status: "all",
        page: pagination.pageIndex + 1,
        pageSize: pagination.pageSize,
      }),
    enabled: open && !!importId,
    staleTime: 30_000,
  });

  const rows: ParsedRow[] = useMemo(
    () => (data?.items ?? []).map((item) => ({ ...item, data: parseRowData(item) })),
    [data],
  );

  const columns = useMemo(() => buildColumns(rows), [rows]);

  const canApplyCorrections = Boolean(sourceCsvText && onApplyCorrections);

  const allRowsQuery = useAllBulkImportRows(importId, open);
  const allRows: ParsedRow[] = useMemo(
    () => (allRowsQuery.data ?? []).map((item) => ({ ...item, data: parseRowData(item) })),
    [allRowsQuery.data],
  );

  const companyTypesQuery = useCompanyTypes({ enabled: open });
  const fundingCompanyType = companyTypesQuery.data?.find((t) => t.code === "FundingCompany");
  const facilityType = companyTypesQuery.data?.find((t) => t.code === "MedicalFacility");
  const providerType = companyTypesQuery.data?.find((t) => t.code === "MedicalProvider");
  const companyTypeIdByEntityType: Record<SellingEntityType, string | undefined> = {
    FundingCompany: fundingCompanyType?.id,
    MedicalFacility: facilityType?.id,
    MedicalProvider: providerType?.id,
    LawFirm: undefined,
  };

  const fundingCompaniesQuery = useCompanies(
    { companyTypeId: fundingCompanyType?.id, pageSize: 200 },
    { enabled: open && Boolean(fundingCompanyType?.id) },
  );
  const facilitiesQuery = useCompanies(
    { companyTypeId: facilityType?.id, pageSize: 200 },
    { enabled: open && Boolean(facilityType?.id) },
  );
  const providersQuery = useCompanies(
    { companyTypeId: providerType?.id, pageSize: 200 },
    { enabled: open && Boolean(providerType?.id) },
  );
  const optionsByEntityType: Record<SellingEntityType, { value: string; label: string }[]> = {
    FundingCompany: fundingCompaniesQuery.options,
    MedicalFacility: facilitiesQuery.options,
    MedicalProvider: providersQuery.options,
    LawFirm: [],
  };

  const unmatchedGroups: UnmatchedGroup[] = useMemo(() => {
    if (allRows.length === 0) return [];
    const groups = new Map<string, UnmatchedGroup>();
    for (const field of ENTITY_FIELDS) {
      const canonical = new Set(
        optionsByEntityType[field.entityType].map((o) => o.label.trim().toLowerCase()),
      );
      for (const row of allRows) {
        const raw = row.data[field.header]?.trim();
        if (!raw) continue;
        if (canonical.has(raw.toLowerCase())) continue;
        const key = `${field.header}::${raw.toLowerCase()}`;
        const existing = groups.get(key);
        if (existing) {
          existing.rowCount += 1;
        } else {
          groups.set(key, {
            header: field.header,
            entityType: field.entityType,
            noun: field.noun,
            value: raw,
            rowCount: 1,
          });
        }
      }
    }
    return Array.from(groups.values()).sort((a, b) => b.rowCount - a.rowCount);
  }, [allRows, optionsByEntityType]);

  const suggestionFor = (group: UnmatchedGroup) => {
    const candidates = optionsByEntityType[group.entityType];
    const scored = candidates
      .map((c) => ({ ...c, score: nameSimilarity(group.value, c.label) }))
      .filter((c) => c.score >= SUGGESTION_THRESHOLD)
      .sort((a, b) => b.score - a.score);
    return scored[0];
  };

  const groupKey = (group: UnmatchedGroup) => `${group.header}::${group.value.toLowerCase()}`;

  const applyGroup = async (group: UnmatchedGroup, targetName: string) => {
    if (!sourceCsvText || !onApplyCorrections) return;
    const key = groupKey(group);
    setApplyingKey(key);
    try {
      const corrected = applyCsvColumnCorrections(sourceCsvText, {
        [group.header]: new Map([[group.value.trim().toLowerCase(), targetName]]),
      });
      await onApplyCorrections(corrected);
      setOverrides((prev) => {
        const next = { ...prev };
        delete next[key];
        return next;
      });
    } finally {
      setApplyingKey(null);
    }
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Review Bulk Upload Details"
      subtitle="Review your data before importing to ensure everything is accurate and ready to proceed."
      size="xl"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={confirming}>
            Cancel
          </Button>
          <Button variant="primary" onClick={onConfirm} loading={confirming}>
            {confirming ? "Processing..." : "Confirm & Process"}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <BaseTable
          columns={columns}
          data={rows}
          getRowId={(row) => row.id}
          isLoading={isLoading}
          manualPagination
          pagination={pagination}
          onPaginationChange={setPagination}
          pageCount={
            data ? Math.max(1, Math.ceil(data.totalCount / pagination.pageSize)) : 1
          }
          totalCount={data?.totalCount ?? 0}
          pageSizeOptions={PAGE_SIZE_OPTIONS}
          headerClassName={TABLE_HEADER_CLASSNAME}
          headerCellClassName={TABLE_HEADER_CELL_CLASSNAME}
          primaryButtonClassName={PRIMARY_BUTTON_CLASSNAME}
          emptyMessage="No rows found in this upload."
        />

        {unmatchedGroups.length > 0 && (
          <div className="rounded-xl border border-gray-200">
            <button
              type="button"
              onClick={() => setPanelExpanded((v) => !v)}
              className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left"
            >
              <div>
                <p className="flex items-center gap-2 text-sm font-semibold text-gray-900">
                  <TriangleAlert className="h-4 w-4 text-amber-500" />
                  Unmatched Entities Detected ({unmatchedGroups.length} Unique{" "}
                  {unmatchedGroups.length === 1 ? "String" : "Strings"} across{" "}
                  {unmatchedGroups.reduce((sum, g) => sum + g.rowCount, 0)} Rows)
                </p>
                <p className="mt-0.5 text-sm text-gray-500">
                  {canApplyCorrections
                    ? "Map these imported names to existing database entities before finalizing the import."
                    : "These imported names don't have a matching record yet — bulk correction isn't available for this file type; fix each one from the lien's Provider & Funding Details page after import."}
                </p>
              </div>
              {panelExpanded ? (
                <ChevronUp className="h-4 w-4 shrink-0 text-gray-400" />
              ) : (
                <ChevronDown className="h-4 w-4 shrink-0 text-gray-400" />
              )}
            </button>

            {panelExpanded && (
              <div className="overflow-x-auto border-t border-gray-200">
                <table className="w-full text-sm">
                  <thead className={TABLE_HEADER_CLASSNAME}>
                    <tr>
                      <th className={TABLE_HEADER_CELL_CLASSNAME}>Imported String</th>
                      <th className={TABLE_HEADER_CELL_CLASSNAME}>Row Count</th>
                      <th className={TABLE_HEADER_CELL_CLASSNAME}>Suggested Match</th>
                      <th className={TABLE_HEADER_CELL_CLASSNAME}>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {unmatchedGroups.map((group) => {
                      const key = groupKey(group);
                      const suggestion = suggestionFor(group);
                      const selectedValue = overrides[key] ?? suggestion?.value;
                      const selectedOption = optionsByEntityType[group.entityType].find(
                        (o) => o.value === selectedValue,
                      );
                      const score = selectedOption
                        ? nameSimilarity(group.value, selectedOption.label)
                        : undefined;
                      const isApplying = applyingKey === key;
                      return (
                        <tr key={key} className="border-t border-gray-100">
                          <td className={TABLE_CELL_CLASSNAME}>&ldquo;{group.value}&rdquo;</td>
                          <td className={TABLE_CELL_CLASSNAME}>{group.rowCount} Rows</td>
                          <td className={TABLE_CELL_CLASSNAME}>
                            <div className="flex items-center gap-1.5">
                              <BaseSelect
                                value={selectedValue}
                                onChange={(v) =>
                                  setOverrides((prev) => ({ ...prev, [key]: v }))
                                }
                                options={optionsByEntityType[group.entityType]}
                                placeholder="No Suggestion Found"
                                searchPlaceholder={`Search ${group.noun.toLowerCase()}s...`}
                                clearable
                                className="min-w-[180px]"
                              />
                              {score !== undefined && (
                                <span className="shrink-0 rounded-full bg-green-100 px-1.5 py-0.5 text-xs font-medium text-green-700">
                                  {Math.round(score * 100)}%
                                </span>
                              )}
                            </div>
                          </td>
                          <td className={TABLE_CELL_CLASSNAME}>
                            <div className="flex items-center gap-2">
                              {canApplyCorrections ? (
                                <>
                                  <Button
                                    type="button"
                                    variant="primary"
                                    disabled={!selectedValue || isApplying}
                                    loading={isApplying}
                                    onClick={() => {
                                      if (selectedOption) {
                                        void applyGroup(group, selectedOption.label);
                                      }
                                    }}
                                  >
                                    Apply to {group.rowCount} Rows
                                  </Button>
                                  <button
                                    type="button"
                                    className="flex shrink-0 items-center gap-1 text-sm font-medium text-[#EE7132] hover:text-[#D9672E]"
                                    disabled={isApplying}
                                    onClick={() => setCreatingGroup(group)}
                                  >
                                    <Plus className="h-4 w-4" />
                                    Create New Entity
                                  </button>
                                </>
                              ) : (
                                <span className="text-xs text-gray-400">
                                  Fix after import
                                </span>
                              )}
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>

      {creatingGroup && companyTypeIdByEntityType[creatingGroup.entityType] && (
        <CompanyFormModal
          open
          onClose={() => setCreatingGroup(null)}
          title={`Add ${creatingGroup.noun}`}
          companyTypeId={companyTypeIdByEntityType[creatingGroup.entityType] as string}
          lockCompanyType
          initialName={creatingGroup.value}
          onSaved={(created) => {
            const group = creatingGroup;
            setCreatingGroup(null);
            void applyGroup(group, created.name);
          }}
        />
      )}
    </Modal>
  );
}
