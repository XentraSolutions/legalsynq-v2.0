import { Pressable, ScrollView, Text, View } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface CaseDetailTab<TId extends string = string> {
  id: TId;
  label: string;
}

interface CaseDetailTabBarProps<TId extends string> {
  tabs: readonly CaseDetailTab<TId>[];
  activeTab: TId;
  onChange: (tabId: TId) => void;
}

export function CaseDetailTabBar<TId extends string>({
  tabs,
  activeTab,
  onChange,
}: CaseDetailTabBarProps<TId>) {
  return (
    <View className="border-b border-[#dedfe2] bg-[#f7f7f8] dark:border-[#2c2d32] dark:bg-[#050506]">
      <ScrollView
        horizontal
        bounces={false}
        contentContainerClassName="px-6"
        showsHorizontalScrollIndicator={false}
      >
        {tabs.map((tab) => {
          const isActive = tab.id === activeTab;
          return (
            <Pressable
              accessibilityRole="tab"
              accessibilityState={{ selected: isActive }}
              className={cx(
                'mr-6 border-b-2 py-3.5',
                isActive ? 'border-[#ee7132]' : 'border-transparent'
              )}
              key={tab.id}
              onPress={() => onChange(tab.id)}
            >
              <Text
                className={cx(
                  FIGMA_TEXT.body,
                  isActive
                    ? 'text-[#202228] dark:text-white'
                    : 'text-[#777a84] dark:text-[#a1a1aa]'
                )}
              >
                {tab.label}
              </Text>
            </Pressable>
          );
        })}
      </ScrollView>
    </View>
  );
}
