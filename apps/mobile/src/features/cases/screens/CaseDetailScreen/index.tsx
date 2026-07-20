import type { ReactNode } from 'react';
import { useState } from 'react';
import { Modal as ReactNativeModal, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CaseDetailHeader } from '@/features/cases/components/CaseDetailHeader';
import { CaseDetailPlaceholderPage } from '@/features/cases/components/CaseDetailPlaceholderPage';
import { CaseDetailTabBar } from '@/features/cases/components/CaseDetailTabBar';
import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { CaseSummaryRow } from '@/features/cases/components/CaseSummaryRow';
import { NoteItem } from '@/features/cases/components/NoteItem';
import {
  useAddCaseNote,
  useCaseDetail,
  useCaseNotes,
  useCaseUpdates,
} from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { CaseDetailResponse, CaseUpdate } from '@/shared/api/endpoints/Cases';
import type { BadgeVariant } from '@/shared/components/Badge/Badge';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Input } from '@/shared/components/Input';
import { Modal } from '@/shared/components/Modal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
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

function CardHeader({
  icon,
  title,
  onEdit,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  onEdit?: () => void;
}) {
  return (
    <View className="mb-2 flex-row items-center justify-between">
      <View className="flex-row items-center gap-2">
        <Ionicons color="#6f737d" name={icon} size={18} />
        <Text className="font-jakarta-medium text-[14px] leading-5 text-[#202228] dark:text-white">
          {title}
        </Text>
      </View>
      {onEdit ? (
        <Pressable
          accessibilityLabel={`Edit ${title}`}
          accessibilityRole="button"
          className="h-9 w-9 items-center justify-center rounded-full border border-[#dedee0] dark:border-[#303138]"
          onPress={onEdit}
        >
          <Ionicons color="#6f737d" name="pencil-outline" size={16} />
        </Pressable>
      ) : null}
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
  return (
    <CaseDetailTabPage testID="case-details-page">
      <SectionCard title="Case Tracking">
        <CardHeader icon="document-text-outline" title="Case Details" onEdit={onEditDetails} />
        <CaseSummaryRow label="Tracking Policy ID" value={caseItem.externalReference} />
        <CaseSummaryRow label="Current Status" value={displayStatus(caseItem.status)} />
        <CaseSummaryRow label="Case Type" value={caseItem.accidentType} />
        <CaseSummaryRow label="Date of Loss" value={displayDate(caseItem.dateOfIncident)} />
        <CaseSummaryRow label="State of Incident" value={caseItem.stateOfIncident} />
        <CaseSummaryRow label="Insurance Carrier" value={caseItem.insuranceCarrier} />
        <CaseSummaryRow label="Policy Number" value={caseItem.policyNumber} />
        <CaseSummaryRow label="Claim Number" value={caseItem.claimNumber} />
        <CaseSummaryRow label="Case Tracking Note" showDivider={false} value={caseItem.description} />
      </SectionCard>

      <View className="mt-4">
        <SectionCard title="Plaintiff">
          <CardHeader icon="person-outline" title="Plaintiff Info" onEdit={onEditPersonal} />
          <CaseSummaryRow label="Full Name" value={caseItem.clientDisplayName} />
          <CaseSummaryRow label="Phone Number" value={caseItem.clientPhone} />
          <CaseSummaryRow label="Email" value={caseItem.clientEmail} />
          <CaseSummaryRow label="Birthdate" value={displayDate(caseItem.clientDob)} />
          <CaseSummaryRow label="Address" showDivider={false} value={caseItem.clientAddress} />
        </SectionCard>
      </View>

      <View className="mt-4">
        <SectionCard title="Recent Updates">
          {updatesLoading ? (
            <Spinner />
          ) : updates.length > 0 ? (
            updates.slice(0, 3).map((update, index) => (
              <View
                key={update.id ?? `${updateTimestamp(update)}-${index}`}
                className={cx(
                  'py-4',
                  index < Math.min(updates.length, 3) - 1
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
            ))
          ) : (
            <Text className="py-5 text-center font-jakarta text-[14px] leading-5 text-[#777a84] dark:text-[#a1a1aa]">
              No recent updates.
            </Text>
          )}
        </SectionCard>
      </View>
    </CaseDetailTabPage>
  );
}

function NotesTab({
  isLoading,
  notes,
  onAddNote,
}: {
  isLoading: boolean;
  notes: ReturnType<typeof useCaseNotes>['data'];
  onAddNote: () => void;
}) {
  return (
    <CaseDetailTabPage testID="case-notes-page">
      <View className="flex-row items-center justify-between">
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Notes</Text>
        <Button label="+ Add Note" size="sm" variant="ghost" onPress={onAddNote} />
      </View>
      <View className="mt-4 gap-4">
        {isLoading ? (
          <Spinner />
        ) : (notes ?? []).length > 0 ? (
          notes?.map((note) => <NoteItem key={note.id} note={note} />)
        ) : (
          <View className="rounded-[20px] bg-white px-6 py-10 dark:bg-[#191a1f]" style={SHADOWS.sm}>
            <Text className={cx(FIGMA_TEXT.body, 'text-center text-[#777a84] dark:text-[#a1a1aa]')}>
              No notes have been added.
            </Text>
          </View>
        )}
      </View>
    </CaseDetailTabPage>
  );
}

function ManageCaseModal({
  visible,
  onClose,
  onPayoffQuote,
  onComingSoon,
}: {
  visible: boolean;
  onClose: () => void;
  onPayoffQuote: () => void;
  onComingSoon: (feature: string) => void;
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
      onPress: () => onComingSoon('Merge Case'),
    },
    {
      danger: true,
      icon: 'trash-outline' as const,
      label: 'Delete Case',
      onPress: () => onComingSoon('Delete Case'),
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

export function CaseDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<DetailRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const notesQuery = useCaseNotes(route.params.caseId);
  const updatesQuery = useCaseUpdates(route.params.caseId);
  const addNote = useAddCaseNote(route.params.caseId);
  const toast = useToast();
  const [activeTab, setActiveTab] = useState<CaseDetailTabId>('summary');
  const [noteVisible, setNoteVisible] = useState(false);
  const [manageVisible, setManageVisible] = useState(false);
  const [noteContent, setNoteContent] = useState('');

  async function submitNote() {
    const content = noteContent.trim();
    if (!content) return;

    try {
      await addNote.mutateAsync(content);
      setNoteContent('');
      setNoteVisible(false);
      toast.showSuccess('Note posted');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to post the note');
    }
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
      {activeTab === 'liens' ? <CaseDetailPlaceholderPage title="Liens" /> : null}
      {activeTab === 'documents' ? <CaseDetailPlaceholderPage title="Documents" /> : null}
      {activeTab === 'servicing' ? <CaseDetailPlaceholderPage title="Servicing" /> : null}
      {activeTab === 'notes' ? (
        <NotesTab
          isLoading={notesQuery.isLoading}
          notes={notesQuery.data}
          onAddNote={() => setNoteVisible(true)}
        />
      ) : null}
      {activeTab === 'tasks' ? <CaseDetailPlaceholderPage title="Task Manager" /> : null}

      <ManageCaseModal
        visible={manageVisible}
        onClose={() => setManageVisible(false)}
        onPayoffQuote={() => {
          setManageVisible(false);
          navigation.navigate('PayoffQuote', { caseId: route.params.caseId });
        }}
        onComingSoon={(feature) => {
          setManageVisible(false);
          toast.showInfo(`${feature} is coming soon.`);
        }}
      />

      <Modal
        footer={
          <Button
            disabled={!noteContent.trim()}
            label="Post Note"
            loading={addNote.isPending}
            onPress={submitNote}
          />
        }
        title="Add Note"
        visible={noteVisible}
        onClose={() => setNoteVisible(false)}
      >
        <Input
          multiline
          placeholder="Add a note..."
          value={noteContent}
          onChangeText={setNoteContent}
        />
      </Modal>
    </View>
  );
}
