"use client";

import * as React from "react";
import {
  type ColumnDef,
  type ColumnFiltersState,
  type OnChangeFn,
  type PaginationState,
  type Row,
  type RowSelectionState,
  type SortingState,
  type Table as TanstackTable,
  flexRender,
  getCoreRowModel,
  getExpandedRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
} from "@tanstack/react-table";
import { Checkbox } from "@/components/ui/checkbox";
import { Pagination } from "@/components/ui/pagination";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

export interface BaseTableFooterCell {
  content: React.ReactNode;
  colSpan?: number;
  align?: "left" | "right";
  className?: string;
}

export interface BaseTableProps<TData> {
  columns: ColumnDef<TData, any>[];
  data: TData[];
  /** Row id used by selection/expansion state. Defaults to row index. */
  getRowId?: (row: TData) => string;

  // Sorting — internal by default; pass sorting+onSortingChange+manualSorting for server-driven.
  sorting?: SortingState;
  onSortingChange?: OnChangeFn<SortingState>;
  manualSorting?: boolean;
  enableSorting?: boolean;

  // Filtering — global search box; pass globalFilter+onGlobalFilterChange+manualFiltering for server-driven.
  globalFilter?: string;
  onGlobalFilterChange?: OnChangeFn<string>;
  columnFilters?: ColumnFiltersState;
  onColumnFiltersChange?: OnChangeFn<ColumnFiltersState>;
  manualFiltering?: boolean;

  // Pagination — internal by default; pass manualPagination+pageCount+pagination+onPaginationChange for server-driven.
  /** Set to false to render every row with no pagination controls. Defaults to true. */
  enablePagination?: boolean;
  manualPagination?: boolean;
  pageCount?: number;
  pagination?: PaginationState;
  onPaginationChange?: OnChangeFn<PaginationState>;
  pageSize?: number;
  /** Total row count across all pages (server-driven). Defaults to the current page's row count. */
  totalCount?: number;
  /** When set, renders a page-size dropdown ("Showing X-Y of Z entries") in the footer instead of the plain "Page X of Y" text. */
  pageSizeOptions?: number[];

  // Row selection — checkbox column rendered only when this is provided.
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: OnChangeFn<RowSelectionState>;
  enableRowSelection?: boolean | ((row: Row<TData>) => boolean);

  // Row expansion — chevron column rendered only when both are provided.
  enableExpanding?: boolean;
  renderSubRow?: (row: Row<TData>) => React.ReactNode;

  /** Optional whole-row click handler (e.g. open a preview drawer). Adds a pointer cursor to rows. */
  onRowClick?: (row: TData) => void;
  /** Per-row className, e.g. to style a row mid-deletion. */
  getRowClassName?: (row: TData) => string | undefined;

  // Footer / totals row. Mirrors LienTable's LienFooterCell shape (merged label cell + right-aligned totals).
  footerCells?: BaseTableFooterCell[];

  // Toolbar slot (search/filters/refresh/export) — composed by the consumer, not baked in.
  toolbar?: React.ReactNode;

  emptyMessage?: string;
  isLoading?: boolean;
  className?: string;
  /** Overrides the default bg-primary styling on the active pagination page button (e.g. selling's orange brand). */
  primaryButtonClassName?: string;
  /** Overrides the default header row background (e.g. selling's #F5F5F5). */
  headerClassName?: string;
  /** Overrides the default per-column header cell typography (e.g. selling's 14px/500). */
  headerCellClassName?: string;
}

