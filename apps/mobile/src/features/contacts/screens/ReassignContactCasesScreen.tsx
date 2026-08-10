import { useMemo, useState } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useMutation } from '@tanstack/react-query';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';

import { useContact, useContacts } from '../hooks';
import { useCases } from '@/features/cases/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { CasesApi } from '@/shared/api/endpoints/Cases';
import type { Contact } from '@/shared/api/endpoints/Contacts';
import { Button, Input, Spinner } from '@/shared/components';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

const TYPE_CONFIG: Record<string, { code: string; singular: string; title: string }> = {
  LawFirm: { code: '1', singular: 'law firm', title: 'Law Firm' },
  Provider: { code: '2', singular: 'medical provider', title: 'Medical Provider' },
  LienHolder: { code: '3', singular: 'funding company', title: 'Funding Company' },
  Lead: { code: '5', singular: 'lead', title: 'Lead' },
};

function normalize(value?: string | null) {
  return value?.trim().toLowerCase() ?? '';
}

export function ReassignContactCasesScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'ReassignContactCases'>>();
  const toast = useToast();
  const contactQuery = useContact(route.params.contactId);
  const casesQuery = useCases();
  const contact = contactQuery.data;
  const config = contact ? TYPE_CONFIG[contact.contactType] : undefined;
  const targetsQuery = useContacts(
    { contactType: contact?.contactType, isActive: true, page: 1, pageSize: 1000 },
    Boolean(contact && config)
  );
  const [selected, setSelected] = useState<Contact>();
  const [selectorVisible, setSelectorVisible] = useState(false);
  const [search, setSearch] = useState('');
  const reassign = useMutation({
    mutationFn: () =>
      CasesApi.batchReassignContact({
        contactType: config!.code,
        oldId: contact!.id,
        newId: selected!.id,
      }),
  });
  const sourceName = contact?.organization || contact?.displayName || 'this contact';
  const caseCount = useMemo(() => {
    if (!contact) return 0;
    const names = [contact.organization, contact.displayName].map(normalize).filter(Boolean);
    return casesQuery.cases.filter((item) =>
      contact.contactType === 'LawFirm'
        ? item.lawFirmId === contact.id || names.includes(normalize(item.lawFirm))
        : contact.contactType === 'CaseManager'
          ? item.caseManagerId === contact.id || names.includes(normalize(item.caseManager))
          : false
    ).length;
  }, [casesQuery.cases, contact]);
  const targets = (targetsQuery.data?.items ?? []).filter((item) => item.id !== contact?.id);
  const filteredTargets = targets.filter((item) =>
    [item.organization, item.displayName, item.email]
      .join(' ')
      .toLowerCase()
      .includes(search.trim().toLowerCase())
  );

  async function assign() {
    try {
      const result = await reassign.mutateAsync();
      if (!result.isSuccess) throw new Error(result.message || 'Unable to re-assign cases');
      toast.showSuccess('Cases re-assigned successfully');
      navigation.navigate('Contacts');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to re-assign cases');
    }
  }

  if (contactQuery.isLoading)
    return (
      <View className="flex-1 items-center justify-center bg-[#fafafa]">
        <Spinner />
      </View>
    );
  return (
    <SafeAreaView edges={['top', 'bottom']} className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <View className="flex-1">
        <View className="px-6 pt-4">
          <Pressable
            accessibilityLabel="Go back"
            className="h-10 w-10 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
            onPress={() => navigation.goBack()}
          >
            <Ionicons color="#71717a" name="arrow-back" size={20} />
          </Pressable>
        </View>
        <View className="px-6 pt-7">
          <Text className="font-jakarta-bold text-[24px] leading-8 text-[#18181b] dark:text-white">
            Re-Assign Case
          </Text>
          <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#71717a]')}>
            You are reassigning {caseCount} cases from{' '}
            <Text className="font-jakarta-semibold text-[#18181b] dark:text-white">
              {sourceName}
            </Text>
            . Select the new {config?.singular ?? 'contact'} below that will receive these cases.
          </Text>
        </View>
        <View className="mt-6 px-6">
          <Text className={cx(FIGMA_TEXT.formLabel, 'mb-2 text-[#18181b] dark:text-white')}>
            {config?.title ?? 'Contact'} <Text className="text-[#ff383c]">*</Text>
          </Text>
          <Pressable
            accessibilityLabel={`Select ${config?.title ?? 'contact'}`}
            className="h-[52px] flex-row items-center rounded-[14px] border border-[#dedee0] bg-white px-4 dark:bg-[#191a1f]"
            onPress={() => setSelectorVisible(true)}
          >
            <Text
              className={cx(
                FIGMA_TEXT.input,
                selected ? 'flex-1 text-[#18181b] dark:text-white' : 'flex-1 text-[#94a3b8]'
              )}
            >
              {selected?.organization ||
                selected?.displayName ||
                `Select ${config?.singular ?? 'contact'}`}
            </Text>
            <Ionicons color="#71717a" name="chevron-down" size={18} />
          </Pressable>
          {!config ? (
            <Text className={cx(FIGMA_TEXT.formLabel, 'mt-2 text-[#ff383c]')}>
              This contact type does not support batch reassignment.
            </Text>
          ) : null}
        </View>
        <View className="mt-auto gap-3 px-6 pb-2">
          <Button
            label="Assign Case"
            disabled={!selected || !config}
            loading={reassign.isPending}
            onPress={assign}
          />
          <Button label="Cancel" variant="secondary" onPress={() => navigation.goBack()} />
        </View>
      </View>
      <Modal
        transparent
        animationType="fade"
        visible={selectorVisible}
        onRequestClose={() => setSelectorVisible(false)}
      >
        <View className="flex-1 justify-end bg-black/25 p-4">
          <View className="max-h-[72%] rounded-[24px] bg-white p-6 dark:bg-[#191a1f]">
            <Pressable
              accessibilityLabel="Close selector"
              className="absolute right-3 top-3 z-10 h-6 w-6 items-center justify-center rounded-full bg-[#ebebec]"
              onPress={() => setSelectorVisible(false)}
            >
              <Ionicons color="#71717a" name="close" size={16} />
            </Pressable>
            <Text className="font-jakarta-medium text-[16px] text-[#18181b] dark:text-white">
              Select {config?.title ?? 'Contact'}
            </Text>
            <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#71717a]')}>
              Choose the {config?.singular ?? 'contact'} that will receive these cases.
            </Text>
            <Input
              className="mt-5"
              leftIcon={<Ionicons color="#71717a" name="search-outline" size={18} />}
              placeholder="Search..."
              value={search}
              onChangeText={setSearch}
            />
            <ScrollView className="mt-3" showsVerticalScrollIndicator>
              {targetsQuery.isLoading ? (
                <View className="py-8">
                  <Spinner />
                </View>
              ) : (
                filteredTargets.map((item) => {
                  const checked = item.id === selected?.id;
                  return (
                    <Pressable
                      key={item.id}
                      className="h-12 flex-row items-center border-b border-[#e4e4e7]"
                      onPress={() => {
                        setSelected(item);
                        setSelectorVisible(false);
                      }}
                    >
                      <Text
                        className={cx(FIGMA_TEXT.body, 'flex-1 text-[#18181b] dark:text-white')}
                      >
                        {item.organization || item.displayName}
                      </Text>
                      {checked ? (
                        <View className="h-5 w-5 items-center justify-center rounded-full bg-[#ee7132]">
                          <Ionicons color="#fff" name="checkmark" size={14} />
                        </View>
                      ) : null}
                    </Pressable>
                  );
                })
              )}
            </ScrollView>
            <Button
              className="mt-4"
              label="Cancel"
              variant="secondary"
              onPress={() => setSelectorVisible(false)}
            />
          </View>
        </View>
      </Modal>
    </SafeAreaView>
  );
}
