import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { MUTED } from './dashboardShared';

export function SectionTitle({
  icon,
  subtitle,
  title,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  subtitle: string;
  title: string;
}) {
  return (
    <View>
      <View className="flex-row items-center gap-2">
        <Ionicons color={MUTED} name={icon} size={17} />
        <Text className={cx(TYPE.cardTitle, 'text-[#24272d] dark:text-[#f5f5f5]')}>{title}</Text>
      </View>
      <Text className={cx(TYPE.cardDescription, 'mt-2 text-[#8d9098] dark:text-[#8f929b]')}>
        {subtitle}
      </Text>
    </View>
  );
}
