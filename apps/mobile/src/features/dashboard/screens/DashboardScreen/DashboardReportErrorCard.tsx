import { Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { cx, FIGMA_TEXT as TYPE } from '@/shared/styles';
import { ORANGE } from './index';
import { CardShell } from './CardShell';

export function DashboardReportErrorCard({
  isDark,
  onRetry,
  title,
}: {
  isDark: boolean;
  onRetry: () => void;
  title: string;
}) {
  return (
    <CardShell isDark={isDark}>
      <View className="items-center py-6">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-[#fff0e9] dark:bg-[#3a241b]">
          <Ionicons color={ORANGE} name="warning-outline" size={22} />
        </View>
        <Text className={cx(TYPE.cardTitle, 'mt-4 text-center text-[#24272d] dark:text-white')}>
          {title} could not be loaded
        </Text>
        <Text
          className={cx(
            TYPE.cardDescription,
            'mt-2 text-center text-[#8d9098] dark:text-[#8f929b]'
          )}
        >
          Pull down to refresh the dashboard or try this report again.
        </Text>
        <Pressable
          accessibilityLabel={`Retry ${title}`}
          accessibilityRole="button"
          className="mt-5 h-9 items-center justify-center rounded-full bg-[#fff0e9] px-6 dark:bg-[#3a241b]"
          onPress={onRetry}
        >
          <Text className={cx(TYPE.cta, 'text-[#d94f16] dark:text-[#fb8b5c]')}>Retry</Text>
        </Pressable>
      </View>
    </CardShell>
  );
}
