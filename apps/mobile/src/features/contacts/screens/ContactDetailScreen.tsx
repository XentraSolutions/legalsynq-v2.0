import { useMemo, useState } from 'react';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp, RouteProp } from '@react-navigation/native';

import { useContact, useDeactivateContact } from '../hooks';
import { useCases } from '@/features/cases/hooks';
import type { CaseListItem } from '@/features/cases/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { Button, Card, EmptyState, Header, Spinner } from '@/shared/components';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';

type DetailTab = 'overview' | 'cases' | 'activities' | 'legalContacts';
const TABS: Array<{ key: DetailTab; label: string }> = [
  { key: 'overview', label: 'Overview' },
  { key: 'cases', label: 'Cases' },
  { key: 'activities', label: 'Activities' },
  { key: 'legalContacts', label: 'Legal Contacts' },
];
const PAGE_SIZE = 5;

function normalize(value?: string | null) {
  return value?.trim().toLowerCase() ?? '';
}

export function ContactDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<RouteProp<MainStackParamList, 'ContactDetail'>>();
  const toast = useToast();
  const query = useContact(route.params.contactId);
  const casesQuery = useCases();
  const deactivate = useDeactivateContact(route.params.contactId);
  const [tab, setTab] = useState<DetailTab>('overview');
  const [casePage, setCasePage] = useState(1);
  const [manageVisible, setManageVisible] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const contact = query.data;

  const linkedCases = useMemo(() => {
    if (!contact) return [];
    const names = [contact.organization, contact.displayName].map(normalize).filter(Boolean);
    return casesQuery.cases.filter((item) => {
      if (contact.contactType === 'LawFirm') {
        return item.lawFirmId === contact.id || names.includes(normalize(item.lawFirm));
      }
      if (contact.contactType === 'CaseManager') {
        return item.caseManagerId === contact.id || names.includes(normalize(item.caseManager));
      }
      return false;
    });
  }, [casesQuery.cases, contact]);
  const activeCases = linkedCases.filter((item) => !/closed|settled|complete/i.test(item.status));

  async function remove() {
    try {
      await deactivate.mutateAsync();
      toast.showSuccess('Contact deleted successfully');
      setConfirmDelete(false);
      navigation.navigate('Contacts');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to delete contact');
    }
  }

  return (
    <View className="flex-1 bg-[#fafafa] dark:bg-[#050506]">
      <Header
        showBack
        title={contact?.organization || contact?.displayName || 'Contact Details'}
        subtitle={contact ? `Contact ID: ${contact.id.slice(0, 8)}` : undefined}
        rightAction={
          contact ? (
            <Pressable
              accessibilityLabel="Manage contact"
              className="h-9 w-9 items-center justify-center rounded-full bg-white shadow-sm dark:bg-[#191a1f]"
              onPress={() => setManageVisible(true)}
            >
              <Ionicons color="#71717a" name="ellipsis-vertical" size={20} />
            </Pressable>
          ) : null
        }
        onBack={() => navigation.goBack()}
      />
      {query.isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Spinner />
        </View>
      ) : !contact ? (
        <EmptyState
          title="Contact unavailable"
          description="This contact could not be loaded."
          actionLabel="Go Back"
          icon={<Ionicons color="#ee7132" name="person-outline" size={58} />}
          onAction={() => navigation.goBack()}
        />
      ) : (
        <>
          <View className="h-12 shrink-0 border-b border-[#dedee0]">
            <ScrollView
              horizontal
              contentContainerClassName="h-12 items-stretch"
              showsHorizontalScrollIndicator={false}
              style={{ flexGrow: 0, height: 48 }}
            >
              {TABS.map((item) => (
                <Pressable
                  key={item.key}
                  className={cx(
                    'h-12 justify-center border-b-2 px-4 pb-2 pt-2',
                    tab === item.key ? 'border-[#ee7132]' : 'border-transparent'
                  )}
                  onPress={() => {
                    setTab(item.key);
                    setCasePage(1);
                  }}
                >
                  <Text
                    className={cx(
                      FIGMA_TEXT.body,
                      tab === item.key ? 'text-[#18181b] dark:text-white' : 'text-[#71717a]'
                    )}
                  >
                    {item.label}
                  </Text>
                </Pressable>
              ))}
            </ScrollView>
          </View>
          <ScrollView contentContainerClassName="gap-5 px-6 pb-10 pt-6">
            {tab === 'overview' ? (
              <Overview
                contact={contact}
                cases={linkedCases}
                activeCases={activeCases}
                page={casePage}
                onPage={setCasePage}
                onOpenCase={(caseId) => navigation.navigate('CaseDetail', { caseId })}
              />
            ) : null}
            {tab === 'cases' ? (
              <CasesCard
                title="Cases"
                cases={linkedCases}
                page={casePage}
                onPage={setCasePage}
                onOpenCase={(caseId) => navigation.navigate('CaseDetail', { caseId })}
              />
            ) : null}
            {tab === 'activities' ? (
              <EmptyPanel
                icon="time-outline"
                title="No activities yet"
                description="Contact activity will appear here as changes are recorded."
              />
            ) : null}
            {tab === 'legalContacts' ? (
              <Card className="rounded-[20px] p-6">
                <SectionTitle title="Legal Contacts" />
                <InfoRow label="Contact Name" value={contact.displayName} />
                <InfoRow label="Job Title" value={contact.title || '—'} />
                <InfoRow label="Email Address" value={contact.email || '—'} />
                <InfoRow label="Phone Number" value={contact.phone || '—'} />
              </Card>
            ) : null}
          </ScrollView>
        </>
      )}
      <Modal
        transparent
        animationType="fade"
        visible={manageVisible}
        onRequestClose={() => setManageVisible(false)}
      >
        <View className="flex-1 justify-end bg-black/25 p-4">
          <View className="relative rounded-[24px] bg-white p-6 shadow-lg dark:bg-[#191a1f]">
            <Pressable
              accessibilityLabel="Close manage contact"
              className="absolute right-3 top-4 h-6 w-6 items-center justify-center rounded-xl bg-[#ebebec] dark:bg-[#2a2b30]"
              onPress={() => setManageVisible(false)}
            >
              <Ionicons color="#71717a" name="close" size={16} />
            </Pressable>
            <Text className="pr-8 font-jakarta-medium text-[16px] leading-6 text-[#18181b] dark:text-white">
              Manage Contact
            </Text>
            <Text className={cx(FIGMA_TEXT.body, 'mt-3 pr-4 text-[#71717a]')}>
              Select an action to manage{' '}
              <Text className="font-jakarta-medium text-[#18181b] dark:text-white">
                {contact?.organization || contact?.displayName}
              </Text>
              .
            </Text>
            <View className="mt-5">
              <ManageRow
                icon="swap-horizontal-outline"
                label="Re-assign Case"
                onPress={() => {
                  setManageVisible(false);
                  if (contact?.id)
                    navigation.navigate('ReassignContactCases', { contactId: contact.id });
                }}
              />
              <ManageRow
                icon="create-outline"
                label="Edit"
                onPress={() => {
                  setManageVisible(false);
                  navigation.navigate('ContactForm', { contactId: contact?.id });
                }}
              />
              <ManageRow
                danger
                last
                icon="trash-outline"
                label="Delete"
                onPress={() => {
                  setManageVisible(false);
                  setConfirmDelete(true);
                }}
              />
            </View>
            <Button
              className="mt-5"
              label="Cancel"
              variant="secondary"
              onPress={() => {
                setManageVisible(false);
              }}
            />
          </View>
        </View>
      </Modal>
      <Modal
        transparent
        animationType="fade"
        visible={confirmDelete}
        onRequestClose={() => setConfirmDelete(false)}
      >
        <View className="flex-1 justify-end bg-black/25 p-4">
          <View className="rounded-[24px] bg-white p-6 dark:bg-[#191a1f]">
            <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec]">
              <Ionicons color="#18181b" name="trash-outline" size={18} />
            </View>
            <Text className="mt-3 font-jakarta-semibold text-[16px] text-[#18181b] dark:text-white">
              Delete Contact?
            </Text>
            <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-[#71717a]')}>
              Are you sure you want to delete {contact?.displayName}? This removes the contact from
              active lists.
            </Text>
            <Button
              className="mt-5"
              label="Yes, Delete"
              loading={deactivate.isPending}
              variant="danger"
              onPress={remove}
            />
            <Button
              className="mt-3"
              label="Cancel"
              variant="secondary"
              onPress={() => setConfirmDelete(false)}
            />
          </View>
        </View>
      </Modal>
    </View>
  );
}

