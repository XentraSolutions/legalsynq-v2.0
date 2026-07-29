import { Pressable, TextInput, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { cx, FIGMA_TEXT } from '@/shared/styles';

export interface SearchBarProps {
  value: string;
  onChangeText: (value: string) => void;
  placeholder?: string;
  onSubmit?: () => void;
}

export function SearchBar({ value, onChangeText, placeholder = 'Search', onSubmit }: SearchBarProps) {
  return (
    <View className="h-11 flex-row items-center rounded-full bg-white px-4 shadow-sm dark:bg-[#191a1f]">
      <Ionicons color="#64748b" name="search" size={18} />
      <TextInput
        className={cx(FIGMA_TEXT.input, 'ml-2 flex-1 text-[#202228] dark:text-white')}
        placeholder={placeholder}
        placeholderTextColor="#94a3b8"
        returnKeyType="search"
        value={value}
        onChangeText={onChangeText}
        onSubmitEditing={onSubmit}
      />
      {value ? (
        <Pressable accessibilityRole="button" onPress={() => onChangeText('')} hitSlop={12}>
          <Ionicons color="#94a3b8" name="close-circle" size={18} />
        </Pressable>
      ) : null}
    </View>
  );
}
