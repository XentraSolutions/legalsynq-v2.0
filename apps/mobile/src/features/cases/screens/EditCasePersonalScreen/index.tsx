import { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, View } from 'react-native';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';

import { useCaseDetail, useUpdatePersonalInfo } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { FIGMA_TEXT, cx } from '@/shared/styles';

type PersonalRoute = NativeStackScreenProps<MainStackParamList, 'EditCasePersonal'>['route'];

export function EditCasePersonalScreen() {
  const navigation = useNavigation();
  const route = useRoute<PersonalRoute>();
  const caseQuery = useCaseDetail(route.params.caseId);
  const updatePersonal = useUpdatePersonalInfo(route.params.caseId);
  const toast = useToast();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [dob, setDob] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [city, setCity] = useState('');
  const [state, setState] = useState('');
  const [zipcode, setZipcode] = useState('');

  useEffect(() => {
    const item = caseQuery.data;
    if (!item) return;
    setFirstName(item.clientFirstName);
    setLastName(item.clientLastName);
    setDob(item.clientDob ?? '');
    setPhone(item.clientPhone ?? '');
    setEmail(item.clientEmail ?? '');
    setAddress(item.clientAddress ?? '');
  }, [caseQuery.data]);

  async function save() {
    if (!firstName.trim() || !lastName.trim()) {
      toast.showError('First name and last name are required.');
      return;
    }

    try {
      await updatePersonal.mutateAsync({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        dob: dob.trim() || undefined,
        phone: phone.trim() || undefined,
        email: email.trim(),
        address: address.trim() || undefined,
        city: city.trim() || undefined,
        state: state.trim() || undefined,
        zipcode: zipcode.trim() || undefined,
      });
      toast.showSuccess('Personal information updated');
      navigation.goBack();
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to update personal information');
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
              Edit Personal Information
            </Text>
            <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#777a84] dark:text-[#a1a1aa]')}>
              Update personal details to keep the case record accurate and up to date.
            </Text>

            <View className="mt-6 gap-5">
              <Input label="First Name *" value={firstName} onChangeText={setFirstName} />
              <Input label="Last Name *" value={lastName} onChangeText={setLastName} />
              <Input
                autoCapitalize="none"
                label="Date of Birth"
                placeholder="YYYY-MM-DD"
                value={dob}
                onChangeText={setDob}
              />
              <Input
                keyboardType="phone-pad"
                label="Phone"
                value={phone}
                onChangeText={setPhone}
              />
              <Input
                autoCapitalize="none"
                keyboardType="email-address"
                label="Email Address"
                value={email}
                onChangeText={setEmail}
              />
              <Input label="Address" value={address} onChangeText={setAddress} />
              <Input label="City" value={city} onChangeText={setCity} />
              <Input
                autoCapitalize="characters"
                label="State"
                value={state}
                onChangeText={setState}
              />
              <Input
                keyboardType="number-pad"
                label="ZIP Code"
                value={zipcode}
                onChangeText={setZipcode}
              />
            </View>

            <Button
              className="mt-8"
              label="Save"
              loading={updatePersonal.isPending}
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
    </View>
  );
}
