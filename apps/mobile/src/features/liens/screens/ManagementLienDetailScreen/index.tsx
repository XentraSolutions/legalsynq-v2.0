import type { ReactNode } from 'react';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as DocumentPicker from 'expo-document-picker';
import { useNavigation, useRoute } from '@react-navigation/native';
import type { NavigationProp } from '@react-navigation/native';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { useQuery } from '@tanstack/react-query';

import { LienConfirmationModal, LienDocumentTypeModal } from '@/features/liens/components';
import {
  managementLienKeys,
  useManagementLienDetail,
  useUploadLienDocument,
} from '@/features/liens/hooks';
import type { MainStackParamList } from '@/navigation/types/navigation';
import { ContactsApi } from '@/shared/api/endpoints/Contacts';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import type { LienDocumentType } from '@/shared/api/endpoints/Liens';
import { Button } from '@/shared/components/Button';
import { EmptyState } from '@/shared/components/EmptyState';
import { Header } from '@/shared/components/Header';
import { Spinner } from '@/shared/components/Spinner';
import { useAuth, useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT } from '@/shared/styles';
import { formatCurrency } from '@/shared/utils';

function value(value: string | number | null | undefined): string {
  if (value === null || value === undefined || value === '') return '—';
  return String(value);
}

function DetailRow({ label, value: detail }: { label: string; value?: string | number | null }) {
  return (
    <View className="flex-row gap-4 border-b border-[#ececef] py-3 dark:border-[#292a2f]">
      <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#71717a] dark:text-[#a1a1aa]')}>
        {label}
      </Text>
      <Text className={cx(FIGMA_TEXT.bodyStrong, 'max-w-[58%] text-right text-[#202228] dark:text-white')}>
        {value(detail)}
      </Text>
    </View>
  );
}

