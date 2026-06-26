import { useState } from 'react';
import { FlatList, View } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { OfferCard } from '@/features/offers/components';
import { useOfferActions, useOffers } from '@/features/offers/hooks';
import type { OfferDirection } from '@/features/offers/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Header } from '@/shared/components/Header';
import { Tabs } from '@/shared/components/Tabs';
import { useToast } from '@/shared/hooks';

export function OffersListScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const [direction, setDirection] = useState<OfferDirection>('received');
  const offers = useOffers(direction);
  const actions = useOfferActions();
  const toast = useToast();

  async function updateOffer(offerId: string, status: 'ACCEPTED' | 'DECLINED') {
    await actions.mutateAsync({ offerId, status });
    toast.showSuccess(status === 'ACCEPTED' ? 'Offer accepted' : 'Offer declined');
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header subtitle="Review active buyer and seller offers" title="Offers" />
      <Tabs
        activeTab={direction}
        tabs={[
          { id: 'received', label: 'Received' },
          { id: 'sent', label: 'Sent' },
        ]}
        onTabChange={(id) => setDirection(id as OfferDirection)}
      />
      <FlatList
        contentContainerClassName="gap-3 px-5 pb-6 pt-3"
        data={offers.offers}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <OfferCard
            direction={direction}
            offer={item}
            onAccept={() => updateOffer(item.id, 'ACCEPTED')}
            onDecline={() => updateOffer(item.id, 'DECLINED')}
            onPress={() => navigation.navigate('OfferDetail', { offerId: item.id })}
          />
        )}
      />
    </View>
  );
}
