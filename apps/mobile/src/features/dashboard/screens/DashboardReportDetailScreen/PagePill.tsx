import { Pressable, Text } from 'react-native';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';

export function PagePill({
  isCurrent,
  page,
  onPress,
}: {
  isCurrent: boolean;
  page: number;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'h-8 min-w-[32px] items-center justify-center rounded-2xl px-3',
        isCurrent && 'bg-[#ebebec] dark:bg-[#2a2b30]'
      )}
      disabled={isCurrent}
      onPress={onPress}
    >
      <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>{page}</Text>
    </Pressable>
  );
}
