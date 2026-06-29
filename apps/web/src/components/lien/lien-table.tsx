"use client";

import * as React from "react";
import { useState } from "react";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
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
}

export function LienTable({
  liens,
  checkedIds,
  onToggleCheck,
  onToggleAll,
  columns,
  footer,
  emptyMessage = "No liens found",
  className,
  loadedAt,
  onRefresh,
  isRefreshing,
  expandable = true,
}: LienTableProps) {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const selectable = checkedIds !== undefined;
  const allChecked = selectable && liens.length > 0 && checkedIds!.size === liens.length;

  const toggleExpand = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const showLastLoaded = loadedAt !== undefined || onRefresh !== undefined;

  // checkbox col (when selectable) + optional chevron col + data columns
  const totalCols = (selectable ? 1 : 0) + (expandable ? 1 : 0) + columns.length;

  return (
    <div className={cn("border border-gray-100 rounded-lg overflow-hidden", className)}>
      {showLastLoaded && (
        <div className="flex items-center justify-between px-3 py-2 bg-white border-b border-gray-100">
          <span className="text-[11px] text-gray-400">
            Last loaded:{" "}
            {loadedAt
              ? loadedAt.toLocaleString(undefined, {
                  month: "short",
                  day: "numeric",
                  year: "numeric",
                  hour: "2-digit",
                  minute: "2-digit",
                  second: "2-digit",
                })
              : "—"}
          </span>
          <button
            type="button"
            onClick={onRefresh}
            disabled={!onRefresh || isRefreshing}
            className="flex items-center gap-1 text-[11px] text-gray-400 hover:text-primary transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <i className={cn("ri-refresh-line text-xs", isRefreshing && "animate-spin")} />
            {isRefreshing ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      )}
      <div className="overflow-x-auto">
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 bg-gray-50">
              {selectable && (
                <th className="px-3 py-2 text-left w-10">
                  <Checkbox
                    checked={allChecked}
                    onCheckedChange={onToggleAll}
                  />
                </th>
              )}
              {expandable && (
                <th className="w-7">
                  <span className="sr-only">Expand</span>
                </th>
              )}
              {columns.map((col) => (
                <th
                  key={col.id}
                  className={cn(
                    "px-3 py-2 text-[11px] font-medium text-gray-400 uppercase tracking-wide whitespace-nowrap",
                    col.align === "right" ? "text-right" : "text-left",
                  )}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {liens.length === 0 ? (
              <tr>
                <td
                  colSpan={totalCols}
                  className="px-3 py-8 text-center text-sm text-gray-400"
                >
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              liens.map((lien) => {
                const isChecked = selectable ? checkedIds!.has(lien.id) : false;
                const isExpanded = expandedIds.has(lien.id);
                return (
                  <React.Fragment key={lien.id}>
                    <tr className="hover:bg-gray-50/50 transition-colors">
                      {selectable && (
                        <td className="px-3 py-2.5">
                          <Checkbox
                            checked={checkedIds!.has(lien.id)}
                            onCheckedChange={() => onToggleCheck!(lien.id)}
                          />
                        </td>
                      )}
                      {expandable && (
                        <td className="pl-2 py-2.5">
                          <button
                            type="button"
                            onClick={() => toggleExpand(lien.id)}
                            aria-label={isExpanded ? "Collapse row" : "Expand row"}
                            className="w-5 h-5 flex items-center justify-center rounded text-gray-400 hover:text-gray-600 hover:bg-gray-200/60 transition-colors"
                          >
                            <i
                              className={cn(
                                "ri-arrow-right-s-line text-sm transition-transform duration-150 leading-none",
                                isExpanded && "rotate-90",
                              )}
                            />
                          </button>
                        </td>
                      )}
                      {columns.map((col) => (
                        <td
                          key={col.id}
                          className={cn(
                            "px-3 py-2.5",
                            col.align === "right" && "text-right",
                          )}
                        >
                          {col.cell(lien, isChecked)}
                        </td>
                      ))}
                    </tr>

                    {expandable && isExpanded && (
                      <tr className="bg-gray-50/70 border-t-0">
                        <td colSpan={totalCols} className="px-4 pb-3 pt-2">
                          <div className="flex items-center flex-wrap gap-x-6 gap-y-1">
                            <LienExpandedField
                              label="Facility"
                              value={lien.facility ?? "—"}
                            />
                            {lien.status && (
                              <LienExpandedField label="Status">
                                <Badge variant="outline">{lien.status}</Badge>
                              </LienExpandedField>
                            )}
                          </div>
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })
            )}
          </tbody>
          {footer && (
            <tfoot>
              <tr className="border-t-2 border-gray-200 bg-gray-50/80">
                {footer.map((cell, i) => (
                  <td
                    key={i}
                    colSpan={cell.colSpan}
                    className={cn(
                      "px-3 py-2.5",
                      cell.align === "right" && "text-right",
                      cell.className,
                    )}
                  >
                    {cell.content}
                  </td>
                ))}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
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
      {children ?? (
        <span className="text-xs text-gray-600">{value}</span>
      )}
    </div>
  );
}
