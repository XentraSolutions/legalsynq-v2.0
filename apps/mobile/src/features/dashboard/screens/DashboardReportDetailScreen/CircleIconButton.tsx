import { Pressable } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { FIGMA_COLORS } from '@/shared/styles';

export function CircleIconButton({
  accessibilityLabel,
  icon,
  isDark,
  onPress,
}: {
  accessibilityLabel: string;
  icon: keyof typeof Ionicons.glyphMap;
  isDark: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className="h-11 w-11 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.16 : 0.4,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
      onPress={onPress}
    >
      <Ionicons color={isDark ? '#e7e7e9' : '#525762'} name={icon} size={20} />
    </Pressable>
  );
}
