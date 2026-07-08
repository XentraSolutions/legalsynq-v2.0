"use client";

export const dynamic = "force-dynamic";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { PageHeader } from "@/components/lien/page-header";
import { useLienStore } from "@/stores/lien-store";
import { useRoleAccess } from "@/hooks/use-role-access";
import { ApiError } from "@/lib/api-client";
import { batchService } from "@/lib/batch/batch.service";
import { PaginationMeta } from "@/lib/batch/batch.types";
import { useRouter } from "next/navigation";
import { ActionMenu } from "@/components/lien/action-menu";

export default function BatchListPage() {
  const ra = useRoleAccess();
  const addToast = useLienStore((s) => s.addToast);
  const router = useRouter();
  const [list, setList] = useState<any[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>({
    page: 1,
    pageSize: 50,
    totalCount: 0,
    totalPages: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const currentQuery = useCallback(
    () => ({
      page: 1,
      limit: 50,
      keyword: search || undefined,
      template: "",
      status: "",
    }),
    [search],
  );

  const fetchList = useCallback(async (query: PaginationMeta) => {
    setLoading(true);
    setError(null);
    try {
      const result = await batchService.getBatchList(query);
      setList(result.items);
      console.log(result);
      setPagination((prev) => ({
        ...prev,
        page: result.pagination.page,
        totalCount: result.pagination.totalCount,
      }));
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Failed to load liens");
      }
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchList(currentQuery());
  }, [search, statusFilter, fetchList, currentQuery]);

  const handlePageChange = (newPage: number) => {
    fetchList({
      ...currentQuery(),
      page: newPage,
      pageSize: pagination.pageSize,
    });
  };

  const canEdit = ra.can("lien:edit");

  return (
    <div className="space-y-5">
      <PageHeader
        title="Bulk Imports History"
        subtitle={loading ? "Loading..." : `${pagination?.totalCount}`}
        actions={
          <div className="relative">
            {/* Dropdown Button */}
            <button
              onClick={() => router.push("batch-entry/create")}
              className="flex items-center gap-1.5 text-sm font-medium text-white bg-primary hover:bg-primary/90 rounded-lg px-4 py-2 transition-colors"
            >
              Create
            </button>
          </div>
        }
      />

      {loading ? (
        <div className="p-10 text-center">
          <div className="inline-block h-6 w-6 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          <p className="text-sm text-gray-400 mt-2">Loading liens...</p>
        </div>
      ) : (
        <>
          <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Label
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Template
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      File
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Date
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Rows
                    </th>
                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Status
                    </th>

                    <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wide">
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {list.map((l) => (
                    //  ${selection.isSelected(l.id) ? "bg-primary/5" : ""
                    <tr
                      key={l.id}
                      className={`hover:bg-gray-50 transition-colors cursor-pointer}`}
                      // onClick={() => setPreviewId(l.id)}
                    >
                      <td className="px-4 py-3">{l.label}</td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {l.template}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {l.file}
                      </td>

                      <td className="px-4 py-3 text-xs text-gray-400 whitespace-nowrap">
                        {l.createdAt}
                        {l.updatedAt}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {l.rows}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {l.status}
                      </td>
                      <td
                        className="px-3 py-2.5 text-right"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <ActionMenu
                          items={[
                            {
                              label: "View",
                              icon: "ri-eye-line",
                              onClick: () => {},
                            },
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {list.length === 0 && !error && (
              <div className="p-10 text-center text-sm text-gray-400">
                No liens match your filters.
              </div>
            )}
          </div>

          {/* {pagination.totalPages && pagination.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <p className="text-sm text-gray-500">
                Page {pagination.page} of {pagination.totalPages} (
                {pagination.totalCount} total)
              </p>
              <div className="flex gap-2">
                <button
                  onClick={() => handlePageChange(pagination.page - 1)}
                  disabled={pagination.page <= 1}
                  className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
                >
                  Previous
                </button>
                <button
                  onClick={() => handlePageChange(pagination.page + 1)}
                  disabled={pagination.page >= pagination.totalPages}
                  className="text-sm px-3 py-1.5 border border-gray-200 rounded-lg hover:bg-gray-50 disabled:opacity-40"
                >
                  Next
                </button>
              </div>
            </div>
          )} */}
        </>
      )}
    </div>
  );
}
