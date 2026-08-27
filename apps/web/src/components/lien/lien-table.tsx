"use client";

import * as React from "react";
import type {
  ColumnDef,
  OnChangeFn,
  RowSelectionState,
} from "@tanstack/react-table";
import {
  BaseTable,
  type BaseTableFooterCell,
} from "@/components/ui/base-table";
import { Badge } from "@/components/ui/badge";
import { DateDisplay } from "@/components/ui/date-display";
import { cn } from "@/lib/utils";
import type { CaseLienItem, CaseLienItemMetadata } from "@/lib/cases";

export type LienRow = CaseLienItem & CaseLienItemMetadata;

export interface LienColumnDef {
  id: string;
  header: string;
  align?: "left" | "right";
  cell: (lien: LienRow, isChecked: boolean) => React.ReactNode;
}

export interface LienFooterCell {
  content: React.ReactNode;
  colSpan?: number;
  align?: "left" | "right";
  className?: string;
}

interface LienTableProps {
  liens: LienRow[];
  columns: LienColumnDef[];
  /** Footer row cells. Colspan values must account for whether checkboxes are shown. */
  footer?: LienFooterCell[];
  emptyMessage?: string;
  className?: string;
  /**
   * When provided, renders a checkbox column for row selection.
   * Omit entirely for a read-only display table.
   */
  checkedIds?: Set<string>;
  onToggleCheck?: (id: string) => void;
  onToggleAll?: () => void;
  /** When provided, rows for which this returns false render a disabled, unselectable checkbox. */
  isRowSelectable?: (lien: LienRow) => boolean;
  /**
   * Timestamp of last fetch. When provided (even null), renders a "Last loaded" toolbar.
   * Omit to hide the toolbar entirely.
   */
  loadedAt?: Date | null;
  /** Enables the Refresh button in the "Last loaded" toolbar. */
  onRefresh?: () => void;
  /** Spins the refresh icon while a refetch is in progress. */
  isRefreshing?: boolean;
  /** Set to false to hide the expand/collapse chevron column. Defaults to true. */
  expandable?: boolean;
  /** Set to false to render every row with no pagination controls. Defaults to true. */
  paginated?: boolean;
  /** Rows per page when paginated. Defaults to 10. */
  pageSize?: number;
}

interface LienTableToolbarProps {
  loadedAt?: Date | null;
  onRefresh?: () => void;
  isRefreshing?: boolean;
  className?: string;
}

/**
 * "Last loaded" + Refresh toolbar shared by LienTable and any custom
 * empty-state markup that bypasses LienTable's own row rendering.
 */
export function LienTableToolbar({
  loadedAt,
  onRefresh,
  isRefreshing,
  className,
}: LienTableToolbarProps) {
  return (
    <div
      className={cn(
        "flex items-center justify-between px-3 py-2 bg-white border-b border-gray-100",
        className,
      )}
    >
      <span className="text-[11px] text-gray-400">
        Last loaded:{" "}
        {loadedAt ? (
          <DateDisplay value={loadedAt.toISOString()} format="datetime" />
        ) : (
          "—"
        )}
      </span>
      <button
        type="button"
        onClick={onRefresh}
        disabled={!onRefresh || isRefreshing}
        className="flex items-center gap-1 text-[11px] text-gray-400 hover:text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
      >
        <i
          className={cn(
            "ri-refresh-line text-xs",
            isRefreshing && "animate-spin",
          )}
        />
        {isRefreshing ? "Refreshing..." : "Refresh"}
      </button>
    </div>
  );
}

