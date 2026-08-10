import type { ComponentProps } from 'react';
import { useEffect } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';

import { useContact, useCreateContact, useUpdateContact } from '../hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { CreateContactRequest } from '@/shared/api/endpoints/Contacts';
import { Button, Header, Input } from '@/shared/components';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const TYPES = [
  ['LawFirm', 'Law Firm'],
  ['Provider', 'Medical Provider'],
  ['CaseManager', 'Case Manager'],
  ['LienHolder', 'Funding Company'],
  ['Lead', 'Lead'],
  ['InternalUser', 'Other'],
] as const;

const EMPTY: CreateContactRequest = {
  contactType: 'LawFirm',
  firstName: '',
  lastName: '',
  title: '',
  organization: '',
  email: '',
  phone: '',
  fax: '',
  website: '',
  addressLine1: '',
  city: '',
  state: '',
  postalCode: '',
  notes: '',
};

export function ContactFormScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'ContactForm'>>();
  const toast = useToast();
  const id = route.params?.contactId;
  const contactQuery = useContact(id);
  const create = useCreateContact();
  const update = useUpdateContact(id ?? '');
  const { control, formState, handleSubmit, reset, watch, setValue } =
    useForm<CreateContactRequest>({
      defaultValues: { ...EMPTY, contactType: route.params?.contactType ?? EMPTY.contactType },
    });
  const selectedType = watch('contactType');

  useEffect(() => {
    if (contactQuery.data) {
      const contact = contactQuery.data;
      reset({
        contactType: contact.contactType,
        firstName: contact.firstName,
        lastName: contact.lastName,
        title: contact.title ?? '',
        organization: contact.organization ?? '',
        email: contact.email ?? '',
        phone: contact.phone ?? '',
        fax: contact.fax ?? '',
        website: contact.website ?? '',
        addressLine1: contact.addressLine1 ?? '',
        city: contact.city ?? '',
        state: contact.state ?? '',
        postalCode: contact.postalCode ?? '',
        notes: contact.notes ?? '',
      });
    }
  }, [contactQuery.data, reset]);

  async function submit(values: CreateContactRequest) {
    try {
      const payload = Object.fromEntries(
        Object.entries(values).map(([key, value]) => [
          key,
          typeof value === 'string' ? value.trim() : value,
        ])
      ) as unknown as CreateContactRequest;
      const saved = id ? await update.mutateAsync(payload) : await create.mutateAsync(payload);
      toast.showSuccess(id ? 'Contact updated successfully' : 'New contact added');
      if (id) navigation.goBack();
      else navigation.navigate('ContactDetail', { contactId: saved.id });
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to save contact');
    }
  }

  if (id && contactQuery.isLoading) {
    return (
      <View className="flex-1 items-center justify-center bg-[#fafafa] dark:bg-[#050506]">
        <Text className={cx(FIGMA_TEXT.body, 'text-[#71717a]')}>Loading contact…</Text>
      </View>
    );
  }

  if (id && contactQuery.isError) {
    return (
      <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
        <Header showBack title="Edit Contact" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center px-6">
          <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#18181b] dark:text-white')}>
            Unable to load contact
          </Text>
          <Button className="mt-4" label="Try Again" onPress={() => void contactQuery.refetch()} />
        </View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header
        showBack
        title={id ? 'Edit Contact' : 'Add New Contact'}
        onBack={() => navigation.goBack()}
      />
      <ScrollView
        contentContainerClassName="gap-4 px-6 pb-10 pt-4"
        keyboardShouldPersistTaps="handled"
      >
        <Text className={cx(FIGMA_TEXT.body, 'text-[#71717a]')}>
          {id
            ? 'Update the contact information below.'
            : 'Add contact information to your directory.'}
        </Text>
        <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#71717a]')}>Contact Type</Text>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerClassName="gap-2"
        >
          {TYPES.map(([value, label]) => (
            <Pressable
              key={value}
              className={cx(
                'h-9 flex-row items-center gap-2 rounded-full border px-3',
                selectedType === value
                  ? 'border-[#ee7132] bg-[#fff2eb]'
                  : 'border-[#dedee0] bg-white dark:bg-[#191a1f]'
              )}
              onPress={() => setValue('contactType', value)}
            >
              <Ionicons
                color={selectedType === value ? '#a95024' : '#71717a'}
                name={selectedType === value ? 'checkmark-circle' : 'ellipse-outline'}
                size={16}
              />
              <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#18181b] dark:text-white')}>
                {label}
              </Text>
            </Pressable>
          ))}
        </ScrollView>
        <FormInput
          control={control}
          error={formState.errors.firstName?.message}
          label="First Name *"
          name="firstName"
          rules={{ validate: (value) => Boolean(value?.trim()) || 'First name is required' }}
        />
        <FormInput
          control={control}
          error={formState.errors.lastName?.message}
          label="Last Name *"
          name="lastName"
          rules={{ validate: (value) => Boolean(value?.trim()) || 'Last name is required' }}
        />
        <FormInput control={control} label="Job Title" name="title" />
        <FormInput control={control} label="Organization" name="organization" />
        <FormInput
          autoCapitalize="none"
          control={control}
          keyboardType="email-address"
          label="Email"
          name="email"
        />
        <FormInput control={control} keyboardType="phone-pad" label="Phone" name="phone" />
        <FormInput control={control} keyboardType="phone-pad" label="Fax" name="fax" />
        <FormInput
          autoCapitalize="none"
          control={control}
          keyboardType="url"
          label="Website"
          name="website"
        />
        <FormInput control={control} label="Street Address" name="addressLine1" />
        <FormInput control={control} label="City" name="city" />
        <FormInput
          autoCapitalize="characters"
          control={control}
          label="State"
          maxLength={2}
          name="state"
        />
        <FormInput control={control} keyboardType="number-pad" label="ZIP Code" name="postalCode" />
        <FormInput control={control} label="Notes" multiline name="notes" />
        <Button
          label={id ? 'Save Changes' : 'Add Contact'}
          loading={create.isPending || update.isPending}
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
  rules,
  ...props
}: {
  control: ReturnType<typeof useForm<CreateContactRequest>>['control'];
  error?: string;
  label: string;
  name: keyof CreateContactRequest;
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
