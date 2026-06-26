import { useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { CASE_TYPE_LABELS, STATUS_LABELS } from '@/features/mockData';
import { LienStatusBadge, LienTimeline, MakeOfferModal } from '@/features/liens/components';
import { useLienDetail } from '@/features/liens/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { Chip } from '@/shared/components/Chip';
import { Divider } from '@/shared/components/Divider';
import { Header } from '@/shared/components/Header';
import { Spinner } from '@/shared/components/Spinner';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency, formatDisplayDate } from '@/shared/utils';

export function LienDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'LienDetail'>['route']>();
  const { data: lien, isLoading } = useLienDetail(route.params.lienId);
  const [offerVisible, setOfferVisible] = useState(false);

  if (isLoading || !lien) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Lien Details" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Lien Details" onBack={() => navigation.goBack()} />
      <ScrollView className="flex-1 px-5" contentContainerClassName="pb-28 pt-4">
        <View className="flex-row items-center justify-between">
          <View className="flex-row items-center gap-2">
            <LienStatusBadge status={lien.status} />
            <Chip label={CASE_TYPE_LABELS[lien.caseType]} />
          </View>
        </View>
        <Text className="mt-3 font-jakarta-bold text-[28px] leading-[34px] text-[#f97332]">
          {formatCurrency(lien.askingPrice ?? lien.lienAmount)}
        </Text>
        <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>
          Lien Amount: {formatCurrency(lien.lienAmount)}
        </Text>
        <Divider />
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-[#202228] dark:text-white')}>Details</Text>
        <View className="mt-4 flex-row flex-wrap gap-y-4">
          {[
            ['Jurisdiction', lien.jurisdiction],
            ['Incident Date', formatDisplayDate(lien.incidentDate, 'MM/dd/yyyy')],
            ['Case Type', CASE_TYPE_LABELS[lien.caseType]],
            ['Listed Date', lien.listedAt ? formatDisplayDate(lien.listedAt) : 'Not listed'],
            ['Seller Org', lien.sellerOrgName],
            ['Offer Count', `${lien.offerCount} offers`],
          ].map(([label, value]) => (
            <View className="w-1/2" key={label}>
              <Text className={cx(FIGMA_TEXT.formLabel, 'text-content-tertiary dark:text-[#8f929b]')}>{label}</Text>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'mt-1 text-[#202228] dark:text-white')}>{value}</Text>
            </View>
          ))}
        </View>
        <Divider />
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-[#202228] dark:text-white')}>History</Text>
        <View className="mt-3">
          <LienTimeline
            entries={[
              { label: STATUS_LABELS[lien.status], date: formatDisplayDate(lien.updatedAt) },
              { label: 'Draft', date: formatDisplayDate(lien.createdAt) },
            ]}
          />
        </View>
        <Divider />
        <Text className={cx(FIGMA_TEXT.sectionTitle, 'mt-4 text-[#202228] dark:text-white')}>Attached Documents</Text>
        <View className="mt-3 gap-3">
          {lien.documents.map((document) => (
            <View className="flex-row items-center rounded-[14px] border border-border bg-white p-3 dark:border-[#303138] dark:bg-[#191a1f]" key={document.id}>
              <Ionicons color="#f97332" name="document-text-outline" size={22} />
              <Text className={cx(FIGMA_TEXT.body, 'ml-3 flex-1 text-[#202228] dark:text-white')}>{document.filename}</Text>
              <Text className={cx(FIGMA_TEXT.rowValue, 'text-[#f97332]')}>Download</Text>
            </View>
          ))}
        </View>
      </ScrollView>
      <View className="absolute bottom-0 left-0 right-0 flex-row gap-3 border-t border-border bg-white px-5 py-3 dark:border-[#292a2f] dark:bg-[#191a1f]">
        <Button className="flex-1" label="Make an Offer" onPress={() => setOfferVisible(true)} />
        <Button className="w-1/3" label="Contact" variant="secondary" />
      </View>
      <MakeOfferModal
        askingPrice={lien.askingPrice ?? lien.lienAmount}
        lienId={lien.id}
        visible={offerVisible}
        onClose={() => setOfferVisible(false)}
      />
    </View>
  );
}
