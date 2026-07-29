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
import DateTimePicker from '@react-native-community/datetimepicker';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import {
  useCaseDetail,
  useCaseTrackingOptions,
  useUpdateCaseDetails,
} from '@/features/cases/hooks';
import {
  mergeCaseTrackingMetadata,
  parseCaseTrackingMetadata,
} from '@/features/cases/utils/caseTrackingMetadata';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import {
  SelectOptionModal,
  type SelectOptionItem,
} from '@/shared/components/SelectOptionModal';
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

type PickerField = 'status' | 'medicalStatus' | 'caseType' | 'state' | 'lead';
type DateField = 'trackingFollowUp' | 'dateOfLoss';

function parseIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  if (!year || !month || !day) return new Date();
  return new Date(year, month - 1, day);
}

function formatIsoDate(value: Date): string {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function formatDisplayDate(value: string): string {
  if (!value) return 'mm / dd / yyyy';
  const [year, month, day] = value.split('-');
  return year && month && day ? `${month} / ${day} / ${year}` : value;
}

export function EditCaseDetailsScreen() {
  const navigation = useNavigation();
  const route = useRoute<DetailsRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const trackingOptions = useCaseTrackingOptions();
  const updateDetails = useUpdateCaseDetails(route.params.caseId);
  const toast = useToast();
  const [status, setStatus] = useState('');
  const [medicalStatus, setMedicalStatus] = useState('');
  const [caseType, setCaseType] = useState('');
  const [stateOfIncident, setStateOfIncident] = useState('');
  const [trackingFollowUp, setTrackingFollowUp] = useState('');
  const [dateOfLoss, setDateOfLoss] = useState('');
  const [leadId, setLeadId] = useState('');
  const [lead, setLead] = useState('');
  const [description, setDescription] = useState('');
  const [pickerField, setPickerField] = useState<PickerField | null>(null);
  const [dateField, setDateField] = useState<DateField | null>(null);

  useEffect(() => {
    const item = caseQuery.data;
    if (!item) return;
    const tracking = parseCaseTrackingMetadata(item.notes);
    setStatus(item.status);
    setMedicalStatus(tracking.currentMedicalStatus);
    setCaseType(item.accidentType || tracking.accidentType);
    setStateOfIncident(item.stateOfIncident || tracking.stateOfIncident);
    setTrackingFollowUp(tracking.trackingFollowUpDate);
    setDateOfLoss(item.dateOfIncident ?? '');
    setLeadId(tracking.leadId);
    setLead(tracking.lead);
    setDescription(item.description ?? '');
  }, [caseQuery.data]);

  const pickerConfig: Record<
    PickerField,
    { title: string; value: string; selectedLabel?: string; options: SelectOptionItem[] }
  > = {
    status: {
      title: 'Current Status',
      value: status,
      options: [...CASE_STATUS_OPTIONS],
    },
    medicalStatus: {
      title: 'Current Medical Status',
      value: medicalStatus,
      options: (trackingOptions.data?.medicalStatuses ?? []).map((item) => ({
        label: item.name,
        value: item.name,
      })),
    },
    caseType: {
      title: 'Case Type',
      value: caseType,
      options: (trackingOptions.data?.caseTypes ?? []).map((item) => ({
        label: item.name,
        value: item.name,
      })),
    },
    state: {
      title: 'State of Incident',
      value: stateOfIncident,
      options: (trackingOptions.data?.states ?? []).map((item) => ({
        label: item.code ? `${item.name} (${item.code})` : item.name,
        value: item.code || item.name,
      })),
    },
    lead: {
      title: 'Lead',
      value: leadId,
      selectedLabel: lead,
      options: (trackingOptions.data?.leads ?? []).map((item) => ({
        label: item.displayName,
        value: item.id,
      })),
    },
  };

  const selectedPicker = pickerField ? pickerConfig[pickerField] : null;

  function openPicker(field: PickerField) {
    setPickerField(field);
  }

  function closePicker() {
    setPickerField(null);
  }

  function selectOption(option: SelectOptionItem) {
    switch (pickerField) {
      case 'status':
        setStatus(option.value);
        break;
      case 'medicalStatus':
        setMedicalStatus(option.value);
        break;
      case 'caseType':
        setCaseType(option.value);
        break;
      case 'state':
        setStateOfIncident(option.value);
        break;
      case 'lead':
        setLeadId(option.value);
        setLead(option.label);
        break;
    }
    closePicker();
  }

  async function save() {
    const caseItem = caseQuery.data;
    if (!caseItem) return;

    if (!status.trim()) {
      toast.showError('Current status is required.');
      return;
    }
    if (!caseType.trim() || !stateOfIncident.trim()) {
      toast.showError('Case type and state of incident are required.');
      return;
    }

    try {
      await updateDetails.mutateAsync({
        primary: {
          status: status.trim(),
          dateOfLoss: dateOfLoss.trim() || undefined,
        },
        details: {
          description: description.trim(),
          notes: mergeCaseTrackingMetadata(caseItem.notes, {
            accidentType: caseType,
            currentMedicalStatus: medicalStatus,
            lead,
            leadId,
            stateOfIncident,
            trackingFollowUpDate: trackingFollowUp,
          }),
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
              Update the case details to keep the record accurate and up to date.
            </Text>

            <View className="mt-6 gap-5">
              <View>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                  Current Status *
                </Text>
                <Pressable
                  accessibilityLabel="Select current status"
                  accessibilityRole="button"
                  className="h-10 flex-row items-center rounded-xl border border-transparent bg-white px-3 dark:bg-[#191a1f]"
                  onPress={() => openPicker('status')}
                >
                  <Text className={cx(FIGMA_TEXT.input, 'flex-1 text-[#202228] dark:text-white')}>
                    {CASE_STATUS_OPTIONS.find((option) => option.value === status)?.label ?? status}
                  </Text>
                  <Ionicons color="#777a84" name="chevron-down" size={18} />
                </Pressable>
              </View>
              {(
                [
                  {
                    field: 'medicalStatus',
                    label: 'Current Medical Status',
                    placeholder: 'Select medical status',
                    value: medicalStatus,
                  },
                  {
                    field: 'caseType',
                    label: 'Case Type *',
                    placeholder: 'Select case type',
                    value: caseType,
                  },
                  {
                    field: 'state',
                    label: 'State of Incident *',
                    placeholder: 'Select state',
                    value: stateOfIncident,
                  },
                ] as const
              ).map((field) => (
                <View key={field.field}>
                  <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                    {field.label}
                  </Text>
                  <Pressable
                    accessibilityLabel={`Select ${field.label.replace(' *', '').toLowerCase()}`}
                    accessibilityRole="button"
                    className="h-10 flex-row items-center rounded-xl bg-white px-3 dark:bg-[#191a1f]"
                    onPress={() => openPicker(field.field)}
                  >
                    <Text
                      className={cx(
                        FIGMA_TEXT.input,
                        'flex-1',
                        field.value
                          ? 'text-[#202228] dark:text-white'
                          : 'text-[#777a84] dark:text-[#a1a1aa]'
                      )}
                    >
                      {field.value || field.placeholder}
                    </Text>
                    <Ionicons color="#777a84" name="chevron-down" size={17} />
                  </Pressable>
                </View>
              ))}
              <View>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                  Tracking Follow Up
                </Text>
                <Pressable
                  accessibilityLabel="Select tracking follow up"
                  accessibilityRole="button"
                  className="h-10 flex-row items-center rounded-xl bg-white px-3 dark:bg-[#191a1f]"
                  onPress={() => setDateField('trackingFollowUp')}
                >
                  <Text
                    className={cx(
                      FIGMA_TEXT.input,
                      'flex-1',
                      trackingFollowUp
                        ? 'text-[#202228] dark:text-white'
                        : 'text-[#777a84] dark:text-[#a1a1aa]'
                    )}
                  >
                    {formatDisplayDate(trackingFollowUp)}
                  </Text>
                  <Ionicons color="#777a84" name="calendar-clear-outline" size={17} />
                </Pressable>
              </View>
              <View>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                  Date of Loss
                </Text>
                <Pressable
                  accessibilityLabel="Select date of loss"
                  accessibilityRole="button"
                  className="h-10 flex-row items-center rounded-xl bg-white px-3 dark:bg-[#191a1f]"
                  onPress={() => setDateField('dateOfLoss')}
                >
                  <Text
                    className={cx(
                      FIGMA_TEXT.input,
                      'flex-1',
                      dateOfLoss
                        ? 'text-[#202228] dark:text-white'
                        : 'text-[#777a84] dark:text-[#a1a1aa]'
                    )}
                  >
                    {formatDisplayDate(dateOfLoss)}
                  </Text>
                  <Ionicons color="#777a84" name="calendar-clear-outline" size={17} />
                </Pressable>
              </View>
              <View>
                <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#202228] dark:text-white')}>
                  Lead
                </Text>
                <Pressable
                  accessibilityLabel="Select lead"
                  accessibilityRole="button"
                  className="h-10 flex-row items-center rounded-xl bg-white px-3 dark:bg-[#191a1f]"
                  onPress={() => openPicker('lead')}
                >
                  <Text
                    className={cx(
                      FIGMA_TEXT.input,
                      'flex-1',
                      lead
                        ? 'text-[#202228] dark:text-white'
                        : 'text-[#777a84] dark:text-[#a1a1aa]'
                    )}
                  >
                    {lead || 'Select lead'}
                  </Text>
                  <Ionicons color="#777a84" name="chevron-down" size={17} />
                </Pressable>
              </View>
              <Input
                multiline
                label="Notes"
                placeholder="Leave some notes here..."
                value={description}
                onChangeText={setDescription}
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

      <SelectOptionModal
        options={selectedPicker?.options ?? []}
        selectedLabel={selectedPicker?.selectedLabel}
        selectedValue={selectedPicker?.value}
        title={selectedPicker?.title ?? 'Select option'}
        visible={pickerField !== null}
        onClose={closePicker}
        onSelect={selectOption}
      />

      <Modal
        animationType="fade"
        transparent
        visible={dateField !== null}
        onRequestClose={() => setDateField(null)}
      >
        <View className="flex-1 items-center justify-center bg-black/40 px-6">
          <Pressable className="absolute inset-0" onPress={() => setDateField(null)} />
          <View className="w-full rounded-[20px] bg-white p-6 dark:bg-[#191a1f]">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
              {dateField === 'trackingFollowUp' ? 'Tracking Follow Up' : 'Date of Loss'}
            </Text>
            <DateTimePicker
              display={Platform.OS === 'ios' ? 'spinner' : 'calendar'}
              mode="date"
              value={parseIsoDate(
                dateField === 'trackingFollowUp' ? trackingFollowUp : dateOfLoss
              )}
              onChange={(_, selectedDate) => {
                if (selectedDate) {
                  if (dateField === 'trackingFollowUp') {
                    setTrackingFollowUp(formatIsoDate(selectedDate));
                  } else {
                    setDateOfLoss(formatIsoDate(selectedDate));
                  }
                }
                if (Platform.OS !== 'ios') setDateField(null);
              }}
            />
            {Platform.OS === 'ios' ? (
              <Button
                className="mt-3"
                label="Done"
                size="sm"
                onPress={() => setDateField(null)}
              />
            ) : null}
          </View>
        </View>
      </Modal>
    </View>
  );
}
