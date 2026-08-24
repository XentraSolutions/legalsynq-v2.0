import { useState, type ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { CaseSummaryRow } from '@/features/cases/components/CaseSummaryRow';
import type { CaseDetailResponse, CaseUpdate } from '@/shared/api/endpoints/Cases';
import type {
  LienReduction,
  LienSettlement,
  SettlementPaymentDetail,
} from '@/shared/api/endpoints/Settlement';
import type { BadgeVariant } from '@/shared/components/Badge/Badge';
import { Spinner } from '@/shared/components/Spinner';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

type ServicingTabId = 'details' | 'settlement' | 'history';

const SERVICING_TABS = [
  { id: 'details', label: 'Details' },
  { id: 'settlement', label: 'Settlement' },
  { id: 'history', label: 'History' },
] as const;

const STATUS_LABELS: Record<string, string> = {
  PreDemand: 'Pre-demand',
  DemandSent: 'Demand Sent',
  InNegotiation: 'Negotiations',
  CaseSettled: 'Case Settled',
  Closed: 'Closed',
};

function displayStatus(status: string): string {
  return STATUS_LABELS[status] ?? status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function statusVariant(status: string): BadgeVariant {
  if (status === 'Closed') return 'error';
  if (status === 'DemandSent' || status === 'InNegotiation') return 'warning';
  return 'success';
}

function displayDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy');
  } catch {
    return value;
  }
}

