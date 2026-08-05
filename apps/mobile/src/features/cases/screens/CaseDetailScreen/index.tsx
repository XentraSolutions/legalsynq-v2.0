import type { ReactNode } from 'react';
import { useState } from 'react';
import { Alert, Modal as ReactNativeModal, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CaseDetailHeader } from '@/features/cases/components/CaseDetailHeader';
import { CaseDocumentsTab } from '@/features/cases/components/CaseDocumentsTab';
import { CaseLiensTab } from '@/features/cases/components/CaseLiensTab';
import { CaseNotesTab } from '@/features/cases/components/CaseNotesTab';
import { CaseDetailPlaceholderPage } from '@/features/cases/components/CaseDetailPlaceholderPage';
import { CaseServicingTab } from '@/features/cases/components/CaseServicingTab';
import { CaseDetailTabBar } from '@/features/cases/components/CaseDetailTabBar';
import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { CaseSummaryRow } from '@/features/cases/components/CaseSummaryRow';
import {
  useCaseDetail,
  useCaseLienUpdates,
  useCaseSettlementDetails,
  useCases,
  useCaseUpdates,
  useDeleteCase,
  useMergeCase,
} from '@/features/cases/hooks';
import { parseCaseTrackingMetadata } from '@/features/cases/utils/caseTrackingMetadata';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { CaseDetailResponse, CaseUpdate } from '@/shared/api/endpoints/Cases';
import type { BadgeVariant } from '@/shared/components/Badge/Badge';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { SelectOptionModal } from '@/shared/components/SelectOptionModal';
import type { SelectOptionItem } from '@/shared/components/SelectOptionModal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, SHADOWS } from '@/shared/styles';
import { formatDisplayDate } from '@/shared/utils';

type DetailRoute = NativeStackScreenProps<MainStackParamList, 'CaseDetail'>['route'];
type CaseDetailTabId =
  | 'summary'
  | 'details'
  | 'liens'
  | 'documents'
  | 'servicing'
  | 'notes'
  | 'tasks';

const CASE_DETAIL_TABS = [
  { id: 'summary', label: 'Summary' },
  { id: 'details', label: 'Details' },
  { id: 'liens', label: 'Liens' },
  { id: 'documents', label: 'Documents' },
  { id: 'servicing', label: 'Servicing' },
  { id: 'notes', label: 'Notes' },
  { id: 'tasks', label: 'Task Manager' },
] as const;

const STATUS_LABELS: Record<string, string> = {
  PreDemand: 'Pre-demand',
  DemandSent: 'Demand Sent',
  InNegotiation: 'Negotiations',
  CaseSettled: 'Case Settled',
  Closed: 'Closed',
};

function displayDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy');
  } catch {
    return value;
  }
}

function displayStatus(status: string): string {
  return STATUS_LABELS[status] ?? status.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function statusVariant(status: string): BadgeVariant {
  if (status === 'Closed') return 'error';
  if (status === 'DemandSent' || status === 'InNegotiation') return 'warning';
  return 'success';
}

function SectionCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <View className="rounded-[20px] bg-white px-6 pb-3 pt-6 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <Text className="font-jakarta-semibold text-[16px] leading-[22px] text-[#202228] dark:text-white">
        {title}
      </Text>
      <View className="mt-5">{children}</View>
    </View>
  );
}

