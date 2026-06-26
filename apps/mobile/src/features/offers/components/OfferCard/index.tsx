import { Text, View } from 'react-native';

import { CASE_TYPE_LABELS, LIENS } from '@/features/mockData';
import { OfferStatusBadge } from '@/features/offers/components/OfferStatusBadge';
import type { OfferDirection } from '@/features/offers/types/types';
import type { Offer } from '@/shared/api/endpoints/Liens';
import { Avatar } from '@/shared/components/Avatar';
import { Button } from '@/shared/components/Button';
import { Card } from '@/shared/components/Card';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatDisplayDate, formatRelativeDate } from '@/shared/utils';

export interface OfferCardProps {
  offer: Offer;
  direction: OfferDirection;
  onPress?: () => void;
  onAccept?: () => void;
  onDecline?: () => void;
}

export function OfferCard({ offer, direction, onPress, onAccept, onDecline }: OfferCardProps) {
  const lien = LIENS.find((item) => item.id === offer.lienId) ?? LIENS[0];
  const pendingReceived = direction === 'received' && offer.status === 'PENDING';

  return (
    <Card onPress={onPress}>
      <View className="flex-row items-center justify-between">
        <OfferStatusBadge status={offer.status} />
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>{formatDisplayDate(offer.createdAt, 'MMM d')}</Text>
      </View>
      <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-3 text-[#202228] dark:text-white')}>
        Patient: {lien.patientName} - {CASE_TYPE_LABELS[lien.caseType]}
      </Text>
      <Text className="mt-1 font-jakarta-bold text-[20px] leading-[26px] text-[#f97332]">
        Offer: {formatCurrency(offer.offerAmount)}
      </Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
        Asking: {formatCurrency(lien.askingPrice ?? lien.lienAmount)}
      </Text>
      <View className="mt-3 flex-row items-center">
        <Avatar name={offer.buyerOrgName} size="sm" />
        <Text className={cx(FIGMA_TEXT.body, 'ml-2 flex-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
          {direction === 'received' ? 'From' : 'To'}: {offer.buyerOrgName}
        </Text>
        <Text className={cx(FIGMA_TEXT.microMeta, 'text-content-tertiary dark:text-[#8f929b]')}>Expires {formatRelativeDate(offer.expiresAt)}</Text>
      </View>
      {pendingReceived ? (
        <View className="mt-4 flex-row gap-2">
          <Button className="flex-1" label="Accept" size="sm" onPress={onAccept} />
          <Button className="flex-1" label="Decline" size="sm" variant="danger" onPress={onDecline} />
        </View>
      ) : null}
    </Card>
  );
}
