import { useState } from 'react';
import { FlatList, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { CaseCard } from '@/features/cases/components';
import { useCases } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Header } from '@/shared/components/Header';
import { SearchBar } from '@/shared/components/SearchBar';

export function CasesListScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [search, setSearch] = useState('');
  const cases = useCases(search);

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header subtitle="Track case volume and lien activity" title="Cases" />
      <View className="px-5 pt-3">
        <SearchBar placeholder="Search cases" value={search} onChangeText={setSearch} />
      </View>
      <FlatList
        contentContainerClassName="gap-3 px-5 pb-6 pt-3"
        data={cases.cases}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <CaseCard caseItem={item} onPress={() => navigation.navigate('CaseDetail', { caseId: item.id })} />
        )}
      />
    </View>
  );
}
