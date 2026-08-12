import { Pressable, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';

export function PaginationButton({
  disabled,
  icon,
  label,
  onPress,
}: {
  disabled?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress: () => void;
}) {
  const iconColor = disabled ? '#a1a1aa' : label === 'Next' ? '#18181b' : '#71717a';

  return (
    <Pressable
      accessibilityRole="button"
      className={cx(
        'h-8 flex-row items-center gap-1 rounded-2xl border border-[#dedee0] px-3 dark:border-[#33343a]',
        disabled && 'opacity-50'
      )}
      disabled={disabled}
      onPress={onPress}
    >
      {label === 'Previous' ? <Ionicons color={iconColor} name={icon} size={14} /> : null}
      <Text className={cx(TYPE.rowValue, 'text-[#18181b] dark:text-white')}>{label}</Text>
      {label === 'Next' ? <Ionicons color={iconColor} name={icon} size={14} /> : null}
    </Pressable>
  );
}
