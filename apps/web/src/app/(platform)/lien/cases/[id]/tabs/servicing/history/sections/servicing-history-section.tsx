import type { ColumnDef } from "@tanstack/react-table";
import { BaseTable } from "@/components/ui/base-table";
import { Pagination } from "@/components/ui/pagination";
import { LienTableToolbar } from "@/components/lien/lien-table";
import type { SettlementHistoryItemV3 } from "@/lib/settlement/settlement.types";
import { CollapsibleSection } from "../../../../components/collapsible-section";

export function ServicingHistorySection({
  isLoading,
  isFetching,
  historyItems,
  historyColumns,
  historyLoadedAt,
  onRefresh,
  historyPage,
  historyTotalPages,
  historyTotalCount,
  onPageChange,
}: {
  isLoading: boolean;
  isFetching: boolean;
  historyItems: SettlementHistoryItemV3[];
  historyColumns: ColumnDef<SettlementHistoryItemV3, any>[];
  historyLoadedAt: Date | null;
  onRefresh: () => void;
  historyPage: number;
  historyTotalPages: number;
  historyTotalCount: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <CollapsibleSection title="Servicing History" icon="ri-history-line">
      {isLoading ? (
        <div className="border border-gray-100 rounded-lg overflow-hidden">
          <LienTableToolbar
            loadedAt={historyLoadedAt}
            onRefresh={onRefresh}
            isRefreshing={isFetching}
          />
          <div className="text-center py-8">
            <div className="inline-block h-5 w-5 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <p className="text-sm text-gray-400 mt-2">Loading history...</p>
          </div>
        </div>
      ) : historyItems.length === 0 ? (
        <div className="border border-gray-100 rounded-lg overflow-hidden">
          <LienTableToolbar
            loadedAt={historyLoadedAt}
            onRefresh={onRefresh}
            isRefreshing={isFetching}
          />
          <div className="text-center py-8">
            <i className="ri-history-line text-2xl text-gray-300" />
            <p className="text-sm text-gray-400 mt-2">No history records</p>
          </div>
        </div>
      ) : (
        <>
          <BaseTable
            columns={historyColumns}
            data={historyItems}
            getRowId={(h) => h.id}
            enablePagination={false}
            toolbar={
              <LienTableToolbar
                loadedAt={historyLoadedAt}
                onRefresh={onRefresh}
                isRefreshing={isFetching}
              />
            }
          />
          <div className="mt-3 flex items-center justify-between">
            <p className="text-xs text-gray-400">
              Page {historyPage} of {historyTotalPages} · {historyTotalCount}{" "}
              total
            </p>
            {historyTotalPages > 1 && (
              <Pagination
                page={historyPage}
                totalPages={historyTotalPages}
                onPageChange={onPageChange}
              />
            )}
          </div>
        </>
      )}
    </CollapsibleSection>
  );
}
