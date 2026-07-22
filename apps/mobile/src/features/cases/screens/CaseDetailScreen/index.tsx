import type { ReactNode } from 'react';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CaseDetailHeader } from '@/features/cases/components/CaseDetailHeader';
import { CaseDetailPlaceholderPage } from '@/features/cases/components/CaseDetailPlaceholderPage';
import { CaseDetailTabBar } from '@/features/cases/components/CaseDetailTabBar';
import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import { CaseSummaryRow } from '@/features/cases/components/CaseSummaryRow';
import { NoteItem } from '@/features/cases/components/NoteItem';
import { useAddCaseNote, useCaseDetail, useCaseNotes } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { CaseDetailResponse } from '@/shared/api/endpoints/Cases';
import type { BadgeVariant } from '@/shared/components/Badge/Badge';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Input } from '@/shared/components/Input';
import { Modal } from '@/shared/components/Modal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

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

function DetailsTab({ caseItem }: { caseItem: CaseDetailResponse }) {
  return (
    <CaseDetailTabPage testID="case-details-page">
      <SectionCard title="Case Details">
        <CaseSummaryRow label="External Reference" value={caseItem.externalReference} />
        <CaseSummaryRow label="Phone" value={caseItem.clientPhone} />
        <CaseSummaryRow label="Email" value={caseItem.clientEmail} />
        <CaseSummaryRow label="Address" value={caseItem.clientAddress} />
        <CaseSummaryRow label="Insurance Carrier" value={caseItem.insuranceCarrier} />
        <CaseSummaryRow label="Policy Number" value={caseItem.policyNumber} />
        <CaseSummaryRow label="Claim Number" value={caseItem.claimNumber} />
        <CaseSummaryRow
          label="Demand"
          value={caseItem.demandAmount == null ? null : formatCurrency(caseItem.demandAmount)}
        />
        <CaseSummaryRow
          label="Settlement"
          showDivider={false}
          value={
            caseItem.settlementAmount == null ? null : formatCurrency(caseItem.settlementAmount)
          }
        />
      </SectionCard>
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

export function CaseDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const notesQuery = useCaseNotes(route.params.caseId);
  const addNote = useAddCaseNote(route.params.caseId);
  const toast = useToast();
  const [activeTab, setActiveTab] = useState<CaseDetailTabId>('summary');
  const [noteVisible, setNoteVisible] = useState(false);
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
        onMore={() => setNoteVisible(true)}
      />
      <CaseDetailTabBar
        activeTab={activeTab}
        tabs={CASE_DETAIL_TABS}
        onChange={setActiveTab}
      />

      {activeTab === 'summary' ? <SummaryTab caseItem={caseItem} /> : null}
      {activeTab === 'details' ? <DetailsTab caseItem={caseItem} /> : null}
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
