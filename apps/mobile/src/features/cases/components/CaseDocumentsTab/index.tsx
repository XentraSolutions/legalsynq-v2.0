import { useMemo, useState } from 'react';
import { Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as DocumentPicker from 'expo-document-picker';

import { CaseDetailTabPage } from '@/features/cases/components/CaseDetailTabPage';
import {
  useCaseDocuments,
  useCaseDocumentTypes,
  useUploadCaseDocument,
} from '@/features/cases/hooks';
import { LienDocumentTypeModal } from '@/features/liens/components';
import type { Document } from '@/shared/api/endpoints/Documents';
import type { LienDocumentType } from '@/shared/api/endpoints/Liens';
import { Button } from '@/shared/components/Button';
import { Spinner } from '@/shared/components/Spinner';
import { useToast } from '@/shared/hooks';
import { cx, FIGMA_TEXT, SHADOWS } from '@/shared/styles';
import { formatDisplayDate } from '@/shared/utils';

const MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024;
const ACCEPTED_EXTENSIONS = ['pdf', 'jpg', 'jpeg', 'png', 'docx', 'xlsx', 'xls', 'csv'];
const ACCEPTED_MIME_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-excel',
  'text/csv',
];

function fileExtension(filename: string): string {
  const extension = filename.split('.').pop()?.trim().toLowerCase();
  return extension && extension !== filename.toLowerCase() ? extension : 'file';
}

function uploadedDate(value: string): string {
  try {
    return formatDisplayDate(value, 'MM/dd/yyyy');
  } catch {
    return value;
  }
}

function DocumentRow({
  document,
  typeName,
  showDivider,
}: {
  document: Document;
  typeName: string;
  showDivider: boolean;
}) {
  return (
    <View
      className={cx(
        'flex-row items-center gap-3 py-4',
        showDivider ? 'border-b border-[#e4e4e7] dark:border-[#303138]' : ''
      )}
    >
      <View className="h-10 w-10 items-center justify-center rounded-md bg-[#f4f4f5] dark:bg-[#2a2b30]">
        <Text className="font-jakarta-medium text-[12px] uppercase leading-4 text-[#18181b] dark:text-white">
          {fileExtension(document.title).slice(0, 4)}
        </Text>
      </View>
      <View className="min-w-0 flex-1">
        <Text
          className="font-jakarta text-[14px] leading-5 text-[#18181b] dark:text-white"
          numberOfLines={1}
        >
          {document.title}
        </Text>
        <View className="mt-0.5 flex-row items-center">
          <Text
            className="max-w-[58%] font-jakarta text-[12px] leading-4 text-[#71717a] dark:text-[#a1a1aa]"
            numberOfLines={1}
          >
            {typeName}
          </Text>
          <View className="mx-1 h-[3px] w-[3px] rounded-full bg-[#a1a1aa]" />
          <Text className="font-jakarta text-[12px] leading-4 text-[#71717a] dark:text-[#a1a1aa]">
            {uploadedDate(document.createdAt)}
          </Text>
        </View>
      </View>
      <View className="h-8 w-5 items-center justify-center">
        <Ionicons color="#71717a" name="ellipsis-vertical" size={18} />
      </View>
    </View>
  );
}

