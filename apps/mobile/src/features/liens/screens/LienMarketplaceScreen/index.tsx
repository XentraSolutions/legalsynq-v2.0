import { useState } from 'react';
import { FlatList, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { LienCard, LienFilterBar, LIEN_FILTERS } from '@/features/liens/components';
import { useLienList } from '@/features/liens/hooks';
import type { LienFilter } from '@/features/liens/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { SearchBar } from '@/shared/components/SearchBar';
import { Skeleton } from '@/shared/components/Skeleton';

export function LienMarketplaceScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState<LienFilter>(LIEN_FILTERS[0]);
  const liensQuery = useLienList(activeFilter, search);

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header rightAction={<Ionicons color="#f97332" name="options-outline" size={24} />} subtitle="Browse buyer-side lien assets" title="Marketplace" />
      <View className="px-5 pt-3">
        <SearchBar placeholder="Search liens" value={search} onChangeText={setSearch} />
      </View>
      <LienFilterBar activeFilter={activeFilter} onFilterChange={setActiveFilter} />
      <View className="mb-2 flex-row items-center justify-between px-5">
        <Text className="font-jakarta-medium text-[12px] leading-[16px] text-[#6f737d] dark:text-[#a1a1aa]">{liensQuery.totalCount} results</Text>
        <Text className="font-jakarta-semibold text-[12px] leading-[16px] text-[#f97332]">Sort by: Amount down</Text>
      </View>
      {liensQuery.isLoading ? (
        <View className="gap-3 px-5">
          <Skeleton height={180} width="100%" />
          <Skeleton height={180} width="100%" />
        </View>
      ) : (
        <FlatList
          contentContainerClassName="gap-3 px-5 pb-6"
          data={liensQuery.liens}
          keyExtractor={(item) => item.id}
          ListEmptyComponent={
            <EmptyState
              description="Try another filter or search term."
              icon={<Ionicons color="#94a3b8" name="search" size={64} />}
              title="No liens found"
            />
          }
          renderItem={({ item }) => (
            <LienCard
              lien={item}
              onPress={() => navigation.navigate('LienDetail', { lienId: item.id })}
            />
          )}
          refreshing={liensQuery.isRefetching}
          onEndReached={() => {
            if (liensQuery.hasNextPage) {
              void liensQuery.fetchNextPage();
            }
          }}
          onRefresh={() => {
            void liensQuery.refetch();
          }}
        />
      )}
    </View>
  );
}
