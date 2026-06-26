import { Text, View } from 'react-native';

import type { Note } from '@/features/cases/types/types';
import { Avatar } from '@/shared/components/Avatar';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatRelativeDate } from '@/shared/utils';

export function NoteItem({ note }: { note: Note }) {
  return (
    <View className="flex-row gap-3">
      <Avatar name={note.authorName} size="sm" />
      <View className="flex-1">
        <View className="flex-row items-center justify-between">
          <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>{note.authorName}</Text>
          <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>{formatRelativeDate(note.createdAt)}</Text>
        </View>
        <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>{note.content}</Text>
      </View>
    </View>
  );
}
