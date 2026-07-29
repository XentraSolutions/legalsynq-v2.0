import { apiClient } from '@/shared/api/client';

import type {
  Contact,
  ContactListResult,
  ContactQueryParams,
  CreateContactRequest,
  UpdateContactRequest,
} from './types';

const BASE_PATH = '/liens/api/liens/contacts';

const TYPE_PATHS = {
  LawFirm: 'law-firms',
  Provider: 'providers',
  LienHolder: 'lien-holders',
  Lead: 'leads',
  CaseManager: 'case-managers',
} as const;

export const ContactsApi = {
  async list(params: ContactQueryParams = {}): Promise<ContactListResult> {
    const response = await apiClient.get<ContactListResult>(BASE_PATH, { params });
    return response.data;
  },

  async get(id: string): Promise<Contact> {
    const response = await apiClient.get<Contact>(`${BASE_PATH}/${id}`);
    return response.data;
  },

  async create(body: CreateContactRequest): Promise<Contact> {
    const response = await apiClient.post<Contact>(BASE_PATH, body);
    return response.data;
  },

  async update(id: string, body: UpdateContactRequest): Promise<Contact> {
    const response = await apiClient.put<Contact>(`${BASE_PATH}/${id}`, body);
    return response.data;
  },

  async deactivate(id: string): Promise<Contact> {
    const response = await apiClient.put<Contact>(`${BASE_PATH}/${id}/deactivate`);
    return response.data;
  },

  async reactivate(id: string): Promise<Contact> {
    const response = await apiClient.put<Contact>(`${BASE_PATH}/${id}/reactivate`);
    return response.data;
  },

  async listByType(type: keyof typeof TYPE_PATHS): Promise<Contact[]> {
    const response = await apiClient.get<Contact[]>(`${BASE_PATH}/${TYPE_PATHS[type]}`);
    return response.data;
  },
};
