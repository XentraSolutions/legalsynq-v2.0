"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import type { ColumnDef } from "@tanstack/react-table";
import { useLienStore } from "@/stores/lien-store";
import { casesService, type CaseDetail, type CaseLienItem } from "@/lib/cases";
import { ApiError } from "@/lib/api-client";
import { StatusBadge } from "@/components/lien/status-badge";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import type {
  CreateMedicalCodeLiensDto,
  CreateMedicalFacilityDto,
  CreateMedicalLiensDto,
  CreateMedicalPaymentDto,
} from "@/lib/cases/cases.types";
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
import { MedicalLienDetailSection } from "./sections/medical-lien-detail-section";

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
  const [search, setSearch] = useState("");

  const [liensUpdates, setLiensUpdates] = useState<CaseLienUpdateRow[]>([]);
  const [lienId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [forms, setForms] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });

  const [data, setData] = useState<Record<number, any>>({
    [0]: undefined,
    [1]: undefined,
    [2]: undefined,
  });
  const addToast = useLienStore((s) => s.addToast);
  const [errors, setErrors] = useState<Record<string, string>>({});
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

  const fetchLienDetails = useCallback(async () => {
    if (lienId) {
      try {
        setLoading(true);
        const taskPromises = [
          casesService.getMedicalInfo(lienId),
          casesService.getMedicalFacility(lienId),
          casesService.getMedicalCodes(lienId),
          casesService.loadLiensDocuments(lienId),
          casesService.getPayee(lienId),
        ];

        // 2. Wait for ALL tasks to either resolve or reject
        const results = await Promise.allSettled(taskPromises);

        // 3. Process the results individually if needed
        results.forEach((result, index) => {
          if (result.status === "fulfilled") {
            if (result.value.data) {
              setData((prev) => ({
                ...prev,
                [index]: { ...result.value.data, hasInitialValue: true },
              }));
            }
          } else {
            console.error(`Task ${index} failed due to:`, result.reason);
          }
        });
      } catch (error) {
        // Promise.allSettled itself rarely throws unless input is invalid
        console.error("Unexpected execution error", error);
      } finally {
        setLoading(false);
      }
    }
  }, [lienId]);

  const fetchLienDocuments = useCallback(async () => {
    if (lienId) {
      try {
        const docs = await casesService.loadLiensDocuments(lienId);
        setData((prev) => ({
          ...prev,
          [3]: docs.data,
        }));
      } catch (error) {
        // Promise.allSettled itself rarely throws unless input is invalid
        console.error("Unexpected execution error", error);
      } finally {
        setLoading(false);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forms[3], lienId]);

  useEffect(() => {
    fetchData();
    fetchLienDetails();
  }, [fetchLienDetails, lienId]);
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

  useEffect(() => {}, [data[3]]);

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
          onClick={() => setSelectedId(row.original.id)}
        >
          {row.original.id}
        </span>
      ),
    },
    {
      id: "facilityName",
      header: "Facility Name",
      cell: ({ row }) => (
        <span className="text-sm text-gray-600 truncate max-w-[160px] block">
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
    {
      id: "view",
      header: "",
      meta: { align: "right" },
      cell: ({ row }) => (
        <Link
          href={`/lien/liens/${row.original.id}`}
          className="inline-flex items-center justify-center w-7 h-7 rounded hover:bg-gray-100 text-gray-400 hover:text-gray-600 transition-colors"
        >
          <i className="ri-eye-line text-sm" />
        </Link>
      ),
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

  function onFormValid(formData: any, index: number) {
    setForms((prev: Record<number, any>) => {
      const copy = prev;
      copy[index] = formData ?? copy[index];
      return copy;
    });
  }

  const dateConverter = (dateData: string) => {
    if (!dateData) return;

    const date = new Date(dateData);

    // Format the date using the US locale to automatically get MM/DD/YYYY
    const formatter = new Intl.DateTimeFormat("en-US", {
      month: "2-digit",
      day: "2-digit",
      year: "numeric",
    });

    const formattedDate = formatter.format(date);
    return formattedDate;
  };

  async function save() {
    try {
      // Implement save logic here (API call)
      Promise.allSettled([
        await saveMedicalLien({
          ...forms[0],
          purchaseDate: dateConverter(forms[0].purchaseDate),
          initialServiceDate: dateConverter(forms[0].initialServiceDate),
          endServiceDate: dateConverter(forms[0].endServiceDate),
        }),
        await saveMedicalFacilityLiens(forms[1]),

        forms[2]?.codeRows?.forEach(async (element: any) => {
          await updateMedicalCodeLiens({
            payee: forms[2].payee,
            outboundCheckNumber: forms[2].outboundCheckNumber,
            ...element,
          });
        }),
        await saveMedicalPayee(forms[2]),
      ]);

      addToast({
        type: "success",
        title: "Liens Updated",
        description: `Liens has been updated.`,
      });
      setSelectedId(null);
    } finally {
      // stopLoading();
    }
  }

  const saveMedicalLien = async (payload: CreateMedicalLiensDto) => {
    try {
      const request: CreateMedicalLiensDto = {
        id: forms[0].id,
        caseId: caseId,
        status: payload.status,
        purchaseDate: payload.purchaseDate,
        initialServiceDate: payload.initialServiceDate,
        endServiceDate: payload.endServiceDate,
        note: payload.note,
        isBulk: payload.isBulk == "true" ? "Yes" : "No",
        isServicing: payload.isServicing == "true" ? "Yes" : "No",
        fundingCompanyId: payload.fundingCompanyId,
      };
      !forms[0].hasInitialValue
        ? await casesService.createMedicalLiens(request)
        : await casesService.updateMedicalLiens(request);

      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.isConflict) {
          setErrors({ caseNumber: "A case with this number already exists" });
        } else {
          addToast({
            type: "error",
            title: "Create Failed",
            description: err.message,
          });
        }
      } else {
        addToast({
          type: "error",
          title: "Update Medical Information Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalFacilityLiens = async (
    payload: CreateMedicalFacilityDto,
  ) => {
    if (!payload.facilityId) return;
    try {
      if (!lienId) return;

      const request: CreateMedicalFacilityDto = {
        liensId: lienId,
        facilityId: payload.facilityId,
        facility: payload.facility,
        facilityContactId: payload.facilityContactId,
        facilityContact: payload.facilityContact,
        email: payload.email,
        medicalProviderId: payload.medicalProviderId,
        medicalProvider: payload.medicalProvider,
      };
      !forms[1].hasInitialValue
        ? await casesService.createMedicalFacilityLiens(request)
        : await casesService.updateMedicalFacilityLiens(request);
      addToast({
        type: "success",
        title: "Facility Updated",
        description: `Facility has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const saveMedicalPayee = async (payload: CreateMedicalPaymentDto) => {
    try {
      if (!lienId) return;

      const request: CreateMedicalPaymentDto = {
        id: null,
        liensId: lienId,
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      await casesService.createMedicalPaymentLiens(request);
      addToast({
        type: "success",
        title: "Payee Updated",
        description: `Payee has been updated.`,
      });
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const updateMedicalCodeLiens = async (payload: CreateMedicalCodeLiensDto) => {
    try {
      const request: CreateMedicalCodeLiensDto = {
        id: payload?.id?.includes("temp") ? null : payload.id,
        liensId: lienId ?? "",
        code: payload.code,
        medicareCost: parseFloat(payload.medicareCost).toFixed(2),
        billingAmount: parseFloat(payload.billingAmount).toFixed(2),
        purchaseAmount: parseFloat(payload.purchaseAmount).toFixed(2),
        payee: payload.payee,
        outboundCheckNumber: payload.outboundCheckNumber,
      };
      request.id == null
        ? await casesService.createMedicalCodeLiens(request)
        : await casesService.updateMedicalCodeLiens(request);
      setErrors({});
    } catch (err) {
      if (err instanceof ApiError) {
        addToast({
          type: "error",
          title: "Update Failed",
          description: err.message,
        });
      } else {
        addToast({
          type: "error",
          title: "Update Failed",
          description: "An unexpected error occurred",
        });
      }
    }
  };

  const leftContent = (
    <div className="space-y-4">
      {!lienId ? (
        <>
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
        </>
      ) : (
        <MedicalLienDetailSection
          caseId={caseId}
          lienId={lienId}
          loading={loading}
          data={data}
          onFormValid={onFormValid}
          onDocumentsUploaded={fetchLienDocuments}
          onGoBack={() => setSelectedId(null)}
          onSave={() => save()}
        />
      )}
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
