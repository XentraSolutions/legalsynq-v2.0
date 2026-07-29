import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CASE_TYPE_LABELS, LIENS } from '@/features/mockData';
import { NoteItem } from '@/features/cases/components';
import { useAddCaseNote, useCaseDetail, useCaseNotes } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Badge } from '@/shared/components/Badge';
import { Button } from '@/shared/components/Button';
import { Card } from '@/shared/components/Card';
import { Chip } from '@/shared/components/Chip';
import { Divider } from '@/shared/components/Divider';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { Modal } from '@/shared/components/Modal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

export function CaseDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'CaseDetail'>['route']>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const notesQuery = useCaseNotes(route.params.caseId);
  const addNote = useAddCaseNote(route.params.caseId);
  const toast = useToast();
  const [noteVisible, setNoteVisible] = useState(false);
  const [noteContent, setNoteContent] = useState('');
  const caseItem = caseQuery.data;

  async function submitNote() {
    await addNote.mutateAsync(noteContent);
    setNoteContent('');
    setNoteVisible(false);
    toast.showSuccess('Note posted');
  }

  if (!caseItem) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Case Details" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      </View>
    );
  }

  const linkedLiens = LIENS.filter((lien) => caseItem.linkedLienIds.includes(lien.id));

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header
        rightAction={<Ionicons color="#f97332" name="chatbubble-ellipses-outline" size={22} />}
        showBack
        title="Case Details"
        onBack={() => navigation.goBack()}
      />
      <ScrollView className="flex-1 px-5" contentContainerClassName="pb-8 pt-4">
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>{caseItem.patientName}</Text>
        <View className="mt-2 flex-row gap-2">
          <Chip label={CASE_TYPE_LABELS[caseItem.caseType]} />
          <Badge label={caseItem.status} variant="success" />
        </View>
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-5 text-[#202228] dark:text-white')}>Case Info</Text>
        <View className="mt-4 flex-row flex-wrap gap-y-4">
          {[
            ['Reference', caseItem.caseReference],
            ['Case Type', CASE_TYPE_LABELS[caseItem.caseType]],
            ['Incident Date', formatDisplayDate(caseItem.incidentDate)],
            ['Jurisdiction', caseItem.jurisdiction],
            ['Status', caseItem.status],
            ['Assigned Attorney', caseItem.assignedAttorney],
          ].map(([label, value]) => (
            <View className="w-1/2" key={label}>
              <Text className={cx(FIGMA_TEXT.formLabel, 'text-content-tertiary dark:text-[#8f929b]')}>{label}</Text>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-1 text-[#202228] dark:text-white')}>{value}</Text>
            </View>
          ))}
        </View>
        <Divider />
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-[#202228] dark:text-white')}>Linked Liens</Text>
        <View className="mt-3 gap-3">
          {linkedLiens.map((lien) => (
            <Card key={lien.id} onPress={() => navigation.navigate('LienDetail', { lienId: lien.id })}>
              <View className="flex-row items-center justify-between">
                <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
                  {formatCurrency(lien.askingPrice ?? lien.lienAmount)}
                </Text>
                <Badge label={lien.status} variant="primary" />
              </View>
              <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
                Listed {lien.listedAt ? formatDisplayDate(lien.listedAt) : 'Draft'}
              </Text>
            </Card>
          ))}
        </View>
        <Divider />
        <View className="mt-4 flex-row items-center justify-between">
          <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Notes</Text>
          <Button label="+ Add Note" size="sm" variant="ghost" onPress={() => setNoteVisible(true)} />
        </View>
        <View className="mt-3 gap-4">
          {(notesQuery.data ?? []).map((note) => (
            <NoteItem key={note.id} note={note} />
          ))}
        </View>
      </ScrollView>
      <Modal
        footer={
          <Button
            label="Post Note"
            loading={addNote.isPending}
            onPress={submitNote}
          />
        }
        title="Add Note"
        visible={noteVisible}
        onClose={() => setNoteVisible(false)}
      >
        <Input multiline placeholder="Add a note..." value={noteContent} onChangeText={setNoteContent} />
      </Modal>
    </View>
  );
}
