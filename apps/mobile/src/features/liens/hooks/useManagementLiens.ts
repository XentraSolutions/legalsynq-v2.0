import { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { useCases } from '@/features/cases/hooks';
import {
  buildLienFilterOptions,
  filterManagementLiens,
  mapLienToForm,
  mapManagementLiens,
} from '@/features/liens/mappers/lienManagementMappers';
import { LienExportService } from '@/features/liens/services/lienExportService';
import type {
  LienEditSection,
  LienFormValues,
  LienManagementFilters,
} from '@/features/liens/types/types';
import { LiensApi } from '@/shared/api/endpoints/Liens';
import type { LienDocumentType, LienExportFilter } from '@/shared/api/endpoints/Liens';
import { DocumentsApi } from '@/shared/api/endpoints/Documents';

export const managementLienKeys = {
  all: ['management-liens'] as const,
  list: () => [...managementLienKeys.all, 'list'] as const,
  facilities: () => [...managementLienKeys.all, 'facilities'] as const,
  documentTypes: () => [...managementLienKeys.all, 'document-types'] as const,
  detail: (id: string) => [...managementLienKeys.all, 'detail', id] as const,
};

export class LienRelatedSaveError extends Error {
  lienId: string;

  constructor(lienId: string, cause: unknown) {
    super(
      cause instanceof Error
        ? `The lien was created, but related information could not be saved: ${cause.message}`
        : 'The lien was created, but related information could not be saved.'
    );
    this.name = 'LienRelatedSaveError';
    this.lienId = lienId;
  }
}

export function useManagementLiens(search: string, filters: LienManagementFilters) {
  const casesQuery = useCases();
  const liensQuery = useQuery({
    queryKey: managementLienKeys.list(),
    queryFn: () => LiensApi.listAllManagementLiens(),
  });
  const facilitiesQuery = useQuery({
    queryKey: managementLienKeys.facilities(),
    queryFn: () => LiensApi.listFacilities(),
    staleTime: 5 * 60 * 1000,
  });

  const allLiens = useMemo(
    () => mapManagementLiens(liensQuery.data ?? [], casesQuery.cases, facilitiesQuery.data ?? []),
    [casesQuery.cases, facilitiesQuery.data, liensQuery.data]
  );
  const liens = useMemo(
    () => filterManagementLiens(allLiens, search, filters),
    [allLiens, filters, search]
  );

  return {
    ...liensQuery,
    isLoading: liensQuery.isLoading,
    isRefetching:
      liensQuery.isRefetching || casesQuery.isRefetching || facilitiesQuery.isRefetching,
    isError: liensQuery.isError,
    error: liensQuery.error,
    liens,
    totalCount: allLiens.length,
    filterOptions: buildLienFilterOptions(allLiens),
    refetchAll: async () => {
      await Promise.all([liensQuery.refetch(), casesQuery.refetch(), facilitiesQuery.refetch()]);
    },
  };
}

export function useManagementLienDetail(lienId: string) {
  return useQuery({
    queryKey: managementLienKeys.detail(lienId),
    queryFn: async () => {
      const [lien, details, documents] = await Promise.all([
        LiensApi.getManagementLien(lienId),
        LiensApi.getManagementLienDetails(lienId),
        DocumentsApi.listDocuments({
          productId: 'SYNQ_LIENS',
          referenceId: lienId,
          referenceType: 'LIEN',
          limit: 200,
        }),
      ]);
      const documentList = [
        ...details.documentList,
        ...documents.data
          .filter((document) => !details.documentList.some((item) => item.id === document.id))
          .map((document) => ({
            id: document.id,
            liensId: document.referenceId,
            filename: document.title,
            typeId: document.documentTypeId,
            url: '',
            status: document.scanStatus,
          })),
      ];
      const combinedDetails = { ...details, documentList };
      return { lien, details: combinedDetails, formValues: mapLienToForm(lien, combinedDetails) };
    },
    enabled: Boolean(lienId),
  });
}

export interface LienDocumentUploadInput {
  tenantId: string;
  documentType: LienDocumentType;
  file: {
    uri: string;
    name: string;
    mimeType?: string | null;
  };
}

export function useUploadLienDocument(lienId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ tenantId, documentType, file }: LienDocumentUploadInput) => {
      const formData = new FormData();
      formData.append('file', {
        uri: file.uri,
        name: file.name,
        type: file.mimeType || 'application/octet-stream',
      } as unknown as Blob);
      formData.append('tenantId', tenantId);
      formData.append('productId', 'SYNQ_LIENS');
      formData.append('referenceId', lienId);
      formData.append('referenceType', 'LIEN');
      formData.append('documentTypeId', documentType.id);
      formData.append('title', file.name);
      formData.append(
        'description',
        documentType.description || `${documentType.name} supporting the lien`
      );
      return DocumentsApi.uploadDocument(formData);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: managementLienKeys.detail(lienId) });
    },
  });
}

