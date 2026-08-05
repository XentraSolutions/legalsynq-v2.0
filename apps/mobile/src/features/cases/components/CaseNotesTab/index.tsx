import { useMemo, useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { useAddCaseNote, useCaseNotes, useDeleteCaseNote } from '@/features/cases/hooks';
import type { Note } from '@/features/cases/types/types';
import { Avatar } from '@/shared/components/Avatar';
import { Button } from '@/shared/components/Button';
import { Spinner } from '@/shared/components/Spinner';
import { useAuth, useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
import { formatDisplayDate } from '@/shared/utils';

type NotesView = 'tracking' | 'feeds';

function noteTimestamp(value: string): string {
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy hh:mm a');
  } catch {
    return value;
  }
}

function NotesSegment({
  active,
  onChange,
}: {
  active: NotesView;
  onChange: (view: NotesView) => void;
}) {
  const options = [
    { id: 'tracking' as const, icon: 'list-outline' as const, label: 'Case Tracking' },
    { id: 'feeds' as const, icon: 'chatbubble-outline' as const, label: 'Feeds' },
  ];

  return (
    <View className="mx-6 mt-6 flex-row rounded-full bg-[#ebebec] p-1 dark:bg-[#2a2b30]">
      {options.map((option) => {
        const selected = active === option.id;
        return (
          <Pressable
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            className={cx(
              'h-8 flex-1 flex-row items-center justify-center gap-2 rounded-full',
              selected ? 'bg-white dark:bg-[#191a1f]' : ''
            )}
            key={option.id}
            style={selected ? SHADOWS.sm : undefined}
            onPress={() => onChange(option.id)}
          >
            <Ionicons color={selected ? '#18181b' : '#71717a'} name={option.icon} size={17} />
            <Text
              className={cx(
                FIGMA_TEXT.bodyStrong,
                selected
                  ? 'text-[#18181b] dark:text-white'
                  : 'text-[#71717a] dark:text-[#a1a1aa]'
              )}
            >
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

function EmptyNotes({ view }: { view: NotesView }) {
  const tracking = view === 'tracking';
  return (
    <View
      className="items-center rounded-[20px] bg-white px-6 py-10 dark:bg-[#191a1f]"
      style={SHADOWS.sm}
    >
      <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]">
        <Ionicons
          color="#18181b"
          name={tracking ? 'list-outline' : 'chatbubble-outline'}
          size={18}
        />
      </View>
      <Text className="mt-5 font-jakarta-semibold text-[16px] leading-6 text-[#18181b] dark:text-white">
        {tracking ? 'No Case Tracking Notes' : 'No Feed Notes'}
      </Text>
      <Text className="mt-2 text-center font-jakarta text-[14px] leading-5 text-[#71717a] dark:text-[#a1a1aa]">
        {tracking
          ? 'Case tracking notes and their history will appear here once added.'
          : 'No feed notes have been added yet. They will appear here once available.'}
      </Text>
    </View>
  );
}

function NoteCard({
  note,
  canManage,
  onManage,
}: {
  note: Note;
  canManage: boolean;
  onManage: () => void;
}) {
  return (
    <View className="rounded-[20px] bg-white p-6 dark:bg-[#191a1f]" style={SHADOWS.sm}>
      <View className="flex-row items-start gap-3">
        <Avatar name={note.authorName} size="sm" />
        <View className="min-w-0 flex-1">
          <View className="flex-row items-start gap-2">
            <View className="min-w-0 flex-1">
              <Text className="font-jakarta-medium text-[14px] leading-5 text-[#18181b] dark:text-white">
                {note.authorName}
              </Text>
              <Text className="mt-1 font-jakarta text-[12px] leading-4 text-[#71717a] dark:text-[#a1a1aa]">
                {noteTimestamp(note.createdAt)}
              </Text>
            </View>
            {canManage ? (
              <Pressable
                accessibilityLabel={`Manage note by ${note.authorName}`}
                accessibilityRole="button"
                className="h-7 w-6 items-center justify-center"
                hitSlop={10}
                onPress={onManage}
              >
                <Ionicons color="#71717a" name="ellipsis-vertical" size={18} />
              </Pressable>
            ) : null}
          </View>
          <Text className="mt-3 font-jakarta text-[12px] leading-4 text-[#71717a] dark:text-[#a1a1aa]">
            {note.content}
          </Text>
        </View>
      </View>
    </View>
  );
}

function ManageNoteModal({
  deleting,
  note,
  onClose,
  onDelete,
}: {
  deleting: boolean;
  note: Note | null;
  onClose: () => void;
  onDelete: () => void;
}) {
  return (
    <Modal animationType="fade" transparent visible={Boolean(note)} onRequestClose={onClose}>
      <View className="flex-1 justify-end bg-black/30 p-4">
        <Pressable
          accessibilityLabel="Close manage note"
          className="absolute inset-0"
          onPress={onClose}
        />
        <View className="rounded-[24px] bg-white p-6 dark:bg-[#191a1f]" style={SHADOWS.lg}>
          <View className="flex-row items-start justify-between gap-4">
            <View className="flex-1">
              <Text className="font-jakarta-medium text-[16px] leading-6 text-[#18181b] dark:text-white">
                Manage Note
              </Text>
              <Text className="mt-2 font-jakarta text-[14px] leading-5 text-[#71717a] dark:text-[#a1a1aa]">
                Select an action to manage this note.
              </Text>
            </View>
            <Pressable
              accessibilityLabel="Close note actions"
              accessibilityRole="button"
              className="h-6 w-6 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]"
              onPress={onClose}
            >
              <Ionicons color="#71717a" name="close" size={16} />
            </Pressable>
          </View>
          <Pressable
            accessibilityRole="button"
            className="mt-5 h-12 flex-row items-center border-b border-[#e4e4e7] dark:border-[#303138]"
            disabled={deleting}
            onPress={onDelete}
          >
            <Ionicons color="#ff383c" name="trash-outline" size={18} />
            <Text className="ml-2 flex-1 font-jakarta text-[14px] leading-5 text-[#ff383c]">
              {deleting ? 'Deleting…' : 'Delete Note'}
            </Text>
            <Ionicons color="#71717a" name="chevron-forward" size={20} />
          </Pressable>
          <Button className="mt-5" label="Cancel" variant="secondary" onPress={onClose} />
        </View>
      </View>
    </Modal>
  );
}

export function CaseNotesTab({ caseId }: { caseId: string }) {
  const notesQuery = useCaseNotes(caseId);
  const addNote = useAddCaseNote(caseId);
  const deleteNote = useDeleteCaseNote(caseId);
  const { user } = useAuth();
  const toast = useToast();
  const [activeView, setActiveView] = useState<NotesView>('tracking');
  const [content, setContent] = useState('');
  const [managedNote, setManagedNote] = useState<Note | null>(null);
  const notes = notesQuery.data ?? [];
  const visibleNotes = useMemo(
    () =>
      notes.filter((note) =>
        activeView === 'feeds' ? note.category === 'general' : note.category !== 'general'
      ),
    [activeView, notes]
  );

  async function submitNote() {
    const trimmed = content.trim();
    if (!trimmed) return;
    try {
      await addNote.mutateAsync({ content: trimmed, category: 'general' });
      setContent('');
      toast.showSuccess('Note added successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to add the note.');
    }
  }

  async function removeNote() {
    if (!managedNote) return;
    try {
      await deleteNote.mutateAsync(managedNote.id);
      setManagedNote(null);
      toast.showSuccess('Note deleted successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to delete the note.');
    }
  }

  return (
    <KeyboardAvoidingView
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      className="flex-1"
      testID="case-notes-page"
    >
      <NotesSegment active={activeView} onChange={setActiveView} />
      <ScrollView
        className="flex-1"
        contentContainerClassName="gap-4 px-6 pb-8 pt-6"
        contentContainerStyle={{ flexGrow: 1 }}
        keyboardShouldPersistTaps="handled"
      >
        {notesQuery.isLoading ? (
          <View className="flex-1 items-center justify-center">
            <Spinner />
          </View>
        ) : notesQuery.isError ? (
          <View className="items-center rounded-[20px] bg-white px-6 py-10 dark:bg-[#191a1f]" style={SHADOWS.sm}>
            <Ionicons color="#ee7132" name="alert-circle-outline" size={38} />
            <Text className="mt-3 text-center font-jakarta text-[14px] leading-5 text-[#71717a]">
              Notes could not be loaded.
            </Text>
            <Button
              className="mt-5 w-full"
              label="Try Again"
              size="sm"
              variant="secondary"
              onPress={() => void notesQuery.refetch()}
            />
          </View>
        ) : visibleNotes.length ? (
          visibleNotes.map((note) => (
            <NoteCard
              canManage={Boolean(user?.id && note.authorId === user.id)}
              key={note.id}
              note={note}
              onManage={() => setManagedNote(note)}
            />
          ))
        ) : (
          <EmptyNotes view={activeView} />
        )}
      </ScrollView>

      {activeView === 'feeds' ? (
        <View className="flex-row items-end gap-3 border-t border-[#dedee0] bg-white px-6 py-4 dark:border-[#303138] dark:bg-[#191a1f]">
          <TextInput
            accessibilityLabel="Add feed note"
            className="max-h-28 min-h-10 flex-1 rounded-[20px] bg-[#ebebec] px-4 py-2 font-jakarta text-[14px] leading-5 text-[#18181b] dark:bg-[#2a2b30] dark:text-white"
            maxLength={5000}
            multiline
            placeholder="Add note..."
            placeholderTextColor="#858892"
            value={content}
            onChangeText={setContent}
          />
          <Pressable
            accessibilityLabel="Send note"
            accessibilityRole="button"
            className={cx(
              'h-10 w-10 items-center justify-center rounded-full bg-[#f97332]',
              !content.trim() || addNote.isPending ? 'opacity-50' : ''
            )}
            disabled={!content.trim() || addNote.isPending}
            onPress={() => void submitNote()}
          >
            {addNote.isPending ? (
              <Spinner color="#ffffff" />
            ) : (
              <Ionicons color="#ffffff" name="send-outline" size={18} />
            )}
          </Pressable>
        </View>
      ) : null}

      <ManageNoteModal
        deleting={deleteNote.isPending}
        note={managedNote}
        onClose={() => setManagedNote(null)}
        onDelete={() => void removeNote()}
      />
    </KeyboardAvoidingView>
  );
}