export function BaseTable<TData>({
  columns,
  data,
  getRowId,
  sorting,
  onSortingChange,
  manualSorting = false,
  enableSorting = true,
  globalFilter,
  onGlobalFilterChange,
  columnFilters,
  onColumnFiltersChange,
  manualFiltering = false,
  enablePagination = true,
  manualPagination = false,
  pageCount,
  pagination,
  onPaginationChange,
  pageSize = 10,
  totalCount,
  pageSizeOptions,
  rowSelection,
  onRowSelectionChange,
  enableRowSelection,
  enableExpanding = false,
  renderSubRow,
  footerCells,
  toolbar,
  emptyMessage = "No records found",
  isLoading = false,
  className,
  onRowClick,
  getRowClassName,
  primaryButtonClassName,
  headerClassName,
  headerCellClassName,
}: BaseTableProps<TData>) {
  const selectable = rowSelection !== undefined;
  const expandable = enableExpanding && renderSubRow !== undefined;

  const [internalPagination, setInternalPagination] =
    React.useState<PaginationState>({
      pageIndex: 0,
      pageSize,
    });

  const effectivePagination = enablePagination
    ? (pagination ?? internalPagination)
    : { pageIndex: 0, pageSize: Math.max(data.length, 1) };

  const table = useReactTable({
    data,
    columns,
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
    // Only include keys that are actually controlled by the caller — spreading an
    // undefined value here (e.g. `sorting: undefined`) overrides TanStack's internal
    // default (`[]`, `{}`, ...) instead of leaving that piece of state uncontrolled,
    // which breaks row model computations like getSortedRowModel.
    state: {
      ...(sorting !== undefined ? { sorting } : {}),
      ...(globalFilter !== undefined ? { globalFilter } : {}),
      ...(columnFilters !== undefined ? { columnFilters } : {}),
      ...(rowSelection !== undefined ? { rowSelection } : {}),
      pagination: effectivePagination,
    },
    onSortingChange,
    onGlobalFilterChange,
    onColumnFiltersChange,
    onRowSelectionChange,
    onPaginationChange: onPaginationChange ?? setInternalPagination,
    enableRowSelection: enableRowSelection ?? selectable,
    enableSorting,
    manualSorting,
    manualFiltering,
    manualPagination,
    pageCount,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: manualSorting ? undefined : getSortedRowModel(),
    getFilteredRowModel: manualFiltering ? undefined : getFilteredRowModel(),
    getPaginationRowModel:
      enablePagination && !manualPagination
        ? getPaginationRowModel()
        : undefined,
    getExpandedRowModel: expandable ? getExpandedRowModel() : undefined,
  });

  const rows = table.getRowModel().rows;
  const totalCols =
    (selectable ? 1 : 0) +
    (expandable ? 1 : 0) +
    table.getVisibleLeafColumns().length;

  const handleHeaderClick = React.useCallback(
    (columnId: string) => {
      const nextSorting = (prevSorting: SortingState) => {
        const currentSort = prevSorting.find((item) => item.id === columnId);

        if (!currentSort) {
          return [{ id: columnId, desc: false }];
        }

        if (currentSort.desc === false) {
          return [{ id: columnId, desc: true }];
        }

        return [];
      };

      if (onSortingChange) {
        onSortingChange(nextSorting);
      } else {
        table.setSorting(nextSorting);
      }
    },
    [onSortingChange, table],
  );

  const { pageIndex, pageSize: currentPageSize } = table.getState().pagination;
  const pageCountResolved = table.getPageCount();
  const showPagination = enablePagination && pageCountResolved > 1;
  const totalRows = totalCount ?? table.getFilteredRowModel().rows.length;

  const setPagination = onPaginationChange ?? setInternalPagination;
  const handlePageSizeChange = (value: string) => {
    const nextPageSize = Number(value);
    if (!Number.isFinite(nextPageSize) || nextPageSize <= 0) return;
    setPagination({ pageIndex: 0, pageSize: nextPageSize });
  };

  const firstRow = totalRows === 0 ? 0 : pageIndex * currentPageSize + 1;
  const lastRow = Math.min(totalRows, (pageIndex + 1) * currentPageSize);

  return (
    <div
      className={cn(
        "border border-gray-100 rounded-lg overflow-hidden",
        className,
      )}
    >
      {toolbar}
      <Table>
        <TableHeader className={headerClassName}>
          {table.getHeaderGroups().map((headerGroup) => (
            <TableRow
              key={headerGroup.id}
              className="hover:bg-transparent border-b border-gray-100"
            >
              {selectable && (
                <TableHead className="w-10 normal-case">
                  <Checkbox
                    checked={table.getIsAllRowsSelected()}
                    onCheckedChange={(v) => table.toggleAllRowsSelected(!!v)}
                  />
                </TableHead>
              )}
              {expandable && (
                <TableHead className="w-7">
                  <span className="sr-only">Expand</span>
                </TableHead>
              )}
              {headerGroup.headers.map((header) => {
                const meta = header.column.columnDef.meta as
                  | {
                      align?: "left" | "right";
                      headerClassName?: string;
                      width?: string;
                      minWidth?: string;
                    }
                  | undefined;

                const align = meta?.align;
                const sortable = header.column.getCanSort();
                return (
                  <TableHead
                    key={header.id}
                    style={{
                      width: meta?.width,
                      minWidth: meta?.minWidth,
                    }}
                    className={cn(
                      "text-[12px] whitespace-nowrap",
                      align === "right" ? "text-right" : "text-left",
                      sortable && "cursor-pointer select-none",
                      headerCellClassName,
                      meta?.headerClassName,
                    )}
                    onClick={
                      sortable
                        ? () => handleHeaderClick(header.column.id)
                        : undefined
                    }
                  >
                    <span
                      className={cn(
                        "inline-flex items-center gap-1",
                        align === "right" && "flex-row-reverse",
                      )}
                    >
                      {header.isPlaceholder
                        ? null
                        : flexRender(
                            header.column.columnDef.header,
                            header.getContext(),
                          )}
                      {sortable && (
                        <i
                          className={cn(
                            "text-xs leading-none",
                            header.column.getIsSorted() === "asc"
                              ? "ri-arrow-up-line"
                              : header.column.getIsSorted() === "desc"
                                ? "ri-arrow-down-line"
                                : "ri-arrow-up-down-line",
                          )}
                        />
                      )}
                    </span>
                  </TableHead>
                );
              })}
            </TableRow>
          ))}
        </TableHeader>
        <TableBody>
          {isLoading ? (
            Array.from({ length: currentPageSize }).map((_, i) => (
              <TableRow key={i} className="animate-pulse">
                {Array.from({ length: totalCols }).map((_, j) => (
                  <TableCell key={j}>
                    <div className="h-4 bg-gray-100 rounded w-3/4" />
                  </TableCell>
                ))}
              </TableRow>
            ))
          ) : rows.length === 0 ? (
            <TableRow>
              <TableCell
                colSpan={totalCols}
                className="text-center py-8 text-sm text-gray-400"
              >
                {emptyMessage}
              </TableCell>
            </TableRow>
          ) : (
            rows.map((row) => (
              <React.Fragment key={row.id}>
                <TableRow
                  className={cn(
                    onRowClick && "cursor-pointer",
                    getRowClassName?.(row.original),
                  )}
                  onClick={
                    onRowClick ? () => onRowClick(row.original) : undefined
                  }
                >
                  {selectable && (
                    <TableCell>
                      {row.getCanSelect() && (
                        <Checkbox
                          checked={row.getIsSelected()}
                          onCheckedChange={(v) => row.toggleSelected(!!v)}
                        />
                      )}
                    </TableCell>
                  )}
                  {expandable && (
                    <TableCell className="pl-2">
                      <button
                        type="button"
                        onClick={() => row.toggleExpanded()}
                        aria-label={
                          row.getIsExpanded() ? "Collapse row" : "Expand row"
                        }
                        className="w-5 h-5 flex items-center justify-center rounded text-gray-400 hover:text-gray-600 hover:bg-gray-200/60 transition-colors"
                      >
                        <i
                          className={cn(
                            "ri-arrow-right-s-line text-sm transition-transform duration-150 leading-none",
                            row.getIsExpanded() && "rotate-90",
                          )}
                        />
                      </button>
                    </TableCell>
                  )}
                  {row.getVisibleCells().map((cell) => {
                    const meta = cell.column.columnDef.meta as
                      | {
                          align?: "left" | "right";
                          cellClassName?: string;
                          width?: string;
                          minWidth?: string;
                        }
                      | undefined;
                    return (
                      <TableCell
                        key={cell.id}
                        className={cn(
                          meta?.align === "right" && "text-right",
                          meta?.cellClassName,
                        )}
                        style={{
                          width: meta?.width,
                          minWidth: meta?.minWidth,
                        }}
                      >
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext(),
                        )}
                      </TableCell>
                    );
                  })}
                </TableRow>
                {expandable && row.getIsExpanded() && (
                  <TableRow className="bg-gray-50/70 hover:bg-gray-50/70">
                    <TableCell colSpan={totalCols} className="px-4 pb-3 pt-2">
                      {renderSubRow!(row)}
                    </TableCell>
                  </TableRow>
                )}
              </React.Fragment>
            ))
          )}
        </TableBody>
        {footerCells && (
          <tfoot>
            <TableRow className="border-t-2 border-gray-200 bg-gray-50/80 hover:bg-gray-50/80">
              {footerCells.map((cell, i) => (
                <TableCell
                  key={i}
                  colSpan={cell.colSpan}
                  className={cn(
                    cell.align === "right" && "text-right",
                    cell.className,
                  )}
                >
                  {cell.content}
                </TableCell>
              ))}
            </TableRow>
          </tfoot>
        )}
      </Table>
      {enablePagination && (
        <div className="flex items-center justify-between px-3 py-2 border-t border-gray-100">
          {pageSizeOptions ? (
            <div className="flex items-center gap-3 text-xs text-gray-500">
              <span className="flex items-center gap-2">
                Rows per page:
                <Select
                  value={String(currentPageSize)}
                  onValueChange={handlePageSizeChange}
                >
                  <SelectTrigger
                    className="h-7 w-[68px] px-2 text-xs"
                    aria-label="Rows per page"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {pageSizeOptions.map((size) => (
                      <SelectItem
                        key={size}
                        value={String(size)}
                        className="text-xs"
                      >
                        {size}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </span>
              <span>
                {totalRows === 0 ? "0" : `${firstRow}-${lastRow}`} of{" "}
                {totalRows}
              </span>
            </div>
          ) : (
            <span className="text-xs text-gray-500">
              Page {pageIndex + 1} of {Math.max(pageCountResolved, 1)} ·{" "}
              {totalRows} total
            </span>
          )}
          {showPagination && (
            <Pagination
              page={pageIndex + 1}
              totalPages={pageCountResolved}
              onPageChange={(p) => table.setPageIndex(p - 1)}
              primaryButtonClassName={primaryButtonClassName}
            />
          )}
        </div>
      )}
    </div>
  );
}

export type { TanstackTable };
