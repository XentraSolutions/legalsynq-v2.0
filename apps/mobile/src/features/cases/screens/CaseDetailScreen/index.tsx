import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { NoteItem } from '@/features/cases/components';
import { useAddCaseNote, useCaseDetail, useCaseNotes } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Badge } from '@/shared/components/Badge';
import { Button } from '@/shared/components/Button';
import { Divider } from '@/shared/components/Divider';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { Modal } from '@/shared/components/Modal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

type DetailRoute = NativeStackScreenProps<MainStackParamList, 'CaseDetail'>['route'];

function displayDate(value: string): string {
  try {
    return formatDisplayDate(value);
  } catch {
    return value;
  }
}

export function CaseDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const notesQuery = useCaseNotes(route.params.caseId);
  const addNote = useAddCaseNote(route.params.caseId);
  const toast = useToast();
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
        <Header showBack title="Case Details" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      </View>
    );
  }

  if (caseQuery.isError || !caseQuery.data) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Case Details" onBack={() => navigation.goBack()} />
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
  const detailFields = [
    ['Case Number', caseItem.caseNumber],
    ['External Reference', caseItem.externalReference],
    ['Date of Loss', caseItem.dateOfIncident ? displayDate(caseItem.dateOfIncident) : '—'],
    ['Date of Birth', caseItem.clientDob ? displayDate(caseItem.clientDob) : '—'],
    ['Phone', caseItem.clientPhone],
    ['Email', caseItem.clientEmail],
    ['Insurance Carrier', caseItem.insuranceCarrier],
    ['Claim Number', caseItem.claimNumber],
  ];

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Case Details" onBack={() => navigation.goBack()} />
      <ScrollView className="flex-1 px-5" contentContainerClassName="pb-8 pt-4">
        <View className="flex-row items-start justify-between gap-3">
          <View className="flex-1">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              {caseItem.clientDisplayName}
            </Text>
            {caseItem.title ? (
              <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
                {caseItem.title}
              </Text>
            ) : null}
          </View>
          <Badge label={caseItem.status} variant="success" />
        </View>

        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-6 text-[#202228] dark:text-white')}>
          Case Information
        </Text>
        <View className="mt-4 flex-row flex-wrap gap-y-5">
          {detailFields.map(([label, value]) => (
            <View className="w-1/2 pr-3" key={label}>
              <Text className={cx(FIGMA_TEXT.formLabel, 'text-content-tertiary dark:text-[#8f929b]')}>
                {label}
              </Text>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-1 text-[#202228] dark:text-white')}>
                {value || '—'}
              </Text>
            </View>
          ))}
        </View>

        {caseItem.demandAmount != null || caseItem.settlementAmount != null ? (
          <>
            <Divider />
            <View className="flex-row gap-4">
              <View className="flex-1 rounded-[16px] bg-white p-4 dark:bg-[#191a1f]">
                <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#8f929b]')}>Demand</Text>
                <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-1 text-[#202228] dark:text-white')}>
                  {formatCurrency(caseItem.demandAmount ?? 0)}
                </Text>
              </View>
              <View className="flex-1 rounded-[16px] bg-white p-4 dark:bg-[#191a1f]">
                <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#8f929b]')}>Settlement</Text>
                <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-1 text-[#202228] dark:text-white')}>
                  {formatCurrency(caseItem.settlementAmount ?? 0)}
                </Text>
              </View>
            </View>
          </>
        ) : null}

        <Divider />
        <View className="flex-row items-center justify-between">
          <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Notes</Text>
          <Button label="+ Add Note" size="sm" variant="ghost" onPress={() => setNoteVisible(true)} />
        </View>
        <View className="mt-4 gap-4">
          {notesQuery.isLoading ? (
            <Spinner />
          ) : (notesQuery.data ?? []).length > 0 ? (
            notesQuery.data?.map((note) => <NoteItem key={note.id} note={note} />)
          ) : (
            <Text className={cx(FIGMA_TEXT.body, 'text-[#8f929b]')}>No notes have been added.</Text>
          )}
        </View>
      </ScrollView>

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
