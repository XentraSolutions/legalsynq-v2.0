import { Controller, useForm } from 'react-hook-form';
import { ScrollView, Text, View } from 'react-native';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigation } from '@react-navigation/native';

import { CASE_TYPE_LABELS } from '@/features/mockData';
import { useSellLien } from '@/features/liens/hooks';
import { sellLienSchema, type SellLienFormValues } from '@/features/liens/validation';
import { Button } from '@/shared/components/Button';
import { Card } from '@/shared/components/Card';
import { Chip } from '@/shared/components/Chip';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import type { LienCaseType } from '@/shared/api/endpoints/Liens';
import { useState } from 'react';

export function SellLienScreen() {
  const navigation = useNavigation();
  const toast = useToast();
  const sellLien = useSellLien();
  const [step, setStep] = useState(1);
  const { control, handleSubmit, trigger, watch, setValue, formState } = useForm<SellLienFormValues>({
    defaultValues: {
      patientFirstName: '',
      patientLastName: '',
      caseType: 'AUTO_ACCIDENT',
      incidentDate: '',
      jurisdiction: '',
      caseReference: '',
      lienAmount: '',
      askingPrice: '',
      notes: '',
    },
    resolver: zodResolver(sellLienSchema),
  });

  async function next() {
    const fieldsByStep: Record<number, Array<keyof SellLienFormValues>> = {
      1: ['patientFirstName', 'patientLastName', 'caseType', 'incidentDate', 'jurisdiction'],
      2: ['lienAmount', 'askingPrice'],
      3: [],
      4: [],
    };
    const valid = await trigger(fieldsByStep[step]);
    if (valid) {
      setStep((current) => Math.min(current + 1, 4));
    }
  }

  async function submit(values: SellLienFormValues) {
    await sellLien.mutateAsync({
      patientName: `${values.patientFirstName} ${values.patientLastName[0] ?? ''}.`,
      caseType: values.caseType,
      jurisdiction: values.jurisdiction,
      lienAmount: Number(values.lienAmount),
      askingPrice: Number(values.askingPrice),
    });
    toast.showSuccess('Lien listing submitted');
    navigation.goBack();
  }

  const values = watch();

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Sell Lien" onBack={() => navigation.goBack()} />
      <ScrollView className="flex-1 px-5" contentContainerClassName="pb-8 pt-5">
        <View className="mb-5 flex-row gap-2">
          {[1, 2, 3, 4].map((item) => (
            <View
              className={`h-2 flex-1 rounded-full ${item <= step ? 'bg-[#f97332]' : 'bg-border dark:bg-[#292a2f]'}`}
              key={item}
            />
          ))}
        </View>
        {step === 1 ? (
          <View className="gap-4">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Case Information</Text>
            <Controller
              control={control}
              name="patientFirstName"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.patientFirstName?.message} label="Patient First Name" value={value} onChangeText={onChange} />
              )}
            />
            <Controller
              control={control}
              name="patientLastName"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.patientLastName?.message} label="Patient Last Name" value={value} onChangeText={onChange} />
              )}
            />
            <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#6f737d] dark:text-[#a1a1aa]')}>Case Type</Text>
            <View className="flex-row flex-wrap gap-2">
              {(Object.keys(CASE_TYPE_LABELS) as LienCaseType[]).map((caseType) => (
                <Chip
                  key={caseType}
                  label={CASE_TYPE_LABELS[caseType]}
                  selected={values.caseType === caseType}
                  onPress={() => setValue('caseType', caseType)}
                />
              ))}
            </View>
            <Controller
              control={control}
              name="incidentDate"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.incidentDate?.message} label="Incident Date" placeholder="MM/DD/YYYY" value={value} onChangeText={onChange} />
              )}
            />
            <Controller
              control={control}
              name="jurisdiction"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.jurisdiction?.message} label="Jurisdiction" placeholder="City, State" value={value} onChangeText={onChange} />
              )}
            />
            <Controller
              control={control}
              name="caseReference"
              render={({ field: { value, onChange } }) => (
                <Input label="Case Reference #" value={value} onChangeText={onChange} />
              )}
            />
          </View>
        ) : null}
        {step === 2 ? (
          <View className="gap-4">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Lien Details</Text>
            <Controller
              control={control}
              name="lienAmount"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.lienAmount?.message} keyboardType="numeric" label="Total Lien Amount ($)" value={value} onChangeText={onChange} />
              )}
            />
            <Controller
              control={control}
              name="askingPrice"
              render={({ field: { value, onChange } }) => (
                <Input errorMessage={formState.errors.askingPrice?.message} hint="Set your target sale price" keyboardType="numeric" label="Asking Price ($)" value={value} onChangeText={onChange} />
              )}
            />
            <Controller
              control={control}
              name="notes"
              render={({ field: { value, onChange } }) => (
                <Input label="Notes / Description" multiline value={value} onChangeText={onChange} />
              )}
            />
          </View>
        ) : null}
        {step === 3 ? (
          <View className="gap-4">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Documents</Text>
            <Text className={cx(FIGMA_TEXT.body, 'text-[#6f737d] dark:text-[#a1a1aa]')}>Attach supporting documents</Text>
            <View className="items-center justify-center rounded-[16px] border border-dashed border-[#f97332] bg-white p-8 dark:bg-[#191a1f]">
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#f97332]')}>+ Add Document</Text>
            </View>
            <Text className={cx(FIGMA_TEXT.formLabel, 'text-content-tertiary dark:text-[#8f929b]')}>No documents added yet</Text>
          </View>
        ) : null}
        {step === 4 ? (
          <View className="gap-4">
            <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>Review & Submit</Text>
            <Card>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
                {values.patientFirstName} {values.patientLastName}
              </Text>
              <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#6f737d] dark:text-[#a1a1aa]')}>{CASE_TYPE_LABELS[values.caseType]}</Text>
              <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>{values.jurisdiction}</Text>
              <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>Lien: ${values.lienAmount}</Text>
              <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>Ask: ${values.askingPrice}</Text>
            </Card>
          </View>
        ) : null}
      </ScrollView>
      <View className="flex-row gap-3 border-t border-border bg-white px-5 py-3 dark:border-[#292a2f] dark:bg-[#191a1f]">
        {step > 1 ? (
          <Button className="flex-1" label="Back" variant="ghost" onPress={() => setStep((current) => current - 1)} />
        ) : null}
        {step < 4 ? (
          <Button className="flex-1" label={step === 3 ? 'Review' : 'Next'} onPress={next} />
        ) : (
          <Button className="flex-1" label="Submit Listing" loading={sellLien.isPending} onPress={handleSubmit(submit)} />
        )}
      </View>
    </View>
  );
}
