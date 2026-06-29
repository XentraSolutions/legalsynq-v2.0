import { contactsApi } from "./contacts.api";
import {
  mapContactToListItem,
  mapContactToDetail,
  mapContactPagination,
} from "./contacts.mapper";
import type {
  ContactsQuery,
  ContactListItem,
  ContactDetail,
  ContactCaseSummary,
  PaginationMeta,
  CreateContactRequestDto,
  UpdateContactRequestDto,
  ExportResponse,
} from "./contacts.types";

export interface ContactListResult {
  items: ContactListItem[];
  pagination: PaginationMeta;
}

export const contactsService = {
  async getContacts(query: ContactsQuery = {}): Promise<ContactListResult> {
    const { data } = await contactsApi.list(query);
    return {
      items: data.items.map(mapContactToListItem),
      pagination: mapContactPagination(data),
    };
  },

  async getContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.getById(id);
    return mapContactToDetail(data);
  },

  async createContact(
    request: CreateContactRequestDto,
  ): Promise<ContactDetail> {
    const { data } = await contactsApi.create(request);
    return mapContactToDetail(data);
  },

  async updateContact(
    id: string,
    request: UpdateContactRequestDto,
  ): Promise<ContactDetail> {
    const { data } = await contactsApi.update(id, request);
    return mapContactToDetail(data);
  },

  async deactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.deactivate(id);
    return mapContactToDetail(data);
  },

  async reactivateContact(id: string): Promise<ContactDetail> {
    const { data } = await contactsApi.reactivate(id);
    return mapContactToDetail(data);
  },

  async deleteContact(id: string): Promise<unknown> {
    const { data } = await contactsApi.delete(id);
    return data;
  },

  async exportContacts(contactType: string): Promise<ExportResponse> {
    const { data } = await contactsApi.export(contactType);
    return data;
  },

  async getCasesByContact(contactId: string): Promise<ContactCaseSummary[]> {
    const { data } = await contactsApi.getCases(contactId);
    return Array.isArray(data) ? data : [];
  },
};
