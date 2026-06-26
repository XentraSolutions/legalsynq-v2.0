import { ScrollView, Text, View } from 'react-native';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CASE_TYPE_LABELS, LIENS } from '@/features/mockData';
import { OfferStatusBadge } from '@/features/offers/components';
import { useOfferActions, useOfferDetail } from '@/features/offers/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Avatar } from '@/shared/components/Avatar';
import { Button } from '@/shared/components/Button';
import { Card } from '@/shared/components/Card';
import { Divider } from '@/shared/components/Divider';
import { Header } from '@/shared/components/Header';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

export function OfferDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'OfferDetail'>['route']>();
  const offerQuery = useOfferDetail(route.params.offerId);
  const actions = useOfferActions();
  const toast = useToast();
  const offer = offerQuery.data;
  const lien = offer ? LIENS.find((item) => item.id === offer.lienId) ?? LIENS[0] : undefined;

  async function updateOffer(status: 'ACCEPTED' | 'DECLINED') {
    if (!offer) {
      return;
    }
    await actions.mutateAsync({ offerId: offer.id, status });
    toast.showSuccess(status === 'ACCEPTED' ? 'Offer accepted' : 'Offer declined');
    navigation.goBack();
  }

  if (!offer || !lien) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Offer Details" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Offer Details" onBack={() => navigation.goBack()} />
      <ScrollView className="flex-1" contentContainerClassName="pb-28">
        <Card className="mx-5 mt-4">
          <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#6f737d] dark:text-[#a1a1aa]')}>Offer Amount</Text>
          <Text className="mt-1 font-jakarta-bold text-[28px] leading-[34px] text-[#f97332]">
            {formatCurrency(offer.offerAmount)}
          </Text>
          <View className="mt-3">
            <OfferStatusBadge status={offer.status} />
          </View>
          <Divider />
          {[
            ['Lien reference', lien.caseReference],
            ['Patient', lien.patientName],
            ['Case Type', CASE_TYPE_LABELS[lien.caseType]],
            ['Asking Price', formatCurrency(lien.askingPrice ?? lien.lienAmount)],
            ['Submitted', formatDisplayDate(offer.createdAt)],
            ['Expires', formatDisplayDate(offer.expiresAt)],
          ].map(([label, value]) => (
            <View className="mt-3 flex-row justify-between gap-4" key={label}>
              <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>{label}</Text>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'flex-1 text-right text-[#202228] dark:text-white')}>{value}</Text>
            </View>
          ))}
          <Divider />
          <View className="flex-row items-center">
            <Avatar name={offer.buyerOrgName} size="md" />
            <View className="ml-3">
              <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>Submitted by</Text>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>{offer.buyerOrgName}</Text>
            </View>
          </View>
        </Card>
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mx-5 mt-4 text-[#202228] dark:text-white')}>Notes</Text>
        <Text className={cx(FIGMA_TEXT.body, 'mx-5 mt-2 text-[#6f737d] dark:text-[#a1a1aa]')}>
          {offer.notes ?? 'No notes were included with this offer.'}
        </Text>
      </ScrollView>
      {offer.status === 'PENDING' ? (
        <View className="absolute bottom-0 left-0 right-0 flex-row gap-3 border-t border-border bg-white px-5 py-3 dark:border-[#292a2f] dark:bg-[#191a1f]">
          <Button className="flex-1" label="Accept Offer" onPress={() => updateOffer('ACCEPTED')} />
          <Button className="flex-1" label="Decline" variant="danger" onPress={() => updateOffer('DECLINED')} />
        </View>
      ) : null}
    </View>
  );
}
