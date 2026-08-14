import { Pressable, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';

export function OptionRow({
  label,
  selected,
  onPress,
}: {
  label: string;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'mb-2 min-h-[48px] flex-row items-center justify-between rounded-[14px] border px-4',
        selected
          ? 'border-[#f97332] bg-[#fff1e9] dark:bg-[#3b2418]'
          : 'border-[#eeeeef] bg-white dark:border-[#33343a] dark:bg-[#202126]'
      )}
      onPress={onPress}
    >
      <Text
        className={cx(
          TYPE.rowValue,
          selected ? 'text-[#18181b] dark:text-white' : 'text-[#525762] dark:text-[#e7e7e9]'
        )}
      >
        {label}
      </Text>
      {selected ? <Ionicons color="#f97332" name="checkmark-circle" size={20} /> : null}
    </Pressable>
  );
}
