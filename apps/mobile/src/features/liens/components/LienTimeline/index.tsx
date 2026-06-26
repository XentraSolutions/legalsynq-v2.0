import { Text, View } from 'react-native';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface TimelineEntry {
  label: string;
  date: string;
}

export function LienTimeline({ entries }: { entries: TimelineEntry[] }) {
  return (
    <View className="gap-3">
      {entries.map((entry, index) => (
        <View className="flex-row" key={`${entry.label}-${entry.date}`}>
          <View className="items-center">
            <View className="h-3 w-3 rounded-full bg-[#f97332]" />
            {index < entries.length - 1 ? <View className="mt-1 h-8 w-px bg-border dark:bg-[#292a2f]" /> : null}
          </View>
          <View className="ml-3">
            <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>{entry.label}</Text>
            <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>{entry.date}</Text>
          </View>
        </View>
      ))}
    </View>
  );
}
