import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { ContactsApi } from '@/shared/api/endpoints/Contacts';
import type { ContactQueryParams, CreateContactRequest } from '@/shared/api/endpoints/Contacts';

export const contactKeys = {
  all: ['contacts'] as const,
  list: (params: ContactQueryParams) => [...contactKeys.all, 'list', params] as const,
  detail: (id: string) => [...contactKeys.all, 'detail', id] as const,
};

export function useContacts(params: ContactQueryParams, enabled = true) {
  return useQuery({
    enabled,
    queryKey: contactKeys.list(params),
    queryFn: () => ContactsApi.list(params),
  });
}

export function useContact(id?: string) {
  return useQuery({
    queryKey: contactKeys.detail(id ?? ''),
    queryFn: () => ContactsApi.get(id!),
    enabled: Boolean(id),
  });
}

function useRefreshContacts() {
  const queryClient = useQueryClient();
  return async (id?: string) => {
    await queryClient.invalidateQueries({ queryKey: contactKeys.all });
    if (id) await queryClient.invalidateQueries({ queryKey: contactKeys.detail(id) });
  };
}

export function useCreateContact() {
  const refresh = useRefreshContacts();
  return useMutation({ mutationFn: ContactsApi.create, onSuccess: () => refresh() });
}

export function useUpdateContact(id: string) {
  const refresh = useRefreshContacts();
  return useMutation({
    mutationFn: (body: CreateContactRequest) => ContactsApi.update(id, body),
    onSuccess: () => refresh(id),
  });
}

export function useDeactivateContact(id: string) {
  const refresh = useRefreshContacts();
  return useMutation({
    mutationFn: () => ContactsApi.deactivate(id),
    onSuccess: () => refresh(id),
  });
}

export function useExportContacts() {
  return useMutation({ mutationFn: (contactType?: string) => ContactsApi.exportCsv(contactType) });
}
