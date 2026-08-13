"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { PageHeader } from "@/components/lien/page-header";
import { BaseTable } from "@/components/ui/base-table";
import { ColumnDef } from "@tanstack/react-table";
import {
  CaseHistoryItem,
  HistoryNoteType,
} from "@/lib/liens/lien-report.types";
import {
  useCaseNoteHistory,
  useCaseNoteHistoryExport,
} from "@/hooks/use-casenote-history";
import { PaginationMeta } from "@/lib/contacts";
import { Tabs } from "@/components/ui/tabs";
const TABS = [
  { key: "TRACKING", label: "Case Tracking Notes" },
  { key: "FEED", label: "Feed Notes" },
] as const;
type TabKey = (typeof TABS)[number]["key"];

const DEFAULT_PAGINATION = {
  page: 1,
  pageSize: 10,
  totalCount: 0,
  totalPages: 1,
};

export default function HistoryNotesPage() {
  const router = useRouter();
  const [selectedType, setSelectedType] = useState<HistoryNoteType>("TRACKING");

  // const [pagination, setPagination] = useState(1)
  const [pagination, setPagination] =
    useState<PaginationMeta>(DEFAULT_PAGINATION);
  const { data: notes, isLoading: loadingData } = useCaseNoteHistory({
    type: selectedType,
    request: {
      noteType: selectedType,
      page: pagination.page,
      limit: pagination.pageSize,
      sortBy: "noteDate",
      sortDirection: "desc",
    },
  });
  const { mutate: exportHistory, isPending: exporting } =
    useCaseNoteHistoryExport();

  const columns = useMemo<ColumnDef<CaseHistoryItem, any>[]>(
    () => [
      {
        id: "caseId",
        header: "Case ID",
        accessorFn: (row) => row.caseId,
        meta: { minWidth: "150px" },
        cell: ({ row }) => (
          <span className="text-sm">{row.original.caseId}</span>
        ),
      },
      {
        id: "caseName",
        header: "Case Name",
        accessorFn: (row) => row.caseName,
        meta: { minWidth: "150px" },
        cell: ({ row }) => (
          <span className="text-sm">{row.original.caseName}</span>
        ),
      },
      {
        id: "noteType",
        header: "Note Type",
        accessorFn: (row) => row.noteType,
        cell: ({ row }) => (
          <span className="text-sm">{row.original.noteType}</span>
        ),
      },
      {
        id: "noteAuthor",
        header: "Note Author",
        accessorFn: (row) => row.noteAuthor,
        cell: ({ row }) => (
          <span className="text-sm">{row.original.noteAuthor}</span>
        ),
      },
      {
        id: "noteContent",
        header: "Note Content",
        accessorFn: (row) => row.noteContent,
        meta: { minWidth: "180px" },
        cell: ({ row }) => (
          <span className="text-sm">{row.original.noteContent}</span>
        ),
      },
    ],
    [router],
  );

  return (
    <div className="space-y-4">
      <>
        <Tabs
          bordered={false}
          defaultTab={selectedType}
          onChange={(key) => {
            setSelectedType(key as TabKey);
            setPagination(DEFAULT_PAGINATION);
          }}
          tabs={TABS.map((tab) => ({
            key: tab.key,
            label: tab.label,
          }))}
        />

        {/* LIST */}
        <div className="overflow-x-auto">
          <BaseTable
            data={notes?.items ?? []}
            columns={columns}
            getRowId={(c) => c.noteId}
            isLoading={loadingData}
            emptyMessage="No data found."
            manualPagination
            enablePagination
            enableSorting={false}
            pageCount={notes?.totalPages}
            totalCount={notes?.totalCount}
            pagination={{
              pageIndex: pagination.page - 1,
              pageSize: pagination.pageSize,
            }}
            onPaginationChange={(updater) => {
              const next =
                typeof updater === "function"
                  ? updater({
                      pageIndex: pagination.page - 1,
                      pageSize: pagination.pageSize,
                    })
                  : updater;

              setPagination((prev) => ({ ...prev, page: next.pageIndex + 1 }));
            }}
            className="bg-white border-gray-200 rounded-xl"
          />
        </div>

        <div className="bg-white border border-gray-200 rounded-xl p-6 text-sm text-gray-500">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            {/* LEFT */}
            <button
              onClick={() => router.back()}
              className="px-3 py-2 border border-gray-200 rounded-lg text-sm self-start hover:shadow-sm"
            >
              Go Back
            </button>
            {/* RIGHT */}
            <div className="flex flex-wrap gap-2 sm:gap-2 sm:flex-row sm:items-center sm:justify-end">
              <button
                disabled={exporting}
                onClick={() =>
                  exportHistory({
                    type: selectedType,
                    request: {
                      noteType: selectedType,
                      page: pagination.page,
                      limit: pagination.pageSize,
                      sortBy: "noteDate",
                      sortDirection: "desc",
                    },
                  })
                }
                className="px-3 py-2 border border-gray-200 text-blue-500 rounded-lg text-sm hover:shadow-sm"
              >
                {exporting ? "Exporting..." : "Export CSV"}
              </button>
            </div>
          </div>
        </div>
      </>
    </div>
  );
}
