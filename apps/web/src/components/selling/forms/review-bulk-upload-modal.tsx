"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { ColumnDef, PaginationState } from "@tanstack/react-table";
import { Modal } from "@/components/selling/modal";
import { Button } from "@/components/selling/button";
import { BaseTable } from "@/components/ui/base-table";
import { liensService } from "@/lib/selling";
import type { BulkImportRowItem } from "@/lib/selling/liens.types";
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
}

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];
const PRIMARY_BUTTON_CLASSNAME = "bg-[#EE7132] hover:bg-[#EE7132]/90 text-white";

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

export function ReviewBulkUploadModal({
  open,
  importId,
  onClose,
  onConfirm,
  confirming,
}: ReviewBulkUploadModalProps) {
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });

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
    </Modal>
  );
}