function ManageRow({
  danger,
  icon,
  label,
  last,
  onPress,
}: {
  danger?: boolean;
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  last?: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable
      accessibilityLabel={label}
      className={cx('h-12 flex-row items-center border-b border-[#e4e4e7]', last && 'border-b-0')}
      onPress={onPress}
    >
      <Ionicons color={danger ? '#ff383c' : '#18181b'} name={icon} size={17} />
      <Text
        className={cx(
          FIGMA_TEXT.body,
          'ml-2 flex-1',
          danger ? 'text-[#ff383c]' : 'text-[#18181b] dark:text-white'
        )}
      >
        {label}
      </Text>
      <Ionicons color="#71717a" name="chevron-forward" size={20} />
    </Pressable>
  );
}

function Overview({
  contact,
  cases,
  activeCases,
  page,
  onPage,
  onOpenCase,
}: {
  contact: NonNullable<ReturnType<typeof useContact>['data']>;
  cases: CaseListItem[];
  activeCases: CaseListItem[];
  page: number;
  onPage: (page: number) => void;
  onOpenCase: (id: string) => void;
}) {
  return (
    <>
      <Card className="rounded-[20px] p-6">
        <SectionTitle title="Statistics" />
        <View className="mt-5 flex-row gap-3">
          <Stat label="Total Cases" value={String(cases.length)} />
          <Stat label="Active Cases" value={String(activeCases.length)} />
        </View>
        <View className="mt-3 rounded-[14px] border border-[#dedee0] p-4">
          <Text className={cx(FIGMA_TEXT.body, 'text-[#858892]')}>Active Cases Total Billing</Text>
          <Text className="mt-2 font-jakarta-semibold text-[16px] text-[#18181b] dark:text-white">
            —
          </Text>
        </View>
      </Card>
      <Card className="rounded-[20px] p-6">
        <SectionTitle title="Contact Information" />
        <InfoRow
          label="Contact Type"
          value={contact.contactType.replace(/([a-z])([A-Z])/g, '$1 $2')}
        />
        <InfoRow label="Contact Name" value={contact.organization || contact.displayName} />
        <InfoRow label="Email Address" value={contact.email || '—'} />
        <InfoRow label="Phone Number" value={contact.phone || '—'} />
        <InfoRow label="Address" value={contact.addressLine1 || '—'} />
        <InfoRow label="City" value={contact.city || '—'} />
        <InfoRow label="State" value={contact.state || '—'} />
        <InfoRow label="ZIP Code" value={contact.postalCode || '—'} />
      </Card>
      <CasesCard
        title="Recent Cases"
        cases={cases}
        page={page}
        onPage={onPage}
        onOpenCase={onOpenCase}
      />
    </>
  );
}

