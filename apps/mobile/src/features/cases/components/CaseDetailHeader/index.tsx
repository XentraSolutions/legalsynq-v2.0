import { Pressable, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';

interface CaseDetailHeaderProps {
  title: string;
  subtitle: string;
  onBack: () => void;
  onMore: () => void;
}

export function CaseDetailHeader({ title, subtitle, onBack, onMore }: CaseDetailHeaderProps) {
  return (
    <SafeAreaView edges={['top']} className="bg-[#f7f7f8] dark:bg-[#050506]">
      <View className="h-20 flex-row items-center px-6">
        <Pressable
          accessibilityLabel="Go back"
          accessibilityRole="button"
          className="h-10 w-10 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
          hitSlop={12}
          style={SHADOWS.sm}
          onPress={onBack}
        >
          <Ionicons color="#777a84" name="arrow-back" size={22} />
        </Pressable>

        <View className="mx-3 flex-1 items-center">
          <Text
            className={cx(FIGMA_TEXT.screenTitle, 'text-center text-[#202228] dark:text-white')}
            numberOfLines={1}
          >
            {title}
          </Text>
          <Text
            className={cx(
              FIGMA_TEXT.screenSubtitle,
              'mt-1 text-center text-[#777a84] dark:text-[#a1a1aa]'
            )}
            numberOfLines={1}
          >
            {subtitle}
          </Text>
        </View>

        <Pressable
          accessibilityLabel="Manage case"
          accessibilityRole="button"
          className="h-10 w-10 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
          hitSlop={12}
          style={SHADOWS.sm}
          onPress={onMore}
        >
          <Ionicons color="#777a84" name="ellipsis-vertical" size={22} />
        </Pressable>
      </View>
    </SafeAreaView>
  );
}