function DetailSectionCard({
  title,
  onEdit,
  children,
}: {
  title: string;
  onEdit?: () => void;
  children: ReactNode;
}) {
  const [expanded, setExpanded] = useState(true);

  return (
    <View className="rounded-[20px] bg-white px-6 pb-3 pt-6 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <View className="flex-row items-center justify-between gap-4">
        <Pressable
          accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title}`}
          accessibilityRole="button"
          className="min-h-8 flex-1 flex-row items-center gap-2"
          onPress={() => setExpanded((value) => !value)}
        >
          <Ionicons
            color="#6f737d"
            name={expanded ? 'chevron-down' : 'chevron-forward'}
            size={16}
          />
          <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#202228] dark:text-white">
            {title}
          </Text>
        </Pressable>
        {onEdit ? (
          <Pressable
            accessibilityLabel={`Edit ${title}`}
            accessibilityRole="button"
            className="h-8 flex-row items-center gap-1 rounded-2xl border border-[#dedee0] px-3 dark:border-[#303138]"
            onPress={onEdit}
          >
            <Ionicons color="#202228" name="pencil-outline" size={14} />
            <Text className="font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
              Edit
            </Text>
          </Pressable>
        ) : null}
      </View>
      {expanded ? <View className="mt-4">{children}</View> : null}
    </View>
  );
}

function TrackingFlag({ label, checked }: { label: string; checked: boolean }) {
  return (
    <View className="flex-row items-start gap-3 py-2">
      <View
        className={cx(
          'mt-0.5 h-4 w-4 items-center justify-center rounded-md',
          checked ? 'bg-[#f97332]' : 'bg-[#ebebec] dark:bg-[#303138]'
        )}
      >
        {checked ? <Ionicons color="#ffffff" name="checkmark" size={12} /> : null}
      </View>
      <Text className="flex-1 font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
        {label}
      </Text>
    </View>
  );
}

function SummaryTab({ caseItem }: { caseItem: CaseDetailResponse }) {
  const status = displayStatus(caseItem.status);

  return (
    <CaseDetailTabPage testID="case-summary-page">
      <SectionCard title="Case Summary">
        <CaseSummaryRow label="Plaintiff Name" value={caseItem.clientDisplayName} />
        <CaseSummaryRow label="Case ID" value={caseItem.caseNumber} />
        <CaseSummaryRow label="Accident Type" value={caseItem.accidentType} />
        <CaseSummaryRow
          badgeVariant={statusVariant(caseItem.status)}
          label="Case Status"
          value={status}
        />
        <CaseSummaryRow label="Date of Loss" value={displayDate(caseItem.dateOfIncident)} />
        <CaseSummaryRow label="Date of Birth" value={displayDate(caseItem.clientDob)} />
        <CaseSummaryRow label="State of Incident" value={caseItem.stateOfIncident} />
        <CaseSummaryRow label="Law Firm" value={caseItem.lawFirm} />
        <CaseSummaryRow
          label="Case Manager"
          showDivider={false}
          value={caseItem.caseManager}
        />
      </SectionCard>
    </CaseDetailTabPage>
  );
}

function updateTimestamp(update: CaseUpdate): string {
  const value =
    update.updatedAtUtc ?? update.updatedAt ?? update.createdAtUtc ?? update.createdAt ?? '';
  return value ? displayDate(value) : '';
}

function DetailsTab({
  caseItem,
  updates,
  updatesLoading,
  onEditDetails,
  onEditPersonal,
}: {
  caseItem: CaseDetailResponse;
  updates: CaseUpdate[];
  updatesLoading: boolean;
  onEditDetails: () => void;
  onEditPersonal: () => void;
}) {
  const [showAllUpdates, setShowAllUpdates] = useState(false);
  const visibleUpdates = showAllUpdates ? updates : updates.slice(0, 3);
  const tracking = parseCaseTrackingMetadata(caseItem.notes);

  return (
    <CaseDetailTabPage testID="case-details-page">
      <DetailSectionCard title="Case Tracking Details" onEdit={onEditDetails}>
        <CaseSummaryRow label="Tracking Policy ID" value={caseItem.externalReference} />
        <CaseSummaryRow label="Document Type" value={tracking.documentType} />
        <CaseSummaryRow
          badgeVariant={statusVariant(caseItem.status)}
          label="Current Status"
          value={displayStatus(caseItem.status)}
        />
        <CaseSummaryRow
          badgeVariant="success"
          label="Current Medical Status"
          value={tracking.currentMedicalStatus}
        />
        <CaseSummaryRow label="Case Type" value={caseItem.accidentType || tracking.accidentType} />
        <CaseSummaryRow
          label="State of Incident"
          value={caseItem.stateOfIncident || tracking.stateOfIncident}
        />
        <CaseSummaryRow label="Lead" value={tracking.lead} />
        <CaseSummaryRow label="Case Tracking Note" value={caseItem.description} />
        <View className="pt-2">
          <TrackingFlag
            checked={tracking.shareCase}
            label="Share this Case with Associated Law Firm"
          />
          <TrackingFlag checked={tracking.caseDropped} label="Case Dropped" />
          <TrackingFlag checked={tracking.isUccFiled} label="UCC Filed" />
          <TrackingFlag checked={tracking.childSupportLiens} label="Child Support" />
          <TrackingFlag checked={tracking.minorComp} label="Minor Comp" />
        </View>
      </DetailSectionCard>

      <View className="mt-4">
        <DetailSectionCard title="Plaintiff Info" onEdit={onEditPersonal}>
          <CaseSummaryRow label="Full Name" value={caseItem.clientDisplayName} />
          <CaseSummaryRow label="Phone Number" value={caseItem.clientPhone} />
          <CaseSummaryRow label="Email" value={caseItem.clientEmail} />
          <CaseSummaryRow label="Birthdate" value={displayDate(caseItem.clientDob)} />
          <CaseSummaryRow label="Address" showDivider={false} value={caseItem.clientAddress} />
        </DetailSectionCard>
      </View>

      <View className="mt-4">
        <DetailSectionCard title="Recent Updates">
          {updatesLoading ? (
            <Spinner />
          ) : updates.length > 0 ? (
            <>
              {visibleUpdates.map((update, index) => (
                <View
                  key={update.id ?? `${updateTimestamp(update)}-${index}`}
                  className={cx(
                    'py-4',
                    index < visibleUpdates.length - 1
                      ? 'border-b border-[#e4e4e7] dark:border-[#303138]'
                      : ''
                  )}
                >
                  <View className="flex-row items-start justify-between gap-3">
                    <Text className="flex-1 font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
                      {update.title ?? update.action ?? 'Case Update'}
                    </Text>
                    <Text className="font-jakarta text-[12px] leading-4 text-[#777a84]">
                      {updateTimestamp(update)}
                    </Text>
                  </View>
                  <Text className="mt-2 font-jakarta text-[12px] leading-4 text-[#777a84] dark:text-[#a1a1aa]">
                    {update.description ?? update.message ?? update.note ?? 'Case record updated.'}
                  </Text>
                </View>
              ))}
              {updates.length > 3 ? (
                <Button
                  className="mt-2"
                  label={showAllUpdates ? 'Show Recent Updates' : 'View All Updates'}
                  size="sm"
                  variant="secondary"
                  onPress={() => setShowAllUpdates((value) => !value)}
                />
              ) : null}
            </>
          ) : (
            <Text className="py-5 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
              No recent updates.
            </Text>
          )}
        </DetailSectionCard>
      </View>
    </CaseDetailTabPage>
  );
}

function ManageCaseModal({
  visible,
  onClose,
  onDeleteCase,
  onMergeCase,
  onPayoffQuote,
}: {
  visible: boolean;
  onClose: () => void;
  onDeleteCase: () => void;
  onMergeCase: () => void;
  onPayoffQuote: () => void;
}) {
  const actions = [
    {
      icon: 'document-text-outline' as const,
      label: 'Payoff Quote',
      onPress: onPayoffQuote,
    },
    {
      icon: 'copy-outline' as const,
      label: 'Merge Case',
      onPress: onMergeCase,
    },
    {
      danger: true,
      icon: 'trash-outline' as const,
      label: 'Delete Case',
      onPress: onDeleteCase,
    },
  ];

  return (
    <ReactNativeModal animationType="fade" transparent visible={visible} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/30 p-4">
        <Pressable className="absolute inset-0" onPress={onClose} />
        <View
          className="rounded-[24px] bg-white p-6 dark:bg-[#191a1f]"
          style={SHADOWS.lg}
        >
          <View className="flex-row items-start justify-between">
            <View className="flex-1 pr-8">
              <Text className="font-jakarta-medium text-[16px] leading-6 text-[#202228] dark:text-white">
                Manage Case
              </Text>
              <Text className="mt-2 font-jakarta text-[14px] leading-5 text-[#71717a]">
                Select an action to manage this case and its associated information.
              </Text>
            </View>
            <Pressable
              accessibilityLabel="Close manage case"
              accessibilityRole="button"
              className="h-6 w-6 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#303138]"
              onPress={onClose}
            >
              <Ionicons color="#71717a" name="close" size={16} />
            </Pressable>
          </View>

          <View className="mt-5">
            {actions.map((action, index) => (
              <Pressable
                key={action.label}
                accessibilityRole="button"
                className={cx(
                  'flex-row items-center py-3',
                  index < actions.length - 1
                    ? 'border-b border-[#e4e4e7] dark:border-[#303138]'
                    : ''
                )}
                onPress={action.onPress}
              >
                <Ionicons
                  color={action.danger ? '#ff383c' : '#202228'}
                  name={action.icon}
                  size={17}
                />
                <Text
                  className={cx(
                    'ml-2 flex-1 font-jakarta text-[14px] leading-5',
                    action.danger ? 'text-[#ff383c]' : 'text-[#202228] dark:text-white'
                  )}
                >
                  {action.label}
                </Text>
                <Ionicons color="#71717a" name="chevron-forward" size={20} />
              </Pressable>
            ))}
          </View>

          <Button className="mt-5" label="Cancel" variant="secondary" onPress={onClose} />
        </View>
      </View>
    </ReactNativeModal>
  );
}

function MergeCaseModal({
  currentCaseId,
  onClose,
  onSelect,
}: {
  currentCaseId: string;
  onClose: () => void;
  onSelect: (option: SelectOptionItem) => void;
}) {
  const casesQuery = useCases();
  const options = casesQuery.cases
    .filter((caseItem) => caseItem.id !== currentCaseId)
    .map((caseItem) => ({
      label: `${caseItem.clientName} (${caseItem.caseNumber})`,
      value: caseItem.id,
    }));

  return (
    <SelectOptionModal
      emptyMessage={
        casesQuery.isLoading ? 'Loading cases…' : 'No other cases are available to merge.'
      }
      options={options}
      searchThreshold={0}
      title="Select Case to Merge"
      visible
      onClose={onClose}
      onSelect={onSelect}
    />
  );
}

export function CaseDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<DetailRoute>();
  const [activeTab, setActiveTab] = useState<CaseDetailTabId>('summary');
  const caseQuery = useCaseDetail(route.params.caseId);
  const updatesQuery = useCaseUpdates(route.params.caseId);
  const lienUpdatesQuery = useCaseLienUpdates(route.params.caseId);
  const settlementQuery = useCaseSettlementDetails(route.params.caseId, activeTab === 'servicing');
  const mergeCase = useMergeCase(route.params.caseId);
  const deleteCase = useDeleteCase(route.params.caseId);
  const toast = useToast();
  const [manageVisible, setManageVisible] = useState(false);
  const [mergeVisible, setMergeVisible] = useState(false);

  function confirmMerge(option: SelectOptionItem) {
    Alert.alert(
      'Merge Cases',
      `Merge ${option.label} into this case? The selected case will be removed after its information is transferred.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Merge',
          style: 'destructive',
          onPress: () => {
            void mergeCase
              .mutateAsync(option.value)
              .then(() => {
                setMergeVisible(false);
                toast.showSuccess('Cases merged successfully');
              })
              .catch((error: unknown) => {
                toast.showError(error instanceof Error ? error.message : 'Unable to merge cases');
              });
          },
        },
      ]
    );
  }

  function confirmDelete() {
    setManageVisible(false);
    Alert.alert(
      'Delete Case',
      'Delete this case permanently? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: () => {
            void deleteCase
              .mutateAsync()
              .then(() => {
                toast.showSuccess('Case deleted successfully');
                navigation.goBack();
              })
              .catch((error: unknown) => {
                toast.showError(error instanceof Error ? error.message : 'Unable to delete case');
              });
          },
        },
      ]
    );
  }

  if (caseQuery.isLoading) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <CaseDetailHeader
          subtitle="Loading case..."
          title="Case Details"
          onBack={() => navigation.goBack()}
          onMore={() => undefined}
        />
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      </View>
    );
  }

  if (caseQuery.isError || !caseQuery.data) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <CaseDetailHeader
          subtitle="Case unavailable"
          title="Case Details"
          onBack={() => navigation.goBack()}
          onMore={() => undefined}
        />
        <EmptyState
          actionLabel="Try Again"
          description={
            caseQuery.error instanceof Error ? caseQuery.error.message : 'The case was not found.'
          }
          icon={<Ionicons color="#f97332" name="alert-circle-outline" size={58} />}
          title="Unable to load case"
          onAction={() => void caseQuery.refetch()}
        />
      </View>
    );
  }

  const caseItem = caseQuery.data;

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <CaseDetailHeader
        subtitle={`Case ID: ${caseItem.caseNumber}`}
        title={caseItem.clientDisplayName}
        onBack={() => navigation.goBack()}
        onMore={() => setManageVisible(true)}
      />
      <CaseDetailTabBar
        activeTab={activeTab}
        tabs={CASE_DETAIL_TABS}
        onChange={setActiveTab}
      />

      {activeTab === 'summary' ? <SummaryTab caseItem={caseItem} /> : null}
      {activeTab === 'details' ? (
        <DetailsTab
          caseItem={caseItem}
          updates={updatesQuery.data ?? []}
          updatesLoading={updatesQuery.isLoading}
          onEditDetails={() =>
            navigation.navigate('EditCaseDetails', { caseId: route.params.caseId })
          }
          onEditPersonal={() =>
            navigation.navigate('EditCasePersonal', { caseId: route.params.caseId })
          }
        />
      ) : null}
      {activeTab === 'liens' ? (
        <CaseLiensTab
          caseItem={caseItem}
          updates={lienUpdatesQuery.data ?? []}
          updatesLoading={lienUpdatesQuery.isLoading}
          onCreate={() =>
            navigation.navigate('CreateLien', { caseId: route.params.caseId })
          }
          onView={(lienId) => navigation.navigate('ManagementLienDetail', { lienId })}
        />
      ) : null}
      {activeTab === 'documents' ? <CaseDocumentsTab caseId={route.params.caseId} /> : null}
      {activeTab === 'servicing' ? (
        <CaseServicingTab
          caseItem={caseItem}
          payments={settlementQuery.data?.payments ?? []}
          reductions={settlementQuery.data?.reductions ?? []}
          settlementError={settlementQuery.isError}
          settlementLoading={settlementQuery.isLoading}
          settlements={settlementQuery.data?.settlements ?? []}
          updates={updatesQuery.data ?? []}
          onAddPayment={() => toast.showInfo('Add Payment is not available yet.')}
          onEdit={() => navigation.navigate('EditCaseDetails', { caseId: route.params.caseId })}
          onNoRecovery={() => toast.showInfo('No Recovery is not available yet.')}
          onSetupReduction={() => toast.showInfo('Setup Reduction is not available yet.')}
        />
      ) : null}
      {activeTab === 'notes' ? <CaseNotesTab caseId={route.params.caseId} /> : null}
      {activeTab === 'tasks' ? <CaseDetailPlaceholderPage title="Task Manager" /> : null}

      <ManageCaseModal
        visible={manageVisible}
        onClose={() => setManageVisible(false)}
        onDeleteCase={confirmDelete}
        onMergeCase={() => {
          setManageVisible(false);
          setMergeVisible(true);
        }}
        onPayoffQuote={() => {
          setManageVisible(false);
          navigation.navigate('PayoffQuote', { caseId: route.params.caseId });
        }}
      />

      {mergeVisible ? (
        <MergeCaseModal
          currentCaseId={route.params.caseId}
          onClose={() => setMergeVisible(false)}
          onSelect={confirmMerge}
        />
      ) : null}

    </View>
  );
}
