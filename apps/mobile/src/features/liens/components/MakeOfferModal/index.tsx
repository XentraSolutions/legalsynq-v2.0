import { useState } from 'react';
import { Text, View } from 'react-native';

import { useMakeOffer } from '@/features/liens/hooks';
import { Button } from '@/shared/components/Button';
import { Chip } from '@/shared/components/Chip';
import { Input } from '@/shared/components/Input';
import { Modal } from '@/shared/components/Modal';
import { useToast } from '@/shared/hooks';
import { formatCurrency } from '@/shared/utils';

export interface MakeOfferModalProps {
  lienId: string;
  askingPrice: number;
  visible: boolean;
  onClose: () => void;
}

export function MakeOfferModal({ lienId, askingPrice, visible, onClose }: MakeOfferModalProps) {
  const [offerAmount, setOfferAmount] = useState(String(Math.round(askingPrice * 0.94)));
  const [notes, setNotes] = useState('');
  const [expiry, setExpiry] = useState('7d');
  const makeOffer = useMakeOffer();
  const toast = useToast();

  async function submitOffer() {
    await makeOffer.mutateAsync({ lienId, offerAmount: Number(offerAmount), notes });
    toast.showSuccess('Offer submitted');
    onClose();
  }

  return (
    <Modal
      footer={
        <View className="gap-2">
          <Button label="Submit Offer" loading={makeOffer.isPending} onPress={submitOffer} />
          <Button label="Cancel" variant="ghost" onPress={onClose} />
        </View>
      }
      title="Make an Offer"
      visible={visible}
      onClose={onClose}
    >
      <Text className="mb-4 text-sm text-content-secondary">
        Current asking price: {formatCurrency(askingPrice)}
      </Text>
      <Input
        keyboardType="numeric"
        label="Your Offer ($)"
        value={offerAmount}
        onChangeText={setOfferAmount}
      />
      <Input
        className="mt-4"
        label="Notes (optional)"
        multiline
        placeholder="Add context for the seller"
        value={notes}
        onChangeText={setNotes}
      />
      <Text className="mb-2 mt-4 text-sm font-medium text-content-secondary">Offer valid for</Text>
      <View className="flex-row gap-2">
        {['24h', '48h', '7d'].map((option) => (
          <Chip
            key={option}
            label={option}
            selected={expiry === option}
            onPress={() => setExpiry(option)}
          />
        ))}
      </View>
    </Modal>
  );
}
