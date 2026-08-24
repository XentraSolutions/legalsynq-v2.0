import { Pressable, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_COLORS } from '@/shared/styles';

export function CircleButton({
  accessibilityLabel,
  accent,
  dot,
  icon,
  iconColor,
  isDark,
  onPress,
}: {
  accessibilityLabel?: string;
  accent?: boolean;
  dot?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  iconColor: string;
  isDark: boolean;
  onPress?: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      className={cx(
        'h-10 w-10 items-center justify-center rounded-full',
        accent ? 'bg-[#ee7132]' : 'bg-white dark:bg-[#191a1f]'
      )}
      style={{
        shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
        shadowOpacity: isDark ? 0.18 : 0.5,
        shadowRadius: 8,
        shadowOffset: { height: 3, width: 0 },
        elevation: 2,
      }}
      onPress={onPress}
    >
      <Ionicons color={iconColor} name={icon} size={19} />
      {dot ? <View className="absolute right-2 top-2 h-2 w-2 rounded-full bg-[#ef4444]" /> : null}
    </Pressable>
  );
}