function ServicingSegmentedControl({
  activeTab,
  onChange,
}: {
  activeTab: ServicingTabId;
  onChange: (tab: ServicingTabId) => void;
}) {
  return (
    <View
      accessibilityRole="tablist"
      className="flex-row rounded-[24px] bg-[#ebebec] px-2 py-1 dark:bg-[#303138]"
    >
      {SERVICING_TABS.map((tab) => {
        const active = activeTab === tab.id;
        return (
          <Pressable
            key={tab.id}
            accessibilityRole="tab"
            accessibilityState={{ selected: active }}
            className={cx(
              'min-h-8 flex-1 items-center justify-center rounded-[20px] px-3 py-1.5',
              active ? 'bg-white dark:bg-[#191a1f]' : ''
            )}
            style={active ? SHADOWS.sm : undefined}
            onPress={() => onChange(tab.id)}
          >
            <Text
              className={cx(
                FIGMA_TEXT.body,
                active ? 'text-[#202228] dark:text-white' : 'text-[#777a84] dark:text-[#a1a1aa]'
              )}
            >
              {tab.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

function ServicingDetailsCard({
  caseItem,
  onEdit,
}: {
  caseItem: CaseDetailResponse;
  onEdit: () => void;
}) {
  return (
    <View className="rounded-[20px] bg-white px-6 pb-3 pt-6 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <View className="flex-row items-center justify-between gap-4 pb-2">
        <Text className="font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
          Servicing Details
        </Text>
        <Pressable
          accessibilityLabel="Edit servicing details"
          accessibilityRole="button"
          className="h-8 flex-row items-center gap-1 rounded-2xl border border-[#dedee0] px-3 dark:border-[#303138]"
          onPress={onEdit}
        >
          <Ionicons color="#202228" name="pencil-outline" size={14} />
          <Text className="font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
            Edit
          </Text>
        </Pressable>
      </View>

      <CaseSummaryRow
        badgeVariant={statusVariant(caseItem.status)}
        label="Case Status"
        value={displayStatus(caseItem.status)}
      />
      <CaseSummaryRow label="Switched Date" value={null} />
      <CaseSummaryRow label="Current Law Firm" value={caseItem.lawFirm} />
      <CaseSummaryRow label="Current Lawyer" value={null} />
      <CaseSummaryRow
        label="Current Case Manager"
        showDivider={false}
        value={caseItem.caseManager}
      />
    </View>
  );
}

function SettlementSectionCard({ children, title }: { children: ReactNode; title: string }) {
  const [expanded, setExpanded] = useState(true);

  return (
    <View className="overflow-hidden rounded-[20px] bg-white dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <Pressable
        accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title}`}
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        className="flex-row items-center gap-2 px-6 pb-4 pt-6"
        onPress={() => setExpanded((value) => !value)}
      >
        <Ionicons color="#71717a" name={expanded ? 'chevron-down' : 'chevron-forward'} size={18} />
        <Text className="flex-1 font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
          {title}
        </Text>
      </Pressable>
      {expanded ? children : null}
    </View>
  );
}

function EmptySettlementState({
  actionLabel,
  description,
  icon,
  onAction,
  title,
}: {
  actionLabel?: string;
  description: string;
  icon: keyof typeof Ionicons.glyphMap;
  onAction?: () => void;
  title: string;
}) {
  return (
    <View className="items-center px-6 py-10">
      <View className="h-12 w-12 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#303138]">
        <Ionicons color="#202228" name={icon} size={20} />
      </View>
      <Text className="mt-5 text-center font-jakarta-medium text-[16px] leading-6 text-[#202228] dark:text-white">
        {title}
      </Text>
      <Text className="mt-2 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
        {description}
      </Text>
      {actionLabel && onAction ? (
        <Pressable
          accessibilityRole="button"
          className="mt-5 h-11 w-full items-center justify-center rounded-full bg-[#f97332]"
          onPress={onAction}
        >
          <Text className="font-jakarta-medium text-[14px] leading-5 text-white">
            {actionLabel}
          </Text>
        </Pressable>
      ) : null}
    </View>
  );
}

function SettlementDetailsCard({
  isError,
  isLoading,
  onSetupReduction,
  reductions,
}: {
  isError: boolean;
  isLoading: boolean;
  onSetupReduction: () => void;
  reductions: LienReduction[];
}) {
  return (
    <SettlementSectionCard title="Settlement Details">
      {isLoading ? (
        <View className="items-center py-8">
          <Spinner />
        </View>
      ) : isError ? (
        <Text className="py-8 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
          Reductions could not be loaded.
        </Text>
      ) : reductions.length > 0 ? (
        <View className="px-6 pb-4">
          {reductions.map((reduction, index) => (
            <View
              key={reduction.id}
              className={cx(
                'py-4',
                index < reductions.length - 1
                  ? 'border-b border-[#dedfe2] dark:border-[#33343a]'
                  : ''
              )}
            >
              <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
                Lien ID: {reduction.lienId}
              </Text>
              <CaseSummaryRow label="Reduction Amount" value={formatCurrency(reduction.amount)} />
              <CaseSummaryRow
                label="Reduction Date"
                showDivider={false}
                value={displayDate(reduction.reductionDate)}
              />
            </View>
          ))}
        </View>
      ) : (
        <EmptySettlementState
          actionLabel="Setup Reduction"
          description="No reductions have been added yet. Any reductions will appear here"
          icon="document-text-outline"
          title="No Reduction"
          onAction={onSetupReduction}
        />
      )}
    </SettlementSectionCard>
  );
}

function SettlementList({ settlements }: { settlements: LienSettlement[] }) {
  return (
    <View className="px-6 pb-4">
      {settlements.map((settlement, index) => (
        <View
          key={settlement.id}
          className={cx(
            'py-4',
            index < settlements.length - 1 ? 'border-b border-[#dedfe2] dark:border-[#33343a]' : ''
          )}
        >
          <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
            PAY-{settlement.paymentNumber}
          </Text>
          <Text className="mt-1 font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
            Lien ID: {settlement.lienId}
          </Text>
          <View className="mt-2">
            <CaseSummaryRow label="Amount" value={formatCurrency(settlement.amount)} />
            <CaseSummaryRow label="Status" showDivider={false} value={settlement.status} />
          </View>
        </View>
      ))}
    </View>
  );
}

function PaymentsCard({
  isError,
  isLoading,
  onAddPayment,
  onNoRecovery,
  settlements,
}: {
  isError: boolean;
  isLoading: boolean;
  onAddPayment: () => void;
  onNoRecovery: () => void;
  settlements: LienSettlement[];
}) {
  const openLiens = settlements.filter(
    (settlement) => !/(closed|resolved|settled)/i.test(settlement.status)
  );
  const closedLiens = settlements.filter((settlement) =>
    /(closed|resolved|settled)/i.test(settlement.status)
  );

  return (
    <SettlementSectionCard title="Payments">
      <View className="flex-row gap-2 px-6 pb-5 pt-2">
        <Pressable
          accessibilityRole="button"
          className="h-11 flex-1 items-center justify-center rounded-full border border-[#dedee0] dark:border-[#3a3b42]"
          onPress={onNoRecovery}
        >
          <Text className="font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
            No Recovery
          </Text>
        </Pressable>
        <Pressable
          accessibilityRole="button"
          className="h-11 flex-1 items-center justify-center rounded-full bg-[#f97332]"
          onPress={onAddPayment}
        >
          <Text className="font-jakarta-medium text-[14px] leading-5 text-white">Add Payment</Text>
        </Pressable>
      </View>

      {isLoading ? (
        <View className="items-center py-8">
          <Spinner />
        </View>
      ) : isError ? (
        <Text className="py-8 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
          Payments could not be loaded.
        </Text>
      ) : (
        <>
          <Text className="bg-[#f1f1f2] px-6 py-3 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:bg-[#25262c] dark:text-white">
            Open Liens
          </Text>
          {openLiens.length > 0 ? (
            <SettlementList settlements={openLiens} />
          ) : (
            <EmptySettlementState
              description="Liens will appear here once they are opened and become active."
              icon="document-text-outline"
              title="No Open Liens Yet"
            />
          )}

          <Text className="bg-[#f1f1f2] px-6 py-3 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:bg-[#25262c] dark:text-white">
            Closed Liens
          </Text>
          {closedLiens.length > 0 ? (
            <SettlementList settlements={closedLiens} />
          ) : (
            <EmptySettlementState
              description="Liens will appear here once they are fully settled and closed."
              icon="document-text-outline"
              title="No Closed Liens Yet"
            />
          )}
        </>
      )}
    </SettlementSectionCard>
  );
}

function PaymentHistoryCard({
  isError,
  isLoading,
  payments,
}: {
  isError: boolean;
  isLoading: boolean;
  payments: SettlementPaymentDetail[];
}) {
  return (
    <SettlementSectionCard title="Payment History">
      {isLoading ? (
        <View className="items-center py-8">
          <Spinner />
        </View>
      ) : isError ? (
        <Text className="py-8 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
          Payment history could not be loaded.
        </Text>
      ) : payments.length > 0 ? (
        <View className="px-6 pb-4">
          {payments.map((payment, index) => (
            <View
              key={payment.id}
              className={cx(
                'py-4',
                index < payments.length - 1 ? 'border-b border-[#dedfe2] dark:border-[#33343a]' : ''
              )}
            >
              <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
                PAY-{payment.paymentNumber}
              </Text>
              <Text className="mt-1 font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
                Lien ID: {payment.lienId}
              </Text>
              <View className="mt-2">
                <CaseSummaryRow label="Amount" value={formatCurrency(payment.amount)} />
                <CaseSummaryRow label="Payee" value={payment.payee} />
                <CaseSummaryRow label="Check Number" value={payment.checkNumber} />
                <CaseSummaryRow
                  label="Payment Date"
                  showDivider={false}
                  value={displayDate(payment.paymentDate)}
                />
              </View>
            </View>
          ))}
        </View>
      ) : (
        <EmptySettlementState
          description="Payment history will appear here once payments have been recorded."
          icon="time-outline"
          title="No Payment History Yet"
        />
      )}
    </SettlementSectionCard>
  );
}

function ServicingHistoryCard({ updates }: { updates: CaseUpdate[] }) {
  return (
    <View className="rounded-[20px] bg-white px-6 pb-3 pt-6 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <Text className="pb-2 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
        Servicing History
      </Text>
      {updates.length > 0 ? (
        updates.map((update, index) => {
          const timestamp =
            update.updatedAtUtc ??
            update.updatedAt ??
            update.createdAtUtc ??
            update.createdAt ??
            '';
          return (
            <View
              key={update.id ?? `${timestamp}-${index}`}
              className={cx(
                'py-3',
                index < updates.length - 1 ? 'border-b border-[#dedfe2] dark:border-[#33343a]' : ''
              )}
            >
              <View className="flex-row items-start justify-between gap-3">
                <Text className="flex-1 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
                  {update.title ?? update.action ?? 'Case Update'}
                </Text>
                <Text className="font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
                  {displayDate(timestamp)}
                </Text>
              </View>
              <Text className="mt-1 font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
                {update.description ?? update.message ?? update.note ?? 'Case record updated.'}
              </Text>
            </View>
          );
        })
      ) : (
        <Text className="py-8 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
          No servicing history yet.
        </Text>
      )}
    </View>
  );
}

interface CaseServicingTabProps {
  caseItem: CaseDetailResponse;
  updates: CaseUpdate[];
  settlements: LienSettlement[];
  payments: SettlementPaymentDetail[];
  reductions: LienReduction[];
  settlementLoading: boolean;
  settlementError: boolean;
  onAddPayment: () => void;
  onEdit: () => void;
  onNoRecovery: () => void;
  onSetupReduction: () => void;
}

export function CaseServicingTab({
  caseItem,
  updates,
  settlements,
  payments,
  reductions,
  settlementLoading,
  settlementError,
  onAddPayment,
  onEdit,
  onNoRecovery,
  onSetupReduction,
}: CaseServicingTabProps) {
  const [activeTab, setActiveTab] = useState<ServicingTabId>('details');

  return (
    <CaseDetailTabPage testID="case-servicing-page">
      <ServicingSegmentedControl activeTab={activeTab} onChange={setActiveTab} />
      <View className="mt-6">
        {activeTab === 'details' ? (
          <ServicingDetailsCard caseItem={caseItem} onEdit={onEdit} />
        ) : null}
        {activeTab === 'settlement' ? (
          <View className="gap-3">
            <SettlementDetailsCard
              isError={settlementError}
              isLoading={settlementLoading}
              reductions={reductions}
              onSetupReduction={onSetupReduction}
            />
            <PaymentsCard
              isError={settlementError}
              isLoading={settlementLoading}
              onAddPayment={onAddPayment}
              onNoRecovery={onNoRecovery}
              settlements={settlements}
            />
            <PaymentHistoryCard
              isError={settlementError}
              isLoading={settlementLoading}
              payments={payments}
            />
          </View>
        ) : null}
        {activeTab === 'history' ? <ServicingHistoryCard updates={updates} /> : null}
      </View>
    </CaseDetailTabPage>
  );
}
