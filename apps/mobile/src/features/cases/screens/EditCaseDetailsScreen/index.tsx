import { useEffect, useState } from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  Platform,
  Pressable,
  ScrollView,
  Text,
  View,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { useCaseDetail, useUpdateCaseDetails } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { FIGMA_TEXT, cx } from '@/shared/styles';

type DetailsRoute = NativeStackScreenProps<MainStackParamList, 'EditCaseDetails'>['route'];

const CASE_STATUS_OPTIONS = [
  { label: 'Pre-demand', value: 'PreDemand' },
  { label: 'Demand Sent', value: 'DemandSent' },
  { label: 'Negotiations', value: 'InNegotiation' },
  { label: 'Case Settled', value: 'CaseSettled' },
  { label: 'Closed', value: 'Closed' },
] as const;

export function EditCaseDetailsScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailsRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const updateDetails = useUpdateCaseDetails(route.params.caseId);
  const toast = useToast();
  const [status, setStatus] = useState('');
  const [dateOfLoss, setDateOfLoss] = useState('');
  const [notes, setNotes] = useState('');
  const [statusPickerVisible, setStatusPickerVisible] = useState(false);

  useEffect(() => {
    const item = caseQuery.data;
    if (!item) return;
    setStatus(item.status);
    setDateOfLoss(item.dateOfIncident ?? '');
    setNotes(item.notes ?? '');
  }, [caseQuery.data]);

  async function save() {
    if (!status.trim()) {
      toast.showError('Current status is required.');
      return;
    }

    try {
      await updateDetails.mutateAsync({
        primary: {
          status: status.trim(),
          dateOfLoss: dateOfLoss.trim() || undefined,
        },
        details: {
          notes: notes.trim(),
        },
      });
      toast.showSuccess('Case details updated');
      navigation.goBack();
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to update case details');
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Edit Case" onBack={() => navigation.goBack()} />
      {caseQuery.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      ) : caseQuery.isError || !caseQuery.data ? (
        <EmptyState
          actionLabel="Try Again"
          description="The case could not be loaded."
          title="Unable to edit case"
          onAction={() => void caseQuery.refetch()}
        />
      ) : (
        <KeyboardAvoidingView
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          className="flex-1"
        >
          <ScrollView
            className="flex-1"
            contentContainerClassName="px-6 pb-10"
            keyboardShouldPersistTaps="handled"
          >
            <Text className="mt-2 font-jakarta-bold text-[24px] leading-8 text-[#202228] dark:text-white">
              Edit Case Details
            </Text>
            <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
              Update the supported case details to keep the record accurate and up to date.
            </Text>

            <View className="mt-6 gap-5">
              <View>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#6f737d] dark:text-[#a1a1aa]')}>
                  Current Status *
                </Text>
                <Pressable
                  accessibilityLabel="Select current status"
                  accessibilityRole="button"
                  className="h-[52px] flex-row items-center rounded-[14px] border border-border bg-white px-4 dark:border-[#303138] dark:bg-[#191a1f]"
                  onPress={() => setStatusPickerVisible(true)}
                >
                  <Text className={cx(FIGMA_TEXT.input, 'flex-1 text-[#202228] dark:text-white')}>
                    {CASE_STATUS_OPTIONS.find((option) => option.value === status)?.label ?? status}
                  </Text>
                  <Ionicons color="#777a84" name="chevron-down" size={18} />
                </Pressable>
              </View>
              <Input
                autoCapitalize="none"
                label="Date of Loss"
                placeholder="YYYY-MM-DD"
                value={dateOfLoss}
                onChangeText={setDateOfLoss}
              />
              <Input
                multiline
                label="Notes"
                placeholder="Leave some notes here..."
                value={notes}
                onChangeText={setNotes}
              />
            </View>

            <Button
              className="mt-8"
              label="Continue"
              loading={updateDetails.isPending}
              onPress={() => void save()}
            />
            <Button
              className="mt-3"
              label="Cancel"
              variant="secondary"
              onPress={() => navigation.goBack()}
            />
          </ScrollView>
        </KeyboardAvoidingView>
      )}

      <Modal
        animationType="fade"
        transparent
        visible={statusPickerVisible}
        onRequestClose={() => setStatusPickerVisible(false)}
      >
        <View className="flex-1 items-center justify-center bg-black/40 px-6">
          <Pressable className="absolute inset-0" onPress={() => setStatusPickerVisible(false)} />
          <View className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              Current Status
            </Text>
            <View className="mt-4">
              {CASE_STATUS_OPTIONS.map((option) => (
                <Pressable
                  key={option.value}
                  accessibilityRole="button"
                  className="flex-row items-center border-b border-[#e4e4e7] py-4 dark:border-[#303138]"
                  onPress={() => {
                    setStatus(option.value);
                    setStatusPickerVisible(false);
                  }}
                >
                  <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#202228] dark:text-white')}>
                    {option.label}
                  </Text>
                  {option.value === status ? (
                    <Ionicons color="#ee7132" name="checkmark" size={20} />
                  ) : null}
                </Pressable>
              ))}
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}