export function CaseDocumentsTab({ caseId }: { caseId: string }) {
  const toast = useToast();
  const [typeSelectorVisible, setTypeSelectorVisible] = useState(false);
  const documentsQuery = useCaseDocuments(caseId);
  const documentTypesQuery = useCaseDocumentTypes(typeSelectorVisible || Boolean(documentsQuery.data?.data.length));
  const uploadDocument = useUploadCaseDocument(caseId);
  const typeNames = useMemo(
    () => new Map((documentTypesQuery.data ?? []).map((type) => [type.id, type.name])),
    [documentTypesQuery.data]
  );
  const documents = documentsQuery.data?.data ?? [];

  async function selectDocumentType(documentType: LienDocumentType) {
    try {
      const result = await DocumentPicker.getDocumentAsync({
        copyToCacheDirectory: true,
        multiple: false,
        type: ACCEPTED_MIME_TYPES,
      });
      if (result.canceled || !result.assets[0]) return;

      const file = result.assets[0];
      const extension = fileExtension(file.name);
      if (!ACCEPTED_EXTENSIONS.includes(extension)) {
        toast.showError('Choose a PDF, JPG, PNG, DOCX, XLSX, XLS, or CSV file.');
        return;
      }
      if (typeof file.size === 'number' && file.size > MAX_FILE_SIZE_BYTES) {
        toast.showError('The selected file must be 50 MB or smaller.');
        return;
      }

      setTypeSelectorVisible(false);
      await uploadDocument.mutateAsync({ documentType, file });
      toast.showSuccess('Document uploaded successfully');
    } catch (error) {
      toast.showError(error instanceof Error ? error.message : 'Unable to upload the document.');
    }
  }

  return (
    <>
      <CaseDetailTabPage testID="case-documents-page">
        <View
          className="rounded-[16px] bg-white p-4 dark:bg-[#191a1f]"
          style={SHADOWS.sm}
        >
          {documentsQuery.isLoading ? (
            <View className="items-center py-12">
              <Spinner />
            </View>
          ) : documentsQuery.isError ? (
            <View className="items-center px-4 py-8">
              <Ionicons color="#ee7132" name="alert-circle-outline" size={38} />
              <Text className={cx(FIGMA_TEXT.body, 'mt-3 text-center text-[#71717a] dark:text-[#a1a1aa]')}>
                Documents could not be loaded.
              </Text>
              <Button
                className="mt-5 w-full"
                label="Try Again"
                size="sm"
                variant="secondary"
                onPress={() => void documentsQuery.refetch()}
              />
            </View>
          ) : documents.length === 0 ? (
            <View className="items-center rounded-[12px] border border-dashed border-[#dedee0] px-4 py-6 dark:border-[#3f3f46]">
              <View className="h-10 w-10 items-center justify-center rounded-full bg-[#ebebec] dark:bg-[#2a2b30]">
                <Ionicons color="#18181b" name="cloud-upload-outline" size={19} />
              </View>
              <Text className="mt-6 font-jakarta-medium text-[14px] leading-5 text-[#18181b] dark:text-white">
                No Documents Yet
              </Text>
              <Text className="mt-1 text-center font-jakarta text-[12px] leading-4 text-[#71717a] dark:text-[#a1a1aa]">
                Accepted formats: .pdf, .jpg, .png, .docx, .xlsx, .xls, .csv. Max size: 50MB
              </Text>
              <Button
                className="mt-6 w-full"
                label="Choose File"
                loading={uploadDocument.isPending}
                size="sm"
                variant="secondary"
                onPress={() => setTypeSelectorVisible(true)}
              />
            </View>
          ) : (
            <>
              <View className="px-2">
                {documents.map((document, index) => (
                  <DocumentRow
                    document={document}
                    key={document.id}
                    showDivider={index < documents.length - 1}
                    typeName={typeNames.get(document.documentTypeId) ?? 'Document'}
                  />
                ))}
              </View>
              <Button
                className="mt-4 w-full"
                label="Upload More"
                leftIcon={<Ionicons color="#18181b" name="cloud-upload-outline" size={18} />}
                loading={uploadDocument.isPending}
                size="sm"
                variant="secondary"
                onPress={() => setTypeSelectorVisible(true)}
              />
            </>
          )}
        </View>
      </CaseDetailTabPage>

      <LienDocumentTypeModal
        error={documentTypesQuery.isError}
        isLoading={documentTypesQuery.isLoading}
        options={documentTypesQuery.data ?? []}
        visible={typeSelectorVisible}
        onAddNew={() => toast.showInfo('Adding document types is not available yet.')}
        onClose={() => setTypeSelectorVisible(false)}
        onSelect={(documentType) => void selectDocumentType(documentType)}
      />
    </>
  );
}
