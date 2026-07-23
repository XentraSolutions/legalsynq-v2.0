import { useState } from 'react';
import { Linking, Pressable, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { useCaseDetail, usePayoffQuote } from '@/features/cases/hooks';
import { PayoffQuoteService } from '@/features/cases/services/payoffQuoteService';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { FIGMA_TEXT, SHADOWS, cx } from '@/shared/styles';

type PayoffRoute = NativeStackScreenProps<MainStackParamList, 'PayoffQuote'>['route'];

export function PayoffQuoteScreen() {
  const navigation = useNavigation();
  const route = useRoute<PayoffRoute>();
  const quoteQuery = usePayoffQuote(route.params.caseId);
  const caseQuery = useCaseDetail(route.params.caseId);
  const toast = useToast();
  const [sharing, setSharing] = useState(false);

  async function shareQuote() {
    if (!quoteQuery.data) return;
    setSharing(true);
    try {
      await PayoffQuoteService.share(
        quoteQuery.data.url,
        caseQuery.data?.caseNumber ?? route.params.caseId
      );
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to share the payoff quote');
    } finally {
      setSharing(false);
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header
        showBack
        title="Payoff Quote"
        onBack={() => navigation.goBack()}
        rightAction={
          quoteQuery.data ? (
            <Pressable
              accessibilityLabel="Share payoff quote"
              accessibilityRole="button"
              className="h-9 w-9 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
              disabled={sharing}
              onPress={() => void shareQuote()}
            >
              {sharing ? <Spinner /> : <Ionicons color="#6f737d" name="share-outline" size={20} />}
            </Pressable>
          ) : null
        }
      />

      {quoteQuery.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      ) : quoteQuery.isError || !quoteQuery.data ? (
        <EmptyState
          actionLabel="Try Again"
          description={
            quoteQuery.error instanceof Error
              ? quoteQuery.error.message
              : 'A payoff quote is not available for this case.'
          }
          icon={<Ionicons color="#f97332" name="document-text-outline" size={58} />}
          title="Payoff quote unavailable"
          onAction={() => void quoteQuery.refetch()}
        />
      ) : (
        <View className="flex-1 px-6 pb-8 pt-4">
          <View
            className="flex-1 items-center justify-center rounded-[20px] bg-white px-8 dark:bg-[#191a1f]"
            style={SHADOWS.sm}
          >
            <View className="h-20 w-20 items-center justify-center rounded-full bg-[#fff0e8]">
              <Ionicons color="#ee7132" name="document-text-outline" size={40} />
            </View>
            <Text
              className={cx(FIGMA_TEXT.sectionTitle, 'mt-5 text-center text-[#202228] dark:text-white')}
            >
              Payoff Quote
            </Text>
            <Text
              className={cx(FIGMA_TEXT.body, 'mt-2 text-center text-[#777a84] dark:text-[#a1a1aa]')}
            >
              The payoff statement is ready to view or share as a PDF document.
            </Text>
            <Button
              className="mt-8 w-full"
              label="Open Payoff Quote"
              leftIcon={<Ionicons color="#111111" name="open-outline" size={18} />}
              onPress={() => void Linking.openURL(quoteQuery.data.url)}
            />
            <Button
              className="mt-3 w-full"
              label="Share"
              loading={sharing}
              variant="secondary"
              onPress={() => void shareQuote()}
            />
          </View>
        </View>
      )}
    </View>
  );
}
