import { useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useQuery } from '@tanstack/react-query';

import { useCases } from '@/features/cases/hooks';
import { LienConfirmationModal } from '@/features/liens/components';
import {
  managementLienKeys,
  LienRelatedSaveError,
  useCreateManagementLien,
  useManagementLienDetail,
  useUpdateManagementLien,
} from '@/features/liens/hooks';
import type {
  LienEditSection,
  LienFormValues,
  LienMedicalCodeFormValue,
} from '@/features/liens/types/types';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { ContactsApi } from '@/shared/api/endpoints/Contacts';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import { Button } from '@/shared/components/Button';
import { Checkbox } from '@/shared/components/Checkbox';
import { Header } from '@/shared/components/Header';
import { Input } from '@/shared/components/Input';
import { SelectOptionModal, type SelectOptionItem } from '@/shared/components/SelectOptionModal';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency } from '@/shared/utils';

const EMPTY_CODE: LienMedicalCodeFormValue = {
  code: '',
  medicalCost: '',
  billingAmount: '',
  purchaseAmount: '',
  payee: '',
  outboundCheckNumber: '',
};

const EMPTY_FORM: LienFormValues = {
  lienNumber: '',
  caseId: '',
  status: 'Open',
  purchaseDate: '',
  initialServiceDate: '',
  endServiceDate: '',
  notes: '',
  isBulk: false,
  isServicing: false,
  fundingCompanyId: '',
  facilityId: '',
  facilityContactId: '',
  facilityEmail: '',
  facilityPhone: '',
  medicalProviderId: '',
  originalAmount: '',
  jurisdiction: '',
  subjectFirstName: '',
  subjectLastName: '',
  medicalCodes: [],
  deletedMedicalCodeIds: [],
  payee: '',
  outboundCheckNumber: '',
};

export function CreateLienScreen() {
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'CreateLien'>['route']>();
  return <LienForm mode="create" initialCaseId={route.params?.caseId} />;
}

export function EditLienScreen() {
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'EditLien'>['route']>();
  return <LienForm editSection={route.params.section} lienId={route.params.lienId} mode="edit" />;
}