function CollapsibleSection({
  children,
  editable = false,
  title,
  onEdit,
}: {
  children: ReactNode;
  editable?: boolean;
  title: string;
  onEdit?: () => void;
}) {
  const [expanded, setExpanded] = useState(true);

  return (
    <View className="mt-5 rounded-[20px] bg-white px-5 py-5 shadow-sm dark:bg-[#191a1f]">
      <View className="flex-row items-start gap-3">
        <Pressable
          accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title}`}
          accessibilityRole="button"
          accessibilityState={{ expanded }}
          className="mt-0.5 h-6 w-4 items-center justify-center"
          hitSlop={10}
          onPress={() => setExpanded((current) => !current)}
        >
          <Ionicons color="#777984" name={expanded ? 'chevron-down' : 'chevron-forward'} size={20} />
        </Pressable>
        <Pressable
          accessibilityLabel={`${expanded ? 'Collapse' : 'Expand'} ${title}`}
          accessibilityRole="button"
          accessibilityState={{ expanded }}
          className="flex-1"
          onPress={() => setExpanded((current) => !current)}
        >
          <Text className={cx(FIGMA_TEXT.cardTitle, 'text-[#202228] dark:text-white')}>
            {title}
          </Text>
        </Pressable>
        {editable ? (
          <Pressable
            accessibilityLabel={`Edit ${title}`}
            accessibilityRole="button"
            className="h-9 flex-row items-center gap-1.5 rounded-full border border-[#dedee0] px-3 dark:border-[#34353b]"
            onPress={onEdit}
          >
            <Ionicons color="#202228" name="create-outline" size={17} />
            <Text className={cx(FIGMA_TEXT.body, 'text-[#202228] dark:text-white')}>Edit</Text>
          </Pressable>
        ) : null}
      </View>
      {expanded ? <View className="mt-4">{children}</View> : null}
    </View>
  );
}

export function ManagementLienDetailScreen() {
  const navigation = useNavigation<NavigationProp<MainStackParamList>>();
  const route = useRoute<NativeStackScreenProps<MainStackParamList, 'ManagementLienDetail'>['route']>();
  const { user } = useAuth();
  const toast = useToast();
  const query = useManagementLienDetail(route.params.lienId);
  const uploadDocument = useUploadLienDocument(route.params.lienId);
  const [documentTypeVisible, setDocumentTypeVisible] = useState(false);
  const [pendingDocument, setPendingDocument] = useState<{
    documentType: LienDocumentType;
    file: DocumentPicker.DocumentPickerAsset;
  } | null>(null);
  const facilitiesQuery = useQuery({
    queryKey: managementLienKeys.facilities(),
    queryFn: () => LiensApi.listFacilities(),
    staleTime: 5 * 60 * 1000,
  });
  const contactsQuery = useQuery({
    queryKey: [...managementLienKeys.all, 'detail-contacts'],
    queryFn: async () => {
      const [providers, fundingCompanies] = await Promise.all([
        ContactsApi.listByType('Provider'),
        ContactsApi.listByType('LienHolder'),
      ]);
      return { providers, fundingCompanies };
    },
    staleTime: 5 * 60 * 1000,
  });
  const documentTypesQuery = useQuery({
    queryKey: managementLienKeys.documentTypes(),
    queryFn: () => LiensApi.listDocumentTypes(),
    staleTime: 5 * 60 * 1000,
  });
  const facilityId = query.data?.formValues.facilityId ?? '';
  const facilityContactsQuery = useQuery({
    queryKey: [...managementLienKeys.facilities(), facilityId, 'contacts'],
    queryFn: () => LiensApi.listFacilityContacts(facilityId),
    enabled: Boolean(facilityId),
    staleTime: 5 * 60 * 1000,
  });

  if (query.isLoading) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Medical Lien Details" onBack={() => navigation.goBack()} />
        <View className="flex-1 items-center justify-center"><Spinner /></View>
      </View>
    );
  }

  if (query.isError || !query.data) {
    return (
      <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
        <Header showBack title="Medical Lien Details" onBack={() => navigation.goBack()} />
        <EmptyState
          actionLabel="Try Again"
          description={query.error instanceof Error ? query.error.message : 'The lien could not be loaded.'}
          icon={<Ionicons color="#f97332" name="alert-circle-outline" size={58} />}
          title="Unable to load lien"
          onAction={() => void query.refetch()}
        />
      </View>
    );
  }

  const { lien, details, formValues } = query.data;
  const facilityName = facilitiesQuery.data?.find((item) => item.id === formValues.facilityId)?.name;
  const facilityContact = facilityContactsQuery.data?.find(
    (item) => item.id === formValues.facilityContactId
  );
  const facilityContactName = facilityContact
    ? [facilityContact.firstName, facilityContact.lastName].filter(Boolean).join(' ')
    : '';
  const providerName = contactsQuery.data?.providers.find(
    (item) => item.id === formValues.medicalProviderId
  )?.displayName;
  const fundingCompanyName = contactsQuery.data?.fundingCompanies.find(
    (item) => item.id === formValues.fundingCompanyId
  )?.displayName;
  const totalMedical = details.codeList.reduce((sum, code) => sum + Number(code.medicareCost || 0), 0);
  const totalBilling = details.codeList.reduce((sum, code) => sum + Number(code.billingAmount || 0), 0);
  const totalPurchase = details.codeList.reduce((sum, code) => sum + Number(code.purchaseAmount || 0), 0);
  const editLien = (section: 'company' | 'provider' | 'medicalCodes') => () =>
    navigation.navigate('EditLien', { lienId: lien.id, section });

  async function selectDocumentType(documentTypeId: string) {
    const documentType = documentTypesQuery.data?.find((item) => item.id === documentTypeId);
    if (!documentType) {
      toast.showError('Select a valid document type.');
      return;
    }

    try {
      const result = await DocumentPicker.getDocumentAsync({
        copyToCacheDirectory: true,
        multiple: false,
      });
      if (!result.canceled && result.assets[0]) {
        setDocumentTypeVisible(false);
        setPendingDocument({ documentType, file: result.assets[0] });
      }
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to select the document.');
    }
  }

  async function confirmDocumentUpload() {
    if (!pendingDocument) return;
    if (!user?.tenantId) {
      toast.showError('A tenant is required to upload this document.');
      return;
    }

    try {
      await uploadDocument.mutateAsync({
        tenantId: user.tenantId,
        documentType: pendingDocument.documentType,
        file: pendingDocument.file,
      });
      setPendingDocument(null);
      toast.showSuccess('Document uploaded successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to upload the document.');
    }
  }

  return (
    <View className="flex-1 bg-[#f7f7f8] dark:bg-[#050506]">
      <Header showBack title="Medical Lien Details" onBack={() => navigation.goBack()} />
      <ScrollView className="flex-1 px-5" contentContainerClassName="pb-10">
        <CollapsibleSection
          editable
          title="Medical Lien & Funding Company Information"
          onEdit={editLien('company')}
        >
          <DetailRow label="Lien Status" value={formValues.status} />
          <DetailRow label="Purchase Date" value={formValues.purchaseDate} />
          <DetailRow label="Initial Service Date" value={formValues.initialServiceDate} />
          <DetailRow label="End Service Date" value={formValues.endServiceDate} />
          <DetailRow label="Funding Company" value={fundingCompanyName || formValues.fundingCompanyId} />
          <DetailRow label="Notes" value={formValues.notes} />
        </CollapsibleSection>

        <CollapsibleSection
          editable
          title="Medical Facility and Provider Information"
          onEdit={editLien('provider')}
        >
          <DetailRow label="Facility Name" value={facilityName || formValues.facilityId} />
          <DetailRow label="Contact Person" value={facilityContactName || formValues.facilityContactId} />
          <DetailRow label="Email Address" value={formValues.facilityEmail} />
          <DetailRow label="Provider Name" value={providerName || formValues.medicalProviderId} />
        </CollapsibleSection>

        <CollapsibleSection editable title="Medical Code Information" onEdit={editLien('medicalCodes')}>
          {details.codeList.length ? details.codeList.map((code) => (
            <View className="border-b border-[#ececef] py-4 dark:border-[#292a2f]" key={code.id}>
              <Text className={cx(FIGMA_TEXT.bodyStrong, 'text-[#202228] dark:text-white')}>{value(code.code)}</Text>
              <DetailRow label="Medical Care Cost" value={formatCurrency(Number(code.medicareCost || 0))} />
              <DetailRow label="Billing Amount" value={formatCurrency(Number(code.billingAmount || 0))} />
              <DetailRow label="Purchase Amount" value={formatCurrency(Number(code.purchaseAmount || 0))} />
            </View>
          )) : <Text className={cx(FIGMA_TEXT.body, 'py-3 text-[#71717a] dark:text-[#a1a1aa]')}>No medical codes added.</Text>}
          <DetailRow label="Total Medical Cost" value={formatCurrency(totalMedical)} />
          <DetailRow label="Total Billing Amount" value={formatCurrency(totalBilling)} />
          <DetailRow label="Total Purchase Amount" value={formatCurrency(totalPurchase || lien.purchasePrice || 0)} />
        </CollapsibleSection>

        <CollapsibleSection title="Uploaded Documents">
          {details.documentList.length ? details.documentList.map((document) => (
            <View className="flex-row items-center gap-3 border-b border-[#ececef] py-3 dark:border-[#292a2f]" key={document.id}>
              <Ionicons color="#ee7132" name="document-text-outline" size={22} />
              <Text className={cx(FIGMA_TEXT.body, 'flex-1 text-[#202228] dark:text-white')} numberOfLines={2}>
                {document.filename}
              </Text>
            </View>
          )) : <Text className={cx(FIGMA_TEXT.body, 'py-3 text-[#71717a] dark:text-[#a1a1aa]')}>No documents uploaded.</Text>}
          <Button
            className="mt-4 w-full"
            label="Upload More"
            leftIcon={<Ionicons color="#555964" name="cloud-upload-outline" size={16} />}
            loading={documentTypesQuery.isLoading}
            variant="secondary"
            onPress={() => setDocumentTypeVisible(true)}
          />
        </CollapsibleSection>
      </ScrollView>

      <LienDocumentTypeModal
        error={documentTypesQuery.isError}
        isLoading={documentTypesQuery.isLoading}
        options={documentTypesQuery.data ?? []}
        visible={documentTypeVisible}
        onAddNew={() =>
          toast.showError('Adding new document types is not available in the mobile app yet.')
        }
        onClose={() => setDocumentTypeVisible(false)}
        onSelect={(documentType) => void selectDocumentType(documentType.id)}
      />
      <LienConfirmationModal
        confirmLabel="Yes, Upload Document"
        description={
          pendingDocument
            ? `Upload ${pendingDocument.file.name} as ${pendingDocument.documentType.name}?`
            : ''
        }
        loading={uploadDocument.isPending}
        title="Upload Document?"
        visible={Boolean(pendingDocument)}
        onCancel={() => setPendingDocument(null)}
        onConfirm={() => void confirmDocumentUpload()}
      />
    </View>
  );
}
