"use client";

import { useEffect, useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import { useQueryClient } from "@tanstack/react-query";
import { useLienStore } from "@/stores/lien-store";
import { useTimezone } from "@/lib/use-timezone";
import { useSessionContext } from "@/providers/session-provider";
import {
  useCaseLiens,
  CASE_PAYMENTS_QUERY_KEY,
  SETTLEMENT_PAYMENT_DETAILS_QUERY_KEY,
  useUpdateServicingDetails,
} from "@/hooks/use-case-liens";
import { useSettlementHistory } from "@/hooks/use-settlement-history";
import { LayoutSplit, type PanelMode } from "@/components/lien/layout-split";
import type {
  CaseDetail,
  CaseLienItem,
  CaseLienItemMetadata,
} from "@/lib/cases";
import { contactsService } from "@/lib/contacts";
import { servicingService } from "@/lib/servicing";
import { ApiError } from "@/lib/api-client";
import type { SettlementHistoryItemV3 } from "@/lib/settlement/settlement.types";
import { SetupReductionForm } from "../../components/setup-reduction-form";
import { NoRecoveryForm } from "../../components/no-recovery-form";
import { AddPaymentForm } from "../../components/add-payment-form";
import { FeedsSection } from "../../components/feeds-section";
import {
  formatNoteTimestamp,
  describeSettlementHistoryItem,
} from "../../utils/case-detail-utils";
import { PaymentHistoryWidget } from "../../widgets/payment-history-widget";
import { ServicingDetailsSection } from "./servicing-details/sections/servicing-details-section";
import { OpenLiensSection } from "./settlement-details/sections/open-liens-section";
import { ClosedLiensSection } from "./settlement-details/sections/closed-liens-section";
import { ServicingHistorySection } from "./history/sections/servicing-history-section";
import { SERVICING_SUB_TABS, type ServicingSubTab } from "./types";
import { dateConvertertoIso } from "@/lib/cases/cases.mapper";
import { settlementService } from "@/lib/settlement";
import { ConfirmDialog } from "@/components/lien/modal";

function toDateInputValue(value: string): string {
  if (!value) return "";

  const isoDate = /^(\d{4}-\d{2}-\d{2})/.exec(value);
  if (isoDate) return isoDate[1];

  const legacyDate = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
  return legacyDate
    ? `${legacyDate[3]}-${legacyDate[1]}-${legacyDate[2]}`
    : "";
}

export function ServicingTab({
  caseDetail,
  liensList,
  liensLoadedAt,
  onRefreshLiens,
  isLiensFetching,
  payments,
  paymentsLoadedAt,
  onRefreshPayments,
  isPaymentsFetching,
  panelMode,
  onPanelModeChange,
  onRefreshCase,
}: {
  caseDetail: CaseDetail;
  liensList: (CaseLienItem & CaseLienItemMetadata)[];
  liensLoadedAt: Date | null;
  onRefreshLiens: () => void;
  isLiensFetching: boolean;
  payments: import("@/lib/settlement/settlement.types").LegacyCasePayment[];
  paymentsLoadedAt: Date | null;
  onRefreshPayments: () => void;
  isPaymentsFetching: boolean;
  panelMode: PanelMode;
  onPanelModeChange: (m: PanelMode) => void;
  onRefreshCase: () => Promise<void>;
}) {
  const addToast = useLienStore((s) => s.addToast);
  const { data } = useCaseLiens(caseDetail.id, {}, "all-liens");
  const liens = data?.items ?? liensList;
  const timezone = useTimezone();
  const [subTab, setSubTab] = useState<ServicingSubTab>("servicing-details");
  const [isAddPaymentOpen, setIsAddPaymentOpen] = useState(false);
  const [isNoRecoveryOpen, setIsNoRecoveryOpen] = useState(false);
  const [setupReductionFormShown, showSetupReductionForm] = useState(false);
  const [historyPage, setHistoryPage] = useState(1);
  const HISTORY_PAGE_SIZE = 10;
  const historyQueryClient = useQueryClient();
  const isHistoryVisible = subTab === "history";
  const historyQuery = useSettlementHistory(
    { caseId: caseDetail.id, page: historyPage, limit: HISTORY_PAGE_SIZE },
    { enabled: isHistoryVisible },
  );
  const historyItems = historyQuery.data?.items ?? [];
  const historyTotalPages = historyQuery.data?.pagination.totalPages ?? 0;
  const historyTotalCount = historyQuery.data?.pagination.totalCount ?? 0;
  const historyLoadedAt = historyQuery.dataUpdatedAt
    ? new Date(historyQuery.dataUpdatedAt)
    : null;
  // Mark history stale on servicing mutations; only force a refetch if the
  // History sub-tab is actually visible, otherwise it'll refetch lazily
  // once the user switches to it (query is disabled while hidden).
  const refetchHistory = () => {
    historyQueryClient.invalidateQueries({
      queryKey: ["settlement-history"],
      refetchType: isHistoryVisible ? "active" : "none",
    });
  };
  // onRefreshLiens only refetches the paged "case-liens" query (used by the
  // Liens tab). This tab reads from the separate "case-liens-all" query (see
  // useCaseLiens call above), so payment/reduction/no-recovery mutations must
  // invalidate that key too or the open/closed lien balances go stale.
  const refreshAllLienData = () => {
    historyQueryClient.invalidateQueries({
      queryKey: ["case-liens-all", caseDetail.id],
    });
    historyQueryClient.invalidateQueries({
      queryKey: CASE_PAYMENTS_QUERY_KEY(caseDetail.id),
    });
    historyQueryClient.invalidateQueries({
      queryKey: SETTLEMENT_PAYMENT_DETAILS_QUERY_KEY(caseDetail.id),
    });
  };

  /* TEMP: visual fallback data for UI review only */
  const initialCaseStatus = caseDetail.status || "PreDemand";
  const [caseStatus, setCaseStatus] = useState(initialCaseStatus);
  const [savedCaseStatus, setSavedCaseStatus] = useState(initialCaseStatus);
  const [switchedLawFirm, setSwitchedLawFirm] = useState(false);
  const [switchedDate, setSwitchedDate] = useState(
    toDateInputValue(caseDetail.switchedDate),
  );
  const [currentLawFirm, setCurrentLawFirm] = useState(
    caseDetail.lawFirmId || "",
  );
  const [currentLawyer, setCurrentLawyer] = useState(
    caseDetail.attorneyId || "",
  );
  const [currentCaseManager, setCurrentCaseManager] = useState(
    caseDetail.caseManagerId || "",
  );
  const [attorneyRoleCode, setAttorneyRoleCode] = useState<
    string | undefined
  >();
  const [caseManagerRoleCode, setCaseManagerRoleCode] = useState<
    string | undefined
  >();
  const [isSavingServicingDetails, setIsSavingServicingDetails] =
    useState(false);

  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<boolean>(false);

  let openLiens = liens.filter((i) => i.closedAtUtc === null);
  let closedLiens = liens.filter((i) => i.closedAtUtc !== null);

  const openLiensTotalBilling = openLiens.reduce(
    (s, l) => s + l.originalAmount,
    0,
  );
  const openLiensTotalPurchase = openLiens.reduce(
    (s, l) => s + (l.purchaseAmount ?? 0),
    0,
  );
  const openLiensTotalBalance = openLiens.reduce((s, l) => s + l.balance, 0);
  const openLiensTotalReduction = openLiens.reduce(
    (s, l) => s + (l.reductionAmount ?? 0),
    0,
  );
  const openLiensTotalPayment = openLiens.reduce(
    (s, l) => s + (l.paymentAmount ?? 0),
    0,
  );
  const closedLiensTotalBilling = closedLiens.reduce(
    (s, l) => s + l.originalAmount,
    0,
  );
  const closedLiensTotalPurchase = closedLiens.reduce(
    (s, l) => s + (l.purchaseAmount ?? 0),
    0,
  );
  const closedLiensTotalReduction = closedLiens.reduce(
    (s, l) => s + (l.reductionAmount ?? 0),
    0,
  );
  const closedLiensTotalBalance = closedLiens.reduce(
    (s, l) => s + l.balance,
    0,
  );
  const closedLiensTotalPayment = closedLiens.reduce(
    (s, l) => s + (l.paymentAmount ?? 0),
    0,
  );

  const { lookup } = useSessionContext();

  const [selectedPayment, setSelectedPayment] = useState<any>();

  const caseStatusList =
    lookup?.CaseStatus.map((s) => {
      return { key: s.id, value: s.code, label: s.name };
    }) ?? [];
  const canSaveServicingDetails =
    switchedLawFirm || caseStatus !== savedCaseStatus;
  // Fetch role codes for attorney and case manager on component mount
  useEffect(() => {
    const fetchRoleCodes = async () => {
      const [attorney, caseManager] = await Promise.all([
        contactsService.getAttorneyRoleCode(),
        contactsService.getCaseManagerRoleCode(),
      ]);
      setAttorneyRoleCode(attorney);
      setCaseManagerRoleCode(caseManager);
    };
    fetchRoleCodes();
  }, []);

  const { mutate: updateCase, isPending: isUpdating } =
    useUpdateServicingDetails();
  const handleSaveServicingDetails = async () => {
    const payload = {
      caseId: caseDetail.id,
      caseStatusId: caseStatus,
      switchedDate: switchedLawFirm
        ? switchedDate || new Date().toISOString().slice(0, 10)
        : undefined,
      lawFirmId: switchedLawFirm ? currentLawFirm : undefined,
      attorney: switchedLawFirm ? currentLawyer : undefined,
      caseManager: switchedLawFirm ? currentCaseManager : undefined,
    };
    await updateCase(payload, {
      onSuccess: () => {
        addToast({
          type: "success",
          title: "Servicing Details Saved",
          description: "Your servicing details were saved.",
        });
        setSwitchedLawFirm(false);
        setSavedCaseStatus(caseStatus);
      },
    });
  };

  const handleEditPayment = (p: any) => {
    const formattedLien = {
      ...p,
      type: p.typeId,
      status: p.statusId,
      checkDate: dateConvertertoIso(p.checkDate),
      isEditing: true,
    };
    setSelectedPayment(formattedLien);
    setIsAddPaymentOpen(true);
  };

  const handleDelete = async () => {
    setDeleting(true);
    if (!deletingId) return;
    try {
      await settlementService.deleteSettlementPayment(deletingId);
      addToast({
        type: "success",
        title: "Payment Deleted",
        description: "The payment record was removed.",
      });
      setDeletingId(null);
      onRefreshPayments();
    } catch {
      addToast({
        type: "error",
        title: "Delete Failed",
        description: "Failed to delete the payment.",
      });
    } finally {
      setDeleting(false);
    }
  };

  useEffect(() => {
    // getCase();
  }, []);

  const historyColumns: ColumnDef<SettlementHistoryItemV3, any>[] = [
    {
      id: "timestamp",
      header: "Timestamp",
      cell: ({ row }) => (
        <span className="text-xs text-gray-500 whitespace-nowrap">
          {formatNoteTimestamp(row.original.createdAt, timezone)}
        </span>
      ),
    },
    {
      id: "description",
      header: "Description",
      cell: ({ row }) => (
        <span className="text-sm text-gray-600">
          {describeSettlementHistoryItem(row.original)}
        </span>
      ),
    },
    {
      id: "updatedBy",
      header: "Updated By",
      cell: ({ row }) => (
        <span className="text-sm text-gray-500 whitespace-nowrap">
          {row.original.updatedBy}
        </span>
      ),
    },
  ];

  const leftContent = (
    <div className="space-y-4">
      <div className="bg-white border border-gray-200 rounded-lg overflow-hidden">
        <div className="flex border-b border-gray-100">
          {SERVICING_SUB_TABS.map((st) => (
            <button
              key={st.key}
              onClick={() => setSubTab(st.key)}
              className={[
                "flex-1 px-4 py-2.5 text-xs font-medium transition-colors flex items-center justify-center gap-1.5",
                subTab === st.key
                  ? "text-primary border-b-2 border-primary bg-primary/5"
                  : "text-gray-500 hover:text-gray-700 hover:bg-gray-50",
              ].join(" ")}
            >
              <i className={`${st.icon} text-sm`} />
              {st.label}
            </button>
          ))}
        </div>
      </div>

      {subTab === "servicing-details" && (
        <ServicingDetailsSection
          caseStatus={caseStatus}
          onCaseStatusChange={setCaseStatus}
          caseStatusList={caseStatusList}
          switchedLawFirm={switchedLawFirm}
          onSwitchedLawFirmChange={setSwitchedLawFirm}
          switchedDate={switchedDate}
          onSwitchedDateChange={setSwitchedDate}
          currentLawFirm={currentLawFirm}
          onCurrentLawFirmChange={setCurrentLawFirm}
          currentLawyer={currentLawyer}
          onCurrentLawyerChange={setCurrentLawyer}
          currentCaseManager={currentCaseManager}
          onCurrentCaseManagerChange={setCurrentCaseManager}
          attorneyRoleCode={attorneyRoleCode}
          caseManagerRoleCode={caseManagerRoleCode}
          canSave={canSaveServicingDetails && !isSavingServicingDetails}
          onSave={handleSaveServicingDetails}
          isSaving={isUpdating}
        />
      )}

      {subTab === "settlement-details" && (
        <div className="space-y-4">
          <OpenLiensSection
            openLiens={openLiens}
            liensLoadedAt={liensLoadedAt}
            onRefreshLiens={onRefreshLiens}
            isLiensFetching={isLiensFetching}
            openLiensTotalBilling={openLiensTotalBilling}
            openLiensTotalPurchase={openLiensTotalPurchase}
            openLiensTotalReduction={openLiensTotalReduction}
            openLiensTotalBalance={openLiensTotalBalance}
            openLiensTotalPayment={openLiensTotalPayment}
            onSetupReduction={() => showSetupReductionForm(true)}
            onNoRecovery={() => setIsNoRecoveryOpen(true)}
            onAddPayment={() => setIsAddPaymentOpen(true)}
          />

          <ClosedLiensSection
            closedLiens={closedLiens}
            liensLoadedAt={liensLoadedAt}
            onRefreshLiens={onRefreshLiens}
            isLiensFetching={isLiensFetching}
            closedLiensTotalBilling={closedLiensTotalBilling}
            closedLiensTotalPurchase={closedLiensTotalPurchase}
            closedLiensTotalReduction={closedLiensTotalReduction}
            closedLiensTotalBalance={closedLiensTotalBalance}
            closedLiensTotalPayment={closedLiensTotalPayment}
          />

          <PaymentHistoryWidget
            payments={payments}
            liens={liens}
            paymentsLoadedAt={paymentsLoadedAt}
            onRefreshPayments={() => {
              onRefreshPayments();
              refetchHistory();
            }}
            onEditPayment={handleEditPayment}
            onDeletePayment={setDeletingId}
            isPaymentsFetching={isPaymentsFetching}
          />

          <ConfirmDialog
            open={deletingId !== null}
            onClose={() => setDeletingId(null)}
            onConfirm={handleDelete}
            loading={deleting}
            title="Delete Payment Record"
            description="Are you sure you want to delete this payment record? This action cannot be undone and will permanently remove the payment record from the system."
            confirmLabel="Delete"
            confirmVariant="danger"
          />
        </div>
      )}

      {subTab === "history" && (
        <ServicingHistorySection
          isLoading={historyQuery.isLoading}
          isFetching={historyQuery.isFetching}
          historyItems={historyItems}
          historyColumns={historyColumns}
          historyLoadedAt={historyLoadedAt}
          onRefresh={() => historyQuery.refetch()}
          historyPage={historyPage}
          historyTotalPages={historyTotalPages}
          historyTotalCount={historyTotalCount}
          onPageChange={setHistoryPage}
        />
      )}
    </div>
  );

  const rightContent = (
    <FeedsSection
      caseId={caseDetail.id}
      panelMode={panelMode}
      onPanelModeChange={onPanelModeChange}
    />
  );

  return (
    <>
      <LayoutSplit
        left={leftContent}
        right={rightContent}
        mode={panelMode}
        onModeChange={onPanelModeChange}
        showControls={false}
      />
      <SetupReductionForm
        open={setupReductionFormShown}
        onClose={() => showSetupReductionForm(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          showSetupReductionForm(false);
          onRefreshLiens();
          refreshAllLienData();
          onRefreshPayments();
          refetchHistory();
        }}
      />
      <NoRecoveryForm
        open={isNoRecoveryOpen}
        onClose={() => setIsNoRecoveryOpen(false)}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          setIsNoRecoveryOpen(false);
          onRefreshLiens();
          refreshAllLienData();
          onRefreshPayments();
          refetchHistory();
        }}
      />
      <AddPaymentForm
        selectedPayment={selectedPayment}
        open={isAddPaymentOpen}
        isEditing={selectedPayment != null}
        onClose={() => {
          setSelectedPayment(undefined);
          setIsAddPaymentOpen(false);
        }}
        caseId={caseDetail.id}
        liens={liens}
        liensLoadedAt={liensLoadedAt}
        onRefreshLiens={onRefreshLiens}
        isLiensFetching={isLiensFetching}
        onSaved={() => {
          setSelectedPayment(undefined);
          setIsAddPaymentOpen(false);
          onRefreshPayments();
          refreshAllLienData();
          refetchHistory();
        }}
      />
    </>
  );
}
