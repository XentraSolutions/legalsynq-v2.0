import type { ComponentProps } from 'react';
import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { ScrollView, Text, View } from 'react-native';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';
import { useCreateFacility, useFacility, useUpdateFacility } from '../hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { FacilityRequest } from '@/shared/api/endpoints/Facilities';
import { Button, Header, Input, Spinner } from '@/shared/components';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const EMPTY: FacilityRequest = {
  name: '',
  code: '',
  externalReference: '',
  addressLine1: '',
  addressLine2: '',
  city: '',
  state: '',
  postalCode: '',
  phone: '',
  email: '',
  fax: '',
};
export function FacilityFormScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'FacilityForm'>>();
  const toast = useToast();
  const id = route.params?.facilityId;
  const query = useFacility(id);
  const create = useCreateFacility();
  const update = useUpdateFacility(id ?? '');
  const { control, formState, handleSubmit, reset } = useForm<FacilityRequest>({
    defaultValues: EMPTY,
  });
  useEffect(() => {
    if (query.data) {
      const f = query.data;
      reset({
        name: f.name,
        code: f.code ?? '',
        externalReference: f.externalReference ?? '',
        addressLine1: f.addressLine1 ?? '',
        addressLine2: f.addressLine2 ?? '',
        city: f.city ?? '',
        state: f.state ?? '',
        postalCode: f.postalCode ?? '',
        phone: f.phone ?? '',
        email: f.email ?? '',
        fax: f.fax ?? '',
      });
    }
  }, [query.data, reset]);
  async function submit(values: FacilityRequest) {
    try {
      const body = Object.fromEntries(
        Object.entries(values).map(([key, value]) => [key, value?.trim() || undefined])
      ) as unknown as FacilityRequest;
      const saved = id ? await update.mutateAsync(body) : await create.mutateAsync(body);
      toast.showSuccess(id ? 'Facility updated successfully' : 'Medical facility added');
      if (id) navigation.goBack();
      else navigation.navigate('FacilityDetail', { facilityId: saved.id });
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to save facility');
    }
  }
  if (id && query.isLoading)
    return (
      <View className="flex-1 items-center justify-center bg-[#fafafa]">
        <Spinner />
      </View>
    );
  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header
        showBack
        title={id ? 'Edit Medical Facility' : 'Add Medical Facility'}
        onBack={() => navigation.goBack()}
      />
      <ScrollView
        contentContainerClassName="gap-4 px-6 pb-10 pt-4"
        keyboardShouldPersistTaps="handled"
      >
        <Text className={cx(FIGMA_TEXT.body, 'text-[#71717a]')}>
          Add the facility information below.
        </Text>
        <Field
          control={control}
          error={formState.errors.name?.message}
          label="Facility Name *"
          name="name"
          rules={{ validate: (value) => Boolean(value?.trim()) || 'Facility name is required' }}
        />
        <Field control={control} label="Facility Code" name="code" />
        <Field
          control={control}
          label="Email"
          name="email"
          keyboardType="email-address"
          autoCapitalize="none"
        />
        <Field control={control} label="Phone" name="phone" keyboardType="phone-pad" />
        <Field control={control} label="Fax" name="fax" keyboardType="phone-pad" />
        <Field control={control} label="Street Address" name="addressLine1" />
        <Field control={control} label="Address Line 2" name="addressLine2" />
        <Field control={control} label="City" name="city" />
        <Field
          control={control}
          label="State"
          name="state"
          maxLength={2}
          autoCapitalize="characters"
        />
        <Field control={control} label="ZIP Code" name="postalCode" keyboardType="number-pad" />
        <Button
          label={id ? 'Save Changes' : 'Add Facility'}
          loading={create.isPending || update.isPending}
          onPress={handleSubmit(submit)}
        />
      </ScrollView>
    </View>
  );
}
function Field({
  control,
  error,
  label,
  name,
  rules,
  ...props
}: {
  control: ReturnType<typeof useForm<FacilityRequest>>['control'];
  error?: string;
  label: string;
  name: keyof FacilityRequest;
  rules?: { validate?: (value: string | undefined) => boolean | string };
} & Omit<ComponentProps<typeof Input>, 'errorMessage' | 'label' | 'onChangeText' | 'value'>) {
  return (
    <Controller
      control={control}
      name={name}
      rules={rules}
      render={({ field: { onBlur, onChange, value } }) => (
        <Input
          {...props}
          errorMessage={error}
          label={label}
          value={String(value ?? '')}
          onBlur={onBlur}
          onChangeText={onChange}
        />
      )}
    />
  );
}