function numberValue(value: string): number {
  const parsed = Number(value.replace(/[^0-9.-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function apiDate(value: string): string | undefined {
  const trimmed = value.trim();
  if (!trimmed) return undefined;
  const parts = trimmed.split('/');
  if (parts.length === 3) {
    const [month, day, year] = parts;
    return `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
  }
  return trimmed.slice(0, 10);
}

function legacyDate(value: string): string | undefined {
  const normalized = apiDate(value);
  if (!normalized) return undefined;
  const [year, month, day] = normalized.split('-');
  return year && month && day ? `${month}/${day}/${year}` : value;
}

async function saveCompanyInfo(lienId: string, values: LienFormValues, update: boolean) {
  const medicalBody = {
    id: lienId,
    caseId: values.caseId || undefined,
    status: values.status,
    purchaseDate: legacyDate(values.purchaseDate),
    initialServiceDate: values.initialServiceDate || undefined,
    endServiceDate: values.endServiceDate || undefined,
    note: values.notes || undefined,
    isBulk: String(values.isBulk),
    isServicing: String(values.isServicing),
    fundingCompanyId: values.fundingCompanyId || undefined,
  };
  await (update
    ? LiensApi.updateMedicalInfo(medicalBody)
    : LiensApi.createMedicalInfo(medicalBody));
}

async function saveProviderInfo(lienId: string, values: LienFormValues, update: boolean) {
  if (values.facilityId) {
    const facilityBody = {
      liensId: lienId,
      facilityId: values.facilityId,
      facilityContactId: values.facilityContactId || undefined,
      email: values.facilityEmail || undefined,
      phone: values.facilityPhone || undefined,
      medicalProviderId: values.medicalProviderId || undefined,
    };
    await (update
      ? LiensApi.updateFacilityInfo(facilityBody)
      : LiensApi.createFacilityInfo(facilityBody));
  }
}

async function saveMedicalCodes(lienId: string, values: LienFormValues) {
  for (const id of values.deletedMedicalCodeIds) {
    await LiensApi.deleteMedicalCode(id);
  }
  for (const code of values.medicalCodes.filter((item) => item.code.trim())) {
    const body = {
      id: code.id,
      liensId: lienId,
      code: code.code.trim(),
      medicareCost: code.medicalCost || undefined,
      billingAmount: code.billingAmount || undefined,
      purchaseAmount: code.purchaseAmount || undefined,
      payee: values.payee || code.payee || undefined,
      outboundCheckNumber:
        values.outboundCheckNumber || code.outboundCheckNumber || undefined,
    };
    await (code.id ? LiensApi.updateMedicalCode(body) : LiensApi.createMedicalCode(body));
  }
}

async function saveRelatedLienData(lienId: string, values: LienFormValues, update: boolean) {
  await saveCompanyInfo(lienId, values, update);
  await saveProviderInfo(lienId, values, update);
  await saveMedicalCodes(lienId, values);
}

function baseUpdate(values: LienFormValues) {
  return {
    externalReference: values.fundingCompanyId || undefined,
    lienType: 'MedicalLien',
    caseId: values.caseId || undefined,
    facilityId: values.facilityId || undefined,
    originalAmount: numberValue(values.originalAmount),
    jurisdiction: values.jurisdiction || undefined,
    isConfidential: false,
    subjectFirstName: values.subjectFirstName || undefined,
    subjectLastName: values.subjectLastName || undefined,
    incidentDate: apiDate(values.purchaseDate),
    description: values.notes || undefined,
  };
}

export function useCreateManagementLien() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: LienFormValues) => {
      const created = await LiensApi.createManagementLien({
        lienNumber: values.lienNumber.trim(),
        externalReference: values.fundingCompanyId || undefined,
        lienType: 'MedicalLien',
        caseId: values.caseId || undefined,
        facilityId: values.facilityId || undefined,
        originalAmount: numberValue(values.originalAmount),
        jurisdiction: values.jurisdiction || undefined,
        isConfidential: false,
        subjectFirstName: values.subjectFirstName || undefined,
        subjectLastName: values.subjectLastName || undefined,
        incidentDate: apiDate(values.purchaseDate),
        description: values.notes || undefined,
      });
      try {
        await saveRelatedLienData(created.id, values, false);
      } catch (error) {
        throw new LienRelatedSaveError(created.id, error);
      }
      return created;
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managementLienKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ]);
    },
  });
}

export function useUpdateManagementLien(lienId: string, section?: LienEditSection) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: LienFormValues) => {
      if (section === 'medicalCodes') {
        await saveMedicalCodes(lienId, values);
        return;
      }

      const updated = await LiensApi.updateManagementLien(lienId, baseUpdate(values));
      if (section === 'company') {
        await saveCompanyInfo(lienId, values, true);
      } else if (section === 'provider') {
        await saveProviderInfo(lienId, values, true);
      } else {
        await saveRelatedLienData(lienId, values, true);
      }
      return updated;
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: managementLienKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['dashboard'] }),
      ]);
    },
  });
}

export function useExportLiens() {
  return useMutation({
    mutationFn: async (filters: LienExportFilter) => {
      const file = await LiensApi.exportLiens(filters);
      await LienExportService.share(file);
    },
  });
}
