import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Buffer } from 'buffer';
import { useNavigation } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';

import { useContacts, useExportContacts, useFacilities } from '../hooks';
import { shareContactsCsv } from '../services/contactExportService';
import type { MainStackParamList } from '@/navigation/types/navigation';
import type { Contact, ContactType } from '@/shared/api/endpoints/Contacts';
import type { Facility } from '@/shared/api/endpoints/Facilities';
import { FacilitiesApi } from '@/shared/api/endpoints/Facilities';
import {
  Card,
  EmptyState,
  Header,
  SearchBar,
  SelectOptionModal,
  Spinner,
} from '@/shared/components';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

type ContactTab = { key: string; kind: 'contact'; label: string; type: ContactType };
type FacilityTab = { key: string; kind: 'facility'; label: string };
type DirectoryTab = ContactTab | FacilityTab;
const TABS: DirectoryTab[] = [
  { key: 'law-firms', kind: 'contact', label: 'Law Firms', type: 'LawFirm' },
  { key: 'facilities', kind: 'facility', label: 'Medical Facilities' },
  { key: 'providers', kind: 'contact', label: 'Medical Providers', type: 'Provider' },
  { key: 'funding', kind: 'contact', label: 'Funding Companies', type: 'LienHolder' },
  { key: 'leads', kind: 'contact', label: 'Leads', type: 'Lead' },
];
const PAGE_SIZE = 5;