function LienForm({
  editSection,
  initialCaseId,
  lienId = '',
  mode,
}: {
  editSection?: LienEditSection;
  initialCaseId?: string;
  lienId?: string;
  mode: 'create' | 'edit';
}) {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const toast = useToast();
  const detailQuery = useManagementLienDetail(lienId);
  const createLien = useCreateManagementLien();
  const updateLien = useUpdateManagementLien(lienId, editSection);
  const casesQuery = useCases();
  const [values, setValues] = useState<LienFormValues>({
    ...EMPTY_FORM,
    caseId: initialCaseId ?? '',
  });
  const [confirmVisible, setConfirmVisible] = useState(false);
  const [draftCode, setDraftCode] = useState<LienMedicalCodeFormValue>({ ...EMPTY_CODE });
  const [codesExpanded, setCodesExpanded] = useState(true);
  const facilitiesQuery = useQuery({
    queryKey: managementLienKeys.facilities(),
    queryFn: () => LiensApi.listFacilities(),
    staleTime: 5 * 60 * 1000,
  });
  const contactsQuery = useQuery({
    queryKey: [...managementLienKeys.all, 'form-contacts'],
    queryFn: async () => {
      const [providers, fundingCompanies] = await Promise.all([
        ContactsApi.listByType('Provider'),
        ContactsApi.listByType('LienHolder'),
      ]);
      return { providers, fundingCompanies };
    },
    staleTime: 5 * 60 * 1000,
  });
  const facilityContactsQuery = useQuery({
    queryKey: [...managementLienKeys.facilities(), values.facilityId, 'contacts'],
    queryFn: () => LiensApi.listFacilityContacts(values.facilityId),
    enabled: Boolean(values.facilityId),
    staleTime: 5 * 60 * 1000,
  });
  useEffect(() => {
    if (mode === 'edit' && detailQuery.data?.formValues) {
      setValues({
        ...detailQuery.data.formValues,
        medicalCodes: detailQuery.data.formValues.medicalCodes,
      });
    }
  }, [detailQuery.data, mode]);

  const isPending = createLien.isPending || updateLien.isPending;
  const showCompany = mode === 'create' || editSection === 'company';
  const showProvider = mode === 'create' || editSection === 'provider';
  const showMedicalCodes = mode === 'create' || editSection === 'medicalCodes';
  const cases = useMemo<SelectOptionItem[]>(
    () => casesQuery.cases.map((item) => ({ label: `${item.clientName} · ${item.caseNumber}`, value: item.id })),
    [casesQuery.cases]
  );
  const facilities = useMemo<SelectOptionItem[]>(
    () => (facilitiesQuery.data ?? []).map((item) => ({ label: item.name, value: item.id })),
    [facilitiesQuery.data]
  );
  const providers = useMemo<SelectOptionItem[]>(
    () => (contactsQuery.data?.providers ?? []).map((item) => ({ label: item.displayName, value: item.id })),
    [contactsQuery.data]
  );
  const fundingCompanies = useMemo<SelectOptionItem[]>(
    () => (contactsQuery.data?.fundingCompanies ?? []).map((item) => ({ label: item.displayName || item.organization || item.id, value: item.id })),
    [contactsQuery.data]
  );
  const facilityContacts = useMemo<SelectOptionItem[]>(
    () =>
      (facilityContactsQuery.data ?? []).map((item) => ({
        label: [item.firstName, item.lastName].filter(Boolean).join(' ') || item.id,
        value: item.id,
      })),
    [facilityContactsQuery.data]
  );

  function setField<K extends keyof LienFormValues>(key: K, value: LienFormValues[K]) {
    setValues((current) => ({ ...current, [key]: value }));
  }

  function addMedicalCode() {
    if (!draftCode.code.trim()) {
      toast.showError('Medical code and description are required.');
      return;
    }
    setField('medicalCodes', [...values.medicalCodes, { ...draftCode }]);
    setDraftCode({ ...EMPTY_CODE });
  }

  function removeMedicalCode(index: number) {
    const code = values.medicalCodes[index];
    setValues((current) => ({
      ...current,
      medicalCodes: current.medicalCodes.filter((_, codeIndex) => codeIndex !== index),
      deletedMedicalCodeIds: code?.id
        ? [...current.deletedMedicalCodeIds, code.id]
        : current.deletedMedicalCodeIds,
    }));
  }

  function requestSave() {
    if (mode === 'create' && !values.caseId) {
      toast.showError('Select the case associated with this lien.');
      return;
    }
    if ((mode === 'create' || editSection === 'company') && !values.purchaseDate) {
      toast.showError('Purchase date is required.');
      return;
    }
    if ((mode === 'create' || editSection === 'company') && !values.initialServiceDate) {
      toast.showError('Initial date is required.');
      return;
    }
    if (editSection === 'provider' && !values.facilityId) {
      toast.showError('Facility name is required.');
      return;
    }
    setConfirmVisible(true);
  }

  async function confirmSave() {
    try {
      if (mode === 'create') {
        const created = await createLien.mutateAsync(values);
        setConfirmVisible(false);
        toast.showSuccess(`Lien ${created.lienNumber || 'record'} created`);
        navigation.navigate('ManagementLienDetail', { lienId: created.id });
      } else {
        await updateLien.mutateAsync(values);
        setConfirmVisible(false);
        toast.showSuccess('Lien updated successfully');
        navigation.goBack();
      }
    } catch (error) {
      if (error instanceof LienRelatedSaveError) {
        setConfirmVisible(false);
        toast.showError(error.message);
        navigation.navigate('EditLien', { lienId: error.lienId, section: 'company' });
        return;
      }
      toast.showError(error instanceof Error ? error.message : `Unable to ${mode} the lien`);
    }
  }

  if (mode === 'edit' && detailQuery.isLoading) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Edit Lien" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center"><Spinner /></View>
      </View>
    );
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title={mode === 'create' ? 'Create Lien' : 'Edit Lien'} onBack={() => navigation.goBack()} />
      <ScrollView
        className="flex-1 px-5"
        contentContainerClassName="gap-4 pb-10"
        keyboardShouldPersistTaps="handled"
      >
        {showCompany ? (
          <>
            <SectionHeading
              description="Provide the basic details for the medical lien."
              title="Medical Lien & Funding Company Information"
            />
            {mode === 'create' ? (
              <>
                <SelectField label="Associated Case *" options={cases} value={values.caseId} onSelect={(selected) => setField('caseId', selected)} />
                <Input label="Lien Number" placeholder="Generated automatically if blank" value={values.lienNumber} onChangeText={(text) => setField('lienNumber', text)} />
                <Input label="Original Amount" keyboardType="decimal-pad" placeholder="$ 0.00" value={values.originalAmount} onChangeText={(text) => setField('originalAmount', text)} />
                <Input label="Jurisdiction" value={values.jurisdiction} onChangeText={(text) => setField('jurisdiction', text)} />
              </>
            ) : null}
            <SelectField label="Lien Status *" options={['Open', 'Closed', 'Rejected'].map((item) => ({ label: item, value: item }))} value={values.status} onSelect={(selected) => setField('status', selected)} />
            <Input label="Purchase Date *" placeholder="YYYY-MM-DD" value={values.purchaseDate} onChangeText={(text) => setField('purchaseDate', text)} />
            <Input label="Initial Date *" placeholder="YYYY-MM-DD" value={values.initialServiceDate} onChangeText={(text) => setField('initialServiceDate', text)} />
            <Input label="End Service Date" placeholder="YYYY-MM-DD" value={values.endServiceDate} onChangeText={(text) => setField('endServiceDate', text)} />
            <Input label="Notes" multiline value={values.notes} onChangeText={(text) => setField('notes', text)} />
            <View className="flex-row gap-5 py-1">
              <Checkbox checked={values.isBulk} label="Bulk" onChange={(checked) => setField('isBulk', checked)} />
              <Checkbox checked={values.isServicing} label="Servicing" onChange={(checked) => setField('isServicing', checked)} />
            </View>
            <SelectField label="Funding Company" options={fundingCompanies} value={values.fundingCompanyId} onSelect={(selected) => setField('fundingCompanyId', selected)} />
          </>
        ) : null}

        {showProvider ? (
          <>
            <SectionHeading
              description="Provide the required medical facility and provider details."
              title="Medical Facility and Provider Information"
            />
            <SelectField label="Facility Name *" options={facilities} value={values.facilityId} onSelect={(selected) => setField('facilityId', selected)} />
            <SelectField label="Contact Person" options={facilityContacts} value={values.facilityContactId} onSelect={(selected) => setField('facilityContactId', selected)} />
            <Input autoCapitalize="none" keyboardType="email-address" label="Email Address" value={values.facilityEmail} onChangeText={(text) => setField('facilityEmail', text)} />
            {mode === 'create' ? <Input keyboardType="phone-pad" label="Phone" value={values.facilityPhone} onChangeText={(text) => setField('facilityPhone', text)} /> : null}
            <SelectField label="Medical Provider Name" options={providers} value={values.medicalProviderId} onSelect={(selected) => setField('medicalProviderId', selected)} />
          </>
        ) : null}

        {showMedicalCodes ? (
          <MedicalCodeEditor
            codes={values.medicalCodes}
            draft={draftCode}
            expanded={codesExpanded}
            outboundCheckNumber={values.outboundCheckNumber}
            payee={values.payee}
            onAdd={addMedicalCode}
            onDraftChange={(patch) => setDraftCode((current) => ({ ...current, ...patch }))}
            onRemove={removeMedicalCode}
            onToggle={() => setCodesExpanded((current) => !current)}
            onOutboundCheckChange={(text) => setField('outboundCheckNumber', text)}
            onPayeeChange={(text) => setField('payee', text)}
          />
        ) : null}
        <Button className="mt-3" label="Save" loading={isPending} onPress={requestSave} />
        <Button disabled={isPending} label="Cancel" variant="secondary" onPress={() => navigation.goBack()} />
      </ScrollView>

      <LienConfirmationModal
        confirmLabel={mode === 'create' ? 'Yes, Create Lien' : 'Yes, Save Changes'}
        description={`Are you sure you want to ${mode === 'create' ? 'create this lien' : 'save these lien changes'}?`}
        loading={isPending}
        title={mode === 'create' ? 'Create Lien?' : 'Update Lien?'}
        visible={confirmVisible}
        onCancel={() => setConfirmVisible(false)}
        onConfirm={() => void confirmSave()}
      />
    </View>
  );
}

