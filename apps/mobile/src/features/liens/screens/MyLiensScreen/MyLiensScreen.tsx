import { useMemo, useState } from 'react';
import { FlatList, Pressable, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { DEMO_USER } from '@/features/mockData';
import { LienCard } from '@/features/liens/components';
import { useLienList } from '@/features/liens/hooks';
import type { LienFilter } from '@/features/liens/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { Header } from '@/shared/components/Header';
import { Tabs } from '@/shared/components/Tabs';

export function MyLiensScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [activeTab, setActiveTab] = useState('all');
  const filter = useMemo<LienFilter>(
    () => ({
      id: activeTab,
      label: activeTab,
      status: activeTab === 'all' ? undefined : (activeTab.toUpperCase() as LienFilter['status']),
    }),
    [activeTab]
  );
  const liensQuery = useLienList(filter, '');
  const myLiens = liensQuery.liens.filter((lien) => lien.sellerId === DEMO_USER.id);

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header
        rightAction={
          <Button label="+ Sell" size="sm" variant="secondary" onPress={() => navigation.navigate('SellLien')} />
        }
        subtitle="Manage seller lien listings"
        title="My Liens"
      />
      <Tabs
        activeTab={activeTab}
        tabs={[
          { id: 'all', label: 'All' },
          { id: 'available', label: 'Available' },
          { id: 'pending', label: 'Pending' },
          { id: 'sold', label: 'Sold' },
          { id: 'draft', label: 'Draft' },
        ]}
        onTabChange={setActiveTab}
      />
      <FlatList
        contentContainerClassName="gap-3 px-5 pb-24 pt-3"
        data={myLiens}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <LienCard
            actionLabel="Manage"
            lien={item}
            onPress={() => navigation.navigate('LienDetail', { lienId: item.id })}
          />
        )}
      />
      <Pressable
        accessibilityRole="button"
        className="absolute bottom-6 right-5 h-14 w-14 items-center justify-center rounded-full bg-[#f97332] shadow-lg"
        onPress={() => navigation.navigate('SellLien')}
      >
        <Ionicons color="#ffffff" name="add" size={28} />
      </Pressable>
    </View>
  );
}
