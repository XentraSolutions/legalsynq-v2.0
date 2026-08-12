import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';

export function LegendPagination({
  page,
  totalPages,
  onNext,
  onPrevious,
}: {
  page: number;
  totalPages: number;
  onNext: () => void;
  onPrevious: () => void;
}) {
  const canGoPrevious = page > 1;
  const canGoNext = page < totalPages;

  return (
    <View className="mt-3 flex-row items-center justify-between border-t border-[#ececf0] pt-3 dark:border-[#292a2f]">
      <Pressable
        accessibilityRole="button"
        className={cx(
          'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
          !canGoPrevious && 'opacity-50'
        )}
        disabled={!canGoPrevious}
        onPress={onPrevious}
      >
        <Ionicons
          color={canGoPrevious ? '#71717a' : '#a1a1aa'}
          name="chevron-back-outline"
          size={14}
        />
        <Text className={cx(TYPE.rowValue, 'text-[#22252b] dark:text-white')}>Previous</Text>
      </Pressable>
      <Text className={cx(TYPE.rowMuted, 'text-[#8d9098] dark:text-[#8f929b]')}>
        Page {page} of {totalPages}
      </Text>
      <Pressable
        accessibilityRole="button"
        className={cx(
          'h-8 flex-row items-center gap-1 rounded-full border border-[#dedee0] px-3 dark:border-[#33343a]',
          !canGoNext && 'opacity-50'
        )}
        disabled={!canGoNext}
        onPress={onNext}
      >
        <Text className={cx(TYPE.rowValue, 'text-[#22252b] dark:text-white')}>Next</Text>
        <Ionicons
          color={canGoNext ? '#22252b' : '#a1a1aa'}
          name="chevron-forward-outline"
          size={14}
        />
      </Pressable>
    </View>
  );
}
