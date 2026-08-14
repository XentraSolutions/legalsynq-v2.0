import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { MUTED } from './index';
import { CardShell } from './CardShell';

export function DashboardEmptyStateCard({
  isDark,
  message,
  title,
}: {
  isDark: boolean;
  message: string;
  title: string;
}) {
  return (
    <CardShell isDark={isDark}>
      <View className="items-center py-6">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-[#ececee] dark:bg-[#2a2b30]">
          <Ionicons color={MUTED} name="analytics-outline" size={22} />
        </View>
        <Text className={cx(TYPE.cardTitle, 'mt-4 text-center text-[#24272d] dark:text-white')}>
          {title}
        </Text>
        <Text
          className={cx(
            TYPE.cardDescription,
            'mt-2 text-center text-[#8d9098] dark:text-[#8f929b]'
          )}
        >
          {message}
        </Text>
      </View>
    </CardShell>
  );
}
