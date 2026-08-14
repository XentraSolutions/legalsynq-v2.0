import { Pressable, ScrollView, Text, View } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface TabItem {
  id: string;
  label: string;
}

export interface TabsProps {
  tabs: TabItem[];
  activeTab: string;
  onTabChange: (id: string) => void;
}

export function Tabs({ tabs, activeTab, onTabChange }: TabsProps) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
      <View className="flex-row gap-2 px-5 py-3">
        {tabs.map((tab) => {
          const active = tab.id === activeTab;
          return (
            <Pressable
              accessibilityRole="tab"
              accessibilityState={{ selected: active }}
              className={cx(
                'rounded-full px-4 py-2',
                active ? 'bg-[#f97332]' : 'bg-white dark:bg-[#191a1f]'
              )}
              key={tab.id}
              onPress={() => onTabChange(tab.id)}
            >
              <Text className={cx(FIGMA_TEXT.rowValue, active ? 'text-[#111111]' : 'text-[#6f737d] dark:text-[#c7c8cc]')}>
                {tab.label}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </ScrollView>
  );
}