export function ContactsScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const toast = useToast();
  const [tab, setTab] = useState(TABS[0]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [filterVisible, setFilterVisible] = useState(false);
  const [statusFilter, setStatusFilter] = useState<'active' | 'inactive' | 'all'>('active');
  const isActive = statusFilter === 'all' ? undefined : statusFilter === 'active';
  const contactQuery = useContacts(
    {
      contactType: tab.kind === 'contact' ? tab.type : undefined,
      isActive,
      page,
      pageSize: PAGE_SIZE,
      search: search.trim() || undefined,
    },
    tab.kind === 'contact'
  );
  const facilityQuery = useFacilities(
    { isActive, page, pageSize: PAGE_SIZE, search: search.trim() || undefined },
    tab.kind === 'facility'
  );
  const query = tab.kind === 'facility' ? facilityQuery : contactQuery;
  const exportContacts = useExportContacts();
  const items = query.data?.items ?? [];
  const total = query.data?.totalCount ?? 0;
  const pageCount = Math.max(1, query.data?.totalPages ?? Math.ceil(total / PAGE_SIZE));

  async function exportCurrentType() {
    if (tab.kind === 'facility') {
      try {
        const facilities = await FacilitiesApi.list({ isActive, page: 1, pageSize: 10000 });
        const cell = (value: string) =>
          /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
        const csv = [
          ['Name', 'Email', 'Phone', 'Address', 'City', 'State', 'ZIP'],
          ...facilities.items.map((facility) => [
            facility.name,
            facility.email ?? '',
            facility.phone ?? '',
            facility.addressLine1 ?? '',
            facility.city ?? '',
            facility.state ?? '',
            facility.postalCode ?? '',
          ]),
        ]
          .map((row) => row.map((value) => cell(value)).join(','))
          .join('\n');
        await shareContactsCsv(Buffer.from(csv, 'utf8').toString('base64'), tab.label);
        toast.showSuccess('Medical facilities exported successfully');
      } catch (error) {
        toast.showError(error instanceof Error ? error.message : 'Unable to export facilities');
      }
      return;
    }
    try {
      const data = await exportContacts.mutateAsync(tab.type);
      await shareContactsCsv(data, tab.label);
      toast.showSuccess('Contacts exported successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to export contacts');
    }
  }

  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header
        title="Contacts"
        rightActionContainerClassName="w-auto"
        rightAction={
          <View className="flex-row gap-3">
            <RoundButton icon="share-outline" label="Export contacts" onPress={exportCurrentType} />
            <RoundButton
              accent
              icon="add"
              label={tab.kind === 'facility' ? 'Add medical facility' : 'Add contact'}
              onPress={() =>
                tab.kind === 'facility'
                  ? navigation.navigate('FacilityForm', {})
                  : navigation.navigate('ContactForm', { contactType: tab.type })
              }
            />
          </View>
        }
      />
      <View className="px-6 pb-3 pt-2">
        <View className="flex-row items-start">
          <Text className={cx(FIGMA_TEXT.sectionTitle, 'flex-1 text-[#18181b] dark:text-white')}>
            Contacts
          </Text>
        </View>
        <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#71717a]')}>
          View, manage, and keep your contact information up to date.
        </Text>
      </View>
      <View className="h-12 shrink-0 border-b border-[#dedee0]">
        <ScrollView
          horizontal
          className="shrink-0"
          contentContainerClassName="h-12 items-stretch px-3"
          showsHorizontalScrollIndicator={false}
          style={{ flexGrow: 0, height: 48 }}
        >
          {TABS.map((item) => (
            <Pressable
              key={item.key}
              className={cx(
                'h-12 justify-center border-b-2 px-3 pb-2 pt-2',
                tab.key === item.key ? 'border-[#ee7132]' : 'border-transparent'
              )}
              onPress={() => {
                setTab(item);
                setPage(1);
              }}
            >
              <Text
                className={cx(
                  FIGMA_TEXT.body,
                  tab.key === item.key ? 'text-[#18181b] dark:text-white' : 'text-[#71717a]'
                )}
              >
                {item.label}
              </Text>
            </Pressable>
          ))}
        </ScrollView>
      </View>
      <View className="flex-row items-center gap-3 px-6 py-4">
        <View className="flex-1">
          <SearchBar
            placeholder="Search..."
            value={search}
            onChangeText={(value) => {
              setSearch(value);
              setPage(1);
            }}
          />
        </View>
        <RoundButton
          icon="options-outline"
          label="Configure search"
          onPress={() => setFilterVisible(true)}
        />
      </View>
      {query.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      ) : query.isError ? (
        <EmptyState
          actionLabel="Try Again"
          description="The contacts list could not be loaded."
          icon={<Ionicons color="#ee7132" name="alert-circle-outline" size={58} />}
          title="Unable to load contacts"
          onAction={() => void query.refetch()}
        />
      ) : items.length === 0 ? (
        <EmptyState
          actionLabel={
            search ? 'Clear Search' : tab.kind === 'facility' ? 'Add Facility' : 'Add Contact'
          }
          description={
            search ? 'Try a different search.' : 'Add your first contact to get started.'
          }
          icon={<Ionicons color="#ee7132" name="people-outline" size={58} />}
          title={search ? 'No matching contacts' : `No ${tab.label.toLowerCase()} yet`}
          onAction={() =>
            search
              ? setSearch('')
              : tab.kind === 'facility'
                ? navigation.navigate('FacilityForm', {})
                : navigation.navigate('ContactForm', { contactType: tab.type })
          }
        />
      ) : (
        <ScrollView contentContainerClassName="gap-4 px-6 pb-8 pt-1">
          {tab.kind === 'facility'
            ? (items as Facility[]).map((item) => (
                <FacilityCard
                  key={item.id}
                  facility={item}
                  onPress={() => navigation.navigate('FacilityDetail', { facilityId: item.id })}
                />
              ))
            : (items as Contact[]).map((item) => (
                <ContactCard
                  key={item.id}
                  contact={item}
                  onPress={() => navigation.navigate('ContactDetail', { contactId: item.id })}
                />
              ))}
          <Pagination
            page={page}
            pageCount={pageCount}
            total={total}
            visible={Math.min(page * PAGE_SIZE, total)}
            onNext={() => setPage((current) => Math.min(pageCount, current + 1))}
            onPrevious={() => setPage((current) => Math.max(1, current - 1))}
          />
        </ScrollView>
      )}
      <SelectOptionModal
        options={[
          { label: 'Active', value: 'active' },
          { label: 'Inactive', value: 'inactive' },
          { label: 'All contacts', value: 'all' },
        ]}
        selectedValue={statusFilter}
        title="Contact Status"
        visible={filterVisible}
        onClose={() => setFilterVisible(false)}
        onSelect={(option) => {
          setStatusFilter(option.value as 'active' | 'inactive' | 'all');
          setPage(1);
          setFilterVisible(false);
        }}
      />
    </View>
  );
}

