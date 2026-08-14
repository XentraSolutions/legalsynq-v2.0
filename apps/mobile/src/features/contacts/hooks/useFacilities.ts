import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FacilitiesApi } from '@/shared/api/endpoints/Facilities';
import type { FacilityQueryParams, FacilityRequest } from '@/shared/api/endpoints/Facilities';

export const facilityKeys = {
  all: ['facilities'] as const,
  list: (params: FacilityQueryParams) => ['facilities', 'list', params] as const,
  detail: (id: string) => ['facilities', 'detail', id] as const,
  staff: (id: string) => ['facilities', 'staff', id] as const,
};
export function useFacilities(params: FacilityQueryParams, enabled = true) {
  return useQuery({
    enabled,
    queryKey: facilityKeys.list(params),
    queryFn: () => FacilitiesApi.list(params),
  });
}
export function useFacility(id?: string) {
  return useQuery({
    queryKey: facilityKeys.detail(id ?? ''),
    queryFn: () => FacilitiesApi.get(id!),
    enabled: Boolean(id),
  });
}
export function useFacilityStaff(id: string) {
  return useQuery({
    queryKey: facilityKeys.staff(id),
    queryFn: () => FacilitiesApi.listStaff(id),
    enabled: Boolean(id),
  });
}
export function useCreateFacility() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: FacilityRequest) => FacilitiesApi.create(body),
    onSuccess: () => client.invalidateQueries({ queryKey: facilityKeys.all }),
  });
}
export function useUpdateFacility(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: FacilityRequest) => FacilitiesApi.update(id, body),
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: facilityKeys.all });
      await client.invalidateQueries({ queryKey: facilityKeys.detail(id) });
    },
  });
}
export function useDeactivateFacility(id: string) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: () => FacilitiesApi.deactivate(id),
    onSuccess: () => client.invalidateQueries({ queryKey: facilityKeys.all }),
  });
}