function SectionTitle({ title }: { title: string }) {
  return (
    <View className="flex-row items-center gap-3">
      <Ionicons color="#71717a" name="chevron-down" size={20} />
      <Text className="font-jakarta-semibold text-[16px] text-[#18181b] dark:text-white">
        {title}
      </Text>
    </View>
  );
}
function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View className="flex-1 rounded-[14px] border border-[#dedee0] p-4">
      <Text className={cx(FIGMA_TEXT.body, 'text-[#858892]')}>{label}</Text>
      <Text className="mt-2 font-jakarta-semibold text-[16px] text-[#18181b] dark:text-white">
        {value}
      </Text>
    </View>
  );
}
function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <View className="flex-row items-start border-b border-[#e4e4e7] py-4">
      <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#858892]')}>{label}</Text>
      <Text
        className={cx(FIGMA_TEXT.body, 'max-w-[58%] text-right text-[#18181b] dark:text-white')}
      >
        {value}
      </Text>
    </View>
  );
}

function CasesCard({
  title,
  cases,
  page,
  onPage,
  onOpenCase,
}: {
  title: string;
  cases: CaseListItem[];
  page: number;
  onPage: (page: number) => void;
  onOpenCase: (id: string) => void;
}) {
  const pages = Math.max(1, Math.ceil(cases.length / PAGE_SIZE));
  const rows = cases.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  return (
    <Card className="rounded-[20px] p-6">
      <SectionTitle title={title} />
      {rows.length === 0 ? (
        <Text className={cx(FIGMA_TEXT.body, 'py-8 text-center text-[#858892]')}>
          No linked cases yet.
        </Text>
      ) : (
        rows.map((item) => (
          <Pressable
            key={item.id}
            className="border-b border-[#e4e4e7] py-4"
            onPress={() => onOpenCase(item.id)}
          >
            <View className="flex-row items-center">
              <Text className="flex-1 font-jakarta-semibold text-[15px] text-[#18181b] dark:text-white">
                {item.clientName}
              </Text>
              <StatusBadge status={item.status} />
            </View>
            <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#858892]')}>
              Case ID: {item.caseNumber}
            </Text>
          </Pressable>
        ))
      )}
      {cases.length > 0 ? (
        <View className="mt-5 flex-row items-center">
          <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#858892]')}>
            {Math.min(page * PAGE_SIZE, cases.length)} of {cases.length} entries
          </Text>
          <PageButton
            disabled={page === 1}
            label="Previous"
            onPress={() => onPage(Math.max(1, page - 1))}
          />
          <View className="w-2" />
          <PageButton
            disabled={page === pages}
            label="Next"
            onPress={() => onPage(Math.min(pages, page + 1))}
          />
        </View>
      ) : null}
    </Card>
  );
}
function StatusBadge({ status }: { status: string }) {
  return (
    <View className="rounded-full bg-[#dcfce7] px-3 py-1">
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#2b7744]')}>
        {status.replace(/([a-z])([A-Z])/g, '$1 $2')}
      </Text>
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
      accessibilityLabel={`${label} cases`}
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
function EmptyPanel({
  icon,
  title,
  description,
}: {
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  description: string;
}) {
  return (
    <View className="items-center py-20">
      <Ionicons color="#ee7132" name={icon} size={54} />
      <Text className="mt-4 font-jakarta-semibold text-[18px] text-[#18181b] dark:text-white">
        {title}
      </Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-2 text-center text-[#858892]')}>{description}</Text>
    </View>
  );
}