function FacilityCard({ facility, onPress }: { facility: Facility; onPress: () => void }) {
  return (
    <Pressable accessibilityLabel={`View ${facility.name}`} onPress={onPress}>
      <Card className="rounded-[20px] p-6">
        <View className="flex-row items-start">
          <Text className="flex-1 font-jakarta-semibold text-[16px] leading-6 text-[#18181b] dark:text-white">
            {facility.name}
          </Text>
          <Ionicons color="#71717a" name="ellipsis-horizontal" size={20} />
        </View>
        {facility.email ? <Info icon="mail-outline" value={facility.email} /> : null}
        {facility.phone ? <Info icon="call-outline" value={facility.phone} /> : null}
        {facility.addressLine1 || facility.city ? (
          <Info
            icon="location-outline"
            value={[facility.addressLine1, facility.city, facility.state]
              .filter(Boolean)
              .join(', ')}
          />
        ) : null}
      </Card>
    </Pressable>
  );
}

function ContactCard({ contact, onPress }: { contact: Contact; onPress: () => void }) {
  return (
    <Pressable accessibilityLabel={`View ${contact.displayName}`} onPress={onPress}>
      <Card className="rounded-[20px] p-6">
        <View className="flex-row items-start">
          <View className="flex-1">
            <Text className="font-jakarta-semibold text-[16px] leading-6 text-[#18181b] dark:text-white">
              {contact.organization || contact.displayName}
            </Text>
            {contact.organization ? (
              <Text className={cx(FIGMA_TEXT.body, 'text-[#71717a]')}>{contact.displayName}</Text>
            ) : null}
          </View>
          <Ionicons color="#71717a" name="ellipsis-horizontal" size={20} />
        </View>
        {contact.email ? <Info icon="mail-outline" value={contact.email} /> : null}
        {contact.phone ? <Info icon="call-outline" value={contact.phone} /> : null}
        {contact.addressLine1 || contact.city ? (
          <Info
            icon="location-outline"
            value={[contact.addressLine1, contact.city, contact.state].filter(Boolean).join(', ')}
          />
        ) : null}
      </Card>
    </Pressable>
  );
}

function Info({ icon, value }: { icon: keyof typeof Ionicons.glyphMap; value: string }) {
  return (
    <View className="mt-3 flex-row items-center gap-2">
      <Ionicons color="#8f929b" name={icon} size={16} />
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#71717a]')}>{value}</Text>
    </View>
  );
}

function RoundButton({
  accent,
  icon,
  label,
  onPress,
}: {
  accent?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={label}
      className={cx(
        'h-10 w-10 items-center justify-center rounded-full',
        accent ? 'bg-[#ee7132]' : 'bg-white dark:bg-[#191a1f]'
      )}
      onPress={onPress}
    >
      <Ionicons color={accent ? '#fff' : '#71717a'} name={icon} size={20} />
    </Pressable>
  );
}

function Pagination({
  page,
  pageCount,
  total,
  visible,
  onNext,
  onPrevious,
}: {
  page: number;
  pageCount: number;
  total: number;
  visible: number;
  onNext: () => void;
  onPrevious: () => void;
}) {
  return (
    <View className="mt-2 flex-row items-center">
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#71717a]')}>
        {visible} of {total} entries
      </Text>
      <PageButton disabled={page === 1} label="Previous" onPress={onPrevious} />
      <View className="w-2" />
      <PageButton disabled={page === pageCount} label="Next" onPress={onNext} />
    </View>
  );
}

function PageButton({
  disabled,
  label,
  onPress,
}: {
  disabled: boolean;
  label: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={`${label} page`}
      disabled={disabled}
      className={cx(
        'h-9 justify-center rounded-full border border-[#dedee0] px-3',
        disabled && 'opacity-40'
      )}
      onPress={onPress}
    >
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#18181b] dark:text-white')}>{label}</Text>
    </Pressable>
  );
}
