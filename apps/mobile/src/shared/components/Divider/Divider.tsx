import { Text, View } from 'react-native';

export interface DividerProps {
  orientation?: 'horizontal' | 'vertical';
  label?: string;
}

export function Divider({ orientation = 'horizontal', label }: DividerProps) {
  if (orientation === 'vertical') {
    return <View className="mx-2 h-full w-px bg-border dark:bg-[#292a2f]" />;
  }

  if (!label) {
    return <View className="my-2 h-px bg-border dark:bg-[#292a2f]" />;
  }

  return (
    <View className="my-2 flex-row items-center gap-3">
      <View className="h-px flex-1 bg-border dark:bg-[#292a2f]" />
      <Text className="text-sm text-content-tertiary dark:text-[#8f929b]">{label}</Text>
      <View className="h-px flex-1 bg-border dark:bg-[#292a2f]" />
    </View>
  );
}
