import { Pressable, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute, type RouteProp } from '@react-navigation/native';
import { useColorScheme as useNativeWindColorScheme } from 'nativewind';

import type { MainStackParamList } from '@/navigation/types/navigation';
import { cx, FIGMA_COLORS, FIGMA_TEXT as TYPE } from '@/shared/styles';

type PlaceholderRoute = RouteProp<MainStackParamList, 'Placeholder'>;

export function PlaceholderScreen() {
  const navigation = useNavigation();
  const route = useRoute<PlaceholderRoute>();
  const { colorScheme } = useNativeWindColorScheme();
  const isDark = colorScheme === 'dark';

  return (
    <SafeAreaView edges={['top']} className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <View className="flex-row items-center justify-between px-6 py-4">
        <Pressable
          accessibilityRole="button"
          className="h-10 w-10 items-center justify-center rounded-full bg-white dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.18 : 0.42,
            shadowRadius: 8,
            shadowOffset: { height: 3, width: 0 },
            elevation: 2,
          }}
          onPress={() => navigation.goBack()}
        >
          <Ionicons color={isDark ? '#e7e7e9' : '#525762'} name="arrow-back" size={18} />
        </Pressable>
      </View>

      <View className="flex-1 px-6 py-3">
        <View
          className="flex-1 items-center justify-center rounded-[20px] bg-white px-6 dark:bg-[#191a1f]"
          style={{
            shadowColor: isDark ? FIGMA_COLORS.shadowDark : FIGMA_COLORS.shadowLight,
            shadowOpacity: isDark ? 0.18 : 0.44,
            shadowRadius: 10,
            shadowOffset: { height: 4, width: 0 },
            elevation: 2,
          }}
        >
          <View className="mb-5 h-14 w-14 items-center justify-center rounded-full bg-[#fff0e8] dark:bg-[#3a2318]">
            <Ionicons color="#f97332" name="construct-outline" size={24} />
          </View>
          <Text className={cx(TYPE.screenTitle, 'text-center text-[#18181b] dark:text-white')}>
            {route.params.title}
          </Text>
          <Text
            className={cx(
              TYPE.screenSubtitle,
              'mt-3 text-center text-[#71717a] dark:text-[#a1a1aa]'
            )}
          >
            {route.params.subtitle ??
              'This workspace area is ready for the next implementation pass.'}
          </Text>
        </View>
      </View>
    </SafeAreaView>
  );
}