export function LienTable({
  liens,
  checkedIds,
  onToggleCheck,
  onToggleAll,
  isRowSelectable,
  columns,
  footer,
  emptyMessage = "No liens found",
  className,
  loadedAt,
  onRefresh,
  isRefreshing,
  expandable = true,
  paginated = true,
  pageSize = 10,
}: LienTableProps) {
  const selectable = checkedIds !== undefined;
  const showLastLoaded = loadedAt !== undefined || onRefresh !== undefined;

  const selectableLiens = React.useMemo(
    () => (isRowSelectable ? liens.filter(isRowSelectable) : liens),
    [liens, isRowSelectable],
  );

  const rowSelection = React.useMemo<RowSelectionState>(() => {
    const obj: RowSelectionState = {};
    checkedIds?.forEach((id) => {
      obj[id] = true;
    });
    return obj;
  }, [checkedIds]);

  const handleRowSelectionChange: OnChangeFn<RowSelectionState> = (updater) => {
    const next =
      typeof updater === "function" ? updater(rowSelection) : updater;
    const nextIds = new Set(Object.keys(next).filter((id) => next[id]));
    const prevIds = checkedIds ?? new Set<string>();

    if (nextIds.size === prevIds.size) return;

    const allNowSelected =
      nextIds.size === selectableLiens.length &&
      prevIds.size !== selectableLiens.length;
    const allNowDeselected =
      nextIds.size === 0 && prevIds.size === selectableLiens.length;
    if (allNowSelected || allNowDeselected) {
      onToggleAll?.();
      return;
    }

    const toggled = liens.find(
      (lien) => prevIds.has(lien.id) !== nextIds.has(lien.id),
    );
    if (toggled) onToggleCheck?.(toggled.id);
  };

  // `columns` and `checkedIds` are re-created by the caller on every render
  // (they close over form state like input values). TanStack's flexRender
  // treats a function passed as `cell` as a component *type*, so recomputing
  // this memo's `cell` functions on every keystroke makes React remount each
  // cell instead of updating it — dropping input focus. Route through refs
  // so the `cell` function identity stays stable while still reading live
  // values, and only rebuild the memo when the column shape itself changes.
  const columnsRef = React.useRef(columns);
  columnsRef.current = columns;
  const checkedIdsRef = React.useRef(checkedIds);
  checkedIdsRef.current = checkedIds;

  const columnIds = columns.map((col) => col.id).join("|");

  const tanstackColumns = React.useMemo<ColumnDef<LienRow, any>[]>(
    () =>
      columnsRef.current.map((col) => ({
        id: col.id,
        header: col.header,
        meta: { align: col.align },
        enableSorting: false,
        cell: ({ row }) => {
          const currentCol = columnsRef.current.find((c) => c.id === col.id)!;
          return currentCol.cell(
            row.original,
            selectable ? checkedIdsRef.current!.has(row.original.id) : false,
          );
        },
      })),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- columnsRef/checkedIdsRef read live values; only the column shape (ids) and selectability should force a rebuild
    [columnIds, selectable],
  );

  const footerCells: BaseTableFooterCell[] | undefined = footer?.map(
    (cell) => ({
      content: cell.content,
      colSpan: cell.colSpan,
      align: cell.align,
      className: cell.className,
    }),
  );

  return (
    <BaseTable
      data={liens}
      columns={tanstackColumns}
      getRowId={(lien) => lien.id}
      rowSelection={selectable ? rowSelection : undefined}
      onRowSelectionChange={handleRowSelectionChange}
      enableRowSelection={
        selectable && isRowSelectable
          ? (row) => isRowSelectable(row.original)
          : undefined
      }
      enablePagination={paginated}
      pageSize={pageSize}
      enableExpanding={expandable}
      renderSubRow={(row) => <LienExpandedRow lien={row.original} />}
      footerCells={footerCells}
      emptyMessage={emptyMessage}
      className={className}
      toolbar={
        showLastLoaded ? (
          <LienTableToolbar
            loadedAt={loadedAt}
            onRefresh={onRefresh}
            isRefreshing={isRefreshing}
          />
        ) : undefined
      }
    />
  );
}

function LienExpandedRow({ lien }: { lien: LienRow }) {
  return (
    <div className="flex items-center flex-wrap gap-x-6 gap-y-1">
      {lien.status && (
        <LienExpandedField label="Status">
          <Badge variant="outline">{lien.status}</Badge>
        </LienExpandedField>
      )}
    </div>
  );
}

function LienExpandedField({
  label,
  value,
  children,
}: {
  label: string;
  value?: string;
  children?: React.ReactNode;
}) {
  return (
    <div className="flex items-center gap-1.5">
      <span className="text-[10px] font-medium text-gray-400 uppercase tracking-wide">
        {label}
      </span>
      {children ?? <span className="text-xs text-gray-600">{value}</span>}
    </div>
  );
}