function amount(value: string): number {
  const parsed = Number(value.replace(/[^0-9.-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function MedicalCodeEditor({
  codes,
  draft,
  expanded,
  outboundCheckNumber,
  payee,
  onAdd,
  onDraftChange,
  onRemove,
  onToggle,
  onOutboundCheckChange,
  onPayeeChange,
}: {
  codes: LienMedicalCodeFormValue[];
  draft: LienMedicalCodeFormValue;
  expanded: boolean;
  outboundCheckNumber: string;
  payee: string;
  onAdd: () => void;
  onDraftChange: (patch: Partial<LienMedicalCodeFormValue>) => void;
  onRemove: (index: number) => void;
  onToggle: () => void;
  onOutboundCheckChange: (value: string) => void;
  onPayeeChange: (value: string) => void;
}) {
  const totals = codes.reduce(
    (current, code) => ({
      medical: current.medical + amount(code.medicalCost),
      billing: current.billing + amount(code.billingAmount),
      purchase: current.purchase + amount(code.purchaseAmount),
    }),
    { medical: 0, billing: 0, purchase: 0 }
  );

  return (
    <>
      <SectionHeading
        description="Provide the required medical code information associated with this lien."
        title="Medical Code Information"
      />
      <Input
        label="Medical Code & Description *"
        placeholder="Select medical code & description"
        value={draft.code}
        onChangeText={(text) => onDraftChange({ code: text })}
      />
      <Input
        keyboardType="decimal-pad"
        label="Medical Cost"
        placeholder="$ 0.00"
        value={draft.medicalCost}
        onChangeText={(text) => onDraftChange({ medicalCost: text })}
      />
      <Input
        keyboardType="decimal-pad"
        label="Billing Amount *"
        placeholder="$ 0.00"
        value={draft.billingAmount}
        onChangeText={(text) => onDraftChange({ billingAmount: text })}
      />
      <Input
        keyboardType="decimal-pad"
        label="Purchase Amount *"
        placeholder="$ 0.00"
        value={draft.purchaseAmount}
        onChangeText={(text) => onDraftChange({ purchaseAmount: text })}
      />
      <Button className="self-end" label="+ Add" size="sm" variant="secondary" onPress={onAdd} />

      <View className="rounded-[18px] bg-white p-4 dark:bg-[#191a1f]">
        <Pressable
          accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} added cost and billing details`}
          accessibilityRole="button"
          className="flex-row items-center gap-2"
          onPress={onToggle}
        >
          <Ionicons color="#71717a" name={expanded ? 'chevron-down' : 'chevron-forward'} size={16} />
          <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>
            Added Cost & Billing Details
          </Text>
        </Pressable>

        {expanded ? (
          <View className="mt-3 gap-3">
            {codes.length ? (
              codes.map((code, index) => (
                <View className="border-b border-[#e4e4e7] pb-3 dark:border-[#303138]" key={code.id ?? `new-${index}`}>
                  <View className="flex-row items-center gap-3">
                    <Text className={cx(FIGMA_TEXT.bodyStrong, 'flex-1 text-[#202228] dark:text-white')}>
                      {code.code}
                    </Text>
                    <Pressable
                      accessibilityLabel={`Remove medical code ${code.code}`}
                      accessibilityRole="button"
                      className="h-8 w-8 items-center justify-center rounded-full bg-[#ededee] dark:bg-[#2a2b30]"
                      onPress={() => onRemove(index)}
                    >
                      <Ionicons color="#71717a" name="trash-outline" size={16} />
                    </Pressable>
                  </View>
                  <MedicalCodeAmount label="Medical Care Cost" value={amount(code.medicalCost)} />
                  <MedicalCodeAmount label="Billing Amount" value={amount(code.billingAmount)} />
                  <MedicalCodeAmount label="Purchase Amount" value={amount(code.purchaseAmount)} />
                </View>
              ))
            ) : (
              <Text className={cx(FIGMA_TEXT.body, 'py-2 text-[#71717a] dark:text-[#a1a1aa]')}>
                No medical codes added.
              </Text>
            )}

            <View className="rounded-xl border border-[#dedee0] dark:border-[#34353b]">
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'px-3 py-2 text-[#202228] dark:text-white')}>
                Total Summary
              </Text>
              <MedicalCodeAmount label="Medical Care Cost" value={totals.medical} />
              <MedicalCodeAmount label="Billing Amount" value={totals.billing} />
              <MedicalCodeAmount label="Purchase Amount" value={totals.purchase} />
            </View>
          </View>
        ) : null}
      </View>

      <Input label="Payee" value={payee} onChangeText={onPayeeChange} />
      <Input label="Outbound Check" value={outboundCheckNumber} onChangeText={onOutboundCheckChange} />
    </>
  );
}

function MedicalCodeAmount({ label, value }: { label: string; value: number }) {
  return (
    <View className="mt-2 flex-row gap-3 px-3">
      <Text className={cx(FIGMA_TEXT.formLabel, 'flex-1 text-[#858892] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text className={cx(FIGMA_TEXT.formLabel, 'text-[#202228] dark:text-white')}>
        {formatCurrency(value)}
      </Text>
    </View>
  );
}

function SectionHeading({ description, title }: { description: string; title: string }) {
  return (
    <View className="mt-4">
      <Text className={cx(FIGMA_TEXT.sectionTitle, 'text-[#202228] dark:text-white')}>{title}</Text>
      <Text className={cx(FIGMA_TEXT.body, 'mt-1 text-[#71717a] dark:text-[#a1a1aa]')}>{description}</Text>
    </View>
  );
}

function SelectField({
  label,
  options,
  value,
  onSelect,
}: {
  label: string;
  options: SelectOptionItem[];
  value: string;
  onSelect: (value: string) => void;
}) {
  const [visible, setVisible] = useState(false);
  const selected = options.find((option) => option.value === value);
  return (
    <View>
      <Text className={cx(FIGMA_TEXT.formLabel, 'mb-1.5 text-[#6f737d] dark:text-[#a1a1aa]')}>{label}</Text>
      <Pressable
        accessibilityRole="button"
        className="h-[52px] flex-row items-center rounded-[14px] border border-border bg-white px-4 dark:border-[#303138] dark:bg-[#191a1f]"
        onPress={() => setVisible(true)}
      >
        <Text className={cx(FIGMA_TEXT.input, 'flex-1 text-[#202228] dark:text-white')} numberOfLines={1}>
          {selected?.label || value || 'Select an option'}
        </Text>
        <Ionicons color="#71717a" name="chevron-down" size={18} />
      </Pressable>
      <SelectOptionModal
        options={options}
        selectedLabel={selected?.label}
        selectedValue={value}
        title={label.replace(' *', '')}
        visible={visible}
        onClose={() => setVisible(false)}
        onSelect={(option) => {
          onSelect(option.value);
          setVisible(false);
        }}
      />
    </View>
  );
}
