"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import type { ColumnDef } from "@tanstack/react-table";
import { casesService, type CaseDetail, type CaseLienItem } from "@/lib/cases";
import { StatusBadge } from "@/components/lien/status-badge";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import type { PaginationMeta } from "@/lib/billofsale";
import { EmailSection } from "../../components/email-section";
import { SmsSection } from "../../components/sms-section";
import { ContactsSection } from "../../components/contacts-section";
import { formatCurrency } from "../../utils/case-detail-utils";
import { LienListSection } from "./sections/lien-list-section";
import {
  LienUpdatesSection,
  type CaseLienUpdateRow,
} from "./sections/lien-updates-section";

export function LiensTab({
  caseId,
  liens,
  liensPagination,
  caseDetail,
  panelMode,
  onPanelModeChange,
  onAddMedicalLien,
}: {
  caseId: string;
  liens: CaseLienItem[];
  liensPagination: PaginationMeta;
  caseDetail: CaseDetail;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  onAddMedicalLien: (m: boolean) => void;
}) {
  const router = useRouter();
  const [search, setSearch] = useState("");

  const [liensUpdates, setLiensUpdates] = useState<CaseLienUpdateRow[]>([]);
  const [pagination, setPagination] = useState<PaginationMeta>(liensPagination);

  const fetchData = useCallback(async () => {
    const updates = await casesService.getCaseLiensUpdates(caseId);
    setLiensUpdates(
      Array.isArray(updates)
        ? updates.map((item) => ({
            ...item,
            lienId: (item as CaseLienUpdateRow).lienId ?? undefined,
          }))
        : [],
    );
  }, [caseId]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  /* TEMP: visual fallback data for UI review only */
  const displayLiens = liens.map((l) => {
    return {
      ...l,
      facility: l.facility || "---",
      facilityName: l.facilityName || "---",
      serviceDate: l.serviceDate || "---",
      purchaseDate: l.purchaseDate || "---",
      purchaseAmount: l.purchaseAmount || 0,
    };
  });

  const filtered = useMemo(() => {
    if (!search.trim()) return displayLiens;

    const q = search.toLowerCase();
    return displayLiens.filter((l) => {
      return (
        l.lienNumber.toLowerCase().includes(q) ||
        l.facilityName.toLowerCase().includes(q) ||
        l.lienType.toLowerCase().includes(q) ||
        l.status.toLowerCase().includes(q)
      );
    });
  }, [displayLiens, search]);

  const paginatedLiens = useMemo(() => {
    const startIndex = (pagination.page - 1) * pagination.pageSize;
    return filtered.slice(startIndex, startIndex + pagination.pageSize);
  }, [filtered, pagination.page, pagination.pageSize]);

  useEffect(() => {
    const totalCount = liensPagination.totalCount;
    const totalPages = Math.max(1, Math.ceil(totalCount / pagination.pageSize));
    const safePage = Math.min(pagination.page, totalPages);
    setPagination((prev) => {
      if (
        prev.totalCount === totalCount &&
        prev.totalPages === totalPages &&
        prev.page === safePage
      ) {
        return prev;
      }

      return { ...prev, totalCount, totalPages, page: safePage };
    });
  }, [filtered.length, pagination.page, pagination.pageSize]);

  const totalBilling = filtered.reduce(
    (sum, l) => sum + (l.originalAmount ?? 0),
    0,
  );
  const totalPurchase = filtered.reduce(
    (sum, l) => sum + (l.purchaseAmount ?? 0),
    0,
  );

  const lienRowColumns: ColumnDef<(typeof displayLiens)[number], any>[] = [
    {
      id: "lienId",
      header: "Lien ID",
      cell: ({ row }) => (
        <span
          className="text-xs font-mono cursor-pointer text-primary hover:underline"
          onClick={() => router.push(`/lien/cases/${caseId}/liens/${row.original.id}`)}
        >
          {row.original.id}
        </span>
      ),
    },
    {
      id: "facilityName",
      header: "Facility Name",
      cell: ({ row }) => (
        <span className="text-sm text-gray-600 truncate max-w-40 block">
          {row.original.facilityName}
        </span>
      ),
    },
    {
      id: "serviceDate",
      header: "Service Date",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.serviceDate}
        </span>
      ),
    },
    {
      id: "purchaseDate",
      header: "Purchase Date",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {row.original.purchaseDate}
        </span>
      ),
    },
    {
      id: "purchaseAmount",
      header: "Purchase Amt",
      meta: { align: "right" },
      cell: ({ row }) => (
        <span className="text-sm text-gray-700 tabular-nums">
          {formatCurrency(row.original.purchaseAmount)}
        </span>
      ),
    },
    {
      id: "originalAmount",
      header: "Billing Amt",
      meta: { align: "right" },
      cell: ({ row }) => (
        <span className="text-sm text-gray-700 font-medium tabular-nums">
          {formatCurrency(row.original.originalAmount)}
        </span>
      ),
    },
    {
      id: "status",
      header: "Status",
      cell: ({ row }) => <StatusBadge status={row.original.status} />,
    },
  ];

  const exportCaseLiens = async () => {
    const response = await casesService.exportCaseLiens({
      caseId: caseId,
      liensId: null,
      lawFirmId: null,
      medicalFacilityId: null,
      purchaseDate: null,
      caseManagerId: null,
      lienStatusId: null,
    });

    const src = `data:text/${response.data[0]?.export_format};base64,${response.data[0]?.base64}`;
    const link = document.createElement("a");
    link.href = src;
    link.download = response.data[0]?.filename;
    link.click();
    link.remove();
  };

  const leftContent = (
    <div className="space-y-4">
      <LienListSection
        search={search}
        onSearchChange={(v) => {
          setSearch(v);
          setPagination((prev) => ({ ...prev, page: 1 }));
        }}
        filtered={filtered}
        paginatedLiens={paginatedLiens}
        pagination={pagination}
        onPageChange={(page) => setPagination((p) => ({ ...p, page }))}
        columns={lienRowColumns}
        totalPurchase={totalPurchase}
        totalBilling={totalBilling}
        onAddMedicalLien={() => onAddMedicalLien(true)}
        onExport={exportCaseLiens}
      />

      <LienUpdatesSection
        liensUpdates={liensUpdates}
        entriesCount={liens.length}
      />
    </div>
  );

  const rightContent = (
    <div className="space-y-4">
      <EmailSection />
      <SmsSection />
      <ContactsSection
        items={[
          {
            icon: "ri-building-line",
            iconBgClass: "bg-blue-50",
            iconColorClass: "text-blue-500",
            name: caseDetail.insuranceCarrier || "",
            role: "Law Firm",
          },
        ]}
      />
    </div>
  );

  return (
    <LayoutSplit
      left={leftContent}
      right={rightContent}
      mode={panelMode}
      onModeChange={onPanelModeChange}
    />
  );
}
