import type { ComponentProps } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { ScrollView, Text, View } from 'react-native';
import { zodResolver } from '@hookform/resolvers/zod';
import { useNavigation } from '@react-navigation/native';

import { useCreateCase } from '@/features/cases/hooks';
import { Button } from '@/shared/components/Button';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { useToast } from '@/shared/hooks';
import { ApiError } from '@/shared/types/api';
import { createCaseRequestSchema, type CreateCaseRequest } from '@/shared/api/endpoints/Cases';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const DEFAULT_VALUES: CreateCaseRequest = {
  caseNumber: '',
  clientFirstName: '',
  clientLastName: '',
  externalReference: '',
  title: '',
  clientDob: '',
  clientPhone: '',
  clientEmail: '',
  clientAddress: '',
  dateOfIncident: '',
  insuranceCarrier: '',
  policyNumber: '',
  claimNumber: '',
  description: '',
  notes: '',
};

function optional(value: string | undefined): string | undefined {
  return value?.trim() || undefined;
}

export function CreateCaseScreen() {
  const navigation = useNavigation();
  const toast = useToast();
  const createCase = useCreateCase();
  const { control, formState, handleSubmit, setError } = useForm<CreateCaseRequest>({
    defaultValues: DEFAULT_VALUES,
    resolver: zodResolver(createCaseRequestSchema),
  });

  async function submit(values: CreateCaseRequest) {
    try {
      const created = await createCase.mutateAsync({
        caseNumber: optional(values.caseNumber),
        clientFirstName: values.clientFirstName.trim(),
        clientLastName: values.clientLastName.trim(),
        externalReference: optional(values.externalReference),
        title: optional(values.title),
        clientDob: optional(values.clientDob),
        clientPhone: optional(values.clientPhone),
        clientEmail: optional(values.clientEmail),
        clientAddress: optional(values.clientAddress),
        dateOfIncident: optional(values.dateOfIncident),
        insuranceCarrier: optional(values.insuranceCarrier),
        policyNumber: optional(values.policyNumber),
        claimNumber: optional(values.claimNumber),
        description: optional(values.description),
        notes: optional(values.notes),
      });
      toast.showSuccess(`Case ${created.caseNumber} created`);
      navigation.goBack();
    } catch (error) {
      if (error instanceof ApiError && error.statusCode === 409) {
        setError('caseNumber', { message: 'A case with this number already exists' });
        return;
      }
      toast.showError(error instanceof Error ? error.message : 'Unable to create the case');
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Create Case" onBack={() => navigation.goBack()} />
      <ScrollView
        className="flex-1 px-5"
        contentContainerClassName="gap-4 pb-8 pt-4"
        keyboardShouldPersistTaps="handled"
      >
        <View className="mb-1">
          <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>
            Case Information
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#6f737d] dark:text-[#a1a1aa]')}>
            Add the client and incident details for this case.
          </Text>
        </View>
        <FormInput control={control} error={formState.errors.caseNumber?.message} label="Case Number" name="caseNumber" placeholder="Generated automatically if blank" />
        <FormInput control={control} error={formState.errors.clientFirstName?.message} label="Client First Name *" name="clientFirstName" />
        <FormInput control={control} error={formState.errors.clientLastName?.message} label="Client Last Name *" name="clientLastName" />
        <FormInput control={control} error={formState.errors.externalReference?.message} label="External Reference" name="externalReference" />
        <FormInput control={control} error={formState.errors.title?.message} label="Case Title" name="title" />
        <FormInput control={control} error={formState.errors.dateOfIncident?.message} label="Date of Loss" name="dateOfIncident" placeholder="YYYY-MM-DD" />
        <FormInput control={control} error={formState.errors.clientDob?.message} label="Client Date of Birth" name="clientDob" placeholder="YYYY-MM-DD" />
        <FormInput control={control} error={formState.errors.clientPhone?.message} keyboardType="phone-pad" label="Phone" name="clientPhone" />
        <FormInput autoCapitalize="none" control={control} error={formState.errors.clientEmail?.message} keyboardType="email-address" label="Email" name="clientEmail" />
        <FormInput control={control} error={formState.errors.clientAddress?.message} label="Address" name="clientAddress" />
        <FormInput control={control} error={formState.errors.insuranceCarrier?.message} label="Insurance Carrier" name="insuranceCarrier" />
        <FormInput control={control} error={formState.errors.policyNumber?.message} label="Policy Number" name="policyNumber" />
        <FormInput control={control} error={formState.errors.claimNumber?.message} label="Claim Number" name="claimNumber" />
        <FormInput control={control} error={formState.errors.description?.message} label="Description" multiline name="description" />
        <FormInput control={control} error={formState.errors.notes?.message} label="Notes" multiline name="notes" />
        <Button
          className="mt-2"
          label="Create Case"
          loading={createCase.isPending}
          onPress={handleSubmit(submit)}
        />
      </ScrollView>
    </View>
  );
}

function FormInput({
  control,
  error,
  label,
  name,
  ...inputProps
}: {
  control: ReturnType<typeof useForm<CreateCaseRequest>>['control'];
  error?: string;
  label: string;
  name: keyof CreateCaseRequest;
} & Omit<ComponentProps<typeof Input>, 'errorMessage' | 'label' | 'onChangeText' | 'value'>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field: { onBlur, onChange, value } }) => (
        <Input
          {...inputProps}
          errorMessage={error}
          label={label}
          value={value ?? ''}
          onBlur={onBlur}
          onChangeText={onChange}
        />
      )}
    />
  );
}
