import { View } from 'react-native';
import { SearchBar } from '@/shared/components/SearchBar';
import { CircleIconButton } from './CircleIconButton';

export function ReportTopControls({
  isDark,
  searchPlaceholder,
  searchQuery,
  onOpenFilter,
  onOpenSort,
  onSearchChange,
}: {
  isDark: boolean;
  searchPlaceholder: string;
  searchQuery: string;
  onOpenFilter: () => void;
  onOpenSort: () => void;
  onSearchChange: (query: string) => void;
}) {
  return (
    <View className="flex-row items-center gap-3">
      <View className="flex-1">
        <SearchBar
          placeholder={searchPlaceholder}
          value={searchQuery}
          onChangeText={onSearchChange}
        />
      </View>
      <CircleIconButton
        accessibilityLabel="Filter"
        icon="options-outline"
        isDark={isDark}
        onPress={onOpenFilter}
      />
      <CircleIconButton
        accessibilityLabel="Sort"
        icon="swap-vertical-outline"
        isDark={isDark}
        onPress={onOpenSort}
      />
    </View>
  );
}
